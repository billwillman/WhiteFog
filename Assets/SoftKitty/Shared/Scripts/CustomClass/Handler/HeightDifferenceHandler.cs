using UnityEngine;
namespace SoftKitty{
    public class HeightDifferenceHandler : Handler{
        public Entity entity(GraphRuntimeState _runtime)
        { //Entity for comparison.
            return GetEntity(inputPort[0].connectedNodes[0],_runtime);
        }
        public enum targetTypes{
          Variable,
          Entity,
          Fixed_Value
        }
        public targetTypes targetType; //Type of the target.
        public float targetVariable(GraphRuntimeState _runtime)
        { //The target Variable.
            return GetVariableValue(inputPort[1].connectedNodes[0], _runtime);
        }
        public Entity targetEntity(GraphRuntimeState _runtime)
        { //The target Entity.
            return GetEntity(inputPort[2].connectedNodes[0], _runtime);
        }
        public float targetValue; //The target value.
        public enum logics{
          Within_Range,
          Greater_Than,
          Less_Than
        }
        public logics logic; //The logic of the comparison.
        public float rangeX; //The minimal height of the range.
        public float rangeY; //the maxmium height of the range.
 
        public HeightDifferenceHandler(){}
 
        public HeightDifferenceHandler(Handler _baseClass) : base(_baseClass){}
 
        public HeightDifferenceHandler InitHandler(Handler _baseClass){
            HeightDifferenceHandler  _newObj =new HeightDifferenceHandler(_baseClass);
            _newObj.targetType = (targetTypes)_baseClass.IntValues[0];
            _newObj.targetValue = _baseClass.FloatValues[0];
            _newObj.logic = (logics)_baseClass.IntValues[1];
            _newObj.rangeX = _baseClass.FloatValues[1];
            _newObj.rangeY = _baseClass.FloatValues[2];
            return _newObj;
        }

        bool _result = false;
        float _targetHeight;
        public override void Update(GraphRuntimeState _runtime){ //Update state of the handler
            if (entity(_runtime) == null)
            {
                if (SGD_Settings.Instance.DebugLevel >= 1)
                {
                    Debug.LogError("<HeightDifferenceHandler> [Node:" + Uid + "] failed to Update(), because the entity is null");
                }
                return;
            }
           
            switch (targetType)
            {
                case targetTypes.Variable:
                    _targetHeight = targetVariable(_runtime);
                    break;
                case targetTypes.Entity:
                    if (targetEntity(_runtime) == null)
                    {
                        if (SGD_Settings.Instance.DebugLevel >= 1)
                        {
                            Debug.LogError("<HeightDifferenceHandler> [Node:" + Uid + "] failed to Update(), because the targetEntity is null");
                        }
                        return;
                    }
                    _targetHeight = targetEntity(_runtime).Position.y;
                    break;
                default:
                    _targetHeight = targetValue;
                    break;
            }

            _result = false;
            switch (logic)
            {
                case logics.Within_Range:
                    _result = ( entity(_runtime).Position.y- _targetHeight>rangeX && entity(_runtime).Position.y - _targetHeight < rangeY);
                    break;
                case logics.Greater_Than:
                    _result = ( entity(_runtime).Position.y>_targetHeight);
                    break;
                case logics.Less_Than:
                    _result = ( entity(_runtime).Position.y < _targetHeight);
                    break;
            }
            SetActive(_runtime, _result);
            if (SGD_Settings.Instance.DebugLevel >= 3) Debug.Log("<HeightDifferenceHandler> [Node:" + Uid + "] -> Condition Update(" + entity(_runtime).Position.y + " is "+logic+" than " + _targetHeight+") > "+_result);
        }
 
    }
}
