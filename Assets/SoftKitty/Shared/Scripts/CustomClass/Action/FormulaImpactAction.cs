using System;
using System.Collections;
using UnityEngine;

namespace SoftKitty
{
    public sealed class FormulaImpactAction : Action
    {
        public FormulaImpactAction() { }
        private FormulaImpactAction(Action data) : base(data) { }
        public FormulaImpactAction InitAction(Action data) => new FormulaImpactAction(data);

        public static int SegmentCount(Action action)
        {
            if (action.Type == ActionType.Multiple_Time) return Mathf.Max(1, action.Count);
            if (action.Type == ActionType.For_X_Seconds)
                return 1 + Mathf.FloorToInt(Mathf.Max(0, action.KeepTime) / Mathf.Max(.01f, action.Interval));
            return 1;
        }

        public override void Run(GraphInstance instance, GraphRuntimeState runtime)
        {
            Entity caster = SkillPlayback.ReadEntity(this, 1, runtime);
            Entity target = SkillPlayback.ReadEntity(this, 2, runtime);
            if (caster == null || target == null) return;
            try
            {
                if (calculation == null || inputPort[3].connectedNodes.Count != 1)
                    throw new InvalidOperationException("缺少公式配置或技能等级输入。");
                int level = Mathf.RoundToInt(GetVariableValue(inputPort[3].connectedNodes[0], runtime));
                calculation.ValidateShares(SegmentCount(this));
                var context = calculation.Capture(runtime.RootState, caster, target, level);
                var total = Mathf.Max(0, calculation.damage.Evaluate(context));
                context.DynamicVariableDic["hit.total"] = total;
                var playback = SkillPlayback.On(instance);
                runtime.NodeStates[NodeIndex].lastBool = true;
                var weights = calculation.segmentPercent.ToArray();
                playback.ScheduleReleases(runtime, caster, WaitTime, 1, 0, () =>
                {
                    var host = (SkillPlayback.Style)IntValues[0] == SkillPlayback.Style.Direct ? playback : playback.ProjectileHost();
                    host.Schedule(Strike(host, caster, target, context, weights, runtime));
                });
            }
            catch (Exception e) { Debug.LogError(instance.mGraph.name + " · " + Uid + "：" + e.Message, instance); }
        }

        private IEnumerator Strike(SkillPlayback host, Entity source, Entity target, RootRuntimeState context, float[] weights, GraphRuntimeState cast)
        {
            if (!target.AvailableForInteraction) yield break;
            var caster = SkillPlayback.FindActor(source);
            var victim = SkillPlayback.FindActor(target);
            var origin = SkillPlayback.Position(source, caster);
            float distance = Vector3.Distance(origin, SkillPlayback.Position(target, victim));
            if (FloatValues[0] > 0 && distance > FloatValues[0])
            {
                if (BoolValues[0]) host.Signal(caster, origin, "超出射程", .35f, Color.gray);
                yield break;
            }
            var style = (SkillPlayback.Style)IntValues[0];
            if (style != SkillPlayback.Style.Direct)
                yield return host.Flight(style, origin, target, victim, distance / Mathf.Max(.1f, FloatValues[1]), FloatValues[2]);

            bool appliedWeakness = false;
            var vars = context.DynamicVariableDic;
            for (int i = 0; i < weights.Length; i++)
            {
                if (i > 0) yield return new WaitForSeconds(Mathf.Max(.01f, Interval));
                if (!target.AvailableForInteraction || (victim && !victim.isActiveAndEnabled)) yield break;
                if (style == SkillPlayback.Style.Direct && cast.CastInterrupted) yield break;
                vars["hit.index"] = i + 1;
                vars["hit.share"] = weights[i] * .01f;
                vars["hit.raw"] = vars["hit.total"] * vars["hit.share"];
                if (!TryEvaluate(calculation.accuracy, context, host, out var chance)) yield break;
                if (chance <= 0 || (chance < 1 && UnityEngine.Random.value >= chance))
                {
                    host.Signal(victim, SkillPlayback.Position(target, victim), "未命中", .18f, Color.gray);
                    continue;
                }

                var health = target.GetAttributeData("chp");
                if (health == null) yield break;
                if (BoolValues[1] && !appliedWeakness)
                {
                    SkillDefence.Weaken(health, FloatValues[3], FloatValues[4]);
                    host.Signal(victim, SkillPlayback.Position(target, victim), "易伤", FloatValues[4], new Color(.9f,.3f,.85f));
                    appliedWeakness = true;
                }
                if (i == 0 && FloatValues[5] > 0) yield return new WaitForSeconds(FloatValues[5]);
                if (!target.AvailableForInteraction || (victim && !victim.isActiveAndEnabled)) yield break;
                if (style == SkillPlayback.Style.Direct && cast.CastInterrupted) yield break;
                if (!TryEvaluate(calculation.settlement, context, host, out var defended)) yield break;
                float hp = target.GetAttributeFloat("chp");
                float loss = Mathf.Clamp(SkillDefence.Incoming(health, Mathf.Max(0, defended)), 0, Mathf.Max(0, hp));
                if (loss > 0)
                {
                    health.SetValue(hp - loss, source);
                    target.OnEntityEvent("Damage", loss, source, false);
                }
                host.Signal(victim, SkillPlayback.Position(target, victim), "命中", .18f, new Color(1f,.55f,.22f));
            }
        }

        private bool TryEvaluate(SkillFormula formula, RootRuntimeState context, SkillPlayback host, out float value)
        {
            try { value = formula.Evaluate(context); return true; }
            catch (Exception e)
            {
                Debug.LogError(formula.name + " · " + Uid + "：" + e.Message, host);
                value = 0;
                return false;
            }
        }
    }
}
