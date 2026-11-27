# Unity 技能编辑器 Action 审查

2026-11-26 首次审查 · 2026-11-28 更新去留方案 · WhiteFog · 团结 1.9.3 · MCC 1.03

**保留「产生投射物」「改变当前投射物目标」「分段命中与公式结算」。** 简单固定伤害、施法事件和普通回复可逐步迁到原生组合。旧图保留能正常加载和运行的兼容入口。限时吸收盾、提示环和复杂挂点特效仍有各自的运行时行为要保留。

将 MCC 1.03 原始安装包、核心导入提交 `00cff19`、Demo 导入提交 `d1e238d` 与当前工作区对照。旧稿的 18 类运行时装配实测对应首轮 12 个原生 Action 和 6 个项目 Action。本次补审后来增加的 `RedirectProjectile`，合计 7 个项目节点。工作台菜单读接口文件，节点图扫描 Action 脚本，运行时按节点类名调用 `InitAction`。[工作台添加入口](../Assets/SoftKitty/Shared/Editor/Graph/GraphView/SkillWorkbench.Projectile.cs) · [节点图类型发现](../Assets/SoftKitty/Shared/Editor/Graph/GraphView/MasterGraphWindow.cs) · [MCC 运行时装配](../Assets/SoftKitty/Shared/Scripts/Core/Graph/Root.cs)

## 1. 七个项目 Action 的去留

