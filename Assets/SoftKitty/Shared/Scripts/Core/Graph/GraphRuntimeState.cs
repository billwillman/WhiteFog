using UnityEngine;
using System.Collections.Generic;

namespace SoftKitty
{
    // Immutable effective caster attributes for the lifetime of one skill execution.
    public class SkillAttributeSnapshot
    {
        // Install a project-specific snapshot factory at startup if a subclass supplies extra inputs.
        public static System.Func<Entity, SkillAttributeSnapshot> Factory { private get; set; }
        public ulong DealerId { get; }
        private readonly Dictionary<string, float> values = new Dictionary<string, float>();

        protected SkillAttributeSnapshot(Entity dealer)
        {
            if (dealer == null) throw new System.ArgumentNullException(nameof(dealer));
            DealerId = dealer.RuntimeId;
            foreach (var attribute in GameManager.AttributeData.AttributeList)
                if (!attribute.stringValue) values[attribute.uid] = dealer.GetAttributeFloat(attribute.uid);
        }

        public static SkillAttributeSnapshot Capture(Entity dealer)
        {
            var snapshot = Factory == null ? new SkillAttributeSnapshot(dealer) : Factory(dealer);
            if (snapshot == null || dealer == null || snapshot.DealerId != dealer.RuntimeId)
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
            return entity != null && entity.RuntimeId == DealerId;
        }
    }

    public struct GraphRuntimeState
    {
        private Entity castOwner;
        private uint castVersion;
        private CombatRoll? releaseKey;
        public SkillAttributeSnapshot CasterSnapshot
        {
            get => RootState.CasterSnapshot;
            internal set => RootState.CasterSnapshot = value;
        }
        internal IReadOnlyList<Entity> orderedTargets;
        internal ProjectileContact projectileHit;
        internal Entity selectedTarget;
        internal GraphInstance selectionInstance;
        internal HashSet<Entity> castProjectileHits;
        internal bool projectileContact;
        internal System.Action<int, Entity, float, float> onDamageSettled;
        internal SkillCastTrace trace;
        internal string traceOwner, traceTrigger, traceEmitter;
        internal int traceFlight, traceSegment;

        internal void BeginTracedFlight(string node)
        {
            if (trace == null) return;
            traceEmitter = traceOwner ?? node;
            traceFlight = trace.NextFlight();
        }

        internal void TraceLoss(string node, Entity target, float before, int segment = 0)
        {
            if (trace == null) return;
            float loss = before - target.GetAttributeFloat("chp");
            if (loss > 0) trace.Add(SkillCastTrace.Event.HealthLoss, node, target, this, traceSegment > 0 ? traceSegment : segment, loss);
        }

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

        internal GraphRuntimeState ForSelectedTarget(Entity target, string nodeUid, int encounterOrder)
        {
            var branch = this;
            branch.selectedTarget = target;
            branch.releaseKey = EventKey.With("select").With(nodeUid).With(target.CombatRandomId).With((uint)encounterOrder);
            branch.RootState.InvolvedEntityDic = new Dictionary<string, Entity>(RootState.InvolvedEntityDic);
            branch.RootState.DynamicVariableDic = new Dictionary<string, float>(RootState.DynamicVariableDic);
            branch.HandlerStates = (HandlerRuntimeState[])HandlerStates.Clone();
            branch.NodeStates = (NodeRuntimeState[])NodeStates.Clone();
            for (int i = 0; i < branch.NodeStates.Length; i++) branch.NodeStates[i].lastHash = ~Hash;
            return branch;
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
            if (CasterSnapshot != null && CasterSnapshot.IsSource(entity, selector)) return CasterSnapshot.GetAttribute(key);
            AttributeObject.RequireDefinition(key, selector?.Uid ?? "属性读取");
            return entity.GetAttributeFloat(key);
        }

        public int Hash;
        public RootRuntimeState RootState;
        public NodeRuntimeState [] NodeStates;
        public HandlerRuntimeState[] HandlerStates;

        
    }
}
