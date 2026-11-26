# Master Combat Core 源码基线评估与接入建议

> 评估对象：`E:\Tuanjie19_WhiteFog`
>
> 评估日期：2026-11-26
>
> 评估目标：基于实际目录源码，重新判断该工程是否可以作为 Unity / 团结引擎项目的技能 / 战斗框架基线。

---

## 1. 结论先行

`E:\Tuanjie19_WhiteFog` 是一个完整可打开的团结引擎 Unity 工程，不是空壳或纯文档目录。工程内包含：

- 团结引擎 `2022.3.62t11 / 1.9.3`
- URP `14.2.0-t1`
- Master Combat Core 1.03 相关源码
- SoftKitty 自研技能图编辑器与运行时图系统
- Demo 场景、预制体、技能图资产、说明文档
- AssetBundle 加载骨架

因此，之前的判断需要从“第三方插件是否可用”调整为：

> 这是一个已经做过团结引擎迁移、具备完整源码和 Demo 验证的战斗节点图框架基线。

但最终结论仍然要分场景：

| 场景 | 结论 | 说明 |
|---|---|---|
| 单机 PVE / 客户端权威战斗 | **推荐进入 PoC** | 节点图、技能编辑、Buff / DoT、伤害公式体系已经比较完整 |
| 带热更的商业客户端 | **条件可用** | 源码基础不错，但 HybridCLR / xLua / ILRuntime 未接入，需要改造 |
| 强 PVP / MMO / 服务端权威 / 帧同步 | **不建议直接作为核心框架** | 网络同步、确定性、服务端复用基本为空白 |
| 买量 F2P、高频技能迭代 | **谨慎推荐** | 编辑器体验是优势，但运行时要保持轻量，避免框架过重 |

综合评分：

```text
单机 / 客户端战斗基线：        8 / 10
带热更的商业客户端基线：       7 / 10
强 PVP / 服务端权威战斗基线：  4 / 10
```

---

## 2. 工程身份确认

### 2.1 这是完整团结工程

根目录包含：

```text
Assets/
Packages/
ProjectSettings/
docs/
QingyunSect/
README.md
SkillEditor.html
```

其中 `Assets` 下有完整源码、资产、Shader、Material、Prefab、场景和编辑器工具。

### 2.2 框架身份

文档明确写明该工程对应：

```text
团结引擎 1.9.3 / 2022.3.62t11
Master Combat Core 1.03
中文技能图
```

证据：

```html
6:E:\Tuanjie19_WhiteFog\docs\Tuanjie技能编辑器使用手册.html
<meta name="description" content="团结引擎 1.9.3 与 Master Combat Core 1.03 中文技能图操作手册">

26:E:\Tuanjie19_WhiteFog\docs\Tuanjie技能编辑器使用手册.html
<p>团结引擎 <code>1.9.3 / 2022.3.62t11</code> · Master Combat Core <code>1.03</code> · 中文技能图</p>
```

核心源码目录：

```text
E:\Tuanjie19_WhiteFog\Assets\SoftKitty\MasterCombatCore\
E:\Tuanjie19_WhiteFog\Assets\SoftKitty\Shared\
```

主要包括：

```text
CombatDamage.cs
SkillPlayback.cs
OverTimeEffectInstance.cs
GraphObject.cs
GraphInstance.cs
NodeBase.cs
Root.cs
Entity.cs
AttributeClass.cs
SkillFormula.cs
SkillWorkbench.cs
MasterGraphView.cs
```

---

## 3. 五项重新评估

### 总表

| 验证项 | 源码复核评级 | 评分 | 结论 |
|---|---:|---:|---|
| Unity / 团结版本与 SRP 兼容 | **绿灯** | 9 / 10 | 已锁定团结引擎版本，URP 已实装 |
| AB / Addressables 资源加载 | **黄灯偏绿** | 8 / 10 | AB 可用；Addressables 未实装；加密 AB 需重写 Loader |
| HybridCLR / xLua / ILRuntime 热更 | **黄灯** | 6 / 10 | 无现成热更框架，但 AOT / 反射基础设施友好 |
| GraphObject / GraphInstance 调试序列化 | **绿灯** | 8.5 / 10 | 图资产、运行时实例、SerializeReference、调试回灌已具备 |
| 网络同步 / 确定性 / 服务端复用 | **红灯** | 2 / 10 | 未发现网络实现；Random、Time、协程强耦合客户端运行时 |

---

## 4. 分项证据与判断

### 4.1 Unity / 团结版本与 SRP 兼容：绿灯

工程版本已经明确：

