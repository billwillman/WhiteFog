using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoftKitty
{
    // Reference measurements for comparison with a cast in the workbench.
    [CreateAssetMenu(menuName = "Soft Kitty/单次施法伤害记录")]
    public sealed class SkillDamageReport : ScriptableObject
    {
        public int skillId;
        public string casterId;
        public List<Entry> entries = new List<Entry>();

        [Serializable]
        public struct Entry
        {
            public int logicIndex;
            public string targetId;
            public long healthBefore, healthAfter, shieldSpent;
        }

        // The explicit replay entry point is retained for assets authored with the original replay flow.
        public sealed class Cast
        {
            internal readonly OriginalSkillGraph skill;
            internal readonly ulong casterRuntimeId;
            private readonly Dictionary<(int, string), List<Entry>> remaining = new Dictionary<(int, string), List<Entry>>();

            internal Cast(OriginalSkillGraph skill, Entity caster, List<Entry> entries)
            {
                this.skill = skill;
                casterRuntimeId = caster.RuntimeId;
                foreach (var entry in entries)
                {
                    if (entry.logicIndex < 1 || string.IsNullOrWhiteSpace(entry.targetId) ||
                        entry.healthBefore < entry.healthAfter || entry.healthAfter < 0 || entry.shieldSpent < 0)
                        throw new ArgumentException("战报记录无效。");
                    var key = (entry.logicIndex, entry.targetId);
                    if (!remaining.TryGetValue(key, out var records)) remaining.Add(key, records = new List<Entry>());
                    records.Add(entry);
                }
            }

            internal IReadOnlyList<Entry> Take(int index, string targetId)
            {
                var key = (index, targetId);
                if (!remaining.TryGetValue(key, out var records)) return Array.Empty<Entry>();
                remaining.Remove(key);
                return records;
            }
        }

        public Cast Begin(OriginalSkillGraph skill, Entity caster)
        {
            if (!skill || skillId != skill.sourceId || caster == null || string.IsNullOrEmpty(casterId) ||
                casterId != caster.CombatRandomId || entries == null)
                throw new ArgumentException("战报的技能编号或施放者与本次施法不符。");
            return new Cast(skill, caster, entries);
        }
    }
}
