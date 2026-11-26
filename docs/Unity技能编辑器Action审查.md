# Unity 技能编辑器 Action 审查

2026-11-26 · WhiteFog · 团结 1.9.3 · 工程内 MCC 1.03

**18 个 Action 类里，12 个来自 MCC 导入基线，6 个由本项目新增。最先可以收回的是施法提示的事件分发和普通治疗的数值运算。最需要保留的是已出手对象的生命周期、指定资源和骨骼的动画／特效播放，以及跨时刻的战斗结算。** 同一个策划按钮可以生成几个 MCC 节点。是否需要新增运行时类，取决于原图缺少哪一种执行行为。

按 `00cff19` 核对核心导入代码，按 `d1e238d` 识别 Demo。对照了工作台四页、节点图、运行时初始化、四张原作技能图的 `RootSet.Actions`，以及项目治疗模板和 MCC 预设。工作台添加菜单读取接口文件，节点图刷新 Action 列表时扫描脚本，运行时由 `Root` 反射调用 `InitAction`，按节点 UID 将实例放入 `NodeDic`。本次在隔离的团结工程让全部 18 类经过该初始化路径。[工作台添加入口](../Assets/SoftKitty/Shared/Editor/Graph/GraphView/SkillWorkbench.Projectile.cs) · [节点图类型发现](../Assets/SoftKitty/Shared/Editor/Graph/GraphView/MasterGraphWindow.cs) · [MCC 运行时装配](../Assets/SoftKitty/Shared/Scripts/Core/Graph/Root.cs)

## 1. 六个项目 Action 应怎样处理

| 项目 Action | MCC 可以直接承担 | 确需保留的行为，以及工作台的处理建议 |
| --- | --- | --- |
| [SkillSignal](../Assets/SoftKitty/Shared/Scripts/CustomClass/Action/SkillSignalAction.cs) | `Event` 可以向相同接收者发送同名事件。把实体参数接到施放者，Float 取 0、Bool 取 false，即可还原事件。 | 现有节点还会用 `SkillPlayback.Signal` 画带时长、颜色的环。建议“施法提示”做成事件与表现组合，两种输出按需配置。一键模板要避免重复发送事件。 |
| [SkillVital](../Assets/SoftKitty/Shared/Scripts/CustomClass/Action/SkillVitalAction.cs) | 普通回复可用 `Max → Add → Min → AttributeChange.Override`。工程的[持续治疗图](../Assets/WhiteFog/SkillTemplates/持续治疗.asset)已经这样做。单一能量盾也有原生[高级伤害图](../Assets/SoftKitty/MasterCombatCore/Presets/GeneralDamageAdvanced.asset)可供配置。 | 回复仍要保留可交互条件、正确的受术者、施放来源和可选表现。限时盾余额及破盾溢出要统一规则。建议把回复放到原生模板，护盾独立设计；目前它们共用一个枚举。 |
| [SkillImpact](../Assets/SoftKitty/Shared/Scripts/CustomClass/Action/SkillImpactAction.cs) | 距离 Handler、条件分支、定时／重复 Action、属性写回和 `Event` 可以表达固定数值直接命中。新投射技已能通过通用投射物接命中分支。 | 此类还实现发射前取值、飞行接触、易伤、结算延迟、扣血上限和事件。建议列为旧组合兼容，按需要拆成射程条件、发射和伤害结算模板。简单 `AttributeChange.Substract` 单独接上会丢失部分语义。 |
| [FormulaImpact](../Assets/SoftKitty/Shared/Scripts/CustomClass/Action/FormulaImpactAction.cs) | 公式中的加乘、比较、属性输入、随机变量、分支和最终写回均有原生节点。 | 配置资产跨图复用、等级行、每段权重、施放时攻击快照、接触后按实际受击者读防御，以及逐段抽命中率需要执行上下文。保留公式结算能力；投射样式和提示可以交给通用表现／投射节点。各段权重之和、漏段跳过、打断、实际受击者应作为迁移验收项。 |
| [OriginalSkillVisual](../Assets/SoftKitty/Shared/Scripts/CustomClass/Action/OriginalSkillVisualAction.cs) | 包含多种完全不同的事件。原生 `Animation` 支持按 State 名播放，`Instantiate` 支持生成资源、跟随实体；时间标记可以作为时间轴元数据。 | 建议保留旧图加载适配，创作入口按动画、挂点特效、投射物、标记区分。单独选 Clip、跨层 State、只跟位置、跟骨骼旋转、独立停止和接触回调各有具体差异。逐阶段见下节。 |
| [Projectile](../Assets/SoftKitty/Shared/Scripts/CustomClass/Action/ProjectileAction.cs) | 原生 `CombatDamage` 已有发射、脚本／物理驱动、寻敌、速度曲线、物理层、标签筛选、同实体命中冷却和命中销毁；实施比较时也应包含它的预制体路径。 | 本项目还要求定向盒在时间窗内连续扫掠、先筛资格再选最近受击者、锁定实体身份、发射后独立于施法，以及按接触位置运行后续图。保留这一层，继续用 MCC 受击实体、标签和图端口。由原生发射器驱动的预制体应只有一个运动及命中执行者。 |

