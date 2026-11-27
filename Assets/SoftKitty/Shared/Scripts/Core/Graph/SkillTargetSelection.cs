using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoftKitty
{
    // Custom rules only implement a predicate or a sort key. The editor discovers serializable subclasses.
    [Serializable] public abstract class TargetRule
    {
        public abstract string Label { get; }
    }

    [Serializable] public abstract class TargetFilter : TargetRule
    {
        public abstract bool Matches(TargetCandidate candidate);
    }

    [Serializable] public abstract class TargetPriority : TargetRule
    {
        public abstract float Key(TargetCandidate candidate);
    }

    public struct TargetCandidate
    {
        public EntityComponent Actor;
        public float DistanceSquared;
        public int ListIndex;
        public bool AlreadyHit;
        public bool HitDuringCast;
        public float Health => Actor.mData.GetAttributeFloat("chp");
        public float HealthPercent
        {
            get
            {
                float maximum = Actor.mData.GetAttributeFloat("hp");
                return maximum > 0 ? 100f * Health / maximum : float.PositiveInfinity;
            }
        }
    }

    [Serializable] public sealed class TargetAlive : TargetFilter
    {
        public override string Label => "目标存活";
        public override bool Matches(TargetCandidate c) => SkillProjectile.IsLive(c.Actor);
    }
    [Serializable] public sealed class TargetUnhit : TargetFilter
    {
        public override string Label => "本枚投射物尚未接触";
        public override bool Matches(TargetCandidate c) => !c.AlreadyHit;
    }
    [Serializable] public sealed class TargetUnhitDuringCast : TargetFilter
    {
        public override string Label => "本次施法的投射物均未接触";
        public override bool Matches(TargetCandidate c) => !c.HitDuringCast;
    }
    [Serializable] public sealed class TargetInRadius : TargetFilter
    {
        [Min(0), InspectorName("半径（米）")] public float radius = 8;
        public override string Label => "在指定半径内";
        public override bool Matches(TargetCandidate c) => c.DistanceSquared <= radius * radius;
    }
    [Serializable] public sealed class TargetLowHealth : TargetFilter
    {
        [Range(0,100), InspectorName("生命比例上限（%）")] public float percent = 50;
        public override string Label => "生命比例不超过";
        public override bool Matches(TargetCandidate c) => c.HealthPercent <= percent;
    }
    [Serializable] public sealed class TargetListOrder : TargetPriority
    {
        public override string Label => "按施法名单顺序";
        public override float Key(TargetCandidate c) => c.ListIndex;
    }
    [Serializable] public sealed class TargetNearest : TargetPriority
    {
        public override string Label => "离参考点最近";
        public override float Key(TargetCandidate c) => c.DistanceSquared;
    }
    [Serializable] public sealed class TargetLeastHealth : TargetPriority
    {
        public override string Label => "当前生命值最低";
        public override float Key(TargetCandidate c) => c.Health;
    }
    [Serializable] public sealed class TargetLeastHealthPercent : TargetPriority
    {
        public override string Label => "生命比例最低";
        public override float Key(TargetCandidate c) => c.HealthPercent;
    }

    [Serializable]
    public sealed class SkillTargetSelection
    {
        public bool sceneEnemies;
        [SerializeReference] public List<TargetFilter> filters = new List<TargetFilter> { new TargetAlive(), new TargetUnhit() };
        [SerializeReference] public List<TargetPriority> priorities = new List<TargetPriority> { new TargetListOrder() };

        internal Entity Pick(GraphRuntimeState state)
        {
            var caster = SkillPlayback.FindActor(state.RootState.GetInvolvedEntity("Dealer"));
            if (!caster) return null;
            var origin = state.projectileContact && state.RootState.RootInstance
                ? state.RootState.RootInstance.transform.position : caster.transform.position;
            var roster = state.orderedTargets;
            TargetCandidate best = default;
            void Consider(EntityComponent actor, int index)
            {
                if (!actor || !actor.isActiveAndEnabled || actor.mData == null || actor.gameObject.scene != caster.gameObject.scene) return;
                var candidate = new TargetCandidate {
                    Actor = actor, ListIndex = index, DistanceSquared = (actor.transform.position - origin).sqrMagnitude,
                    AlreadyHit = state.projectileHit?.HasHit(actor.mData) == true,
                    HitDuringCast = state.castProjectileHits?.Contains(actor.mData) == true
                };
                foreach (var filter in filters)
                    if (filter == null || !filter.Matches(candidate)) return;
                if (!best.Actor || Compare(candidate, best) < 0) best = candidate;
            }
            if (sceneEnemies)
            {
                string enemyTag = caster.mData.hasMatchingTag("friendly") ? "hostile" :
                    caster.mData.hasMatchingTag("hostile") ? "friendly" : null;
                if (enemyTag == null) return null;
                foreach (var actor in GameManager.GetEntityInstanceList())
                {
                    if (!actor || actor == caster || actor.mData == null || !actor.mData.hasMatchingTag(enemyTag)) continue;
                    int index = int.MaxValue;
                    if (roster != null)
                        for (int i = 0; i < roster.Count; i++)
                            if (roster[i] == actor.mData) { index = i; break; }
                    Consider(actor, index);
                }
            }
            else if (roster != null)
            {
                for (int i = 0; i < roster.Count; i++) Consider(SkillPlayback.FindActor(roster[i]), i);
            }
            else Consider(SkillPlayback.FindActor(state.RootState.GetInvolvedEntity("Target")), 0);
            return best.Actor ? best.Actor.mData : null;
        }

        private int Compare(TargetCandidate a, TargetCandidate b)
        {
            foreach (var priority in priorities)
            {
                if (priority == null) continue;
                int result = priority.Key(a).CompareTo(priority.Key(b));
                if (result != 0) return result;
            }
            return string.CompareOrdinal(a.Actor.mData.CombatRandomId, b.Actor.mData.CombatRandomId);
        }
    }
}
