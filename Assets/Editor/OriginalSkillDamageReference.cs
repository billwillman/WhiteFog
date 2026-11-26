using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SoftKitty
{
    public partial class SkillWorkbench
    {
        [Serializable] private sealed class OriginalDamageCatalog
        {
            public string version;
            public OriginalDamageRow[] skills;
        }

        [Serializable] private sealed class OriginalDamageRow
        {
            public int skillId, levelTableId, advance, level, rawRatio, rawFlat, targetCount, cooldownMs;
            public bool hasLevelRow;
            public string timing, limit, levelAssetPath;
        }

        private const string OriginalDamageCatalogPath = "Assets/Editor/宗师之上伤害核对.json";
        private static TextAsset originalDamageFile;
        private static OriginalDamageCatalog originalDamageCatalog;
        private bool showOriginalDamageDetails;

        private static void DrawOriginalSegmentRatios(OriginalSkillGraph skill)
        {
            var segments = skill.RootSet.Actions.Where(a => a.ClassName == "FormulaImpact" &&
                    a.calculation?.independentSegment == true && a.calculation.segmentPercent?.Count == 1 &&
                    a.calculation.EffectiveMode == SkillCalculation.Mode.SharedFlow &&
                    a.calculation.GetStageEntries().Any(s => s != null && s.kind == SkillDamageFlow.Kind.SegmentShare &&
                        a.calculation.IsStageEnabled(s.id)))
                .Select(a => a.calculation).OrderBy(c => c.logicIndex).ToArray();
            if (segments.Length == 0) return;
            EditorGUILayout.LabelField("分段倍率（相对于整招公式）", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            foreach (var segment in segments)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    float percent = EditorGUILayout.DelayedFloatField("逻辑段 " + segment.logicIndex + "（%）", segment.segmentPercent[0]);
                    bool verified = EditorGUILayout.ToggleLeft(segment.segmentRatioVerified ? "已核实" : "待核", segment.segmentRatioVerified, GUILayout.Width(78));
                    if ((percent != segment.segmentPercent[0] || verified != segment.segmentRatioVerified) &&
                        !float.IsNaN(percent) && !float.IsInfinity(percent) && percent >= 0)
                    {
                        Undo.RecordObject(skill, "编辑分段倍率");
                        segment.segmentPercent[0] = percent;
                        segment.segmentRatioVerified = verified;
                        EditorUtility.SetDirty(skill);
                    }
                }
            }
        }

        private void DrawOriginalDamageReference(OriginalSkillGraph skill)
        {
            if (!originalDamageFile)
            {
                originalDamageFile = AssetDatabase.LoadAssetAtPath<TextAsset>(OriginalDamageCatalogPath);
                if (!originalDamageFile) return;
                originalDamageCatalog = JsonUtility.FromJson<OriginalDamageCatalog>(originalDamageFile.text);
            }
            if (originalDamageCatalog?.version != skill.sourceVersion) return;
            var row = originalDamageCatalog.skills.FirstOrDefault(entry => entry.skillId == skill.sourceId);
            if (row == null) return;
            string ratio = row.hasLevelRow ? (row.rawRatio / 10f).ToString("0.###") + "% 攻击" : "倍率待战报核对";
            showOriginalDamageDetails = EditorGUILayout.Foldout(showOriginalDamageDetails,
                "《宗师之上》" + skill.sourceVersion + " · " + row.skillId + " · " + ratio + " · " + row.targetCount + " 目标", true);
            DrawOriginalSegmentRatios(skill);
            if (!showOriginalDamageDetails) return;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (row.hasLevelRow)
                    EditorGUILayout.LabelField("原包等级行", row.levelTableId + " · " + row.advance + "阶" + row.level +
                        "级 · 倍率原值 " + row.rawRatio + " / 1000 · 固定项 " + row.rawFlat);
                EditorGUILayout.LabelField("技能主表", row.targetCount + " 目标 · 基础冷却 " + row.cooldownMs + " ms");
                EditorGUILayout.LabelField(row.timing, EditorStyles.wordWrappedLabel);
                EditorGUILayout.HelpBox(row.limit, MessageType.Info);
                const string folder = "Assets/WhiteFog/Skills/攻击公式/";
                var table = string.IsNullOrEmpty(row.levelAssetPath) ? null
                    : AssetDatabase.LoadAssetAtPath<SkillLevelTable>(row.levelAssetPath);
                if (table && GUILayout.Button(row.hasLevelRow ? "定位本次技能编号对应的原表等级" : "定位工程试配等级表（原倍率待核）"))
                    EditorGUIUtility.PingObject(table);
                if (row.hasLevelRow)
                {
                    var pvp = AssetDatabase.LoadAssetAtPath<SkillDamageFlow>(folder + "宗师之上数值/宗师之上PvP直伤实测.asset");
                    if (pvp && GUILayout.Button("定位可选 PvP 直伤实测流程"))
                        EditorGUIUtility.PingObject(pvp);
                }
            }
        }
    }
}
