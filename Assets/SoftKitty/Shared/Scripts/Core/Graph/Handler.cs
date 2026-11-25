using UnityEngine;

namespace SoftKitty
{
    [System.Serializable]
    public class Handler : Condition
    {
        public HandlerStateType StateType = HandlerStateType.Default;
        public int xTimeCount = 1;
        public int HandlerIndex = 0;
        protected Handler(Handler _source)
        {
            CopyCustomSetting((CustomNode)_source);
            this.Name = _source.Name;
            this.Uid = _source.Uid;
            this.Position = _source.Position;
            this.minimized = _source.minimized;
            inputPort.Clear();
            for (int i = 0; i < _source.inputPort.Count; i++)
            {
                inputPort.Add(_source.inputPort[i].Copy());
            }
            outputPort.Clear();
            for (int i = 0; i < _source.outputPort.Count; i++)
            {
                outputPort.Add(_source.outputPort[i].Copy());
            }
            this.ClassName = _source.ClassName;
            this.StateType = _source.StateType;
            this.xTimeCount = _source.xTimeCount;
        }

        public Handler()
        {
        }
        public Handler(Root _root)
        {
            mRoot = _root;
            Name = "Condition";
            Uid = "Node" + _root.GetAvailableUid().ToString();
            minimized = false;
            Position = Vector2.zero;
            ClassName = "AttributeValue";
            StateType = HandlerStateType.Default;
            xTimeCount = 1;
            outputPort.Clear();
            outputPort.Add(new PortSetting("Condition", "ConditionOut", new string[3] { "TriggerIn", "ConditionIn", "BranchIn"}, true));
            ResetInputPortSetting();
        }

        public void ResetInputPortSetting()
        {
            inputPort.Clear();
            CustomInit("Handler", ClassName);
        }

        public Handler Copy(bool _runtime = false)
        {
            Handler _copy = new Handler(this);
            if (_runtime) _copy.InitRuntimeNodeName();
            _copy.mType = NodeType.Handler;
            return _copy;
        }

        public override bool isActive(GraphRuntimeState _runtime)
        {
            if (SGD_Settings.Instance.DebugLevel >= 2) Debug.Log("<Handler> isActive():" + Uid);
            if (_runtime.NodeStates[NodeIndex].lastHash == _runtime.Hash) return _runtime.NodeStates[NodeIndex].lastBool;

            if (StateType == HandlerStateType.Switch)
            {
                return _runtime.NodeStates[NodeIndex].SetHashCache(_runtime.Hash, _runtime.HandlerStates[HandlerIndex].Active);
            }
            else if (StateType == HandlerStateType.Keep_1_Frame)
            {
                return _runtime.NodeStates[NodeIndex].SetHashCache(_runtime.Hash, Time.frameCount <= _runtime.HandlerStates[HandlerIndex].LastActiveFrame + 1);
            }
            else if (StateType == HandlerStateType.Keep_5_Frame)
            {
                return _runtime.NodeStates[NodeIndex].SetHashCache(_runtime.Hash, Time.frameCount <= _runtime.HandlerStates[HandlerIndex].LastActiveFrame + 5);
            }
            else if (StateType == HandlerStateType.Keep_1_Second)
            {
                return _runtime.NodeStates[NodeIndex].SetHashCache(_runtime.Hash, Time.time <= _runtime.HandlerStates[HandlerIndex].LastActiveTime + 1F);
            }
            else if (StateType == HandlerStateType.Every_X_Time)
            {
                bool _active = _runtime.HandlerStates[HandlerIndex].TriggeredCount >= xTimeCount;
                if (_runtime.HandlerStates[HandlerIndex].Active) ResetRuntime(_runtime);
                return _runtime.NodeStates[NodeIndex].SetHashCache(_runtime.Hash, _active);
            }
            else if (StateType == HandlerStateType.Default)
            {
                bool _active = _runtime.HandlerStates[HandlerIndex].Active;
                if (_runtime.HandlerStates[HandlerIndex].Active) ResetRuntime(_runtime);
                return _runtime.NodeStates[NodeIndex].SetHashCache(_runtime.Hash, _active);
            }
            return _runtime.NodeStates[NodeIndex].SetHashCache(_runtime.Hash, false);
        }

        public override void ResetRuntime(GraphRuntimeState _runtime)
        {
            _runtime.HandlerStates[HandlerIndex].LastActiveTime = -2F;
            _runtime.HandlerStates[HandlerIndex].LastActiveFrame = -2;
            _runtime.HandlerStates[HandlerIndex].TriggeredCount = 0;
            _runtime.HandlerStates[HandlerIndex].Active = false;
        }

        public override void Init()
        {
            Reset();
        }


        public override void OnDestroy()
        {

        }

        public void SetActive(GraphRuntimeState _runtime,bool _active)
        {
            if (SGD_Settings.Instance.DebugLevel >= 2) Debug.Log("<Handler> SetActive(" + _active + "):" + Uid);
            switch (StateType)
            {
                case HandlerStateType.Switch:
                    _runtime.HandlerStates[HandlerIndex].Active = _active;
                    break;
                case HandlerStateType.Default:
                    _runtime.HandlerStates[HandlerIndex].Active = _active;
                    break;
                case HandlerStateType.Every_X_Time:
                    if (_active)
                    {
                        _runtime.HandlerStates[HandlerIndex].TriggeredCount++;
                        _runtime.HandlerStates[HandlerIndex].LastActiveTime = Time.time;
                        _runtime.HandlerStates[HandlerIndex].LastActiveFrame = Time.frameCount;
                    }
                    break;
                default:
                    if (_active)
                    {
                        _runtime.HandlerStates[HandlerIndex].LastActiveTime = Time.time;
                        _runtime.HandlerStates[HandlerIndex].LastActiveFrame = Time.frameCount;
                    }
                    break;
            }
        }


    }
}