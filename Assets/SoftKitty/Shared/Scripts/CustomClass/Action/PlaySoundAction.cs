using UnityEngine;
namespace SoftKitty{
    public class PlaySoundAction : Action{
        public string path; //The path of the audio clip to play from Resources folder.
        public bool is3d; //Whether play this audio clip as 3D sound.
        public Entity position(GraphRuntimeState _runtime) { //The position of entity where the 3d sound will be played.
            return GetEntity(inputPort[1].connectedNodes[0],_runtime);
        }
        public float volumn(GraphRuntimeState _runtime) { //The volumn of the sound, default as 1
            return GetVariableValue(inputPort[2].connectedNodes[0],_runtime, 1F);
        }
 
        public PlaySoundAction(){}
 
        public PlaySoundAction(Action _baseClass) : base(_baseClass){}
 
        public PlaySoundAction InitAction(Action _baseClass){
            PlaySoundAction  _newObj =new PlaySoundAction(_baseClass);
            _newObj.path = _baseClass.StringValues[0];
            _newObj.is3d = _baseClass.BoolValues[0];
            return _newObj;
        }
 
        public override void Execute(GraphRuntimeState _runtime){ //Execute this action once
            AudioClip _clip = Resources.Load<AudioClip>(path);
            if (_clip != null)
            {
                if (is3d)
                {
                    if (position(_runtime) != null)
                    {
                       GameManager.PlaySound3D(_clip, volumn(_runtime), position(_runtime).Position);
                        if (SGD_Settings.Instance.DebugLevel >= 3)
                            Debug.Log("<PlaySoundAction> [Node:" + Uid + "] Executed() > GameManager.PlaySound3D("+ 
                                _clip.name + "," + volumn(_runtime) + ", (Entity)" + position(_runtime).uid  + ")");
                    }
                    else if (SGD_Settings.Instance.DebugLevel >= 1)
                    {
                        Debug.LogError("<PlaySoundAction> [Node:" + Uid + "] failed to Execute(), because the position entity does not exist.");
                    }
                }
                else
                {
                    GameManager.PlaySound2D(_clip, volumn(_runtime));
                    if (SGD_Settings.Instance.DebugLevel >= 3)
                        Debug.Log("<PlaySoundAction> [Node:" + Uid + "] Executed() > GameManager.PlaySound2D(" +
                            _clip.name + "," + volumn(_runtime) + ")");
                }
            }
            else if (SGD_Settings.Instance.DebugLevel >= 1)
            {
                Debug.LogError("<PlaySoundAction> [Node:" + Uid + "] failed to Execute(), because the path does not exist:" + path);
            }
        }
 
    }
}