```1:3:E:\Tuanjie19_WhiteFog\ProjectSettings\ProjectVersion.txt
m_EditorVersion: 2022.3.62t11
m_EditorVersionWithRevision: 2022.3.62t11 (b99029341900)
m_TuanjieEditorVersion: 1.9.3
```

包依赖里包含 URP 和 AssetBundle 模块：

```2:5:E:\Tuanjie19_WhiteFog\Packages\manifest.json
  "dependencies": {
    "com.unity.render-pipelines.universal": "14.2.0-t1",
    "com.unity.modules.assetbundle": "1.0.0",
```

`GraphicsSettings` 里已经实装 URP，不是只装包未启用：

```41:69:E:\Tuanjie19_WhiteFog\ProjectSettings\GraphicsSettings.asset
  m_CustomRenderPipeline: {fileID: 11400000, guid: 4dd8a5caad669d8479edcc93b9533a0f, type: 2}
...
    UnityEngine.Rendering.Universal.UniversalRenderPipeline: {fileID: 11400000, guid: 2bde2ab0fb2e7b9439e77298cafd095c, type: 2}
```

判断：

```text
版本兼容风险已大幅降低。
如果目标项目也是团结 1.9.x / Unity 2022.3.x + URP，这一项基本通过。
```

保留风险：

```text
如果目标工程是更高版本 Unity 6，或其他定制引擎版本，需要重新编译验证。
第三方 MCC 1.03 的后续升级维护责任不在本工程内。
```

---

### 4.2 AB / Addressables：黄灯偏绿

`AssetLoader` 提供 AB 预加载：

```48:58:E:\Tuanjie19_WhiteFog\Assets\SoftKitty\Shared\Scripts\AssetLoader.cs
public virtual void PreloadBundle(string _bundlePath)
{
    var myLoadedAssetBundle = AssetBundle.LoadFromFile(Path.Combine(Application.streamingAssetsPath, _bundlePath));
    if (myLoadedAssetBundle == null)
    {
        Debug.Log("Failed to load AssetBundle: " + _bundlePath);
    }
    else
    {
        LoadedBundle.Add(_bundlePath, myLoadedAssetBundle);
    }
}
```

资源路径格式为：

```text
BundleName#ObjectPath
```

例如：

```text
IconData#icons/food
```

证据：

```105:132:E:\Tuanjie19_WhiteFog\Assets\SoftKitty\Shared\Scripts\AssetLoader.cs
/// Example code of load from AssetBundle, 
/// In this example, the bundle path and the object path are separated using "#".
/// For example, an object with the path "icons/food" stored in a bundle named "IconData"
/// would be accessed using the path: "IconData#icons/food".
string[] _args = _path.Split("#");
if (_args.Length >= 2)
{
    ...
    var go = myLoadedAssetBundle.LoadAsset<T>(_args[1]);
    return go;
}
```

Addressables 只有注释示例，没有实际集成：

```100:103:E:\Tuanjie19_WhiteFog\Assets\SoftKitty\Shared\Scripts\AssetLoader.cs
//===============Example code of load from Addressables====================
//var op = Addressables.LoadAssetAsync<T>(_path);
//T go = op.WaitForCompletion();
//return go;
```

并且 `manifest.json` 中没有 Addressables 包。

判断：

```text
普通 AB：可用。
Addressables：未实装。
加密 AB：当前 AssetLoader 是明文 AssetBundle.LoadFromFile，需要重写 CustomLoader。
```

如果目标项目已有资源加密、解密、热更包管理，应通过继承 `AssetLoader` 覆盖 `Load<T>()`、`LoadAndInstantiate<T>()`、`PreloadBundle()`。

评级：**黄灯偏绿**。

---

### 4.3 HybridCLR / xLua / ILRuntime：黄灯

在 `Assets/**/*.cs` 中搜索以下内容：

```text
HybridCLR | ILRuntime | XLua | xLua | hotfix | Hotfix | ToLua
```

结果：**0 命中**。

因此结论很明确：

```text
该工程没有现成热更框架。
```

但它有一个非常重要的 AOT 友好设计：`link.xml`。

```1:6:E:\Tuanjie19_WhiteFog\Assets\SoftKitty\MasterCombatCore\link.xml
<linker>
  <!-- Built-in graph classes are resolved from serialized class names at runtime. -->
  <assembly fullname="Assembly-CSharp">
    <type fullname="SoftKitty.CombatRoot" preserve="all" />
    <type fullname="SoftKitty.SkillImpactAction" preserve="all" />
    <type fullname="SoftKitty.FormulaImpactAction" preserve="all" />
```

