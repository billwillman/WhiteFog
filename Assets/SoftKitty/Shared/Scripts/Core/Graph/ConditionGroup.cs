using UnityEngine;
using System.Collections.Generic;


namespace SoftKitty
{

    [System.Serializable]
    public class ConditionGroup : Condition
    {
        public List<NodeBase> Handlers= new List<NodeBase>();//Runtime
        public LogicType Type = LogicType.And;

 
        public ConditionGroup()
        {

        }

        public ConditionGroup(Root _root)
        {
            mRoot = _root;
            Name = "Condition Group";
            Uid = "Node" + _root.GetAvailableUid().ToString();
            minimized = false;
            Position = Vector2.zero;
            Type = LogicType.And;
            Handlers.Clear();
            inputPort.Clear();
            outputPort.Clear();
            inputPort.Add(new PortSetting("Condition", "ConditionIn", new string[1] { "ConditionOut" }, true));
            outputPort.Add(new PortSetting("Condition", "ConditionOut", new string[3] { "TriggerIn", "ConditionIn", "BranchIn" }, true));
        }



        public ConditionGroup Copy(bool _runtime = false)
        {
            ConditionGroup _copy = new ConditionGroup(mRoot);
            _copy.Name = Name;
            _copy.minimized = minimized;
            _copy.Uid = Uid;
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
            foreach (var obj in Handlers) _copy.Handlers.Add(obj);
            _copy.Type = Type;
            if (_runtime) _copy.InitRuntimeNodeName();
            _copy.mType = NodeType.ConditionGroup;
            return _copy;
        }

        public override bool isActive(GraphRuntimeState _runtime)
        {
            if (_runtime.NodeStates[NodeIndex].lastHash == _runtime.Hash) return _runtime.NodeStates[NodeIndex].lastBool;
            bool _value = (Type == LogicType.And || Type == LogicType.Nor) ? true : false;
            int _match = 0;
            foreach (var obj in Handlers)
            {
                obj.Update(_runtime);
                switch (Type)
                {
                    case LogicType.And:
                        if (!obj.isActive(_runtime)) _value = false;
                        break;
                    case LogicType.Or:
                        if (obj.isActive(_runtime)) _value = true;
                        break;
                    case LogicType.Xor:
                        if (obj.isActive(_runtime)) _match++;
                        break;
                    case LogicType.Nor:
                        if (obj.isActive(_runtime)) _value = false;
                        break;
                }
            }
            if (Type == LogicType.Xor) _value = (_match > 0 && _match < Handlers.Count);
            return _runtime.NodeStates[NodeIndex].SetHashCache(_runtime.Hash, _value);

        }
        public override void Update(GraphRuntimeState _runtime)
        {
            foreach (var obj in Handlers) obj.Update(_runtime);
        }

        public override void Init()
        {
            foreach (var obj in Handlers) obj.Init();
        }

        public override void ResetRuntime(GraphRuntimeState _runtime)
        {
            foreach (var obj in Handlers) obj.ResetRuntime(_runtime);
        }

        public override void OnDestroy()
        {
            foreach (var obj in Handlers) obj.OnDestroy();
        }
    }
}