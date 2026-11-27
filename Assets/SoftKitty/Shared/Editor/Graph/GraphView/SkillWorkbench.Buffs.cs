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
        [SerializeField] private string selectedBuffUid;
        private int buffTemplate;
        private bool showBuffCreation;
        private string newBuffUid = "", newBuffName = "";
        private static readonly string[] BuffTypes = { "属性增减", "持续伤害", "持续治疗", "吸收护盾", "行为控制" };
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
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("状态库", db.name + "  ·  公共节拍 " + db.OverTimeEffectInterval + " 秒");
                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                    if (GUILayout.Button("保存状态库", EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                        AssetDatabase.SaveAssetIfDirty(db);
            }
            showBuffCreation = EditorGUILayout.Foldout(showBuffCreation || db.overTimeEffects.Count == 0, "从 Buff 模板新建", true);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                if (showBuffCreation) DrawBuffCreation(db);
            }
            if (db.overTimeEffects.Count == 0)
            {
                selectedBuffUid = null;
                return;
            }
            var names = db.overTimeEffects.Select(x => x.name + "  ·  " + x.uid).Prepend("请选择状态").ToArray();
            int selected = string.IsNullOrEmpty(selectedBuffUid) ? 0 : db.overTimeEffects.FindIndex(x => x.uid == selectedBuffUid) + 1;
            selected = EditorGUILayout.Popup("选择状态", selected, names);
            if (selected == 0)
            {
                selectedBuffUid = null;
                return;
            }
            var effect = db.overTimeEffects[selected - 1];
            selectedBuffUid = effect.uid;
            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                DrawBuffSettings(db, effect);
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
                    selectedBuffUid = effect.uid;
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
            bool interruptOnEnter = EditorGUILayout.Toggle("进入状态时打断施法", effect.interruptCastOnEnter);
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
                effect.interruptCastOnEnter = interruptOnEnter;
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
            if (current < 0) choices.Insert(0, new { id = Mathf.FloorToInt(variable.Constant),
                label = variable.Constant == -1 ? "请选择状态" : "未映射 ID " + variable.Constant });
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
            if (GUILayout.Button("查看 Buff：" + effect.name + " · " + effect.uid)) { selectedBuffUid = effect.uid; page = 1; }
        }
    }
}
