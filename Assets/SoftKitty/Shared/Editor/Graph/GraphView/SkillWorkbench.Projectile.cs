using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectileField = SoftKitty.SkillProjectile.Field;

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
                else
                {
                    string label = key == "Animation" ? "播放角色动画" : GraphEditorText.Localize(key);
                    if (projectileContact)
                    {
                        switch (key)
                        {
                            case "FormulaImpact": label = "结算本次伤害（分段命中与公式结算）"; break;
                            case "RedirectProjectile": label = "改变当前投射物目标（继续飞行）"; break;
                            case "Projectile": label = "产生新的投射物"; break;
                        }
                    }
                    menu.AddItem(new GUIContent(label), false, () => choose(key, 0));
                }
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
                if (after.ClassName == "SelectTarget")
                {
                    foreach (var port in node.inputPort.Where(p => p.Type == "EntityIn"))
                    {
                        var bound = root.Entities.Find(e => port.uid.Contains(e.Uid + "#0"));
                        if (bound != null && (bound.Type == EntityType.NextCastTarget || bound.Type == EntityType.Target))
                            bound.Type = EntityType.SelectedTarget;
                    }
                }
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

        private static SkillProjectileSnap ProjectileConfigField(SkillProjectileSnap config)
        {
            const string title = "投射物配置";
            const string help = "选择这次发射使用的配置文件。每项默认跟随配置。点「复写」为当前节点单独设置，点「恢复」重新跟随配置。要修改共用的 Prefab 或命中盒，点「打开投射物 Asset」，在 Inspector 里编辑。";
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.HorizontalScope(GUILayout.Width(EditorGUIUtility.labelWidth)))
                {
                    GUILayout.Label(title, GUILayout.ExpandWidth(false));
                    SkillFieldHelp.Button(title, help);
                    GUILayout.FlexibleSpace();
                }
                var field = EditorGUILayout.GetControlRect(false);
                bool repaint = Event.current.type == EventType.Repaint;
                // Keep Unity's picker and drag handling, and draw the value without its generated type suffix.
                Color color = GUI.contentColor;
                SkillProjectileSnap selected;
                try
                {
                    if (repaint) GUI.contentColor = Color.clear;
                    selected = (SkillProjectileSnap)EditorGUI.ObjectField(field, config, typeof(SkillProjectileSnap), false);
                }
                finally { GUI.contentColor = color; }
                field.xMin += 3;
                field.xMax -= 20; // Leave the native picker button visible.
                GUI.Label(field, config ? config.name : "请选择投射物配置", EditorStyles.label);
                return selected;
            }
        }

        private void DrawProjectile(Action node, bool impactNode = false)
        {
            var snap = node.projectileSnap;
            var selected = ProjectileConfigField(snap);
            if (selected != snap)
            {
                Change(() => SetProjectileSnap(node, selected, impactNode));
                snap = selected;
            }
            string saveLabel = snap ? "另存投射物配置" : "新建投射物配置";
            string saveHelp = snap
                ? "将配置原值和本节点的复写合并为新配置。当前节点改用新配置并清除复写，其他节点继续使用原配置。"
                : "从当前投射参数创建配置资产，并绑定到这个节点。保存后，其他技能也可以选择这份配置。";
            bool save;
            using (new SkillFieldHelp.Row(saveLabel, saveHelp))
                save = GUILayout.Button(saveLabel);
            if (save)
            {
                string path = EditorUtility.SaveFilePanelInProject("保存投射物配置", "投射物配置", "asset", "选择投射物配置的保存位置。");
                if (!string.IsNullOrEmpty(path))
                {
                    SaveProjectileConfig(node, impactNode, path);
                    snap = node.projectileSnap;
                }
            }
            bool bound = snap && snap.settings != null;
            if (!bound)
            {
                string hint = node.projectile != null
                    ? "尚未绑定配置。下方显示旧节点保存的参数，点「新建投射物配置」可将它们保存成文件并绑定。"
                    : "请选择或新建投射物配置。绑定后才能发射。";
                EditorGUILayout.HelpBox(hint, MessageType.Info);
            }
            var defaults = SkillProjectile.For(node, graph);
            var effective = defaults.WithOverrides(node);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(bound ? "投射物配置值（只读）" : "待绑定的投射参数（只读预览）", EditorStyles.boldLabel);
                    using (new EditorGUI.DisabledScope(!snap))
                        if (GUILayout.Button("打开投射物 Asset", GUILayout.ExpandWidth(false)))
                            EditorUtility.OpenPropertyEditor(snap);
                }
                var preview = defaults.Copy();
                DrawProjectileSettings(preview, (field, draw) =>
                {
                    bool overridden = node.OverridesProjectileField(field);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUILayout.VerticalScope())
                        using (new EditorGUI.DisabledScope(true)) draw(preview);
                        using (new EditorGUI.DisabledScope(!bound && !overridden))
                            if (GUILayout.Button(overridden ? "恢复" : "复写", GUILayout.Width(52)))
                                Change(() => node.SetProjectileOverride(field, overridden ? null : defaults));
                    }
                    if (!overridden) return;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label("本节点", EditorStyles.boldLabel, GUILayout.Width(52));
                        using (new EditorGUILayout.VerticalScope())
                        using (var change = new EditorGUI.ChangeCheckScope())
                        {
                            draw(effective);
                            if (change.changed) Change(() => node.SetProjectileOverride(field, effective));
                        }
                    }
                }, effective);
            }
        }

        private void SaveProjectileConfig(Action node, bool impactNode, string path)
        {
            var settings = SkillProjectile.For(node, graph).WithOverrides(node);
            if (impactNode && !node.projectileSnap && !node.OverridesProjectileField(ProjectileField.Speed)
                && node.IntValues[0] != (int)SkillPlayback.Style.Projectile && node.FloatValues.Count > 1 && node.FloatValues[1] > 0)
                settings.speed = node.FloatValues[1];
            var asset = CreateInstance<SkillProjectileSnap>();
            asset.settings = settings;
            string uniquePath = AssetDatabase.GenerateUniqueAssetPath(path);
            AssetDatabase.CreateAsset(asset, uniquePath);
            if (!AssetDatabase.Contains(asset))
            {
                DestroyImmediate(asset);
                return;
            }
            Change(() =>
            {
                SetProjectileSnap(node, asset, impactNode);
                node.ClearProjectileOverrides();
            });
        }

        private static void DrawTrajectoryValue(string label, string value, string help)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, value);
                SkillFieldHelp.Button(label, help);
            }
        }

        internal static void DrawProjectileSettings(SkillProjectile settings,
            System.Action<ProjectileField, System.Action<SkillProjectile>> drawField = null, SkillProjectile effective = null)
        {
            void Row(ProjectileField field, System.Action<SkillProjectile> draw)
            {
                if (drawField == null) draw(settings);
                else drawField(field, draw);
            }

            Row(ProjectileField.Prefab, shot => shot.prefab = PrefabField("飞行特效 Prefab", shot.prefab, "飞行中的投射物外观。拖入飞剑、法球等预制体；飞行速度和命中范围分别由下方设置控制。"));
            Row(ProjectileField.Trail, shot => shot.trail = PrefabField("拖尾特效 Prefab", shot.trail, "投射物飞行时跟随的尾迹预制体。留空会省略尾迹。"));
            Row(ProjectileField.TrailSeconds, shot => shot.trailSeconds = Mathf.Max(0, SkillFieldHelp.FloatField("拖尾消散（秒）", shot.trailSeconds, "飞行结束后停止发射尾迹粒子，并在这里指定的秒数后移除拖尾对象。")));
            Row(ProjectileField.LaunchBone, shot => shot.launchBone = SkillFieldHelp.TextField("发射挂点", shot.launchBone ?? "", "在施法者模型中查找这个名字的骨骼或子物体，将其作为投射物起点。留空时使用角色根部。"));
            Row(ProjectileField.LaunchOffset, shot => shot.launchOffset = SkillFieldHelp.Vector3Field("发射偏移（角色局部）", shot.launchOffset, "在发射挂点上，按施法者自身朝向移动起点。X、Y、Z 分别对应角色的右方、上方、前方，单位为米。"));
            Row(ProjectileField.TargetBone, shot => shot.targetBone = SkillFieldHelp.TextField("目标挂点", shot.targetBone ?? "", "在被瞄准角色的模型中查找这个名字的骨骼或子物体，将其作为飞行终点的参考位置。"));
            Row(ProjectileField.ImpactOffset, shot => shot.impactOffset = SkillFieldHelp.Vector3Field("目标偏移（角色局部）", shot.impactOffset, "从目标挂点出发，按目标自身朝向偏移飞行终点。X、Y、Z 分别对应右、上、前，单位为米。"));
            Row(ProjectileField.CastTargetIndex, shot => shot.castTargetIndex = Mathf.Max(0, SkillFieldHelp.DelayedIntField("瞄准名单序号", shot.castTargetIndex, "填 0 使用节点接入的目标。填 1、2 等会瞄准本次施法名单中对应的角色，缺少该角色时跳过这一发。")));
            Row(ProjectileField.Trajectory, shot => shot.trajectory = (SkillProjectile.TrajectoryType)SkillFieldHelp.Popup("位移轨迹", (int)shot.trajectory, new[] { "直线", "公式曲线（正弦）", "路径点", "从发射点向目标取点" }, "直线、正弦曲线和路径点可分别配置瞄准方式和进度。从发射点向目标取点会固定本段的起点，每帧读取目标挂点。飞行位移等于（初速度 + 加速度 × 已飞秒数）× 已飞秒数。转向下一目标时，从接触点重新起算。"));
            bool launchToTarget = (effective ?? settings).UsesLaunchDisplacement;
            if (launchToTarget)
                DrawTrajectoryValue("瞄准方式", "持续瞄准目标", "此轨迹每帧使用目标挂点的当前位置。");
            else
                Row(ProjectileField.Path, shot => shot.path = (SkillProjectile.FlightPath)SkillFieldHelp.Popup("瞄准方式", (int)shot.path, new[] { "出手时锁点", "持续瞄准目标" }, "锁点会记录出手时的位置。持续瞄准会在飞行中读取目标挂点的位置，两种方式都能搭配任意轨迹。"));
            if (settings.trajectory == SkillProjectile.TrajectoryType.Sine || effective?.trajectory == SkillProjectile.TrajectoryType.Sine)
            {
                Row(ProjectileField.SineAmplitude, shot => shot.sineAmplitude = SkillFieldHelp.Vector3Field("公式振幅（米）", shot.sineAmplitude, "沿右、上、前三个方向偏移的最大幅度。只想向上拱起时填 (0, 1, 0)。"));
                Row(ProjectileField.SineHalfWaves, shot => shot.sineHalfWaves = Mathf.Clamp(SkillFieldHelp.DelayedIntField("半波数", shot.sineHalfWaves, "1 表示单侧拱起，2 表示先向一侧、再向另一侧。范围 1 至 16。"), 1, 16));
            }
            if (settings.trajectory == SkillProjectile.TrajectoryType.Waypoints || effective?.trajectory == SkillProjectile.TrajectoryType.Waypoints)
                Row(ProjectileField.PathPoints, DrawPathPoints);
            if (launchToTarget)
                DrawTrajectoryValue("飞行进度", "从发射点按位移取点", "位移 = (初速度 + 加速度 × 已飞秒数) × 已飞秒数，沿当前起点到目标的方向取点。");
            else
                Row(ProjectileField.ProgressMode, shot => shot.progressMode = (SkillProjectile.ProgressMode)SkillFieldHelp.Popup("飞行进度", (int)shot.progressMode, new[] { "按初速度和加速度", "指定飞行时长" }, "直线追踪按速度从当前位置继续飞。曲线按出手时起终点的直线距离换算进度。指定飞行时长会在设定的秒数内走完全程。"));
            if (!launchToTarget && (settings.progressMode == SkillProjectile.ProgressMode.Duration || effective?.progressMode == SkillProjectile.ProgressMode.Duration))
                Row(ProjectileField.FlightSeconds, shot => shot.flightSeconds = Mathf.Max(.001f, SkillFieldHelp.FloatField("飞行时长（秒）", shot.flightSeconds, "从这一发出手开始计时，每发独立。路径点进度 50% 对应固定时长的一半。")));
            if (launchToTarget || settings.progressMode != SkillProjectile.ProgressMode.Duration || (effective != null && effective.progressMode != SkillProjectile.ProgressMode.Duration))
            {
                Row(ProjectileField.Speed, shot => shot.speed = Mathf.Max(.01f, SkillFieldHelp.FloatField("初速度（米/秒）", shot.speed, "设置投射物刚发射时每秒飞行的距离。例如填 10，且加速度为 0 时，每秒飞行 10 米。")));
                Row(ProjectileField.Acceleration, shot => shot.acceleration = SkillFieldHelp.FloatField("加速度（米/秒²）", shot.acceleration, "逐步推进时，每秒按此值改变速度。从发射点按位移取点时，此值是位移公式中时间平方项的系数。填 0 时匀速飞行。"));
            }
            Row(ProjectileField.TrackingTimeout, shot => shot.trackingTimeout = Mathf.Max(.01f, SkillFieldHelp.FloatField("最长追踪（秒）", shot.trackingTimeout, "追踪移动目标时最多飞行多久。超过这段时间便结束追踪。")));
            Row(ProjectileField.HitRecipients, shot => shot.hitRecipients = (SkillProjectile.HitRecipients)SkillFieldHelp.Popup("可命中对象", (int)shot.hitRecipients,
                new[] { "确定目标 · 仅瞄准实体", "非确定目标 · 首个符合标签的受击体" }, "「仅瞄准实体」只检测本次目标。「首个符合标签的受击体」会在飞行路径上查找匹配任一指定标签的实体。"));
            Row(ProjectileField.HitTags, DrawProjectileHitTags);
            Row(ProjectileField.HitBoxSize, shot => shot.hitBoxSize = Vector3.Max(Vector3.one * .01f, SkillFieldHelp.Vector3Field("命中盒尺寸（米）", shot.hitBoxSize, "投射物检测受击体所用盒子的完整宽、高、深，对应 X、Y、Z。增大数值会扩大飞行路径上的接触范围。")));
            Row(ProjectileField.HitBoxStart, shot => shot.hitBoxStart = Mathf.Max(0, SkillFieldHelp.FloatField("命中盒开启（发射后秒数）", shot.hitBoxStart, "从投射物发射开始，延迟多久才允许检测接触。填 0 会从发射位置开始检测。")));
            Row(ProjectileField.HitBoxSeconds, shot => shot.hitBoxSeconds = Mathf.Max(0, SkillFieldHelp.FloatField("开启时长（0 至飞行结束）", shot.hitBoxSeconds, "从命中盒开启开始，可以检测接触的持续秒数。填 0 会一直检测到飞行结束。")));
            Row(ProjectileField.HitBoxOffset, shot => shot.hitBoxOffset = SkillFieldHelp.Vector3Field("命中盒偏移（飞行局部）", shot.hitBoxOffset, "相对于投射物的位置移动命中盒中心。Z 沿飞行方向，X 和 Y 分别向右、向上，单位为米。"));
            Row(ProjectileField.HitLayers, shot =>
            {
                int mask = UnityEditorInternal.InternalEditorUtility.LayerMaskToConcatenatedLayersMask(shot.hitLayers);
                int picked = SkillFieldHelp.MaskField("受击层", mask, UnityEditorInternal.InternalEditorUtility.layers, "选择参与接触检测的 Unity 物理层。");
                if (picked != mask) shot.hitLayers = UnityEditorInternal.InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(picked);
            });
            Row(ProjectileField.Impact, shot => shot.impact = PrefabField("命中特效 Prefab", shot.impact, "投射物接触有效受击体时，在接触点生成的特效预制体。"));
            Row(ProjectileField.ImpactScale, shot => shot.impactScale = Mathf.Max(0, SkillFieldHelp.FloatField("命中特效缩放", shot.impactScale, "按比例缩放命中特效的外观。1 为原尺寸，2 为两倍；碰撞范围由命中盒尺寸控制。")));
        }

        private static void DrawPathPoints(SkillProjectile shot)
        {
            using (new SkillFieldHelp.Row("路径点", "进度按全程百分比填写。偏移单位为米，沿起点看向瞄准点，X 向右、Y 向上、Z 向前。两个端点自动归零，各点之间平滑过渡。"))
                EditorGUILayout.LabelField("路径点", EditorStyles.boldLabel);
            for (int i = 0; i < shot.pathPoints.Count; i++)
            {
                var point = shot.pathPoints[i];
                float lower = i == 0 ? .01f : shot.pathPoints[i - 1].progress * 100 + .01f;
                float upper = i + 1 == shot.pathPoints.Count ? 99.99f : shot.pathPoints[i + 1].progress * 100 - .01f;
                using (new EditorGUILayout.HorizontalScope())
                {
                    point.progress = Mathf.Clamp(SkillFieldHelp.FloatField("全程进度（%）", point.progress * 100, "这个点位于整条轨迹的百分之几。0% 是起点，100% 是瞄准点。"), lower, upper) / 100;
                    if (GUILayout.Button("移除", GUILayout.Width(48)))
                    {
                        shot.pathPoints.RemoveAt(i--);
                        GUI.changed = true;
                        continue;
                    }
                }
                point.offset = SkillFieldHelp.Vector3Field("相对偏移（米）", point.offset, "相对于该进度的直线位置向右、向上、向前偏移多少米。负 X 会飞向左侧。");
                shot.pathPoints[i] = point;
            }
            if (GUILayout.Button("添加路径点"))
            {
                // Insert in the widest free interval, including the gap before the first point.
                float widest = 0, previous = 0, midpoint = .5f;
                int insert = 0;
                for (int i = 0; i <= shot.pathPoints.Count; i++)
                {
                    float next = i == shot.pathPoints.Count ? 1 : shot.pathPoints[i].progress;
                    if (next - previous > widest)
                    {
                        widest = next - previous;
                        midpoint = (previous + next) * .5f;
                        insert = i;
                    }
                    previous = next;
                }
                if (widest >= .0002f) shot.pathPoints.Insert(insert, new ProjectilePathPoint { progress = midpoint });
                GUI.changed = true;
            }
        }

        private static void DrawProjectileHitTags(SkillProjectile shot)
        {
            using (new SkillFieldHelp.Row("可命中 MCC 实体标签", "「首个符合标签的受击体」模式使用此列表，请至少填写一个受击方标签。飞行时选择路径上第一个匹配任一标签的受击体。"))
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
            if (shot.hitRecipients == SkillProjectile.HitRecipients.FirstMatchingEntity && !shot.hitTags.Any(tag => !string.IsNullOrWhiteSpace(tag)))
                EditorGUILayout.HelpBox("请填写受击方的 MCC 实体标签。预览场中，敌方为 hostile，主角为 friendly。", MessageType.Info);
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
            EditorGUILayout.LabelField("事件动作   " + HitEventSource(node), EditorStyles.boldLabel);
            if (node.ClassName == "RedirectProjectile")
                EditorGUILayout.LabelField("当前投射物实际命中此次转向选定的目标时，执行以下动作。「本次目标」就是该受击者。", EditorStyles.wordWrappedMiniLabel);
            if (node.outputPort.Count == 0 || node.outputPort[0].uid.Count == 0)
                EditorGUILayout.LabelField("此事件尚未添加后续动作。");
            else
                foreach (var link in node.outputPort[0].uid)
                {
                    string uid = link.Split('#')[0];
                    var child = graph.RootSet.Actions.Find(a => a.Uid == uid);
                    if (child == null) { EditorGUILayout.LabelField("执行分支 " + uid); continue; }
                    string label = HitActionLabel(child);
                    if (child.ClassName == "FormulaImpact") label += "  ·  " + CalculationSource(child);
                    if (GUILayout.Button("编辑 " + label)) { selection = uid; Repaint(); }
                }
            if (node.ClassName == "SelectTarget")
            {
                if (GUILayout.Button("添加找到目标后执行的动作")) ShowHitActionMenu(node);
                return;
            }
            if (GUILayout.Button("添加命中后执行的动作")) ShowHitActionMenu(node);
            string buffHelp = node.EmitsSettledHit ? "每段成功结算后对实际受击者施加状态，未通过命中判定的段会跳过。" :
                "有效接触后，对实际碰到的实体执行状态动作。若 Buff 依赖伤害图的命中率判定，请将状态动作放在该图的判定成功分支。";
            if (GUILayout.Button(new GUIContent("添加命中后施加 Buff", buffHelp)))
                Change(() => selection = AddConfiguredAction(graph.RootSet, "OverTimeEffect", 0, 0, node).Uid);
        }

        private void ShowHitActionMenu(Action source)
        {
            var menu = new GenericMenu();
            AddActionChoices(menu, (key, stage) =>
                Change(() => selection = AddConfiguredAction(graph.RootSet, key, stage, 0, source).Uid),
                CanContinueProjectile(graph.RootSet, source));
            menu.ShowAsContext();
        }

        private static bool CanContinueProjectile(Root root, Action source)
        {
            var sources = ExecutionSources(root);
            var pending = new Stack<NodeBase>();
            var seen = new HashSet<NodeBase>();
            pending.Push(source);
            while (pending.Count > 0)
            {
                var node = pending.Pop();
                if (!seen.Add(node)) continue;
                if (node is Action action && (action.ClassName == "RedirectProjectile" || SkillProjectile.IsLaunch(action) || SkillProjectile.IsImpactLaunch(action))) return true;
                if (!(node is Action { ClassName: "SelectTarget" } || node is Branch { Type: BranchType.Action })) continue;
                for (int port = 0; port < node.inputPort.Count; port++)
                    foreach (var owner in sources[node.Uid + "#" + port]) pending.Push(owner);
            }
            return false;
        }

        private void DrawVisualAction(Action node)
        {
            int stage = node.IntValues.ElementAtOrDefault(0);
            if (stage != 1 && stage != 2 && stage != 6) return;
            var fx = OriginalSkillVisualAction.EffectFor(node, graph as OriginalSkillGraph)?.Copy() ?? new OriginalSkillGraph.EffectCue();
            EditorGUI.BeginChangeCheck();
            fx.prefab = PrefabField("特效 Prefab", fx.prefab, "这个动作播放的特效预制体。比如剑雨落地的粒子效果；落在哪里由「挂接对象」和「挂点」决定。");
            fx.onTarget = SkillFieldHelp.Popup("挂接对象", fx.onTarget ? 1 : 0, new[] { "施法者", "目标" }, "决定去哪个角色身上找挂点。选「施法者」，从出招角色取位置；选「目标」，从表现目标取位置。") == 1;
            fx.bone = SkillFieldHelp.TextField("挂点", fx.bone ?? "", "填写模型中骨骼或子物体的名字，例如 vfx_p_hit。特效从这个位置出现。留空时使用角色物体的位置。");
            fx.followBone = SkillFieldHelp.Toggle("跟随位置", fx.followBone, "开启后，特效跟着挂点移动，延迟判定也取挂点当时的位置。关闭后，特效和判定中心使用特效出现时记录的位置。");
            fx.followRotation = SkillFieldHelp.Toggle("使用挂点旋转", fx.followRotation, "开启后用挂点的朝向生成特效，同时跟随位置时还会跟着挂点转动。关闭时使用角色朝向生成特效。");
            string autoDuration = fx.prefab && fx.prefab.TryGetComponent<OriginalEffectLifetime>(out var lifetime)
                ? $"这个预制体的原作回收时长为 {lifetime.seconds:0.###} 秒。"
                : "工作台会按粒子系统的启动延迟、发射时长和粒子寿命估算回收时间。";
            fx.duration = Mathf.Max(0, SkillFieldHelp.FloatField("显示时长（0 自动）", fx.duration, "填写正数可指定特效显示的秒数。填 0 时，" + autoDuration + "命中判定有独立的延迟设置。"));
            if (stage == 6) fx.triggerHit = SkillFieldHelp.Toggle("触发目标命中", fx.triggerHit, "开启后，这道轨道特效会检查表现目标的碰撞器是否与判定盒重叠。重叠时执行本节点「命中后执行」端口连接的动作。关闭后只播放表现。");
            if (stage == 6 && fx.triggerHit)
            {
                fx.hitDelay = Mathf.Max(0, SkillFieldHelp.FloatField("判定延迟（秒）", fx.hitDelay, "从这次轨道特效出现开始，等多少秒检查一次目标。填 0 会立即检查。例如特效在 1.003 秒出现，延迟 0.2 秒，约在 1.203 秒检查。"));
                fx.hitBoxSize = Vector3.Max(Vector3.one * .01f, SkillFieldHelp.Vector3Field("判定盒尺寸（米）", fx.hitBoxSize, "轨道特效检查目标时使用的盒子大小。X、Y、Z 分别是沿判定盒自身方向的完整宽、高、深。比如三个值都填 1.3，就是各边长 1.3 米的盒子。调整它只改变命中范围。"));
            }
            if (EditorGUI.EndChangeCheck()) Change(() => node.visualEffect = fx);
        }
    }

    [CustomEditor(typeof(SkillProjectileSnap))]
    internal sealed class SkillProjectileConfigEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var config = (SkillProjectileSnap)target;
            var shot = config.settings?.Copy() ?? new SkillProjectile();
            EditorGUI.BeginChangeCheck();
            SkillWorkbench.DrawProjectileSettings(shot);
            if (!EditorGUI.EndChangeCheck()) return;
            Undo.RecordObject(config, "修改投射物配置");
            config.settings = shot;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssetIfDirty(config);
        }
    }
}