| 扩展节点 | 可用的 MCC 原生节点或组件 | 直接替换会遗漏的行为 | 去留方案与受影响的图 |
| --- | --- | --- | --- |
| **产生投射物** [`Projectile`](../Assets/SoftKitty/Shared/Scripts/CustomClass/Action/ProjectileAction.cs) | `Instantiate` 生成挂有原生 `CombatDamage` 的预制体，可完成普通直飞、追踪、碰撞、标签过滤、对同一实体的命中间隔和命中销毁。 | 当前节点在配置的命中窗口内沿飞行段做定向盒扫掠，先筛资格再取最近接触者，按**实际受击实体与接触位置**执行下游。共用投射物配置、节点逐项复写、出手后独立飞行及落空行为也要保留。 | **保留。** 当前 10 张图、23 个独立发射节点依赖它。破空剑法三发，落花无情剑十一发。使用带原生发射器的预制体时，同一发只由一个执行者负责移动和结算。 |
| **改变当前投射物目标** [`RedirectProjectile`](../Assets/SoftKitty/Shared/Scripts/CustomClass/Action/RedirectProjectileAction.cs) | 原生 `CombatDamage` 的自动寻敌能跟踪目标，`Instantiate` 可再发一枚投射物。 | 需要由一次接触后的图分支指挥**同一枚**投射物继续飞。它携带已命中名单，验证新目标可交互，并独立处理这次接触的后续动作，避免迟到的旧分支改变下一程。再发一枚会切断同一发的记录。 | **保留。** 当前白虹剑、狂风快剑各 1 个。保留同一发先打甲再飞向乙的出招过程，并验证乙提前死亡、回头重复命中和改向延迟。 |
| **分段命中与公式结算** [`FormulaImpact`](../Assets/SoftKitty/Shared/Scripts/CustomClass/Action/FormulaImpactAction.cs) | `Variable` 读取属性，数学节点算倍率及分段，`Handler`、`Branch` 与随机输入做判定，`AttributeChange` 和 `Event` 写回及发送事件。可引用原生 MCC 伤害图。 | 需要绑定等级表和可复用的结算流程，按配置保留施放时施法者属性快照、结算时实际受击者的防御、各段权重和命中判定，以及护盾吸收、扣血和报表。只搬算式会失去这些输入的采样时点和接线语义。 | **保留为统一结算入口。** 当前 14 张图、29 个节点。共享伤害流程或单招专用流程可改用原生公式节点编辑，保留结算动作的上下文。重点复测破空剑法等多发技能和流星剑雨等多段直接命中。 |
| **技能命中** [`SkillImpact`](../Assets/SoftKitty/Shared/Scripts/CustomClass/Action/SkillImpactAction.cs) | 简单固定伤害可用 `Distance`、`Branch`、带延迟或重复的 `Action`、`AttributeChange` 和 `Event` 组成原生图。投射技可用保留的 `Projectile` 接统一结算。 | 旧节点会在动作开始取一次伤害输入供后续段复用，并处理射程、接触后易伤、首段延迟和实际来伤。只接减属性会遗漏对实际受击者结算、盾余额、生命下限和正确的伤害事件。 | **旧图兼容，简单新招用原生组合或统一结算。** 当前图未找到引用。首版八张攻击样例若从历史版本恢复或导入，仍会用到它，名单见下表。 |
| **表现动作** [`OriginalSkillVisual`](../Assets/SoftKitty/Shared/Scripts/CustomClass/Action/OriginalSkillVisualAction.cs) | 起手用 `Animation`，普通特效用带寿命的预制体和 `Instantiate`，发射用保留的 `Projectile`，时间标记保留为时间轴数据。 | 普通生成和动画无法直接覆盖原图中指定骨骼或局部偏移、只跟位置或跟随旋转、粒子生命周期、原作轨道时点和轨道命中盒。把位移标记转为 `Transform` 会额外产生实际位移。 | **保留旧图读取和复杂挂点，普通动作逐步拆分。** 当前 14 张图、59 个节点。工作台的「挂点特效（可配置命中盒）」仍生成这个类。迁移时用基础剑法、流星剑雨、玄铁剑法-下和破空剑法核对时序、挂点和接触回调。 |
| **施法提示** [`SkillSignal`](../Assets/SoftKitty/Shared/Scripts/CustomClass/Action/SkillSignalAction.cs) | 原生 `Event` 负责业务事件，同名事件仍发送给原接收者，实体参数接施法者。固定外观的环可用 `Instantiate` 加自带寿命的特效预制体。 | 只用事件会失去提示环的位置、施法或敌对颜色、显示时长。旧节点可同时发事件和显示环，拆开后要核对仅显示时不发事件、需要发时只发一次。 | **发事件已改用原生组合，保留可配置提示环及旧图兼容。** 当前图未找到引用。首版六张样例用过这个类，名单见下表。 |
| **护盾与回复** [`SkillVital`](../Assets/SoftKitty/Shared/Scripts/CustomClass/Action/SkillVitalAction.cs) | 普通治疗已可用实体属性及 `Max → Add → Min → AttributeChange.Override`，并用可交互条件约束受术者。原生 `GeneralDamageAdvanced` 提供护盾属性和伤害分支，可作搭图参考。 | 普通治疗迁移还需保留施法来源和可选回复提示。限时吸收盾需要一份与结算共用的盾余额、到期规则，以及同一击破盾后的剩余伤害。原生预设的两个分支没有直接体现同一击的溢出。例如盾剩 10、来伤 25，当前项目口径是吸收 10、继续结算余下 15。 | **普通回复用原生组合，保留限时吸收盾和旧图兼容。** 当前图未找到引用。首版清罡护体使用过它。更换盾实现时须核对到期、耗尽和破盾同一击三种情况。 |

[项目扫掠与命中目标](../Assets/SoftKitty/Shared/Scripts/Core/Graph/SkillProjectile.cs) · [原生伤害图预设](../Assets/SoftKitty/MasterCombatCore/Presets/GeneralDamageAdvanced.asset) · [伤害、易伤与盾余额](../Assets/SoftKitty/MasterCombatCore/Scripts/Core/SkillDefence.cs)

### 当前技能图依赖

本次统计工程 `Assets` 中序列化资产的 `RootSet.Actions`。只计实际动作，类型菜单和重复的序列化引用副本不计。下列 14 张图在 [原作技能目录](../Assets/WhiteFog/Skills/宗师之上/)；数字是每张图的有效节点数。

