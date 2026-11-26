using UnityEngine;
using System.Collections.Generic;
using System;
using System.Reflection;

namespace SoftKitty
{
    /// <summary>
    /// Generate a debug string for each graph calculation.
    /// The string can be copied from the Console and pasted into the Graph Debug Mode to visualize node results.
    /// </summary>
    [System.Serializable]
    public class GraphDebugText
    {
        public string NodeUid;
        public string DebugText;
    }
    [System.Serializable]
    public class GraphDebugTextRoot
    {
        public string Title;
        public List<GraphDebugText> Data = new List<GraphDebugText>();
    }

    /// <summary>
    /// The runtime state of the Root node, this will not be saved with the Graph.
    /// </summary>
    public struct RootRuntimeState
    {
        public CombatRoll RandomKey;
        public GameObject RootInstance;
        public string instanceUid;
        public Dictionary<string, Entity> InvolvedEntityDic;
        public Dictionary<string, float> DynamicVariableDic;
        /// <summary>
        /// Override the float value of a Dynamic Variable by its UID
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_value"></param>
        public void SetDynamicVariableFloat(string _uid, float _value)
        {
            if (DynamicVariableDic.ContainsKey(_uid))
            {
                DynamicVariableDic[_uid] = _value;
            }
            else
            {
                DynamicVariableDic.Add(_uid, _value);
            }
        }

        /// <summary>
        /// Override a instance based Entity variable by a unique string tag.
        /// </summary>
        /// <param name="_tag"></param>
        /// <param name="_entity"></param>
        public void SetInvolvedEntity(string _tag, Entity _entity)
        {
            if (SGD_Settings.Instance.DebugLevel >= 3) Debug.Log("<Root> -> SetInvolvedEntity(" + _tag + " ," + _entity.uid + ")");
            if (InvolvedEntityDic.ContainsKey(_tag))
            {
                InvolvedEntityDic[_tag] = _entity;
            }
            else
            {
                InvolvedEntityDic.Add(_tag, _entity);
            }
        }

        /// <summary>
        /// Remove a instance based Entity variable by its unique string tag.
        /// </summary>
        /// <param name="_tag"></param>
        public void RemoveInvolvedEntity(string _tag)
        {
            if (SGD_Settings.Instance.DebugLevel >= 3) Debug.Log("<Root> -> RemoveInvolvedEntity():" + _tag);
            if (InvolvedEntityDic.ContainsKey(_tag)) InvolvedEntityDic.Remove(_tag);
        }

        /// <summary>
        /// Retrieves a instance based Entity by its unique string tag.
        /// </summary>
        /// <param name="_tag"></param>
        /// <returns></returns>
        public Entity GetInvolvedEntity(string _tag)
        {

            if (InvolvedEntityDic.ContainsKey(_tag))
            {
                if (SGD_Settings.Instance.DebugLevel >= 3 && InvolvedEntityDic[_tag] != null) Debug.Log("<Root> -> GetInvolvedEntity():" + _tag + " >" + InvolvedEntityDic[_tag].uid);
                return InvolvedEntityDic[_tag];
            }
            return null;
        }
    }

    /// <summary>
    /// Root node can access all nodes in the Graph, and it is a expandable node which you can use the 'Custom Graph Interface Tool' to create your custom Root Node.
    /// </summary>
    [System.Serializable]
    public class Root : CustomNode
    {
        /// <summary>
        /// All Action nodes in the Graph
        /// </summary>
        public List<Action> Actions = new List<Action>();
        /// <summary>
        /// All Trigger nodes in the Graph
        /// </summary>
        public List<Trigger> Triggers = new List<Trigger>();
        /// <summary>
        /// All Condition Group nodes in the Graph
        /// </summary>
        public List<ConditionGroup> ConditionGroups = new List<ConditionGroup>();
        /// <summary>
        /// All Condition nodes in the Graph
        /// </summary>
        public List<Handler> Handlers = new List<Handler>();
        /// <summary>
        /// All Math nodes in the Graph
        /// </summary>
        public List<MathClass> Maths = new List<MathClass>();
        /// <summary>
        /// All Variable nodes in the Graph
        /// </summary>
        public List<VariableClass> Variables = new List<VariableClass>();
        /// <summary>
        /// All Entity nodes in the Graph
        /// </summary>
        public List<EntityClass> Entities = new List<EntityClass>();
        /// <summary>
        /// All sticky notes in the Graph
        /// </summary>
        public List<Note> Notes = new List<Note>();
        /// <summary>
        /// All Groups in the Graph
        /// </summary>
        public List<CustomGroup> Groups = new List<CustomGroup>();
        /// <summary>
        /// All Branch nodes in the Graph
        /// </summary>
        public List<Branch> Branchs = new List<Branch>();

