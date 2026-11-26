using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoftKitty
{
    [CreateAssetMenu(menuName = "Soft Kitty/技能伤害流程")]
    public sealed class SkillDamageFlow : ScriptableObject
    {
        public enum Kind { BaseDamage, SegmentShare, HitChance, ModifyDamage, Vulnerability, Shield, ApplyHealth, ReportedResult }

        [Serializable]
        public sealed class Stage
        {
            public string id;
            public string label;
            public Kind kind;
            public SkillFormula formula;
            public SkillFormula conditionChance;
        }

        public List<Stage> stages = new List<Stage>();
        [HideInInspector] public bool ownsFormulas;

        // Legacy actions stored their three formulas directly on SkillCalculation.
        internal static Stage[] FromNodeAttachments(SkillCalculation action) => new Stage[]
        {
            new Stage { id = "damage", label = "基础伤害", kind = Kind.BaseDamage, formula = action.damage },
            new Stage { id = "share", label = "分段倍率", kind = Kind.SegmentShare },
            new Stage { id = "accuracy", label = "命中检定", kind = Kind.HitChance, formula = action.accuracy },
            new Stage { id = "settlement", label = "防御结算", kind = Kind.ModifyDamage, formula = action.settlement },
            new Stage { id = "vulnerability", label = "易伤", kind = Kind.Vulnerability },
            new Stage { id = "shield", label = "护盾吸收", kind = Kind.Shield },
            new Stage { id = "health", label = "写入生命", kind = Kind.ApplyHealth }
        };
    }
}
