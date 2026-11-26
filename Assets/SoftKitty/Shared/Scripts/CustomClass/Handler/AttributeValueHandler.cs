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
            var selector = _runtime.AttributeSelector(inputPort[0].connectedNodes[0]);
            var owner = selector?.GetEntity(_runtime);
            if (owner == null)
            {
                if (SGD_Settings.Instance.DebugLevel >= 1)Debug.LogError("<AttributeValueHandler> [Node:" + Uid + "] failed to Update(), because the entity is null.");
                return;
            }
            bool snapshotInput = _runtime.CasterSnapshot?.IsSource(owner, selector) == true;
            if (!snapshotInput && _id == -1) _id = GameManager.AttributeData.GetId(uid);
            if (!snapshotInput && owner.GetAttributeData(_id) == null)
            {
                if (SGD_Settings.Instance.DebugLevel >= 1) Debug.LogError("<AttributeValueHandler> [Node:" + Uid + "] failed to Update(), because Attribute<" + uid + "> can't be found.");
                return;
            }
            float actual = _runtime.ReadAttribute(owner, selector, uid);
            _result = false;
            switch (logic) {
                case logics.Greater:
                    _result = ( actual > value(_runtime));
                    break;
                case logics.Greater_Or_Equal:
                    _result = (actual >= value(_runtime));
                    break;
                case logics.Equal:
                    _result = (actual == value(_runtime));
                    break;
                case logics.Less_Or_Equal:
                    _result = (actual <= value(_runtime));
                    break;
                case logics.Less:
                    _result = (actual < value(_runtime));
                    break;
            }
            SetActive(_runtime, _result);
            if (SGD_Settings.Instance.DebugLevel >= 3) Debug.Log("<AttributeValueHandler> [Node:" + Uid + "] -> Condition Update( (Entity)" + owner.uid + ".GetAttributeFloat(" + uid + ")="+ actual + " is " + logic+ " than " + value(_runtime)+") > "+_result);
        }

    }
}
