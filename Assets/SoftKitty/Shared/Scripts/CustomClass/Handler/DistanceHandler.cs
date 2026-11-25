using UnityEngine;
namespace SoftKitty{
    public class DistanceHandler : Handler{
        public Entity entityA(GraphRuntimeState _runtime) { //One of the entity for distance comparation
            return GetEntity(inputPort[0].connectedNodes[0], _runtime);
        }
        public enum targetTypes{
          Entity,
          Fixed_Position
        }
        public targetTypes targetType; //The type of the target for comparison.
        public Entity entityB(GraphRuntimeState _runtime)
        { //One of the entity for distance comparation
            return GetEntity(inputPort[1].connectedNodes[0], _runtime);
        }
        public float posX; //X axis of the target position for comparison.
        public float posY; //Y axis of the target position for comparison.
        public float posZ; //Z axis of the target position for comparison.
        public float distance(GraphRuntimeState _runtime)
        { //Distance value for comparation.
            return GetVariableValue(inputPort[2].connectedNodes[0], _runtime);
        }
        public enum logics{
          Greater,
          Greater_Or_Equal,
          Equal,
          Less_Or_Equal,
          Less
        }
        public logics logic; //The logic type of the comparison.
        public bool ignoreY; //Ignore Y position difference.
 
        public DistanceHandler(){}
 
        public DistanceHandler(Handler _baseClass) : base(_baseClass){}
 
        public DistanceHandler InitHandler(Handler _baseClass){
            DistanceHandler  _newObj =new DistanceHandler(_baseClass);
            _newObj.targetType = (targetTypes)_baseClass.IntValues[0];
            _newObj.posX = _baseClass.FloatValues[0];
            _newObj.posY = _baseClass.FloatValues[1];
            _newObj.posZ = _baseClass.FloatValues[2];
            _newObj.logic = (logics)_baseClass.IntValues[1];
            _newObj.ignoreY = _baseClass.BoolValues[0];
            return _newObj;
        }

        float _distance;
        Vector3 _pointB;
        bool _result;
        public override void Update(GraphRuntimeState _runtime)
        { //Update state of the handler
            if (entityA(_runtime) == null)
            {
                if (SGD_Settings.Instance.DebugLevel >= 1) Debug.LogError("<DistanceHandler> [Node:" + Uid + "] failed to Update(), because the entityA is null.");
                return;
            }
            if (targetType == targetTypes.Entity)
            {
                if (entityB(_runtime) == null)
                {
                    if (SGD_Settings.Instance.DebugLevel >= 1) Debug.LogError("<DistanceHandler> [Node:" + Uid + "] failed to Update(), because the entityB is null.");
                    return;
                }
                _pointB = GetPos(entityB(_runtime));
            }
            else
            {
                _pointB = new Vector3(posX,posY,posZ);
                if (ignoreY) _pointB.y = 0F;
            }
            _distance = Vector3.Distance(GetPos(entityA(_runtime)), _pointB);
            _result = false;
            switch (logic)
            {
                case logics.Greater:
                    _result= (_distance > distance(_runtime));
                    break;
                case logics.Greater_Or_Equal:
                    _result = (_distance >= distance(_runtime));
                    break;
                case logics.Equal:
                    _result = (_distance == distance(_runtime));
                    break;
                case logics.Less_Or_Equal:
                    _result = (_distance <= distance(_runtime));
                    break;
                case logics.Less:
                    _result = (_distance < distance(_runtime));
                    break;
            }
            SetActive(_runtime, _result);
            if (SGD_Settings.Instance.DebugLevel >= 3) Debug.Log("<DistanceHandler> [Node:" + Uid + "] -> Condition Update("+_distance + " is "+logic+" than " + distance(_runtime)+") > "+_result);
        }

        private Vector3 GetPos(Entity _entity)
        {
            return new Vector3(_entity.Position.x, ignoreY ? 0F : _entity.Position.y, _entity.Position.z);
        }

    }
}