        /// <summary>
        /// The Dictionary of all nodes in the Graph
        /// </summary>
        [SerializeReference, HideInInspector]
        public Dictionary<string, NodeBase> NodeDic = new Dictionary<string, NodeBase>();
        /// <summary>
        /// All trigger nodes connect with the Root
        /// </summary>
        [SerializeReference, HideInInspector]
        public List<Trigger> ActiveTriggers = new List<Trigger>();
        private bool usesActionConditions;



        #region Internal Methods
        public virtual int GetAvailableUid()
        {
            List<int> _uids = new List<int>();
            _uids.Add(GetIndexOfUid());
            foreach (var obj in Actions)
            {
                _uids.Add(obj.GetIndexOfUid());
            }
            foreach (var obj in Triggers)
            {
                _uids.Add(obj.GetIndexOfUid());
            }
            foreach (var obj in ConditionGroups)
            {
                _uids.Add(obj.GetIndexOfUid());
            }
            foreach (var obj in Handlers)
            {
                _uids.Add(obj.GetIndexOfUid());
            }
            foreach (var obj in Maths)
            {
                _uids.Add(obj.GetIndexOfUid());
            }
            foreach (var obj in Variables)
            {
                _uids.Add(obj.GetIndexOfUid());
            }
            foreach (var obj in Entities)
            {
                _uids.Add(obj.GetIndexOfUid());
            }
            foreach (var obj in Branchs)
            {
                _uids.Add(obj.GetIndexOfUid());
            }
            int _result = _uids.Count;
            for (int i = 0; i < _uids.Count; i++)
            {
                if (!_uids.Contains(i))
                {
                    _result = i;
                    break;
                }
            }
            return _result;
        }



        public void InitRuntimeState(ref GraphRuntimeState _runtime)
        {
            if (SGD_Settings.Instance.DebugLevel >= 2) Debug.Log("<Root> -> InitRuntimeState()");
            _runtime.RootState = new RootRuntimeState();
            _runtime.RootState.InvolvedEntityDic = new Dictionary<string, Entity>();
            _runtime.RootState.DynamicVariableDic = new Dictionary<string, float>();
            _runtime.NodeStates = new NodeRuntimeState[NodeDic.Values.Count];
            for (int i = 0; i < _runtime.NodeStates.Length; i++) _runtime.NodeStates[i] = new NodeRuntimeState();
            _runtime.HandlerStates = new HandlerRuntimeState[Handlers.Count];
            for (int i = 0; i < _runtime.HandlerStates.Length; i++) _runtime.HandlerStates[i] = new HandlerRuntimeState(true);
        }


        public override void Reset()
        {
            Name = "Root";
            Uid = "Node0";
            minimized = false;
            Position = Vector2.zero;
            Groups.Clear();
            Notes.Clear();
            Actions.Clear();
            Triggers.Clear();
            ConditionGroups.Clear();
            Handlers.Clear();
            Maths.Clear();
            Variables.Clear();
            Entities.Clear();
            Branchs.Clear();
            ClassName = "Combat";
            outputPort.Clear();
            outputPort.Add(new PortSetting("Action", "RootOut", new string[2] { "ActionIn", "BranchInAction" }, true));
            ResetInputPortSetting();
        }

        public void ResetInputPortSetting()
        {
            inputPort.Clear();
            inputPort.Add(new PortSetting("Trigger", "RootIn", new string[1] { "TriggerOut" }, true));
            CustomInit("Root", ClassName);
        }

        #endregion

        /// <summary>
        /// Allocate a positive cache generation. Action condition rechecks use negative generations.
        /// </summary>
        /// <returns></returns>
        public int GenerateInstanceHash()
        {
            nextCacheGeneration = nextCacheGeneration == int.MaxValue ? 1 : nextCacheGeneration + 1;
            return nextCacheGeneration;
        }
        private int nextCacheGeneration;

