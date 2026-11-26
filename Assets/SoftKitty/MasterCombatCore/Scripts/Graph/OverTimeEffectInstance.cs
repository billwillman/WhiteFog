using UnityEngine;
using System.Reflection;
using System;
using System.Collections.Generic;

namespace SoftKitty
{
    /// <summary>
    /// OverTimeEffectInstance is the instance of a graph object associated with over-time effects. It is automatically managed in the backend and is typically not interacted with directly by developers.
    /// </summary>
    public class OverTimeEffectInstance
    {
        #region Internal Variables & Methods
        private Root currentRootInstance;
        private OverTimeEffect mSetting;
        public static Dictionary<GraphObject, Root> runtimeInitializedGraph = new Dictionary<GraphObject, Root>();
        protected GraphRuntimeState mRuntime;
        #endregion

        /// <summary>
        /// Executes the over-time effect associated with the specified UID and layer, involving the provided dealer and target entities.
        /// </summary>
        /// <param name="_uid"></param>
        /// <param name="_layer"></param>
        /// <param name="_dealer"></param>
        /// <param name="_target"></param>
        public void Execute(string _uid, int _layer, Entity _dealer, Entity _target)
            => Execute(_uid, _layer, _dealer, _target, null);

        internal void Execute(string _uid, int _layer, Entity _dealer, Entity _target, ulong? eventId)
        {
 #if MASTER_COMBAT_CORE
            foreach (var obj in mSetting.DynamicVariables) {
                if(obj.uid!= "Layer") mRuntime.RootState.SetDynamicVariableFloat(obj.uid, obj.value);
            }
#endif
            mRuntime.RootState.SetDynamicVariableFloat("Layer", _layer);
            SetDealer(_dealer);
            SetTarget(_target);
            var effect = _target?.GetOverTimeEffect(_uid);
            mRuntime.CasterSnapshot = effect?.casterSnapshot;
            mRuntime.EnsureCasterSnapshot(_dealer);
            bool _success = IfTriggerThenPerformAction(eventId);
            if (SGD_Settings.Instance.DebugLevel >= 2) Debug.Log("<OverTimeEffectInstance> -> Perform() > isTriggered:" + _success);
        }
        /// <summary>
        /// Evaluates whether the over-time effect's triggers have been activated, and if so, performs the associated action.
        /// </summary>
        /// <returns></returns>
        public bool IfTriggerThenPerformAction()
            => IfTriggerThenPerformAction(null);

        private bool IfTriggerThenPerformAction(ulong? eventId)
        {
            SetRandomKey(eventId);
            int _hash = currentRootInstance.GenerateInstanceHash();
            mRuntime.Hash = _hash;
            bool _triggered = TestTriggers(_hash);
            if (_triggered)
            {
                currentRootInstance.ExecuteAllAction(mRuntime);
            }
#if MASTER_COMBAT_CORE
#if UNITY_EDITOR
            if (SGD_Settings.Instance.GraphDebugMode) Debug.Log( currentRootInstance.GenerateGraphDebugText("****This is a debug text of OTE[" + mSetting.name + "] Tick with GraphObject[" + mSetting.DesignGraph.name + "], copy this text and paste in the Debug panel of 'Graph View' window.****", mRuntime));
#endif
#endif
            return _triggered;
        }
        /// <summary>
        /// Reset the runtime data
        /// </summary>
        public void ResetRuntime()
        {
            for (int i = 0; i < mRuntime.HandlerStates.Length; i++)
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
        /// Returns whether the over-time effect's triggers have been activated, based on the provided integer hash.
        /// </summary>
        /// <param name="_hash"></param>
        /// <returns></returns>
        public bool isTriggered(int _hash)
        {
            SetRandomKey(unchecked((uint)_hash));
            return TestTriggers(_hash);
        }

        private void SetRandomKey(ulong? eventId)
        {
            var root = mRuntime.RootState;
            mRuntime.RootState.RandomKey = CombatRandom.Event("effect", root.GetInvolvedEntity("Dealer")?.CombatRandomId,
                root.instanceUid, root.GetInvolvedEntity("Target")?.CombatRandomId, eventId);
        }

        private bool TestTriggers(int _hash)
        {
            ResetRuntime();
            mRuntime.Hash = _hash;
            bool _result = true;
            foreach (var obj in currentRootInstance.ActiveTriggers)
            {
                if (SGD_Settings.Instance.DebugLevel >= 2) Debug.Log("<OverTimeEffectInstance> -> isTriggered:" + obj.Uid);
                if (!obj.isActive(mRuntime)) _result = false;
            }
            return _result;
        }
        /// <summary>
        /// Sets the dealer entity for this over-time effect instance.
        /// </summary>
        /// <param name="_dealer"></param>
        public void SetDealer(Entity _dealer)
        {
            mRuntime.RootState.SetInvolvedEntity("Dealer", _dealer);
        }
        /// <summary>
        /// Sets the target entity for this over-time effect instance.
        /// </summary>
        /// <param name="_target"></param>
        public void SetTarget(Entity _target)
        {
            mRuntime.RootState.SetInvolvedEntity("Target", _target);
        }
        /// <summary>
        /// Constructor to initialize the over-time effect instance with the provided settings.
        /// </summary>
        /// <param name="_setting"></param>
        public OverTimeEffectInstance(OverTimeEffect _setting)
        {
#if MASTER_COMBAT_CORE
            mSetting = _setting;
            GraphObject _graph = (GraphObject)_setting.DesignGraph;
            if (!runtimeInitializedGraph.ContainsKey(_graph))
            {
                string _className = "SoftKitty." + _graph.RootSet.ClassName + "Root";
                Type realRootType = Type.GetType(_className);
                if (realRootType == null)
                {
                    if (SGD_Settings.Instance.DebugLevel > 0) Debug.LogError($"<OverTimeEffectInstance> -> Type '{_className}' not found. Make sure the class name is correct and includes the full namespace if applicable.");
                    return;
                }
                if (!typeof(Root).IsAssignableFrom(realRootType))
                {
                    if (SGD_Settings.Instance.DebugLevel > 0) Debug.LogError($"<OverTimeEffectInstance> -> Type '{_className}' does not inherit from 'Root'.");
                    return;
                }
                object realSubclassInstance = Activator.CreateInstance(realRootType) as Root;
                MethodInfo initMethod = realRootType.GetMethod("InitRoot");
                if (initMethod != null)
                {
                    object result = initMethod.Invoke(realSubclassInstance, new object[1] { _graph.RootSet });
                    Root initializedRoot = result as Root;
                    if (initializedRoot != null)
                    {
                        runtimeInitializedGraph.Add(_graph, initializedRoot);
                        if (SGD_Settings.Instance.DebugLevel >= 3) Debug.Log("<OverTimeEffectInstance> -> GetRootInstance [" + realRootType.ToString() + "] has added!");
                    }
                    else
                    {
                        if (SGD_Settings.Instance.DebugLevel > 0) Debug.LogError("<OverTimeEffectInstance> -> Sorry, your custom class <" + _graph.RootSet.ClassName + "> is not inherit from <Root> class, make sure it inherit from <Root> class and have 'InitRoot' function as example shows.");
                    }
                }
            }
            if (runtimeInitializedGraph.ContainsKey(_graph))
            {
                currentRootInstance = runtimeInitializedGraph[_graph];
                mRuntime = new GraphRuntimeState();
                currentRootInstance.InitRuntimeState(ref mRuntime);
                mRuntime.RootState.instanceUid = mSetting.uid;
            }
#endif
        }
    }
}
