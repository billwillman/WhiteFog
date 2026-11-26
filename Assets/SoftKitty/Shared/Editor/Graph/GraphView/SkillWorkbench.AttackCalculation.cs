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
                if (GUILayout.Button("配置攻击公式")) Change(() => action.calculation = new SkillCalculation());
                return;
            }
            EditorGUILayout.LabelField("施放时取攻击，命中后取目标防御", EditorStyles.boldLabel);
            var table = (SkillLevelTable)EditorGUILayout.ObjectField("技能等级表", config.levels, typeof(SkillLevelTable), false);
            if (table != config.levels) Change(() => config.levels = table);
            if (table && table.rows.Count > 0)
            {
                var levels = table.rows.Select(row => row.level).Distinct().OrderBy(level => level).ToArray();
                if (!levels.Contains(formulaSampleLevel)) formulaSampleLevel = levels[0];
                formulaSampleLevel = EditorGUILayout.IntPopup("公式试算等级", formulaSampleLevel,
                    levels.Select(level => level + " 级").ToArray(), levels);
            }
            DrawFormulaSlot("整招总伤害", config.damage, f => config.damage = f);
            DrawFormulaSlot("逐段命中率（0～1）", config.accuracy, f => config.accuracy = f);
            DrawFormulaSlot("命中后伤害", config.settlement, f => config.settlement = f);

            int count = FormulaImpactAction.SegmentCount(action);
            if (GUILayout.Button("按当前 " + count + " 段均分 100%"))
                Change(() => config.segmentPercent = Enumerable.Repeat(100f / count, count).ToList());
            for (int i = 0; i < config.segmentPercent.Count; i++)
            {
                int segment = i;
                float old = config.segmentPercent[i];
                float next = EditorGUILayout.DelayedFloatField("第 " + (i + 1) + " 段占比（%）", old);
                if (next != old) Change(() => config.segmentPercent[segment] = next);
            }
            try { config.ValidateShares(count); }
            catch (InvalidOperationException e) { EditorGUILayout.HelpBox(e.Message, MessageType.Warning); }
        }

        private void DrawFormulaSlot(string label, SkillFormula current, System.Action<SkillFormula> assign)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var next = (SkillFormula)EditorGUILayout.ObjectField(label, current, typeof(SkillFormula), false);
                if (next != current) Change(() => assign(next));
                using (new EditorGUI.DisabledScope(!current))
                {
                    if (GUILayout.Button("看公式", GUILayout.Width(56)))
                    {
                        OpenFormulas(current, false);
                        var action = graph.RootSet.Actions.Find(a => a.Uid == selection);
                        var table = action?.calculation?.levels;
                        var row = table ? table.rows.Find(r => r.level == formulaSampleLevel) : null;
                        if (row != null)
                            foreach (var field in row.fields) sampleNumbers["参数/level." + field.key] = field.value;
                    }
                    if (GUILayout.Button("专用副本", GUILayout.Width(68)))
                    {
                        string path = EditorUtility.SaveFilePanelInProject("保存本技能专用公式", graph.name + "_" + current.name, "asset", "选择保存位置");
                        if (string.IsNullOrEmpty(path)) return;
                        MasterGraphWindow.CloseIfEditing(current);
                        AssetDatabase.SaveAssetIfDirty(current);
                        path = AssetDatabase.GenerateUniqueAssetPath(path);
                        if (AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(current), path))
                            Change(() => assign(AssetDatabase.LoadAssetAtPath<SkillFormula>(path)));
                    }
                }
            }
        }
    }
}
