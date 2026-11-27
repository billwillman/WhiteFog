using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SoftKitty
{
    public partial class SkillWorkbench
    {
        [Serializable]
        private sealed class TrialValue
        {
            public string uid;
            public float value;
        }

        [Serializable]
        private sealed class TrialRole
        {
            public SkillLibraryCharacter selected;
            public string templateUid;
            public bool expanded;
            public List<TrialValue> values = new List<TrialValue>();

            public void UseTemplate(Entity template)
            {
                if (templateUid == template?.uid) return;
                values.Clear();
                templateUid = template?.uid;
            }

            public void Apply(Entity entity)
            {
                if (entity.uid != templateUid) return;
                foreach (var field in values)
                {
                    var entry = entity.Attributes.Find(a => a.uid == field.uid);
                    if (entry != null && !entry.setting.stringValue) entry.SetValue(field.value);
                }
            }
        }

        [SerializeField] private TrialRole friendlyTrial = new TrialRole(), enemyTrial = new TrialRole();
        private string trialOpening;
        private GameObject activeTrialPrefab;

        private SkillLibraryCharacter TrialCharacter(bool enemy)
        {
            var selected = enemy ? enemyTrial.selected : friendlyTrial.selected;
            var camp = enemy ? SkillLibraryCharacter.Camp.Enemy : SkillLibraryCharacter.Camp.Friendly;
            if (selected && selected.camp == camp) return selected;
            if (graph.libraryCharacter && graph.libraryCharacter.camp == camp) return graph.libraryCharacter;
            return LibraryRoles.FirstOrDefault(r => r.camp == camp && r.defaultForPreview);
        }

        private Entity TrialTemplate(bool enemy)
        {
            var role = TrialCharacter(enemy);
            return Template(role ? role.entityUid : enemy ? "EnemyBot01" : "player");
        }

        private GameObject TrialEnemyModel()
        {
            var role = TrialCharacter(true);
            if (role && role.enemyPreviewModel) return role.enemyPreviewModel;
            bool useSkillModel = !enemyTrial.selected && (!graph.libraryCharacter || graph.libraryCharacter == role);
            return useSkillModel ? (graph as OriginalSkillGraph)?.previewEnemy : null;
        }

        private void DrawTrialRole(bool enemy)
        {
            var config = enemy ? enemyTrial : friendlyTrial;
            var camp = enemy ? SkillLibraryCharacter.Camp.Enemy : SkillLibraryCharacter.Camp.Friendly;
            var roles = LibraryRoles.Where(c => c.camp == camp).ToArray();
            string side = enemy ? "敌方" : "友方";
            int current = Array.IndexOf(roles, config.selected) + 1;
            var labels = roles.Select(c => c.name + " · " + (string.IsNullOrEmpty(c.entityUid) ? "未绑定" : c.entityUid))
                .Prepend("自动（技能所属角色或场景默认）").ToArray();
            int index = EditorGUILayout.Popup(side + "角色", current, labels);
            if (index != current) config.selected = index == 0 ? null : roles[index - 1];
            var role = TrialCharacter(enemy);
            var template = TrialTemplate(enemy);
            config.UseTemplate(template);
            if (template == null)
            {
                EditorGUILayout.HelpBox(side + "角色 " + (role ? role.name : "") + " 缺少有效的 MCC 实体绑定。", MessageType.Warning);
                return;
            }
            string name = role ? role.name : enemy ? "小雕" : "主角";
            config.expanded = EditorGUILayout.Foldout(config.expanded, name + "（" + template.uid + "） · 临时基础值", true);
            if (!config.expanded) return;
            using (new EditorGUI.IndentLevelScope())
            {
                var defs = GameManager.AttributeData;
                EditorGUILayout.LabelField("模板初值", TrialStatLine(template, false), EditorStyles.wordWrappedMiniLabel);
                var numbers = template.Attributes.Where(a => defs.AttributeList.Any(d => d.uid == a.uid && !d.stringValue)).ToArray();
                foreach (var field in config.values.ToArray())
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var definition = defs.AttributeList.Find(a => a.uid == field.uid);
                        float next = EditorGUILayout.DelayedFloatField(definition?.name + " · " + field.uid, field.value);
                        if (Finite(next)) field.value = next;
                        if (GUILayout.Button("恢复", GUILayout.Width(48))) config.values.Remove(field);
                    }
                }
                var available = numbers.Where(a => config.values.All(v => v.uid != a.uid)).ToArray();
                if (available.Length > 0)
                {
                    var options = available.Select(a => defs.GetAttribute(a.uid).name + " · " + a.uid).Prepend("选择要覆盖的数值").ToArray();
                    int add = EditorGUILayout.Popup("临时覆盖", 0, options);
                    if (add > 0)
                    {
                        var entry = available[add - 1];
                        config.values.Add(new TrialValue { uid = entry.uid, value = entry.floatValue });
                    }
                }
            }
        }

        private static string OpeningStats(EntityComponent actor)
        {
            return actor.uid + " · " + TrialStatLine(actor.mData, true);
        }

        private static readonly (string key, string label)[] TrialStats =
            { ("atk", "攻"), ("def", "防"), ("chp", "当前生命"), ("hp", "生命上限") };

        private static string TrialStatLine(Entity entity, bool live)
        {
            var fields = new List<string>();
            foreach (var stat in TrialStats)
            {
                var value = entity.Attributes.Find(a => a.uid == stat.key);
                if (value == null) continue;
                string text = live ? entity.GetAttributeFloat(stat.key).ToString("0.##") : value.isFixed ? value.floatValue.ToString("0.##")
                    : value.minValue.ToString("0.##") + "–" + value.maxValue.ToString("0.##");
                fields.Add(stat.label + " " + text);
            }
            return string.Join("，", fields);
        }
    }
}
