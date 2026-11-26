using UnityEngine;
using System.Collections.Generic;

namespace SoftKitty
{
    public struct GraphRuntimeState
    {
        private Entity castOwner;
        private uint castVersion;
        private CombatRoll? releaseKey;
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

        public GraphRuntimeState ForCast(Entity owner)
        {
            var cast = this;
            cast.castOwner = owner;
            cast.castVersion = owner?.CastInterruptionVersion ?? 0;
            // Pooled emitters may acquire a different target and skill level while this timeline waits.
            cast.RootState.InvolvedEntityDic = new Dictionary<string, Entity>(RootState.InvolvedEntityDic);
            cast.RootState.DynamicVariableDic = new Dictionary<string, float>(RootState.DynamicVariableDic);
            return cast;
        }

        public int Hash;
        public RootRuntimeState RootState;
        public NodeRuntimeState [] NodeStates;
        public HandlerRuntimeState[] HandlerStates;

        
    }
}
