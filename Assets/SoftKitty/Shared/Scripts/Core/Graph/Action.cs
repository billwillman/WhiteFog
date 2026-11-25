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
        }

        public void ResetInputPortSetting()
        {
            inputPort.Clear(); ;
            inputPort.Add(new PortSetting("Action", "ActionIn", new string[2] { "RootOut", "BranchOutAction" }, false));
            CustomInit("Action", ClassName);
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