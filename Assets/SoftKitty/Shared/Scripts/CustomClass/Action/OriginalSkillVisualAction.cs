using System.Collections;
using UnityEngine;

namespace SoftKitty
{
    [UnityEngine.Scripting.Preserve]
    public sealed class OriginalSkillVisualAction : Action
    {
        public OriginalSkillVisualAction() { }
        private OriginalSkillVisualAction(Action data) : base(data) { }
        [UnityEngine.Scripting.Preserve]
        public OriginalSkillVisualAction InitAction(Action data) => new OriginalSkillVisualAction(data);

        public override void Run(GraphInstance instance, GraphRuntimeState state)
        {
            if (IntValues[0] != 3 && IntValues[0] != 6)
            {
                base.Run(instance, state);
                return;
            }

            var caster = SkillPlayback.ReadEntity(this, 1, state);
            var target = SkillPlayback.ReadEntity(this, 2, state);
            var graph = instance.mGraph as OriginalSkillGraph;
            if (caster == null || target == null || !graph) return;
            var host = SkillPlayback.On(instance);
            state.NodeStates[NodeIndex].lastBool = true;
            var impact = host.OriginalProjectileImpact;
            if (IntValues[0] == 6)
            {
                int index = IntValues[1];
                if (index < 0 || index >= graph.effects.Length) return;
                var cue = graph.effects[index];
                host.ScheduleReleases(state, caster, WaitTime, FormulaImpactAction.SegmentCount(this), Interval,
                    () => PlayCue(host, cue, caster, target, impact));
                return;
            }
            host.ScheduleReleases(state, caster, WaitTime, FormulaImpactAction.SegmentCount(this), Interval,
                () => Launch(host, graph, caster, target, impact));
        }

        private static Transform Anchor(EntityComponent actor, string name)
        {
            if (string.IsNullOrEmpty(name)) return actor.transform;
            foreach (var bone in actor.GetComponentsInChildren<Transform>())
                if (bone.name == name) return bone;
            if (name == "weapon_r")
                foreach (var bone in actor.GetComponentsInChildren<Transform>())
                    if (bone.name == "Bip01 R Hand") return bone;
            return ActorRoot(actor.transform);
        }

        private static void PlayCue(SkillPlayback host, OriginalSkillGraph.EffectCue cue, Entity source, Entity recipient, System.Action<Entity, Entity> impact)
        {
            var actor = SkillPlayback.FindActor(cue.onTarget ? recipient : source);
            if (!actor || !actor.AvailableForInteraction) return;
            var anchor = Anchor(actor, cue.bone);
            var point = anchor.position;
            if (cue.bone == "vfx_p_hit" && anchor.name != cue.bone)
                point = SkillPlayback.Position(cue.onTarget ? recipient : source, actor);
            var fx = Spawn(cue.prefab, point, cue.followRotation ? anchor.rotation : actor.transform.rotation,
                cue.followBone && cue.followRotation ? anchor : null, cue.duration <= 0);
            if (fx && cue.duration > 0) Object.Destroy(fx, cue.duration);
            if (fx && cue.followBone && !cue.followRotation)
                host.ProjectileHost().Schedule(FollowPosition(fx.transform, anchor));
            if (cue.onTarget && impact != null)
            {
                if (cue.hitDelay > 0) host.ProjectileHost().Schedule(DelayedHit(cue.hitDelay, source, recipient, impact));
                else impact(source, recipient);
            }
        }

        private static IEnumerator DelayedHit(float delay, Entity source, Entity recipient, System.Action<Entity, Entity> impact)
        {
            yield return new WaitForSeconds(delay);
            var target = SkillPlayback.FindActor(recipient);
            if (target && target.AvailableForInteraction) impact(source, recipient);
        }

        private static void Launch(SkillPlayback host, OriginalSkillGraph graph, Entity source, Entity recipient, System.Action<Entity, Entity> impact)
        {
            var caster = SkillPlayback.FindActor(source);
            var target = SkillPlayback.FindActor(recipient);
            if (caster && target && target.AvailableForInteraction)
                host.ProjectileHost().Schedule(Fly(graph, caster, target, impact));
        }

