using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Kind = SoftKitty.SkillDamageFlow.Kind;
using Stage = SoftKitty.SkillDamageFlow.Stage;

namespace SoftKitty
{
    public partial class SkillWorkbench
    {
        private SkillDamageFlow editingDamageFlow;
        private string editingStageCondition;
        private const string FormulaDirectory = "Assets/WhiteFog/Skills/攻击公式/";

        private static string StageLabel(Kind kind)
        {
            switch (kind)
            {
                case Kind.BaseDamage: return "基础伤害";
                case Kind.ReportedResult: return "历史战报回放";
                case Kind.SegmentShare: return "分段占比";
                case Kind.HitChance: return "命中判定";
                case Kind.ModifyDamage: return "数值修正";
                case Kind.Vulnerability: return "易伤";
                case Kind.Shield: return "护盾吸收";
                case Kind.ApplyHealth: return "扣减生命";
                default: return kind.ToString();
            }
        }

        private static bool UsesStageFormula(Kind kind)
            => kind == Kind.BaseDamage || kind == Kind.HitChance || kind == Kind.ModifyDamage;

        internal static SkillDamageFlow CreateDamageFlowAsset(string path, SkillCalculation config = null)
        {
            var flow = CreateInstance<SkillDamageFlow>();
            InitializeDamageFlow(flow, config);
            AssetDatabase.CreateAsset(flow, AssetDatabase.GenerateUniqueAssetPath(path));
            return flow;
        }

        private static void InitializeDamageFlow(SkillDamageFlow flow, SkillCalculation config)
        {
            SkillFormula Template(string name)
                => AssetDatabase.LoadAssetAtPath<SkillFormula>(FormulaDirectory + name + ".asset");
            flow.stages = new List<Stage>();
            void Add(Kind kind, string id, SkillFormula formula = null, string label = null)
            {
                flow.stages.Add(new Stage { id = id, kind = kind, label = label ?? StageLabel(kind), formula = formula });
            }

            Add(Kind.BaseDamage, "base", config?.damage ? config.damage : Template("通用总伤害"));
            bool hasLegacyFormulas = config != null && (config.damage || config.accuracy || config.settlement);
            if (hasLegacyFormulas) Add(Kind.SegmentShare, "share");
            Add(Kind.HitChance, "accuracy", config?.accuracy ? config.accuracy : Template("通用命中率"));
            if (!hasLegacyFormulas) Add(Kind.SegmentShare, "share");
            Add(Kind.ModifyDamage, "defence", config?.settlement ? config.settlement : Template("通用防御结算"), "目标防御");
            Add(Kind.Vulnerability, "vulnerability");
            Add(Kind.Shield, "shield");
            Add(Kind.ApplyHealth, "health");
        }

        private static void EditDamageFlow(SkillDamageFlow flow, System.Action edit)
        {
            Undo.RegisterCompleteObjectUndo(flow, "编辑本招专用流程");
            edit();
            EditorUtility.SetDirty(flow);
        }

