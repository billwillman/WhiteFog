using UnityEngine;
namespace SoftKitty{
    public class DestroyAction : Action{
        public Entity entity(GraphRuntimeState _runtime)
        { //Entity to destroy.
           return GetEntity(inputPort[1].connectedNodes[0],_runtime);
        }
        public float wait; //Destroy after 'Wait Time' in seconds.
 
        public DestroyAction(){}
 
        public DestroyAction(Action _baseClass) : base(_baseClass){}
 
        public DestroyAction InitAction(Action _baseClass){
            DestroyAction  _newObj =new DestroyAction(_baseClass);
            _newObj.wait = _baseClass.FloatValues[0];
            return _newObj;
        }
 
        public override void Execute(GraphRuntimeState _runtime){ //Execute this action once
            if (entity(_runtime) != null)
            {
                if (GameManager.GetEntityInstance(entity(_runtime).uid) != null)
                {
                    GameObject.Destroy(GameManager.GetEntityInstance(entity(_runtime).uid).gameObject,wait);
                    if (SGD_Settings.Instance.DebugLevel >= 3)
                        Debug.Log("<DestroyAction> [Node:" + Uid + "] Executed() > Entity:" + entity(_runtime).uid + "  Destroied." );
                }
            }
            else if (SGD_Settings.Instance.DebugLevel >= 1)
            {
                Debug.LogError("<DestroyAction> [Node:" + Uid + "] failed to Execute(), because the entity is null");
            }
        }
 
    }
}
