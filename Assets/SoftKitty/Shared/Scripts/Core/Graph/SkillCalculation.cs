using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoftKitty
{
    [Serializable]
    public sealed class SkillCalculation
    {
        // Keep the numeric values of previously published editor assets.
        public enum Mode { Automatic = 0, SharedFlow = 1, NativeGraph = 2, Legacy = 3 }
        public Mode mode;
        public GraphObject nativeGraph;
        public List<SkillLevelTable.Field> nativeParameters = new List<SkillLevelTable.Field>();
        public SkillFormula accuracy, damage, settlement;
        public SkillLevelTable levels;
        [UnityEngine.Serialization.FormerlySerializedAs("reportLogicIndex")]
        public int logicIndex = 1;
        public bool independentSegment;
        public SkillDamageFlow flow;
        public List<string> disabledStageIds = new List<string>();
        public List<float> segmentPercent = new List<float> { 100 };
        private enum FlowSource { Unspecified, FlowAsset, NodeAttachments }
        [SerializeField] private FlowSource flowSource;
        [SerializeField] private SkillDamageFlow linkedStageAsset;
        [SerializeField] private List<string> disabledLinkedStageIds = new List<string>();
        [NonSerialized] private SkillDamageFlow.Stage[] cachedLinkedStages;

        public Mode EffectiveMode => mode == Mode.NativeGraph ? Mode.NativeGraph : Mode.SharedFlow;
        public bool UsesLinkedAssets
        {
            get
            {
                if (mode == Mode.Legacy) return true;
                if (flowSource != FlowSource.Unspecified) return flowSource == FlowSource.NodeAttachments;
                // Old native-mode assets had no stored return selection. Prefer an existing flow;
                // if their only formula data is on the action, preserve it when returning.
                return !flow && (mode == Mode.Automatic || mode == Mode.NativeGraph && (accuracy || damage || settlement));
            }
        }
        public SkillDamageFlow ActiveFlow => UsesLinkedAssets ? linkedStageAsset : flow;
        public List<string> ActiveDisabledStageIds => UsesLinkedAssets
            ? disabledLinkedStageIds ?? (disabledLinkedStageIds = new List<string>())
            : disabledStageIds ?? (disabledStageIds = new List<string>());
        public bool ReadsDamageReport => EffectiveMode == Mode.SharedFlow && ActiveFlow && ActiveFlow.stages != null &&
            ActiveFlow.stages.Count > 0 && ActiveFlow.stages[0]?.kind == SkillDamageFlow.Kind.ReportedResult;

        public bool IsStageEnabled(string id) => !ActiveDisabledStageIds.Contains(id);

        public void SetStageEnabled(string id, bool enabled)
        {
            var disabled = ActiveDisabledStageIds;
            if (enabled) disabled.RemoveAll(key => key == id);
            else if (!disabled.Contains(id)) disabled.Add(id);
        }

        public void SetMode(Mode newMode)
        {
            if (newMode != Mode.SharedFlow && newMode != Mode.NativeGraph)
                throw new ArgumentException("请选择伤害流程或 MCC 原生图。", nameof(newMode));
            flowSource = UsesLinkedAssets ? FlowSource.NodeAttachments : FlowSource.FlowAsset;
            mode = newMode;
        }

        public void SetActiveFlow(SkillDamageFlow asset)
        {
            bool keepAttachments = UsesLinkedAssets;
            flowSource = keepAttachments ? FlowSource.NodeAttachments : FlowSource.FlowAsset;
            if (keepAttachments) linkedStageAsset = asset;
            else flow = asset;
            if (mode != Mode.NativeGraph) mode = Mode.SharedFlow;
        }

        public IReadOnlyList<SkillDamageFlow.Stage> GetStageEntries()
        {
            var activeFlow = ActiveFlow;
            if (activeFlow) return activeFlow.stages;
            if (!UsesLinkedAssets) return Array.Empty<SkillDamageFlow.Stage>();
            if (cachedLinkedStages == null || cachedLinkedStages[0].formula != damage ||
                cachedLinkedStages[2].formula != accuracy || cachedLinkedStages[3].formula != settlement)
                cachedLinkedStages = SkillDamageFlow.FromNodeAttachments(this);
            return cachedLinkedStages;
        }

        public static bool IsNativeParameterReserved(string key) => key != null && (key == "skill.level" ||
            key.StartsWith("level.", StringComparison.Ordinal) || key.StartsWith("hit.", StringComparison.Ordinal));

        public void ValidateFlow()
        {
            if (EffectiveMode == Mode.NativeGraph)
            {
                if (!nativeGraph || nativeGraph is SkillFormula || nativeGraph is OriginalSkillGraph || nativeGraph.RootSet?.ClassName != "Combat")
                    throw new InvalidOperationException("请选择 MCC Combat 伤害图。");
                if (nativeParameters != null)
                {
                    var keys = new HashSet<string>();
                    foreach (var p in nativeParameters)
                        if (p == null || string.IsNullOrWhiteSpace(p.key) || IsNativeParameterReserved(p.key) ||
                            !keys.Add(p.key) || float.IsNaN(p.value) || float.IsInfinity(p.value))
                            throw new InvalidOperationException("原生图参数为空、重复、受上下文管理或数值无效：" + p?.key);
                }
                return;
            }
            if (!ActiveFlow && !UsesLinkedAssets) throw new InvalidOperationException("请选择共享伤害流程。");
            if (!ActiveFlow && !levels) throw new InvalidOperationException("请绑定伤害流程所需的等级表。");
            var stages = GetStageEntries();
            if (ReadsDamageReport)
            {
                if (logicIndex < 1 || stages.Count != 2 || stages[1]?.kind != SkillDamageFlow.Kind.ApplyHealth ||
                    string.IsNullOrWhiteSpace(stages[0].id) || string.IsNullOrWhiteSpace(stages[1].id) ||
                    stages[0].id == stages[1].id || !IsStageEnabled(stages[0].id) || !IsStageEnabled(stages[1].id))
                    throw new InvalidOperationException("旧回放流程需要战报实扣、生命写回和有效段号。");
                return;
            }
            if (stages == null || stages.Count < 2 || stages[0]?.kind != SkillDamageFlow.Kind.BaseDamage ||
                stages[stages.Count - 1]?.kind != SkillDamageFlow.Kind.ApplyHealth)
                throw new InvalidOperationException("伤害流程必须从基础伤害开始，以写入生命结束。");
            var ids = new HashSet<string>();
            var kinds = new HashSet<SkillDamageFlow.Kind>();
            bool sideEffects = false;
            foreach (var stage in stages)
            {
                if (stage == null || string.IsNullOrWhiteSpace(stage.id) || !ids.Add(stage.id))
                    throw new InvalidOperationException("伤害环节标识为空或重复。");
                if (stage.kind == SkillDamageFlow.Kind.ReportedResult || !Enum.IsDefined(typeof(SkillDamageFlow.Kind), stage.kind) ||
                    (!kinds.Add(stage.kind) && stage.kind != SkillDamageFlow.Kind.ModifyDamage))
                    throw new InvalidOperationException("伤害环节类型无效或重复：" + stage.label);
                if (!IsStageEnabled(stage.id))
                {
                    if (stage.kind == SkillDamageFlow.Kind.BaseDamage || stage.kind == SkillDamageFlow.Kind.ApplyHealth)
                        throw new InvalidOperationException("基础伤害和写入生命必须参与本招计算。");
                    continue;
                }
                if (sideEffects && stage.kind == SkillDamageFlow.Kind.HitChance)
                    throw new InvalidOperationException("请将命中检定排在易伤和耗盾之前。");
                if (stage.kind == SkillDamageFlow.Kind.Vulnerability || stage.kind == SkillDamageFlow.Kind.Shield)
                    sideEffects = true;
                if ((stage.kind == SkillDamageFlow.Kind.BaseDamage || stage.kind == SkillDamageFlow.Kind.HitChance ||
                    stage.kind == SkillDamageFlow.Kind.ModifyDamage) && !stage.formula)
                    throw new InvalidOperationException("参与计算的环节缺少公式：" + stage.label);
            }
        }

        internal IReadOnlyList<SkillDamageFlow.Stage> ActiveStages()
        {
            var stages = GetStageEntries();
            if (ActiveDisabledStageIds.Count == 0) return stages;
            var active = new List<SkillDamageFlow.Stage>(stages.Count);
            foreach (var stage in stages)
                if (IsStageEnabled(stage.id)) active.Add(stage);
            return active;
        }

        public RootRuntimeState Capture(RootRuntimeState source, Entity caster, Entity target, int level)
        {
            var context = source;
            context.InvolvedEntityDic = new Dictionary<string, Entity>(source.InvolvedEntityDic);
            context.InvolvedEntityDic["Dealer"] = caster;
            context.InvolvedEntityDic["Target"] = target;
            context.DynamicVariableDic = new Dictionary<string, float>(source.DynamicVariableDic);
            context.DynamicVariableDic["skill.level"] = level;
            ValidateFlow();
            if (EffectiveMode == Mode.NativeGraph && nativeParameters != null)
                foreach (var p in nativeParameters) context.DynamicVariableDic[p.key] = p.value;
            if (levels) levels.Read(level, context.DynamicVariableDic);
            return context;
        }

        public void ValidateShares(int count)
        {
            if (independentSegment && logicIndex < 1) throw new InvalidOperationException("逻辑段号须从 1 开始。");
            if (segmentPercent == null || segmentPercent.Count != count) throw new InvalidOperationException("段占比数量应等于判定次数 " + count);
            double sum = 0;
            foreach (float percent in segmentPercent)
            {
                if (float.IsNaN(percent) || float.IsInfinity(percent) || percent < 0 || (!independentSegment && percent > 100))
                    throw new InvalidOperationException(independentSegment ? "分段倍率须为有限的非负百分数。" : "各段占比应为 0 到 100 的数值。");
                sum += percent;
            }
            if (!independentSegment && Math.Abs(sum - 100) > .005) throw new InvalidOperationException("各段占比之和应为 100%。");
        }
    }
}
