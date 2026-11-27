using UnityEngine;
using System.Collections.Generic;

namespace SoftKitty
{
    [CreateAssetMenu(fileName = "GraphObject", menuName = "Soft Kitty/技能图")]
    public class GraphObject : GraphBase
    {
        public Root RootSet;
        [Tooltip("Stable skill ID for combat rolls. Set this when assets share a name or must survive renaming.")]
        public string combatRandomId;
        public string CombatRandomId => string.IsNullOrEmpty(combatRandomId) ? name : combatRandomId;
        public SkillLibraryCharacter libraryCharacter;
        [HideInInspector] public bool previewAsOpponent;
        [Min(1)] public int castTargetLimit = 1;
        [HideInInspector] public bool useInputTiming;
        [Min(0)] public float moveAfterSeconds;
        [Min(0)] public float nextSkillAfterSeconds;
        public bool autoSave = false;
        [HideInInspector]
        public List<Sprite> NodeIconSprites = new List<Sprite>();//0-Root 1-Node 2-Logic 3-Math 4-Variable 5-Entity
        [HideInInspector]
        public List<string> ActionClassNames = new List<string>();
        [HideInInspector]
        public List<string> HandlerClassNames = new List<string>();
        [HideInInspector]
        public List<string> RootClassNames = new List<string>();
        [HideInInspector]
        public bool pendingSave = false;
        [HideInInspector]
        public string summaryNoteId = "";

        [HideInInspector]
        public string GetSummary
        {
            get
            {
                if (RootSet == null || string.IsNullOrEmpty(summaryNoteId)) return "";
                foreach (var obj in RootSet.Notes) {
                    if (obj.Uid == summaryNoteId) return obj.Text;
                }
                return "";
            }
        }

    }
}
