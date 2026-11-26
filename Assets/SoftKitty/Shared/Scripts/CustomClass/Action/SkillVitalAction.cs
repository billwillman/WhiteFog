using UnityEngine;

namespace SoftKitty
{
    public sealed class SkillVitalAction : Action
    {
        public SkillVitalAction() { }
        private SkillVitalAction(Action data) : base(data) { }
        public SkillVitalAction InitAction(Action data) => new SkillVitalAction(data);
        public override void Execute(GraphRuntimeState runtime)
        {
            var entity = SkillPlayback.ReadEntity(this, 1, runtime);
            if (entity == null || !entity.AvailableForInteraction || inputPort[2].connectedNodes.Count == 0) return;
            var health = entity.GetAttributeData("chp");
            if (health == null) return;
            float amount = Mathf.Max(0, GetVariableValue(inputPort[2].connectedNodes[0], runtime));
            var actor = SkillPlayback.FindActor(entity);
            var host = SkillPlayback.On(runtime.RootState.RootInstance.GetComponent<GraphInstance>());
            if (IntValues[0] == 0)
            {
                SkillDefence.Shield(health, amount, FloatValues[0]);
                host.Signal(actor, SkillPlayback.Position(entity, actor), "护盾", FloatValues[0], Color.cyan);
            }
            else
            {
                float value = Mathf.Min(entity.GetAttributeFloat("hp"), entity.GetAttributeFloat("chp") + amount);
                health.SetValue(value, runtime.RootState.GetInvolvedEntity("Dealer"));
                host.Signal(actor, SkillPlayback.Position(entity, actor), "回复", .4f, Color.green);
            }
        }
    }
}
