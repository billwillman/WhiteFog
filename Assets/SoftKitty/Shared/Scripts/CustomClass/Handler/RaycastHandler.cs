using UnityEngine;
namespace SoftKitty{
    public class RaycastHandler : Handler{
        public Entity entityA(GraphRuntimeState _runtime)
        { //The Entity which will be the start point of the ray check.
            return GetEntity(inputPort[0].connectedNodes[0],_runtime);
        }
        public float offsetAX; //X axis local position offset of the ray start point.
        public float offsetAY; //Y axis local position offset of the ray start point.
        public float offsetAZ; //Z axis local position offset of the ray start point.
        public Entity entityB(GraphRuntimeState _runtime)
        { //The Entity which will be the end point of the ray check.
            return GetEntity(inputPort[1].connectedNodes[0],_runtime);
        }
        public float offsetBX; //X axis local position offset of the ray end point.
        public float offsetBY; //Y axis local position offset of the ray end point.
        public float offsetBZ; //Z axis local position offset of the ray end point.
        public enum layerMasks{
          Ground,
          Specified
        }
        public layerMasks layerMask; //The layerMask for raycast.
        public int layer; //The specified layer id for raycast.
        public bool invert; //When invert is checked, this node will ouput 'true' when nothing collide by raycast.
 
        public RaycastHandler(){}
 
        public RaycastHandler(Handler _baseClass) : base(_baseClass){}
 
        public RaycastHandler InitHandler(Handler _baseClass){
            RaycastHandler  _newObj =new RaycastHandler(_baseClass);
            _newObj.offsetAX = _baseClass.FloatValues[0];
            _newObj.offsetAY = _baseClass.FloatValues[1];
            _newObj.offsetAZ = _baseClass.FloatValues[2];
            _newObj.offsetBX = _baseClass.FloatValues[3];
            _newObj.offsetBY = _baseClass.FloatValues[4];
            _newObj.offsetBZ = _baseClass.FloatValues[5];
            _newObj.layerMask = (layerMasks)_baseClass.IntValues[0];
            _newObj.layer = _baseClass.IntValues[1];
            _newObj.invert = _baseClass.BoolValues[0];
            return _newObj;
        }

        bool _result;
        public override void Update(GraphRuntimeState _runtime){ //Update state of the handler
            if (entityA(_runtime) == null)
            {
                if (SGD_Settings.Instance.DebugLevel >= 1)
                {
                    Debug.LogError("<RaycastHandler> [Node:" + Uid + "] failed to Update(), because the entityA is null");
                }
                return;
            }
            if (entityB(_runtime) == null)
            {
                if (SGD_Settings.Instance.DebugLevel >= 1)
                {
                    Debug.LogError("<RaycastHandler> [Node:" + Uid + "] failed to Update(), because the entityB is null");
                }
                return;
            }
            _result = Physics.Linecast(entityA(_runtime).Position+ offsetAX* Vector3.Cross(entityA(_runtime).Forward, Vector3.up) + offsetAY*Vector3.up+offsetAZ* entityA(_runtime).Forward,
                entityB(_runtime).Position + offsetBX * Vector3.Cross(entityB(_runtime).Forward, Vector3.up) + offsetBY * Vector3.up + offsetBZ * entityB(_runtime).Forward, 
                layerMask == layerMasks.Ground ? SGD_Settings.Instance.GroundLayer : layer, 
                QueryTriggerInteraction.Ignore);
            
            SetActive(_runtime, invert ?(!_result):(_result));
            if (SGD_Settings.Instance.DebugLevel >= 3) Debug.Log("<RaycastHandler> [Node:" + Uid + "] -> Condition Update() > " + (invert ? (!_result) : (_result)));
        }
 
    }
}
