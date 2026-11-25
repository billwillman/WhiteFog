using UnityEngine;
using System.Collections.Generic;

namespace SoftKitty
{
    public struct NodeRuntimeState
    {
        public int lastHash;
        public float lastFloat;
        public bool lastBool;
        public float SetHashCache(int _hash, float _result)
        {
            lastHash = _hash;
            lastFloat = _result;
            return _result;
        }

        public bool SetHashCache(int _hash, bool _result)
        {
            lastHash = _hash;
            lastBool = _result;
            return _result;
        }
    }

    [System.Serializable]
    public class NodeBase
    {
        public NodeType mType;
        public string Name;
        public string Uid;
        public Vector2 Position;
        public int NodeIndex = 0;
        public List<PortSetting> inputPort = new List<PortSetting>();
        public List<PortSetting> outputPort = new List<PortSetting>();
        public bool minimized = false;
        public uint InstanceCache = 0;
        [SerializeReference, HideInInspector]
        protected Root mRoot;

        public void SetRoot(Root _root)
        {
            mRoot = _root;
        }
        public int GetIndexOfUid()
        {
            int _result = -1;
            int.TryParse(Uid.Replace("Node", ""), out _result);
            return _result;
        }

        public float GetVariableValue(GraphRuntimeState _runtime, float _default = 0F)
        {
            if (SGD_Settings.Instance.DebugLevel >= 3) Debug.Log("<Node>"+ Uid + " -> GetVariableValue()");
            if (_runtime.NodeStates[NodeIndex].lastHash == _runtime.Hash) return _runtime.NodeStates[NodeIndex].lastFloat;
            
            if (mType== NodeType.Variable)
            {
                return _runtime.NodeStates[NodeIndex].SetHashCache(_runtime.Hash, ((VariableClass)this).Value(_runtime));
            }
            else if (mType == NodeType.Math)
            {
                return _runtime.NodeStates[NodeIndex].SetHashCache(_runtime.Hash, ((MathClass)this).EqualTo(_runtime));
            }
            else if (mType == NodeType.Branch && ((Branch)this).Type == BranchType.Variable)
            {
                if (((Branch)this).Result(_runtime) != null) return _runtime.NodeStates[NodeIndex].SetHashCache(_runtime.Hash, (float)((Branch)this).Result(_runtime));
            }
            return _runtime.NodeStates[NodeIndex].SetHashCache(_runtime.Hash, _default);
        }

        public virtual Entity GetEntity(GraphRuntimeState _runtime)
        {
            return null;
        }

        




        public Action GetAction(GameObject _obj, GraphRuntimeState _runtime)
        {
            if (SGD_Settings.Instance.DebugLevel >= 3) Debug.Log("<Node>" + Uid + " -> GetAction()");
            if (mType == NodeType.Action)
            {
                return ((Action)this);
            }
            else if (mType == NodeType.Branch && ((Branch)this).Type == BranchType.Action)
            {
                if (((Branch)this).Result(_runtime) != null) return (Action)((Branch)this).Result(_runtime);
            }
            return null;
        }

        public virtual void Execute(GraphRuntimeState _runtime)
        {

        }

        public virtual void Run(GraphInstance _instance, GraphRuntimeState _runtime)
        {

        }

        
        public virtual bool isConditionActive(GraphRuntimeState _runtime)
        {
            if (SGD_Settings.Instance.DebugLevel >= 3) Debug.Log("<Node>" + Uid + " -> GetConditionActive()");
            if (_runtime.NodeStates[NodeIndex].lastHash == _runtime.Hash) return _runtime.NodeStates[NodeIndex].lastBool;
            
            Update(_runtime);
            return _runtime.NodeStates[NodeIndex].SetHashCache(_runtime.Hash, isActive(_runtime));
        }



        public virtual bool isActive(GraphRuntimeState _runtime)
        {
            return true;
        }

        public virtual void Reset()
        {
            
        }

        public virtual void ResetRuntime(GraphRuntimeState _runtime)
        {

        }

        public virtual void Update(GraphRuntimeState _runtime)
        {
           
        }

        public virtual void Init()
        {
           
        }

        public virtual void OnDestroy()
        {
            
        }

        public void RuntimeNodeLink(Root _root)
        {
            foreach (var obj in inputPort)
            {
                obj.connectedNodes = new List<NodeBase>();
                for (int i = 0; i < obj.uid.Count; i++)
                {
                    if (_root.NodeDic.ContainsKey(obj.uid[i]))
                        obj.connectedNodes.Add(_root.NodeDic[obj.uid[i]]);
                    else
                        Debug.LogError(obj.uid[i] + "  not found with inputPort of " + Uid);
                }
            }
            foreach (var obj in outputPort)
            {
                obj.connectedNodes = new List<NodeBase>();
                for (int i = 0; i < obj.uid.Count; i++)
                {
                    if (_root.NodeDic.ContainsKey(obj.uid[i]))
                        obj.connectedNodes.Add(_root.NodeDic[obj.uid[i]]);
                    else
                        Debug.LogError(obj.uid[i]+"  not found with outputPort of "+ Uid);
                }
            }
        }

        public void RegenerateUid(Root _root)
        {
            Uid = "Node" + _root.GetAvailableUid().ToString();
        }

        public string GetNodeName(string _uid)
        {
            if (_uid.Contains("#"))
            {
                string[] _args = _uid.Split("#");
                return _args[0];
            }
            else
            {
                return _uid;
            }
        }

        public void InitRuntimeNodeName()
        {
            mType = NodeType.Root;
            foreach (var obj in inputPort) {
                for(int i=0; i<obj.uid.Count;i++) obj.uid[i] = GetNodeName(obj.uid[i]);
            }
            foreach (var obj in outputPort)
            {
                for (int i = 0; i < obj.uid.Count; i++) obj.uid[i] = GetNodeName(obj.uid[i]);
            }
        }
    }
}
