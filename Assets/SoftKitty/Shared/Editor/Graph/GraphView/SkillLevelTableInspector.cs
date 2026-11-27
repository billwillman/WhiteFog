using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace SoftKitty
{
    [CustomEditor(typeof(SkillLevelTable)), CanEditMultipleObjects]
    internal sealed class SkillLevelTableInspector : Editor
    {
        public override VisualElement CreateInspectorGUI() => SkillDamageHelp.ListInspector(serializedObject, "rows", "技能等级（Rows）",
            "每行是一档技能等级。释放时按等级数字查找对应行，再把该行的参数交给公式。例如 1 级与 2 级可以使用不同的攻击倍率。缺少所需等级或同一等级有两行，读取都会失败。");
    }

    [CustomPropertyDrawer(typeof(SkillLevelTable.Row))]
    internal sealed class SkillLevelRowDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var level = property.FindPropertyRelative("level");
            var fold = SkillDamageHelp.Group(property, "等级 " + level.intValue);
            fold.TrackPropertyValue(level, value => fold.text = "等级 " + value.intValue);
            fold.Add(SkillDamageHelp.Field(level, "技能等级（Level）", "填这行对应的技能等级，例如 1。结算按这个数字查找，与这一行排在第几位无关。一张表中同一等级只填一次。"));
            fold.Add(SkillDamageHelp.Field(property.FindPropertyRelative("fields"), "本级数值（Fields）",
                "每一项是一对参数名和数值。公式需要哪些等级参数，就在这里填哪些。缺少公式要读取的参数，结算会提示缺少输入。"));
            return fold;
        }
    }

    [CustomPropertyDrawer(typeof(SkillLevelTable.Field))]
    internal sealed class SkillLevelValueDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var key = property.FindPropertyRelative("key");
            var fold = SkillDamageHelp.Group(property, "等级参数");
            void UpdateTitle(SerializedProperty p) => fold.text = string.IsNullOrEmpty(p.stringValue) ? "等级参数" : p.stringValue;
            UpdateTitle(key);
            fold.TrackPropertyValue(key, UpdateTitle);
            fold.Add(SkillDamageHelp.Field(key, "参数名（Key）", "填一个本行唯一的参数名。例如此处填 power，公式用 level.power 读取。已有参数改名时，需要同步修改读取它的公式。"));
            fold.Add(SkillDamageHelp.Field(property.FindPropertyRelative("value"), "数值（Value）",
                "填这个参数在当前技能等级下的数。系统会原样传给公式。例如公式用攻击力乘以 level.power，此处填 0.7 就按攻击力的 70% 计算。"));
            return fold;
        }
    }
}
