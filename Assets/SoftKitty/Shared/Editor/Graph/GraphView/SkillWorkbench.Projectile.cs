using System;
using System.Collections.Generic;
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

        private static void AddActionChoices(GenericMenu menu, System.Action<string, int> choose, bool projectileContact = false)
        {
            string folder = Application.dataPath + "/SoftKitty/Shared/Scripts/CustomClass/Action";
            foreach (var file in Directory.GetFiles(folder, "*Action_interface.txt").OrderBy(f => f))
            {
                string key = Path.GetFileName(file).Replace("Action_interface.txt", "");
                if (key == "RedirectProjectile" && !projectileContact) continue;
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

        private static IEnumerable<NodeBase> LayoutNodes(Root root) =>
            new NodeBase[] { root }.Concat(root.Actions).Concat(root.Entities).Concat(root.Variables)
                .Concat(root.Handlers).Concat(root.Triggers).Concat(root.ConditionGroups).Concat(root.Maths).Concat(root.Branchs);

        private const float LayoutCardWidth = 320;
        private const float LayoutColumnStep = LayoutCardWidth + 96;
        private const float LayoutRowGap = 64;

        // The workbench can author nodes while the GraphView is closed. Reserve space for expanded cards.
        private static Rect LayoutRect(NodeBase node)
        {
            float height = node is EntityClass ? 144 : node is VariableClass ? 208 : node is Root ? 256 : 320;
            if (node is Action action)
                height = (action.ClassName == "FormulaImpact" ? 880 : 560) + action.outputPort.Sum(p => p.uid.Count) * 24;
            return new Rect(node.Position, new Vector2(LayoutCardWidth, height));
        }

        private static IEnumerable<Rect> OccupiedNodeRects(Root root) =>
            LayoutNodes(root).Select(LayoutRect).Concat(root.Notes.Select(n => new Rect(n.Position, n.Size)))
                .Concat(root.Groups.Select(g => g.Position));

        internal static Action AddConfiguredAction(Root root, string key, int stage, float time, Action after = null)
        {
            bool placeGroup = after != null || key == "CappedHeal";
            var existing = placeGroup ? new HashSet<NodeBase>(LayoutNodes(root)) : null;
            var occupied = placeGroup ? OccupiedNodeRects(root).ToList() : null;
            Action node;
            if (key == "CappedHeal") node = AddCappedHeal(root, time, 25f);
            else if (key == "OverTimeEffect") node = AddBuffAction(root, -1, 0, EntityType.Target, time);
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
            if (SkillProjectile.IsLaunch(node)) node.requiresProjectileSnap = true;
            if (after != null)
            {
                after.EnsureArrivalPort();
                root.outputPort[0].uid.Remove(node.Uid + "#0");
                node.inputPort[0].uid.Remove(root.Uid + "#0");
                Wire(after, node, 0);
            }
            if (placeGroup)
            {
                var added = LayoutNodes(root).Where(n => !existing.Contains(n)).ToList();
                PlaceActionGroup(node, after, added, occupied);
            }
            return node;
        }

        private static void PlaceActionGroup(Action action, Action source, List<NodeBase> added, List<Rect> occupied)
        {
            Vector2 offset = source == null
                ? Vector2.up * (occupied.Max(r => r.yMax) + LayoutRowGap - added.Min(n => n.Position.y))
                : source.Position + Vector2.right * LayoutColumnStep - action.Position;
            foreach (var node in added) node.Position += offset;

            // Keep the corridor between execution ports free, including when several actions share a hit.
            var inputs = added.Where(n => n != action).ToList();
            if (source != null && inputs.Count > 0 && inputs.All(n => n is EntityClass || n is VariableClass))
            {
                float down = Mathf.Max(0, LayoutRect(source).yMax + LayoutRowGap - inputs.Min(n => n.Position.y));
                foreach (var input in inputs) input.Position += new Vector2(-96, down);
            }

            PlaceBelowObstacles(added, occupied);
        }

        private static void PlaceBelowObstacles(List<NodeBase> added, List<Rect> occupied)
        {
            float shift;
            do
            {
                shift = 0;
                foreach (var node in added)
                {
                    var card = LayoutRect(node);
                    foreach (var other in occupied)
                    {
                        var clearance = Rect.MinMaxRect(other.xMin - 32, other.yMin - LayoutRowGap,
                            other.xMax + 32, other.yMax + LayoutRowGap);
                        if (card.Overlaps(clearance)) shift = Mathf.Max(shift, clearance.yMax - card.yMin);
                    }
                }
                if (shift > 0) foreach (var node in added) node.Position += Vector2.up * shift;
            } while (shift > 0);
        }

        private static GameObject PrefabField(string label, GameObject prefab, string help) =>
            (GameObject)SkillFieldHelp.ObjectField(label, prefab, typeof(GameObject), false, help);

        private void DrawProjectile(Action node, bool impactNode = false)
        {
            var snap = node.projectileSnap;
            var selected = (SkillProjectileSnap)SkillFieldHelp.ObjectField("投射物 Snap", snap,
                typeof(SkillProjectileSnap), false, "复用外观、弹道、碰撞窗口和命中特效。编辑 Snap 会同步更新引用它的节点。");
            if (selected != snap)
            {
                Change(() => SetProjectileSnap(node, selected, impactNode));
                snap = selected;
            }
            if (GUILayout.Button(snap ? "另存投射物 Snap" : "新建投射物 Snap"))
            {
                string path = EditorUtility.SaveFilePanelInProject("保存投射物 Snap", "投射物 Snap", "asset", "选择 Snap 的保存位置。");
                if (!string.IsNullOrEmpty(path))
                {
                    var asset = CreateInstance<SkillProjectileSnap>();
                    asset.settings = SkillProjectile.For(node, graph).Copy();
                    if (impactNode && !snap && node.IntValues[0] != (int)SkillPlayback.Style.Projectile && node.FloatValues.Count > 1 && node.FloatValues[1] > 0)
                        asset.settings.speed = node.FloatValues[1];
                    AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath(path));
                    Change(() => SetProjectileSnap(node, asset, impactNode));
                    snap = asset;
                }
            }
            if (!snap)
            {
                var text = node.requiresProjectileSnap || SkillProjectile.IsImpactLaunch(node)
                    ? "请选择或新建投射物 Snap。绑定后才会发射。"
                    : "此节点沿用原来的投射设置。新建 Snap 可从现有设置开始，也可选择已有 Snap。";
                EditorGUILayout.HelpBox(text, MessageType.Info);
                return;
            }
            // Draw into a copy so opening the inspector doesn't migrate assets or bypass Undo.
            var shot = SkillProjectile.For(node, graph).Copy();
            EditorGUI.BeginChangeCheck();
            shot.prefab = PrefabField("投射物 Prefab", shot.prefab, "飞行中的投射物外观。拖入飞剑、法球等预制体；飞行速度和命中范围分别由下方设置控制。");
            shot.trail = PrefabField("拖尾 Prefab", shot.trail, "投射物飞行时跟随的尾迹预制体。留空会省略尾迹。");
            if (shot.trail) shot.trailSeconds = Mathf.Max(0, SkillFieldHelp.FloatField("拖尾消散（秒）", shot.trailSeconds, "飞行结束后停止发射尾迹粒子，并在这里指定的秒数后移除拖尾对象。"));
            shot.launchBone = SkillFieldHelp.TextField("发射挂点", shot.launchBone ?? "", "在施法者模型中查找这个名字的骨骼或子物体，将其作为投射物起点。留空时使用角色根部。");
            shot.launchOffset = SkillFieldHelp.Vector3Field("发射偏移（角色局部）", shot.launchOffset, "在发射挂点上，按施法者自身朝向移动起点。X、Y、Z 分别对应角色的右方、上方、前方，单位为米。");
            shot.targetBone = SkillFieldHelp.TextField("目标挂点", shot.targetBone ?? "", "在被瞄准角色的模型中查找这个名字的骨骼或子物体，将其作为飞行终点的参考位置。");
            shot.impactOffset = SkillFieldHelp.Vector3Field("目标偏移（角色局部）", shot.impactOffset, "从目标挂点出发，按目标自身朝向偏移飞行终点。X、Y、Z 分别对应右、上、前，单位为米。");
            shot.path = (SkillProjectile.FlightPath)SkillFieldHelp.Popup("飞行方式", (int)shot.path, new[] { "飞向出手时的锁定点", "追踪移动目标" }, "锁定点模式会飞向出手时记录的位置；追踪模式会随目标移动更新终点，直到命中或追踪结束。");
            shot.speed = Mathf.Max(.01f, SkillFieldHelp.FloatField("初速度（米/秒）", shot.speed, "设置投射物刚发射时每秒飞行的距离。例如填 10，且加速度为 0 时，每秒飞行 10 米。"));
            shot.acceleration = SkillFieldHelp.FloatField("加速度（米/秒²）", shot.acceleration, "每秒改变多少飞行速度。正值加速，负值减速，填 0 则保持初速度。");
            if (shot.path == SkillProjectile.FlightPath.FollowTarget)
                shot.trackingTimeout = Mathf.Max(.01f, SkillFieldHelp.FloatField("最长追踪（秒）", shot.trackingTimeout, "追踪移动目标时最多飞行多久。超过这段时间便结束追踪。"));
            DrawHitBoxFields(shot);
            shot.impact = PrefabField("命中特效", shot.impact, "投射物接触有效受击体时，在接触点生成的特效预制体。");
            if (shot.impact) shot.impactScale = Mathf.Max(0, SkillFieldHelp.FloatField("命中特效缩放", shot.impactScale, "按比例缩放命中特效的外观。1 为原尺寸，2 为两倍；碰撞范围由命中盒尺寸控制。"));
            if (!EditorGUI.EndChangeCheck()) return;
            Undo.RecordObject(snap, "修改投射物 Snap");
            snap.settings = shot;
            EditorUtility.SetDirty(snap);
            AssetDatabase.SaveAssetIfDirty(snap);
        }

        private static void SetProjectileSnap(Action node, SkillProjectileSnap snap, bool impactNode)
        {
            node.projectileSnap = snap;
            node.requiresProjectileSnap = true;
            if (snap) node.projectile = null;
            if (impactNode && snap)
            {
                node.IntValues[0] = (int)SkillPlayback.Style.Projectile;
                node.EnsureArrivalPort();
            }
        }

        private void DrawHitActions(Action node)
        {
            EditorGUILayout.LabelField(node.EmitsSettledHit ? "每段结算命中后执行" : "接触受击体后执行", EditorStyles.boldLabel);
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
                AddActionChoices(menu, (key, stage) => Change(() => selection = AddConfiguredAction(graph.RootSet, key, stage, 0, node).Uid), SkillProjectile.IsLaunch(node) || SkillProjectile.IsImpactLaunch(node));
                menu.ShowAsContext();
            }
            string buffHelp = node.EmitsSettledHit ? "每段成功结算后对实际受击者施加状态，未通过命中判定的段会跳过。" :
                "有效接触后，对实际碰到的实体执行状态动作。若 Buff 依赖伤害图的命中率判定，请将状态动作放在该图的判定成功分支。";
            if (GUILayout.Button(new GUIContent("添加命中后施加 Buff", buffHelp)))
                Change(() => selection = AddConfiguredAction(graph.RootSet, "OverTimeEffect", 0, 0, node).Uid);
        }

        private static void DrawHitBoxFields(SkillProjectile shot)
        {
            shot.hitRecipients = (SkillProjectile.HitRecipients)SkillFieldHelp.Popup("可命中对象", (int)shot.hitRecipients,
                new[] { "确定目标 · 仅瞄准实体", "非确定目标 · 首个符合标签的受击体" }, "「仅瞄准实体」只检测本次目标。「首个符合标签的受击体」会在飞行路径上查找匹配任一指定标签的实体。");
            if (shot.hitRecipients == SkillProjectile.HitRecipients.FirstMatchingEntity)
            {
                using (new SkillFieldHelp.Row("可命中 MCC 实体标签", "列表至少填写一个受击方的 MCC 实体标签。飞行时选择路径上第一个匹配任一标签的受击体。"))
                    EditorGUILayout.LabelField("可命中 MCC 实体标签（匹配任一项）");
                for (int i = 0; i < shot.hitTags.Count; i++)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        shot.hitTags[i] = EditorGUILayout.TextField(shot.hitTags[i]).Trim();
                        SkillFieldHelp.Button("可命中标签", "填写受击方的 MCC 实体标签。列表中任一项匹配即可，预览中的敌人使用 hostile，主角使用 friendly。");
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
            shot.hitBoxSize = Vector3.Max(Vector3.one * .01f, SkillFieldHelp.Vector3Field("命中盒尺寸（米）", shot.hitBoxSize, "投射物检测受击体所用盒子的完整宽、高、深，对应 X、Y、Z。增大数值会扩大飞行路径上的接触范围。"));
            shot.hitBoxStart = Mathf.Max(0, SkillFieldHelp.FloatField("命中盒开启（发射后秒数）", shot.hitBoxStart, "从投射物发射开始，延迟多久才允许检测接触。填 0 会从发射位置开始检测。"));
            shot.hitBoxSeconds = Mathf.Max(0, SkillFieldHelp.FloatField("开启时长（0 至飞行结束）", shot.hitBoxSeconds, "从命中盒开启开始，可以检测接触的持续秒数。填 0 会一直检测到飞行结束。"));
            shot.hitBoxOffset = SkillFieldHelp.Vector3Field("命中盒偏移（飞行局部）", shot.hitBoxOffset, "相对于投射物的位置移动命中盒中心。Z 沿飞行方向，X 和 Y 分别向右、向上，单位为米。");
            var layerMask = UnityEditorInternal.InternalEditorUtility.LayerMaskToConcatenatedLayersMask(shot.hitLayers);
            int pickedLayers = SkillFieldHelp.MaskField("受击层", layerMask, UnityEditorInternal.InternalEditorUtility.layers, "选择参与接触检测的 Unity 物理层。受击体的碰撞器必须在勾选的层内。");
            if (pickedLayers != layerMask) shot.hitLayers = UnityEditorInternal.InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(pickedLayers);
        }

        private void DrawVisualAction(Action node)
        {
            int stage = node.IntValues.ElementAtOrDefault(0);
            if (stage != 1 && stage != 2 && stage != 6) return;
            var fx = OriginalSkillVisualAction.EffectFor(node, graph as OriginalSkillGraph)?.Copy() ?? new OriginalSkillGraph.EffectCue();
            EditorGUI.BeginChangeCheck();
            fx.prefab = PrefabField("特效 Prefab", fx.prefab, "这个动作播放的特效预制体。比如剑雨落地的粒子效果；落在哪里由「挂接对象」和「挂点」决定。");
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
