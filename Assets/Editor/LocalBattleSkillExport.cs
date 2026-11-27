using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SoftKitty;
using UnityEditor;
using UnityEngine;

// A local script envelope also loads over file://. The payload contains data only.
public static class LocalBattleSkillExport
{
    [Serializable] private class Package
    {
        public int version = 1, id, targetLimit;
        public string name, entry, enemyModel;
        public bool opponent;
        public List<Graph> graphs = new List<Graph>();
        public List<Status> statuses = new List<Status>();
    }
    [Serializable] private class Graph
    {
        public string key, root, output;
        public List<Node> nodes = new List<Node>();
    }
    [Serializable] private class Port { public string type; public string[] links; }
    [Serializable] private class Stage { public string kind, formula; }
    [Serializable] private class Node
    {
        public string id, op, key, motion, fx, trail, impact, bone, targetBone;
        public Port[] inputs, outputs;
        public int type, count, segment, mask, recipients;
        public float at, interval, duration, value, share, speed, acceleration, open, window, trailSeconds, impactScale;
        public bool follow, followRotation, onTarget, triggerHit;
        public float hitDelay;
        public Vector3 from, to, size, offset;
        public int[] ints;
        public string[] strings, tags;
        public bool[] flags;
        public Stage[] stages;
        public List<SkillLevelTable.Row> levels;
    }
    [Serializable] private class Status
    {
        public int id; public string key, graph; public float duration, interval;
        public List<DynamicFloat> variables;
    }

    [MenuItem("Tools/技能工作台/导出选中技能的 H5 本地战斗包")]
    public static void Selected() => Export(Selection.activeObject as OriginalSkillGraph);

    // Batch entry for the first migrated asset. All compilation below uses its graph.
    public static void ExportPierce() => Export(AssetDatabase.LoadAssetAtPath<OriginalSkillGraph>(
        "Assets/WhiteFog/Skills/宗师之上/破空剑法.asset"));