### 迁移前四招的依赖

团结 AssetDatabase 实际加载到 **29 个动作，全部序列化为 `OriginalSkillVisual`**。其中 4 个动画、1 个施法特效、1 个武器特效、4 次发射、11 个轨道特效，另有 8 个收招／位移标记。扫描范围是四张原作图的有效动作数组；序列化引用副本和可选类型清单不计入实例数。

| 原作技能 | 动作数 | 迁移时必须比对 |
| --- | ---: | --- |
| [基础剑法](../Assets/WhiteFog/Skills/宗师之上/基础剑法.asset) · 21020 | 6 | 动画、两条轨道特效，0.317 秒发射一次。 |
| [流星剑雨](../Assets/WhiteFog/Skills/宗师之上/流星剑雨.asset) · 25120 | 9 | 动画和六条轨道特效，按各自行时、目标和挂点播放。 |
| [玄铁剑法-下](../Assets/WhiteFog/Skills/宗师之上/玄铁剑法-下.asset) · 102412 | 6 | 怪物施放，三条轨道特效；施放者和受术者需要按图绑定。 |
| [破空剑法](../Assets/WhiteFog/Skills/宗师之上/破空剑法.asset) · 25090 | 8 | 动画、施法特效、右手挂接；1.120、1.251、1.443 秒各发一剑。 |

| 旧阶段 | 执行行为 | 替代时的边界 |
| --- | --- | --- |
| 0 播放角色动画 | 读动作绑定或旧图 `castMotion`；State 走指定层和过渡，Clip 走 Playables。 | 原生动画动作在按 UID 找到的实例根组件上按名称播放。简单 State 可复用；子 Animator、实例身份、Clip 引用、层和混合参数需要动画适配。工作台可共用一个动画配置入口。[原生播放](../Assets/SoftKitty/Shared/Scripts/CustomClass/Action/AnimationAction.cs) · [项目绑定](../Assets/SoftKitty/Shared/Scripts/Core/Graph/SkillMotion.cs) |
| 1 施法特效、2 武器特效 | 按角色根／武器骨骼放置，旧武器特效挂到骨骼并按配置销毁。 | `Instantiate` 的路径和实体父挂接可处理简单特效。指定骨骼、局部挂点、只跟位置与寿命需要适配。[原生实例化](../Assets/SoftKitty/Shared/Scripts/CustomClass/Action/InstantiateAction.cs) |
| 3 发射 | 旧节点已委托 `ProjectileAction.Schedule`，无须为每种飞剑增加执行器。 | 资产迁移时保留原资源、发射时刻、初速、加速度、命中后连线或预览回调。 |
| 4 收招、5 位移标记 | 两者在该 Action 的执行分支里都没有位移或收招调用。 | 可转为保留原始时点的注释／标记；将阶段 5 自动翻译成 `Transform` 会凭空加一次位移。 |
| 6 轨道特效 | 读取轨道配置，决定挂在谁身上及如何跟随。旧配置通过 `onTarget` 推导 `triggerHit`；提供回调时才会在命中盒检查后调用它。 | 挂目标特效、实际接触、伤害结算应有各自的配置。转为通用挂点特效时，需单独迁移原有的命中回调时机。[阶段执行器](../Assets/SoftKitty/Shared/Scripts/CustomClass/Action/OriginalSkillVisualAction.cs) |

