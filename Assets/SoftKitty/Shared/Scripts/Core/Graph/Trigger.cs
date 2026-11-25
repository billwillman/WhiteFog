using UnityEngine;
using System.Collections.Generic;


namespace SoftKitty
{
    [System.Serializable]
    public class Trigger : NodeBase
    {
        public List<NodeBase> TriggerConditions = new List<NodeBase>();//Runtime
        public List<NodeBase> ResetConditions = new List<NodeBase>();//Runtime
        public bool AutoReset = true;

        public Trigger()
        {

        }

     

        public Trigger(Root _root)
        {
            mRoot = _root;
            minimized = false;
            Name = "Trigger";
            Uid = "Node" + _root.GetAvailableUid().ToString();
            Position = Vector2.zero;
            AutoReset = true;
            TriggerConditions.Clear();
            ResetConditions.Clear();

            inputPort.Clear();
            outputPort.Clear();
            inputPort.Add(new PortSetting("Conditions To Trigger", "TriggerIn", new string[1] { "ConditionOut" }, true));
            PortSetting _resetPort = new PortSetting("Conditions To Reset", "TriggerIn", new string[1] { "ConditionOut" }, true);
            _resetPort.createByDefault = false;
            inputPort.Add(_resetPort);
            outputPort.Add(new PortSetting("Trigger", "TriggerOut", new string[1] { "RootIn" }, false));
        }

        public Trigger Copy(bool _runtime = false)
        {
            Trigger _copy = new Trigger(mRoot);
            _copy.Name = Name;
            _copy.Uid = Uid;
            _copy.minimized = minimized;
            _copy.Position = Position;
            _copy.inputPort.Clear();
            for (int i = 0; i < inputPort.Count; i++)
            {
                _copy.inputPort.Add(inputPort[i].Copy());
            }
            _copy.outputPort.Clear();
            for (int i = 0; i < outputPort.Count; i++)
            {
                _copy.outputPort.Add(outputPort[i].Copy());
            }
            foreach (var obj in TriggerConditions) _copy.TriggerConditions.Add(obj);
            foreach (var obj in ResetConditions) _copy.ResetConditions.Add(obj);
            _copy.AutoReset = AutoReset;
            if (_runtime) _copy.InitRuntimeNodeName();
            _copy.mType = NodeType.Trigger;
            return _copy;
        }

        public override bool isActive(GraphRuntimeState _runtime)
        {
            if (SGD_Settings.Instance.DebugLevel >= 2) Debug.Log("<Trigger> Checking isActive:" + Uid + "  TriggerConditions.Count:" + TriggerConditions.Count);
            if (_runtime.NodeStates[NodeIndex].lastHash == _runtime.Hash) return _runtime.NodeStates[NodeIndex].lastBool;
            bool _value = true;
            foreach (var obj in TriggerConditions)
            {
                if (!obj.isConditionActive(_runtime)) _value = false;
            }
            ResetRuntime(_runtime);
            return _runtime.NodeStates[NodeIndex].SetHashCache(_runtime.Hash, _value);
        }

        public override void ResetRuntime(GraphRuntimeState _runtime)
        {
            bool _shouldReset = true;
            if (!AutoReset)
            {
                foreach (var obj in ResetConditions)
                {
                    if (!obj.isConditionActive(_runtime)) _shouldReset = false;
                }
            }
            if (_shouldReset)
            {
                if (SGD_Settings.Instance.DebugLevel >= 2) Debug.Log("<Trigger> Reset():" + Uid);
                foreach (var obj in TriggerConditions) obj.ResetRuntime(_runtime);
                foreach (var obj in ResetConditions) obj.ResetRuntime(_runtime);
            }
        }

        public override void Update(GraphRuntimeState _runtime)
        {
            if (SGD_Settings.Instance.DebugLevel >= 3) Debug.Log("<Trigger> Update():" + Uid);
            foreach (var obj in TriggerConditions) obj.Update(_runtime);
            foreach (var obj in ResetConditions) obj.Update(_runtime);
        }

        public override void Init()
        {
            if (SGD_Settings.Instance.DebugLevel >= 3) Debug.Log("<Trigger> Init():" + Uid);
            foreach (var obj in TriggerConditions) obj.Init();
            foreach (var obj in ResetConditions) obj.Init();
        }

        public override void OnDestroy()
        {
            foreach (var obj in TriggerConditions) obj.OnDestroy();
            foreach (var obj in ResetConditions) obj.OnDestroy();
        }
    }
}