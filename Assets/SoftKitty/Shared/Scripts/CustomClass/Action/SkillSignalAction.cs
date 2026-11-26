using UnityEngine;

namespace SoftKitty
{
    public sealed class SkillSignalAction : Action
    {
        public SkillSignalAction() { }
        private SkillSignalAction(Action data) : base(data) { }
        public SkillSignalAction InitAction(Action data) => new SkillSignalAction(data);
        public override void Execute(GraphRuntimeState runtime)
        {
            var entity = SkillPlayback.ReadEntity(this, 1, runtime);
            if (entity == null) return;
            var actor = SkillPlayback.FindActor(entity);
            var host = SkillPlayback.On(runtime.RootState.RootInstance.GetComponent<GraphInstance>());
            host.Signal(actor, SkillPlayback.Position(entity, actor), StringValues[0], FloatValues[0],
                IntValues[0] == 0 ? new Color(.3f,1f,.86f) : new Color(1f,.35f,.18f));
            if (BoolValues.Count == 0 || !BoolValues[0])
                entity.OnEntityEvent(StringValues[0], 0, runtime.RootState.GetInvolvedEntity("Dealer"), false);
        }
    }
}
