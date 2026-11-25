using UnityEngine;
namespace SoftKitty{
    public class TagCompareHandler : Handler{
        public Entity entityA(GraphRuntimeState _runtime)
        { //The entity for comparison.
            return GetEntity(inputPort[0].connectedNodes[0],_runtime);
        }
        public Entity entityB(GraphRuntimeState _runtime)
        { //The entity for comparison.
            return GetEntity(inputPort[1].connectedNodes[0], _runtime);
        }
        public int index; //Index of the tag for The entity for comparison, start with 0.
        public enum logics{
          Same,
          Different
        }
        public logics logic; //Logic for comparison.
 
        public TagCompareHandler(){}
 
        public TagCompareHandler(Handler _baseClass) : base(_baseClass){}
 
        public TagCompareHandler InitHandler(Handler _baseClass){
            TagCompareHandler  _newObj =new TagCompareHandler(_baseClass);
            _newObj.index = _baseClass.IntValues[0];
            _newObj.logic = (logics)_baseClass.IntValues[1];
            return _newObj;
        }
        bool _match;
        bool _result;
        public override void Update(GraphRuntimeState _runtime){ //Update state of the handler
            if (entityA(_runtime) == null)
            {
                if (SGD_Settings.Instance.DebugLevel >= 1) Debug.LogError("<TagCompareHandler> [Node:" + Uid + "] failed to Update(), because the entityA is null.");
                return;
            }
            if (entityB(_runtime) == null)
            {
                if (SGD_Settings.Instance.DebugLevel >= 1) Debug.LogError("<TagCompareHandler> [Node:" + Uid + "] failed to Update(), because the entityB is null.");
                return;
            }

            if (entityA(_runtime).Tags.Count<=index || entityB(_runtime).Tags.Count <= index) {
                if (SGD_Settings.Instance.DebugLevel >= 1) Debug.LogError("<TagCompareHandler> [Node:" + Uid + "] failed to Update(), because one of the entity does not have " + (index+1).ToString()+" tags.");
                return;
            }
            _match = (entityA(_runtime).Tags[index] == entityB(_runtime).Tags[index]);
            _result = (_match && logic == logics.Same) || (!_match && logic == logics.Different);
            SetActive(_runtime, _result);
            if (SGD_Settings.Instance.DebugLevel >= 3) Debug.Log("<TagCompareHandler> [Node:" + Uid + "] -> Condition Update( ["+ entityA(_runtime).uid + "]" + entityA(_runtime).Tags[index] + " is "+logic+" as [" + entityB(_runtime).uid + "]" + entityB(_runtime).Tags[index] + ") > "+ _result);
        }
 
    }
}
