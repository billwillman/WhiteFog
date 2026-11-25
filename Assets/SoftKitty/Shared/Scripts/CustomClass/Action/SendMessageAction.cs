using UnityEngine;
namespace SoftKitty{
    public class SendMessageAction : Action{
        public Entity entity(GraphRuntimeState _runtime) { //The entity to send message to, if no entity is connected, the message will be send to the gameObject carry this asset itself.
            return GetEntity(inputPort[1].connectedNodes[0],_runtime);
        }
        public string msg; //The name of the function send with the message.
        public enum argTypes{
          None,
          Float,
          Dealer_Entity,
          Target_Entity,
          Specified_Entity,
          this_transform
        }
        public argTypes argType; //The type of the arg send with the message.
        public float argValue(GraphRuntimeState _runtime)
        { //The value of the arg send with the message.
            return GetVariableValue(inputPort[2].connectedNodes[0],_runtime);
        }
        public string argEntityId; //The uid of the Specified Entity as arg.
 
        public SendMessageAction(){}
 
        public SendMessageAction(Action _baseClass) : base(_baseClass){}
 
        public SendMessageAction InitAction(Action _baseClass){
            SendMessageAction  _newObj =new SendMessageAction(_baseClass);
            _newObj.msg = _baseClass.StringValues[0];
            _newObj.argType = (argTypes)_baseClass.IntValues[0];
            _newObj.argEntityId = _baseClass.StringValues[1];
            return _newObj;
        }

        public override void Execute(GraphRuntimeState _runtime)
        { //Execute this action once
            if (entity(_runtime) != null)
            {
                if (GameManager.GetEntityInstance(entity(_runtime).uid) != null)
                {
                    if (argType == argTypes.this_transform)
                    {
                        GameManager.GetEntityInstance(entity(_runtime).uid).SendMessage(msg,
                     _runtime.RootState.RootInstance.transform,
                     SendMessageOptions.DontRequireReceiver
                     );
                        if (SGD_Settings.Instance.DebugLevel >= 3)
                            Debug.Log("<SendMessageAction> [Node:" + Uid + "] Executed() > (Entity)" + entity(_runtime).uid + ".SendMessage(" +
                                msg+", " + _runtime.RootState.RootInstance.name+")");
                    }
                    else if (argType == argTypes.Float)
                    {
                        GameManager.GetEntityInstance(entity(_runtime).uid).SendMessage(msg,
                      argValue(_runtime),
                      SendMessageOptions.DontRequireReceiver
                      );
                        if (SGD_Settings.Instance.DebugLevel >= 3)
                            Debug.Log("<SendMessageAction> [Node:" + Uid + "] Executed() > (Entity)" + entity(_runtime).uid + ".SendMessage(" +
                                msg + ", " + argValue(_runtime) + ")");
                    }
                    else if (argType == argTypes.None)
                    {
                        GameManager.GetEntityInstance(entity(_runtime).uid).SendMessage(msg,
                      SendMessageOptions.DontRequireReceiver
                      );
                        if (SGD_Settings.Instance.DebugLevel >= 3)
                            Debug.Log("<SendMessageAction> [Node:" + Uid + "] Executed() > (Entity)" + entity(_runtime).uid + ".SendMessage(" +
                                msg + ")");
                    }
                    else
                    {
                        GameManager.GetEntityInstance(entity(_runtime).uid).SendMessage(msg,
                      GetArgEntity(_runtime),
                      SendMessageOptions.DontRequireReceiver
                      );
                        if (SGD_Settings.Instance.DebugLevel >= 3)
                            Debug.Log("<SendMessageAction> [Node:" + Uid + "] Executed() > (Entity)" + entity(_runtime).uid + ".SendMessage(" +
                                msg + ", " + GetArgEntity(_runtime).uid + ")");
                    }
                }
                else if (SGD_Settings.Instance.DebugLevel >= 1)
                {
                    Debug.LogError("<SendMessageAction> [Node:" + Uid + "] failed to Execute(), because the entity instance does not exists in the scene.");
                }
            }
            else if (SGD_Settings.Instance.DebugLevel >= 1)
            {
                Debug.LogError("<SendMessageAction> [Node:" + Uid + "] failed to Execute(), because the entity is null");
            }
        }

        private Entity GetArgEntity(GraphRuntimeState _runtime)
        {
            if (argType == argTypes.Target_Entity)
            {
                return _runtime.RootState.GetInvolvedEntity("Target");
            }
            else if (argType == argTypes.Dealer_Entity)
            {
                return _runtime.RootState.GetInvolvedEntity("Dealer");
            }
            else
            {
                return GameManager.GetEntity(argEntityId);
            }
        }

    }
}
