using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SoftKitty
{
    public partial class SkillWorkbench
    {
        private int formulaSampleLevel = 1;
        private static readonly (string key, GUIContent title, float scale)[] LevelColumns =
        {
            ("ratio", new GUIContent("攻击倍率（倍）", "0.55 表示最终攻击的 55%"), 1f),
            ("flat", new GUIContent("固定加值", "计算整招伤害时，加在攻击乘倍率之后的点数"), 1f),
            ("accuracy", new GUIContent("基础命中率（%）", "95 表示 95%"), 100f),
            ("agiFactor", new GUIContent("身法修正（百分点）", "双方每相差 1 点身法，命中率变化的百分点"), 100f)
        };

        private static IEnumerable<SkillLevelTable> BoundLevelTables(GraphObject skill)
        {
            if (!skill || skill.RootSet == null) return Enumerable.Empty<SkillLevelTable>();
            return skill.RootSet.Actions.Where(a => a.ClassName == "FormulaImpact")
                .Select(a => a.calculation?.levels).Where(table => table).Distinct();
        }

        private static void SaveSkillAndLevels(GraphObject skill)
        {
            foreach (var table in BoundLevelTables(skill)) AssetDatabase.SaveAssetIfDirty(table);
            if (skill && skill.RootSet != null)
                foreach (var flow in skill.RootSet.Actions.Select(a => a.calculation?.ActiveFlow).Where(f => f).Distinct())
                    AssetDatabase.SaveAssetIfDirty(flow);
            AssetDatabase.SaveAssetIfDirty(skill);
        }

        private void DrawLevelTable(SkillLevelTable table)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(new GUIContent("技能等级表 · " + table.name, AssetDatabase.GetAssetPath(table)), EditorStyles.boldLabel);
                    if (GUILayout.Button("新增一级", EditorStyles.miniButton, GUILayout.Width(68))) AddLevel(table);
                }

                var keys = table.rows.SelectMany(row => row.fields).Select(field => field.key)
                    .Where(key => !string.IsNullOrWhiteSpace(key)).Distinct().ToList();
                var columns = LevelColumns.Where(col => keys.Contains(col.key)).ToList();
                foreach (string key in keys)
                    if (!columns.Exists(col => col.key == key)) columns.Add((key, new GUIContent(key), 1f));

                const float levelWidth = 48f;
                Rect RowRect() => EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight,
                    GUILayout.MinWidth(levelWidth + columns.Count * 105f));
                Rect Cell(Rect row, int index)
                {
                    if (index < 0) return new Rect(row.x, row.y, levelWidth, row.height);
                    float width = (row.width - levelWidth) / columns.Count;
                    return new Rect(row.x + levelWidth + index * width, row.y, width - 5f, row.height);
                }

                var header = RowRect();
                EditorGUI.LabelField(Cell(header, -1), "等级", EditorStyles.miniBoldLabel);
                for (int i = 0; i < columns.Count; i++)
                    EditorGUI.LabelField(Cell(header, i), columns[i].title, EditorStyles.miniBoldLabel);

                foreach (var level in table.rows.OrderBy(row => row.level))
                {
                    var row = RowRect();
                    EditorGUI.LabelField(Cell(row, -1), level.level + " 级");
                    for (int i = 0; i < columns.Count; i++)
                    {
                        var col = columns[i];
                        var field = level.fields.Find(f => f.key == col.key);
                        var rect = Cell(row, i);
                        if (field == null)
                        {
                            if (GUI.Button(rect, "补值", EditorStyles.miniButton))
                            {
                                Undo.RegisterCompleteObjectUndo(table, "补充等级数值");
                                level.fields.Add(new SkillLevelTable.Field { key = col.key });
                                EditorUtility.SetDirty(table);
                            }
                            continue;
                        }
                        EditorGUI.BeginChangeCheck();
                        float input = EditorGUI.FloatField(rect, field.value * col.scale);
                        if (EditorGUI.EndChangeCheck() && !float.IsNaN(input) && !float.IsInfinity(input))
                        {
                            Undo.RecordObject(table, "修改技能等级数值");
                            field.value = input / col.scale;
                            EditorUtility.SetDirty(table);
                        }
                    }
                }
            }
        }

        private static void AddLevel(SkillLevelTable table)
        {
            var last = table.rows.OrderBy(row => row.level).LastOrDefault();
            var next = new SkillLevelTable.Row();
            if (last != null)
            {
                next.level = last.level + 1;
                foreach (var field in last.fields)
                    next.fields.Add(new SkillLevelTable.Field { key = field.key, value = field.value });
            }
            else
            {
                foreach (var col in LevelColumns)
                    next.fields.Add(new SkillLevelTable.Field { key = col.key, value = col.key == "ratio" || col.key == "accuracy" ? 1f : 0f });
            }
            Undo.RegisterCompleteObjectUndo(table, "新增技能等级");
            table.rows.Add(next);
            EditorUtility.SetDirty(table);
        }

        private void DrawAttackCalculation(Action action)
        {
            var config = action.calculation;
            if (config == null)
            {
                if (GUILayout.Button("配置伤害结算")) Change(() => action.calculation = new SkillCalculation { mode = SkillCalculation.Mode.SharedFlow });
                return;
            }
            EditorGUILayout.LabelField("攻击公式与技能等级", EditorStyles.boldLabel);
            bool wasNative = config.EffectiveMode == SkillCalculation.Mode.NativeGraph;
            bool native = EditorGUILayout.Popup("计算方式", wasNative ? 1 : 0, new[] { "技能伤害流程", "MCC 原生伤害图" }) == 1;
            if (native != wasNative)
                Change(() => config.SetMode(native ? SkillCalculation.Mode.NativeGraph : SkillCalculation.Mode.SharedFlow));
            var table = (SkillLevelTable)EditorGUILayout.ObjectField("技能等级表", config.levels, typeof(SkillLevelTable), false);
            if (table != config.levels) Change(() => config.levels = table);
            if (!config.levels && GUILayout.Button("新建本技能等级表"))
            {
                string path = EditorUtility.SaveFilePanelInProject("新建技能等级表", graph.name + "_等级表", "asset", "选择保存位置");
                if (!string.IsNullOrEmpty(path))
                {
                    table = CreateInstance<SkillLevelTable>();
                    AssetDatabase.CreateAsset(table, AssetDatabase.GenerateUniqueAssetPath(path));
                    AddLevel(table);
                    AssetDatabase.SaveAssetIfDirty(table);
                    Change(() => config.levels = table);
                }
            }
            if (!native && table && table.rows.Count > 0)
            {
                var levels = table.rows.Select(row => row.level).Distinct().OrderBy(level => level).ToArray();
                if (!levels.Contains(formulaSampleLevel)) formulaSampleLevel = levels[0];
                formulaSampleLevel = EditorGUILayout.IntPopup("公式试算等级", formulaSampleLevel,
                    levels.Select(level => level + " 级").ToArray(), levels);
            }
            if (native) DrawNativeDamageGraph(config);
            else DrawDamageFlow(config);
            if (!native && !config.GetStageEntries().Any(s => s != null && s.kind == SkillDamageFlow.Kind.SegmentShare && config.IsStageEnabled(s.id))) return;
            int count = FormulaImpactAction.SegmentCount(action);
            if (native)
                EditorGUILayout.LabelField("每段执行一次原生图。本段序号与占比分别通过 hit.index、hit.share 传给图内节点。", EditorStyles.wordWrappedMiniLabel);
            if (!native)
            {
                bool independent = EditorGUILayout.Toggle("独立命中段倍率", config.independentSegment);
                if (independent != config.independentSegment) Change(() => config.independentSegment = independent);
            }
            if (config.independentSegment)
            {
                int index = Mathf.Max(1, EditorGUILayout.DelayedIntField("起始逻辑段号", config.logicIndex));
                if (index != config.logicIndex) Change(() => config.logicIndex = index);
                bool verified = EditorGUILayout.Toggle("分段倍率已核实", config.segmentRatioVerified);
                if (verified != config.segmentRatioVerified) Change(() => config.segmentRatioVerified = verified);
                EditorGUILayout.LabelField(config.segmentRatioVerified ? "已核实 · 使用下列倍率结算" : "待核 · 实战使用下列配置值", EditorStyles.miniLabel);
                if (config.segmentPercent.Count != count && GUILayout.Button("匹配执行次数，保留已有倍率"))
                    Change(() => config.segmentPercent = Enumerable.Range(0, count)
                        .Select(i => i < config.segmentPercent.Count ? config.segmentPercent[i] : 0f).ToList());
            }
            else if (GUILayout.Button("按当前 " + count + " 段均分 100%"))
                Change(() => config.segmentPercent = Enumerable.Repeat(100f / count, count).ToList());
            for (int i = 0; i < config.segmentPercent.Count; i++)
            {
                int segment = i;
                float old = config.segmentPercent[i];
                string label = config.independentSegment ? "逻辑段 " + (config.logicIndex + i) + " 倍率（%）" : "第 " + (i + 1) + " 段占比（%）";
                float next = EditorGUILayout.DelayedFloatField(label, old);
                if (next != old) Change(() => config.segmentPercent[segment] = next);
            }
            try { config.ValidateShares(count); }
            catch (InvalidOperationException e) { EditorGUILayout.HelpBox(e.Message, MessageType.Warning); }
        }

        internal static SkillFormula CreateFormulaAsset(string path)
        {
            var formula = BlankFormula();
            AssetDatabase.CreateAsset(formula, AssetDatabase.GenerateUniqueAssetPath(path));
            return formula;
        }

        private static SkillFormula BlankFormula()
        {
            var formula = CreateInstance<SkillFormula>();
            formula.RootSet = EmptyRoot();
            var output = new VariableClass(formula.RootSet) { Type = VariableType.Constant };
            formula.RootSet.Variables.Add(output);
            formula.outputUid = output.Uid;
            return formula;
        }

        private void DrawFormulaSlot(string label, SkillFormula current, SkillLevelTable table,
            System.Action<SkillFormula> assign, SkillDamageFlow owner)
        {
            SkillFormula Bind(SkillFormula value)
            {
                if (value && owner.ownsFormulas && AssetDatabase.GetAssetPath(value) != AssetDatabase.GetAssetPath(owner))
                    value = EmbedFormulaCopy(owner, value);
                EditDamageFlow(owner, () => assign(value));
                return value;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                var next = (SkillFormula)EditorGUILayout.ObjectField(label, current, typeof(SkillFormula), false);
                if (next != current) Bind(next);
                if (!current && GUILayout.Button("新建公式", GUILayout.Width(68)))
                {
                    SkillFormula created = null;
                    if (owner.ownsFormulas)
                    {
                        created = BlankFormula();
                        created.name = label;
                        AssetDatabase.AddObjectToAsset(created, owner);
                    }
                    else
                    {
                        string path = EditorUtility.SaveFilePanelInProject("新建数值公式", owner.name + "_公式", "asset", "选择保存位置");
                        if (!string.IsNullOrEmpty(path)) created = CreateFormulaAsset(path);
                    }
                    if (created)
                    {
                        OpenFormulas(Bind(created), false);
                        GUIUtility.ExitGUI();
                    }
                }
                using (new EditorGUI.DisabledScope(!current))
                {
                    if (GUILayout.Button("看公式", GUILayout.Width(56)))
                    {
                        var editable = owner.ownsFormulas && AssetDatabase.GetAssetPath(current) != AssetDatabase.GetAssetPath(owner)
                            ? Bind(current) : current;
                        OpenFormulas(editable, false);
                        var row = table ? table.rows.Find(r => r.level == formulaSampleLevel) : null;
                        if (row != null)
                            foreach (var field in row.fields) sampleNumbers["参数/level." + field.key] = field.value;
                    }
                    if (GUILayout.Button("复制公式", GUILayout.Width(68)))
                    {
                        if (owner.ownsFormulas)
                        {
                            var copy = EmbedFormulaCopy(owner, current);
                            copy.name = current.name + "_副本";
                            Bind(copy);
                            return;
                        }
                        string path = EditorUtility.SaveFilePanelInProject("保存公式副本", owner.name + "_" + current.name, "asset", "选择保存位置");
                        if (string.IsNullOrEmpty(path)) return;
                        MasterGraphWindow.CloseIfEditing(current);
                        AssetDatabase.SaveAssetIfDirty(current);
                        path = AssetDatabase.GenerateUniqueAssetPath(path);
                        var external = Instantiate(current);
                        AssetDatabase.CreateAsset(external, path);
                        Bind(external);
                    }
                }
            }
        }
    }
}
