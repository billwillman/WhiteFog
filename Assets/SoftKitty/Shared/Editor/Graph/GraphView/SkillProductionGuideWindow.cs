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
            AddStep(content, "2. 登记角色并绑定 MCC 实体", "Project 窗口右键 → Create → Soft Kitty → 技能库角色",
                "新建角色资产，命名并在 Inspector 选择阵营。回到工作台「角色属性」，选择同一阵营和角色。敌方角色可把美术 Prefab 拖入「敌位预览模型」。\n" +
                "在「MCC 实体 UID」选择已有实体。要创建独立实体，保持「未绑定」，填写唯一的「新实体 UID」，点「创建实体并绑定」。例如，独臂木侠绑定的实体 UID 是 DubiMuxia。");
            AddStep(content, "3. 填角色战斗数值", "角色属性 · 属性定义",
                "在「角色属性」填写攻击 atk、防御 def、生命上限 hp 和当前生命 chp。例如角色满血出场且生命上限为 100，就把 hp 和 chp 都填成 100。需要新的属性字段时，先在「属性定义」创建，再回「角色属性」点「添加属性」。");
            AddStep(content, "4. 给角色创建技能并安排动作", "技能编辑 → 技能库 → 选择阵营和角色 → 新建技能",
                "选「空白技能」并保存，工作台会填好「所属角色」。沿用已有招式时，可打开「敌方 → 独臂木侠 → 102410 普攻」，点「复制技能」编辑副本。单独建图的入口是 Project 窗口右键 → Create → Soft Kitty → 技能图。\n" +
                "在时间轴点「添加动作 → 播放角色动画」，选择 Animator State 或 Animation Clip。找到抬手、挥剑等实际出手时刻，用它安排下一步的发射或直接结算。");
            AddStep(content, "5. 制作并发射投射物（飞剑、火球等）", "Project 窗口右键 → Create → Soft Kitty → 投射物配置",
                "在 Project 选好保存目录，新建配置并命名（例如「飞剑」）。选中它，在 Inspector 填飞行特效 Prefab、发射挂点、初速度和命中盒，按需设置拖尾。已有合适的配置可直接复用。\n" +
                "回到技能时间轴，在出手时刻点「添加动作 → 产生投射物」，把「飞剑」资产拖入节点的「投射物配置」。也可在节点点「新建投射物配置」，保存后会自动绑定。\n" +
                "「打开投射物 Asset」修改共用配置；只改本次发射就点字段旁的「复写」，需要复用改后的版本时点「另存投射物配置」。\n" +
                "如果要做 MCC 原版的场景伤害对象，在 Hierarchy 空白处右键 → Soft Kitty → Create Damage Object，再到 CombatDamage → Projectile 选择驱动方式。");
            AddStep(content, "6. 连接命中并配置伤害", "技能编辑 → 选中「产生投射物」→ 添加命中后执行的动作 → 分段命中与公式结算",
                "在飞剑的命中口添加结算动作，命中方式选「直接结算」。飞剑碰到谁，就对谁结算。近战招式可以在时间轴出手时刻直接添加这个动作。\n" +
                "若想用一个节点完成发射和结算，直接在时间轴添加「分段命中与公式结算」，命中方式选「产生投射物」，绑定投射物配置即可。两种接法选一种，以免重复发射。\n" +
                "在结算动作的「流程资产」选择已有伤害流程；按技能等级取值时，再绑定「技能等级表」。需要自建时，在 Project 窗口右键 → Create → Soft Kitty 下分别选「技能伤害流程」「数值公式」或「技能等级表」。把公式填进对应流程环节，在「公式与数值」试算后设为运行时输出。");
            AddStep(content, "7. 需要灼烧等状态时，创建 Buff 并接到技能", "Buff 状态 → 从 Buff 模板新建",
                "先打开「选择状态」查找已有 Buff。要新增灼烧，可选「持续伤害」模板，填写唯一的「新状态 UID」和显示名，点「创建状态并引用模板图」。设置持续时间和每跳伤害，点「保存状态库」。新建整个状态库的入口是 Project 窗口右键 → Create → Soft Kitty → Data Objects → Over Time Effect Data，建好后在 Soft Kitty Data Settings 绑定。\n" +
                "回到技能编辑。碰到目标就施加状态，在投射物节点点「添加命中后施加 Buff」；要求这段伤害命中才施加，在结算节点点同一按钮，再选择刚才的 Buff。使用 MCC 原生伤害图时，把状态动作接到图内的命中成功分支。");
            AddStep(content, "8. 保存并试放验收", "技能编辑 → 保存；Preview → 快速试放",
                "选择友方、敌方和出招方向。例如让敌方独臂木侠向主角出招，依次检查出手动作、发射挂点、实际受击者、命中次数、生命变化和 Buff 层数。在相同条件下点「再次试放所选技能」，核对结果。\n" +
                "有参考战报时，可在 Project 窗口右键 → Create → Soft Kitty → 单次施法伤害记录，填好记录后拖入「对照战报（可选）」。正式角色初值在「角色属性」修改，「临时基础值」只用于本次试放。");
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
