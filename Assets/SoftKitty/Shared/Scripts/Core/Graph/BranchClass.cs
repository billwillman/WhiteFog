using UnityEngine;
using System.Collections.Generic;
namespace SoftKitty
{
    [System.Serializable]
    public class Branch : NodeBase
    {
        public BranchType Type = BranchType.Variable;
        public List<NodeBase> Handlers = new List<NodeBase>();//runtime
       
        

        public override void Run(GraphInstance _instance, GraphRuntimeState _runtime)
        {

            if (AllConditionMatch(_runtime))
            {
                foreach (var obj in outputPort[0].connectedNodes)obj.Run(_instance, _runtime);
            }
            else
            {
                foreach (var obj in outputPort[1].connectedNodes) obj.Run(_instance, _runtime);
            }
        }

      

        public override void Execute(GraphRuntimeState _runtime)
        {
            if (AllConditionMatch(_runtime))
            {
                foreach (var obj in outputPort[0].connectedNodes) Action.ExecuteChecked(obj, _runtime);
            }
            else
            {
                foreach (var obj in outputPort[1].connectedNodes) Action.ExecuteChecked(obj, _runtime);
            }
        }
        public object ValueTrue(GraphRuntimeState _runtime)
        {
            switch (Type)
            {
                default:
                    if (inputPort[1].connectedNodes.Count > 0) return inputPort[1].connectedNodes[0].GetVariableValue(_runtime);
                    break;
                case BranchType.Entity:
                    if (inputPort[1].connectedNodes.Count > 0) return inputPort[1].connectedNodes[0].GetEntity(_runtime);
                    break;

            }
            return null;
        }


        public object ValueFalse(GraphRuntimeState _runtime)
        {
                switch (Type)
                {
                    default:
                        if (inputPort[2].connectedNodes.Count > 0) return inputPort[2].connectedNodes[0].GetVariableValue(_runtime);
                        break;
                    case BranchType.Entity:
                        if (inputPort[2].connectedNodes.Count > 0) return inputPort[2].connectedNodes[0].GetEntity(_runtime);
                        break;
                   
                }
                return null;
        }

        public object Result(GraphRuntimeState _runtime)
        {
            return AllConditionMatch(_runtime) ? ValueTrue(_runtime) : ValueFalse(_runtime);
        }

        public Branch()
        {

        }

        public Branch(Root _root)
        {
            mRoot = _root;
            minimized = false;
            Name = "Branch";
            Uid = "Node" + _root.GetAvailableUid().ToString();
            Position = Vector2.zero;
            Type = BranchType.Variable;
            FormatPort();
        }

        public void FormatPort()
        {
            Name = "Branch (" + Type.ToString() + ")";
            inputPort.Clear();
            outputPort.Clear();
            inputPort.Add(new PortSetting("Conditions", "BranchIn", new string[1] { "ConditionOut"}, true));
            inputPort.Add(new PortSetting(Type== BranchType.Action?"Action": "True", GetInTag(), GetInConnectableTags(), false));
            inputPort.Add(new PortSetting("False", GetInTag(), GetInConnectableTags(), false));
            outputPort.Add(new PortSetting(Type == BranchType.Action ? "True" : "Result", GetoutTag(), GetOutConnectableTags(), true));
            outputPort.Add(new PortSetting(Type == BranchType.Action ? "False" : "Result", GetoutTag(), GetOutConnectableTags(), true));
        }

        public string GetInTag()
        {
            switch (Type)
            {
                default:
                    return "BranchInValue";
                case BranchType.Entity:
                    return "BranchInEntity";
                case BranchType.Action:
                    return "BranchInAction";
            }
        }

        public string GetoutTag()
        {
            switch (Type)
            {
                default:
                    return "Variable";
                case BranchType.Entity:
                    return "EntityOut";
                case BranchType.Action:
                    return "BranchOutAction";
            }

        }

        public string[] GetInConnectableTags()
        {
            switch (Type)
            {
                default:
                    return new string[1] { "Variable" };
                case BranchType.Entity:
                    return new string[1] { "EntityOut" };
                case BranchType.Action:
                    return new string[2] { "RootOut", "BranchOutAction" };
            }
        }

        public string [] GetOutConnectableTags()
        {
            switch (Type)
            {
                default:
                    return new string[4] { "MathIn", "Variable", "CustomVariable", "BranchInValue" };
                case BranchType.Entity:
                    return new string[2] { "BranchInEntity", "EntityIn" };
                case BranchType.Action:
                    return new string[2] { "ActionIn", "BranchInAction" };
            }
        }

        public bool AllConditionMatch(GraphRuntimeState _runtime)
        {
            if (_runtime.NodeStates[NodeIndex].lastHash == _runtime.Hash) return _runtime.NodeStates[NodeIndex].lastBool;
            bool _active = true;
            foreach (var obj in inputPort[0].connectedNodes)
            {
                if (!obj.isConditionActive(_runtime)) _active = false;
            }
            return _runtime.NodeStates[NodeIndex].SetHashCache(_runtime.Hash, _active);
        }



        public Branch Copy(bool _runtime = false)
        {
            Branch _copy = new Branch(mRoot);
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
            _copy.mType = NodeType.Branch;
            return _copy;
        }

        public override Entity GetEntity(GraphRuntimeState _runtime)
        {
            if (mType == NodeType.Branch && Type == BranchType.Entity)
            {
                if (Result(_runtime) != null) return (Entity)Result(_runtime);
            }
            return null;
        }

    }
}