这说明作者知道 IL2CPP 下反射类型可能被裁剪，所以把图节点类型全部保留。

运行时通过字符串类名反射创建 Root：

```91:111:E:\Tuanjie19_WhiteFog\Assets\SoftKitty\Shared\Scripts\Core\Graph\GraphInstance.cs
private void GetRootInstance()
{
    if (!runtimeInitializedGraph.ContainsKey(mGraph))
    {
        string _className = "SoftKitty." + mGraph.RootSet.ClassName + "Root";
        Type realRootType = Type.GetType(_className);
        ...
        object realSubclassInstance = Activator.CreateInstance(realRootType) as Root;
```

Entity 模块也用了字符串类型 + JSON 的恢复方式：

```81:108:E:\Tuanjie19_WhiteFog\Assets\SoftKitty\Shared\Scripts\Core\DataObject\Entity.cs
[System.Serializable]
public class EntityModuleWrapper
{
    public string type;
    public string jsonData;
...
    var t = ResolveType(type);
    if (t == null) return null;
    runtimeModule = (EntityModule)System.Activator.CreateInstance(t);
    runtimeModule = runtimeModule.FromJson(jsonData);
```

判断：

```text
没有热更框架，但源码对 IL2CPP / AOT 反射是友好的。
接 HybridCLR 的价值比接 xLua 更高。
```

推荐改造方向：

```text
AOT 层保留：
    GraphObject 加载
    GraphInstance 宿主
    Entity / Attribute 基础模型
    资源加载接口
    节点接口定义

热更层放置：
    Action / Handler / Root 子类
    技能规则
    Buff / DoT 执行逻辑
    伤害公式以外的业务变化逻辑
```

必须处理：

```text
1. link.xml 的 assembly fullname 从 Assembly-CSharp 调整到热更程序集。
2. Type.GetType("SoftKitty." + ClassName + "Root") 需要支持热更程序集查找。
3. 热更前后类型名、节点类名、序列化字符串必须稳定。
4. 不要让配置资源直接持有热更类型引用，优先用字符串 ID / 类型名解耦。
```

评级：**黄灯**。

---

### 4.4 GraphObject / GraphInstance 调试序列化：绿灯

图资产是 ScriptableObject：

```6:9:E:\Tuanjie19_WhiteFog\Assets\SoftKitty\Shared\Scripts\Core\Graph\GraphObject.cs
[CreateAssetMenu(fileName = "GraphObject", menuName = "Soft Kitty/技能图")]
public class GraphObject : GraphBase
{
    public Root RootSet;
```

运行时实例与图资产分离：

```15:31:E:\Tuanjie19_WhiteFog\Assets\SoftKitty\Shared\Scripts\Core\Graph\GraphInstance.cs
public class GraphInstance : MonoBehaviour
{
    ...
    [SerializeReference,HideInInspector]
    protected Root currentRootInstance;
```

运行时有独立状态缓存：

```33:41:E:\Tuanjie19_WhiteFog\Assets\SoftKitty\Shared\Scripts\Core\Graph\GraphInstance.cs
#region Internal Method
[HideInInspector]
public static Dictionary<GraphObject, Root> runtimeInitializedGraph = new Dictionary<GraphObject, Root>();

protected GraphRuntimeState mRuntime;

public virtual void Awake()
{
    runtimeInitializedGraph.Clear();
}
```

有 ResetRuntime 机制：

```195:200:E:\Tuanjie19_WhiteFog\Assets\SoftKitty\Shared\Scripts\Core\Graph\GraphInstance.cs
/// Reset the runtime data
public void ResetRuntime()
{
    for (int i=0;i<mRuntime.HandlerStates.Length;i++)
    {
```

有调试文本回灌：

```53:56:E:\Tuanjie19_WhiteFog\Assets\SoftKitty\MasterCombatCore\Scripts\Graph\OverTimeEffectInstance.cs
#if MASTER_COMBAT_CORE
#if UNITY_EDITOR
if (SGD_Settings.Instance.GraphDebugMode) Debug.Log( currentRootInstance.GenerateGraphDebugText(...));
#endif
#endif
```

判断：

```text
这部分设计质量较高。
GraphObject / GraphInstance 分离是正确方向。
SerializeReference + RuntimeState + ResetRuntime + DebugText 已经具备商业项目调试基础。
```

评级：**绿灯**。

---

### 4.5 网络同步 / 确定性 / 服务端复用：红灯

搜索以下关键字：

```text
lockstep | rollback | deterministic | replication |
NetworkManager | Mirror | PUN | Photon |
syncVar | ServerRpc
```

在运行时 C# 源码中未发现有效实现。

