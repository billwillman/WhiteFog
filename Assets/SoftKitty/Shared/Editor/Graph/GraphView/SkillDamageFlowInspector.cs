using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Kind = SoftKitty.SkillDamageFlow.Kind;

namespace SoftKitty
{
    internal static class SkillDamageHelp
    {
        internal const string Stages = "从上往下结算。普通伤害以「基础伤害」开始，以「扣减生命」结束。中间可加入命中判定、会心或护盾。拖动列表会改变计算顺序，共用此流程的技能都会受到影响。";
        internal const string StageId = "给这一步一个流程内唯一的代号，例如 critical。技能按代号记住启用哪些步骤，随机判定也会用到它。已有环节想改界面名字，请改「显示名称」。代号留空或重复会让流程校验失败。";
        internal const string StageName = "填一个自己看得懂的名字，例如「会心增伤」或「扣除目标防御」。这个名字显示在工作台和节点图上。改名不会改变计算方式。";
        internal const string Condition = "决定这一段伤害要不要执行当前的数值修正。留空表示每次走到这里都执行。需要概率时，选一张数值公式，让它输出 0 到 1。\n\n例如原伤害是 100，修正公式把伤害翻倍。触发概率公式输出 0.3 时，有 30% 的机会变成 200，其余情况沿用 100 继续结算。输出 0 会跳过这一步，输出 1 一定执行。技能是否打中敌人由「命中判定」单独决定。";
        internal const string Output = "结算时取这个节点算出的数值作为整张公式的结果。先在公式页选择「试算输出」，核对数值，再点「把当前节点设为运行时输出」。留空或指向已删除的节点会让公式无法执行。";
        internal const string Kinds = "BaseDamage（基础伤害）算出起始值。SegmentShare（分段占比）分配本段伤害。HitChance（命中判定）决定这一段是否打中。ModifyDamage（数值修正）计算减防或会心等变化。Vulnerability（易伤）读取目标易伤。Shield（护盾）计算吸收量。ApplyHealth（扣减生命）写入结果。ReportedResult（历史战报）读取记录中的扣血值。";

        internal static string Formula(Kind kind)
        {
            switch (kind)
            {
                case Kind.BaseDamage: return "选一张公式计算起始伤害。例如攻击力 100 乘招式倍率 0.7，输出 70，后面的环节从 70 开始算。基础伤害公式必须填写。";
                case Kind.HitChance: return "选一张公式计算这段攻击的命中率，必须填写。输出 0.8 表示 80% 的机会命中。未命中时，这一段停止结算。";
                case Kind.ModifyDamage: return "选一张公式计算修正后的伤害。例如当前伤害 100，公式返回 200，后续环节就从 200 继续算。这个环节必须填写公式，可以另设触发概率。";
                default: return "这一类型由战斗系统直接执行，公式可以留空。分段占比读取本段权重，易伤和护盾读取目标当前状态，扣减生命负责写入结果。";
            }
        }

        internal static VisualElement WithButton(VisualElement control, string title, string text)
            => WithButton(control, title, () => text);

        internal static VisualElement WithButton(VisualElement control, string title, Func<string> explain)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            control.style.flexGrow = 1;
            control.style.flexBasis = 0;
            control.style.minWidth = 0;
            row.Add(control);
            var help = new Button(() => SkillFieldHelpWindow.Open(title, explain())) { text = "?", tooltip = "查看中文说明 · " + title };
            help.style.width = 22;
            help.style.flexShrink = 0;
            help.style.alignSelf = Align.FlexStart;
            row.Add(help);
            return row;
        }

        internal static VisualElement Field(SerializedProperty property, string title, string text)
            => WithButton(new PropertyField(property, title), title, text);

        internal static VisualElement ListInspector(SerializedObject data, string property, string title, string help)
        {
            var root = new VisualElement();
            var script = new PropertyField(data.FindProperty("m_Script"), "资源脚本");
            script.SetEnabled(false);
            root.Add(WithButton(script, "资源脚本", "这里显示定义此类配置的脚本。可以在项目窗口定位它，日常调数值只需编辑下方的配置。"));
            root.Add(Field(data.FindProperty(property), title, help));
            return root;
        }

        internal static Foldout Group(SerializedProperty property, string title)
        {
            var fold = new Foldout { text = title };
            fold.SetValueWithoutNotify(property.isExpanded);
            fold.RegisterValueChangedCallback(e => { if (e.target == fold) property.isExpanded = e.newValue; });
            return fold;
        }
    }

    [CustomEditor(typeof(SkillDamageFlow)), CanEditMultipleObjects]
    internal sealed class SkillDamageFlowInspector : Editor
    {
        public override VisualElement CreateInspectorGUI() =>
            SkillDamageHelp.ListInspector(serializedObject, "stages", "结算环节（Stages）", SkillDamageHelp.Stages);
    }

    [CustomPropertyDrawer(typeof(SkillDamageFlow.Stage))]
    internal sealed class SkillDamageStageDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var name = property.FindPropertyRelative("label");
            var kind = property.FindPropertyRelative("kind");
            var fold = SkillDamageHelp.Group(property, name.stringValue);
            void UpdateTitle(SerializedProperty p) => fold.text = string.IsNullOrEmpty(p.stringValue) ? "伤害环节" : p.stringValue;
            UpdateTitle(name);
            fold.TrackPropertyValue(name, UpdateTitle);
            fold.Add(SkillDamageHelp.Field(property.FindPropertyRelative("id"), "环节标识（ID）", SkillDamageHelp.StageId));
            fold.Add(SkillDamageHelp.Field(name, "显示名称（Label）", SkillDamageHelp.StageName));
            fold.Add(SkillDamageHelp.Field(kind, "环节类型（Kind）", SkillDamageHelp.Kinds));
            const string formulaTitle = "计算公式（Formula）";
            fold.Add(SkillDamageHelp.WithButton(new PropertyField(property.FindPropertyRelative("formula"), formulaTitle), formulaTitle,
                () => kind.hasMultipleDifferentValues ? "基础伤害、命中判定与数值修正各自使用此处的公式。选中单一类型可以查看示例。" : SkillDamageHelp.Formula((Kind)kind.intValue)));
            const string chanceTitle = "触发概率（Condition Chance）";
            fold.Add(SkillDamageHelp.WithButton(new PropertyField(property.FindPropertyRelative("conditionChance"), chanceTitle), chanceTitle, () =>
                (!kind.hasMultipleDifferentValues && (Kind)kind.intValue != Kind.ModifyDamage ? "只有数值修正（ModifyDamage）会读取这个字段，当前类型可以留空。\n\n" : "") + SkillDamageHelp.Condition));
            return fold;
        }
    }
}
