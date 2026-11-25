using UnityEngine;
namespace SoftKitty{
    public class OverTimeEffectHandler : Handler{
        public Entity entity(GraphRuntimeState _runtime)
        { //Entity to check OverTimeEffect state,
            return GetEntity(inputPort[0].connectedNodes[0],_runtime);
        }
        public string uid; //uid of the OverTimeEffect,
        public enum logics{
          Exist,
          Nonexist,
          Layer_Greater_Than,
          Layer_Less_Than
        }
        public logics logic; //Comparation logic.
        public int layerGreater; //The layer count to compare with.
        public int layerLess; //The layer count to compare with.
 
        public OverTimeEffectHandler(){}
 
        public OverTimeEffectHandler(Handler _baseClass) : base(_baseClass){}
 
        public OverTimeEffectHandler InitHandler(Handler _baseClass){
            OverTimeEffectHandler  _newObj =new OverTimeEffectHandler(_baseClass);
            _newObj.uid = _baseClass.StringValues[0];
            _newObj.logic = (logics)_baseClass.IntValues[0];
            _newObj.layerGreater = _baseClass.IntValues[1];
            _newObj.layerLess = _baseClass.IntValues[2];
            return _newObj;
        }

        bool _result = false;
        public override void Update(GraphRuntimeState _runtime){ //Update state of the handler
            if (entity(_runtime) == null)
            {
                if (SGD_Settings.Instance.DebugLevel >= 1) Debug.LogError("<OverTimeEffectHandler> [Node:" + Uid + "] failed to Update(), because the entity is null.");
                return;
            }
            if (GameManager.OverTimeEffectData == null)
            {
                if (SGD_Settings.Instance.DebugLevel >= 1) Debug.LogError("<OverTimeEffectHandler> [Node:" + Uid + "] failed to Update(), because OverTimeEffectObject is not assigned in 'SoftKitty General Data Settings");
                return;
            }
            if (GameManager.GetOverTimeEffect(uid) == null)
            {
                if (SGD_Settings.Instance.DebugLevel >= 1) Debug.LogError("<OverTimeEffectHandler> [Node:" + Uid + "] failed to Update(), because OverTimeEffectObject<" + uid + "> can't be found in OverTimeEffectObject.");
                return;
            }

            _result = false;
            switch (logic) {
                case logics.Exist:
                    _result = (entity(_runtime).GetOverTimeEffectLayer(uid) > 0);
                    break;
                case logics.Nonexist:
                    _result = (entity(_runtime).GetOverTimeEffectLayer(uid) <= 0);
                    break;
                case logics.Layer_Greater_Than:
                    _result = (entity(_runtime).GetOverTimeEffectLayer(uid) > layerGreater);
                    break;
                case logics.Layer_Less_Than:
                    _result = (entity(_runtime).GetOverTimeEffectLayer(uid) < layerLess);
                    break;
            }
            SetActive(_runtime, _result);
            if (SGD_Settings.Instance.DebugLevel >= 3) Debug.Log("<OverTimeEffectHandler> [Node:" + Uid + "] -> Condition Update( (Entity)" + entity(_runtime).uid + ".OverTimeEffect(" + uid + ") layer:" + entity(_runtime).GetOverTimeEffectLayer(uid) + " is " + logic.ToString() + " than " + (logic == logics.Layer_Greater_Than ? layerGreater : "") + (logic == logics.Layer_Less_Than ? layerLess : "")+" ) > "+_result);
        }
 
    }
}
