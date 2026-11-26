# Combat Random

给每场战斗创建一个 `CombatRandomSession`，传入对局种子。施放指令 ID、来源、技能、目标、节点、段号与用途相同，每次都会取到同一个值。不同战斗可以在同一进程内独立运行。

```csharp
using SoftKitty;

var battle = new CombatRandomSession(42);
var cast = battle.ForEvent("cast", "hero/1", "飞剑", "goblin/2", eventId: 17);
var secondStrike = cast.With("node/4").With(1); // 段号从 0 开始
bool hit = secondStrike.With("hit").Unit < 0.75f;
bool critical = secondStrike.With("critical").Unit < 0.20f;
```

同一次范围攻击给所有目标传相同的施放指令 ID，目标传各自稳定的实体实例 ID。新一段、新判定用途使用各自的键。客户端与服务端要约定相同的字符串、大小写与段号。

| 接口 | 取值规则 |
| --- | --- |
| `ForEvent(..., eventId)` | 按事件 ID 直接取值，支持乱序、重复或并行查询。 |
| `NextEvent(...)` | 兼容没有事件 ID 的调用方。每组用途、来源、技能、目标单独计数，同组事件回放时须保持原顺序。 |
| `Unit` / `Index(count)` / `Range(a, b)` | 分别返回 `[0,1)` 概率样本、`[0,count)` 整数下标和包含两端点的浮点数。 |

**接入**

- Unity 或团结工程可把完整包放在 `Packages/com.whitefog.combat-random`。自定义程序集引用 `SoftKitty.CombatRandom`。
- 普通 .NET 工程可通过 `ProjectReference` 引用 `DotNet~/SoftKitty.CombatRandom.csproj`。库目标为 .NET Standard 2.1。
- 在包目录执行 `dotnet run --project Tests~/CombatRandom.Tests.csproj` 可运行回归用例，需要 .NET 8 或更高版本的 SDK 和 .NET 8 运行时。