这四张图的动作上均未保存命中下游连线。对发射节点，工作台在没有连线时可注入“每段预览来伤”；旧轨道特效也可调用这个回调。原作试放中的飘字能证明预览发生了生命变化，不能单独证明四张资产已接入正式伤害公式。[试放注入](../Assets/SoftKitty/Shared/Editor/Graph/GraphView/SkillWorkbench.Demo.cs) · [发射节点的命中输出](../Assets/SoftKitty/Shared/Scripts/CustomClass/Action/ProjectileAction.cs)

## 2. 十二个基线 Action 的去留

下表以执行类 `.cs` 和导入提交逐文件比对。**9 个执行类一致，3 个已有项目改动**。接口汉化和编辑器展示单独计。

| MCC Action | 本地执行内容 | 审查结论 |
| --- | --- | --- |
| [Animation](../Assets/SoftKitty/Shared/Scripts/CustomClass/Action/AnimationAction.cs) | Legacy `Animation.Play(clipName)` 或 Mecanim `Animator.Play(stateName)`。 | 保留。简单名称播放只需资源／状态选择器；要播任意绑定 Clip 则需 Playables 或等效运行时适配。 |
| [AttributeChange](../Assets/SoftKitty/Shared/Scripts/CustomClass/Action/AttributeChangeAction.cs) | 覆盖、加、乘、减。项目在减法分支加入 `SkillDefence.Incoming`。 | 保留原生写属性。通用减属性与伤害结算要有明确边界，特别是护盾、过量伤害和事件。 |
| [CustomData](../Assets/SoftKitty/Shared/Scripts/CustomClass/Action/CustomDataAction.cs) | 按数据库字段类型写实体自定义数值、布尔值及列表项。 | 保留，适合可命名的角色数据；同一实体上的并发施法仍需各自的临时上下文。 |
| [Destroy](../Assets/SoftKitty/Shared/Scripts/CustomClass/Action/DestroyAction.cs) | 按实体 UID 找场景对象，延时销毁 GameObject。 | 保留给实体销毁。清理临时 VFX 需要本次生成的实例引用；具有多个同 UID 实例时还要验证身份。 |
| [Event](../Assets/SoftKitty/Shared/Scripts/CustomClass/Action/EventAction.cs) | 向实体发送命名事件，支持 Float、Entity、Bool 参数。 | 直接复用。工作台“打断施法”快捷入口已创建 `Event` 并填入统一事件名，可以作为其他快捷模板的范例。 |
| [Instantiate](../Assets/SoftKitty/Shared/Scripts/CustomClass/Action/InstantiateAction.cs) | 从 Resources 路径加载，设置父级、坐标、角度、缩放、激活和可选消息。 | 普通生成直接复用。对 Resources 内的资源，编辑器可从资源选择器写回路径；任意目录的 Prefab 引用、骨骼和特效实例寿命有额外运行时要求。 |
| [OverTimeEffect](../Assets/SoftKitty/Shared/Scripts/CustomClass/Action/OverTimeEffectAction.cs) | 给实体施加状态、驱散一层或强制移除状态。 | 用同一状态库与效果图制作持续伤害、回复、控制。状态的持续、刷新、延长和叠层由实体及定义负责。 |
| [PlaySound](../Assets/SoftKitty/Shared/Scripts/CustomClass/Action/PlaySoundAction.cs) | 从 Resources 取 AudioClip，按音量播放 2D 或实体位置上的 3D 音效。 | 直接复用。给既有资源加选择器属于编辑器易用性工作。 |
| [SendMessage](../Assets/SoftKitty/Shared/Scripts/CustomClass/Action/SendMessageAction.cs) | 对实例组件按方法名发送消息，可带数值、实体或执行器 Transform。 | 保留作组件集成。策划侧常见的角色事件优先使用带明确实体输入的 `Event`。 |
| [Teleport](../Assets/SoftKitty/Shared/Scripts/CustomClass/Action/TeleportAction.cs) | 传送至坐标或参照实体的偏移位置。项目加了实例查找与移动后可选打断。 | 延用原节点；击退或传送是否打断是选择项，打断后清理仍走公共入口。 |
| [TempAttributeChange](../Assets/SoftKitty/Shared/Scripts/CustomClass/Action/TempAttributeChangeAction.cs) | 按持续时间、运算和来源标识调用实体的临时属性修正。 | 限时攻防、移速等可先走它。扣到零的护盾余额还需要命中时消费。 |
| [Transform](../Assets/SoftKitty/Shared/Scripts/CustomClass/Action/TransformAction.cs) | 移动、旋转、缩放。项目加了实例查找和实际位移后可选打断。 | 保留并优先修正现有移动路径。实测距离偏差见下节，另开“击退 Action”会把同一问题留在原节点。 |

