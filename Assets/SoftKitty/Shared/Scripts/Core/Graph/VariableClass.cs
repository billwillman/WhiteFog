using UnityEngine;
using System.Collections.Generic;


namespace SoftKitty
{
    [System.Serializable]
    public class VariableClass : NodeBase
    {
        public VariableType Type = VariableType.Constant;
        public float Constant = 0F;
        public string AttributeKey = "Attack";
        public string VariableUid = "";
        public float Max = 1F;


        public VariableClass()
        {

        }
        public VariableClass(Root _root)
        {
            mRoot = _root;
            minimized = false;
            Name = "Variable";
            Uid = "Node" + _root.GetAvailableUid().ToString();
            Position = Vector2.zero;
            Type = VariableType.Constant;
            Constant = 0F;
            Max = 1F;
            AttributeKey = "Attack";
            VariableUid = "";

            inputPort.Clear();
            outputPort.Clear();
            outputPort.Add(new PortSetting("Variable", "Variable", new string[4] { "Variable", "MathIn", "CustomVariable", "BranchInValue" }, true));

            PortSetting _entityPort = new PortSetting("Entity", "EntityIn", new string[1] { "EntityOut" }, false);
            _entityPort.createByDefault = false;
            inputPort.Add(_entityPort);
        }

        public VariableClass Copy(bool _runtime = false)
        {
            VariableClass _copy = new VariableClass(mRoot);
            _copy.Name = Name;
            _copy.Uid = Uid;
            _copy.Position = Position;
            _copy.minimized = minimized;
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
            _copy.Type = Type;
            _copy.Constant = Constant;
            _copy.Max = Max;
            _copy.AttributeKey = AttributeKey;
            _copy.VariableUid = VariableUid;
            if (_runtime) _copy.InitRuntimeNodeName();
            _copy.mType = NodeType.Variable;
            return _copy;
        }


        public void SetValue(float _value)
        {
            Constant = _value;
        }

        public void SetRandomRange(float _min, float _max)
        {
            Constant = _min;
            Max = _max;
        }

        public float Value(GraphRuntimeState _runtime)
        {
            if (_runtime.NodeStates[NodeIndex].lastHash == _runtime.Hash) return _runtime.NodeStates[NodeIndex].lastFloat;
            switch (Type)
            {
                case VariableType.Constant:
                    return _runtime.NodeStates[NodeIndex].SetHashCache(_runtime.Hash, Constant);
                case VariableType.Attribute:
                    if (inputPort[0].uid.Count > 0)
                    {
                        return _runtime.NodeStates[NodeIndex].SetHashCache(_runtime.Hash, inputPort[0].connectedNodes[0].GetEntity(_runtime).GetAttributeFloat(AttributeKey));
                    }
                    else
                    {
                        return _runtime.NodeStates[NodeIndex].SetHashCache(_runtime.Hash, GameManager.GetPlayer().GetAttributeFloat(AttributeKey));
                    }
                case VariableType.DynamicVariable:
                    if (_runtime.RootState.DynamicVariableDic.ContainsKey(VariableUid)) return _runtime.NodeStates[NodeIndex].SetHashCache(_runtime.Hash, _runtime.RootState.DynamicVariableDic[VariableUid]);
                    return _runtime.NodeStates[NodeIndex].SetHashCache(_runtime.Hash, Constant);
                case VariableType.Random:
                    return _runtime.NodeStates[NodeIndex].SetHashCache(_runtime.Hash,
                        _runtime.RootState.RandomKey.With("variable").With(Uid).Range(Constant, Max));
            }
            return _runtime.NodeStates[NodeIndex].SetHashCache(_runtime.Hash, 0F);
        }
    }
}
