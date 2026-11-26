using System.Collections;
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
            host.ScheduleReleases(state, source, node.WaitTime, FormulaImpactAction.SegmentCount(node), node.Interval, () =>
            {
                if (node.ConditionsMet(ref state)) Launch(node, instance, state, source, target, fallback);
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
            var output = node.outputPort.Count > 0 ? node.outputPort[0] : null;
            System.Action<Entity, Vector3> onHit = null;
            if (output != null && output.connectedNodes.Count > 0)
            {
                var arrival = state.ForCast(null);
                arrival.HandlerStates = (HandlerRuntimeState[])state.HandlerStates.Clone();
                onHit = (struck, point) => GraphInstance.RunOnProjectileHit(skill, output, arrival, host.gameObject, point, source, struck);
            }
            else if (fallback != null) onHit = (struck, point) => fallback(source, struck);
            host.Schedule(Fly(settings, caster, victim, onHit));
        }

        private static IEnumerator Fly(SkillProjectile shot, EntityComponent caster, EntityComponent victim, System.Action<Entity, Vector3> impact)
        {
            var query = new SkillProjectile.HitQuery(shot, caster.mData, victim, caster.gameObject.scene.GetPhysicsScene());
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
                if (contact)
                {
                    point = contactPoint;
                    var effect = OriginalSkillVisualAction.Spawn(shot.impact, point, Quaternion.identity);
                    if (effect) effect.transform.localScale *= shot.impactScale;
                    impact?.Invoke(contact.mData, point);
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