并且核心战斗逻辑强依赖 Unity 客户端运行时。

命中判定使用 `UnityEngine.Random`：

```79:83:E:\Tuanjie19_WhiteFog\Assets\SoftKitty\Shared\Scripts\CustomClass\Action\FormulaImpactAction.cs
if (!TryEvaluate(calculation.accuracy, context, host, out var chance)) yield break;
if (chance <= 0 || (chance < 1 && UnityEngine.Random.value >= chance))
{
    host.Signal(victim, SkillPlayback.Position(target, victim), "未命中", .18f, Color.gray);
```

节点图执行依赖协程和 `WaitForSeconds`：

```49:70:E:\Tuanjie19_WhiteFog\Assets\SoftKitty\Shared\Scripts\Core\Graph\GraphInstance.cs
private IEnumerator ActionCo(float _delay, int _count, float _keepTime, float _interval, NodeBase _node, GraphRuntimeState _runtime)
{
    yield return new WaitForSeconds(_delay);
    ...
    yield return new WaitForSeconds(_interval);
```

技能播放依赖 `Time.time`：

```66:77:E:\Tuanjie19_WhiteFog\Assets\SoftKitty\MasterCombatCore\Scripts\Core\SkillPlayback.cs
float due = Time.time + Mathf.Max(0f, wait);
...
} while (Time.time < due);
release();
due = Time.time + Mathf.Max(.01f, interval);
```

核心类继承 `MonoBehaviour`，运行时还依赖 Collider / Rigidbody / Transform 等 Unity 对象。

唯一明显可以抽离复用的模块是 `SkillFormula`：

```60:97:E:\Tuanjie19_WhiteFog\Assets\SoftKitty\Shared\Scripts\Core\Graph\SkillFormula.cs
public float Evaluate(RootRuntimeState context, IDictionary<string, float> sampleInputs = null)
{
    ...
    if (sampleInputs == null || !sampleInputs.TryGetValue(key, out value))
    {
        ...
        value = entity.GetAttributeFloat(variable.AttributeKey);
    }
```

`SkillFormula` 是纯数值图，可以通过 `sampleInputs` 脱离角色实例试算，适合抽取到服务端或工具侧复用。

判断：

```text
该框架不适合直接承载服务端权威战斗。
如要做 PVP，需要重写时间、随机、执行上下文和 Entity 宿主。
可以优先复用 SkillFormula 和纯数值计算部分。
```

评级：**红灯**。

---

## 5. 已发现的明确风险

### 5.1 多目标延迟伤害上下文复用问题

文档里已经明确记录：

```232:232:E:\Tuanjie19_WhiteFog\docs\Tuanjie技能编辑器使用手册.html
<details><summary>同帧命中两名敌人，伤害集中到最后一个目标</summary><p>当前 MCC 1.03 迁移版中，同一 <code>CombatDamage</code> 连续向 A、B 提交三段、每段 7 点的延迟伤害，会复用执行上下文。团结 Play 实测 A 保持 100，B 从 100 降到 58。需要独立结算的并行命中请分别使用执行实例，并让实例存活到最后一段动作完成。单实例复用前，先做这项双目标回归。</p></details>
```

风险等级：**高**。

必须做：

```text
1. 同帧多目标命中测试。
2. 延迟多段伤害上下文隔离测试。
3. 并行技能实例生命周期测试。
4.  projectile、DoT、Buff 叠加场景回归。
```

---

### 5.2 热更边界未建立

当前没有 HybridCLR / xLua / ILRuntime 代码。虽然 `link.xml` 和字符串反射对 AOT 友好，但还不能直接热更。

风险等级：**中高**。

必须做：

```text
1. 明确哪些类进入热更程序集。
2. 调整 Type.GetType 的类查找逻辑。
3. 调整 link.xml 的 assembly fullname。
4. 避免配置资源直接依赖热更类型。
5. 做 IL2CPP 包体下的反射回归。
```

---

### 5.3 AB 加载未接加密管线

当前 `AssetLoader` 使用明文：

```text
AssetBundle.LoadFromFile(Path.Combine(Application.streamingAssetsPath, _bundlePath))
```

风险等级：**中**。

如果目标项目有资源加密、自定义包格式、远端热更包，必须重写 Loader。

---

### 5.4 随机与时间不可确定

命中判定和变量随机使用：

```text
UnityEngine.Random
UnityEngine.Random.value
Random.Range
Time.time
WaitForSeconds
```

风险等级：**高，仅针对 PVP / 服务端权威场景**。

单机客户端项目可以忽略；服务端权威项目必须改造。

---

## 6. 推荐接入方案

