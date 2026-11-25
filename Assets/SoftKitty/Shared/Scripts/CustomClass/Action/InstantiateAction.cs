using UnityEngine;
namespace SoftKitty{
    public class InstantiateAction : Action{
        public string path; //The path of the prefab to instantiate from Resources folder.
        public bool asChild; //Whether load the prefab as the child transform
        public Entity entity(GraphRuntimeState _runtime) { //The entity transform to set as parent.
            return GetEntity(inputPort[1].connectedNodes[0],_runtime);
        }
        public bool setPos; //Whether override the position of the prefab.
        public float posX; //Position X
        public float posY; //Position Y
        public float posZ; //Position Z
        public bool setAngle; //Whether override the euler angles of the prefab.
        public float angleX; //Euler Angle X
        public float angleY; //Euler Angle Y
        public float angleZ; //Euler Angle Z
        public bool setScale; //Whether override the local scale of the prefab.
        public float scale_multiplier(GraphRuntimeState _runtime)
        { //Local Scale Multiplier
            return GetVariableValue(inputPort[2].connectedNodes[0], _runtime);
        }
        public bool set_active; //After instantiate gameObject, whether set it to active.
        public bool send_msg; //After instantiate gameObject, whether SendMessage to it.
        public string msg; //The message send to the instantiated gameObject.
 
        public InstantiateAction(){}
 
        public InstantiateAction(Action _baseClass) : base(_baseClass){}
 
        public InstantiateAction InitAction(Action _baseClass){
            InstantiateAction  _newObj =new InstantiateAction(_baseClass);
            _newObj.path = _baseClass.StringValues[0];
            _newObj.asChild = _baseClass.BoolValues[0];
            _newObj.setPos = _baseClass.BoolValues[1];
            _newObj.posX = _baseClass.FloatValues[0];
            _newObj.posY = _baseClass.FloatValues[1];
            _newObj.posZ = _baseClass.FloatValues[2];
            _newObj.setAngle = _baseClass.BoolValues[2];
            _newObj.angleX = _baseClass.FloatValues[3];
            _newObj.angleY = _baseClass.FloatValues[4];
            _newObj.angleZ = _baseClass.FloatValues[5];
            _newObj.setScale = _baseClass.BoolValues[3];
            _newObj.set_active = _baseClass.BoolValues[4];
            _newObj.send_msg = _baseClass.BoolValues[5];
            _newObj.msg = _baseClass.StringValues[1];
            return _newObj;
        }
 
        public override void Execute(GraphRuntimeState _runtime){ //Execute this action once
            GameObject _source = Resources.Load<GameObject>(path);
            if (_source != null)
            {
                GameObject _newObj = GameManager.InstantiateGameObject(_source,
                        asChild ? GameManager.GetEntityInstance(entity(_runtime).uid).transform : null
                        );

                if (asChild )
                {
                    if (GameManager.GetEntityInstance(entity(_runtime).uid) != null)
                    {
                        if (setPos) _newObj.transform.localPosition = new Vector3(posX, posY, posZ);
                    }
                    else
                    {
                        if (setPos) _newObj.transform.position = entity(_runtime).Position+ entity(_runtime).Forward* posZ + Vector3.Cross(entity(_runtime).Forward,Vector3.up)* posX+Vector3.up* posY;
                    }
                    if (SGD_Settings.Instance.DebugLevel >= 3)
                        Debug.Log("<InstantiateAction> [Node:" + Uid + "] Executed() > New GameObject:" + _newObj.name + "  as child of:" + entity(_runtime).uid);
                }
                else
                {
                    if (setPos)
                        _newObj.transform.position = new Vector3(posX, posY, posZ);
                    else
                        _newObj.transform.position = _source.transform.position;

                    if (SGD_Settings.Instance.DebugLevel >= 3)
                        Debug.Log("<InstantiateAction> [Node:" + Uid + "] Executed() > New GameObject:" + _newObj.name + "  position:" + _newObj.transform.position.ToString());
                }
                if (setAngle) _newObj.transform.localEulerAngles = new Vector3(angleX, angleY, angleZ);
                _newObj.transform.localScale = _source.transform.localScale * (setScale ? scale_multiplier(_runtime) : 1F);
                if (set_active) _newObj.SetActive(true);
                if (send_msg) _newObj.SendMessage(msg, SendMessageOptions.DontRequireReceiver);
               
            }
            else if (SGD_Settings.Instance.DebugLevel >= 1)
            {
                Debug.LogError("<InstantiateAction> [Node:" + Uid + "] failed to Execute(), because the path does not exist:" + path);
            }
        }
 
    }
}
