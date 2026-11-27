using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SoftKitty
{
    internal sealed class SkillProductionGuideWindow : EditorWindow
    {
        internal static void Open()
        {
            var window = GetWindow<SkillProductionGuideWindow>(true, "角色与技能制作流程");
            window.minSize = new Vector2(460, 360);
        }

        public void CreateGUI()
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            rootVisualElement.Add(scroll);
            var content = scroll.contentContainer;
            content.style.maxWidth = 780;
            content.style.paddingLeft = content.style.paddingRight = 16;
            content.style.paddingTop = 12;
            content.style.paddingBottom = 16;

            AddStep(content, "1. 美术交付角色", "Project 窗口",
                "导入模型、材质、骨骼和动作，制作角色 Prefab，配好 Animator 并试播攻击动作。需要从剑尖或手掌发射特效时，把挂点名称交给技能制作者。可参考「独臂木侠」角色 Prefab。");
            AddStep(content, "2. 登记角色并绑定 MCC 实体", "Project 窗口 → Create → Soft Kitty → 技能库角色",
                "新建角色资产，命名并在 Inspector 选择阵营。回到工作台「角色属性」，选择同一阵营和角色。敌方角色可把美术 Prefab 拖入「敌位预览模型」。\n" +
                "在「MCC 实体 UID」选择已有实体。要创建独立实体，保持「未绑定」，填写唯一的「新实体 UID」，点「创建实体并绑定」。例如，独臂木侠绑定的实体 UID 是 DubiMuxia。");
            AddStep(content, "3. 填角色战斗数值", "角色属性 · 属性定义",
                "在「角色属性」填写攻击 atk、防御 def、生命上限 hp 和当前生命 chp。例如角色满血出场且生命上限为 100，就把 hp 和 chp 都填成 100。需要新的属性字段时，先在「属性定义」创建，再回「角色属性」点「添加属性」。");
            AddStep(content, "4. 给角色创建技能", "技能编辑 → 选择阵营和角色 → 新建技能",
                "从头制作时，选择「空白技能」并保存。先选角色再新建，工作台会填好「所属角色」。沿用已有招式时，可以打开「敌方 → 独臂木侠 → 102410 普攻」，点「复制技能」并编辑副本。");
            AddStep(content, "5. 配动作、命中和伤害", "技能编辑 · 公式与数值 · Buff 状态",
                "在时间轴点「添加动作」，选择「播放角色动画」，绑定 Animator State 或 Animation Clip。投射物技能再加「产生投射物」，选择或新建「投射物配置」。点「打开投射物 Asset」，在 Inspector 里设置共用的飞行特效、拖尾和命中盒。只调整这次发射时，点对应参数旁的「复写」。要让其他技能复用这些改动，点「另存投射物配置」。\n" +
                "从投射物的命中口接「分段命中与公式结算」，选择「直接结算」，绑定伤害流程和需要的等级表。需要灼烧等状态时，先在「Buff 状态」配置，再点「添加命中后施加 Buff」。接触就施加的 Buff 接投射物命中口，每段结算成功才施加的 Buff 接结算成功分支。");
            AddStep(content, "6. 试放验收", "Preview → 快速试放",
                "选择友方、敌方和出招方向。试独臂木侠的普攻时，让它从敌方向主角出招。检查动作、发射挂点、目标生命变化、命中次数和 Buff 层数。「临时基础值」用于本次试放，正式初值在「角色属性」修改。");
        }

        private static void AddStep(VisualElement content, string title, string location, string instructions)
        {
            var heading = new Label(title);
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            heading.style.fontSize = 15;
            content.Add(heading);
            var path = new Label(location);
            path.style.whiteSpace = WhiteSpace.Normal;
            path.style.marginBottom = 4;
            content.Add(path);
            var body = new Label(instructions);
            body.style.whiteSpace = WhiteSpace.Normal;
            body.style.marginBottom = 16;
            content.Add(body);
        }
    }
}
