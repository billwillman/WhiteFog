using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SoftKitty
{
    public partial class SkillWorkbench
    {
        [SerializeField] private int page;
        private Vector2 pageScroll;
        [SerializeField] private string buffUid;
        private int buffTemplate, buffOperation, buffRecipient;
        private bool showBuffCreation;
        private string newBuffUid = "", newBuffName = "";
        private static readonly string[] BuffTypes = { "属性增减", "持续伤害", "持续治疗", "吸收护盾", "行为控制" };
        private static readonly string[] BuffOperations = { "施加状态", "驱散一层", "强制移除全部层" };
        private const string Presets = "Assets/SoftKitty/MasterCombatCore/Presets/";
        internal const string RegenPath = "Assets/WhiteFog/SkillTemplates/持续治疗.asset";
        private static readonly string[] BuffPaths = { Presets + "Buff_Berserker.asset", Presets + "DoT_Damage.asset",
            RegenPath, Presets + "GeneralDamageAdvanced.asset", Presets + "DoT_Frost.asset" };
        private static readonly string[] BuffHints = {
            "用临时属性动作按层修正移速。效果图和临时修正各有计时。",
            "每跳读取 damage 与 Layer。灼烧和中毒可以共用同一张图。",
            "每跳回复 = heal × Layer，上限为目标 hp。可为每个状态填写自己的 heal。",
            "原版高级伤害图可查看能量盾结算。多盾额度、优先级和驱散按实例回收需单独设计。",
            "冰霜图先减速，达到阈值后冻结。冻结行为由对应的角色控制器执行。"
        };

        private void DrawBuffPage()
        {
            var db = GameManager.OverTimeEffectData;
            if (!db)
            {
                EditorGUILayout.HelpBox("请先在 Soft Kitty Data Settings 绑定 OverTimeEffectObject 状态库。", MessageType.Info);
                return;
            }
            EditorGUILayout.LabelField("状态库", db.name + "  ·  公共节拍 " + db.OverTimeEffectInterval + " 秒");
            showBuffCreation = EditorGUILayout.Foldout(showBuffCreation || db.overTimeEffects.Count == 0, "从 Buff 模板新建", true);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                if (showBuffCreation) DrawBuffCreation(db);
            }
            if (db.overTimeEffects.Count == 0) return;
            var names = db.overTimeEffects.Select(x => x.name + "  ·  " + x.uid).ToArray();
            int selected = Mathf.Max(0, db.overTimeEffects.FindIndex(x => x.uid == buffUid));
            selected = EditorGUILayout.Popup("选择状态", selected, names);
            var effect = db.overTimeEffects[selected];
            buffUid = effect.uid;
            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                DrawBuffSettings(db, effect);
                DrawBuffBinding(db, effect);
                if (GUILayout.Button("保存状态库")) AssetDatabase.SaveAssetIfDirty(db);
            }
        }

        private void DrawBuffCreation(OverTimeEffectObject db)
        {
            buffTemplate = EditorGUILayout.Popup("制作模板", buffTemplate, BuffTypes);
            EditorGUILayout.HelpBox(BuffHints[buffTemplate], MessageType.Info);
            var preset = AssetDatabase.LoadAssetAtPath<GraphObject>(BuffPaths[buffTemplate]);
            if (!preset) { EditorGUILayout.HelpBox("找不到模板图：" + BuffPaths[buffTemplate], MessageType.Warning); return; }
            if (buffTemplate == 3)
            {
                if (GUILayout.Button("查看 MCC 能量盾结算图")) MasterGraphWindow.OpenWindow(preset);
                return;
            }
            newBuffUid = EditorGUILayout.TextField("新状态 UID", newBuffUid).Trim();
            newBuffName = EditorGUILayout.TextField("显示名", newBuffName).Trim();
            bool duplicate = db.overTimeEffects.Exists(x => x.uid == newBuffUid) || db.IdManager.idToKey.Exists(x => x.key == newBuffUid);
            if (duplicate) EditorGUILayout.HelpBox("这个 UID 已存在，请使用新 UID。", MessageType.Warning);
            using (new EditorGUI.DisabledScope(duplicate || newBuffUid.Length == 0 || newBuffName.Length == 0))
                if (GUILayout.Button("创建状态并引用模板图"))
                {
                    var effect = CreateBuff(db, buffTemplate, newBuffUid, newBuffName, preset);
                    buffUid = effect.uid;
                    showBuffCreation = false;
                    newBuffUid = newBuffName = "";
                    GUI.FocusControl(null);
                }
        }

        internal static OverTimeEffect CreateBuff(OverTimeEffectObject db, int template, string uid, string label, GraphObject preset)
        {
            if (!db || !preset || preset.RootSet == null || template < 0 || template >= BuffTypes.Length || template == 3 ||
                string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(label) ||
                db.overTimeEffects.Exists(e => e.uid == uid) || db.IdManager.idToKey.Exists(k => k.key == uid))
                throw new ArgumentException("状态模板、名称或 UID 无效。");
            Undo.RegisterCompleteObjectUndo(db, "创建 Buff");
            var effect = new OverTimeEffect { uid = uid, name = label, category = BuffTypes[template], duration = 5,
                layered = true, maxLayer = template == 4 ? 3 : 5, DesignGraph = preset };
            foreach (var variable in preset.RootSet.Variables.Where(v => v.Type == VariableType.DynamicVariable))
                if (!string.IsNullOrWhiteSpace(variable.VariableUid) && variable.VariableUid != "Layer" &&
                    !effect.DynamicVariables.Exists(p => p.uid == variable.VariableUid))
                    effect.DynamicVariables.Add(new DynamicFloat { uid = variable.VariableUid, value = variable.Constant });
            db.overTimeEffects.Add(effect);
            db.GetId(uid); // MCC allocates the stable ID when first requested in edit mode.
            MarkBuffDirty(db);
            return effect;
        }

        private static void MarkBuffDirty(OverTimeEffectObject db)
        {
            db.GenerateUniqueHash();
            EditorUtility.SetDirty(db);
        }

        private void DrawBuffSettings(OverTimeEffectObject db, OverTimeEffect effect)
        {
            EditorGUI.BeginChangeCheck();
            string label = EditorGUILayout.DelayedTextField("显示名", effect.name);
            float duration = Mathf.Max(.01f, EditorGUILayout.DelayedFloatField("持续时间（秒）", effect.duration));
            bool layered = EditorGUILayout.Toggle("允许叠层", effect.layered);
            int layers = layered ? Mathf.Max(1, EditorGUILayout.DelayedIntField("层数上限", effect.maxLayer)) : effect.maxLayer;
            bool refresh = EditorGUILayout.Toggle("重复施加时补回时长", effect.canBeRefreshed);
            bool extend = EditorGUILayout.Toggle("重复施加时累加时长", effect.canBeExtended);
            bool dispel = EditorGUILayout.Toggle("允许驱散一层", effect.canBeDispelled);
            var effectGraph = (GraphObject)EditorGUILayout.ObjectField("周期效果图", effect.DesignGraph, typeof(GraphObject), false);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RegisterCompleteObjectUndo(db, "编辑 Buff 定义");
                effect.name = label; effect.duration = duration; effect.layered = layered; effect.maxLayer = layers;
                effect.canBeRefreshed = refresh; effect.canBeExtended = extend; effect.canBeDispelled = dispel;
                effect.DesignGraph = effectGraph;
                MarkBuffDirty(db);
            }
            if (!effectGraph || effectGraph.RootSet == null) { EditorGUILayout.HelpBox("请先绑定有效的周期效果图。", MessageType.Warning); return; }
            foreach (var variable in effectGraph.RootSet.Variables.Where(v => v.Type == VariableType.DynamicVariable)
                .GroupBy(v => v.VariableUid).Select(g => g.First()))
            {
                string key = variable.VariableUid;
                if (string.IsNullOrWhiteSpace(key)) continue;
                if (key == "Layer") { EditorGUILayout.LabelField("Layer", "由 MCC 传入实际层数"); continue; }
                var parameter = effect.DynamicVariables.Find(v => v.uid == key);
                float old = parameter == null ? variable.Constant : parameter.value;
                float value = EditorGUILayout.DelayedFloatField(key + (parameter == null ? "（使用图默认值）" : ""), old);
                if (value == old) continue;
                Undo.RegisterCompleteObjectUndo(db, "编辑状态参数");
                if (parameter == null) effect.DynamicVariables.Add(new DynamicFloat { uid = key, value = value });
                else parameter.value = value;
                MarkBuffDirty(db);
            }
            string users = string.Join("、", db.overTimeEffects.Where(e => e.DesignGraph == effectGraph).Select(e => e.uid));
            EditorGUILayout.LabelField("本库共用此图", users, EditorStyles.wordWrappedLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("打开状态节点树")) MasterGraphWindow.OpenWindow(effectGraph);
                if (GUILayout.Button("查看公式与数值")) { OpenFormulas(effectGraph, true); }
                if (GUILayout.Button("复制成此状态的专用图"))
                {
                    string path = EditorUtility.SaveFilePanelInProject("保存独立效果图", effect.uid + "_Effect", "asset", "选择新图的目录");
                    if (path.Length != 0)
                    {
                        MasterGraphWindow.CloseIfEditing(effectGraph);
                        AssetDatabase.SaveAssetIfDirty(effectGraph);
                        path = AssetDatabase.GenerateUniqueAssetPath(path);
                        if (AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(effectGraph), path))
                        {
                            Undo.RegisterCompleteObjectUndo(db, "引用专用效果图");
                            effect.DesignGraph = AssetDatabase.LoadAssetAtPath<GraphObject>(path);
                            MarkBuffDirty(db);
                        }
                        else ShowNotification(new GUIContent("复制失败，原引用仍然有效"));
                    }
                }
            }
        }

        private void DrawBuffBinding(OverTimeEffectObject db, OverTimeEffect effect)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("挂到技能根节点", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("当前技能", graph ? graph.name : "请先在技能页选一张图");
            cursor = Mathf.Max(0, EditorGUILayout.DelayedFloatField("开始时间（秒）", cursor));
            buffRecipient = EditorGUILayout.Popup("受术者", buffRecipient, new[] { "目标", "施加者" });
            buffOperation = EditorGUILayout.Popup("操作", buffOperation, BuffOperations);
            // Looking up a missing UID through GetId would allocate an ID during repaint.
            var mapping = db.IdManager.idToKey.Find(k => k.key == effect.uid);
            bool validGraph = graph && graph.RootSet != null && graph.RootSet.Uid == "Node0";
            if (mapping == null) EditorGUILayout.HelpBox("原生库中缺少该状态的 ID 映射，请在数据检查器核对。", MessageType.Warning);
            if (validGraph && MasterGraphWindow.IsEditing(graph))
            {
                if (GUILayout.Button("保存节点图并返回工作台编辑")) MasterGraphWindow.CloseIfEditing(graph);
                return;
            }
            using (new EditorGUI.DisabledScope(!validGraph || mapping == null || !(effect.DesignGraph is GraphObject design) || design.RootSet == null))
                if (GUILayout.Button("在游标处添加动作，返回技能页"))
                {
                    Change(() => selection = AddBuffAction(graph.RootSet, mapping.id, buffOperation,
                        buffRecipient == 0 ? EntityType.Target : EntityType.Dealer, cursor).Uid);
                    AssetDatabase.SaveAssetIfDirty(db);
                    AssetDatabase.SaveAssetIfDirty(graph);
                    page = 0;
                }
            EditorGUILayout.LabelField("此按钮添加直接动作。需要接触或命中后触发时，请在相应执行入口或成功分支接线。", EditorStyles.wordWrappedLabel);
        }

        internal static Action AddBuffAction(Root root, int id, int operation, EntityType recipient, float time)
        {
            var action = AddAction(root, "OverTimeEffect", time);
            action.IntValues[0] = operation;
            root.Entities.Last().Type = recipient;
            root.Variables.Last().Constant = id;
            return action;
        }

        private VariableClass BoundBuffId(Action action)
        {
            if (action.inputPort.Count <= 2) return null;
            return graph.RootSet.Variables.Find(v => v.Type == VariableType.Constant && action.inputPort[2].uid.Contains(v.Uid + "#0"));
        }

        private bool DrawBuffChoice(Action action)
        {
            var variable = BoundBuffId(action);
            var db = GameManager.OverTimeEffectData;
            if (variable == null || !db) return false;
            var choices = (from effect in db.overTimeEffects
                           join mapping in db.IdManager.idToKey on effect.uid equals mapping.key
                           select new { mapping.id, label = effect.name + " · " + effect.uid }).ToList();
            int current = choices.FindIndex(x => x.id == Mathf.FloorToInt(variable.Constant));
            if (current < 0) choices.Insert(0, new { id = Mathf.FloorToInt(variable.Constant), label = "未映射 ID " + variable.Constant });
            current = Mathf.Max(current, 0);
            int picked = EditorGUILayout.Popup(variable.outputPort[0].uid.Count > 1 ? "状态（共用数值节点）" : "状态",
                current, choices.Select(x => x.label).ToArray());
            if (picked != current) Change(() => variable.Constant = choices[picked].id);
            return true;
        }

        private void DrawReferencedBuff(Action action)
        {
            var variable = BoundBuffId(action);
            var db = GameManager.OverTimeEffectData;
            if (!db || variable == null || variable.Type != VariableType.Constant) return;
            var mapping = db.IdManager.idToKey.Find(k => k.id == Mathf.FloorToInt(variable.Constant));
            var effect = mapping == null ? null : db.overTimeEffects.Find(e => e.uid == mapping.key);
            if (effect == null) { EditorGUILayout.HelpBox("当前状态 ID 没有对应定义。", MessageType.Warning); return; }
            if (GUILayout.Button("查看 Buff：" + effect.name + " · " + effect.uid)) { buffUid = effect.uid; page = 1; }
        }
    }
}
