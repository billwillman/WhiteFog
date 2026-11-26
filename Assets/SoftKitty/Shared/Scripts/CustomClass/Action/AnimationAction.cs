using UnityEngine;
namespace SoftKitty{
    public class AnimationAction : Action{
        public Entity entity(GraphRuntimeState _runtime) { //The entity to play this animation.
            return GetEntity(inputPort[1].connectedNodes[0],_runtime);
        }
        public enum AnimTypes{
          Legacy,
          Mecanim,
          SkillMotionBinding
        }
        public AnimTypes AnimType; //Whether using Legacy Animation component or Mecanim component.
        public string clipName; //The name of the animation clip(Legacy) .
        public string stateName; //The name of the animation state.
 
        public AnimationAction(){}
 
        public AnimationAction(Action _baseClass) : base(_baseClass){}
 
        public AnimationAction InitAction(Action _baseClass){
            AnimationAction  _newObj =new AnimationAction(_baseClass);
            _newObj.AnimType = (AnimTypes)_baseClass.IntValues[0];
            _newObj.clipName = _baseClass.StringValues[0];
            _newObj.stateName = _baseClass.StringValues[1];
            return _newObj;
        }
 
        public override void Execute(GraphRuntimeState _runtime){ //Execute this action once
            if (AnimType == AnimTypes.SkillMotionBinding)
            {
                var actor = SkillPlayback.FindActor(entity(_runtime));
                if (actor)
                {
                    var graph = _runtime.RootState.RootInstance.GetComponent<GraphInstance>().mGraph;
                    var binding = motion ?? SkillMotion.For(this, graph as OriginalSkillGraph);
                    if (!binding.Play(actor.SkillAnimator, _runtime, out var error) && error != null)
                        Debug.LogWarning(graph.name + " · " + Uid + " · " + error, actor);
                }
                return;
            }
            if (entity(_runtime) != null)
            {
                if (GameManager.GetEntityInstance(entity(_runtime).uid) != null)
                {
                    if (AnimType == AnimTypes.Legacy)
                    {
                        if (GameManager.GetEntityInstance(entity(_runtime).uid).GetComponent<Animation>())
                        {
                            GameManager.GetEntityInstance(entity(_runtime).uid).GetComponent<Animation>().Play(clipName);
                            if (SGD_Settings.Instance.DebugLevel >= 3) 
                                Debug.Log("<AnimationAction> [Node:" + Uid + "] Executed() > Entity:"+ entity(_runtime).uid+"  Play Clip:"+ clipName);
                        }
                        else if (SGD_Settings.Instance.DebugLevel >= 1)
                            Debug.LogError("<AnimationAction> [Node:"+Uid+"] failed to Execute(), because Animation component could not be found on the entity");
                    }
                    else if (AnimType == AnimTypes.Mecanim)
                    {
                        if (GameManager.GetEntityInstance(entity(_runtime).uid).GetComponent<Animator>())
                        {
                            GameManager.GetEntityInstance(entity(_runtime).uid).GetComponent<Animator>().Play(stateName);
                            if (SGD_Settings.Instance.DebugLevel >= 3) Debug.Log("<AnimationAction> [Node:" + Uid + "] Executed() > Entity:" + entity(_runtime).uid + "  Play State:" + stateName);
                        }
                        else if (SGD_Settings.Instance.DebugLevel >= 1)
                            Debug.LogError("<AnimationAction> [Node:" + Uid + "] failed to Execute(), because Animator component could not be found on the entity");
                    }
                }else if (SGD_Settings.Instance.DebugLevel >= 1)
                {
                    Debug.LogError("<AnimationAction> [Node:" + Uid + "] failed to Execute(), because the entity instance does not exists in the scene.");
                }
            }
            else if (SGD_Settings.Instance.DebugLevel >= 1)
            {
                Debug.LogError("<AnimationAction> [Node:" + Uid + "] failed to Execute(), because the entity is null");
            }
        }
 
    }
}
