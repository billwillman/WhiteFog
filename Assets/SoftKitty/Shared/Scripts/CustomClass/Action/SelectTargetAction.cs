using System.Collections.Generic;
using UnityEngine.Scripting;

namespace SoftKitty
{
    [Preserve] public sealed class SelectTargetAction : Action
    {
        public SelectTargetAction() { }
        private SelectTargetAction(Action data) : base(data) { }
        [Preserve] public SelectTargetAction InitAction(Action data) => new SelectTargetAction(data);

        // Keep this contact alive only for a branch that can still redirect its projectile.
        private static bool CanSteer(NodeBase node, HashSet<NodeBase> seen)
        {
            if (node is RedirectProjectileAction) return true;
            if (!(node is SelectTargetAction || node is Branch { Type: BranchType.Action }) || !seen.Add(node))
                return false;
            foreach (var port in node.outputPort)
                foreach (var next in port.connectedNodes)
                    if (CanSteer(next, seen)) return true;
            return false;
        }

        public override void Run(GraphInstance instance, GraphRuntimeState state)
        {
            state.selectionInstance = instance;
            var contact = state.projectileHit;
            if (contact == null || !CanSteer(this, new HashSet<NodeBase>())) { base.Run(instance, state); return; }
            contact.Hold();
            RunTimed(instance, state, contact.Release);
        }

        public override void Execute(GraphRuntimeState state)
        {
            state.selectedTarget = selectionRules?.Pick(state);
            if (state.selectedTarget == null) return;
            var instance = state.selectionInstance ? state.selectionInstance : state.RootState.RootInstance.GetComponent<GraphInstance>();
            foreach (var next in outputPort[0].connectedNodes) next.Run(instance, state);
        }
    }
}
