using UnityEngine;


namespace SoftKitty
{
    public struct HandlerRuntimeState
    {
        public int TriggeredCount;
        public bool Active;
        public int LastActiveFrame;
        public float LastActiveTime;
        public HandlerRuntimeState(bool _useDefault){
            if (_useDefault)
            {
                TriggeredCount = 0;
                Active = false;
                LastActiveFrame = -2;
                LastActiveTime = -2F;
            }
            else
            {
                TriggeredCount = 0;
                Active = false;
                LastActiveFrame = 0;
                LastActiveTime = 0F;
            }
        }
    }
}