        /// <summary>
        /// Generate a debug string for each graph calculation.
        /// The string can be copied from the Console and pasted into the Graph Debug Mode to visualize node results.
        /// </summary>
        /// <param name="_title"></param>
        /// <param name="_runtimeState"></param>
        /// <returns></returns>
        public string GenerateGraphDebugText(string _title,GraphRuntimeState _runtimeState)
        {
            GraphDebugTextRoot _newDebug = new GraphDebugTextRoot();
            _newDebug.Title = _title;
            foreach (var obj in NodeDic.Values)
            {
                GraphDebugText _newText = new GraphDebugText();
                _newText.NodeUid = obj.Uid;
                switch (obj.mType)
                {
                    case NodeType.Action:
                        _newText.DebugText = _runtimeState.NodeStates[obj.NodeIndex].lastBool ? "Action <color=#11FF11>Executed</color>" : "<color=#FF1111>NEVER</color> Executed";
                        break;
                    case NodeType.Trigger:
                        _newText.DebugText = _runtimeState.NodeStates[obj.NodeIndex].lastBool ? "<color=#11FF11>Triggered</color>" : "<color=#FF1111>NEVER</color> Triggered";
                        break;
                    case NodeType.ConditionGroup:
                        _newText.DebugText = _runtimeState.NodeStates[obj.NodeIndex].lastBool ? "<color=#11FF11>Conditions Met</color>" : "Conditions <color=#FF1111>NOT</color> Met";
                        break;
                    case NodeType.Handler:
                        _newText.DebugText = _runtimeState.NodeStates[obj.NodeIndex].lastBool ? "<color=#11FF11>Condition Met</color>" : "Condition <color=#FF1111>NOT</color> Met";
                        break;
                    case NodeType.Math:
                        _newText.DebugText = "Result: <color=#11FF11>" + _runtimeState.NodeStates[obj.NodeIndex].lastFloat.ToString() + "</color>";
                        break;
                    case NodeType.Variable:
                        _newText.DebugText = "Result: <color=#11FF11>" + _runtimeState.NodeStates[obj.NodeIndex].lastFloat.ToString() + "</color>";
                        break;
                    case NodeType.Branch:
                        _newText.DebugText = "Result: <color=#11FF11>" + _runtimeState.NodeStates[obj.NodeIndex].lastBool.ToString() + "</color>";
                        break;
                    case NodeType.Entity:
                        Entity _entity = obj.GetEntity(_runtimeState);
                        if (_entity != null)
                        {
                            _newText.DebugText = "Entity: <color=#11FF11>" + _entity.uid + "</color>";
                        }
                        else
                        {
                            _newText.DebugText = "Entity: <color=#FF1111>NULL</color>";
                        }
                        break;
                }
                _newDebug.Data.Add(_newText);
            }
           
            return JsonUtility.ToJson(_newDebug).Replace("<", "&lt&").Replace(">", "&rt&");
        }


        public Root()
        {

        }
        /// <summary>
        /// Runtime copy of the Root Node
        /// </summary>
        /// <param name="_source"></param>
        public Root(Root _source)
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
            InitRuntimeNodeName();
            this.ClassName = _source.ClassName;
            Groups.Clear();
            Notes.Clear();
            Actions.Clear();
            foreach (var obj in _source.Actions) Actions.Add(obj.Copy(true));
            Triggers.Clear();
            foreach (var obj in _source.Triggers) Triggers.Add(obj.Copy(true));
            ConditionGroups.Clear();
            foreach (var obj in _source.ConditionGroups) ConditionGroups.Add(obj.Copy(true));
            Handlers.Clear();
            foreach (var obj in _source.Handlers) Handlers.Add(obj.Copy(true));
            Maths.Clear();
            foreach (var obj in _source.Maths) Maths.Add(obj.Copy(true));
            Branchs.Clear();
            foreach (var obj in _source.Branchs) Branchs.Add(obj.Copy(true));
            Variables.Clear();
            foreach (var obj in _source.Variables) Variables.Add(obj.Copy(true));
            Entities.Clear();
            foreach (var obj in _source.Entities) Entities.Add(obj.Copy(true));

