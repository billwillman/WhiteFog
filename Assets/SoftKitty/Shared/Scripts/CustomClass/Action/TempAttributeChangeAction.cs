using UnityEngine;
namespace SoftKitty{
    public class TempAttributeChangeAction : Action{
        public Entity entity(GraphRuntimeState _runtime) { //The entity to change the attribute.
            return GetEntity(inputPort[1].connectedNodes[0],_runtime);
        }
        public string uid; //The script key of the attribute.
        public enum operations{
          Add,
          Multiply,
          Override,
          Max,
          Min
        }
        public operations operation; //operation of the input value and existing value.
        public float value(GraphRuntimeState _runtime) { //The input value to calculate with the existing value.
            return GetVariableValue(inputPort[2].connectedNodes[0],_runtime);
        }
        public float duration; //How long (in seconds) this temporary change will take effect.
 
        public TempAttributeChangeAction(){}
 
        public TempAttributeChangeAction(Action _baseClass) : base(_baseClass){}
 
        public TempAttributeChangeAction InitAction(Action _baseClass){
            TempAttributeChangeAction  _newObj =new TempAttributeChangeAction(_baseClass);
            _newObj.uid = _baseClass.StringValues[0];
            _newObj.operation = (operations)_baseClass.IntValues[0];
            _newObj.duration = _baseClass.FloatValues[0];
            return _newObj;
        }
 
        public override void Execute(GraphRuntimeState _runtime){ //Execute this action once
            if (entity(_runtime) != null)
            {
                entity(_runtime).AddTempAttributes(uid, value(_runtime), duration, (BasicMathFunction)operation, _runtime.RootState.instanceUid);
                if (SGD_Settings.Instance.DebugLevel >= 3)
                    Debug.Log("<TempAttributeChangeAction> [Node:" + Uid + "] Executed() > (Entity)" 
                        + entity(_runtime).uid + ".AddTempAttributes("+ uid+", "+ value(_runtime)+", "+ duration+", "+ operation.ToString()+", "+ _runtime.RootState.instanceUid);
            }
            else if (SGD_Settings.Instance.DebugLevel >= 1)
            {
                Debug.LogError("<TempAttributeChangeAction> [Node:" + Uid + "] failed to Execute(), because the entity is null");
            }
        }
 
    }
}
