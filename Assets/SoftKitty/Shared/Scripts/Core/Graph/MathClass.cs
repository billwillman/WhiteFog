using UnityEngine;


namespace SoftKitty
{
    [System.Serializable]
    public class MathClass : NodeBase
    {
        public MathType Type = MathType.Add;
        public float ValueA(GraphRuntimeState _runtime)
        {
                return inputPort[0].connectedNodes[0].GetVariableValue(_runtime);
        }
        public float ValueB(GraphRuntimeState _runtime)
        {
                return inputPort[1].connectedNodes[0].GetVariableValue(_runtime);
        }
        public float ValueC(GraphRuntimeState _runtime)
        {
                return inputPort[2].connectedNodes[0].GetVariableValue(_runtime);
        }

        public MathClass()
        {

        }



        public MathClass(Root _root)
        {
            mRoot = _root;
            minimized = false;
            Name = "Math";
            Uid = "Node" + _root.GetAvailableUid().ToString();
            Position = Vector2.zero;
            Type = MathType.Add;

            inputPort.Clear();
            outputPort.Clear();
            inputPort.Add(new PortSetting("A", "MathIn", new string[1] { "Variable" }, false));
            inputPort.Add(new PortSetting("B", "MathIn", new string[1] { "Variable" }, false));
            inputPort.Add(new PortSetting("C", "MathIn", new string[1] { "Variable" }, false));
            outputPort.Add(new PortSetting("Result", "Variable", new string[4] { "MathIn", "Variable", "CustomVariable", "BranchInValue" }, true));
        }

        public MathClass Copy(bool _runtime = false)
        {
            MathClass _copy = new MathClass(mRoot);
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
            if (_runtime) _copy.InitRuntimeNodeName();
            _copy.mType = NodeType.Math;
            return _copy;
        }

        public bool ValueB_Visible()
        {
            return (int)Type != 5 && (int)Type != 6 && (int)Type != 8 && (int)Type != 9 && (int)Type != 10 && (int)Type != 11;
        }
        public bool ValueC_Visible()
        {
            return (int)Type >= 14;
        }

        public float EqualTo(GraphRuntimeState _runtime)
        {
            if (_runtime.NodeStates[NodeIndex].lastHash == _runtime.Hash) return _runtime.NodeStates[NodeIndex].lastFloat;
            
            switch (Type)
            {
                case MathType.Add:
                    return _runtime.NodeStates[NodeIndex].SetHashCache(_runtime.Hash, ValueA(_runtime) + ValueB(_runtime));
                case MathType.Substract:
                    return _runtime.NodeStates[NodeIndex].SetHashCache(_runtime.Hash, ValueA(_runtime) - ValueB(_runtime));
                case MathType.Multiply:
                    return _runtime.NodeStates[NodeIndex].SetHashCache(_runtime.Hash, ValueA(_runtime) * ValueB(_runtime));
                case MathType.Divide:
                    if (ValueB(_runtime) == 0F) return _runtime.NodeStates[NodeIndex].SetHashCache(_runtime.Hash, 0F);
                    return _runtime.NodeStates[NodeIndex].SetHashCache(_runtime.Hash, ValueA(_runtime) / ValueB(_runtime));
                case MathType.Power:
                    return _runtime.NodeStates[NodeIndex].SetHashCache(_runtime.Hash, Mathf.Pow(ValueA(_runtime), ValueB(_runtime)));
                case MathType.Sqrt:
                    return _runtime.NodeStates[NodeIndex].SetHashCache(_runtime.Hash, Mathf.Sqrt(ValueA(_runtime)));
                case MathType.OneMinus:
                    return _runtime.NodeStates[NodeIndex].SetHashCache(_runtime.Hash, 1F - ValueA(_runtime));
                case MathType.DivRem:
                    return _runtime.NodeStates[NodeIndex].SetHashCache(_runtime.Hash, ValueA(_runtime) % ValueB(_runtime));
                case MathType.Floor:
                    return _runtime.NodeStates[NodeIndex].SetHashCache(_runtime.Hash, Mathf.Floor(ValueA(_runtime)));
                case MathType.Ceil:
                    return _runtime.NodeStates[NodeIndex].SetHashCache(_runtime.Hash, Mathf.Ceil(ValueA(_runtime)));
                case MathType.Round:
                    return _runtime.NodeStates[NodeIndex].SetHashCache(_runtime.Hash, Mathf.Round(ValueA(_runtime)));
                case MathType.Abs:
                    return _runtime.NodeStates[NodeIndex].SetHashCache(_runtime.Hash, Mathf.Abs(ValueA(_runtime)));
                case MathType.Min:
                    return _runtime.NodeStates[NodeIndex].SetHashCache(_runtime.Hash, Mathf.Min(ValueA(_runtime), ValueB(_runtime)));
                case MathType.Max:
                    return _runtime.NodeStates[NodeIndex].SetHashCache(_runtime.Hash, Mathf.Max(ValueA(_runtime), ValueB(_runtime)));
                case MathType.Clamp:
                    return _runtime.NodeStates[NodeIndex].SetHashCache(_runtime.Hash, Mathf.Clamp(ValueA(_runtime), ValueB(_runtime), ValueC(_runtime)));
                case MathType.Lerp:
                    return _runtime.NodeStates[NodeIndex].SetHashCache(_runtime.Hash, Mathf.Lerp(ValueA(_runtime), ValueB(_runtime), ValueC(_runtime)));
            }
            return _runtime.NodeStates[NodeIndex].SetHashCache(_runtime.Hash, 0F);
        }

