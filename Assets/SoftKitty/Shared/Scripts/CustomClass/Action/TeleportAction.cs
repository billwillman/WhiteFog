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
        public const int InterruptIndex = 1;
        private bool interruptOnMove;
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
            _newObj.interruptOnMove = _baseClass.BoolValues.Count > InterruptIndex && _baseClass.BoolValues[InterruptIndex];
            _newObj.stickGround = _baseClass.BoolValues[0];
            return _newObj;
        }
 
        public override void Execute(GraphRuntimeState state)
        {
            var recipient = entity(state);
            if (recipient == null)
            {
                if (SGD_Settings.Instance.DebugLevel >= 1) Debug.LogError("<TeleportAction> [" + Uid + "] entity is null.");
                return;
            }
            var destination = new Vector3(positionX, positionY, positionZ);
            if (positionType == positionTypes.Entity)
            {
                var anchor = targetEntity(state);
                if (anchor == null)
                {
                    if (SGD_Settings.Instance.DebugLevel >= 1) Debug.LogError("<TeleportAction> [" + Uid + "] targetEntity is null.");
                    return;
                }
                destination = anchor.Position + new Vector3(offsetX, offsetY, offsetZ);
            }
            var actor = GameManager.FindEntityInstance(recipient);
            var before = actor ? actor.Position : recipient.Position;
            if (actor) actor.SetPosition(destination, stickGround);
            else recipient.SetPosition(destination, stickGround);
            var after = actor ? actor.Position : recipient.Position;
            if (interruptOnMove && after != before) recipient.InterruptCast();
            if (SGD_Settings.Instance.DebugLevel >= 3) Debug.Log("<TeleportAction> [" + Uid + "] " + recipient.uid + " teleported to " + after);
        }
 
    }
}
