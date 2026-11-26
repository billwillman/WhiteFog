using UnityEngine;
using System.Collections.Generic;

namespace SoftKitty
{
    // Immutable effective caster attributes for the lifetime of one skill execution.
    public class SkillAttributeSnapshot
    {
        // Install a project-specific snapshot factory at startup if a subclass supplies extra inputs.
        public static System.Func<Entity, SkillAttributeSnapshot> Factory { private get; set; }
        public Entity Dealer { get; }
        private readonly Dictionary<string, float> values = new Dictionary<string, float>();

        protected SkillAttributeSnapshot(Entity dealer)
        {
            Dealer = dealer ?? throw new System.ArgumentNullException(nameof(dealer));
            foreach (var attribute in GameManager.AttributeData.AttributeList)
                if (!attribute.stringValue) values[attribute.uid] = dealer.GetAttributeFloat(attribute.uid);
        }

        public static SkillAttributeSnapshot Capture(Entity dealer)
        {
            var snapshot = Factory == null ? new SkillAttributeSnapshot(dealer) : Factory(dealer);
            if (snapshot == null || snapshot.Dealer != dealer)
                throw new System.InvalidOperationException("属性快照必须属于本次施放者。");
            return snapshot;
        }

        public virtual bool TryGetAttribute(string key, out float value) => values.TryGetValue(key, out value);

        public float GetAttribute(string key) => TryGetAttribute(key, out var value)
            ? value : throw new KeyNotFoundException("技能施放快照缺少属性 " + key);

        internal bool IsSource(Entity entity, NodeBase selector)
        {
            if (selector is EntityClass owner)
            {
                if (owner.Type == EntityType.Dealer) return true;
                if (owner.Type == EntityType.Target) return false;
            }
            return entity == Dealer;
        }
    }

    public struct GraphRuntimeState
    {
        internal bool timedInput;
        internal bool TimesMovementOf(Entity actor) => timedInput && actor == castOwner && !CastInterrupted;
        private Entity castOwner;
        private uint castVersion;
        private CombatRoll? releaseKey;
        public SkillAttributeSnapshot CasterSnapshot
        {
            get => RootState.CasterSnapshot;
            internal set => RootState.CasterSnapshot = value;
        }
        internal bool projectileContact;
        internal System.Action<int, Entity, float, float> onDamageSettled;
        internal SkillDamageReport.Cast replay;
        internal HashSet<GraphObject> nativeGraphPath;

        internal void InheritCastOwner(GraphRuntimeState source)
        {
            castOwner = source.castOwner;
            castVersion = source.castVersion;
        }
        internal CombatRoll EventKey => releaseKey ?? RootState.RandomKey;
        public bool CastInterrupted => castOwner != null && castOwner.CastInterruptionVersion != castVersion;

        internal GraphRuntimeState ForRelease(string nodeUid, uint ordinal)
        {
            var release = this;
            // Shared graph variables retain their once-per-evaluation cache across sibling actions.
            release.releaseKey = EventKey.With("release").With(nodeUid).With(ordinal);
            return release;
        }

        internal void SetContactKey(string targetId)
        {
            RootState.RandomKey = EventKey.With("contact").With(targetId);
            releaseKey = null;
        }

        public GraphRuntimeState ForCast(Entity owner, SkillAttributeSnapshot snapshot = null)
        {
            var cast = this;
            cast.castOwner = owner;
            cast.castVersion = owner?.CastInterruptionVersion ?? 0;
            cast.CasterSnapshot = snapshot ?? (owner == null ? CasterSnapshot : SkillAttributeSnapshot.Capture(owner));
            cast.projectileContact = false;
            // Pooled emitters may acquire a different target and skill level while this timeline waits.
            cast.RootState.InvolvedEntityDic = new Dictionary<string, Entity>(RootState.InvolvedEntityDic);
            cast.RootState.DynamicVariableDic = new Dictionary<string, float>(RootState.DynamicVariableDic);
            return cast;
        }

        internal void EnsureCasterSnapshot(Entity dealer)
        {
            if (dealer != null && CasterSnapshot == null)
                CasterSnapshot = SkillAttributeSnapshot.Capture(dealer);
        }

        internal NodeBase AttributeSelector(NodeBase selector)
        {
            while (selector is Branch branch && branch.Type == BranchType.Entity)
            {
                var links = branch.inputPort[branch.AllConditionMatch(this) ? 1 : 2].connectedNodes;
                if (links.Count == 0) return null;
                selector = links[0];
            }
            return selector;
        }

        public float ReadAttribute(Entity entity, NodeBase selector, string key)
        {
            return CasterSnapshot != null && CasterSnapshot.IsSource(entity, selector)
                ? CasterSnapshot.GetAttribute(key) : entity.GetAttributeFloat(key);
        }

        public int Hash;
        public RootRuntimeState RootState;
        public NodeRuntimeState [] NodeStates;
        public HandlerRuntimeState[] HandlerStates;

        
    }
}