        public string NameOfValueA()
        {
            switch (Type)
            {
                case MathType.Add:
                    return "A";
                case MathType.Substract:
                    return "A";
                case MathType.Multiply:
                    return "A";
                case MathType.Divide:
                    return "A";
                case MathType.Power:
                    return "Base";
                case MathType.Sqrt:
                    return "Base";
                case MathType.OneMinus:
                    return "Base";
                case MathType.DivRem:
                    return "A";
                case MathType.Floor:
                    return "Base";
                case MathType.Ceil:
                    return "Base";
                case MathType.Round:
                    return "Base";
                case MathType.Abs:
                    return "Base";
                case MathType.Min:
                    return "A";
                case MathType.Max:
                    return "A";
                case MathType.Clamp:
                    return "Base";
                case MathType.Lerp:
                    return "A";
            }
            return "A";
        }

        public string NameOfValueB()
        {
            switch (Type)
            {
                case MathType.Add:
                    return "B";
                case MathType.Substract:
                    return "B";
                case MathType.Multiply:
                    return "B";
                case MathType.Divide:
                    return "B";
                case MathType.Power:
                    return "Power";
                case MathType.Sqrt:
                    return "";
                case MathType.OneMinus:
                    return "";
                case MathType.DivRem:
                    return "B";
                case MathType.Floor:
                    return "";
                case MathType.Ceil:
                    return "";
                case MathType.Round:
                    return "";
                case MathType.Abs:
                    return "";
                case MathType.Min:
                    return "B";
                case MathType.Max:
                    return "B";
                case MathType.Clamp:
                    return "Min";
                case MathType.Lerp:
                    return "B";
            }
            return "B";
        }

        public string NameOfValueC()
        {
            switch (Type)
            {
                case MathType.Add:
                    return "";
                case MathType.Substract:
                    return "";
                case MathType.Multiply:
                    return "";
                case MathType.Divide:
                    return "";
                case MathType.Power:
                    return "";
                case MathType.Sqrt:
                    return "";
                case MathType.OneMinus:
                    return "";
                case MathType.DivRem:
                    return "";
                case MathType.Floor:
                    return "";
                case MathType.Ceil:
                    return "";
                case MathType.Round:
                    return "";
                case MathType.Abs:
                    return "";
                case MathType.Min:
                    return "";
                case MathType.Max:
                    return "";
                case MathType.Clamp:
                    return "Max";
                case MathType.Lerp:
                    return "t";
            }
            return "C";
        }


    }
}