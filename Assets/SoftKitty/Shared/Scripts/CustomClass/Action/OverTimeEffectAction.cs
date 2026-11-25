using UnityEngine;
namespace SoftKitty{
    public class OverTimeEffectAction:Action{
        public Entity entity(GraphRuntimeState _runtime) { //The target entity.
            return GetEntity(inputPort[1].connectedNodes[0],_runtime);
        }
        public float id(GraphRuntimeState _runtime) { //The index of the over time effect.
            return GetVariableValue(inputPort[2].connectedNodes[0],_runtime);
        }
        public enum actions{
          Add_Effect,
          Dispell_Effect,
          Remove_Effect
        }
        public actions action; //The type of the action.
 
        public OverTimeEffectAction(){}
 
        public OverTimeEffectAction(Action _baseClass) : base(_baseClass){}
 
        public OverTimeEffectAction InitAction(Action _baseClass){
            OverTimeEffectAction  _newObj =new OverTimeEffectAction(_baseClass);
            _newObj.action = (actions)_baseClass.IntValues[0];
            return _newObj;
        }
 
        public override void Execute(GraphRuntimeState _runtime){ //Execute this action once
            if (id(_runtime) < 0) return;
            if (entity(_runtime) != null)
            {
                int _effectId = Mathf.FloorToInt(id(_runtime));
                switch (action)
                {
                    case actions.Add_Effect:
                        entity(_runtime).AddOverTimeEffect(_effectId, _runtime.RootState.GetInvolvedEntity("Dealer"));
                        if (SGD_Settings.Instance.DebugLevel >= 3)
                            Debug.Log("<OverTimeEffectAction> [Node:" + Uid + "] Executed() > (Entity)" + entity(_runtime).uid + ".AddOverTimeEffect("
                                + _effectId + ")");
                        break;
                    case actions.Dispell_Effect:
                        entity(_runtime).DispellOverTimeEffect(_effectId);
                        if (SGD_Settings.Instance.DebugLevel >= 3)
                            Debug.Log("<OverTimeEffectAction> [Node:" + Uid + "] Executed() > (Entity)" + entity(_runtime).uid + ".DispellOverTimeEffect("
                                + _effectId + ")");
                        break;
                    case actions.Remove_Effect:
                        entity(_runtime).RemoveOverTimeEffect(_effectId);
                        if (SGD_Settings.Instance.DebugLevel >= 3)
                            Debug.Log("<OverTimeEffectAction> [Node:" + Uid + "] Executed() > (Entity)" + entity(_runtime).uid + ".RemoveOverTimeEffect("
                                + _effectId + ")");
                        break;
                }
            }
            else if (SGD_Settings.Instance.DebugLevel >= 1)
            {
                Debug.LogError("<OverTimeEffectAction> [Node:" + Uid + "] failed to Execute(), because the entity is null");
            }
        }
 
    }
}