            NodeDic.Clear();
            this.mType = NodeType.Root;
            NodeDic.Add(Uid,this);
            foreach (var obj in Actions)
            {
                Type realActionType = Type.GetType("SoftKitty." + obj.ClassName + "Action");
                if (realActionType != null)
                {
                    object realSubclassInstance = Activator.CreateInstance(realActionType);
                    MethodInfo initMethod = realActionType.GetMethod("InitAction");
                    if (initMethod != null)
                    {
                        object result = initMethod.Invoke(realSubclassInstance, new object[1] { obj });
                        Action initializedAction = result as Action;
                        if (initializedAction != null)
                        {
                            initializedAction.mType = NodeType.Action;
                            NodeDic.Add(obj.Uid, initializedAction);
                            if (SGD_Settings.Instance.DebugLevel >= 3) Debug.Log("<Root> -> Adding " + realActionType.ToString() + "[" + obj.Uid + "] to ActionDic");

                        }
                        else
                        {
                            if (SGD_Settings.Instance.DebugLevel > 0) Debug.LogError("<Root> -> Sorry, your custom class <" + obj.ClassName + "> is not inherit from <Action> class, make sure it inherit from <Action> class and have 'InitAction' function as example shows.");
                        }
                    }

                }
                else
                {
                    if (SGD_Settings.Instance.DebugLevel > 0) Debug.LogError("<Root> -> Sorry, your custom class <" + obj.ClassName + "> does not exists!");
                }
            }
            foreach (var obj in Triggers)
            {
                obj.mType = NodeType.Trigger;
                NodeDic.Add(obj.Uid, obj);
            }
            foreach (var obj in ConditionGroups)
            {
                obj.mType = NodeType.ConditionGroup;
                NodeDic.Add(obj.Uid, obj);
            }
            foreach (var obj in Handlers)
            {
                Type realHandlerType = Type.GetType("SoftKitty." + obj.ClassName + "Handler");
                if (realHandlerType != null)
                {
                    object realSubclassInstance = Activator.CreateInstance(realHandlerType);
                    MethodInfo initMethod = realHandlerType.GetMethod("InitHandler");
                    if (initMethod != null)
                    {
                        object result = initMethod.Invoke(realSubclassInstance, new object[1] { obj });
                        Handler initializedHandler = result as Handler;
                        if (initializedHandler != null)
                        {
                            initializedHandler.mType = NodeType.Handler;
                            NodeDic.Add(obj.Uid, initializedHandler);
                            if (SGD_Settings.Instance.DebugLevel >= 3) Debug.Log("<Root> -> Adding " + realHandlerType.ToString() + "[" + obj.Uid + "] to ConditionDic");
                        }
                        else
                        {
                            if (SGD_Settings.Instance.DebugLevel > 0) Debug.LogError("<Root> -> Sorry, your custom class <" + obj.ClassName + "> is not inherit from <Handler> class, make sure it inherit from <Handler> class and have 'InitHandler' function as example shows.");
                        }
                    }

                }
                else
                {
                    if (SGD_Settings.Instance.DebugLevel > 0) Debug.LogError("<Root> -> Sorry, your custom class <" + obj.ClassName + "> does not exists!");
                }
               
            }
            foreach (var obj in Maths)
            {
                obj.mType = NodeType.Math;
                NodeDic.Add(obj.Uid, obj);
            }
            foreach (var obj in Branchs)
            {
                obj.mType = NodeType.Branch;
                NodeDic.Add(obj.Uid, obj);
            }
            foreach (var obj in Variables)
            {
                obj.mType = NodeType.Variable;
                NodeDic.Add(obj.Uid, obj);
            }
            foreach (var obj in Entities)
            {
                obj.mType = NodeType.Entity;
                NodeDic.Add(obj.Uid, obj);
            }


            int _index = 0;
            foreach (var obj in NodeDic.Values)
            {
                obj.SetRoot(this);
                obj.RuntimeNodeLink(this);
                obj.NodeIndex = _index;
                _index++;
            }
            int _handlerIndex = 0;
            foreach (var obj in Handlers)
            {
                obj.HandlerIndex = _handlerIndex;
                if (NodeDic.TryGetValue(obj.Uid, out var handler)) ((Handler)handler).HandlerIndex = _handlerIndex;
                _handlerIndex++;
            }
            usesActionConditions = Actions.Exists(a => a.ExecutionConditionPort >= 0 && a.inputPort[a.ExecutionConditionPort].uid.Count > 0);

            foreach (var obj in ConditionGroups)
            {
                obj.Handlers.Clear();
                foreach (var node in obj.inputPort[0].connectedNodes) obj.Handlers.Add(node);
            }
            foreach (var obj in Triggers)
            {
                obj.TriggerConditions.Clear();
                obj.ResetConditions.Clear();
                foreach (var node in obj.inputPort[0].connectedNodes) obj.TriggerConditions.Add(node);
                if (!obj.AutoReset) foreach (var node in obj.inputPort[1].connectedNodes) obj.ResetConditions.Add(node);
            }
            foreach (var obj in Branchs)
            {
                obj.Handlers.Clear();
                foreach (var node in obj.inputPort[0].connectedNodes) obj.Handlers.Add(node);
            }

