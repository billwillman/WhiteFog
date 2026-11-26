using UnityEngine;
using System.Collections.Generic;


namespace SoftKitty
{
    [System.Serializable]
    public class Action : CustomNode
    {
        public ActionType Type = ActionType.Only_Once;
        public int Count = 1;
        public float WaitTime = 0F;
        public float KeepTime = 0F;
        public float Interval = 0F;
        [SerializeReference] public SkillCalculation calculation;
        [SerializeReference] public SkillMotion motion;
        [SerializeReference] public SkillProjectile projectile;
        [SerializeReference] public OriginalSkillGraph.EffectCue visualEffect;

       
        public Action()
        {

        }
        public Action(Root _root)
        {
            mRoot = _root;
            minimized = false;
            Name = "Action";
            Uid = "Node" + _root.GetAvailableUid().ToString();
            ClassName = "AttributeChange";
            Position = Vector2.zero;
            Type = ActionType.Only_Once;
            Count = 1;
            WaitTime = 0F;
            KeepTime = 0F;
            Interval = 0F;
            outputPort.Clear();
            ResetInputPortSetting();
        }

        public Action(Action _source)
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
            this.Type = _source.Type;
            this.Count = _source.Count;
            this.WaitTime = _source.WaitTime;
            this.KeepTime = _source.KeepTime;
            this.Interval = _source.Interval;
            calculation = _source.calculation;
            motion = _source.motion?.Copy();
            projectile = _source.projectile?.Copy();
            visualEffect = _source.visualEffect?.Copy();
        }

        public void ResetInputPortSetting()
        {
            inputPort.Clear(); ;
            inputPort.Add(new PortSetting("Action", "ActionIn", new string[2] { "RootOut", "BranchOutAction" }, false));
            CustomInit("Action", ClassName);
            EnsureExecutionConditionPort();
            if (SkillProjectile.IsLaunch(this)) EnsureArrivalPort();
        }

        // Append after the class-specific inputs so all existing entity/value port numbers stay valid.
        public int ExecutionConditionPort => inputPort.Count > 0 && inputPort[inputPort.Count - 1].Type == "ConditionIn"
            ? inputPort.Count - 1 : -1;

        public int EnsureExecutionConditionPort()
        {
            if (ExecutionConditionPort < 0)
                inputPort.Add(new PortSetting("动作前置条件", "ConditionIn", new[] { "ConditionOut" }, true));
            return ExecutionConditionPort;
        }

        public void EnsureArrivalPort()
        {
            if (outputPort.Count == 0)
                outputPort.Add(new PortSetting("命中目标", "BranchOutAction", new[] { "ActionIn", "BranchInAction" }, true));
        }

        private static int nextExecutionHash;
        public bool ConditionsMet(ref GraphRuntimeState state)
        {
            if (ExecutionConditionPort < 0) return true;
            var port = inputPort[ExecutionConditionPort];
            if (port.uid.Count == 0) return true;
            if (port.connectedNodes.Count != port.uid.Count) return false;
            nextExecutionHash = nextExecutionHash == int.MinValue ? -1 : nextExecutionHash - 1;
            state.Hash = nextExecutionHash;
            foreach (var condition in port.connectedNodes)
                if (!condition.isConditionActive(state)) return false;
            return true;
        }

        public static void ExecuteChecked(NodeBase node, GraphRuntimeState state)
        {
            if (!(node is Action action) || action.ConditionsMet(ref state)) node?.Execute(state);
        }

        public Action Copy(bool _runtime=false)
        {
            Action _copy = new Action(this);
            if (_runtime) _copy.InitRuntimeNodeName();
            _copy.mType = NodeType.Action;
            return _copy;
        }


        public override void Run(GraphInstance _instance, GraphRuntimeState _runtime)
        {
            _runtime.NodeStates[NodeIndex].lastBool = true;
            _instance.StartActionCoroutine(WaitTime, Type == ActionType.Multiple_Time ? Count : 1, Type == ActionType.For_X_Seconds ? KeepTime : 0F, Type == ActionType.Only_Once ?0.01F:Interval, this, _runtime);
        }

        public override void Execute(GraphRuntimeState _runtime)
        {

        }



    }
}