| 技能图 | `Projectile` | `RedirectProjectile` | `FormulaImpact` | `OriginalSkillVisual` |
| --- | ---: | ---: | ---: | ---: |
| 两仪剑 | 1 | 0 | 1 | 4 |
| 基础剑法 | 1 | 0 | 1 | 4 |
| 太极剑法 | 2 | 0 | 2 | 3 |
| 少阳指 | 1 | 0 | 1 | 3 |
| 普攻 | 1 | 0 | 1 | 3 |
| 流星剑雨 | 0 | 0 | 5 | 8 |
| 狂风快剑 | 1 | 1 | 1 | 4 |
| 玄铁剑法-上 | 0 | 0 | 1 | 4 |
| 玄铁剑法-下 | 0 | 0 | 1 | 5 |
| 玄铁剑法 | 1 | 0 | 1 | 4 |
| 白虹剑 | 1 | 1 | 1 | 4 |
| 破空剑法 | 3 | 0 | 3 | 4 |
| 落花无情剑 | 11 | 0 | 9 | 4 |
| 黯然销魂掌 | 0 | 0 | 1 | 5 |
| **合计** | **23** | **2** | **29** | **59** |

### 历史样例依赖

下表按首版九招的 `fadf023` 统计。该批图从历史版本恢复或从旧包导入时仍需对应的兼容类。技能图以外的外部存档或外部包需在迁移前另行核对。

| 旧节点 | 首版中实际引用它的图 | 后续历史 |
| --- | --- | --- |
| `SkillImpact` | 回风裂阵、御剑穿云、星弧连闪、缚影蚀脉、赤纹连灼、幽雾侵蚀、裂石冲撞、雷鳞震翅 | 到 `f0a20e9` 时，以上样例的动作已改走公式结算。 |
| `SkillSignal` | 回风裂阵、御剑穿云、星弧连闪、清罡护体、裂石冲撞、雷鳞震翅 | `f0a20e9` 仍有引用，后续原作样例替换了这批旧图。 |
| `SkillVital` | 清罡护体 | `f0a20e9` 仍有引用，导入这张旧图时要保留限时盾和提示表现。 |

### 历史基线 · 迁移前四招的依赖

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
| 4 收招、5 位移标记 | 两者在该 Action 的执行分支里都没有位移或收招调用。 | 工作台现将两类时点列在只读的「原作时间参考」，手动制作技能无需配置。旧图保留原序列化值。将阶段 5 自动翻译成 `Transform` 会凭空加一次位移。 |
| 6 轨道特效 | 读取轨道配置，决定挂在谁身上及如何跟随。旧配置通过 `onTarget` 推导 `triggerHit`；提供回调时才会在命中盒检查后调用它。 | 挂目标特效、实际接触、伤害结算应有各自的配置。转为通用挂点特效时，需单独迁移原有的命中回调时机。[阶段执行器](../Assets/SoftKitty/Shared/Scripts/CustomClass/Action/OriginalSkillVisualAction.cs) |

这四张图的动作上均未保存命中下游连线。对发射节点，工作台在没有连线时可注入“每段预览来伤”；旧轨道特效也可调用这个回调。原作试放中的飘字能证明预览发生了生命变化，不能单独证明四张资产已接入正式伤害公式。[试放注入](../Assets/SoftKitty/Shared/Editor/Graph/GraphView/SkillWorkbench.Demo.cs) · [发射节点的命中输出](../Assets/SoftKitty/Shared/Scripts/CustomClass/Action/ProjectileAction.cs)

## 2. 十二个基线 Action 的首次审查

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

首轮审查在与当时关键源码、四张技能图一致的临时 Unity 工程内验证了 18 个 Action 的运行时装配。Play Mode 中用工作台创建图并走 `CombatDamage.PerformAction`，测得 `SkillSignal` 和 `Event` 均向同一受术者发出一次相同参数的事件。25 点回复分别从 95/100 加到 100/100、从 50/100 加到 75/100，`SkillVital` 和原生模板结果一致，施放者生命均保持 80。另走真实周期图执行入口核对单跳，并测量了 `Transform` 的世界位移。四张原作图本轮核对的是加载结果、阶段和连线。其余行为结论来自代码与预设审查；这次未逐参数试放全部 18 类。

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