### 阶段 0：保留源码基线

```text
1. 不直接改原始目录。
2. 复制一份工程或建立分支。
3. 先跑通 Demo 场景。
4. 记录当前 Commit / 备份版本。
```

---

### 阶段 1：资源管线接入

目标：

```text
GraphObject / SkillFormula / Prefab / VFX 全部走项目现有资源系统
```

需要做：

```text
1. 继承 AssetLoader。
2. 覆盖 PreloadBundle。
3. 覆盖 Load<T>。
4. 覆盖 LoadAndInstantiate<T>。
5. 接项目加密 AB / Addressables / 热更资源路径。
```

验收：

```text
技能图资源可以热更替换。
资源卸载不泄漏。
多个技能图共享资源不重复加载。
```

---

### 阶段 2：热更边界拆分

推荐分层：

```text
AOT 层：
    GraphObject 加载
    GraphInstance 宿主
    Entity / Attribute 基础模型
    资源加载接口
    节点接口定义

Hotfix 层：
    Action
    Handler
    Root 子类
    Skill / Buff / DoT 规则
    数值公式扩展
    业务触发逻辑
```

验收：

```text
改一个技能节点逻辑，不需要重新出包。
新增节点类后，IL2CPP 包体下 Type.GetType 可以解析。
link.xml / 热更程序集注册正确。
```

---

### 阶段 3：并发命中隔离

优先修：

```text
1. 每个独立命中批次独立 GraphInstance。
2. 每次施放使用独立 RootRuntimeState。
3. 延迟伤害 task 不复用其他目标 context。
4. 实例生命周期覆盖最后一段延迟伤害。
```

验收：

```text
同帧 A/B 双目标三段伤害正确。
投射物命中、直接命中、DoT 同时存在时结果正确。
Buff 叠加不走错目标。
```

---

### 阶段 4：确定性改造

仅服务端权威 / PVP 需要。

需要抽象：

```text
ICombatClock
ICombatRandom
ICombatLogger
IEntityResolver
IGraphStateSerializer
```

替换：

```text
UnityEngine.Random -> 确定性 RNG，并序列化 rngState
Time.time / Time.deltaTime -> 显式 currentTime / deltaTime
WaitForSeconds -> 逻辑帧调度或时间戳调度
MonoBehaviour 宿主 -> 纯 C# 执行宿主
Collider / Rigidbody -> 服务端可复用的命中描述或快照
```

可优先复用：

```text
SkillFormula
Attribute 数据模型
Buff / DoT 图配置
```

---

## 7. 最终选型建议

### 推荐用于

```text
单机 ARPG
客户端 PVE
复杂 Buff / DoT / 被动技能
需要可视化技能编辑器的项目
已有 HybridCLR 且愿意改造热更边界的项目
```

### 谨慎用于

```text
买量 F2P 项目
高频上线新技能的项目
有自研战斗管线的项目
已有成熟技能表驱动系统的项目
```

原因：

```text
编辑器体验是优势，但运行时框架偏重。
如果团队只想要配置和帧事件，不需要整套 Entity / Attribute / GraphInstance。
```

### 不推荐直接用于

```text
PVP
MMO
MOBA
服务端权威战斗
帧同步 / 回滚Rollback
强确定性战斗
```

原因：

```text
没有网络同步实现。
随机、时间、协程、MonoBehaviour 强耦合。
需要较大幅度重写才能服务端复用。
```

---

## 8. PoC 验收清单

如果决定接入，建议按下面清单验收：

```text
[ ] Demo 场景在目标团结版本下正常运行
[ ] URP 下特效、材质、后处理兼容
[ ] GraphObject 能打入项目 AB / 加密 AB
[ ] 热更程序集中新增一个 Action 节点并运行成功
[ ] IL2CPP 包体下字符串反射节点不丢失
[ ] 同帧多目标三段延迟伤害结果正确
[ ] 投射物命中与直接命中不会串上下文
[ ] Buff / DoT 叠加、续时、过期正确
[ ] 调试文本能回灌到 Graph View
[ ] SkillFormula 可独立试算并与客户端结果一致
[ ] 若做服务端：Random / Time 全部抽象注入
```

---

## 9. 一句话结论

> `E:\Tuanjie19_WhiteFog` 里的 Master Combat Core 已经不是“插件候选”，而是一个可拆解的源码基线。它的技能图编辑器、GraphObject / GraphInstance、调试序列化、URP / 团结兼容都值得保留；但热更、加密 AB、并发命中隔离、网络确定性是必须自己补齐的四块。适合做客户端复杂战斗，不适合直接做服务端权威 PVP 核心框架。