## 3. 容易误判的执行边界

**能量盾。** 原生 `GeneralDamageAdvanced` 的 `Node63` 查目标 `shield > 0`，`Node61` 选择 `Node62` 扣盾或 `Node1` 扣血，两个分支使用 `Node50` 的伤害。该分支没有把同一击的溢出值再交给生命。项目的 `SkillDefence.Incoming` 会马上消耗 `min(盾余额, 来伤)` 并返回余量。例如盾 10、来伤 25，后者交给生命的余量为 15。基础单盾也可以用 MCC 的属性、Min/Max 和分支表达，需确保所有受击入口读取同一份余额，并明确读取和写回顺序。限时清除、同一击多次读取、多个独立盾和驱散归属要按各自规则验证。目前项目实现每个生命属性只有一份盾余额。[原生预设](../Assets/SoftKitty/MasterCombatCore/Presets/GeneralDamageAdvanced.asset) · [项目盾余额](../Assets/SoftKitty/MasterCombatCore/Scripts/Core/SkillDefence.cs)

**属性变化、命中事件、飘字。** `AttributeChange.Substract` 调用 `Incoming` 后直接写属性，没有按剩余 HP 截断，也没有自动发送 `Damage`；`SkillPlayback.ApplyIncomingDamage` 会限制实际损失并发送事件。通用扣属性和“造成伤害”应分别保留语义，数值运算和外部观测要使用同一个实际损失。Demo 血条订阅 `Damage/Heal` 事件并使用对象池，工作台的飘字订阅 `chp` 的负向变化。在同一预览接两种显示前，应选定一个显示来源。[原生血条事件](../Assets/SoftKitty/MasterCombatCore/Demo/Scripts/UI/HealthBarUi.cs) · [工作台飘字](../Assets/SoftKitty/MasterCombatCore/Scripts/Core/SkillDamageNumbers.cs) · [项目来伤入口](../Assets/SoftKitty/MasterCombatCore/Scripts/Core/SkillPlayback.cs)

**Buff 每跳与技能时间轴。** 普通战斗图从 `CombatDamage` 进入 `Root.RunActions` 和 `Action.Run`，可排延迟和重复。`OverTimeEffectInstance` 写入 Dealer、Target、Layer 及状态动态值后走 `Root.ExecuteAllAction`，直接执行当次动作。实测在周期治疗图上把动作设为延迟 2 秒、重复 3 次，手动触发一跳，当次立即只加一次血。依赖 `Run` 排程的 `SkillImpact/FormulaImpact`，以及会取场景 `RootInstance` 的表现动作，都不能据同一菜单标签推定在周期图里有等效行为。状态页应只提供适配该执行入口的动作或明确提示。效果叠层、刷新、驱散继续使用 MCC 数据库。[状态页](../Assets/SoftKitty/Shared/Editor/Graph/GraphView/SkillWorkbench.Buffs.cs) · [周期图执行](../Assets/SoftKitty/MasterCombatCore/Scripts/Graph/OverTimeEffectInstance.cs)

