using UnityEngine;
namespace SoftKitty
{
    public class AttributeValueHandler : Handler
    {
        public Entity entity(GraphRuntimeState _runtime)
        { //The owner of the attribute.
            return GetEntity(inputPort[0].connectedNodes[0],_runtime); 
        }
        public string uid; //The script key of the attribute
        public enum logics
        {
            Greater,
            Greater_Or_Equal,
            Equal,
            Less_Or_Equal,
            Less
        }
        public logics logic; //The logic type of the comparison.
        public float value(GraphRuntimeState _runtime)
        { //The value to compare with.
            return GetVariableValue(inputPort[1].connectedNodes[0], _runtime); 
        }

        public AttributeValueHandler(){}

        public AttributeValueHandler(Handler _baseClass) : base(_baseClass) { }

        public AttributeValueHandler InitHandler(Handler _baseClass)
        {
            AttributeValueHandler _newObj = new AttributeValueHandler(_baseClass);
            _newObj.uid = _baseClass.StringValues[0];
            _newObj.logic = (logics)_baseClass.IntValues[0];
            return _newObj;
        }

        int _id=-1;
        bool _result = false;
        public override void Update(GraphRuntimeState _runtime)
        {
            if (_id == -1) _id = GameManager.AttributeData.GetId(uid);
            if (entity(_runtime) == null)
            {
                if (SGD_Settings.Instance.DebugLevel >= 1)Debug.LogError("<AttributeValueHandler> [Node:" + Uid + "] failed to Update(), because the entity is null.");
                return;
            }
            if (entity(_runtime).GetAttributeData(_id)==null)
            {
                if (SGD_Settings.Instance.DebugLevel >= 1) Debug.LogError("<AttributeValueHandler> [Node:" + Uid + "] failed to Update(), because Attribute<" + uid + "> can't be found.");
                return;
            }
            _result = false;
            switch (logic) {
                case logics.Greater:
                    _result = ( entity(_runtime).GetAttributeFloat(_id) > value(_runtime));
                    break;
                case logics.Greater_Or_Equal:
                    _result = (entity(_runtime).GetAttributeFloat(_id) >= value(_runtime));
                    break;
                case logics.Equal:
                    _result = (entity(_runtime).GetAttributeFloat(_id) == value(_runtime));
                    break;
                case logics.Less_Or_Equal:
                    _result = (entity(_runtime).GetAttributeFloat(_id) <= value(_runtime));
                    break;
                case logics.Less:
                    _result = (entity(_runtime).GetAttributeFloat(_id) < value(_runtime));
                    break;
            }
            SetActive(_runtime, _result);
            if (SGD_Settings.Instance.DebugLevel >= 3) Debug.Log("<AttributeValueHandler> [Node:" + Uid + "] -> Condition Update( (Entity)" + entity(_runtime).uid + ".GetAttributeFloat(" + _id + ")="+ entity(_runtime).GetAttributeFloat(_id) + " is " + logic+ " than " + value(_runtime)+") > "+_result);
        }

    }
}
