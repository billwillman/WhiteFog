using UnityEngine;
namespace SoftKitty{
    public class AngleDifferenceHandler : Handler{
        public Entity entityA(GraphRuntimeState _runtime)
        { //EntityA for comparison.
            return GetEntity(inputPort[0].connectedNodes[0], _runtime);
        }
        public Entity entityB(GraphRuntimeState _runtime)
        { //EntityB for comparison.
            return GetEntity(inputPort[1].connectedNodes[0], _runtime);
        }
        public enum angleTypes{
          fwdA_bwdB,
          fwdA_fwdB,
          fwdA_Delta,
          bwdA_Delta
        }
        public angleTypes angleType; //Type of the angle for comparison.
        public enum logics{
          Greater,
          Equal,
          Less
        }
        public logics logic; //Comparison logic.
        public float angle(GraphRuntimeState _runtime)
        { //Angle for comparison.
            return GetVariableValue(inputPort[2].connectedNodes[0], _runtime);
        }
        public bool ignoreY; //Ignore Y axis difference.
 
        public AngleDifferenceHandler(){}
 
        public AngleDifferenceHandler(Handler _baseClass) : base(_baseClass){}
 
        public AngleDifferenceHandler InitHandler(Handler _baseClass){
            AngleDifferenceHandler  _newObj =new AngleDifferenceHandler(_baseClass);
            _newObj.angleType = (angleTypes)_baseClass.IntValues[0];
            _newObj.logic = (logics)_baseClass.IntValues[1];
            _newObj.ignoreY = _baseClass.BoolValues[0];
            return _newObj;
        }


        float _angleResult;
        Vector3 _angleA;
        Vector3 _angleB;
        bool _result;
        public override void Update(GraphRuntimeState _runtime){ //Update state of the handler
            if (entityA(_runtime) == null)
            {
                if (SGD_Settings.Instance.DebugLevel >= 1)
                {
                    Debug.LogError("<AngleDifferenceHandler> [Node:" + Uid + "] failed to Update(), because the entityA is null");
                }
                return;
            }
            if (entityB(_runtime) == null)
            {
                if (SGD_Settings.Instance.DebugLevel >= 1)
                {
                    Debug.LogError("<AngleDifferenceHandler> [Node:" + Uid + "] failed to Update(), because the entityB is null");
                }
                return;
            }
           
            switch (angleType)
            {
                case angleTypes.fwdA_bwdB:
                    _angleA = entityA(_runtime).Forward;
                    _angleB = -entityB(_runtime).Forward;
                    break;
                case angleTypes.fwdA_fwdB:
                    _angleA = entityA(_runtime).Forward;
                    _angleB = entityB(_runtime).Forward;
                    break;
                case angleTypes.bwdA_Delta:
                    _angleA = -entityA(_runtime).Forward;
                    _angleB = entityB(_runtime).Position- entityA(_runtime).Position;
                    break;
                default:
                    _angleA = entityA(_runtime).Forward;
                    _angleB = entityB(_runtime).Position - entityA(_runtime).Position;
                    break;
            }
            if (ignoreY) _angleB.y = _angleA.y;
            _angleResult = Vector3.Angle(_angleA, _angleB);
            _result = false;
            switch (logic)
            {
                case logics.Greater:
                    _result = ( _angleResult > angle(_runtime));
                    break;
                case logics.Equal:
                    _result = (Mathf.Abs(_angleResult - angle(_runtime)) <1F);
                    break;
                case logics.Less:
                    _result = (_angleResult < angle(_runtime));
                    break;
            }
            SetActive(_runtime, _result);
            if (SGD_Settings.Instance.DebugLevel >= 3) Debug.Log("<AngleDifferenceHandler> [Node:" + Uid + "] -> Condition Update(" + _angleResult + " / " + angle(_runtime)+") > "+_result);
        }
 
    }
}
