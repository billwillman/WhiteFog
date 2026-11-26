using UnityEngine;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Collections;

namespace SoftKitty
{



    /// <summary>
    /// GraphInstance is the base class for visual graph objects in the system. Developers can inherit from this class to create custom graph objects and handle the interactions and logic associated with them.
    /// </summary>
    public class GraphInstance : MonoBehaviour
    {
        /// <summary>
        /// The visual graph object associated with this instance.
        /// </summary>
        [HideInInspector]
        public GraphObject mGraph;
        /// <summary>
        /// Whether the instance is initialized.
        /// </summary>
        [HideInInspector]
        public bool inited = false;
        /// <summary>
        /// The runtime instance of the root node of the visual graph object.
        /// </summary>
        [SerializeReference,HideInInspector]
        protected Root currentRootInstance;

        #region Internal Method
        [HideInInspector]
        public static Dictionary<GraphObject, Root> runtimeInitializedGraph = new Dictionary<GraphObject, Root>();

        protected GraphRuntimeState mRuntime;

        public virtual void Awake()
        {
            runtimeInitializedGraph.Clear();
        }

        public virtual void Start()
        {
            inited = false;
            Init();
        }
        private IEnumerator ActionCo(float _delay, int _count, float _keepTime, float _interval, NodeBase _node, GraphRuntimeState _runtime)
        {
            yield return new WaitForSeconds(_delay);
            if (_interval < 0.01F) _interval = 0.01F;
            for (int i = 0; i < _count; i++)
            {
                float _timer = 0F;
                while (_timer <= _keepTime)
                {
                    if (_runtime.CastInterrupted) yield break;
                    currentRootInstance.ExecuteAction(_node, _runtime);
                    yield return new WaitForSeconds(_interval);
                    _timer += _interval;
                }
            }
        }

        public void StartActionCoroutine(float _delay, int _count, float _keepTime, float _interval, NodeBase _node, GraphRuntimeState _runtime)
        {
            StartCoroutine(ActionCo(_delay, _count, _keepTime, _interval, _node, _runtime));
        }
        private void GetRootInstance()
        {
            if (!runtimeInitializedGraph.ContainsKey(mGraph))
            {
                string _className = "SoftKitty." + mGraph.RootSet.ClassName + "Root";
                Type realRootType = Type.GetType(_className);
                if (realRootType == null)
                {
                    if (SGD_Settings.Instance.DebugLevel > 0) Debug.LogError($"<GraphInstance> -> Type '{_className}' not found. Make sure the class name is correct and includes the full namespace if applicable.");
                    return;
                }
                if (!typeof(Root).IsAssignableFrom(realRootType))
                {
                    if (SGD_Settings.Instance.DebugLevel > 0) Debug.LogError($"<GraphInstance> -> Type '{_className}' does not inherit from 'Root'.");
                    return;
                }
                object realSubclassInstance = Activator.CreateInstance(realRootType) as Root;
                MethodInfo initMethod = realRootType.GetMethod("InitRoot");
                if (initMethod != null)
                {
                    object result = initMethod.Invoke(realSubclassInstance, new object[1] { mGraph.RootSet });
                    Root initializedRoot = result as Root;
                    if (initializedRoot != null)
                    {
                        runtimeInitializedGraph.Add(mGraph, initializedRoot);
                        if (SGD_Settings.Instance.DebugLevel >= 3) Debug.Log("<GraphInstance> -> GetRootInstance [" + realRootType.ToString() + "] has added with key: " + mGraph.name);
                    }
                    else
                    {
                        if (SGD_Settings.Instance.DebugLevel > 0) Debug.LogError("<GraphInstance> -> Sorry, your custom class <" + mGraph.RootSet.ClassName + "> is not inherit from <Root> class, make sure it inherit from <Root> class and have 'InitRoot' function as example shows.");
                    }
                }
            }

            if (runtimeInitializedGraph.ContainsKey(mGraph))
            {
                currentRootInstance = runtimeInitializedGraph[mGraph];
                mRuntime = new GraphRuntimeState();
                currentRootInstance.InitRuntimeState(ref mRuntime);
            }
        }
        #endregion

        /// <summary>
        /// Sets a dynamic variable in the visual graph object by its unique UID.
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_value"></param>
        public virtual void SetDynamicValue(string _uid,float _value)
        {
            if(currentRootInstance==null) Init();
            mRuntime.RootState.SetDynamicVariableFloat(_uid, _value);
        }
        /// <summary>
        /// Sets the dealer entity for the visual graph object.
        /// </summary>
        /// <param name="_entity"></param>
        public virtual void SetDealerEntity(Entity _entity)
        {
            if (currentRootInstance == null) Init();
            mRuntime.RootState.SetInvolvedEntity("Dealer", _entity);
        }
        /// <summary>
        /// Sets the target entity for the visual graph object.
        /// </summary>
        /// <param name="_entity"></param>
        public virtual void SetTargetEntity(Entity _entity)
        {
            if (currentRootInstance == null) Init();
            mRuntime.RootState.SetInvolvedEntity("Target", _entity);
        }
        /// <summary>
        /// Retrieves the current dealer entity set for the visual graph object.
        /// </summary>
        /// <returns></returns>
        public Entity GetDealerEntity()
        {
            if (currentRootInstance == null) Init();
            return mRuntime.RootState.GetInvolvedEntity("Dealer");
        }
        /// <summary>
        /// Retrieves the current target entity set for the visual graph object.
        /// </summary>
        /// <returns></returns>
        public Entity GetTargetEntity()
        {
            if (currentRootInstance == null) Init();
            return mRuntime.RootState.GetInvolvedEntity("Target");
        }
        /// <summary>
        /// Updates all conditions of the visual graph object. An integer hash is required to cache the result.
        /// </summary>
        /// <param name="_hash"></param>
        public void UpdateAllConditions(int _hash)
        {
            if (currentRootInstance == null) Init();
            mRuntime.Hash = _hash;
            foreach (var obj in currentRootInstance.ActiveTriggers)
            {
                obj.Update(mRuntime);
            }
        }

        /// <summary>
        /// Reset the runtime data
        /// </summary>
        public void ResetRuntime()
        {
            for (int i=0;i<mRuntime.HandlerStates.Length;i++)
            {
                mRuntime.HandlerStates[i].LastActiveTime = -2F;
                mRuntime.HandlerStates[i].LastActiveFrame = -2;
                mRuntime.HandlerStates[i].TriggeredCount = 0;
                mRuntime.HandlerStates[i].Active = false;
            }
#if UNITY_EDITOR
            if (SGD_Settings.Instance.GraphDebugMode)
            {
                for (int i = 0; i < mRuntime.NodeStates.Length; i++)
                {
                    mRuntime.NodeStates[i].lastBool = false;
                    mRuntime.NodeStates[i].lastFloat = 0F;
                }
            }
#endif
        }
        /// <summary>
        /// Initializes the graph object. Called when the game starts.
        /// </summary>
        public virtual void Init()
        {
            if (mGraph != null && mGraph.RootSet != null)
            {
                GetRootInstance();
            }
        }


        

    }
}
