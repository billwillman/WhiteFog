# 定点数与确定性随机数

WhiteFog 的 Unity 脚本可通过 `using Dig.Framework;` 使用这套计算。程序集名为 `WhiteFog.FixedPoint`。使用自定义 asmdef 的调用方在 Assembly Definition References 中添加该程序集。

| 类型 | 用途 |
| --- | --- |
| `Fix` | Q32.32 有符号定点数。支持四则运算、取整、插值、平方根、三角函数和指数对数。 |
| `Fix2` | 二维定点向量。支持点积、长度、距离、归一化与插值。 |
| `Fix3` | 三维定点向量。支持二维向量对应的三维运算，以及右手系叉积。 |
| `DeterministicRng` | 持有种子的顺序随机流。可取原始 64 位值、左闭右开的定点区间值和两端都包含的整数。 |
| `DerivedRng`、`DerivedRngCoord` | 由种子、盐和四个整数坐标直接取值。同一事件可单独复算，调用先后不影响结果。 |
| `Diag` | 将整数按固定格式转成十进制文本，供随机库诊断使用。 |

```csharp
using Dig.Framework;

// 从整数或分数进入定点域。
Fix distance = Fix.FromInt(8);
Fix speed = Fix.FromFraction(5, 2);
Fix seconds = distance / speed;

// 使用相同的定点标量计算三维位移和飞行方向。
var target = new Fix3(distance, Fix.FromInt(2), Fix.FromInt(6));
Fix3 direction = Fix3.Delta(Fix3.Zero, target).Normalize();

// 顺序随机流在一局开始时创建，各次抽取继续使用这一实例。
var rng = new DeterministicRng(42UL);
Fix variation = rng.NextFixBetween(Fix.FromFraction(9, 10), Fix.One);
int count = rng.NextIntBetween(1, 3);

// 本例以施法序号、命中段号和抽取序号标识一次暴击判定。
var hit = new DerivedRngCoord(purpose: 1, a: 27, b: 2, sub: 0);
bool critical = DerivedRng.Unit01(42UL, 7UL, hit) < Fix.FromFraction(1, 4);
```

存档、传输或逐位比对时可用 `Fix.ToRaw()` 和 `Fix.FromRaw(low, high)` 往返还原。表现层需要浮点值时可用 `ToDouble()`。

源码迁自 DIG 的 `dig-project/framework`，版本 `3bb7fd0c6`。原有五个 C# 文件逐字节迁入。WhiteFog 依据 DIG 的 `docs/design/fixed-point.md` 第 3.3 节补齐 `Fix3`。它的 `ToRaw()` 依次输出 X、Y、Z 各自的低、高 32 位，`FromRaw` 按相同顺序还原。
