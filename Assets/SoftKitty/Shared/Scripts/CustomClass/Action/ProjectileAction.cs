using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SoftKitty
{
    [UnityEngine.Scripting.Preserve]
    public sealed class ProjectileAction : Action
    {
        public ProjectileAction() { }
        private ProjectileAction(Action data) : base(data) { }
        [UnityEngine.Scripting.Preserve]
        public ProjectileAction InitAction(Action data) => new ProjectileAction(data);

        public override void Run(GraphInstance instance, GraphRuntimeState state) => Schedule(this, instance, state);

        internal static void Schedule(Action node, GraphInstance instance, GraphRuntimeState state)
        {
            var source = SkillPlayback.ReadEntity(node, 1, state);
            var target = SkillPlayback.ReadEntity(node, 2, state);
            if (source == null || target == null) return;
            var host = SkillPlayback.On(instance);
            var fallback = host.OriginalProjectileImpact;
            state.NodeStates[node.NodeIndex].lastBool = true;
            uint ordinal = 0;
            host.ScheduleReleases(state, source, node.WaitTime, FormulaImpactAction.SegmentCount(node), node.Interval, () =>
            {
                var release = state.ForRelease(node.Uid, ordinal++);
                if (node.ConditionsMet(ref release)) Launch(node, instance, release, source, target, fallback);
            });
        }

        public override void Execute(GraphRuntimeState state) => LaunchNow(this, state);

        internal static void LaunchNow(Action node, GraphRuntimeState state)
        {
            var instance = state.RootState.RootInstance.GetComponent<GraphInstance>();
            Launch(node, instance, state, SkillPlayback.ReadEntity(node, 1, state), SkillPlayback.ReadEntity(node, 2, state),
                SkillPlayback.On(instance).OriginalProjectileImpact);
        }

        private static void Launch(Action node, GraphInstance instance, GraphRuntimeState state, Entity source, Entity target,
            System.Action<Entity, Entity> fallback)
        {
            var caster = SkillPlayback.FindActor(source);
            var victim = SkillPlayback.FindActor(target);
            if (!caster || !SkillProjectile.IsLive(victim)) return;
            var host = SkillPlayback.On(instance).ProjectileHost();
            var skill = instance.mGraph;
            var settings = SkillProjectile.For(node, skill);
            host.Schedule(Fly(settings, caster, victim, state.orderedTargets, BuildContactHandler(node, skill, host, state, source, fallback)));
        }

        internal static System.Action<Entity, Vector3> ContactHandler(Action node, GraphObject skill, SkillPlayback host,
            GraphRuntimeState state, Entity source, System.Action<Entity, Entity> fallback)
        {
            var notify = BuildContactHandler(node, skill, host, state, source, fallback);
            if (notify == null) return null;
            return (target, point) => notify(target, point, null);
        }

        private static System.Action<Entity, Vector3, ProjectileContact> BuildContactHandler(Action node, GraphObject skill,
            SkillPlayback host, GraphRuntimeState state, Entity source, System.Action<Entity, Entity> fallback)
        {
            var output = node.outputPort.Count > 0 ? node.outputPort[0] : null;
            if (output == null || output.connectedNodes.Count == 0)
            {
                if (fallback == null) return null;
                return (struck, point, contact) => fallback(source, struck);
            }
            var flightState = state.ForCast(null);
            flightState.EnsureCasterSnapshot(source);
            var handlers = (HandlerRuntimeState[])state.HandlerStates.Clone();
            return (struck, point, contact) =>
            {
                var arrival = flightState.ForCast(null);
                arrival.HandlerStates = (HandlerRuntimeState[])handlers.Clone();
                arrival.projectileContact = true;
                arrival.projectileHit = contact;
                GraphInstance.RunOnProjectileHit(skill, output, arrival, host.gameObject, point, source, struck);
            };
        }

        private static IEnumerator Fly(SkillProjectile shot, EntityComponent caster, EntityComponent victim,
            IReadOnlyList<Entity> targets, System.Action<Entity, Vector3, ProjectileContact> impact)
        {
            var source = caster.mData;
            var physics = caster.gameObject.scene.GetPhysicsScene();
            int cursor = 0;
            while (targets != null && cursor < targets.Count && targets[cursor] != victim.mData) cursor++;
            var visited = targets != null ? new HashSet<Entity>() : null;
            var muzzle = string.IsNullOrEmpty(shot.launchBone) ? OriginalSkillVisualAction.ActorRoot(caster.transform)
                : OriginalSkillVisualAction.Anchor(caster, shot.launchBone);
            var hit = string.IsNullOrEmpty(shot.targetBone) ? OriginalSkillVisualAction.ActorRoot(victim.transform)
                : OriginalSkillVisualAction.Anchor(victim, shot.targetBone);
            Vector3 point = muzzle.position + caster.transform.rotation * shot.launchOffset;
            Vector3 end = hit.position + victim.transform.rotation * shot.impactOffset;
            var delta = end - point;
            var facing = delta.sqrMagnitude > .000001f ? Quaternion.LookRotation(delta) : caster.transform.rotation;
            var bolt = OriginalSkillVisualAction.Spawn(shot.prefab, point, facing, null, false);
            var trail = OriginalSkillVisualAction.Spawn(shot.trail, point, facing, null, false);
            bool tracking = shot.path == SkillProjectile.FlightPath.FollowTarget;
            try
            {
                while (true)
                {
                    var query = new SkillProjectile.HitQuery(shot, source, victim, physics, visited);
                    float elapsed = 0, speed = shot.speed;
                    var contact = shot.Contact(query, point, speed > 0 ? point : end, facing, 0, 0, out var contactPoint);
                    while (!contact && speed > 0 && (point - end).sqrMagnitude > .000001f)
                    {
                        yield return null;
                        bool aimIsLive = query.AimIsLive;
                        if (query.designated && !aimIsLive) break;
                        float dt = Time.deltaTime;
                        if (tracking && aimIsLive && hit) end = hit.position + victim.transform.rotation * shot.impactOffset;
                        if (tracking) dt = Mathf.Min(dt, Mathf.Max(0, shot.trackingTimeout - elapsed));
                        if (shot.acceleration < 0) dt = Mathf.Min(dt, speed / -shot.acceleration);
                        float step = dt * (speed + .5f * shot.acceleration * dt);
                        float distance = Vector3.Distance(point, end);
                        // When the endpoint is reached mid-frame, clip the hit window to the actual flight time.
                        if (step > distance && speed > 0)
                            dt = 2f * distance / (speed + Mathf.Sqrt(Mathf.Max(0, speed * speed + 2f * shot.acceleration * distance)));
                        var next = Vector3.MoveTowards(point, end, step);
                        if (distance > .00001f) facing = Quaternion.LookRotation(end - point);
                        contact = shot.Contact(query, point, next, facing, elapsed, elapsed + dt, out contactPoint);
                        point = contact ? contactPoint : next;
                        if (bolt) bolt.transform.SetPositionAndRotation(point, facing);
                        if (trail) trail.transform.SetPositionAndRotation(point, facing);
                        elapsed += dt;
                        speed = Mathf.Max(0, speed + shot.acceleration * dt);
                        if (tracking && elapsed >= shot.trackingTimeout) break;
                    }
                    if (!contact) break;
                    point = contactPoint;
                    if (bolt) bolt.transform.SetPositionAndRotation(point, facing);
                    if (trail) trail.transform.SetPositionAndRotation(point, facing);
                    var effect = OriginalSkillVisualAction.Spawn(shot.impact, point, Quaternion.identity);
                    if (effect) effect.transform.localScale *= shot.impactScale;
                    visited?.Add(contact.mData);
                    Entity nextTarget = targets != null && ++cursor < targets.Count ? targets[cursor] : null;
                    var hitContext = new ProjectileContact(nextTarget, visited, targets);
                    impact?.Invoke(contact.mData, point, hitContext);
                    while (hitContext.Waiting) yield return null;
                    victim = hitContext.TakeTarget();
                    if (!victim) break;
                    cursor = 0;
                    while (cursor < targets.Count && targets[cursor] != victim.mData) cursor++;
                    hit = string.IsNullOrEmpty(shot.targetBone) ? OriginalSkillVisualAction.ActorRoot(victim.transform)
                        : OriginalSkillVisualAction.Anchor(victim, shot.targetBone);
                    end = hit.position + victim.transform.rotation * shot.impactOffset;
                    if ((end - point).sqrMagnitude > .000001f) facing = Quaternion.LookRotation(end - point);
                }
                if (trail)
                {
                    trail.transform.position = point;
                    foreach (var particles in trail.GetComponentsInChildren<ParticleSystem>(true))
                        particles.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                    Object.Destroy(trail, shot.trailSeconds);
                    trail = null;
                }
            }
            finally
            {
                if (bolt) Object.Destroy(bolt);
                if (trail) Object.Destroy(trail);
            }
        }
    }
}