        private void DrawNativeDamageGraph(SkillCalculation config)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var next = (GraphObject)EditorGUILayout.ObjectField("原生伤害图", config.nativeGraph, typeof(GraphObject), false);
                if (next != config.nativeGraph)
                {
                    if (!next || next != graph && !(next is SkillFormula) && !(next is OriginalSkillGraph) && next.RootSet?.ClassName == "Combat")
                        Change(() => config.nativeGraph = next);
                    else ShowNotification(new GUIContent("请选择独立的 MCC Combat 图资产"));
                }
                if (GUILayout.Button("新建图", GUILayout.Width(58)))
                {
                    string path = EditorUtility.SaveFilePanelInProject("新建 MCC 伤害图", graph.name + "_伤害图", "asset", "选择保存位置");
                    if (!string.IsNullOrEmpty(path))
                    {
                        var created = CreateInstance<GraphObject>();
                        created.RootSet = EmptyRoot();
                        AssetDatabase.CreateAsset(created, AssetDatabase.GenerateUniqueAssetPath(path));
                        Change(() => config.nativeGraph = created);
                        MasterGraphWindow.OpenWindow(created);
                        GUIUtility.ExitGUI();
                    }
                }
                using (new EditorGUI.DisabledScope(!config.nativeGraph))
                    if (GUILayout.Button("打开节点图", GUILayout.Width(85)))
                        MasterGraphWindow.OpenWindow(config.nativeGraph);
            }
            var root = config.nativeGraph ? config.nativeGraph.RootSet : null;
            if (root == null) return;
            var inputs = root.GetDynamicFloatList();
            EditorGUILayout.LabelField(inputs.Count == 0 ? "此图未声明动态数值参数。" :
                "参数取自施放上下文或图中默认值。勾选后，本招传入指定数值。", EditorStyles.wordWrappedMiniLabel);
            foreach (var input in inputs)
            {
                string key = input.uid;
                if (string.IsNullOrWhiteSpace(key)) continue;
                if (SkillCalculation.IsNativeParameterReserved(key))
                {
                    EditorGUILayout.LabelField(key, "由本次结算提供");
                    continue;
                }
                var field = config.nativeParameters?.Find(p => p != null && p.key == key);
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool enabled = EditorGUILayout.ToggleLeft(key, field != null, GUILayout.MinWidth(80));
                    if (enabled != (field != null))
                        Change(() =>
                        {
                            if (enabled)
                            {
                                if (config.nativeParameters == null) config.nativeParameters = new List<SkillLevelTable.Field>();
                                config.nativeParameters.Add(new SkillLevelTable.Field { key = key, value = input.value });
                            }
                            else config.nativeParameters.Remove(field);
                        });
                    if (field == null) EditorGUILayout.LabelField("图默认值 " + input.value.ToString("G6"));
                    else
                    {
                        float value = EditorGUILayout.DelayedFloatField(field.value);
                        if (value != field.value && !float.IsNaN(value) && !float.IsInfinity(value))
                            Change(() => field.value = value);
                    }
                }
            }
            if (config.nativeParameters != null)
                foreach (var field in config.nativeParameters.ToArray())
                    if (field != null && inputs.All(input => input.uid != field.key))
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(field.key, "图中未声明 · 覆盖值 " + field.value.ToString("G6"));
                            if (GUILayout.Button("移除", GUILayout.Width(45))) Change(() => config.nativeParameters.Remove(field));
                        }
            try { config.ValidateFlow(); }
            catch (InvalidOperationException e) { EditorGUILayout.HelpBox(e.Message, MessageType.Warning); }
        }

        private void DrawDamageFlow(SkillCalculation config)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var selected = (SkillDamageFlow)EditorGUILayout.ObjectField("流程资产", config.ActiveFlow, typeof(SkillDamageFlow), false);
                if (selected != config.ActiveFlow)
                    Change(() => { config.SetActiveFlow(selected); config.ActiveDisabledStageIds.Clear(); });
                using (new EditorGUI.DisabledScope(!config.ActiveFlow))
                    if (GUILayout.Button("定位资源", GUILayout.Width(76)))
                    {
                        Selection.activeObject = config.ActiveFlow;
                        EditorGUIUtility.PingObject(config.ActiveFlow);
                    }
                if (GUILayout.Button("新建公共流程", GUILayout.Width(98)))
                {
                    string path = EditorUtility.SaveFilePanelInProject("新建共享伤害流程", graph.name + "_伤害流程", "asset", "选择保存位置");
                    if (!string.IsNullOrEmpty(path))
                    {
                        var created = CreateDamageFlowAsset(path);
                        Change(() => { config.SetActiveFlow(created); config.ActiveDisabledStageIds.Clear(); });
                    }
                }
                using (new EditorGUI.DisabledScope(!config.ActiveFlow && !config.UsesLinkedAssets))
                    if (GUILayout.Button("制作本招专用流程", GUILayout.Width(124)))
                    {
                        string path = EditorUtility.SaveFilePanelInProject("保存本招专用流程及公式",
                            graph.name + "_专用流程", "asset", "选择保存位置");
                        if (!string.IsNullOrEmpty(path))
                        {
                            var copy = CreatePrivateDamageFlowAsset(config, path);
                            if (copy)
                            {
                                Change(() => config.SetActiveFlow(copy));
                                editingDamageFlow = copy;
                            }
                        }
                    }
            }
            var flow = config.ActiveFlow;
            if (!flow && !config.UsesLinkedAssets)
            {
                EditorGUILayout.HelpBox("选用公共流程后，可直接勾选本招参与的环节。制作本招专用流程后，可单独编辑环节顺序和公式。", MessageType.Info);
                if (GUILayout.Button("使用项目的通用伤害流程"))
                {
                    var shared = AssetDatabase.LoadAssetAtPath<SkillDamageFlow>(FormulaDirectory + "通用伤害流程.asset");
                    if (shared) Change(() => { config.SetActiveFlow(shared); config.ActiveDisabledStageIds.Clear(); });
                    else ShowNotification(new GUIContent("请在「流程资产」选择工程中的伤害流程"));
                }
                return;
            }
            if (flow && !flow.ownsFormulas)
                EditorGUILayout.HelpBox("公共流程在此处只读。点「定位资源」可在项目窗口选中流程资产。调整本招的流程结构或公式时，请先制作本招专用流程。", MessageType.Info);
            if (flow && (flow.stages == null || flow.stages.Count == 0))
            {
                if (flow.ownsFormulas)
                {
                    if (GUILayout.Button("为此空流程创建完整环节"))
                        EditDamageFlow(flow, () => InitializeDamageFlow(flow, config));
                }
                else EditorGUILayout.HelpBox("此公共流程没有结算环节。请定位资源后在 Inspector 中配置。", MessageType.Warning);
                return;
            }

            if (config.ReadsDamageReport)
            {
                int index = Mathf.Max(1, EditorGUILayout.DelayedIntField("回放逻辑段号", config.logicIndex));
                if (index != config.logicIndex) Change(() => config.logicIndex = index);
                EditorGUILayout.HelpBox("此资产用于重放旧战报。可在上方选择属性伤害流程用于实战。", MessageType.Info);
                return;
            }
            if (!flow)
                EditorGUILayout.HelpBox("当前使用动作里已保存的环节与公式。可以直接选择参与环节，或制作本招专用流程以修改顺序和公式。", MessageType.Info);
            EditorGUILayout.LabelField("本次结算参与的环节", EditorStyles.boldLabel);
            foreach (var stage in config.GetStageEntries())
            {
                if (stage == null) continue;
                bool required = stage.kind == Kind.BaseDamage || stage.kind == Kind.ApplyHealth;
                bool enabled = config.IsStageEnabled(stage.id);
                using (new EditorGUI.DisabledScope(required))
                {
                    string label = required ? stage.label + "（必需）" : stage.label;
                    if (!flow && stage.formula) label += " · " + stage.formula.name;
                    bool next = EditorGUILayout.ToggleLeft(label, enabled);
                    if (next != enabled)
                        Change(() => config.SetStageEnabled(stage.id, next));
                }
            }
            try { config.ValidateFlow(); }
            catch (InvalidOperationException e) { EditorGUILayout.HelpBox(e.Message, MessageType.Warning); }

            if (!flow || !flow.ownsFormulas) return;
            bool expanded;
            using (new SkillFieldHelp.Row("编辑伤害流程", SkillDamageHelp.Stages))
                expanded = EditorGUILayout.Foldout(editingDamageFlow == flow,
                    "编辑本招专用流程 · " + flow.name, true);
            if (expanded) editingDamageFlow = flow;
            else if (editingDamageFlow == flow) editingDamageFlow = null;
            if (!expanded) return;
            for (int index = 0; index < flow.stages.Count; index++)
            {
                var stage = flow.stages[index];
                if (stage == null) continue;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        string label = EditorGUILayout.DelayedTextField((index + 1) + ". " + StageLabel(stage.kind), stage.label);
                        if (!string.IsNullOrWhiteSpace(label) && label != stage.label)
                            EditDamageFlow(flow, () => stage.label = label);
                        using (new EditorGUI.DisabledScope(!CanMoveStage(flow.stages, index, -1)))
                            if (GUILayout.Button("上移", GUILayout.Width(42))) MoveStage(flow, index, -1);
                        using (new EditorGUI.DisabledScope(!CanMoveStage(flow.stages, index, 1)))
                            if (GUILayout.Button("下移", GUILayout.Width(42))) MoveStage(flow, index, 1);
                        using (new EditorGUI.DisabledScope(index == 0 || index == flow.stages.Count - 1))
                            if (GUILayout.Button("移除", GUILayout.Width(42)))
                            {
                                EditDamageFlow(flow, () => flow.stages.RemoveAt(index));
                                GUIUtility.ExitGUI();
                            }
                    }
                    if (UsesStageFormula(stage.kind))
                        DrawFormulaSlot("公式", stage.formula, config.levels, f => stage.formula = f, flow, SkillDamageHelp.Formula(stage.kind));
                    if (stage.kind == Kind.ModifyDamage)
                    {
                        bool showCondition;
                        using (new SkillFieldHelp.Row("触发概率（Condition Chance）", SkillDamageHelp.Condition))
                            showCondition = EditorGUILayout.Foldout(editingStageCondition == stage.id,
                                stage.conditionChance ? "触发概率 · " + stage.conditionChance.name : "触发概率 · 每次执行", true);
                        if (showCondition)
                        {
                            editingStageCondition = stage.id;
                            DrawFormulaSlot("触发概率（0～1）", stage.conditionChance, config.levels, f => stage.conditionChance = f, flow, SkillDamageHelp.Condition);
                            EditorGUILayout.LabelField("概率可以来自角色属性、目标属性或常量。未触发时，沿用上一环节的伤害继续结算。", EditorStyles.wordWrappedMiniLabel);
                            if (stage.conditionChance && GUILayout.Button("清除条件，改为每次执行"))
                                EditDamageFlow(flow, () => stage.conditionChance = null);
                        }
                        else if (editingStageCondition == stage.id) editingStageCondition = null;
                    }
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("添加环节")) ShowAddStageMenu(flow);
                if (GUILayout.Button("保存流程")) AssetDatabase.SaveAssetIfDirty(flow);
            }
        }

        internal static SkillDamageFlow CopyDamageFlowAsset(SkillDamageFlow flow, string path)
        {
            if (flow.ownsFormulas) AssetDatabase.SaveAssetIfDirty(flow);
            return SavePrivateDamageFlow(Instantiate(flow), path);
        }

        internal static SkillDamageFlow CreatePrivateDamageFlowAsset(SkillCalculation config, string path)
        {
            if (config.ActiveFlow) return CopyDamageFlowAsset(config.ActiveFlow, path);
            var copy = CreateInstance<SkillDamageFlow>();
            foreach (var stage in config.GetStageEntries())
                copy.stages.Add(new Stage { id = stage.id, label = stage.label, kind = stage.kind,
                    formula = stage.formula, conditionChance = stage.conditionChance });
            return SavePrivateDamageFlow(copy, path);
        }

        private static SkillDamageFlow SavePrivateDamageFlow(SkillDamageFlow copy, string path)
        {
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            copy.ownsFormulas = true;
            AssetDatabase.CreateAsset(copy, path);
            try
            {
                var formulas = new Dictionary<SkillFormula, SkillFormula>();
                SkillFormula Local(SkillFormula source)
                {
                    if (!source) return null;
                    if (!formulas.TryGetValue(source, out var local))
                    {
                        local = EmbedFormulaCopy(copy, source);
                        formulas.Add(source, local);
                    }
                    return local;
                }
                foreach (var stage in copy.stages)
                {
                    stage.formula = Local(stage.formula);
                    stage.conditionChance = Local(stage.conditionChance);
                }
                EditorUtility.SetDirty(copy);
                AssetDatabase.SaveAssetIfDirty(copy);
                return copy;
            }
            catch
            {
                if (AssetDatabase.GetAssetPath(copy) == path) AssetDatabase.DeleteAsset(path);
                throw;
            }
        }

        private static SkillFormula EmbedFormulaCopy(SkillDamageFlow flow, SkillFormula source)
        {
            MasterGraphWindow.CloseIfEditing(source);
            var copy = Instantiate(source);
            copy.name = source.name;
            AssetDatabase.AddObjectToAsset(copy, flow);
            return copy;
        }

        private static bool CanMoveStage(List<Stage> stages, int index, int direction)
        {
            int other = index + direction;
            if (index <= 0 || index >= stages.Count - 1 || other <= 0 || other >= stages.Count - 1) return false;
            var before = stages[direction > 0 ? index : other];
            var after = stages[direction > 0 ? other : index];
            return before != null && after != null &&
                !(before.kind == Kind.HitChance && (after.kind == Kind.Shield || after.kind == Kind.Vulnerability));
        }

        private static void MoveStage(SkillDamageFlow flow, int index, int direction)
        {
            EditDamageFlow(flow, () =>
            {
                var stage = flow.stages[index];
                flow.stages.RemoveAt(index);
                flow.stages.Insert(index + direction, stage);
            });
            GUIUtility.ExitGUI();
        }

        private void ShowAddStageMenu(SkillDamageFlow flow)
        {
            var menu = new GenericMenu();
            void Item(Kind kind, string title, bool showCondition = false)
            {
                if (kind != Kind.ModifyDamage && flow.stages.Any(s => s != null && s.kind == kind))
                {
                    menu.AddDisabledItem(new GUIContent(title));
                    return;
                }
                menu.AddItem(new GUIContent(title), false, () =>
                {
                    int index = flow.stages.FindIndex(s => s != null && (s.kind == Kind.ApplyHealth ||
                        kind == Kind.HitChance && (s.kind == Kind.Vulnerability || s.kind == Kind.Shield)));
                    var added = new Stage { id = Guid.NewGuid().ToString("N"), label = StageLabel(kind), kind = kind };
                    EditDamageFlow(flow, () => flow.stages.Insert(index >= 0 ? index : flow.stages.Count, added));
                    if (showCondition) editingStageCondition = added.id;
                    Repaint();
                });
            }
            Item(Kind.HitChance, StageLabel(Kind.HitChance));
            Item(Kind.SegmentShare, StageLabel(Kind.SegmentShare));
            Item(Kind.ModifyDamage, "数值修正/每次执行");
            Item(Kind.ModifyDamage, "数值修正/概率触发（如暴击、格挡）", true);
            Item(Kind.Vulnerability, StageLabel(Kind.Vulnerability));
            Item(Kind.Shield, StageLabel(Kind.Shield));
            menu.ShowAsContext();
        }
    }
}
