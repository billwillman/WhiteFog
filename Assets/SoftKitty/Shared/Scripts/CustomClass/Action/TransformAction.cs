using UnityEngine;
namespace SoftKitty{
    public class TransformAction : Action{
        public Entity entity(GraphRuntimeState _runtime) { //The entity to be translate.
            return GetEntity(inputPort[1].connectedNodes[0],_runtime);
        }
        public const int InterruptIndex = 5;
        private bool interruptOnMove;
        public bool move; //Offset the position of the transform. 
        public float posX; //Position X
        public float posY; //Position Y
        public float posZ; //Position Z
        public bool rotate; //Offset the euler angles of the transform.
        public float angleX; //Euler Angle X
        public float angleY; //Euler Angle Y
        public float angleZ; //Euler Angle Z
        public bool scale; //Scale multiplication of the transform
        public float scaleX; //Scale multiplication X
        public float scaleY; //Scale multiplication Y
        public float scaleZ; //Scale multiplication Z
        public bool worldSpace; //Whether using World Space to translate the transform.
        public float magnification(GraphRuntimeState _runtime) { //Magnification of the translating, default as 1.
            return GetVariableValue(inputPort[2].connectedNodes[0],_runtime);
        }
        public float duration; //The duration of the movement takes.
        public bool stickGround; //Make sure the entity stick to ground after movement.
 
        public TransformAction(){}
 
        public TransformAction(Action _baseClass) : base(_baseClass){}
 
        public TransformAction InitAction(Action _baseClass){
            TransformAction  _newObj =new TransformAction(_baseClass);
            _newObj.interruptOnMove = _baseClass.BoolValues.Count > InterruptIndex && _baseClass.BoolValues[InterruptIndex];
            _newObj.move = _baseClass.BoolValues[0];
            _newObj.posX = _baseClass.FloatValues[0];
            _newObj.posY = _baseClass.FloatValues[1];
            _newObj.posZ = _baseClass.FloatValues[2];
            _newObj.rotate = _baseClass.BoolValues[1];
            _newObj.angleX = _baseClass.FloatValues[3];
            _newObj.angleY = _baseClass.FloatValues[4];
            _newObj.angleZ = _baseClass.FloatValues[5];
            _newObj.scale = _baseClass.BoolValues[2];
            _newObj.scaleX = _baseClass.FloatValues[6];
            _newObj.scaleY = _baseClass.FloatValues[7];
            _newObj.scaleZ = _baseClass.FloatValues[8];
            _newObj.worldSpace = _baseClass.BoolValues[3];
            _newObj.duration = _baseClass.FloatValues[9];
            _newObj.stickGround = _baseClass.BoolValues[4];
            return _newObj;
        }
 
        public override void Execute(GraphRuntimeState _runtime){ //Execute this action once
            var recipient = entity(_runtime);
            if (recipient != null)
            {
                var actor = GameManager.FindEntityInstance(recipient);
                if (actor)
                {
                    if (move)
                    {
                        actor.Move(new Vector3(posX, posY, posZ) * magnification(_runtime), duration, worldSpace, stickGround, interruptOnMove);
                        if (SGD_Settings.Instance.DebugLevel >= 3)
                            Debug.Log("<TransformAction> [Node:" + Uid + "] Executed() > Entity:" + entity(_runtime).uid + "  Move:" + (new Vector3(posX, posY, posZ) * magnification(_runtime)).ToString());
                    }
                    if (rotate)
                    {
                        actor.Rotate(new Vector3(angleX, angleY, angleZ) * magnification(_runtime), duration, worldSpace);
                        if (SGD_Settings.Instance.DebugLevel >= 3)
                            Debug.Log("<TransformAction> [Node:" + Uid + "] Executed() > Entity:" + entity(_runtime).uid + "  Rotate:" + (new Vector3(angleX, angleY, angleZ) * magnification(_runtime)).ToString());
                    }
                    if (scale)
                    {
                        actor.Scale((Vector3.one - new Vector3(scaleX, scaleY, scaleZ)) * magnification(_runtime) + Vector3.one, duration);
                        if (SGD_Settings.Instance.DebugLevel >= 3)
                            Debug.Log("<TransformAction> [Node:" + Uid + "] Executed() > Entity:" + entity(_runtime).uid + "  Scale:" + (new Vector3(scaleX, scaleY, scaleZ) * magnification(_runtime) + Vector3.one).ToString());
                    }
                }
                else
                {
                    if (move)
                    {
                        var before = recipient.Position;
                        var offset = new Vector3(posX, posY, posZ) * magnification(_runtime);
                        if (worldSpace)
                        {
                            recipient.SetPosition(before + offset, stickGround);
                        }
                        else
                        {
                            var facing = recipient.Forward.sqrMagnitude > 0f ? Quaternion.LookRotation(recipient.Forward) : Quaternion.identity;
                            recipient.SetPosition(before + facing * offset, stickGround);
                        }
                        if (interruptOnMove && offset != Vector3.zero && recipient.Position != before) recipient.InterruptCast();
                        if (SGD_Settings.Instance.DebugLevel >= 3)
                            Debug.Log("<TransformAction> [Node:" + Uid + "] Executed() > Entity:" + entity(_runtime).uid + "  Move:" + (new Vector3(posX, posY, posZ) * magnification(_runtime)).ToString());
                    }
                    if (rotate)
                    {
                        Quaternion targetRotation = Quaternion.LookRotation(entity(_runtime).Forward);
                        targetRotation = Quaternion.Euler(targetRotation.eulerAngles + new Vector3(angleX, angleY, angleZ));
                        entity(_runtime).Forward = targetRotation * Vector3.forward;
                        if (SGD_Settings.Instance.DebugLevel >= 3)
                            Debug.Log("<TransformAction> [Node:" + Uid + "] Executed() > Entity:" + entity(_runtime).uid + "  Rotate:" + (new Vector3(angleX, angleY, angleZ) * magnification(_runtime)).ToString());
                    }
                    if (scale)
                    {
                        entity(_runtime).Scale = (Vector3.one - new Vector3(scaleX, scaleY, scaleZ)) * magnification(_runtime) + Vector3.one;
                        if (SGD_Settings.Instance.DebugLevel >= 3)
                            Debug.Log("<TransformAction> [Node:" + Uid + "] Executed() > Entity:" + entity(_runtime).uid + "  Scale:" + (new Vector3(scaleX, scaleY, scaleZ) * magnification(_runtime) + Vector3.one).ToString());
                    }
                }
            }
            else if (SGD_Settings.Instance.DebugLevel >= 1)
            {
                Debug.LogError("<TransformAction> [Node:" + Uid + "] failed to Execute(), because the entity is null");
            }
        }
 
    }
}