            RuntimeNodeLink(this);
            ActiveTriggers.Clear();
            foreach (var node in inputPort[0].connectedNodes) {
                ActiveTriggers.Add((Trigger)node);
            }
            StructureBuild();
        }

        /// <summary>
        /// Override this method for your own initalization code.
        /// </summary>
        public virtual void StructureBuild()
        {

        }


        /// <summary>
        /// Root node can access all nodes in the Graph, and it is a expandable node which you can use the 'Custom Graph Interface Tool' to create your custom Root Node.
        /// </summary>
        /// <param name="_reset"></param>
        public Root(bool _reset)
        {
            if (_reset)
            {
                Reset();
                Action _newAction = new Action(this);
                _newAction.Position = new Vector2(400F, 0F);
                _newAction.inputPort[0].uid.Add(Uid + "#0");
                outputPort[0].uid.Add(_newAction.Uid + "#0");
                Actions.Add(_newAction);

                Trigger _newTrigger = new Trigger(this);
                _newTrigger.Position = new Vector2(-400F, 0F);
                _newTrigger.outputPort[0].uid.Add(Uid + "#0");
                inputPort[0].uid.Add(_newTrigger.Uid + "#0");
                Triggers.Add(_newTrigger);

                Handler _newHandler = new Handler(this);
                _newHandler.Position = new Vector2(-800F, 0F);
                _newHandler.outputPort[0].uid.Add(_newTrigger.Uid + "#0");
                _newTrigger.inputPort[0].uid.Add(_newHandler.Uid + "#0");
                Handlers.Add(_newHandler);

                StructureReset();
            }
        }

        /// <summary>
        /// Override this method for your own reseting code.
        /// </summary>
        public virtual void StructureReset()
        {

        }

        /// <summary>
        /// Run an action from a GraphInstance with a unique hash integer number
        /// </summary>
        /// <param name="_instance"></param>
        /// <param name="_hash"></param>
        public virtual void RunActions(GraphInstance _instance, GraphRuntimeState _runtime)
        {
            // Later casts on this emitter reset its handler state while earlier timelines can still be waiting.
            if (usesActionConditions)
                _runtime.HandlerStates = (HandlerRuntimeState[])_runtime.HandlerStates.Clone();
            foreach (var obj in outputPort[0].connectedNodes)
            {
                if (obj != null)
                {
                    obj.Run(_instance, _runtime);
                }
            }
        }

        public float GetLatestDelay()
        {
            float _delay = 0F;
            foreach (var obj in Actions)
            {
                if (obj != null)
                {
                    if (obj.WaitTime > _delay) _delay = obj.WaitTime;
                }
            }
            return _delay;
        }

        /// <summary>
        /// Excute all actions with a unique hash integer number
        /// </summary>
        /// <param name="_hash"></param>
        public virtual void ExecuteAllAction(GraphRuntimeState _runtime)
        {
            foreach (var obj in outputPort[0].connectedNodes)
            {
                if (obj != null)
                {
                    Action.ExecuteChecked(obj, _runtime);
                }
            }
        }
        /// <summary>
        /// Excute a specified Action node with a unique hash integer number
        /// </summary>
        /// <param name="_node"></param>
        /// <param name="_hash"></param>
        public virtual void ExecuteAction(NodeBase _node, GraphRuntimeState _runtime)
        {
            Action.ExecuteChecked(_node, _runtime);
        }

        /// <summary>
        /// Retrieves the list of all Dynamic Variables in the Graph.
        /// </summary>
        /// <returns></returns>
        public List<DynamicFloat> GetDynamicFloatList()
        {
            List<DynamicFloat> _list = new List<DynamicFloat>();
            List<string> _uid = new List<string>();
            for (int i = 0; i < Variables.Count; i++)
            {
                if (Variables[i].Type == VariableType.DynamicVariable && !_uid.Contains(Variables[i].VariableUid))
                {
                    _list.Add(new DynamicFloat(Variables[i].VariableUid, Variables[i].Constant));
                    _uid.Add(Variables[i].VariableUid);
                }
            }
            return _list;
        }

        /// <summary>
        /// Retrieves the Dynamic Variable data with its UID
        /// </summary>
        /// <param name="_uid"></param>
        /// <returns></returns>
        public DynamicFloat GetDynamicFloat(string _uid)
        {
            for (int i = 0; i < Variables.Count; i++)
            {
                if (Variables[i].Type == VariableType.DynamicVariable && Variables[i].VariableUid == _uid)
                {
                    return new DynamicFloat(Variables[i].VariableUid, Variables[i].Constant);
                }
            }
            return null;
        }

       
       
    }
}
