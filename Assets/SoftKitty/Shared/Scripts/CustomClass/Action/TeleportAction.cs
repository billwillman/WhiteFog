using UnityEngine;
namespace SoftKitty{
    public class TeleportAction : Action{
        public Entity entity(GraphRuntimeState _runtime)
        { //Entity to teleport.
            return GetEntity(inputPort[1].connectedNodes[0],_runtime);
        }
        public enum positionTypes{
          Entity,
          Fixed_Position
        }
        public positionTypes positionType; //The type of the target position.
        public Entity targetEntity(GraphRuntimeState _runtime)
        { //The target entity to teleport to.
            return GetEntity(inputPort[2].connectedNodes[0], _runtime);
        }
        public float offsetX; //X axis position offset from the target entity.
        public float offsetY; //Y axis position offset from the target entity.
        public float offsetZ; //X axis position offset from the target entity.
        public float positionX; //X axis of the target position.
        public float positionY; //Y axis of the target position.
        public float positionZ; //Z axis of the target position.
        public bool stickGround; //Make sure the entity stick to ground after teleportation.
 
        public TeleportAction(){}
 
        public TeleportAction(Action _baseClass) : base(_baseClass){}
 
        public TeleportAction InitAction(Action _baseClass){
            TeleportAction  _newObj =new TeleportAction(_baseClass);
            _newObj.positionType = (positionTypes)_baseClass.IntValues[0];
            _newObj.offsetX = _baseClass.FloatValues[0];
            _newObj.offsetY = _baseClass.FloatValues[1];
            _newObj.offsetZ = _baseClass.FloatValues[2];
            _newObj.positionX = _baseClass.FloatValues[3];
            _newObj.positionY = _baseClass.FloatValues[4];
            _newObj.positionZ = _baseClass.FloatValues[5];
            _newObj.stickGround = _baseClass.BoolValues[0];
            return _newObj;
        }
 
        public override void Execute(GraphRuntimeState _runtime){ //Execute this action once
            if (entity(_runtime) != null)
            {
                if (GameManager.GetEntityInstance(entity(_runtime).uid) != null)
                {
                    if (positionType == positionTypes.Entity)
                    {
                        if (targetEntity(_runtime) != null)
                        {
                            GameManager.GetEntityInstance(entity(_runtime).uid).SetPosition(targetEntity(_runtime).Position+ new Vector3(offsetX, offsetY, offsetZ), stickGround);
                            if (SGD_Settings.Instance.DebugLevel >= 3)
                                Debug.Log("<TeleportAction> [Node:" + Uid + "] Executed() > Entity:" + entity(_runtime).uid + "  Teleported to:" + targetEntity(_runtime).uid);
                        }
                        else if (SGD_Settings.Instance.DebugLevel >= 1)
                        {
                            Debug.LogError("<TeleportAction> [Node:" + Uid + "] failed to Execute(), because the targetEntity is null");
                        }
                    }
                    else
                    {
                        GameManager.GetEntityInstance(entity(_runtime).uid).SetPosition(new Vector3(positionX, positionY, positionZ), stickGround);
                        if (SGD_Settings.Instance.DebugLevel >= 3)
                            Debug.Log("<TeleportAction> [Node:" + Uid + "] Executed() > Entity:" + entity(_runtime).uid + "  Teleported to:" +(new Vector3(positionX, positionY, positionZ)).ToString() );
                    }
                }
                else
                {
                    if (positionType == positionTypes.Entity)
                    {
                        if (targetEntity(_runtime) != null)
                        {
                            entity(_runtime).SetPosition(targetEntity(_runtime).Position + new Vector3(offsetX, offsetY, offsetZ), stickGround);
                            if (SGD_Settings.Instance.DebugLevel >= 3)
                                Debug.Log("<TeleportAction> [Node:" + Uid + "] Executed() > Entity:" + entity(_runtime).uid + "  Teleported to:" + targetEntity(_runtime).uid);
                        }
                        else if (SGD_Settings.Instance.DebugLevel >= 1)
                        {
                            Debug.LogError("<TeleportAction> [Node:" + Uid + "] failed to Execute(), because the targetEntity is null");
                        }
                    }
                    else
                    {
                        entity(_runtime).SetPosition(new Vector3(positionX, positionY, positionZ), stickGround);
                        if (SGD_Settings.Instance.DebugLevel >= 3)
                            Debug.Log("<TeleportAction> [Node:" + Uid + "] Executed() > Entity:" + entity(_runtime).uid + "  Teleported to:" + (new Vector3(positionX, positionY, positionZ)).ToString());
                    }
                }
            }
            else if (SGD_Settings.Instance.DebugLevel >= 1)
            {
                Debug.LogError("<TeleportAction> [Node:" + Uid + "] failed to Execute(), because the entity is null");
            }
        }
 
    }
}