        public override void Execute(GraphRuntimeState state)
        {
            var instance = state.RootState.RootInstance.GetComponent<GraphInstance>();
            var graph = instance.mGraph as OriginalSkillGraph;
            var caster = SkillPlayback.FindActor(SkillPlayback.ReadEntity(this, 1, state));
            var target = SkillPlayback.FindActor(SkillPlayback.ReadEntity(this, 2, state));
            if (!graph || !caster || (!target && IntValues[0] != 0)) return;
            switch (IntValues[0])
            {
                case 0:
                    var binding = SkillMotion.For(this, graph);
                    if (!binding.Play(caster.GetComponentInChildren<Animator>(), state, out var error) && error != null)
                        Debug.LogWarning(graph.name + " · " + Uid + " · " + error, caster);
                    break;
                case 1:
                    var castRoot = ActorRoot(caster.transform);
                    var cast = Spawn(graph.castEffect, castRoot.position, castRoot.rotation);
                    if (cast) SkillPlayback.On(instance).ProjectileHost().Schedule(FollowPosition(cast.transform, castRoot));
                    break;
                case 2:
                    var bones = caster.GetComponentsInChildren<Transform>(true);
                    var hand = System.Array.Find(bones, bone => bone.name == graph.weaponBone);
                    if (hand && graph.weaponEffect)
                    {
                        var weapon = Object.Instantiate(graph.weaponEffect, hand, false);
                        weapon.SetActive(true);
                        Object.Destroy(weapon, graph.weaponSeconds);
                    }
                    break;
                case 3:
                    var host = SkillPlayback.On(instance);
                    Launch(host, graph, caster.mData, target.mData, host.OriginalProjectileImpact);
                    break;
            }
        }

        private static GameObject Spawn(GameObject prefab, Vector3 at, Quaternion facing, Transform parent = null, bool timed = true)
        {
            if (!prefab) return null;
            var effect = Object.Instantiate(prefab, at, facing, parent);
            effect.SetActive(true);
            if (timed)
            {
                float seconds = .1f;
                foreach (var system in effect.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var settings = system.main;
                    seconds = Mathf.Max(seconds, settings.startDelay.constantMax + settings.duration + settings.startLifetime.constantMax);
                }
                Object.Destroy(effect, seconds);
            }
            return effect;
        }

        private static Transform ActorRoot(Transform actor)
        {
            var animator = actor.GetComponentInChildren<Animator>();
            if (!animator) return actor;
            var root = animator.transform.Find("root");
            return root ? root : actor;
        }

        private static IEnumerator FollowPosition(Transform effect, Transform anchor)
        {
            while (effect && anchor)
            {
                effect.position = anchor.position;
                yield return null;
            }
        }

        private static IEnumerator Fly(OriginalSkillGraph graph, EntityComponent casterActor, EntityComponent targetActor, System.Action<Entity, Entity> impact)
        {
            var caster = casterActor.transform;
            var target = targetActor.transform;
            var source = casterActor.mData;
            var recipient = targetActor.mData;
            var releaseRoot = string.IsNullOrEmpty(graph.projectileBone) ? ActorRoot(caster) : Anchor(casterActor, graph.projectileBone);
            var hitRoot = ActorRoot(target);
            Vector3 start = releaseRoot.position + caster.rotation * graph.launchOffset;
            Vector3 end = hitRoot.position + target.rotation * graph.impactOffset;
            Vector3 delta = end - start;
            float length = delta.magnitude;
            var facing = length > .001f ? Quaternion.LookRotation(delta) : caster.rotation;
            var bolt = Spawn(graph.projectile, start, facing, null, false);
            var tail = Spawn(graph.tail, start, facing, null, false);
            try
            {
                for (float t = 0; graph.projectileSpeed > 0 && t * (graph.projectileSpeed + .5f * graph.projectileAcceleration * t) < length; t += Time.deltaTime)
                {
                    var point = start + delta.normalized * (t * (graph.projectileSpeed + .5f * graph.projectileAcceleration * t));
                    if (bolt) bolt.transform.position = point;
                    if (tail) tail.transform.position = point;
                    yield return null;
                }
                if (bolt) bolt.transform.position = end;
                if (hitRoot && targetActor && targetActor.isActiveAndEnabled && targetActor.mData == recipient && recipient.AvailableForInteraction)
                {
                    var hit = Spawn(graph.impact, hitRoot.position + target.rotation * graph.impactOffset, Quaternion.identity);
                    if (hit) hit.transform.localScale *= graph.impactScale;
                    impact?.Invoke(source, recipient);
                }
                if (tail)
                {
                    tail.transform.position = end;
                    foreach (var system in tail.GetComponentsInChildren<ParticleSystem>(true))
                        system.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                    Object.Destroy(tail, graph.tailSeconds);
                    tail = null;
                }
            }
            finally
            {
                if (bolt) Object.Destroy(bolt);
                if (tail) Object.Destroy(tail);
            }
        }
    }
}
