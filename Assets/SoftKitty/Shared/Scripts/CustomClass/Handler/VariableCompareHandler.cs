using UnityEngine;
namespace SoftKitty{
    public class VariableCompareHandler : Handler{
        public float var1(GraphRuntimeState _runtime)
        { //Variable for comparison.
             return GetVariableValue(inputPort[0].connectedNodes[0],_runtime);
        }
        public float var2(GraphRuntimeState _runtime)
        { //Variable for comparison.
             return GetVariableValue(inputPort[1].connectedNodes[0], _runtime);
        }
        public enum logics{
          Greater,
          Greater_Or_Equal,
          Equal,
          Less_Or_Equal,
          Less,
          Different
        }
        public logics logic; //Comparison logic for A xxx B.
 
        public VariableCompareHandler(){}
 
        public VariableCompareHandler(Handler _baseClass) : base(_baseClass){}
 
        public VariableCompareHandler InitHandler(Handler _baseClass){
            VariableCompareHandler  _newObj =new VariableCompareHandler(_baseClass);
            _newObj.logic = (logics)_baseClass.IntValues[0];
            return _newObj;
        }

        bool _result;
        public override void Update(GraphRuntimeState _runtime){ //Update state of the handler
            _result = false;
            switch (logic)
            {
                case logics.Greater:
                    _result= var1(_runtime) >var2(_runtime);
                    break;
                case logics.Greater_Or_Equal:
                    _result = var1(_runtime) >= var2(_runtime);
                    break;
                case logics.Equal:
                    _result = var1(_runtime) == var2(_runtime);
                    break;
                case logics.Less_Or_Equal:
                    _result = var1(_runtime) <= var2(_runtime);
                    break;
                case logics.Less:
                    _result = var1(_runtime) < var2(_runtime);
                    break;
                case logics.Different:
                    _result = var1(_runtime) != var2(_runtime);
                    break;
            }
            SetActive(_runtime,_result);
            if (SGD_Settings.Instance.DebugLevel >= 3) Debug.Log("<VariableCompareHandler> [Node:" + Uid + "] -> Condition Update(" + var1(_runtime) +" is "+ logic + " than "+var2(_runtime)+") > "+_result.ToString() );
        }
 
    }
}
