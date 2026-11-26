using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SoftKitty
{
    public partial class SkillWorkbench
    {
        private static readonly string[] VisualLabels = { "播放角色动画", "播放施法特效", "挂接武器特效", "产生投射物", "收招标记", "位移标记", "播放轨道特效" };
        private static string VisualLabel(int stage) => stage >= 0 && stage < VisualLabels.Length ? VisualLabels[stage] : "未知表现";

        private static void AddActionChoices(GenericMenu menu, System.Action<string, int> choose)
        {
            string folder = Application.dataPath + "/SoftKitty/Shared/Scripts/CustomClass/Action";
            foreach (var file in Directory.GetFiles(folder, "*Action_interface.txt").OrderBy(f => f))
            {
                string key = Path.GetFileName(file).Replace("Action_interface.txt", "");
                if (key == "OriginalSkillVisual")
                {
                    menu.AddItem(new GUIContent("挂点特效（可配置命中盒）"), false, () => choose(key, 6));
                }
                else if (key == "SkillSignal")
                {
                    menu.AddItem(new GUIContent("发送施法提示事件"), false, () => choose("CastSignal", 0));
                    menu.AddItem(new GUIContent("显示提示环"), false, () => choose("SignalRing", 0));
                }
                else if (key == "SkillVital")
                {
                    menu.AddItem(new GUIContent("回复生命（限制上限）"), false, () => choose("CappedHeal", 0));
                    menu.AddItem(new GUIContent("限时吸收护盾"), false, () => choose(key, 0));
                }
                else menu.AddItem(new GUIContent(key == "Animation" ? "播放角色动画" : GraphEditorText.Localize(key)), false, () => choose(key, 0));
            }
        }

        internal static Action AddConfiguredAction(Root root, string key, int stage, float time, Action after = null)
        {
            Action node;
            if (key == "CappedHeal") node = AddCappedHeal(root, time, 25f);
            else if (key == "CastSignal")
            {
                node = AddAction(root, "Event", time);
                node.StringValues[0] = "施法提示";
                node.BoolValues[1] = true;
                root.Entities.Last().Type = EntityType.Dealer;
            }
            else if (key == "SignalRing")
            {
                node = AddAction(root, "SkillSignal", time);
                node.BoolValues[0] = true;
                node.FloatValues[0] = .35f;
            }
            else
            {
                node = AddAction(root, key, time);
                if (key == "FormulaImpact")
                {
                    node.calculation.mode = SkillCalculation.Mode.SharedFlow;
                    node.calculation.flow = AssetDatabase.LoadAssetAtPath<SkillDamageFlow>(FormulaDirectory + "通用伤害流程.asset");
                }
                if (key == "Animation")
                {
                    node.IntValues[0] = (int)AnimationAction.AnimTypes.SkillMotionBinding;
                    node.motion = new SkillMotion();
                    root.Entities.Last().Type = EntityType.Dealer;
                }
            }
            if (key == "OriginalSkillVisual") node.IntValues[0] = stage;
            if (after != null)
            {
                after.EnsureArrivalPort();
                root.outputPort[0].uid.Remove(node.Uid + "#0");
                node.inputPort[0].uid.Remove(root.Uid + "#0");
                Wire(after, node, 0);
            }
            return node;
        }

        private static GameObject PrefabField(string label, GameObject prefab) =>
            (GameObject)EditorGUILayout.ObjectField(label, prefab, typeof(GameObject), false);

        private void DrawProjectile(Action node)
        {
            // Draw into a copy so opening the inspector doesn't migrate assets or bypass Undo.
            var shot = SkillProjectile.For(node, graph).Copy();
            EditorGUI.BeginChangeCheck();
            shot.prefab = PrefabField("投射物 Prefab", shot.prefab);
            shot.trail = PrefabField("拖尾 Prefab", shot.trail);
            if (shot.trail) shot.trailSeconds = Mathf.Max(0, EditorGUILayout.FloatField("拖尾消散（秒）", shot.trailSeconds));
            shot.launchBone = EditorGUILayout.TextField("发射挂点", shot.launchBone ?? "");
            shot.launchOffset = EditorGUILayout.Vector3Field("发射偏移（角色局部）", shot.launchOffset);
            shot.targetBone = EditorGUILayout.TextField("目标挂点", shot.targetBone ?? "");
            shot.impactOffset = EditorGUILayout.Vector3Field("目标偏移（角色局部）", shot.impactOffset);
            shot.path = (SkillProjectile.FlightPath)EditorGUILayout.Popup("飞行方式", (int)shot.path, new[] { "飞向出手时的锁定点", "追踪移动目标" });
            shot.speed = Mathf.Max(.01f, EditorGUILayout.FloatField("初速度（米/秒）", shot.speed));
            shot.acceleration = EditorGUILayout.FloatField("加速度（米/秒²）", shot.acceleration);
            if (shot.path == SkillProjectile.FlightPath.FollowTarget)
                shot.trackingTimeout = Mathf.Max(.01f, EditorGUILayout.FloatField("最长追踪（秒）", shot.trackingTimeout));
            DrawHitBoxFields(shot);
            shot.impact = PrefabField("命中特效", shot.impact);
            if (shot.impact) shot.impactScale = Mathf.Max(0, EditorGUILayout.FloatField("命中特效缩放", shot.impactScale));
            if (EditorGUI.EndChangeCheck()) Change(() => node.projectile = shot);
        }

        private void DrawHitActions(Action node)
        {
            EditorGUILayout.LabelField("命中后执行", EditorStyles.boldLabel);
            if (node.outputPort.Count == 0 || node.outputPort[0].uid.Count == 0)
                EditorGUILayout.LabelField("未连接命中动作。");
            else
                foreach (var link in node.outputPort[0].uid)
                {
                    string uid = link.Split('#')[0];
                    var child = graph.RootSet.Actions.Find(a => a.Uid == uid);
                    if (child == null) { EditorGUILayout.LabelField("执行分支 " + uid); continue; }
                    string label = HitActionLabel(child);
                    if (child.ClassName == "FormulaImpact") label += "  ·  " + CalculationSource(child);
                    if (GUILayout.Button("查看 " + label)) { selection = uid; Repaint(); }
                }
            if (GUILayout.Button("添加命中后执行的动作"))
            {
                var menu = new GenericMenu();
                AddActionChoices(menu, (key, stage) => Change(() => selection = AddConfiguredAction(graph.RootSet, key, stage, 0, node).Uid));
                menu.ShowAsContext();
            }
        }

        private static void DrawHitBoxFields(SkillProjectile shot)
        {
            shot.hitRecipients = (SkillProjectile.HitRecipients)EditorGUILayout.Popup("可命中对象", (int)shot.hitRecipients,
                new[] { "确定目标 · 仅瞄准实体", "非确定目标 · 首个符合标签的受击体" });
            if (shot.hitRecipients == SkillProjectile.HitRecipients.FirstMatchingEntity)
            {
                EditorGUILayout.LabelField("可命中 MCC 实体标签（匹配任一项）");
                for (int i = 0; i < shot.hitTags.Count; i++)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        shot.hitTags[i] = EditorGUILayout.TextField(shot.hitTags[i]).Trim();
                        if (GUILayout.Button("移除", GUILayout.Width(48)))
                        {
                            shot.hitTags.RemoveAt(i--);
                            GUI.changed = true;
                        }
                    }
                }
                if (GUILayout.Button("添加可命中标签"))
                {
                    shot.hitTags.Add("");
                    GUI.changed = true;
                }
                if (!shot.hitTags.Any(tag => !string.IsNullOrWhiteSpace(tag)))
                    EditorGUILayout.HelpBox("请填写受击方的 MCC 实体标签。预览场中，敌方为 hostile，主角为 friendly。", MessageType.Info);
            }
            shot.hitBoxSize = Vector3.Max(Vector3.one * .01f, EditorGUILayout.Vector3Field("命中盒尺寸（米）", shot.hitBoxSize));
            shot.hitBoxStart = Mathf.Max(0, EditorGUILayout.FloatField("命中盒开启（发射后秒数）", shot.hitBoxStart));
            shot.hitBoxSeconds = Mathf.Max(0, EditorGUILayout.FloatField("开启时长（0 至飞行结束）", shot.hitBoxSeconds));
            shot.hitBoxOffset = EditorGUILayout.Vector3Field("命中盒偏移（飞行局部）", shot.hitBoxOffset);
            var layerMask = UnityEditorInternal.InternalEditorUtility.LayerMaskToConcatenatedLayersMask(shot.hitLayers);
            int pickedLayers = EditorGUILayout.MaskField("受击层", layerMask, UnityEditorInternal.InternalEditorUtility.layers);
            if (pickedLayers != layerMask) shot.hitLayers = UnityEditorInternal.InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(pickedLayers);
        }

        private void DrawStrikeHitBox(Action node)
        {
            var shot = SkillProjectile.For(node, graph).Copy();
            EditorGUI.BeginChangeCheck();
            DrawHitBoxFields(shot);
            if (EditorGUI.EndChangeCheck()) Change(() => node.projectile = shot);
        }

        private void DrawVisualAction(Action node)
        {
            int stage = node.IntValues.ElementAtOrDefault(0);
            if (stage != 1 && stage != 2 && stage != 6) return;
            var fx = OriginalSkillVisualAction.EffectFor(node, graph as OriginalSkillGraph)?.Copy() ?? new OriginalSkillGraph.EffectCue();
            EditorGUI.BeginChangeCheck();
            fx.prefab = PrefabField("特效 Prefab", fx.prefab);
            fx.onTarget = EditorGUILayout.Popup("挂接对象", fx.onTarget ? 1 : 0, new[] { "施法者", "目标" }) == 1;
            fx.bone = EditorGUILayout.TextField("挂点", fx.bone ?? "");
            fx.followBone = EditorGUILayout.Toggle("跟随位置", fx.followBone);
            fx.followRotation = EditorGUILayout.Toggle("使用挂点旋转", fx.followRotation);
            fx.duration = Mathf.Max(0, EditorGUILayout.FloatField("显示时长（0 按粒子）", fx.duration));
            if (stage == 6) fx.triggerHit = EditorGUILayout.Toggle("触发目标命中", fx.triggerHit);
            if (stage == 6 && fx.triggerHit)
            {
                fx.hitDelay = Mathf.Max(0, EditorGUILayout.FloatField("判定延迟（秒）", fx.hitDelay));
                fx.hitBoxSize = Vector3.Max(Vector3.one * .01f, EditorGUILayout.Vector3Field("判定盒尺寸（米）", fx.hitBoxSize));
            }
            if (EditorGUI.EndChangeCheck()) Change(() => node.visualEffect = fx);
        }
    }
}
