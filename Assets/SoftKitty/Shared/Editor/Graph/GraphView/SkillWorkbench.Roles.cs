using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SoftKitty
{
    public partial class SkillWorkbench
    {
        private string newEntityUid = "", roleError;

        private static Entity Template(string uid)
        {
            var db = GameManager.EntityManagerData;
            return db && !string.IsNullOrEmpty(uid) ? db.EntityData.EntityList.Find(e => e.uid == uid) : null;
        }

        private static void SaveRoleData(UnityEngine.Object asset)
        {
            if (asset is DataObject data) data.GenerateUniqueHash();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
        }

        private static void EditRoleData(UnityEngine.Object asset, string operation, System.Action edit)
        {
            Undo.RegisterCompleteObjectUndo(asset, operation);
            edit();
            SaveRoleData(asset);
        }

        private void DrawRolePage()
        {
            var previous = selectedLibraryCharacter;
            if (!DrawRoleSelector(false)) return;
            var role = selectedLibraryCharacter;
            if (previous != role) { roleError = null; newEntityUid = ""; }
            var db = GameManager.EntityManagerData;
            var defs = GameManager.AttributeData;
            if (!db || !defs)
            {
                EditorGUILayout.HelpBox("请在 MCC 数据设置中绑定实体库和属性库。", MessageType.Warning);
                return;
            }
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                EditorGUILayout.LabelField(role.name, EditorStyles.boldLabel);
                var uids = db.EntityData.EntityList.Select(e => e.uid).Prepend("未绑定").ToArray();
                int index = Array.IndexOf(uids, role.entityUid, 1);
                bool missing = index < 0 && !string.IsNullOrEmpty(role.entityUid);
                if (missing) { index = uids.Length; uids = uids.Append("缺少实体 · " + role.entityUid).ToArray(); }
                index = Mathf.Max(0, index);
                int next = EditorGUILayout.Popup("MCC 实体 UID", index, uids);
                if (next != index)
                    EditRoleData(role, "绑定角色实体", () => role.entityUid = next == 0 ? "" : uids[next]);

                if (role.camp == SkillLibraryCharacter.Camp.Enemy)
                {
                    var model = (GameObject)EditorGUILayout.ObjectField("敌位预览模型", role.enemyPreviewModel, typeof(GameObject), false);
                    if (model != role.enemyPreviewModel) EditRoleData(role, "修改试放模型", () => role.enemyPreviewModel = model);
                }
                var entity = Template(role.entityUid);
                if (entity == null)
                {
                    EditorGUILayout.HelpBox(missing ? "绑定的实体已移除，请重新选择或创建。" : "选择实体后可编辑初始属性，也可以创建一条实体记录。", MessageType.Info);
                    newEntityUid = EditorGUILayout.TextField("新实体 UID", newEntityUid);
                    using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(newEntityUid)))
                        if (GUILayout.Button("创建实体并绑定"))
                        {
                            string uid = newEntityUid.Trim();
                            if (Template(uid) != null || db.IdManager.idToKey.Any(pair => pair.key == uid))
                                roleError = "该 UID 已存在，请从列表选择。";
                            else
                            {
                                CreateRoleEntity(role, db, defs, uid);
                                roleError = null;
                                newEntityUid = "";
                            }
                        }
                    if (!string.IsNullOrEmpty(roleError)) EditorGUILayout.HelpBox(roleError, MessageType.Warning);
                    return;
                }
                var shared = LibraryRoles.Where(r => r != role && r.entityUid == entity.uid).Select(r => r.name).ToArray();
                if (shared.Length > 0) EditorGUILayout.HelpBox("共用此实体配置的角色还有 " + string.Join("、", shared), MessageType.Info);
                int level = Mathf.Max(0, EditorGUILayout.DelayedIntField("属性升级等级", entity.AttributesUpgradeLevel));
                if (level != entity.AttributesUpgradeLevel) EditRoleData(db, "修改属性升级等级", () => entity.AttributesUpgradeLevel = level);
                EditorGUILayout.LabelField("初始属性", EditorStyles.boldLabel);
                for (int i = 0; i < entity.Attributes.Count; i++)
                {
                    var entry = entity.Attributes[i];
                    var def = defs.AttributeList.Find(d => d.uid == entry.uid);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (def == null) EditorGUILayout.LabelField("缺少属性 " + entry.uid);
                        else DrawEntityValue(db, entry, def);
                        if (GUILayout.Button("移除", GUILayout.Width(48)))
                        {
                            EditRoleData(db, "移除角色属性", () => entity.Attributes.Remove(entry));
                            break;
                        }
                    }
                }
                var available = defs.AttributeList.Where(d => entity.Attributes.All(a => a.uid != d.uid)).ToArray();
                if (available.Length > 0)
                {
                    int add = EditorGUILayout.Popup("添加属性", 0,
                        available.Select(d => d.name + " · " + d.uid).Prepend("选择属性").ToArray());
                    if (add > 0) EditRoleData(db, "添加角色属性", () => entity.AddAttributeData(available[add - 1].uid));
                }
            }
        }

        internal static void CreateRoleEntity(SkillLibraryCharacter role, EntityManagerObject db, AttributeObject defs, string uid)
        {
            Undo.RegisterCompleteObjectUndo(new UnityEngine.Object[] { db, role }, "创建并绑定角色实体");
            var entity = db.NewEntity(uid);
            db.IdManager.GetId(uid);
            foreach (var def in defs.AttributeList.Where(d => d.coreStats)) entity.AddAttributeData(def.uid);
            role.entityUid = uid;
            SaveRoleData(db);
            SaveRoleData(role);
        }

        private static void DrawEntityValue(EntityManagerObject db, AttributeData entry, Attribute def)
        {
            string label = def.name + " · " + def.uid;
            if (def.stringValue)
            {
                var value = EditorGUILayout.DelayedTextField(label, entry.stringValue);
                if (value != entry.stringValue) EditRoleData(db, "修改初始文本", () => entry.stringValue = value);
                return;
            }
            EditorGUILayout.LabelField(label, GUILayout.Width(EditorGUIUtility.labelWidth));
            bool fixedValue = EditorGUILayout.Popup(entry.isFixed ? 0 : 1, ValueModes, GUILayout.Width(55)) == 0;
            if (fixedValue != entry.isFixed)
                EditRoleData(db, "切换初始值类型", () => {
                    if (!fixedValue) entry.minValue = entry.maxValue = entry.floatValue;
                    else entry.floatValue = entry.minValue;
                    entry.isFixed = fixedValue;
                });
            if (fixedValue)
            {
                float value = EditorGUILayout.DelayedFloatField(entry.floatValue);
                if (Finite(value) && value != entry.floatValue)
                    EditRoleData(db, "修改初始数值", () => entry.floatValue = value);
            }
            else
            {
                float min = EditorGUILayout.DelayedFloatField(entry.minValue);
                GUILayout.Label("至", GUILayout.Width(18));
                float max = EditorGUILayout.DelayedFloatField(entry.maxValue);
                if (Finite(min) && Finite(max) && min <= max && (min != entry.minValue || max != entry.maxValue))
                    EditRoleData(db, "修改初始数值范围", () => { entry.minValue = min; entry.maxValue = max; });
            }
        }

        private static readonly string[] ValueModes = { "固定", "范围" };
        private static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
    }
}
