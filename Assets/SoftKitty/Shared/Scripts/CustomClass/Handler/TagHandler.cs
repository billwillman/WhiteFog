using UnityEngine;
using System.Collections.Generic;
namespace SoftKitty{
    public class TagHandler : Handler{
        public Entity entity(GraphRuntimeState _runtime)
        { //The entity for tag comparison.
            return GetEntity(inputPort[0].connectedNodes[0],_runtime);
        }
        public string tag1; //Tag for comparison.
        public string tag2; //Tag for comparison.
        public string tag3; //Tag for comparison.
        public enum logics{
          All_Match,
          Any_Match,
          None_Match
        }
        public logics logic; //Comparison logic for tags.
 
        public TagHandler(){}
 
        public TagHandler(Handler _baseClass) : base(_baseClass){}
 
        public TagHandler InitHandler(Handler _baseClass){
            TagHandler  _newObj =new TagHandler(_baseClass);
            _newObj.tag1 = _baseClass.StringValues[0];
            _newObj.tag2 = _baseClass.StringValues[1];
            _newObj.tag3 = _baseClass.StringValues[2];
            _newObj.logic = (logics)_baseClass.IntValues[0];
            return _newObj;
        }

        bool _active;
        public override void Update(GraphRuntimeState _runtime){ //Update state of the handler
            if (entity(_runtime) == null)
            {
                if (SGD_Settings.Instance.DebugLevel >= 1) Debug.LogError("<TagHandler> [Node:" + Uid + "] failed to Update(), because the entity is null.");
                return;
            }

            _active = false;
            switch (logic)
            {
                case logics.Any_Match:
                    _active = entity(_runtime).hasMatchingTag(new List<string>() {tag1,tag2,tag3});
                    break;
                case logics.All_Match:
                    _active = (entity(_runtime).hasMatchingTag(tag1) || string.IsNullOrEmpty(tag1)) && (entity(_runtime).hasMatchingTag(tag2) || string.IsNullOrEmpty(tag2)) && (entity(_runtime).hasMatchingTag(tag3) || string.IsNullOrEmpty(tag3));
                    break;
                case logics.None_Match:
                    _active = !entity(_runtime).hasMatchingTag(new List<string>() { tag1, tag2, tag3 });
                    break;
            }
            SetActive(_runtime, _active);
            if (SGD_Settings.Instance.DebugLevel >= 3) Debug.Log("<TagHandler> [Node:" + Uid + "] -> Condition Update() > " + _active);
        }
 
    }
}
