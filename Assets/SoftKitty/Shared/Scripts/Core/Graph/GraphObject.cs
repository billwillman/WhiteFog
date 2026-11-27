using UnityEngine;
using System.Collections.Generic;

namespace SoftKitty
{
    [CreateAssetMenu(fileName = "GraphObject", menuName = "Soft Kitty/技能图")]
    public class GraphObject : GraphBase
    {
        public Root RootSet;

        // Keep the public MCC cache bounded even if callers continually create transient graph assets.
        private const int MaxCachedGraphs = 128;
        [System.NonSerialized] private Root compiledFrom;
        [System.NonSerialized] private int lastUsedFrame;
#if UNITY_EDITOR
        [System.NonSerialized] private int compiledDirtyCount;
#endif

        internal bool IsCompiledGraphCurrent()
        {
            lastUsedFrame = Time.frameCount;
#if UNITY_EDITOR
            if (compiledDirtyCount != UnityEditor.EditorUtility.GetDirtyCount(this)) return false;
#endif
            return ReferenceEquals(compiledFrom, RootSet);
        }

        internal void CacheCompiledGraph(Root compiled)
        {
            var cache = GraphInstance.runtimeInitializedGraph;
            if (cache.Count >= MaxCachedGraphs && !cache.ContainsKey(this))
            {
                GraphObject oldest = null;
                foreach (var graph in cache.Keys)
                    if (ReferenceEquals(oldest, null) || graph.lastUsedFrame < oldest.lastUsedFrame) oldest = graph;
                cache.Remove(oldest);
            }
            cache[this] = compiled;
            compiledFrom = RootSet;
            lastUsedFrame = Time.frameCount;
#if UNITY_EDITOR
            compiledDirtyCount = UnityEditor.EditorUtility.GetDirtyCount(this);
#endif
        }

        public void InvalidateCompiledGraph() => compiledFrom = null;

        protected virtual void OnValidate() => InvalidateCompiledGraph();

        protected virtual void OnDisable() => GraphInstance.runtimeInitializedGraph.Remove(this);

        [Tooltip("Stable skill ID for combat rolls. Set this when assets share a name or must survive renaming.")]
        public string combatRandomId;
        public string CombatRandomId => string.IsNullOrEmpty(combatRandomId) ? name : combatRandomId;
        public SkillLibraryCharacter libraryCharacter;
        [HideInInspector] public bool previewAsOpponent;
        [Min(1)] public int castTargetLimit = 1;
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
