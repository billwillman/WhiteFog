using System.Collections.Generic;

namespace SoftKitty
{
    // Each contact owns its own decision. A delayed branch from an earlier contact cannot steer a later leg.
    internal sealed class ProjectileContact
    {
        private readonly Entity candidate;
        private readonly HashSet<Entity> visited;
        private int pending;
        private Entity destination;
        internal System.Action<Entity, UnityEngine.Vector3, ProjectileContact> OnNextHit { get; private set; }
        internal string RedirectNode { get; private set; }
        internal string RedirectTrigger { get; private set; }
        internal string ArrivedVia, ArrivedViaTrigger;
        private readonly IReadOnlyList<Entity> targets;
        internal ProjectileContact(Entity candidate, HashSet<Entity> visited, IReadOnlyList<Entity> targets)
        {
            this.candidate = candidate;
            this.visited = visited;
            this.targets = targets;
        }
        internal bool HasHit(Entity entity) => visited != null && visited.Contains(entity);
        internal Entity NextTarget => targets != null && Eligible(candidate) ? candidate : null;
        internal bool CanRedirect => targets != null && visited.Count < targets.Count;
        internal bool Waiting => destination == null && pending > 0;
        internal void Hold() => pending++;
        internal void Release() => pending--;
        internal bool Redirect(Entity to, bool ruleSelected)
        {
            if (destination != null || !Eligible(to)) return false;
            if (!ruleSelected && (targets == null || !System.Linq.Enumerable.Contains(targets, to))) return false;
            destination = to;
            return true;
        }

        internal void BindArrival(string node, string trigger, System.Action<Entity, UnityEngine.Vector3, ProjectileContact> handler)
        {
            RedirectNode = node;
            RedirectTrigger = trigger;
            OnNextHit = handler;
        }
        private bool Eligible(Entity to) => to != null && !HasHit(to) &&
            SkillProjectile.IsLive(SkillPlayback.FindActor(to));
        internal EntityComponent TakeTarget() => Eligible(destination) ? SkillPlayback.FindActor(destination) : null;
    }

    [UnityEngine.Scripting.Preserve]
    public sealed class RedirectProjectileAction : Action
    {
        public RedirectProjectileAction() { }
        private RedirectProjectileAction(Action data) : base(data) { }
        [UnityEngine.Scripting.Preserve]
        public RedirectProjectileAction InitAction(Action data) => new RedirectProjectileAction(data);

        public override void Run(GraphInstance instance, GraphRuntimeState state)
        {
            var contact = state.projectileHit;
            if (contact == null || (!contact.CanRedirect && state.selectedTarget == null)) return;
            contact.Hold();
            RunTimed(instance, state, contact.Release);
        }

        public override void Execute(GraphRuntimeState state)
        {
            var target = SkillPlayback.ReadEntity(this, 1, state);
            var contact = state.projectileHit;
            if (contact == null || !contact.Redirect(target, target != null && target == state.selectedTarget)) return;
            if (outputPort.Count == 0 || outputPort[0].connectedNodes.Count == 0)
            {
                if (state.trace != null) contact.BindArrival(Uid, state.traceTrigger, null);
                return;
            }
            var instance = state.RootState.RootInstance.GetComponent<GraphInstance>();
            var host = SkillPlayback.On(instance).ProjectileHost();
            contact.BindArrival(Uid, state.traceTrigger, ProjectileAction.BuildContactHandler(this, instance.mGraph,
                host, state, state.RootState.GetInvolvedEntity("Dealer"), null));
        }
    }
}