**迁移前的云端位移测量。** 通过工作台图运行 `Transform`，设置世界 X 偏移 4 米、时长 0.5 秒、倍率 1、关闭贴地。隔离预览场一名无 CharacterController 和 Rigidbody 的实体最终移动 **8.16 米**，物理步长 0.02 秒。`MoveCo` 每步按 `fixedDeltaTime / duration` 分摊位移，计时却累加 `duration × fixedDeltaTime`。此外，场景实例调用 `Move` 时固定传入贴地 `true`，与表单勾选值不一致。距离偏差已实测；贴地和局部位移还应分别用带地面、带旋转的场景回归。[TransformAction](../Assets/SoftKitty/Shared/Scripts/CustomClass/Action/TransformAction.cs) · [实体移动协程](../Assets/SoftKitty/Shared/Scripts/Core/Gameplay/EntityComponent.cs)

### 四个编辑页和节点图

| 入口 | 复用 MCC 的部分 | 应放在工作台或项目适配层的部分 |
| --- | --- | --- |
| 技能编辑／节点图 | 同一 Root 的 Trigger、11 类 Handler、条件组、Branch、Action、Entity、Variable、Math 和端口。Action 基线支持单次、延迟、重复与持续。 | 时间轴展示、模板生成、Undo、资源选择、每次执行前的动作条件。区分整招入口、动作前置条件和命中后的分支；`EntityExist` 只判实体存在，存活或可交互需要单独语义。多实例技能应检查按 UID 查对象的旧节点。 |
| Buff 状态 | `OverTimeEffectObject`、实体的增删和周期效果图。原生狂暴、持续伤害和冰冻预设均有实例可查。 | Buff 页编辑同一份定义。项目自有的状态进入时打断沿用通用入口；工作台“吸收护盾”入口供查看高级伤害图，未提供独立多盾回收。 |
| 公式与数值 | 数学节点、属性变量和动态图输入可构成治疗、伤害、暴击与防御公式。 | 独立 `SkillFormula` 资产、技能等级表、输入采样时机、结果输出、分段权重校验及试算。`FormulaImpact` 的三项公式应继续可替换。 |
| Preview | 试放用 `CombatDamage` 绑定实体并进入战斗图。可对照 Demo 的事件、受击体、HUD。 | 快速场、当前场景试放、镜头、动画路径估算、来伤输入和飘字观测。Animator 时间轴按给定参数推算，运行中角色的参数及受控状态仍会改变实际播放。 |

## 4. 初次审查的迁移顺序与验证

1. 先用 `Event` 模板承接 `SkillSignal` 的业务事件，用原生数学图承接 `SkillVital` 的普通回复。对照接收人、来源、负输入、封顶、可交互条件和提示表现。把护盾、伤害、表现拆开验收。
2. 优先修正 `Transform` 与 `MoveCo` 的参数和计时，并为不同时长、局部方向、贴地开关及三种移动组件路径补回归。修正已有节点比增加另一套位移更直接。
3. 用同一来源的资源与时点逐招迁移 `OriginalSkillVisual`；先比较存档重开、骨骼跟随、动画和发射，再比较命中后的实际扣血与事件。旧图保留加载路径，原作表演回放与正式伤害配置分别验收。
4. 合并固定伤害和公式伤害中重复的调度、判定与扣血行为，同时保留原生属性写入及周期图入口。简单技能可导出为原生节点组合；具有快照、跨目标重绑定和独立生命周期的配置继续走必要扩展。

本轮在与工作区关键源码、四张技能图一致的临时 Unity 工程内验证了 18 个 Action 的运行时装配。Play Mode 中用工作台创建图并走 `CombatDamage.PerformAction`，测得 `SkillSignal` 和 `Event` 均向同一受术者发出一次相同参数的事件。25 点回复分别从 95/100 加到 100/100、从 50/100 加到 75/100，`SkillVital` 和原生模板结果一致，施放者生命均保持 80。另走真实周期图执行入口核对单跳，并测量了 `Transform` 的世界位移。四张原作图本轮核对的是加载结果、阶段和连线。其余行为结论来自代码与预设审查；这次未逐参数试放全部 18 类。

