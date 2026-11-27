using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SoftKitty
{
    internal static class SkillAttributeEdits
    {
        internal enum Operation { Rename, Replace, Delete }

        internal sealed class Reference
        {
            internal Object asset;
            internal string location;
            internal System.Action<string> change;
        }

        internal static void CheckDefinition(AttributeObject db, string uid, string name, string value, bool text)
        {
            if (string.IsNullOrWhiteSpace(uid) || uid != uid.Trim()) throw new ArgumentException("UID 不能为空或带首尾空格。");
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("请填写显示名。");
            if (!text && (!float.TryParse(value, out float number) || float.IsNaN(number) || float.IsInfinity(number)))
                throw new ArgumentException("数值属性需要有限的数值默认值。");
            if (!db) throw new InvalidOperationException("缺少 MCC 属性库。");
        }

        internal static Attribute Create(AttributeObject db, string uid, string name, string value, bool text)
        {
            CheckDefinition(db, uid, name, value, text);
            if (db.IndexOfAttributes(uid) >= 0) throw new ArgumentException("UID 已存在：" + uid);
            Undo.RegisterCompleteObjectUndo(db, "新建属性定义");
            var definition = new Attribute { uid = uid, name = name, defaultValue = value, stringValue = text };
            db.AttributeList.Add(definition);
            db.IdManager.GetId(uid);
            Save(db);
            return definition;
        }

        internal static void Edit(AttributeObject db, Attribute definition, string name, string value)
        {
            CheckDefinition(db, definition.uid, name, value, definition.stringValue);
            Undo.RegisterCompleteObjectUndo(db, "编辑属性定义");
            definition.name = name;
            definition.defaultValue = value;
            Save(db);
        }

        private static void Save(Object asset)
        {
            if (asset is DataObject data) data.GenerateUniqueHash();
            if (asset is SkillFormula formula) formula.Invalidate();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
        }

        // A null replacement removes an entity value; graph nodes retain the UID for diagnosis and repair.
        internal static List<Reference> Scan(string uid)
        {
            var refs = new List<Reference>();
            var entities = GameManager.EntityManagerData;
            if (entities)
                foreach (var entity in entities.EntityData.EntityList)
                    foreach (var entry in entity.Attributes.Where(a => a.uid == uid))
                        refs.Add(new Reference { asset = entities, location = "角色 " + entity.uid, change = next => {
                            if (next == null || entity.Attributes.Any(a => a != entry && a.uid == next)) entity.Attributes.Remove(entry);
                            else entry.uid = next;
                        } });

            var graphs = AssetDatabase.FindAssets("t:GraphObject", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath).Distinct()
                .SelectMany(path => AssetDatabase.LoadAllAssetsAtPath(path).OfType<GraphObject>()).ToArray();
            foreach (var g in graphs)
            {
                var root = g.RootSet;
                if (root == null) continue;
                foreach (var v in root.Variables.Where(v => v.Type == VariableType.Attribute && v.AttributeKey == uid))
                    refs.Add(new Reference { asset = g, location = g.name + " · " + v.Uid, change = next => { if (next != null) v.AttributeKey = next; } });
                foreach (var node in root.Actions.Cast<CustomNode>().Concat(root.Handlers))
                    if (UsesAttributeField(node) && node.StringValues.FirstOrDefault() == uid)
                        refs.Add(new Reference { asset = g, location = g.name + " · " + node.Uid + " · " + node.ClassName,
                            change = next => { if (next != null) node.StringValues[0] = next; } });
            }

            var affected = new HashSet<string>(refs.Where(r => r.asset is GraphObject).Select(r => AssetDatabase.GetAssetPath(r.asset)));
            if (affected.Count > 0)
            {
                foreach (var graph in graphs)
                {
                    var path = AssetDatabase.GetAssetPath(graph);
                    if (affected.Contains(path)) continue;
                    if (AssetDatabase.GetDependencies(path).Any(affected.Contains))
                        refs.Add(new Reference { asset = graph, location = graph.name + " · 引用受影响的图" });
                }
                foreach (var guid in AssetDatabase.FindAssets("t:SkillDamageFlow", new[] { "Assets" }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!AssetDatabase.GetDependencies(path).Any(affected.Contains)) continue;
                    var flow = AssetDatabase.LoadAssetAtPath<SkillDamageFlow>(path);
                    if (flow) refs.Add(new Reference { asset = flow, location = flow.name + " · 伤害流程引用的公式" });
                }
                var buffs = GameManager.OverTimeEffectData;
                if (buffs)
                    foreach (var buff in buffs.overTimeEffects)
                        if (buff.DesignGraph && AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(buff.DesignGraph)).Any(affected.Contains))
                            refs.Add(new Reference { asset = buffs, location = "Buff " + buff.name + "（" + buff.uid + "）" });
            }

            string literal = "\"" + uid.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
            foreach (var guid in AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) continue;
                int line = 0;
                foreach (string source in File.ReadLines(Path.Combine(Application.dataPath, path.Substring("Assets/".Length))))
                {
                    line++;
                    if (source.Contains(literal))
                        refs.Add(new Reference { location = path + ":" + line + " · 脚本文字，需人工核对" });
                }
            }
            return refs;
        }

        internal static bool UsesAttributeField(CustomNode node) =>
            node.ClassName == "AttributeValue" || node.ClassName == "AttributeChange" || node.ClassName == "TempAttributeChange";

        internal static void Apply(AttributeObject db, string uid, string next, Operation op, List<Reference> reviewed)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请在编辑模式修改属性定义。");
            var original = db.AttributeList.Single(a => a.uid == uid);
            if (op == Operation.Delete) next = null;
            else
            {
                if (string.IsNullOrWhiteSpace(next) || next != next.Trim() || uid == next) throw new ArgumentException("请填写不同的有效 UID。");
                var replacement = db.AttributeList.Find(a => a.uid == next);
                if (op == Operation.Rename && (replacement != null || db.IdManager.idToKey.Any(k => k.key == next)))
                    throw new ArgumentException("新 UID 已被占用。");
                if (op == Operation.Replace && (replacement == null || replacement.stringValue != original.stringValue))
                    throw new ArgumentException("请填写已存在、类型相同的替代属性 UID。");
            }

            var refs = Scan(uid);
            string Identity(Reference r) => (r.asset ? AssetDatabase.GetAssetPath(r.asset) : "") + "|" + r.location;
            if (!refs.Select(Identity).OrderBy(s => s).SequenceEqual(reviewed.Select(Identity).OrderBy(s => s)))
                throw new InvalidOperationException("引用已变化，请重新查看后执行。");
            var graphs = refs.Where(r => r.change != null).Select(r => r.asset).OfType<GraphObject>();
            var openGraph = graphs.FirstOrDefault(MasterGraphWindow.IsEditing);
            var modified = refs.Where(r => r.change != null && (op != Operation.Delete || r.asset is EntityManagerObject))
                .Select(r => r.asset).Append(db).Distinct().ToArray();
            foreach (var asset in modified)
                if (!AssetDatabase.IsOpenForEdit(asset)) throw new InvalidOperationException("资产不可写：" + AssetDatabase.GetAssetPath(asset));

            // The graph window keeps a separate revert snapshot and cached UID choices.
            // Reopen it around the migration so neither can restore stale references.
            if (openGraph) MasterGraphWindow.CloseIfEditing(openGraph);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            try
            {
                Undo.SetCurrentGroupName("修改属性 UID 及引用");
                Undo.RegisterCompleteObjectUndo(modified, "修改属性 UID 及引用");
                foreach (var r in refs) r.change?.Invoke(next);
                int id = db.IdManager.GetId(uid);
                if (op == Operation.Rename)
                {
                    db.IdManager.ReplaceKey(id, next);
                    original.uid = next;
                }
                else
                {
                    db.AttributeList.Remove(original);
                    db.IdManager.RemoveKey(id);
                }
                foreach (var asset in modified) Save(asset);
                Undo.CollapseUndoOperations(group);
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                foreach (var asset in modified) Save(asset);
                throw;
            }
            finally
            {
                if (openGraph) MasterGraphWindow.OpenWindow(openGraph);
            }
        }
    }
}
