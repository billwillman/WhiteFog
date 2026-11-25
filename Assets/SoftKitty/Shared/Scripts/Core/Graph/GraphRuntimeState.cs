using UnityEngine;
using System.Collections.Generic;

namespace SoftKitty
{
    public struct GraphRuntimeState
    {
        public int Hash;
        public RootRuntimeState RootState;
        public NodeRuntimeState [] NodeStates;
        public HandlerRuntimeState[] HandlerStates;

        
    }
}
