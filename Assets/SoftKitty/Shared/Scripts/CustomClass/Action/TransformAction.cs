using UnityEngine;
namespace SoftKitty{
    public class TransformAction : Action{
        public Entity entity(GraphRuntimeState _runtime) { //The entity to be translate.
            return GetEntity(inputPort[1].connectedNodes[0],_runtime);
        }
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
            if (entity(_runtime) != null)
            {
                if (GameManager.GetEntityInstance(entity(_runtime).uid) != null)
                {
                    if (move)
                    {
                        GameManager.GetEntityInstance(entity(_runtime).uid).Move(new Vector3(posX, posY, posZ) * magnification(_runtime), duration, worldSpace);
                        if (SGD_Settings.Instance.DebugLevel >= 3)
                            Debug.Log("<TransformAction> [Node:" + Uid + "] Executed() > Entity:" + entity(_runtime).uid + "  Move:" + (new Vector3(posX, posY, posZ) * magnification(_runtime)).ToString());
                    }
                    if (rotate)
                    {
                        GameManager.GetEntityInstance(entity(_runtime).uid).Rotate(new Vector3(angleX, angleY, angleZ) * magnification(_runtime), duration, worldSpace);
                        if (SGD_Settings.Instance.DebugLevel >= 3)
                            Debug.Log("<TransformAction> [Node:" + Uid + "] Executed() > Entity:" + entity(_runtime).uid + "  Rotate:" + (new Vector3(angleX, angleY, angleZ) * magnification(_runtime)).ToString());
                    }
                    if (scale)
                    {
                        GameManager.GetEntityInstance(entity(_runtime).uid).Scale((Vector3.one - new Vector3(scaleX, scaleY, scaleZ)) * magnification(_runtime) + Vector3.one, duration);
                        if (SGD_Settings.Instance.DebugLevel >= 3)
                            Debug.Log("<TransformAction> [Node:" + Uid + "] Executed() > Entity:" + entity(_runtime).uid + "  Scale:" + (new Vector3(scaleX, scaleY, scaleZ) * magnification(_runtime) + Vector3.one).ToString());
                    }
                }
                else
                {
                    if (move)
                    {
                        if (worldSpace)
                        {
                            entity(_runtime).SetPosition(entity(_runtime).Position + new Vector3(posX, posY, posZ) * magnification(_runtime), stickGround);
                        }
                        else
                        {
                            entity(_runtime).SetPosition(entity(_runtime).Position + entity(_runtime).Forward * posZ * magnification(_runtime) + Vector3.Cross(entity(_runtime).Forward, Vector3.up) * posX * magnification(_runtime) + Vector3.up * posY * magnification(_runtime), stickGround);
                        }
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