## 5. 首轮迁移与工作台复测

四张原作图仍有 29 个时间轴动作。四个起手现在使用 MCC 的 `Animation`，节点上保存各自的 Animator State 绑定和过渡时长。基础剑法的一发及破空剑法的三发使用同一种 `Projectile`，预制体、速度、加速度、命中盒和拖尾参数分别保存在每一发上。保留了节点 UID、发射时刻和执行条件。骨骼跟随、粒子寿命、独立飞行和实际受击体检测仍由项目的表现与碰撞逻辑处理。旧图的表现事件仍可加载。

| 工作台操作 | 生成内容及执行语义 |
| --- | --- |
| 发送施法提示事件 | MCC 的 `Event`，接收者与施法来源分别接线。提示环可单独添加。 |
| 回复生命（限制上限） | MCC 的 `AttributeChange`、实体属性输入及 `Max → Add → Min`。每次执行前检查同一接收者可交互。新建回复技能也使用这套图。 |
| 普通对象和声音 | 沿用 `Instantiate` 与 `PlaySound`，可选择 Resources 中的 Prefab 或 Audio Clip，保存时写回原生资源键。 |
| 固定和公式伤害 | 共用段数规则和实际来伤结算，统一处理护盾吸收、剩余生命下限以及 Damage 事件的数值和来源。 |
| 位移 | MCC 的 `Transform` 将配置的贴地开关传给场景实体。移动按经过的物理时间分摊总偏移，最后一步只走剩余距离。局部偏移按出手时实体朝向换算。 |

### 四招的改前与改后记录

[观看四招工作台复测录屏](previews/宗师之上-四招工作台试放.mp4)（约 12 秒，依次为基础剑法、流星剑雨、玄铁剑法-下、破空剑法）。

在同一个隔离的团结工程、同一工作台快速试放入口分别运行两轮，中间保存并重新载入迁移后的技能图。双方起始距离 8 米，每段预览来伤 1，采样 30 帧/秒。帧号从该次试放开始采样时记为 0。下表列的是受击者生命实际减少的帧。两轮记录的施法与待机片段切换、各特效实例首次出现的帧和扣血帧全部对应。

| 技能 | 实际施法片段 | 观察到的特效实例 | 改前／改后扣血帧 |
| --- | --- | --- | --- |
| 基础剑法 · 21020 | `chr_male_001@jian_jichu_21020` | 武器光 2、飞剑 1、命中特效 1 | 20 ／ 20 |
| 流星剑雨 · 25120 | `chr_male_001@jian_liuxing_25120` | 施法 1、轨迹 1、命中特效 4 | 31、34、35、40、52 ／ 同左 |
| 玄铁剑法-下 · 102412 | `boss_dubi@skill02_102412` | 两种施法特效各 1、命中特效 1，由独臂木侠向主角出手 | 24 ／ 24 |
| 破空剑法 · 25090 | `chr_male_001@jian_pokong_25090` | 武器光 1、施法 1、飞剑和拖尾各 3、命中特效 3 | 42、46、52 ／ 同左 |

### 位移与原生组合的回归

在无障碍场地从非原点出发，设置世界 X 位移 4 米、时长 0.5 秒、物理步长 0.02 秒。普通 Transform、CharacterController 和运动学 Rigidbody 分别移动 4.0001、4.0000、4.0001 米。将实体旋转 90°，分别指定 0.37 秒和 1.21 秒，三条路径的局部 X 都沿世界负 Z 移动 4.0000 米。另在地面之上验证普通 Transform 的贴地开关，关闭时维持高度，开启时落在表面上方 0.01 米。

新建提示事件的接收者、施法来源和发送次数均与旧事件一致。原生回复组合已试过 95/100 加 25、50/100 加 25、负回复量和不可交互目标，结果分别为 100、75、50、50。独立的新建单次回复技能将目标从 50 回复到 80。

另在 Play Mode 复跑 11 项投射物工作台用例和 18 项受击对象用例，全数通过。其中覆盖先碰前排、限定目标提前死亡、出手后打断、命中分支接线，以及对实际受击者结算。
