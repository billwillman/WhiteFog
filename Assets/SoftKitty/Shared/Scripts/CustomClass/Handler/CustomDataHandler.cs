using UnityEngine;
namespace SoftKitty{
    public class CustomDataHandler:Handler{
        public Entity entity(GraphRuntimeState _runtime) { //The entity to retrieve CustomData from.
            return GetEntity(inputPort[0].connectedNodes[0],_runtime);
        }
        public enum types{
          Float_or_Int,
          Bool,
          Int_List,
          ID_Value_List
        }
        public types type; //The type of the CustomData.
        public string uid; //The unique string ID of the CustomData
        public int index; //The index number of the list.
        public int id; //The id of the IdInt or IdFloat value.
        public enum logics{
          Greater,
          Greater_Or_Equal,
          Equal,
          Less_Or_Equal,
          Less,
          Not_Equal
        }
        public logics logic; //Comparison method.
        public float value(GraphRuntimeState _runtime) { //The value to compare with.
            return GetVariableValue(inputPort[1].connectedNodes[0],_runtime);
        }
 
        public CustomDataHandler(){}
 
        public CustomDataHandler(Handler _baseClass) : base(_baseClass){}
 
        public CustomDataHandler InitHandler(Handler _baseClass){
            CustomDataHandler  _newObj =new CustomDataHandler(_baseClass);
            _newObj.type = (types)_baseClass.IntValues[0];
            _newObj.uid = _baseClass.StringValues[0];
            _newObj.index = _baseClass.IntValues[1];
            _newObj.id = _baseClass.IntValues[2];
            _newObj.logic = (logics)_baseClass.IntValues[3];
            return _newObj;
        }

        private bool ComapreValue(float _value, GraphRuntimeState _runtime)
        {
            switch (logic)
            {
                case logics.Greater:
                    return _value > value(_runtime);
                case logics.Greater_Or_Equal:
                    return _value >= value(_runtime);
                case logics.Equal:
                    return _value == value(_runtime);
                case logics.Less_Or_Equal:
                    return _value <= value(_runtime);
                case logics.Less:
                    return _value < value(_runtime);
                case logics.Not_Equal:
                    return _value != value(_runtime);
            }
            return false;
        }

        private bool ComapreValue(int _value, GraphRuntimeState _runtime)
        {
            switch (logic)
            {
                case logics.Greater:
                    return _value > Mathf.FloorToInt(value(_runtime));
                case logics.Greater_Or_Equal:
                    return _value >= Mathf.FloorToInt(value(_runtime));
                case logics.Equal:
                    return _value == Mathf.FloorToInt(value(_runtime));
                case logics.Less_Or_Equal:
                    return _value <= Mathf.FloorToInt(value(_runtime));
                case logics.Less:
                    return _value < Mathf.FloorToInt(value(_runtime));
                case logics.Not_Equal:
                    return _value != Mathf.FloorToInt(value(_runtime));
            }
            return false;
        }

        float _valuefloat;
        bool _result;
        int _valueInt;
        public override void Update(GraphRuntimeState _runtime){ //Update state of the handler
            if (entity(_runtime) == null)
            {
                if (SGD_Settings.Instance.DebugLevel >= 1)
                {
                    Debug.LogError("<CustomDataHandler> [Node:" + Uid + "] failed to Update(), because the entity is null");
                }
                return;
            }
            var _dataType = GameManager.EntityManagerData.GetCustomType(uid);
            _result = false;
            switch (_dataType)
            {
                case CustomData.CustomTypes.Float:
                     _valuefloat= entity(_runtime).GetCustomFloat(uid);
                    _result = ComapreValue(_valuefloat, _runtime);
                    if (SGD_Settings.Instance.DebugLevel >= 3)
                        Debug.Log("<CustomDataHandler> [Node:" + Uid + "] Condition Update("+ _valuefloat + "/"+ value(_runtime) + ") >  " + _result.ToString());
                    break;
                case CustomData.CustomTypes.Int:
                    _valueInt = entity(_runtime).GetCustomInt(uid);
                    _result = ComapreValue(_valueInt, _runtime);
                    if (SGD_Settings.Instance.DebugLevel >= 3)
                        Debug.Log("<CustomDataHandler> [Node:" + Uid + "] Condition Update(" + _valueInt + "/" + Mathf.FloorToInt(value(_runtime)) + ") >  " + _result.ToString());
                    break;
                case CustomData.CustomTypes.Bool:
                    _result = entity(_runtime).GetCustomBool(uid);
                    if (SGD_Settings.Instance.DebugLevel >= 3)
                        Debug.Log("<CustomDataHandler> [Node:" + Uid + "] Condition Update( ) >  " + _result.ToString());
                    break;
                case CustomData.CustomTypes.IntList:
                    _valueInt = entity(_runtime).GetCustomIntList(uid).GetValueByIndex(index);
                    _result = ComapreValue(_valueInt, _runtime);
                    if (SGD_Settings.Instance.DebugLevel >= 3)
                        Debug.Log("<CustomDataHandler> [Node:" + Uid + "] Condition Update(" + _valueInt + "/" + Mathf.FloorToInt(value(_runtime)) + ") >  " + _result.ToString());
                    break;
                case CustomData.CustomTypes.IdIntList:
                    _valueInt = entity(_runtime).GetCustomIdIntList(uid).GetValueById(id);
                    _result = ComapreValue(_valueInt, _runtime);
                    if (SGD_Settings.Instance.DebugLevel >= 3)
                        Debug.Log("<CustomDataHandler> [Node:" + Uid + "] Condition Update(" + _valueInt + "/" + Mathf.FloorToInt(value(_runtime)) + ") >  " + _result.ToString());
                    break;
                case CustomData.CustomTypes.IdFloatList:
                    _valuefloat = entity(_runtime).GetCustomIdFloatList(uid).GetValueById(id);
                    _result = ComapreValue(_valuefloat, _runtime);
                    if (SGD_Settings.Instance.DebugLevel >= 3)
                        Debug.Log("<CustomDataHandler> [Node:" + Uid + "] Condition Update(" + _valuefloat + "/" + value(_runtime) + ") >  " + _result.ToString());
                    break;
                default:
                    if (SGD_Settings.Instance.DebugLevel >= 1)
                        Debug.LogError("<CustomDataHandler> [Node:" + Uid + "] failed to Condition Update(), " +
                            "because the type of the CustomData (" + _dataType.ToString() + ") is not supported. [Data UID:" + uid + "]");
                    break;
            }
            SetActive(_runtime,_result);
        }
 
    }
}
