using UnityEngine;
namespace SoftKitty{
    public class CustomDataAction:Action{
        public Entity entity(GraphRuntimeState _runtime) { //Entity to override the CustomData.
            return GetEntity(inputPort[1].connectedNodes[0],_runtime);
        }
        public enum types{
          Int_or_Float,
          Bool,
          Int_List,
          ID_Value_List
        }
        public types type; //The type of the CustomData
        public int index; //The index number of the list
        public int id; //The ID of IdInt or IdFloat value.
        public string uid; //The unique string id of the CustomData.
        public bool boolValue; //Override the CustomData with this bool value.
        public float value(GraphRuntimeState _runtime) { //Override the CustomData with this value.
            return GetVariableValue(inputPort[2].connectedNodes[0],_runtime);
        }
 
        public CustomDataAction(){}
 
        public CustomDataAction(Action _baseClass) : base(_baseClass){}
 
        public CustomDataAction InitAction(Action _baseClass){
            CustomDataAction  _newObj =new CustomDataAction(_baseClass);
            _newObj.type = (types)_baseClass.IntValues[0];
            _newObj.index = _baseClass.IntValues[1];
            _newObj.id = _baseClass.IntValues[2];
            _newObj.uid = _baseClass.StringValues[0];
            _newObj.boolValue = _baseClass.BoolValues[0];
            return _newObj;
        }
 
        public override void Execute(GraphRuntimeState _runtime){ //Execute this action once
            if (entity(_runtime) != null)
            {
                var _dataType = GameManager.EntityManagerData.GetCustomType(uid);
                switch (_dataType) {
                    case  CustomData.CustomTypes.Float:
                        entity(_runtime).SetCustomFloat(uid, value(_runtime));
                        if (SGD_Settings.Instance.DebugLevel >= 3) 
                            Debug.Log("<CustomDataAction> [Node:" + Uid + "] Executed() > (Entity)" + entity(_runtime).uid 
                                + ".SetCustomFloat(" + uid + ", " + value(_runtime) + ")");
                        break;
                    case CustomData.CustomTypes.Int:
                        entity(_runtime).SetCustomInt(uid, Mathf.FloorToInt(value(_runtime)));
                        if (SGD_Settings.Instance.DebugLevel >= 3)
                            Debug.Log("<CustomDataAction> [Node:" + Uid + "] Executed() > (Entity)" + entity(_runtime).uid
                                + ".SetCustomInt(" + uid + ", " + Mathf.FloorToInt(value(_runtime)) + ")");
                        break;
                    case CustomData.CustomTypes.Bool:
                        entity(_runtime).SetCustomBool(uid, boolValue);
                        if (SGD_Settings.Instance.DebugLevel >= 3)
                            Debug.Log("<CustomDataAction> [Node:" + Uid + "] Executed() > (Entity)" + entity(_runtime).uid
                                + ".SetCustomBool(" + uid + ", " + boolValue + ")");
                        break;
                    case CustomData.CustomTypes.IntList:
                        var _list=entity(_runtime).GetCustomIntList(uid);
                        _list.SetValueByIndex(index, Mathf.FloorToInt(value(_runtime)));
                        if (SGD_Settings.Instance.DebugLevel >= 3)
                            Debug.Log("<CustomDataAction> [Node:" + Uid + "] Executed() > (Entity)" + entity(_runtime).uid
                                + ".SetValueByIndex(" + index + ", " + Mathf.FloorToInt(value(_runtime)) + ") [IntList Data UID:" + uid+"]");
                        break;
                    case CustomData.CustomTypes.IdIntList:
                        var _list2 = entity(_runtime).GetCustomIdIntList(uid);
                        _list2.SetValueById(id, Mathf.FloorToInt(value(_runtime)));
                        if (SGD_Settings.Instance.DebugLevel >= 3)
                            Debug.Log("<CustomDataAction> [Node:" + Uid + "] Executed() > (Entity)" + entity(_runtime).uid
                                + ".SetValueById(" + id + ", " + Mathf.FloorToInt(value(_runtime)) + ") [IdIntList Data UID:" + uid + "]");
                        break;
                    case CustomData.CustomTypes.IdFloatList:
                        var _list3 = entity(_runtime).GetCustomIdFloatList(uid);
                        _list3.SetValueById(id, value(_runtime));
                        if (SGD_Settings.Instance.DebugLevel >= 3)
                            Debug.Log("<CustomDataAction> [Node:" + Uid + "] Executed() > (Entity)" + entity(_runtime).uid
                                + ".SetValueById(" + id + ", " + value(_runtime) + ") [IdFloatList Data UID:" + uid + "]");
                        break;
                    default:
                        if (SGD_Settings.Instance.DebugLevel >= 1)
                            Debug.LogError("<CustomDataAction> [Node:" + Uid + "] failed to Execute(), " +
                                "because the type of the CustomData ("+ _dataType.ToString()+ ") is not supported. [Data UID:" + uid + "]");
                        break;
                }
            }
            else if (SGD_Settings.Instance.DebugLevel >= 1)
            {
                Debug.LogError("<CustomDataAction> [Node:" + Uid + "] failed to Execute(), because the entity is null");
            }
        }
 
    }
}
