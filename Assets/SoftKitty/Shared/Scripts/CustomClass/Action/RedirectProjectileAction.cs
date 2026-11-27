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
        private readonly IReadOnlyList<Entity> targets;
        internal ProjectileContact(Entity candidate, HashSet<Entity> visited, IReadOnlyList<Entity> targets)
        {
            this.candidate = candidate;
            this.visited = visited;
            this.targets = targets;
        }
        internal Entity NextTarget => Eligible(candidate) ? candidate : null;
        internal bool CanRedirect => targets != null && visited.Count < targets.Count;
        internal bool Waiting => destination == null && pending > 0 && CanRedirect;
        internal void Hold() => pending++;
        internal void Release() => pending--;
        internal void Redirect(Entity to)
        {
            if (destination != null || !Eligible(to)) return;
            foreach (var entry in targets)
                if (entry == to) { destination = to; return; }
        }
        private bool Eligible(Entity to) => targets != null && to != null && !visited.Contains(to) &&
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
            if (contact == null || !contact.CanRedirect) return;
            contact.Hold();
            RunTimed(instance, state, contact.Release);
        }

        public override void Execute(GraphRuntimeState state) => state.projectileHit?.Redirect(SkillPlayback.ReadEntity(this, 1, state));
    }
}
