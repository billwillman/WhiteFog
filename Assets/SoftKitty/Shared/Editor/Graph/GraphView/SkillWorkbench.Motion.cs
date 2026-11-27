using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SoftKitty
{
    public partial class SkillWorkbench
    {
        private RuntimeAnimatorController motionBrowserController;
        private static readonly string[] MotionSources = { "Animator State", "直接播放 Animation Clip" };

        private static EntityType? MotionEntity(GraphObject skill, Action action)
        {
            var uid = action.inputPort.ElementAtOrDefault(1)?.uid.FirstOrDefault()?.Split('#')[0];
            return skill.RootSet.Entities.Find(e => e.Uid == uid)?.Type;
        }

        private RuntimeAnimatorController MotionController(GraphObject skill, Action action)
        {
            if (motionBrowserController) return motionBrowserController;
            var entity = MotionEntity(skill, action);
            GameObject actor = null;
            if (previewMode == 1 || (Application.isPlaying && InPreviewStage && lastCaster))
            {
                var from = previewMode == 1 ? dealer : lastCaster;
                var to = previewMode == 1 ? target : lastReceiver;
                if (entity == EntityType.Dealer && from) actor = from.gameObject;
                if (entity == EntityType.Target && to) actor = to.gameObject;
            }
            return actor ? actor.GetComponentInChildren<Animator>()?.runtimeAnimatorController : DefaultMotionController(skill, action);
        }

        internal static RuntimeAnimatorController DefaultMotionController(GraphObject skill, Action action)
        {
            var entity = MotionEntity(skill, action);
            GameObject actor = null;
            if (entity == EntityType.Player || entity == EntityType.Dealer || entity == EntityType.Target)
            {
                bool opponent = entity == EntityType.Target ? !skill.previewAsOpponent : entity == EntityType.Dealer && skill.previewAsOpponent;
                actor = opponent ? (skill as OriginalSkillGraph)?.previewEnemy : AssetDatabase.LoadAssetAtPath<GameObject>("Assets/WhiteFog/OriginalSkills/男角色001.prefab");
            }
            return actor ? actor.GetComponentInChildren<Animator>()?.runtimeAnimatorController : null;
        }

        // State names may repeat in different layers or sub-state machines.
        internal static IEnumerable<(string path, AnimatorState state)> MotionStates(AnimatorStateMachine machine, string prefix)
        {
            foreach (var child in machine.states) yield return (prefix + child.state.name, child.state);
            foreach (var child in machine.stateMachines)
                foreach (var entry in MotionStates(child.stateMachine, prefix + child.stateMachine.name + ".")) yield return entry;
        }

        private static void LocateMotion(Object asset)
        {
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private void DrawMotion(Action action, GraphObject skill)
        {
            var binding = SkillMotion.For(action, skill as OriginalSkillGraph);
            int mode = SkillFieldHelp.Popup("动作来源", (int)binding.source, MotionSources, "选择通过角色 Animator Controller 中的状态播放动作，或直接播放指定的动画片段。");
            if (mode != (int)binding.source)
                Change(() => { binding.source = (SkillMotion.Source)mode; action.motion = binding; });

            if (binding.source == SkillMotion.Source.AnimationClip)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var clip = (AnimationClip)SkillFieldHelp.ObjectField("Animation Clip", binding.clip, typeof(AnimationClip), false, "直接播放的动画片段。请选择可供 Animator 使用的非 Legacy 片段。");
                    if (clip != binding.clip) Change(() => { binding.clip = clip; action.motion = binding; });
                    using (new EditorGUI.DisabledScope(!clip))
                        if (GUILayout.Button("定位动画", GUILayout.Width(80))) LocateMotion(clip);
                }
                DrawMotionResult(MotionTimeline(action));
                if (binding.clip && binding.clip.legacy) EditorGUILayout.HelpBox("请选择供 Animator 播放的非 Legacy 动画。", MessageType.Warning);
                return;
            }

            var runtime = MotionController(skill, action);
            var picked = (RuntimeAnimatorController)SkillFieldHelp.ObjectField(
                "参考 Controller", runtime, typeof(RuntimeAnimatorController), false, "用于列出状态。播放时使用所连角色自己的 Animator。");
            if (picked != runtime) motionBrowserController = picked;
            runtime = MotionController(skill, action);
            var controller = runtime as AnimatorController;
            var overrides = runtime as AnimatorOverrideController;
            if (overrides) controller = overrides.runtimeAnimatorController as AnimatorController;
            if (!controller || controller.layers.Length == 0)
            {
                EditorGUILayout.LabelField("Animator State", binding.StatePath);
                EditorGUILayout.HelpBox("选择角色的 Animator Controller 后可列出 Layer 和 State。", MessageType.Info);
                return;
            }

            var layers = controller.layers;
            var layerNames = layers.Select(l => l.name).ToArray();
            int layer = System.Array.IndexOf(layerNames, binding.layer);
            int nextLayer = SkillFieldHelp.Popup("Layer", layer, layerNames, "在参考 Animator Controller 中选择动画层。状态列表会显示这一层的状态。");
            if (nextLayer != layer && nextLayer >= 0)
            {
                Change(() => { binding.layer = layerNames[nextLayer]; binding.state = ""; action.motion = binding; });
                layer = nextLayer;
            }
            if (layer < 0)
            {
                EditorGUILayout.HelpBox("参考控制器缺少图中配置的层 " + binding.layer + "。请选择 Layer。", MessageType.Warning);
                return;
            }

            var layerData = layers[layer];
            var machine = layerData.syncedLayerIndex >= 0 ? layers[layerData.syncedLayerIndex].stateMachine : layerData.stateMachine;
            var states = MotionStates(machine, "").ToArray();
            int index = System.Array.FindIndex(states, item => item.path == binding.state);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("Animator State");
                if (EditorGUILayout.DropdownButton(new GUIContent(string.IsNullOrEmpty(binding.state) ? "选择状态" : binding.StatePath), FocusType.Keyboard))
                {
                    var menu = new GenericMenu();
                    foreach (var entry in states)
                    {
                        var entryPath = entry.path;
                        menu.AddItem(new GUIContent(entryPath.Replace('.', '/')), entryPath == binding.state,
                            () => Change(() => { binding.state = entryPath; action.motion = binding; }));
                    }
                    if (states.Length == 0) menu.AddDisabledItem(new GUIContent("此层没有状态"));
                    menu.ShowAsContext();
                }
                SkillFieldHelp.Button("Animator State", "选择角色要进入的动画状态。运行时会在角色自己的 Animator 的对应层里查找这个状态。");
                using (new EditorGUI.DisabledScope(index < 0))
                    if (GUILayout.Button("定位状态", GUILayout.Width(80)))
                    {
                        AssetDatabase.OpenAsset(controller);
                        Selection.activeObject = states[index].state;
                    }
            }
            if (index < 0)
            {
                if (!string.IsNullOrEmpty(binding.state)) EditorGUILayout.HelpBox("此控制器中找不到 " + binding.StatePath, MessageType.Warning);
                return;
            }
            Motion motion = controller.GetStateEffectiveMotion(states[index].state, layer);
            if (overrides && motion is AnimationClip baseClip) motion = overrides[baseClip] ?? baseClip;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("State Motion", motion ? motion.name : "（空）");
                using (new EditorGUI.DisabledScope(!motion))
                    if (GUILayout.Button("定位资源", GUILayout.Width(80))) LocateMotion(motion);
            }
            DrawMotionParameters(runtime, controller);
            DrawMotionResult(MotionTimeline(action));
        }
    }
}
