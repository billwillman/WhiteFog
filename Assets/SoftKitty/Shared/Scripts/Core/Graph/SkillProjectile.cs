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
        public enum TrajectoryType { Straight = 0, Sine = 1, Waypoints = 2, FromLaunchToTarget = 3 }
        public enum ProgressMode { Speed, Duration }
        public TrajectoryType trajectory;
        public ProgressMode progressMode;
        public float flightSeconds = .5f;
        public Vector3 sineAmplitude = Vector3.up;
        public int sineHalfWaves = 1;
        public List<ProjectilePathPoint> pathPoints = new List<ProjectilePathPoint>();
        // Zero uses the entity connected to the action's target input.
        public int castTargetIndex;

        public bool UsesLaunchDisplacement => trajectory == TrajectoryType.FromLaunchToTarget;
        public FlightPath EffectiveAim => UsesLaunchDisplacement ? FlightPath.FollowTarget : path;
        internal bool IsStraight => trajectory == TrajectoryType.Straight || UsesLaunchDisplacement;

        public IProjectileTrajectory CreateTrajectory()
        {
            switch (trajectory)
            {
                case TrajectoryType.Sine: return new SineProjectileTrajectory(sineAmplitude, sineHalfWaves);
                case TrajectoryType.Waypoints: return new WaypointProjectileTrajectory(pathPoints);
                default: return StraightProjectileTrajectory.Instance;
            }
        }

        // End sweeps at authored waypoints even when a fast shot finishes within a single frame.
        internal float NextSampleProgress(float current)
        {
            if (IsStraight) return 1;
            if (trajectory == TrajectoryType.Sine)
                return Mathf.Min(1, current + .125f / Mathf.Clamp(sineHalfWaves, 1, 16));
            float next = Mathf.Min(1, current + .0625f);
            if (pathPoints != null)
                foreach (var point in pathPoints)
                    if (point.progress > current && point.progress < next) next = point.progress;
            return next;
        }

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

        [Flags]
        public enum Field
        {
            None = 0,
            Prefab = 1 << 0,
            Trail = 1 << 1,
            TrailSeconds = 1 << 2,
            LaunchBone = 1 << 3,
            LaunchOffset = 1 << 4,
            TargetBone = 1 << 5,
            ImpactOffset = 1 << 6,
            Path = 1 << 7,
            Speed = 1 << 8,
            Acceleration = 1 << 9,
            TrackingTimeout = 1 << 10,
            HitRecipients = 1 << 11,
            HitTags = 1 << 12,
            HitBoxSize = 1 << 13,
            HitBoxStart = 1 << 14,
            HitBoxSeconds = 1 << 15,
            HitBoxOffset = 1 << 16,
            HitLayers = 1 << 17,
            Impact = 1 << 18,
            ImpactScale = 1 << 19,
            Trajectory = 1 << 20,
            ProgressMode = 1 << 21,
            FlightSeconds = 1 << 22,
            SineAmplitude = 1 << 23,
            SineHalfWaves = 1 << 24,
            PathPoints = 1 << 25,
            CastTargetIndex = 1 << 26,
        }

        public SkillProjectile WithOverrides(Action node)
        {
            var result = Copy();
            if (node.projectileOverrides != null)
                result.CopyFieldsFrom(node.projectileOverrides, node.projectileOverrideFields);
            result.speed = LaunchSpeed(node);
            return result;
        }

        internal void CopyFieldsFrom(SkillProjectile source, Field fields)
        {
            if ((fields & Field.Trajectory) != 0) trajectory = source.trajectory;
            if ((fields & Field.ProgressMode) != 0) progressMode = source.progressMode;
            if ((fields & Field.FlightSeconds) != 0) flightSeconds = source.flightSeconds;
            if ((fields & Field.SineAmplitude) != 0) sineAmplitude = source.sineAmplitude;
            if ((fields & Field.SineHalfWaves) != 0) sineHalfWaves = source.sineHalfWaves;
            if ((fields & Field.CastTargetIndex) != 0) castTargetIndex = source.castTargetIndex;
            if ((fields & Field.PathPoints) != 0) pathPoints = source.CopyPathPoints();
            if ((fields & Field.Prefab) != 0) prefab = source.prefab;
            if ((fields & Field.Trail) != 0) trail = source.trail;
            if ((fields & Field.TrailSeconds) != 0) trailSeconds = source.trailSeconds;
            if ((fields & Field.LaunchBone) != 0) launchBone = source.launchBone;
            if ((fields & Field.LaunchOffset) != 0) launchOffset = source.launchOffset;
            if ((fields & Field.TargetBone) != 0) targetBone = source.targetBone;
            if ((fields & Field.ImpactOffset) != 0) impactOffset = source.impactOffset;
            if ((fields & Field.Path) != 0) path = source.path;
            if ((fields & Field.Speed) != 0) speed = source.speed;
            if ((fields & Field.Acceleration) != 0) acceleration = source.acceleration;
            if ((fields & Field.TrackingTimeout) != 0) trackingTimeout = source.trackingTimeout;
            if ((fields & Field.HitRecipients) != 0) hitRecipients = source.hitRecipients;
            if ((fields & Field.HitTags) != 0) hitTags = source.hitTags == null ? new List<string>() : new List<string>(source.hitTags);
            if ((fields & Field.HitBoxSize) != 0) hitBoxSize = source.hitBoxSize;
            if ((fields & Field.HitBoxStart) != 0) hitBoxStart = source.hitBoxStart;
            if ((fields & Field.HitBoxSeconds) != 0) hitBoxSeconds = source.hitBoxSeconds;
            if ((fields & Field.HitBoxOffset) != 0) hitBoxOffset = source.hitBoxOffset;
            if ((fields & Field.HitLayers) != 0) hitLayers = source.hitLayers;
            if ((fields & Field.Impact) != 0) impact = source.impact;
            if ((fields & Field.ImpactScale) != 0) impactScale = source.impactScale;
        }

        public float LaunchSpeed(Action node) =>
            (node.projectileOverrideFields & Field.Speed) != 0 && node.projectileOverrides != null
                ? node.projectileOverrides.speed : node.projectileSpeedOverride > 0 ? node.projectileSpeedOverride : speed;

        private List<ProjectilePathPoint> CopyPathPoints() => pathPoints == null
            ? new List<ProjectilePathPoint>() : new List<ProjectilePathPoint>(pathPoints);

        public SkillProjectile Copy()
        {
            var copy = (SkillProjectile)MemberwiseClone();
            copy.pathPoints = CopyPathPoints();
            copy.hitTags = hitTags == null ? new List<string>() : new List<string>(hitTags);
            return copy;
        }

        public static bool IsLaunch(Action node) => node.ClassName == "Projectile" ||
            (node.ClassName == "OriginalSkillVisual" && node.IntValues.Count > 0 && node.IntValues[0] == 3);

        public static bool IsImpactLaunch(Action node) =>
            (node.ClassName == "SkillImpact" || node.ClassName == "FormulaImpact") &&
            node.IntValues.Count > 0 && node.IntValues[0] == (int)SkillPlayback.Style.Projectile;

        public static SkillProjectile For(Action node, GraphObject skill)
        {
            if (node.projectileSnap && node.projectileSnap.settings != null) return node.projectileSnap.settings;
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
