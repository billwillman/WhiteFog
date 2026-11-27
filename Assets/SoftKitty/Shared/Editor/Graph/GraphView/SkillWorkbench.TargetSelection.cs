using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace SoftKitty
{
    public partial class SkillWorkbench
    {
        private void DrawTargetSelection(Action action)
        {
            EditorGUILayout.LabelField("选目标规则", EditorStyles.boldLabel);
            var rules = action.selectionRules;
            if (rules == null)
            {
                if (GUILayout.Button("配置选择目标")) Change(() => action.selectionRules = new SkillTargetSelection());
                return;
            }
            int limit = Mathf.Max(1, SkillFieldHelp.DelayedIntField("最多获取目标数", rules.Limit,
                "候选目标须满足全部筛选条件。按排序顺序最多取这么多个不同实体，对每个实体执行一次下游动作。「上游选出的目标」读取当前这一人。"));
            if (limit != rules.Limit) Change(() => rules.maxTargets = limit);
            var pool = SkillFieldHelp.Popup("候选范围", rules.sceneEnemies ? 1 : 0,
                new[] { "本次施法目标名单", "当前场景敌方实体" },
                "当前场景用施法者的 friendly / hostile 标签识别对方。投射物接触后距离从接触点计算，其他情况从施法者计算。");
            if ((pool == 1) != rules.sceneEnemies) Change(() => rules.sceneEnemies = pool == 1);
            using (var asset = new SerializedObject(graph))
            {
                var actions = asset.FindProperty("RootSet.Actions");
                var config = actions.GetArrayElementAtIndex(graph.RootSet.Actions.IndexOf(action)).FindPropertyRelative("selectionRules");
                DrawRuleList("筛选条件 · 全部满足（AND）", rules.filters, config.FindPropertyRelative("filters"), false);
                DrawRuleList("排序优先级 · 并列时看下一项", rules.priorities, config.FindPropertyRelative("priorities"), true);
                asset.ApplyModifiedProperties();
            }
            EditorGUILayout.LabelField("无候选目标时，本分支结束。", EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawRuleList<T>(string heading, List<T> list, SerializedProperty property, bool ordered) where T : TargetRule
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(heading, EditorStyles.boldLabel);
                SkillFieldHelp.Button(heading, ordered ? "先比较第一项，仅在相等时比较下一项。全部并列时按战斗实体标识决定。" :
                    "候选者必须通过这里的每个条件。空列表允许全部候选。只有实际接触到的实体才会计入投射物命中记录。");
            }
            for (int i = 0; i < list.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField((ordered ? (i + 1) + ". " : "") + (list[i]?.Label ?? "缺失规则类型"));
                    if (ordered && i > 0 && GUILayout.Button("上移", GUILayout.Width(42)))
                    {
                        int row = i;
                        Change(() => { var item = list[row]; list.RemoveAt(row); list.Insert(row - 1, item); });
                        GUIUtility.ExitGUI();
                    }
                    if (GUILayout.Button("移除", GUILayout.Width(44)))
                    {
                        int row = i;
                        Change(() => list.RemoveAt(row));
                        GUIUtility.ExitGUI();
                    }
                }
                var field = property.GetArrayElementAtIndex(i).Copy();
                var end = field.GetEndProperty();
                if (field.NextVisible(true))
                    while (!SerializedProperty.EqualContents(field, end))
                    {
                        var info = list[i]?.GetType().GetField(field.name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        string label = info?.GetCustomAttribute<InspectorNameAttribute>()?.displayName ?? field.displayName;
                        EditorGUILayout.PropertyField(field, new GUIContent(label, field.tooltip), true);
                        if (!field.NextVisible(false)) break;
                    }
            }
            if (GUILayout.Button(ordered ? "添加排序优先级" : "添加 AND 条件"))
            {
                var menu = new GenericMenu();
                foreach (var type in TypeCache.GetTypesDerivedFrom<T>().Where(t => !t.IsAbstract && t.IsSerializable && t.GetConstructor(Type.EmptyTypes) != null)
                             .OrderBy(t => t.Name, StringComparer.Ordinal))
                {
                    var item = (T)Activator.CreateInstance(type);
                    menu.AddItem(new GUIContent(item.Label), false, () => Change(() => list.Add(item)));
                }
                menu.ShowAsContext();
            }
        }
    }
}
