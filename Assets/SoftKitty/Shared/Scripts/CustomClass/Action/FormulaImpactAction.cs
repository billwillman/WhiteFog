using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Kind = SoftKitty.SkillDamageFlow.Kind;
using Stage = SoftKitty.SkillDamageFlow.Stage;

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
                var context = calculation.Capture(runtime.RootState, caster, target, level);
                int count = SegmentCount(this);
                GraphObject nativeGraph = calculation.EffectiveMode == SkillCalculation.Mode.NativeGraph ? calculation.nativeGraph : null;
                IReadOnlyList<Stage> stages = null;
                float[] weights = null;
                var style = (SkillPlayback.Style)IntValues[0];
                if (nativeGraph)
                {
                    if (nativeGraph == instance.mGraph || runtime.nativeGraphPath?.Contains(nativeGraph) == true)
                        throw new InvalidOperationException("检测到 MCC 伤害图循环调用：" + nativeGraph.name);
                    runtime.nativeGraphPath = runtime.nativeGraphPath == null ? new HashSet<GraphObject>() :
                        new HashSet<GraphObject>(runtime.nativeGraphPath);
                    runtime.nativeGraphPath.Add(instance.mGraph);
                    calculation.ValidateShares(count);
                    weights = calculation.segmentPercent.ToArray();
                }
                else
                {
                    stages = calculation.ActiveStages();
                    foreach (var stage in stages)
                        if (stage.kind == Kind.SegmentShare)
                        {
                            calculation.ValidateShares(count);
                            weights = calculation.segmentPercent.ToArray();
                            break;
                        }
                }
                var playback = SkillPlayback.On(instance);
                var skill = instance.mGraph;
                runtime.NodeStates[NodeIndex].lastBool = true;
                playback.ScheduleReleases(runtime, caster, WaitTime, 1, 0, () =>
                {
                    if (style != SkillPlayback.Style.Direct && !ConditionsMet(ref runtime)) return;
                    var host = style == SkillPlayback.Style.Direct ? playback : playback.ProjectileHost();
                    if (style != SkillPlayback.Style.Direct) runtime.BeginTracedFlight(Uid);
                    if (style == SkillPlayback.Style.Projectile)
                    {
                        var flight = runtime;
                        flight.RootState = context;
                        ProjectileAction.LaunchImpact(this, skill, host, runtime, caster, target, (hit, point) =>
                        {
                            var arrival = flight.ForCast(null);
                            arrival.RootState.SetInvolvedEntity("Target", hit);
                            host.Schedule(Strike(host, caster, hit, arrival.RootState, stages, count, weights, arrival, nativeGraph, point));
                        });
                        return;
                    }
                    var confirmed = EmitsSettledHit ? ProjectileAction.ContactHandler(this, skill, host, runtime, caster, null) : null;
                    host.Schedule(Strike(host, caster, target, context, stages, count, weights, runtime, nativeGraph, confirmed: confirmed));
                });
            }
            catch (Exception e) { Debug.LogError(instance.mGraph.name + " · " + Uid + "：" + e.Message, instance); }
        }

        private IEnumerator Strike(SkillPlayback host, Entity source, Entity target, RootRuntimeState context,
            IReadOnlyList<Stage> stages, int count, float[] weights, GraphRuntimeState cast, GraphObject nativeGraph,
            Vector3? contactPoint = null, System.Action<Entity, Vector3> confirmed = null)
        {
            if (!target.AvailableForInteraction) yield break;
            var caster = SkillPlayback.FindActor(source);
            var victim = SkillPlayback.FindActor(target);
            var origin = SkillPlayback.Position(source, caster);
            float distance = Vector3.Distance(origin, SkillPlayback.Position(target, victim));
            if (!contactPoint.HasValue && FloatValues[0] > 0 && distance > FloatValues[0])
            {
                if (BoolValues[0]) host.Signal(caster, origin, "超出射程", .35f, Color.gray);
                yield break;
            }
            var style = (SkillPlayback.Style)IntValues[0];
            if (!contactPoint.HasValue && style != SkillPlayback.Style.Direct)
            {
                EntityComponent struck = null;
                yield return host.Flight(style, origin, target, victim, distance / Mathf.Max(.1f, FloatValues[1]), FloatValues[2], projectile ?? new SkillProjectile(), source, hit => struck = hit);
                if (!struck) yield break;
                cast.trace?.Add(SkillCastTrace.Event.Contact, Uid, struck.mData, cast);
                victim = struck;
                target = struck.mData;
                context.SetInvolvedEntity("Target", target);
            }

            bool appliedWeakness = false;
            var vars = context.DynamicVariableDic;
            var targetKey = TargetRoll(cast, target);
            int firstConfirmedStage = 1;
            bool vulnerabilityEnabled = false;
            for (int s = 1; stages != null && s < stages.Count; s++)
            {
                if (stages[s].kind == Kind.HitChance) firstConfirmedStage = s + 1;
                if (stages[s].kind == Kind.Vulnerability) vulnerabilityEnabled = true;
            }
            IReadOnlyList<SkillDamageReport.Entry> records = null;
            if (stages != null && stages[0].kind == Kind.ReportedResult)
            {
                if (!SkillPlayback.CanSettle(style, target, victim) || cast.replay == null) yield break;
                if (style == SkillPlayback.Style.Direct && (cast.CastInterrupted || !ConditionsMet(ref cast))) yield break;
                records = cast.replay.Take(calculation.logicIndex, target.CombatRandomId);
                count = records.Count;
            }
            for (int i = 0; i < count; i++)
            {
                if (records == null && i > 0) yield return new WaitForSeconds(Mathf.Max(.01f, Interval));
                if (!SkillPlayback.CanSettle(style, target, victim)) yield break;
                if (style == SkillPlayback.Style.Direct && cast.CastInterrupted) yield break;
                if (records == null && style == SkillPlayback.Style.Direct && !ConditionsMet(ref cast, (uint)i)) continue;
                int index = calculation.independentSegment ? calculation.logicIndex + i : i + 1;
                vars["hit.index"] = index;
                if (nativeGraph)
                {
                    vars["hit.share"] = weights[i] * .01f;
                    var point = contactPoint ?? SkillPlayback.Position(target, victim);
                    if (style == SkillPlayback.Style.Direct && cast.projectileContact && cast.RootState.RootInstance)
                        point = cast.RootState.RootInstance.transform.position;
                    var nativeCast = cast;
                    if (nativeCast.trace != null && nativeCast.traceOwner == null)
                    {
                        nativeCast.traceOwner = Uid;
                        nativeCast.traceSegment = index;
                    }
                    CombatDamage.RunImpactGraph(nativeGraph, nativeCast, context, host, point, source, target,
                        targetKey.With((uint)i).With("mcc"), style != SkillPlayback.Style.Direct);
                    continue;
                }
                vars["hit.share"] = 1;
                float value;
                if (records != null) value = records[i].healthBefore - records[i].healthAfter;
                else
                {
                    context.RandomKey = targetKey.With("damage");
                    if (!TryEvaluate(stages[0].formula, context, host, out value, cast.CasterSnapshot)) yield break;
                    vars["hit.total"] = value = Mathf.Max(0, value);
                }
                var segment = targetKey.With((uint)i);
                var health = target.GetAttributeData("chp");
                if (health == null) yield break;
                float absorbed = records != null ? records[i].shieldSpent : 0;
                for (int s = 1; s < stages.Count; s++)
                {
                    if (s == firstConfirmedStage)
                    {
                        if (vulnerabilityEnabled && BoolValues[1] && !appliedWeakness)
                        {
                            SkillDefence.Weaken(health, FloatValues[3], FloatValues[4]);
                            host.Signal(victim, SkillPlayback.Position(target, victim), "易伤", FloatValues[4], new Color(.9f,.3f,.85f));
                            appliedWeakness = true;
                        }
                        if (i == 0 && FloatValues[5] > 0) yield return new WaitForSeconds(FloatValues[5]);
                        if (!SkillPlayback.CanSettle(style, target, victim)) yield break;
                        if (style == SkillPlayback.Style.Direct && cast.CastInterrupted) yield break;
                    }

                    var stage = stages[s];
                    vars["hit.raw"] = vars["hit.value"] = value;
                    context.RandomKey = segment.With(stage.id);
                    if (stage.kind == Kind.HitChance)
                    {
                        if (!TryEvaluate(stage.formula, context, host, out var chance, cast.CasterSnapshot)) yield break;
                        if (chance <= 0 || (chance < 1 && segment.With("hit").Unit >= chance))
                        {
                            host.Signal(victim, SkillPlayback.Position(target, victim), "未命中", .18f, Color.gray);
                            break;
                        }
                    }
                    else if (stage.kind == Kind.SegmentShare)
                    {
                        vars["hit.share"] = weights[i] * .01f;
                        value *= vars["hit.share"];
                    }
                    else if (stage.kind == Kind.ModifyDamage)
                    {
                        if (stage.conditionChance)
                        {
                            var modifierKey = context.RandomKey;
                            context.RandomKey = modifierKey.With("condition");
                            if (!TryEvaluate(stage.conditionChance, context, host, out var probability, cast.CasterSnapshot)) yield break;
                            if (probability <= 0 || (probability < 1 && modifierKey.With("apply").Unit >= probability)) continue;
                            context.RandomKey = modifierKey;
                        }
                        if (!TryEvaluate(stage.formula, context, host, out value, cast.CasterSnapshot)) yield break;
                    }
                    else if (stage.kind == Kind.Vulnerability)
                        value = SkillDefence.ApplyVulnerability(health, Mathf.Max(0, value));
                    else if (stage.kind == Kind.Shield)
                    {
                        // Commit shield consumption only after all downstream formulas succeed.
                        absorbed = SkillDefence.ShieldAbsorption(health, value);
                        value -= absorbed;
                    }
                    else if (stage.kind == Kind.ApplyHealth)
                    {
                        float before = cast.trace == null && cast.onDamageSettled == null ? 0 : target.GetAttributeFloat("chp");
                        SkillDefence.SpendShield(health, absorbed);
                        SkillPlayback.ApplySettledDamage(source, target, health, value);
                        cast.TraceLoss(Uid, target, before, index);
                        cast.onDamageSettled?.Invoke(index, target, before - target.GetAttributeFloat("chp"), absorbed);
                        confirmed?.Invoke(target, SkillPlayback.Position(target, victim));
                        host.Signal(victim, SkillPlayback.Position(target, victim), "命中", .18f, new Color(1f,.55f,.22f));
                    }
                }
            }
        }

        private bool TryEvaluate(SkillFormula formula, RootRuntimeState context, SkillPlayback host, out float value, SkillAttributeSnapshot casterSnapshot)
        {
            try { value = formula.EvaluateWithAttributes(context, casterSnapshot); return true; }
            catch (Exception e)
            {
                Debug.LogError((formula ? formula.name : "缺少公式") + " · " + Uid + "：" + e.Message, host);
                value = 0;
                return false;
            }
        }
    }
}
