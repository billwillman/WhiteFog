using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SoftKitty
{
    public partial class SkillWorkbench
    {
        private static readonly string[] AttributeTypes = { "数值", "字符串" };
        [SerializeField] private string attributeUid;
        private string attributeSearch = "", proposedUid = "", proposedName = "", proposedValue = "0";
        private string attributeError, reviewedUid;
        private bool addingAttribute, newAttributeIsText;
        private Vector2 attributeListScroll, referenceScroll;
        private List<SkillAttributeEdits.Reference> attributeReferences;

        private void ResetAttributeReview()
        {
            attributeReferences = null;
            reviewedUid = null;
            proposedUid = "";
            attributeError = null;
            referenceScroll = Vector2.zero;
        }

        private void DrawAttributePage()
        {
            var db = GameManager.AttributeData;
            if (!db)
            {
                EditorGUILayout.HelpBox("请先在 SoftKitty 的 Data Settings 中绑定属性库。", MessageType.Info);
                if (GUILayout.Button("打开数据设置")) SettingsService.OpenProjectSettings("Project/SoftKitty/Data Settings");
                return;
            }
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    attributeSearch = EditorGUILayout.TextField("搜索名称或 UID", attributeSearch);
                    if (GUILayout.Button("新建属性", GUILayout.Width(90)))
                    {
                        addingAttribute = true;
                        ResetAttributeReview();
                        proposedName = ""; proposedValue = "0"; newAttributeIsText = false;
                    }
                }
                attributeListScroll = EditorGUILayout.BeginScrollView(attributeListScroll, GUILayout.Height(150));
                foreach (var entry in db.AttributeList)
                {
                    if ((entry.name ?? "").IndexOf(attributeSearch, StringComparison.OrdinalIgnoreCase) < 0 &&
                        (entry.uid ?? "").IndexOf(attributeSearch, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    bool selected = !addingAttribute && attributeUid == entry.uid;
                    if (GUILayout.Toggle(selected, entry.name + "    " + entry.uid, "Button") && !selected)
                    {
                        ResetAttributeReview();
                        attributeUid = entry.uid;
                        addingAttribute = false;
                        GUI.FocusControl(null);
                    }
                }
                EditorGUILayout.EndScrollView();
                var current = db.AttributeList.Find(a => a.uid == attributeUid);
                if (addingAttribute)
                {
                    EditorGUILayout.LabelField("新建属性", EditorStyles.boldLabel);
                    proposedUid = EditorGUILayout.TextField("UID", proposedUid);
                    proposedName = EditorGUILayout.TextField("显示名", proposedName);
                    newAttributeIsText = EditorGUILayout.Popup("类型", newAttributeIsText ? 1 : 0, AttributeTypes) == 1;
                    proposedValue = EditorGUILayout.TextField("默认值", proposedValue);
                    if (GUILayout.Button("保存新属性"))
                        AttributeCommand(() => {
                            attributeUid = SkillAttributeEdits.Create(db, proposedUid.Trim(), proposedName.Trim(), proposedValue, newAttributeIsText).uid;
                            addingAttribute = false; attributeSearch = ""; ResetAttributeReview();
                        });
                }
                else if (current != null)
                {
                    EditorGUILayout.LabelField(current.name, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("UID", current.uid);
                    EditorGUILayout.LabelField("类型", current.stringValue ? "字符串" : "数值");
                    string label = EditorGUILayout.DelayedTextField("显示名", current.name);
                    string value = EditorGUILayout.DelayedTextField("默认值", current.defaultValue);
                    if (label != current.name || value != current.defaultValue)
                        AttributeCommand(() => SkillAttributeEdits.Edit(db, current, label, value));
                    if (GUILayout.Button("检查引用 / 修改 UID / 删除"))
                        AttributeCommand(() => {
                            attributeReferences = SkillAttributeEdits.Scan(current.uid);
                            reviewedUid = current.uid;
                        });
                    if (attributeReferences != null && reviewedUid == current.uid) DrawAttributeReview(db, current);
                }
                else EditorGUILayout.HelpBox("选择要编辑的属性，或新建一条属性定义。", MessageType.Info);
                if (!string.IsNullOrEmpty(attributeError)) EditorGUILayout.HelpBox(attributeError, MessageType.Error);
            }
        }

        private void AttributeCommand(System.Action action)
        {
            try { attributeError = null; action(); }
            catch (Exception e) { attributeError = e.Message; }
        }

        private void DrawAttributeReview(AttributeObject db, Attribute current)
        {
            EditorGUILayout.LabelField("引用检查 · " + attributeReferences.Count + " 处", EditorStyles.boldLabel);
            if (attributeReferences.Count > 0)
            {
                referenceScroll = EditorGUILayout.BeginScrollView(referenceScroll, GUILayout.MaxHeight(190));
                foreach (var r in attributeReferences)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(r.location, EditorStyles.wordWrappedLabel);
                        if (r.asset && GUILayout.Button("定位", GUILayout.Width(45))) EditorGUIUtility.PingObject(r.asset);
                    }
                }
                EditorGUILayout.EndScrollView();
            }
            proposedUid = EditorGUILayout.TextField("新 UID 或替代属性 UID", proposedUid);
            EditorGUILayout.HelpBox("重命名会保留 MCC 数字 ID。替换时，角色若已有替代属性，保留其原值。直接删除会移除角色上的该项数值，图中保留缺失 UID 供定位。脚本中的文字引用需手动处理。", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(proposedUid)))
                {
                    DrawAttributeOperation(db, current, SkillAttributeEdits.Operation.Rename, "修改 UID");
                    DrawAttributeOperation(db, current, SkillAttributeEdits.Operation.Replace, "替换引用后删除");
                }
                DrawAttributeOperation(db, current, SkillAttributeEdits.Operation.Delete, "直接删除");
            }
        }

        private void DrawAttributeOperation(AttributeObject db, Attribute current, SkillAttributeEdits.Operation op, string label)
        {
            if (!GUILayout.Button(label)) return;
            string targetUid = proposedUid.Trim();
            string subject = op == SkillAttributeEdits.Operation.Delete ? current.uid : current.uid + " → " + targetUid;
            if (!EditorUtility.DisplayDialog(label, subject + "\n已列出 " + attributeReferences.Count + " 处引用。", label, "取消")) return;
            AttributeCommand(() => {
                SkillAttributeEdits.Apply(db, current.uid, targetUid, op, attributeReferences);
                attributeUid = op == SkillAttributeEdits.Operation.Delete ? null : targetUid;
                ResetAttributeReview();
            });
        }
    }
}
