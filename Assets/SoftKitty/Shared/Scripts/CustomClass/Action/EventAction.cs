using UnityEngine;
namespace SoftKitty{
    public class EventAction:Action{
        public Entity entity(GraphRuntimeState _runtime) { //Entity to recieve the event.
            return GetEntity(inputPort[1].connectedNodes[0], _runtime);
        }
        public string eventUid; //The unique name of the event, please not to include space and special characters.
        public bool enableFloatArg; //Call the event with connected float value as arg.
        public float floatArg(GraphRuntimeState _runtime) { //Call the event with connected float value as arg.
            return GetVariableValue(inputPort[2].connectedNodes[0], _runtime);
        }
        public bool enableEntityArg; //Call the event with connected entity as arg.
        public Entity entityArg(GraphRuntimeState _runtime) { //Call the event with connected entity as arg.
            return GetEntity(inputPort[3].connectedNodes[0], _runtime);
        }
        public bool enableBoolArg; //Call the event with a bool value as arg.
        public bool boolArg; //Call the event with a bool value as arg.
 
        public EventAction(){}
 
        public EventAction(Action _baseClass) : base(_baseClass){}
 
        public EventAction InitAction(Action _baseClass){
            EventAction  _newObj =new EventAction(_baseClass);
            _newObj.eventUid = _baseClass.StringValues[0];
            _newObj.enableFloatArg = _baseClass.BoolValues[0];
            _newObj.enableEntityArg = _baseClass.BoolValues[1];
            _newObj.enableBoolArg = _baseClass.BoolValues[2];
            _newObj.boolArg = _baseClass.BoolValues[3];
            return _newObj;
        }
 
        public override void Execute(GraphRuntimeState _runtime)
        { //Execute this action once
            if (entity(_runtime) != null)
            {
                entity(_runtime).OnEntityEvent(eventUid, 
                    enableFloatArg ? floatArg(_runtime) : 0F, 
                    enableEntityArg ? entityArg(_runtime) : null, 
                    enableBoolArg ? boolArg : false);

                if (SGD_Settings.Instance.DebugLevel >= 3)
                    Debug.Log("<EventAction> [Node:" + Uid + "] Executed() > (Entity)" + entity(_runtime).uid + ".OnEntityEvent("
                        + eventUid+", "+ (enableFloatArg ? floatArg(_runtime) : 0F).ToString()
                        +", "+(enableEntityArg ? entityArg(_runtime).uid : "")
                        +", "+(enableBoolArg? boolArg.ToString():"false"));
            }
            else if (SGD_Settings.Instance.DebugLevel >= 1)
            {
                Debug.LogError("<EventAction> [Node:" + Uid + "] failed to Execute(), because the entity is null");
            }
        }
 
    }
}
