using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoftKitty
{
    [Serializable]
    public sealed class SkillCalculation
    {
        public SkillFormula accuracy, damage, settlement;
        public SkillLevelTable levels;
        public List<float> segmentPercent = new List<float> { 100 };

        public RootRuntimeState Capture(RootRuntimeState source, Entity caster, Entity target, int level)
        {
            var context = source;
            context.InvolvedEntityDic = new Dictionary<string, Entity>(source.InvolvedEntityDic);
            context.InvolvedEntityDic["Dealer"] = caster;
            context.InvolvedEntityDic["Target"] = target;
            context.DynamicVariableDic = new Dictionary<string, float>(source.DynamicVariableDic);
            context.DynamicVariableDic["skill.level"] = level;
            if (!levels || !accuracy || !damage || !settlement) throw new InvalidOperationException("请绑定等级表、总伤害、命中率和扣血公式。");
            levels.Read(level, context.DynamicVariableDic);
            return context;
        }

        public void ValidateShares(int count)
        {
            if (segmentPercent.Count != count) throw new InvalidOperationException("段占比数量应等于判定次数 " + count);
            double sum = 0;
            foreach (float percent in segmentPercent)
            {
                if (float.IsNaN(percent) || percent < 0 || percent > 100)
                    throw new InvalidOperationException("各段占比应为 0 到 100 的数值。");
                sum += percent;
            }
            if (Math.Abs(sum - 100) > .005) throw new InvalidOperationException("各段占比之和应为 100%。");
        }
    }
}
