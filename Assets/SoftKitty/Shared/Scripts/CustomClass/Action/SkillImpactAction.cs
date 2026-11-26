using System.Collections;
using UnityEngine;

namespace SoftKitty
{
    // MCC resolves InitAction by the class name serialized in the graph.
    public sealed class SkillImpactAction : Action
    {
        public SkillImpactAction() { }
        private SkillImpactAction(Action data) : base(data) { }
        public SkillImpactAction InitAction(Action data) => new SkillImpactAction(data);

        public override void Run(GraphInstance instance, GraphRuntimeState runtime)
        {
            // Resolve per-cast inputs before yielding. One emitter may be used again next frame.
            var source = SkillPlayback.ReadEntity(this, 1, runtime);
            var recipient = SkillPlayback.ReadEntity(this, 2, runtime);
            if (source == null || recipient == null || inputPort[3].connectedNodes.Count == 0) return;
            runtime.NodeStates[NodeIndex].lastBool = true;
            var host = SkillPlayback.On(instance);
            host.ScheduleReleases(runtime, source, WaitTime, 1, 0, () =>
            {
                if ((SkillPlayback.Style)IntValues[0] != SkillPlayback.Style.Direct && !ConditionsMet(ref runtime)) return;
                var playback = (SkillPlayback.Style)IntValues[0] == SkillPlayback.Style.Direct ? host : host.ProjectileHost();
                playback.Schedule(Strike(playback, source, recipient, runtime));
            });
        }

        private IEnumerator Strike(SkillPlayback host, Entity source, Entity recipient, GraphRuntimeState cast)
        {
            if (!recipient.AvailableForInteraction) yield break;
            var caster = SkillPlayback.FindActor(source);
            var victim = SkillPlayback.FindActor(recipient);
            Vector3 start = SkillPlayback.Position(source, caster);
            float distance = Vector3.Distance(start, SkillPlayback.Position(recipient, victim));
            if (FloatValues[0] > 0 && distance > FloatValues[0])
            {
                if (BoolValues[0]) host.Signal(caster, start, "超出射程", .35f, Color.gray);
                yield break;
            }

            var style = (SkillPlayback.Style)IntValues[0];
            if (style != SkillPlayback.Style.Direct)
            {
                EntityComponent struck = null;
                yield return host.Flight(style, start, recipient, victim, distance / Mathf.Max(.1f, FloatValues[1]), FloatValues[2], projectile ?? new SkillProjectile(), source, hit => struck = hit);
                if (!struck) yield break;
                victim = struck;
                recipient = struck.mData;
                cast.RootState = cast.ForCast(null).RootState;
                cast.RootState.SetInvolvedEntity("Target", recipient);
            }
            if (!SkillPlayback.CanSettle(style, recipient, victim)) yield break;
            var health = recipient.GetAttributeData("chp");
            if (health == null) yield break;
            if (BoolValues[1])
            {
                SkillDefence.Weaken(health, FloatValues[3], FloatValues[4]);
                host.Signal(victim, SkillPlayback.Position(recipient, victim), "易伤", FloatValues[4], new Color(.9f,.3f,.85f));
            }
            if (FloatValues[5] > 0) yield return new WaitForSeconds(FloatValues[5]);
            float interval = Mathf.Max(.01f, Interval);
            int count = FormulaImpactAction.SegmentCount(this);
            for (int i = 0; i < count && recipient.AvailableForInteraction; i++)
            {
                if (i > 0) yield return new WaitForSeconds(interval);
                if (!SkillPlayback.CanSettle(style, recipient, victim)) yield break;
                if (style == SkillPlayback.Style.Direct && cast.CastInterrupted) yield break;
                if (style == SkillPlayback.Style.Direct && !ConditionsMet(ref cast, (uint)i)) continue;
                cast.Hash = NextExecutionHash();
                float amount = GetVariableValue(inputPort[3].connectedNodes[0], cast);
                SkillPlayback.ApplyIncomingDamage(source, recipient, Mathf.Max(0, amount));
                host.Signal(victim, SkillPlayback.Position(recipient, victim), "命中", .18f, new Color(1f,.55f,.22f));
            }
        }
    }
}
