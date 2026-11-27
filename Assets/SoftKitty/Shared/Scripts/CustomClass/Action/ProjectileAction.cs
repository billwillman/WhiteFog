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
            var victim = ResolveAim(node, state, target);
            if (!caster || !SkillProjectile.IsLive(victim)) return;
            var host = SkillPlayback.On(instance).ProjectileHost();
            var skill = instance.mGraph;
            state.BeginTracedFlight(node.Uid);
            StartFlight(node, skill, host, state, caster, victim, fallback, null);
        }

        internal static void LaunchImpact(Action node, GraphObject skill, SkillPlayback host, GraphRuntimeState state,
            Entity source, Entity target, System.Action<Entity, Vector3> settle)
        {
            if (!node.projectileSnap || node.projectileSnap.settings == null) return;
            var caster = SkillPlayback.FindActor(source);
            var victim = ResolveAim(node, state, target);
            if (!caster || !SkillProjectile.IsLive(victim)) return;
            var origin = SkillPlayback.Position(source, caster);
            if (node.FloatValues[0] > 0 && Vector3.Distance(origin, SkillPlayback.Position(victim.mData, victim)) > node.FloatValues[0])
            {
                if (node.BoolValues[0]) host.Signal(caster, origin, "超出射程", .35f, Color.gray);
                return;
            }
            StartFlight(node, skill, host, state, caster, victim, null, settle);
        }

        private static EntityComponent ResolveAim(Action node, GraphRuntimeState state, Entity target)
        {
            var config = node.OverridesProjectileField(SkillProjectile.Field.CastTargetIndex)
                ? node.projectileOverrides : node.projectileSnap ? node.projectileSnap.settings : null;
            int index = config?.castTargetIndex ?? 0;
            if (index <= 0) return SkillPlayback.FindActor(target);
            if (state.orderedTargets == null)
                return index == 1 ? SkillPlayback.FindActor(target) : null;
            return index <= state.orderedTargets.Count ? SkillPlayback.FindActor(state.orderedTargets[index - 1]) : null;
        }

        private static void StartFlight(Action node, GraphObject skill, SkillPlayback host, GraphRuntimeState state,
            EntityComponent caster, EntityComponent victim, System.Action<Entity, Entity> fallback, System.Action<Entity, Vector3> settle)
        {
            if (!node.projectileSnap || node.projectileSnap.settings == null) return;
            var actions = BuildContactHandler(node, skill, host, state, caster.mData, fallback);
            var shot = node.projectileSnap.settings.WithOverrides(node);
            host.Schedule(Fly(shot, caster, victim, state.orderedTargets, state.castProjectileHits, (hit, point, contact) =>
            {
                state.trace?.Add(SkillCastTrace.Event.Contact, node.Uid, hit, state, via: contact.ArrivedVia, viaTrigger: contact.ArrivedViaTrigger);
                settle?.Invoke(hit, point);
                actions?.Invoke(hit, point, contact);
            }));
        }

        internal static System.Action<Entity, Vector3> ContactHandler(Action node, GraphObject skill, SkillPlayback host,
            GraphRuntimeState state, Entity source, System.Action<Entity, Entity> fallback, bool effectContact = false)
        {
            var notify = BuildContactHandler(node, skill, host, state, source, fallback);
            if (effectContact && state.trace != null)
                return (target, point) =>
                {
                    state.trace.Add(SkillCastTrace.Event.Contact, node.Uid, target, state);
                    notify?.Invoke(target, point, null);
                };
            if (notify == null) return null;
            return (target, point) => notify(target, point, null);
        }

        internal static System.Action<Entity, Vector3, ProjectileContact> BuildContactHandler(Action node, GraphObject skill,
            SkillPlayback host, GraphRuntimeState state, Entity source, System.Action<Entity, Entity> fallback)
        {
            var output = node.outputPort.Count > 0 ? node.outputPort[0] : null;
            if (output == null || output.connectedNodes.Count == 0)
            {
                if (fallback == null) return null;
                if (state.trace == null) return (struck, point, contact) => fallback(source, struck);
                return (struck, point, contact) =>
                {
                    float before = struck.GetAttributeFloat("chp");
                    fallback(source, struck);
                    state.TraceLoss(node.Uid, struck, before);
                };
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
                if (arrival.trace != null && arrival.traceOwner == null) arrival.traceTrigger = node.Uid;
                arrival.selectedTarget = null;
                GraphInstance.RunOnProjectileHit(skill, output, arrival, host.gameObject, point, source, struck);
            };
        }

        private static IEnumerator Fly(SkillProjectile shot, EntityComponent caster, EntityComponent victim,
            IReadOnlyList<Entity> targets, HashSet<Entity> castHits, System.Action<Entity, Vector3, ProjectileContact> impact)
        {
            var source = caster.mData;
            var physics = caster.gameObject.scene.GetPhysicsScene();
            int cursor = 0;
            while (targets != null && cursor < targets.Count && targets[cursor] != victim.mData) cursor++;
            var visited = new HashSet<Entity>();
            ProjectileContact previousLeg = null;
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
            var trajectory = shot.CreateTrajectory();
            var launchFacing = caster.transform.rotation;
            bool timed = shot.progressMode == SkillProjectile.ProgressMode.Duration;
            bool straight = shot.trajectory == SkillProjectile.TrajectoryType.Straight;
            float duration = Mathf.Max(.001f, shot.flightSeconds);
            try
            {
                while (true)
                {
                    var query = new SkillProjectile.HitQuery(shot, source, victim, physics, visited);
                    var origin = point;
                    float referenceDistance = Vector3.Distance(origin, end);
                    float progress = 0, elapsed = 0, speed = shot.speed;
                    bool instant = !timed && speed <= 0;
                    var contact = shot.Contact(query, point, instant ? end : point, facing, 0, 0, out var contactPoint);
                    while (!contact && !instant && progress < 1 && (timed || speed > 0))
                    {
                        yield return null;
                        bool aimIsLive = query.AimIsLive;
                        if (query.designated && !aimIsLive) break;
                        if (tracking && aimIsLive && hit) end = hit.position + victim.transform.rotation * shot.impactOffset;
                        float remaining = Time.deltaTime;
                        if (tracking) remaining = Mathf.Min(remaining, Mathf.Max(0, shot.trackingTimeout - elapsed));
                        var context = new ProjectilePathContext(origin, end, launchFacing);
                        while (remaining > 0 && progress < 1 && !contact && (timed || speed > 0))
                        {
                            // Legacy straight pursuit advances from its current position at the configured speed.
                            bool pursue = !timed && straight;
                            float sampleEnd = shot.NextSampleProgress(progress);
                            float distance = pursue ? Vector3.Distance(point, end) : referenceDistance * (sampleEnd - progress);
                            float dt = straight ? remaining : Mathf.Min(remaining, 1f / 120f);
                            float nextProgress;
                            if (timed)
                            {
                                float toSample = duration * (sampleEnd - progress);
                                nextProgress = dt >= toSample ? sampleEnd : progress + dt / duration;
                                dt = Mathf.Min(dt, toSample);
                            }
                            else
                            {
                                if (shot.acceleration < 0) dt = Mathf.Min(dt, speed / -shot.acceleration);
                                float step = dt * (speed + .5f * shot.acceleration * dt);
                                if (step >= distance)
                                {
                                    dt = distance <= 0 ? 0 : 2 * distance / (speed + Mathf.Sqrt(Mathf.Max(0, speed * speed + 2 * shot.acceleration * distance)));
                                    nextProgress = sampleEnd;
                                }
                                else nextProgress = pursue ? step / distance : progress + step / referenceDistance;
                            }
                            if (nextProgress >= .999999f) nextProgress = 1;
                            var segment = pursue ? new ProjectilePathContext(point, end, launchFacing) : context;
                            var next = trajectory.EvaluatePosition(segment, nextProgress);
                            if ((end - point).sqrMagnitude > .000001f) facing = Quaternion.LookRotation(end - point);
                            contact = shot.Contact(query, point, next, facing, elapsed, elapsed + dt, out contactPoint);
                            point = contact ? contactPoint : next;
                            elapsed += dt;
                            remaining -= dt;
                            progress = pursue && nextProgress < 1 ? 0 : nextProgress;
                            speed = Mathf.Max(0, speed + shot.acceleration * dt);
                        }
                        if (bolt) bolt.transform.SetPositionAndRotation(point, facing);
                        if (trail) trail.transform.SetPositionAndRotation(point, facing);
                        if (tracking && elapsed >= shot.trackingTimeout - .000001f) break;
                    }
                    if (!contact) break;
                    point = contactPoint;
                    if (bolt) bolt.transform.SetPositionAndRotation(point, facing);
                    if (trail) trail.transform.SetPositionAndRotation(point, facing);
                    var effect = OriginalSkillVisualAction.Spawn(shot.impact, point, Quaternion.identity);
                    if (effect) effect.transform.localScale *= shot.impactScale;
                    visited.Add(contact.mData);
                    castHits?.Add(contact.mData);
                    Entity nextTarget = targets != null && ++cursor < targets.Count ? targets[cursor] : null;
                    var hitContext = new ProjectileContact(nextTarget, visited, targets);
                    var completedLeg = contact == victim ? previousLeg : null;
                    hitContext.ArrivedVia = completedLeg?.RedirectNode;
                    hitContext.ArrivedViaTrigger = completedLeg?.RedirectTrigger;
                    var arrival = completedLeg?.OnNextHit;
                    var nested = arrival == null ? null : new ProjectileContact(nextTarget, visited, targets);
                    impact?.Invoke(contact.mData, point, hitContext);
                    arrival?.Invoke(contact.mData, point, nested);
                    // A redirect authored on this leg takes precedence over the common on-hit redirect.
                    while (nested != null && nested.Waiting) yield return null;
                    previousLeg = nested != null && nested.TakeTarget() ? nested : hitContext;
                    while (previousLeg.Waiting) yield return null;
                    victim = previousLeg.TakeTarget();
                    if (!victim) break;
                    cursor = 0;
                    while (targets != null && cursor < targets.Count && targets[cursor] != victim.mData) cursor++;
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