    [MenuItem("Tools/技能工作台/导出宗师之上 H5 本地战斗包")]
    public static void ExportLibrary()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:OriginalSkillGraph", new[] { "Assets/WhiteFog/Skills/宗师之上" }))
            Export(AssetDatabase.LoadAssetAtPath<OriginalSkillGraph>(AssetDatabase.GUIDToAssetPath(guid)));
    }

    private static string Key(UnityEngine.Object o) => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(o));
    private static Port[] Ports(List<PortSetting> ports) => ports.Select(p => new Port {
        type = p.Type, links = p.uid.Select(link => link.Split('#')[0]).ToArray()
    }).ToArray();

    private static Node Add(Graph g, NodeBase n, string op)
    {
        var node = new Node { id = n.Uid, op = op, inputs = Ports(n.inputPort), outputs = Ports(n.outputPort) };
        g.nodes.Add(node);
        return node;
    }

    public static void Export(OriginalSkillGraph source)
    {
        if (!source || source.sourceId <= 0) throw new InvalidDataException("请选择有原作 ID 的技能图。");
        var pkg = new Package { id = source.sourceId, targetLimit = Mathf.Max(1, source.castTargetLimit), name = source.name, entry = Key(source), opponent = source.previewAsOpponent,
            enemyModel = source.previewEnemy ? source.previewEnemy.name : "" };
        Compile(pkg, source);
        var folder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "QingyunSect/LoginDemo/battle-art/skills");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, pkg.id + ".js");
        string data = "window.localBattlePackages ??= {};\nwindow.localBattlePackages[" + pkg.id +
            "] = " + JsonUtility.ToJson(pkg) + ";\n";
        WritePackage(path, data);
        var ids = Directory.GetFiles(folder, "*.js").Select(Path.GetFileNameWithoutExtension)
            .Select(name => int.TryParse(name, out var id) ? id : 0).Where(id => id > 0).OrderBy(id => id);
        WritePackage(Path.Combine(folder, "index.js"), "window.localBattleSkillIds = [" + string.Join(",", ids) + "];\n");
        OriginalSkillEditorExport.Export();
        Debug.Log("已导出 H5 本地战斗包 " + path);
    }

    private static void WritePackage(string path, string data)
    {
        if (File.Exists(path) && File.ReadAllText(path) == data) return;
        var temp = Path.Combine(Path.GetDirectoryName(path), Path.GetRandomFileName());
        try {
            File.WriteAllText(temp, data, new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        } finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private static string Compile(Package pkg, GraphObject asset)
    {
        string key = Key(asset);
        if (pkg.graphs.Any(item => item.key == key)) return key;
        var root = asset.RootSet;
        var graph = new Graph { key = key, root = root.Uid, output = (asset as SkillFormula)?.outputUid };
        pkg.graphs.Add(graph);
        Add(graph, root, "Root");
        foreach (var x in root.Entities) Add(graph, x, "Entity").type = (int)x.Type;
        foreach (var x in root.Variables) {
            var n = Add(graph, x, "Variable"); n.type = (int)x.Type; n.value = x.Constant;
            n.key = x.Type == VariableType.Attribute ? x.AttributeKey : x.VariableUid;
            if (x.Type == VariableType.Random) throw new InvalidDataException("暂未导出随机变量 " + x.Uid);
        }
        foreach (var x in root.Maths) Add(graph, x, "Math").key = x.Type.ToString();
        foreach (var x in root.Triggers) Add(graph, x, "Trigger");
        foreach (var x in root.Handlers) {
            if (x.ClassName != "AngleDifference" && x.ClassName != "EntityExist") throw new InvalidDataException("未支持条件 " + x.ClassName);
            var n = Add(graph, x, x.ClassName); n.ints = x.IntValues.ToArray(); n.flags = x.BoolValues.ToArray();
        }
        if (root.ConditionGroups.Count + root.Branchs.Count > 0) throw new InvalidDataException("本地运行时尚未支持该图的条件组或分支。");
        foreach (var x in root.Actions) {
            var n = Add(graph, x, x.ClassName);
            n.at = x.WaitTime; n.type = (int)x.Type; n.count = x.Count; n.interval = x.Interval; n.duration = x.KeepTime;
            n.ints = x.IntValues.ToArray(); n.strings = x.StringValues.ToArray();
            switch (n.op) {
                case "Animation":
                    var m = SkillMotion.For(x, asset as OriginalSkillGraph);
                    n.motion = m.source == SkillMotion.Source.AnimationClip ? m.clip.name : m.state;
                    break;
                case "OriginalSkillVisual":
                    if (SkillWorkbench.IsSourceTime(x)) { n.op = "Reference"; break; }
                    var cue = OriginalSkillVisualAction.EffectFor(x, asset as OriginalSkillGraph);
                    if (cue?.prefab == null) throw new InvalidDataException("未支持的表现节点 " + x.Uid);
                    n.op = "Effect"; n.fx = cue.prefab.name; n.bone = cue.bone; n.follow = cue.followBone;
                    n.onTarget = cue.onTarget; n.value = cue.duration; n.followRotation = cue.followRotation;
                    n.triggerHit = cue.triggerHit; n.hitDelay = cue.hitDelay; n.size = cue.hitBoxSize;
                    break;
                case "Projectile":
                    if (x.requiresProjectileSnap && (!x.projectileSnap || x.projectileSnap.settings == null))
                        throw new InvalidDataException(x.Uid + " 缺少投射物 Snap。");
                    var shot = SkillProjectile.For(x, asset as OriginalSkillGraph);
                    if (shot.path != SkillProjectile.FlightPath.LaunchPositionToTargetPosition)
                        throw new InvalidDataException("当前本地战场只支持向发射时目标位置直射。");
                    n.bone = shot.launchBone; n.targetBone = shot.targetBone;
                    n.fx = shot.prefab ? shot.prefab.name : ""; n.trail = shot.trail ? shot.trail.name : "";
                    n.impact = shot.impact ? shot.impact.name : ""; n.impactScale = shot.impactScale; n.trailSeconds = shot.trailSeconds;
                    n.speed = shot.speed; n.acceleration = shot.acceleration; n.from = shot.launchOffset; n.to = shot.impactOffset;
                    n.size = shot.hitBoxSize; n.offset = shot.hitBoxOffset; n.open = shot.hitBoxStart; n.window = shot.hitBoxSeconds;
                    n.mask = shot.hitLayers.value; n.recipients = (int)shot.hitRecipients; n.tags = shot.hitTags.ToArray();
                    break;
                case "FormulaImpact":
                    var calc = x.calculation;
                    calc.ValidateFlow();
                    if (calc.EffectiveMode != SkillCalculation.Mode.SharedFlow || x.IntValues[0] != 0 || x.outputPort.Any(port => port.uid.Count > 0) ||
                        x.Type != ActionType.Only_Once || x.FloatValues.Any(f => f != 0) || x.BoolValues.Any(b => b))
                        throw new InvalidDataException("该命中结算含未支持的选项 " + x.Uid);
                    calc.ValidateShares(1);
                    n.segment = calc.independentSegment ? calc.logicIndex : 1;
                    n.share = calc.segmentPercent[0]; n.levels = calc.levels.rows;
                    n.stages = calc.GetStageEntries().Where(s => calc.IsStageEnabled(s.id)).Select(s => {
                        if (s.conditionChance || s.kind == SkillDamageFlow.Kind.HitChance || s.kind == SkillDamageFlow.Kind.ReportedResult)
                            throw new InvalidDataException("未支持的伤害环节 " + s.label);
                        return new Stage { kind = s.kind.ToString(), formula = s.formula ? Compile(pkg, s.formula) : "" };
                    }).ToArray();
                    break;
                case "OverTimeEffect":
                    var variable = root.Variables.Single(v => v.Uid == n.inputs[2].links.Single() && v.Type == VariableType.Constant);
                    int id = Mathf.FloorToInt(variable.Constant);
                    if (pkg.statuses.Any(s => s.id == id)) break;
                    var db = GameManager.OverTimeEffectData;
                    string uid = db.IdManager.idToKey.Single(k => k.id == id).key;
                    var effect = db.overTimeEffects.Single(e => e.uid == uid);
                    if (effect.layered || effect.permanent || !effect.canBeRefreshed || effect.interruptCastOnEnter || n.ints[0] != 0)
                        throw new InvalidDataException("未支持的状态配置 " + uid);
                    pkg.statuses.Add(new Status { id = id, key = uid, duration = effect.duration, interval = db.OverTimeEffectInterval,
                        graph = Compile(pkg, (GraphObject)effect.DesignGraph), variables = effect.DynamicVariables });
                    break;
                case "RedirectProjectile": case "AttributeChange": case "Event": break;
                default: throw new InvalidDataException(asset.name + " 包含未实现的节点 " + n.op + " " + x.Uid);
            }
        }
        return key;
    }
}
