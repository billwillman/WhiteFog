using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using SoftKitty;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class OriginalSkillEditorExport
{
    private const string Catalog = "Assets/WhiteFog/Skills/宗师之上";
    private static bool queued;

    internal static string[] SkillIds()
    {
        return AssetDatabase.FindAssets("t:GraphObject", new[] { "Assets" })
            .Where(id => {
                string path = AssetDatabase.GUIDToAssetPath(id);
                var graph = AssetDatabase.LoadAssetAtPath<GraphObject>(path);
                if (graph is OriginalSkillGraph source)
                    return InCatalog(path) && source.sourceId > 0 && !string.IsNullOrEmpty(source.sourceVersion);
                return graph && graph.GetType() == typeof(GraphObject) && graph.RootSet?.ClassName == "Combat";
            }).OrderBy(id => id, StringComparer.Ordinal).ToArray();
    }

    [Serializable]
    private sealed class AssetInfo
    {
        public string guid, path, name, enemy, character, camp;
        public bool original, unwiredHit;
        public ActionInfo[] actions;
        public Table[] levels;
        public Field[] statuses;
    }

    [Serializable]
    private sealed class Field
    {
        public string label, value;
        public Field(string label, string value) { this.label = label; this.value = value; }
    }

    [Serializable] private sealed class Table { public string path; public List<SkillLevelTable.Row> rows; }
    [Serializable] private sealed class ActionInfo
    {
        public string uid, title;
        public List<Field> fields = new List<Field>();
        public float duration;
        public bool complete, onContact;
        public string timing, sourceTime;
        public Band[] bands;
    }
    [Serializable] private sealed class Band { public string name; public float start; public bool blend; }

    private static ActionInfo Describe(GraphObject graph, SoftKitty.Action node)
    {
        var info = new ActionInfo { uid = node.Uid, title = SkillWorkbench.ActionName(node) };
        if (SkillWorkbench.IsSourceTime(node)) info.sourceTime = SkillWorkbench.SourceTimeHelp(node);
        void Add(string label, object value)
        {
            string text = value is UnityEngine.Object asset ? AssetPath(asset) :
                value is bool yes ? (yes ? "是" : "否") : Convert.ToString(value, CultureInfo.InvariantCulture);
            info.fields.Add(new Field(label, string.IsNullOrEmpty(text) ? "—" : text));
        }
        var original = graph as OriginalSkillGraph;
        foreach (var f in node.CustomInterface.Elements)
        {
            if (!SkillWorkbench.Visible(node, f.VisibleBind) || GraphEditorText.IsLegacyProjectileField(node, f) ||
                node.ClassName == "OriginalSkillVisual" && (f.Uid == "stage" || f.Uid == "cue")) continue;
            string label = GraphEditorText.Localize(f.Label);
            int i = f.Index;
            switch (f.Type)
            {
                case NodeInterfaceElementType.String_Variable: Add(label, node.StringValues.ElementAtOrDefault(i)); break;
                case NodeInterfaceElementType.Float_Variable: Add(label, node.FloatValues.ElementAtOrDefault(i)); break;
                case NodeInterfaceElementType.Int_Variable: Add(label, node.IntValues.ElementAtOrDefault(i)); break;
                case NodeInterfaceElementType.Bool_Variable: Add(label, node.BoolValues.ElementAtOrDefault(i)); break;
                case NodeInterfaceElementType.Enum:
                    int index = node.IntValues.ElementAtOrDefault(i);
                    if (GraphEditorText.IsHitMode(node, f))
                        Add(GraphEditorText.HitModeLabel, GraphEditorText.HitModeOptions[GraphEditorText.HitModeIndex(index)]);
                    else Add(label, GraphEditorText.Localize(f.EnumOptions.ElementAtOrDefault(index) ?? index.ToString()));
                    break;
                case NodeInterfaceElementType.Input_Entity:
                case NodeInterfaceElementType.Input_Float:
                    var links = node.inputPort.ElementAtOrDefault(i + 1)?.uid;
                    Add(label, links == null ? "" : string.Join("、", links.Select(link => {
                        string id = link.Split('#')[0];
                        var entity = graph.RootSet.Entities.Find(e => e.Uid == id);
                        if (entity != null) return GraphEditorText.Localize(entity.Type.ToString()) + " · " + id;
                        var variable = graph.RootSet.Variables.Find(v => v.Uid == id);
                        return variable == null ? link : variable.Type + " " + (variable.Type == VariableType.DynamicVariable ?
                            variable.VariableUid : variable.Type == VariableType.Attribute ? variable.AttributeKey : variable.Constant.ToString(CultureInfo.InvariantCulture)) + " · " + id;
                    }))); break;
            }
        }
        if (SkillMotion.UsesBinding(node))
        {
            var m = SkillMotion.For(node, original);
            var runtime = SkillWorkbench.DefaultMotionController(graph, node);
            var controller = (runtime is AnimatorOverrideController wrap ? wrap.runtimeAnimatorController : runtime) as AnimatorController;
            bool direct = m.source == SkillMotion.Source.AnimationClip;
            var t = direct ? SkillMotionTimeline.Clip(m.clip) : SkillMotionTimeline.Sample(runtime, controller, m, new Dictionary<string, object>());
            info.complete = t.complete; info.duration = t.seconds; info.timing = t.Description;
            info.bands = t.bands.Select(b => new Band { name = b.name, start = b.start, blend = b.blend }).ToArray();
            Add("动作来源", direct ? "直接播放 Animation Clip" : "Animator State");
            if (direct) Add("动画片段", m.clip);
            else { Add("参考 Controller", runtime); Add("Animator State", m.StatePath); Add("过渡（秒）", m.transitionSeconds); }
            Add("每次播放时长", t.complete ? t.seconds.ToString("0.00") + " 秒" : "时长待定");
            Add("动画状态顺序", t.Description);
        }
        if (SkillProjectile.IsLaunch(node) || ((node.ClassName == "SkillImpact" || node.ClassName == "FormulaImpact") && node.IntValues.ElementAtOrDefault(0) != 0))
        {
            Add("投射物配置", node.projectileSnap);
            var shot = (node.requiresProjectileSnap || SkillProjectile.IsImpactLaunch(node)) && !node.projectileSnap
                ? null : SkillProjectile.For(node, graph);
            if (shot != null)
            {
                if (SkillProjectile.IsLaunch(node) || SkillProjectile.IsImpactLaunch(node))
                {
                    info.onContact = SkillProjectile.IsLaunch(node);
                    Add("飞行道具", shot.prefab); Add("拖尾", shot.trail); Add("命中特效", shot.impact);
                    Add("发射挂点", shot.launchBone); Add("目标挂点", shot.targetBone); Add("发射偏移", shot.launchOffset);
                    Add("命中偏移", shot.impactOffset); Add("飞行路径", shot.path.ToString()); Add("初速（米/秒）", shot.speed);
                    Add("加速度", shot.acceleration); Add("拖尾时长（秒）", shot.trailSeconds); Add("命中特效缩放", shot.impactScale);
                    if (shot.path == SkillProjectile.FlightPath.FollowTarget) Add("追踪超时（秒）", shot.trackingTimeout);
                }
                Add("可命中对象", shot.hitRecipients.ToString()); Add("命中标签", string.Join("、", shot.hitTags));
                Add("命中盒尺寸（米）", shot.hitBoxSize); Add("命中盒偏移", shot.hitBoxOffset); Add("受击层掩码", shot.hitLayers.value);
                Add("命中盒开启（秒）", shot.hitBoxStart); Add("开启时长（0 至飞行结束）", shot.hitBoxSeconds);
            }
        }
        if (node.ClassName == "OriginalSkillVisual" && new[] {1, 2, 6}.Contains(node.IntValues.ElementAtOrDefault(0)))
        {
            var cue = OriginalSkillVisualAction.EffectFor(node, original);
            if (cue != null)
            {
                Add("特效 Prefab", cue.prefab); Add("挂接对象", cue.onTarget ? "目标" : "施法者"); Add("挂点", cue.bone);
                Add("跟随位置", cue.followBone); Add("使用挂点旋转", cue.followRotation);
                Add("显示时长", cue.duration > 0 ? cue.duration + " 秒" : "按粒子自身时长");
                info.onContact = node.IntValues[0] == 6 && cue.triggerHit;
                if (node.IntValues[0] == 6) Add("触发目标命中", cue.triggerHit);
                if (info.onContact) { Add("命中延迟（秒）", cue.hitDelay); Add("命中盒尺寸（米）", cue.hitBoxSize); }
            }
        }
        if (node.ClassName == "FormulaImpact" && node.calculation != null)
        {
            var c = node.calculation;
            Add("结算方式", c.EffectiveMode == SkillCalculation.Mode.NativeGraph ? "MCC 原生图" : "伤害流程");
            Add("逻辑段", c.logicIndex); Add("分段倍率（%）", string.Join(" / ", c.segmentPercent)); Add("倍率已核实", c.segmentRatioVerified);
            Add("等级表", c.levels);
            if (c.EffectiveMode == SkillCalculation.Mode.NativeGraph)
            {
                Add("结算图", c.nativeGraph);
                foreach (var p in c.nativeParameters) Add(p.key, p.value);
            }
            else
            {
                Add("流程资产", c.ActiveFlow);
                foreach (var stage in c.GetStageEntries().Where(s => s != null))
                    Add(stage.label, (c.IsStageEnabled(stage.id) ? "启用" : "关闭") + " · " + stage.kind +
                        (stage.formula ? " · " + AssetPath(stage.formula) : "") +
                        (stage.conditionChance ? " · 条件 " + AssetPath(stage.conditionChance) : ""));
            }
        }
        return info;
    }

    private static string AssetPath(UnityEngine.Object asset) => asset ? AssetDatabase.GetAssetPath(asset) : "";

    private static string Record(string guid)
    {
        var path = AssetDatabase.GUIDToAssetPath(guid);
        var graph = AssetDatabase.LoadAssetAtPath<GraphObject>(path);
        if (!graph || graph.RootSet == null) throw new InvalidDataException("无法读取技能图 " + path);
        var original = graph as OriginalSkillGraph;
        var actions = graph.RootSet.Actions.Select(a => Describe(graph, a)).ToArray();
        var db = GameManager.OverTimeEffectData;
        var info = new AssetInfo {
            guid = guid, path = path, name = graph.name, original = original, enemy = original ? AssetPath(original.previewEnemy) : "",
            character = graph.libraryCharacter ? graph.libraryCharacter.name : "",
            camp = graph.libraryCharacter ? (graph.libraryCharacter.camp == SkillLibraryCharacter.Camp.Friendly ? "友方" : "敌方") : "未归属角色",
            actions = actions, unwiredHit = graph.RootSet.Actions.Where((a, i) => actions[i].onContact)
                .Any(a => a.outputPort.Count == 0 || a.outputPort[0].uid.Count == 0),
            levels = graph.RootSet.Actions.Where(a => a.ClassName == "FormulaImpact").Select(a => a.calculation?.levels)
                .Where(t => t).Distinct().Select(t => new Table { path = AssetPath(t), rows = t.rows }).ToArray(),
            statuses = db ? db.overTimeEffects.Where(e => e.DesignGraph == graph).Select(e => new Field(e.name + " · " + e.uid,
                (e.permanent ? "永久" : e.duration + " 秒") + (e.layered ? " · 最多 " + e.maxLayer + " 层" : "") +
                (e.interruptCastOnEnter ? " · 打断施法" : ""))).ToArray() : Array.Empty<Field>()
        };
        return "{\"asset\":" + JsonUtility.ToJson(info) + ",\"graph\":" + EditorJsonUtility.ToJson(graph) + "}";
    }

    [MenuItem("Tools/技能工作台/同步全部技能至 H5 编辑器")]
    public static void Export()
    {
        var records = SkillIds().Select(Record);
        string data = "// Generated by OriginalSkillEditorExport.\nwindow.unitySkillCatalog = [\n" +
            string.Join(",\n", records) + "\n];\n";
        const string referencePath = "Assets/Editor/宗师之上伤害核对.json";
        var reference = AssetDatabase.LoadAssetAtPath<TextAsset>(referencePath);
        if (reference) data += "window.unityDamageReference = " + reference.text.TrimStart('\uFEFF').Trim() + ";\n";
        string folder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "docs");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "unity-skill-catalog.js");
        if (File.Exists(path) && File.ReadAllText(path) == data) return;
        string temp = Path.Combine(folder, Path.GetRandomFileName());
        try
        {
            File.WriteAllText(temp, data, new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    internal static void Queue()
    {
        if (queued) return;
        queued = true;
        EditorApplication.delayCall += () => { queued = false; Export(); };
    }

    internal static bool InCatalog(string path) =>
        path == Catalog || path.StartsWith(Catalog + "/", StringComparison.Ordinal);
}

internal sealed class OriginalSkillEditorSave : AssetModificationProcessor
{
    private static string[] OnWillSaveAssets(string[] paths)
    {
        if (paths.Any(Relevant)) OriginalSkillEditorExport.Queue();
        return paths;
    }

    internal static bool Relevant(string path) => path.StartsWith("Assets/", StringComparison.Ordinal) &&
        (path == "Assets/Editor/宗师之上伤害核对.json" || path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".controller", StringComparison.OrdinalIgnoreCase) ||
         path.EndsWith(".overrideController", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase) ||
         path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase));
}

internal sealed class OriginalSkillEditorImport : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] oldPaths)
    {
        // Moving dependencies may change the path shown for a clip or prefab.
        if (imported.Concat(deleted).Concat(moved).Concat(oldPaths).Any(OriginalSkillEditorSave.Relevant))
            OriginalSkillEditorExport.Queue();
    }
}
