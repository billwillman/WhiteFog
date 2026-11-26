using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SoftKitty
{
    public partial class SkillWorkbench
    {
        private readonly Dictionary<(RuntimeAnimatorController, string, float), SkillMotionTimeline> motionTimings =
            new Dictionary<(RuntimeAnimatorController, string, float), SkillMotionTimeline>();
        private readonly Dictionary<RuntimeAnimatorController, Dictionary<string, object>> motionParameters =
            new Dictionary<RuntimeAnimatorController, Dictionary<string, object>>();
        private bool expandMotionParameters;

        private void RefreshMotionTimings()
        {
            motionTimings.Clear();
            Repaint();
        }

        private UndoPropertyModification[] OnMotionAssetEdited(UndoPropertyModification[] modifications)
        {
            foreach (var modification in modifications)
            {
                var targetAsset = modification.currentValue?.target;
                if (targetAsset is RuntimeAnimatorController || targetAsset is AnimatorState ||
                    targetAsset is AnimatorStateMachine || targetAsset is AnimatorTransitionBase || targetAsset is Motion)
                {
                    RefreshMotionTimings();
                    break;
                }
            }
            return modifications;
        }

        private Dictionary<string, object> ParametersFor(RuntimeAnimatorController runtime)
        {
            if (!motionParameters.TryGetValue(runtime, out var values))
                motionParameters[runtime] = values = new Dictionary<string, object>();
            return values;
        }

        private SkillMotionTimeline MotionTimeline(Action action)
        {
            if (!SkillMotion.UsesBinding(action)) return null;
            var motion = SkillMotion.For(action, graph as OriginalSkillGraph);
            if (motion.source == SkillMotion.Source.AnimationClip) return SkillMotionTimeline.Clip(motion.clip);
            var runtime = MotionController(graph, action);
            var key = (runtime, motion.StatePath, motion.transitionSeconds);
            if (motionTimings.TryGetValue(key, out var existing)) return existing;
            var controller = runtime is AnimatorOverrideController wrapped ? wrapped.runtimeAnimatorController : runtime;
            return motionTimings[key] = SkillMotionTimeline.Sample(runtime, controller as AnimatorController, motion,
                runtime ? ParametersFor(runtime) : null);
        }

        private static string TimingLabel(SkillMotionTimeline timing) =>
            timing.complete ? timing.seconds.ToString("0.00") + " 秒" : "时长待定";

        private static void DrawMotionResult(SkillMotionTimeline timing)
        {
            if (timing == null) return;
            EditorGUILayout.LabelField("每次播放时长（预演）", TimingLabel(timing));
            EditorGUILayout.HelpBox(timing.Description, timing.complete ? MessageType.None : MessageType.Info);
        }

        private void DrawMotionParameters(RuntimeAnimatorController runtime, AnimatorController controller)
        {
            var parameters = controller.parameters;
            if (parameters.Length == 0) return;
            expandMotionParameters = EditorGUILayout.Foldout(expandMotionParameters, "预演起始参数（" + parameters.Length + "）", true);
            if (!expandMotionParameters) return;
            var values = ParametersFor(runtime);
            EditorGUILayout.HelpBox("参数仅用于这张时间轴。Trigger 在起手时触发一次。", MessageType.None);
            foreach (var parameter in parameters)
            {
                values.TryGetValue(parameter.name, out var previous);
                object changed, defaultValue;
                EditorGUI.BeginChangeCheck();
                switch (parameter.type)
                {
                    case AnimatorControllerParameterType.Int:
                        defaultValue = parameter.defaultInt;
                        changed = EditorGUILayout.DelayedIntField(parameter.name, previous is int i ? i : parameter.defaultInt);
                        break;
                    case AnimatorControllerParameterType.Float:
                        defaultValue = parameter.defaultFloat;
                        changed = EditorGUILayout.DelayedFloatField(parameter.name, previous is float f ? f : parameter.defaultFloat);
                        break;
                    default:
                        bool trigger = parameter.type == AnimatorControllerParameterType.Trigger;
                        defaultValue = !trigger && parameter.defaultBool;
                        changed = EditorGUILayout.Toggle(parameter.name + (trigger ? "（Trigger）" : ""), previous is bool b ? b : (bool)defaultValue);
                        break;
                }
                if (EditorGUI.EndChangeCheck())
                {
                    if (Equals(changed, defaultValue)) values.Remove(parameter.name);
                    else values[parameter.name] = changed;
                    RefreshMotionTimings();
                }
            }
            using (new EditorGUI.DisabledScope(values.Count == 0))
                if (GUILayout.Button("恢复 Controller 默认参数")) { values.Clear(); RefreshMotionTimings(); }
        }

        private static void DrawMotionBands(Rect lane, float horizon, Action action, SkillMotionTimeline timing)
        {
            float scale = lane.width / horizon;
            float start = lane.x + action.WaitTime * scale;
            float repeats = Span(action);
            if (repeats > 0)
            {
                // Paint the scheduling window below the first playback and the last possible tail.
                EditorGUI.DrawRect(new Rect(start, lane.yMax - 3, scale * (repeats + timing.seconds), 2), new Color(.47f,.58f,.66f));
            }
            for (int i = 0; i < timing.bands.Count; i++)
            {
                var band = timing.bands[i];
                float end = i + 1 < timing.bands.Count ? timing.bands[i+1].start : timing.seconds;
                if (end <= band.start) continue;
                var rect = new Rect(start + scale * band.start, lane.y + 2, Mathf.Max(1, (end - band.start) * scale), lane.height - 5);
                EditorGUI.DrawRect(rect, band.blend ? new Color(.95f,.73f,.42f) : new Color(.31f,.72f,.62f));
                GUI.Label(rect, new GUIContent("", band.name + "  " + band.start.ToString("0.00") + "–" + end.ToString("0.00") + " 秒"), GUIStyle.none);
            }
            if (!timing.complete)
                EditorGUI.DrawRect(new Rect(start + scale * timing.seconds, lane.y + 1, 4, lane.height - 2), new Color(.96f,.7f,.34f));
        }
    }
}
