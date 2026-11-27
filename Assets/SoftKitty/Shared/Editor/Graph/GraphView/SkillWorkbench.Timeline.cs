using System.Collections.Generic;
using System.Linq;
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
                        changed = SkillFieldHelp.DelayedIntField(parameter.name, previous is int i ? i : parameter.defaultInt, "设置时间轴预演开始时这个动画参数的值，用于判断 Animator 的状态转场。Trigger 勾选后在预演开始时触发一次。");
                        break;
                    case AnimatorControllerParameterType.Float:
                        defaultValue = parameter.defaultFloat;
                        changed = SkillFieldHelp.DelayedFloatField(parameter.name, previous is float f ? f : parameter.defaultFloat, "设置时间轴预演开始时这个动画参数的值，用于判断 Animator 的状态转场。Trigger 勾选后在预演开始时触发一次。");
                        break;
                    default:
                        bool trigger = parameter.type == AnimatorControllerParameterType.Trigger;
                        defaultValue = !trigger && parameter.defaultBool;
                        changed = SkillFieldHelp.Toggle(parameter.name + (trigger ? "（Trigger）" : ""), previous is bool b ? b : (bool)defaultValue, "设置时间轴预演开始时这个动画参数的值，用于判断 Animator 的状态转场。Trigger 勾选后在预演开始时触发一次。");
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

        private static readonly Color ContactColor = new Color(.36f, .82f, .96f);
        private static readonly Color LossColor = new Color(1f, .62f, .34f);

        private static void DrawTrialLegend(int count)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(count == 0 ? "本次试放尚无接触或扣血记录" : "本次试放实测", EditorStyles.miniBoldLabel, GUILayout.ExpandWidth(false));
                if (count == 0) return;
                foreach (var kind in new[] { SkillCastTrace.Event.Contact, SkillCastTrace.Event.HealthLoss })
                {
                    bool contact = kind == SkillCastTrace.Event.Contact;
                    var swatch = GUILayoutUtility.GetRect(8, EditorGUIUtility.singleLineHeight, GUILayout.Width(8));
                    EditorGUI.DrawRect(new Rect(swatch.x, swatch.center.y - 3, 7, 7), contact ? ContactColor : LossColor);
                    GUILayout.Label(contact ? "接触受击体" : "实际扣血", EditorStyles.miniLabel, GUILayout.ExpandWidth(false));
                }
                GUILayout.FlexibleSpace();
            }
        }

        private static string DescribeTrialMark(SkillCastTrace.Mark mark)
        {
            string origin = mark.flight > 0 ? $" · 第 {mark.flight} 发（{mark.emitter}）" : "";
            if (!string.IsNullOrEmpty(mark.via)) origin += " · 转向 " + mark.via;
            string segment = mark.segment > 0 ? $" · 第 {mark.segment} 段" : "";
            string result = mark.kind == SkillCastTrace.Event.Contact ? "接触" : $"扣血 {mark.loss:0.###}";
            return $"{mark.seconds:0.000} s · {mark.node}{origin}{segment} · {mark.target} · {result}";
        }

        private void DrawTrialMarks(Rect band, float horizon, float step, IEnumerable<SkillCastTrace.Mark> marks, string uid)
        {
            DrawTimelineGrid(band, horizon, step);
            float nextLabel = band.x;
            foreach (var mark in marks)
            {
                float x = band.x + band.width * mark.seconds / horizon;
                bool contact = mark.kind == SkillCastTrace.Event.Contact;
                float y = contact ? band.y + 1 : band.yMax - 8;
                EditorGUI.DrawRect(new Rect(x - 2, y, 4, 7), contact ? ContactColor : LossColor);
                if (x < nextLabel) continue;
                var label = new GUIContent(contact ? $"{mark.seconds:0.000}s" : $"{mark.seconds:0.000}s  −{mark.loss:0.##}");
                float width = EditorStyles.miniLabel.CalcSize(label).x;
                if (x + width + 4 <= band.xMax)
                {
                    GUI.Label(new Rect(x + 4, band.y, width, band.height), label, EditorStyles.miniLabel);
                    nextLabel = x + width + 7;
                }
            }
            var mouse = Event.current.mousePosition;
            if (!band.Contains(mouse)) return;
            float t = (mouse.x - band.x) * horizon / band.width;
            var near = marks.Where(m => Mathf.Abs(m.seconds - t) * band.width / horizon <= 6)
                .OrderBy(m => Mathf.Abs(m.seconds - t)).ToList();
            if (near.Count == 0) return;
            string details = string.Join("\n", near.Select(DescribeTrialMark));
            GUI.Label(band, new GUIContent("", details), GUIStyle.none);
            if (Event.current.type != EventType.MouseDown || Event.current.button != 0) return;
            selection = uid;
            cursor = near[0].seconds;
            selectedTrialMark = details;
            Event.current.Use();
            Repaint();
        }

        private static Rect TimelineLabel(Rect row) =>
            new Rect(row.x, row.y, Mathf.Min(260, row.width * .40f), row.height);

        private static Rect TimelineLane(Rect row) =>
            Rect.MinMaxRect(TimelineLabel(row).xMax + 5, row.y + 2, row.xMax - 4, row.yMax - 2);

        private static float TimelineTickStep(float horizon, float width)
        {
            float desired = horizon / Mathf.Max(1, Mathf.Floor(width / 64));
            float unit = Mathf.Pow(10, Mathf.Floor(Mathf.Log10(desired)));
            float ratio = desired / unit;
            return unit * (ratio <= 1 ? 1 : ratio <= 2 ? 2 : ratio <= 5 ? 5 : 10);
        }

        private void DrawTimelineRuler(Rect lane, float horizon, float step)
        {
            for (int i = 0; i * step <= horizon; i++)
            {
                float time = i * step;
                float x = lane.x + lane.width * time / horizon;
                EditorGUI.DrawRect(new Rect(x, lane.yMax - 3, 1, 3), Color.gray);
                var text = new GUIContent(time.ToString("0.###") + " s");
                float width = EditorStyles.miniLabel.CalcSize(text).x;
                GUI.Label(new Rect(Mathf.Min(x + 2, lane.xMax - width), lane.y - 2, width, lane.height), text, EditorStyles.miniLabel);
            }
            DrawTimelineCursor(lane, horizon);
            int control = GUIUtility.GetControlID(FocusType.Passive);
            var e = Event.current;
            switch (e.GetTypeForControl(control))
            {
                case EventType.MouseDown when e.button == 0 && lane.Contains(e.mousePosition):
                    GUIUtility.hotControl = control;
                    break;
                case EventType.MouseDrag when GUIUtility.hotControl == control:
                    break;
                case EventType.MouseUp when GUIUtility.hotControl == control:
                    GUIUtility.hotControl = 0;
                    break;
                default:
                    return;
            }
            cursor = Mathf.Clamp01((e.mousePosition.x - lane.x) / lane.width) * horizon;
            e.Use();
            Repaint();
        }

        private static void DrawTimelineGrid(Rect lane, float horizon, float step)
        {
            EditorGUI.DrawRect(lane, new Color(.13f, .19f, .21f));
            for (int i = 0; i * step <= horizon; i++)
                EditorGUI.DrawRect(new Rect(lane.x + lane.width * i * step / horizon, lane.y, 1, lane.height), new Color(.22f, .29f, .30f));
        }

        private void DrawTimelineCursor(Rect lane, float horizon) =>
            EditorGUI.DrawRect(new Rect(lane.x + cursor / horizon * lane.width, lane.y, 1, lane.height), Color.white);

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
