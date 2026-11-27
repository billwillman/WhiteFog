using UnityEngine;
namespace SoftKitty{
    public class AttributeChangeAction : Action{
        public Entity entity(GraphRuntimeState _runtime) { //The entity to change the attribute.
            return GetEntity(inputPort[1].connectedNodes[0],_runtime);
        }
        public string uid; //The unique id of the attribute.
        public int id
        {
            get
            {
                return GameManager.AttributeData.GetId(uid);
            }
        }
        public enum operations{
          Override,
          Add,
          Multiply,
          Substract
        }
        public operations operation; //operation of the input value and existing value.
        public float value(GraphRuntimeState _runtime) { //The input value to calculate with the existing value.
            return GetVariableValue(inputPort[2].connectedNodes[0], _runtime);
        }
 
        public AttributeChangeAction(){}
 
        public AttributeChangeAction(Action _baseClass) : base(_baseClass){}
 
        public AttributeChangeAction InitAction(Action _baseClass){
            AttributeChangeAction  _newObj =new AttributeChangeAction(_baseClass);
            _newObj.uid = _baseClass.StringValues[0];
            _newObj.operation = (operations)_baseClass.IntValues[0];
            return _newObj;
        }
        private int _id = -1;
        public override void Execute(GraphRuntimeState _runtime){ //Execute this action once
            if (_id == -1)
            {
                AttributeObject.RequireDefinition(uid, Uid);
                _id = id;
            }
            if (entity(_runtime) != null)
            {
                AttributeData _attValue = entity(_runtime).GetAttributeData(_id);
                if (_attValue != null)
                {
                    var tracedTarget = _runtime.trace != null && uid == "chp" ? entity(_runtime) : null;
                    float before = tracedTarget == null ? 0 : tracedTarget.GetAttributeFloat("chp");
                    switch (operation)
                    {
                        case operations.Override:
                            _attValue.SetValue(value(_runtime), _runtime.RootState.GetInvolvedEntity("Dealer"));
                            if (SGD_Settings.Instance.DebugLevel >= 3)
                                Debug.Log("<AttributeChangeAction> [Node:" + Uid + "] Executed() > (Entity)" + entity(_runtime).uid + ".SetAttributeValue(" +
                                   _id.ToString()+ ", " + value(_runtime).ToString() + " )");
                            break;
                        case operations.Add:
                            _attValue.SetValue(entity(_runtime).GetAttributeFloat(_id) + value(_runtime), _runtime.RootState.GetInvolvedEntity("Dealer"));
                            if (SGD_Settings.Instance.DebugLevel >= 3)
                                Debug.Log("<AttributeChangeAction> [Node:" + Uid + "] Executed() > (Entity)" + entity(_runtime).uid + ".AddAttributeValue(" +
                                   _id.ToString() + ", " + value(_runtime).ToString() + " )");
                            break;
                        case operations.Multiply:
                            _attValue.SetValue(entity(_runtime).GetAttributeFloat(_id) * value(_runtime), _runtime.RootState.GetInvolvedEntity("Dealer"));
                            if (SGD_Settings.Instance.DebugLevel >= 3)
                                Debug.Log("<AttributeChangeAction> [Node:" + Uid + "] Executed() > (Entity)" + entity(_runtime).uid + ".AddAttributeValue(" +
                                   _id.ToString() + ", " + (entity(_runtime).GetAttributeFloat(_id) * value(_runtime)).ToString() + " )");
                            break;
                        case operations.Substract:
                            _attValue.SetValue(entity(_runtime).GetAttributeFloat(_id) - SkillDefence.Incoming(_attValue, value(_runtime)), _runtime.RootState.GetInvolvedEntity("Dealer"));
                            if (SGD_Settings.Instance.DebugLevel >= 3)
                                Debug.Log("<AttributeChangeAction> [Node:" + Uid + "] Executed() > (Entity)" + entity(_runtime).uid + ".SetAttributeValue(" +
                                   _id.ToString() + ", " + (entity(_runtime).GetAttributeFloat(_id) - value(_runtime)).ToString() + " )");
                            break;
                    }
                    if (tracedTarget != null) _runtime.TraceLoss(Uid, tracedTarget, before);
                }
                else if (SGD_Settings.Instance.DebugLevel >= 1)
                {
                    Debug.LogError("<AttributeChangeAction> [Node:" + Uid + "] failed to Execute(), because Attribute<" + uid + "> can't be found");
                }
            }
            else if (SGD_Settings.Instance.DebugLevel >= 1)
            {
                Debug.LogError("<AttributeChangeAction> [Node:" + Uid + "] failed to Execute(), because the entity is null");
            }
        }
 
    }
}
