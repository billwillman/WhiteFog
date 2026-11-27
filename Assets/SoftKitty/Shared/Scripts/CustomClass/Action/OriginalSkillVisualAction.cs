using System.Collections;
using UnityEngine;

namespace SoftKitty
{
    [UnityEngine.Scripting.Preserve]
    public sealed class OriginalSkillVisualAction : Action
    {
        [System.NonSerialized] private SkillMotion legacyMotion;

        public OriginalSkillVisualAction() { }
        private OriginalSkillVisualAction(Action data) : base(data) { }
        [UnityEngine.Scripting.Preserve]
        public OriginalSkillVisualAction InitAction(Action data) => new OriginalSkillVisualAction(data);

        public override void Run(GraphInstance instance, GraphRuntimeState state)
        {
            if (IntValues[0] == 3) { ProjectileAction.Schedule(this, instance, state); return; }
            if (IntValues[0] != 6) { base.Run(instance, state); return; }

            var caster = SkillPlayback.ReadEntity(this, 1, state);
            var target = SkillPlayback.ReadEntity(this, 2, state);
            var graph = instance.mGraph as OriginalSkillGraph;
            if (caster == null || target == null) return;
            var host = SkillPlayback.On(instance);
            state.NodeStates[NodeIndex].lastBool = true;
            var fallback = host.OriginalProjectileImpact;
            var cue = EffectFor(this, graph);
            if (cue == null) return;
            uint ordinal = 0;
            host.ScheduleReleases(state, caster, WaitTime, FormulaImpactAction.SegmentCount(this), Interval,
                () =>
                {
                    var release = state.ForRelease(Uid, ordinal++);
                    if (!ConditionsMet(ref release)) return;
                    var onHit = cue.triggerHit ? ProjectileAction.ContactHandler(this, instance.mGraph, host.ProjectileHost(), release, caster, fallback) : null;
                    PlayCue(host, cue, caster, target, onHit);
                });
        }

        public static OriginalSkillGraph.EffectCue EffectFor(Action node, OriginalSkillGraph graph)
        {
            if (node.visualEffect != null) return node.visualEffect;
            int stage = node.IntValues.Count > 0 ? node.IntValues[0] : 0;
            if (!graph) return new OriginalSkillGraph.EffectCue();
            if (stage == 1) return new OriginalSkillGraph.EffectCue { prefab = graph.castEffect, bone = "root", followBone = true };
            if (stage == 2) return new OriginalSkillGraph.EffectCue { prefab = graph.weaponEffect, bone = graph.weaponBone,
                followBone = true, followRotation = true, duration = graph.weaponSeconds };
            int index = node.IntValues.Count > 1 ? node.IntValues[1] : -1;
            if (index < 0 || index >= graph.effects.Length || graph.effects[index] == null) return null;
            var cue = graph.effects[index].Copy();
            cue.triggerHit = cue.onTarget;
            return cue;
        }

        internal static Transform Anchor(EntityComponent actor, string name)
        {
            if (string.IsNullOrEmpty(name)) return actor.transform;
            foreach (var bone in actor.GetComponentsInChildren<Transform>())
                if (bone.name == name) return bone;
            if (name == "weapon_r")
                foreach (var bone in actor.GetComponentsInChildren<Transform>())
                    if (bone.name == "Bip01 R Hand") return bone;
            return ActorRoot(actor.transform);
        }

        private static void PlayCue(SkillPlayback host, OriginalSkillGraph.EffectCue cue, Entity source, Entity recipient, System.Action<Entity, Vector3> impact)
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
            if (cue.triggerHit && impact != null)
            {
                var facing = cue.followRotation ? anchor.rotation : actor.transform.rotation;
                void TestHit()
                {
                    var target = SkillPlayback.FindActor(recipient);
                    var center = cue.followBone && anchor ? anchor.position : point;
                    if (SkillProjectile.OverlapsTarget(target, center, facing, cue.hitBoxSize, ~0)) impact(recipient, center);
                }
                if (cue.hitDelay > 0) host.ProjectileHost().Schedule(DelayedHit(cue.hitDelay, TestHit));
                else TestHit();
            }
        }

        private static IEnumerator DelayedHit(float delay, System.Action test)
        {
            yield return new WaitForSeconds(delay);
            test();
        }

        public override void Execute(GraphRuntimeState state)
        {
            var instance = state.RootState.RootInstance.GetComponent<GraphInstance>();
            var graph = instance.mGraph as OriginalSkillGraph;
            var caster = SkillPlayback.FindActor(SkillPlayback.ReadEntity(this, 1, state));
            if (!caster) return;
            if (IntValues[0] == 0)
            {
                var binding = motion;
                if (binding == null)
                {
                    var clip = graph ? graph.castMotion : null;
                    if (legacyMotion == null || !ReferenceEquals(legacyMotion.clip, clip))
                        legacyMotion = SkillMotion.For(this, graph);
                    binding = legacyMotion;
                }
                if (!binding.Play(caster.SkillAnimator, state, out var error, caster) && error != null)
                    Debug.LogWarning(instance.mGraph.name + " · " + Uid + " · " + error, caster);
                return;
            }
            var target = SkillPlayback.FindActor(SkillPlayback.ReadEntity(this, 2, state));
            if (!target && (IntValues[0] == 3 || IntValues[0] == 6)) return;
            if ((IntValues[0] == 1 || IntValues[0] == 2) && (visualEffect != null || !graph))
            {
                var effect = EffectFor(this, graph);
                if (effect != null) PlayCue(SkillPlayback.On(instance), effect, caster.mData, target ? target.mData : null, null);
                return;
            }
            switch (IntValues[0])
            {
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
                        SkillPlayback.TrackTemporaryObject(weapon);
                        weapon.SetActive(true);
                        Object.Destroy(weapon, graph.weaponSeconds);
                    }
                    break;
                case 3:
                    ProjectileAction.LaunchNow(this, state);
                    break;
                case 6:
                    var cue = EffectFor(this, graph);
                    if (cue != null)
                    {
                        var host = SkillPlayback.On(instance);
                        var onHit = cue.triggerHit ? ProjectileAction.ContactHandler(this, instance.mGraph, host.ProjectileHost(), state, caster.mData, host.OriginalProjectileImpact) : null;
                        PlayCue(host, cue, caster.mData, target.mData, onHit);
                    }
                    break;
            }
        }

        internal static GameObject Spawn(GameObject prefab, Vector3 at, Quaternion facing, Transform parent = null, bool timed = true)
        {
            if (!prefab) return null;
            var effect = Object.Instantiate(prefab, at, facing, parent);
            SkillPlayback.TrackTemporaryObject(effect);
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

        internal static Transform ActorRoot(Transform actor)
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

    }
}
