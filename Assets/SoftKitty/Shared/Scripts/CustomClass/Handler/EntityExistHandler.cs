using UnityEngine;
namespace SoftKitty{
    public class EntityExistHandler : Handler{
        public Entity entity(GraphRuntimeState _runtime)
        { //The entity to check existence.
            return GetEntity(inputPort[0].connectedNodes[0],_runtime);
        }
        public enum logics{
          Existent,
          Nonexistent,
          Interactable
        }
        public logics logic; //The logic for verification.
 
        public EntityExistHandler(){}
 
        public EntityExistHandler(Handler _baseClass) : base(_baseClass){}
 
        public EntityExistHandler InitHandler(Handler _baseClass){
            EntityExistHandler  _newObj =new EntityExistHandler(_baseClass);
            _newObj.logic = (logics)_baseClass.IntValues[0];
            return _newObj;
        }

        bool _result;
        public override void Update(GraphRuntimeState _runtime){ //Update state of the handler

            _result = false;
            switch (logic)
            {
                case logics.Existent:
                    _result=( entity(_runtime) != null); 
                    break;
                case logics.Interactable:
                    var candidate = entity(_runtime);
                    _result = candidate != null && candidate.AvailableForInteraction;
                    break;
                case logics.Nonexistent:
                    _result = (entity(_runtime) == null); 
                    break;
            }
            SetActive(_runtime, _result);
            if (SGD_Settings.Instance.DebugLevel >= 3) Debug.Log("<EntityExistHandler> [Node:" + Uid + "] -> Update( Exist:" + (entity(_runtime) != null)+"/"+ logic+") > "+ _result);
        }
 
    }
}
