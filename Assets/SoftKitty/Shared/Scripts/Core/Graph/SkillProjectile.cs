using System;
using System.Collections.Generic;
using SoftKitty.MasterCombatCore;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SoftKitty
{
    [Serializable]
    public sealed class SkillProjectile
    {
        public enum FlightPath { LaunchPositionToTargetPosition, FollowTarget }
        public enum HitRecipients { DesignatedTarget, FirstMatchingEntity }
        public HitRecipients hitRecipients;
        public List<string> hitTags = new List<string>();
        public GameObject prefab, trail, impact;
        public string launchBone, targetBone;
        public Vector3 launchOffset, impactOffset;
        public FlightPath path;
        public float speed = 15f, acceleration, trailSeconds, impactScale = 1f;
        public float trackingTimeout = 10f;
        public Vector3 hitBoxSize = new Vector3(1.3f, 1.3f, 1.3f), hitBoxOffset;
        public LayerMask hitLayers = ~0;

        public float hitBoxStart, hitBoxSeconds;

        private static Collider[] overlaps = new Collider[16];
        private static RaycastHit[] sweepHits = new RaycastHit[16];

        internal readonly struct HitQuery
        {
            internal readonly PhysicsScene physics;
            private readonly Entity caster, aimed;
            private readonly EntityComponent aimActor;
            private readonly List<string> allowedTags;
            private readonly HashSet<Entity> visited;
            internal readonly bool designated;

            internal HitQuery(SkillProjectile shot, Entity source, EntityComponent aimActor, PhysicsScene physics, HashSet<Entity> visited = null)
            {
                this.physics = physics;
                this.visited = visited;
                caster = source;
                this.aimActor = aimActor;
                aimed = aimActor ? aimActor.mData : null;
                designated = shot == null || shot.hitRecipients == HitRecipients.DesignatedTarget;
                allowedTags = shot?.hitTags;
            }

            internal bool AimIsLive => aimActor && aimActor.mData == aimed && IsLive(aimActor);

            internal EntityComponent Eligible(Collider collider)
            {
                if (!collider || !collider.TryGetComponent<DamageReciever>(out var receiver) ||
                    !receiver.isActiveAndEnabled || !receiver.Enable) return null;
                var actor = receiver.Entity;
                if (!actor || (designated && (actor != aimActor || actor.mData != aimed))) return null;
                var entity = actor.mData;
                if (entity == null || (visited != null && visited.Contains(entity))) return null;
                if (!designated && (entity == caster || allowedTags == null || !entity.hasMatchingTag(allowedTags))) return null;
                return IsLive(actor) ? actor : null;
            }
        }

        internal static bool IsLive(EntityComponent actor)
        {
            if (!actor || !actor.isActiveAndEnabled || !actor.AvailableForInteraction) return false;
            var entity = actor.mData;
            return !entity.Attributes.Exists(attribute => attribute.uid == "chp") || entity.GetAttributeFloat("chp") > 0;
        }

        internal static bool OverlapsTarget(EntityComponent target, Vector3 center, Quaternion facing, Vector3 size, int layers)
        {
            if (!target) return false;
            var query = new HitQuery(null, null, target, target.gameObject.scene.GetPhysicsScene());
            return Sweep(query, center, center, facing, size, layers, out _);
        }

        private static EntityComponent Sweep(HitQuery query, Vector3 from, Vector3 to, Quaternion facing, Vector3 size,
            int layers, out float fraction)
        {
            fraction = 0;
            if (!query.physics.IsValid() || size.x <= 0 || size.y <= 0 || size.z <= 0) return null;
            Physics.SyncTransforms();
            Vector3 half = size * .5f;
            int count;
            while ((count = query.physics.OverlapBox(from, half, overlaps, facing, layers, QueryTriggerInteraction.Collide)) == overlaps.Length)
                Array.Resize(ref overlaps, overlaps.Length * 2);
            EntityComponent first = null;
            // Initial overlaps all occur at t=0. Break the tie independently of PhysX result order.
            for (int i = 0; i < count; i++)
            {
                var actor = query.Eligible(overlaps[i]);
                if (actor && (!first || actor.GetInstanceID() < first.GetInstanceID())) first = actor;
                overlaps[i] = null;
            }
            if (first) return first;

            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < .00001f) return null;
            while ((count = query.physics.BoxCast(from, half, delta / distance, sweepHits, facing, distance, layers,
                QueryTriggerInteraction.Collide)) == sweepHits.Length) Array.Resize(ref sweepHits, sweepHits.Length * 2);
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = sweepHits[i];
                if (hit.distance > nearest) continue;
                var actor = query.Eligible(hit.collider);
                if (!actor || (hit.distance == nearest && first && first.GetInstanceID() <= actor.GetInstanceID())) continue;
                first = actor;
                nearest = hit.distance;
            }
            if (first) fraction = Mathf.Clamp01(nearest / distance);
            return first;
        }

        internal EntityComponent Contact(HitQuery query, Vector3 from, Vector3 to, Quaternion facing, float start, float end,
            out Vector3 point)
        {
            point = to;
            float opens = Mathf.Max(start, hitBoxStart);
            float closes = hitBoxSeconds > 0 ? Mathf.Min(end, hitBoxStart + hitBoxSeconds) : end;
            if (closes < opens) return null;
            float duration = end - start;
            var a = Vector3.Lerp(from, to, duration > 0 ? (opens - start) / duration : 0);
            var b = Vector3.Lerp(from, to, duration > 0 ? (closes - start) / duration : 1);
            Vector3 offset = facing * hitBoxOffset;
            var actor = Sweep(query, a + offset, b + offset, facing, hitBoxSize, hitLayers, out var fraction);
            if (!actor) return null;
            point = Vector3.Lerp(a, b, fraction);
            return actor;
        }

        public SkillProjectile Copy()
        {
            var copy = (SkillProjectile)MemberwiseClone();
            copy.hitTags = hitTags == null ? new List<string>() : new List<string>(hitTags);
            return copy;
        }

        public static bool IsLaunch(Action node) => node.ClassName == "Projectile" ||
            (node.ClassName == "OriginalSkillVisual" && node.IntValues.Count > 0 && node.IntValues[0] == 3);

        public static SkillProjectile For(Action node, GraphObject skill)
        {
            if (node.projectile != null) return node.projectile;
            if (node.ClassName == "OriginalSkillVisual" && skill is OriginalSkillGraph original)
                return new SkillProjectile {
                    prefab = original.projectile, trail = original.tail, impact = original.impact,
                    launchBone = original.projectileBone, launchOffset = original.launchOffset, impactOffset = original.impactOffset,
                    speed = original.projectileSpeed, acceleration = original.projectileAcceleration,
                    trailSeconds = original.tailSeconds, impactScale = original.impactScale
                };
            return new SkillProjectile();
        }
    }
}
