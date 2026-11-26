using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoftKitty
{
    public sealed class FormulaImpactAction : Action
    {
        public FormulaImpactAction() { }
        private FormulaImpactAction(Action data) : base(data) { }
        public FormulaImpactAction InitAction(Action data) => new FormulaImpactAction(data);

        internal CombatRoll TargetRoll(GraphRuntimeState cast, Entity target)
            => cast.EventKey.With("formulaImpact").With(Uid).With(target.CombatRandomId);

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
                context.RandomKey = TargetRoll(runtime, target).With("damage");
                bool resolvesRecipientOnContact = (SkillPlayback.Style)IntValues[0] != SkillPlayback.Style.Direct &&
                    projectile?.hitRecipients == SkillProjectile.HitRecipients.FirstMatchingEntity;
                var castInputs = resolvesRecipientOnContact ? calculation.damage.CaptureNonTargetAttributes(context) : null;
                if (!resolvesRecipientOnContact)
                    context.DynamicVariableDic["hit.total"] = Mathf.Max(0, calculation.damage.Evaluate(context));
                var playback = SkillPlayback.On(instance);
                runtime.NodeStates[NodeIndex].lastBool = true;
                var weights = calculation.segmentPercent.ToArray();
                playback.ScheduleReleases(runtime, caster, WaitTime, 1, 0, () =>
                {
                    if ((SkillPlayback.Style)IntValues[0] != SkillPlayback.Style.Direct && !ConditionsMet(ref runtime)) return;
                    var host = (SkillPlayback.Style)IntValues[0] == SkillPlayback.Style.Direct ? playback : playback.ProjectileHost();
                    host.Schedule(Strike(host, caster, target, context, weights, runtime, castInputs));
                });
            }
            catch (Exception e) { Debug.LogError(instance.mGraph.name + " · " + Uid + "：" + e.Message, instance); }
        }

        private IEnumerator Strike(SkillPlayback host, Entity source, Entity target, RootRuntimeState context, float[] weights, GraphRuntimeState cast, Dictionary<string, float> castInputs)
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
            {
                EntityComponent struck = null;
                yield return host.Flight(style, origin, target, victim, distance / Mathf.Max(.1f, FloatValues[1]), FloatValues[2], projectile ?? new SkillProjectile(), source, hit => struck = hit);
                if (!struck) yield break;
                victim = struck;
                target = struck.mData;
                context.SetInvolvedEntity("Target", target);
                if (castInputs != null)
                {
                    context.RandomKey = TargetRoll(cast, target).With("damage");
                    if (!TryEvaluate(calculation.damage, context, host, out var total, castInputs)) yield break;
                    context.DynamicVariableDic["hit.total"] = Mathf.Max(0, total);
                }
            }

            bool appliedWeakness = false;
            var vars = context.DynamicVariableDic;
            var targetKey = TargetRoll(cast, target);
            for (int i = 0; i < weights.Length; i++)
            {
                if (i > 0) yield return new WaitForSeconds(Mathf.Max(.01f, Interval));
                if (!SkillPlayback.CanSettle(style, target, victim)) yield break;
                if (style == SkillPlayback.Style.Direct && cast.CastInterrupted) yield break;
                if (style == SkillPlayback.Style.Direct && !ConditionsMet(ref cast, (uint)i)) continue;
                vars["hit.index"] = i + 1;
                vars["hit.share"] = weights[i] * .01f;
                vars["hit.raw"] = vars["hit.total"] * vars["hit.share"];
                var segment = targetKey.With((uint)i);
                context.RandomKey = segment.With("accuracy");
                if (!TryEvaluate(calculation.accuracy, context, host, out var chance)) yield break;
                if (chance <= 0 || (chance < 1 && segment.With("hit").Unit >= chance))
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
                if (!SkillPlayback.CanSettle(style, target, victim)) yield break;
                if (style == SkillPlayback.Style.Direct && cast.CastInterrupted) yield break;
                context.RandomKey = segment.With("settlement");
                if (!TryEvaluate(calculation.settlement, context, host, out var defended)) yield break;
                SkillPlayback.ApplyIncomingDamage(source, target, Mathf.Max(0, defended));
                host.Signal(victim, SkillPlayback.Position(target, victim), "命中", .18f, new Color(1f,.55f,.22f));
            }
        }

        private bool TryEvaluate(SkillFormula formula, RootRuntimeState context, SkillPlayback host, out float value, Dictionary<string, float> castInputs = null)
        {
            try { value = formula.Evaluate(context, castInputs); return true; }
            catch (Exception e)
            {
                Debug.LogError(formula.name + " · " + Uid + "：" + e.Message, host);
                value = 0;
                return false;
            }
        }
    }
}
