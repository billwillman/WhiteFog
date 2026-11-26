using UnityEngine;
using System.Collections.Generic;

namespace SoftKitty
{
    public struct GraphRuntimeState
    {
        private Entity castOwner;
        private uint castVersion;
        public bool CastInterrupted => castOwner != null && castOwner.CastInterruptionVersion != castVersion;

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
