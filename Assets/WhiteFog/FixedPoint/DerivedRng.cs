// 计数派生随机流：**坐标寻址**的确定性取数。框架层，netstandard2.1，零引擎依赖。
//
// 真源：Docs/design/frame-core.md §5（三条流的分工；本类型是「计数派生」那一条的机制面）、
//       Docs/design/host-runtime.md v2 §一「确定性 RNG」行（splitmix64 的算法与常量）。
//
// 【它为什么不是「另一个随机源」】它是同一个 64 位种子的**确定性派生**：
// 值 ＝ splitmix64(种子 异或 盐 异或 事件坐标)。没有内部状态、没有取数顺序、没有非种子来源，
// 因此「战斗域禁非种子随机」这条在这里是**满足**的，不是例外。
//
// 【它与顺序流的区别】<see cref="DeterministicRng"/> 是 xoshiro256++ 的**顺序**流：
// 第 n 次抽取的值取决于前面抽了几次。本类型是**坐标寻址**的：给定（种子，盐，坐标）
// 即可直接算出，与「之前抽过几次」无关——复算方因此不必重放整条时间线，
// 就能算出任一事件点的取值。
//
// 【与顺序流共用同一组算法常量】三个 splitmix64 常量与 <see cref="DeterministicRng"/> 的
// 播种常量**逐值相同**。本类型不引入任何新的算法常量：
// 盐由调用方以入参给出，因此框架**不认识任何一条具体的业务流**——「这条流拿来干什么、
// 坐标四个分量各自代表什么、盐取什么值」全部是调用方的事。
//
// 【机制侧唯一的硬规格】坐标折叠的分量顺序（purpose → a → b → sub）。
// 换序＝换取值序列＝全部历史录像的派生序列改变。

using System;

namespace Dig.Framework
{
    /// <summary>
    /// 计数派生流的**事件坐标**：一次派生取数在这一局里的结构化地址。
    /// 同一坐标在同一（种子，盐）下恒得同值；不同坐标彼此独立。
    /// </summary>
    /// <remarks>
    /// 四个分量的语义**由调用方规定**，本类型只保证「分量不同即坐标不同」。坐标必须由
    /// **逻辑上确定**的量拼成（序号、键值、帧号……），不得掺入宿主量，更不得掺入地址或
    /// 哈希码——那会让复算方算不出同一个值。
    /// </remarks>
    public readonly struct DerivedRngCoord
    {
        /// <summary>按四个分量构造。</summary>
        /// <param name="purpose">用途：调用方的分段维度，决定另外三个分量的语义。</param>
        /// <param name="a">第一分量。</param>
        /// <param name="b">第二分量。</param>
        /// <param name="sub">子序号：同一事件里的第几次取数，从 0 起；拒绝采样重抽也走它。</param>
        public DerivedRngCoord(int purpose, int a, int b, int sub)
        {
            Purpose = purpose;
            A = a;
            B = b;
            Sub = sub;
        }

        /// <summary>用途。</summary>
        public int Purpose { get; }

        /// <summary>第一分量。</summary>
        public int A { get; }

        /// <summary>第二分量。</summary>
        public int B { get; }

        /// <summary>子序号。</summary>
        public int Sub { get; }

        /// <summary>换一个子序号，其余分量不变（拒绝采样与「同事件第二次取数」都用它）。</summary>
        /// <param name="sub">新的子序号。</param>
        /// <returns>新坐标。</returns>
        public DerivedRngCoord WithSub(int sub)
        {
            return new DerivedRngCoord(Purpose, A, B, sub);
        }
    }

    /// <summary>
    /// 计数派生流的取数面。**全部静态、无状态**：给（种子，盐，坐标）即得值。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 盐是入参而不是常量：一个世界种子可以派生任意多条互不相关的计数派生流，
    /// 「哪条流用哪个盐」由调用方在自己的层里钉死，框架不持有这份知识。
    /// </para>
    /// <para>
    /// 算法与常量一旦变更，全部历史录像的派生序列失效。
    /// </para>
    /// </remarks>
    public static class DerivedRng
    {
        /// <summary>拒绝采样的重试上限。超过即硬失败——它在数学上不可能被触到，越界说明实现坏了。</summary>
        /// <remarks>
        /// 单次拒绝概率 &lt; 1/2（阈值 ＝ 2^64 mod span &lt; span &lt;= 2^63），
        /// 连续 64 次被拒的概率小于 2^-64。
        /// </remarks>
        public const int MaxRejectionRetries = 64;

        // splitmix64 的三个钉死常量，与 DeterministicRng 逐值相同（Steele/Lea 的 SplittableRandom）。
        private const ulong SplitMixGamma = 0x9E3779B97F4A7C15UL;
        private const ulong SplitMixMul1 = 0xBF58476D1CE4E5B9UL;
        private const ulong SplitMixMul2 = 0x94D049BB133111EBUL;

        // 坐标折叠用的 FNV-1a 64 位乘子。这里只用它做**混合**，不当哈希码用，
        // 也不碰任何对象的 GetHashCode（战斗代码禁的是后者）。
        private const ulong CoordMixPrime = 0x100000001B3UL;

        /// <summary>
        /// 取一次 64 位原始派生值：<c>splitmix64(种子 异或 盐 异或 坐标)</c>。
        /// </summary>
        /// <param name="seed">世界种子。</param>
        /// <param name="salt">本条派生流的盐（由调用方钉死）。</param>
        /// <param name="coord">事件坐标。</param>
        /// <returns>均匀分布于全 64 位的量。</returns>
        public static ulong Raw(ulong seed, ulong salt, DerivedRngCoord coord)
        {
            unchecked
            {
                return SplitMix64(seed ^ salt ^ PackCoord(coord));
            }
        }

        /// <summary>
        /// 取 <b>[0, 1)</b> 区间的定点值：取 <see cref="Raw"/> 的**高 32 位**作小数部分。
        /// 与顺序流 <see cref="DeterministicRng.NextFix01"/> 的取法逐字相同，
        /// 因此两条流的分布口径一致。
        /// </summary>
        /// <param name="seed">世界种子。</param>
        /// <param name="salt">本条派生流的盐。</param>
        /// <param name="coord">事件坐标。</param>
        /// <returns>落在 [0, 1) 的定点数，**永远取不到 1**。</returns>
        public static Fix Unit01(ulong seed, ulong salt, DerivedRngCoord coord)
        {
            return Fix.FromRawInt64((long)(Raw(seed, salt, coord) >> 32));
        }

        /// <summary>
        /// 取 <b>[0, exclusiveUpper)</b> 的整数，用「取模 ＋ 拒绝采样」消偏。
        /// 被拒的抽取**换子序号重抽**（<see cref="DerivedRngCoord.WithSub"/>），
        /// 因此拒绝行为本身也是坐标寻址的、可被复算方独立算出。
        /// </summary>
        /// <param name="seed">世界种子。</param>
        /// <param name="salt">本条派生流的盐。</param>
        /// <param name="coord">事件坐标；其 <c>Sub</c> 即第一次尝试的子序号。</param>
        /// <param name="exclusiveUpper">上界（取不到），必须为正。</param>
        /// <returns>落在 [0, exclusiveUpper) 的整数。</returns>
        /// <exception cref="ArgumentOutOfRangeException">上界非正。</exception>
        /// <exception cref="InvalidOperationException">拒绝采样超过 <see cref="MaxRejectionRetries"/> 次。</exception>
        public static long Below(ulong seed, ulong salt, DerivedRngCoord coord, long exclusiveUpper)
        {
            if (exclusiveUpper <= 0L)
            {
                throw new ArgumentOutOfRangeException("exclusiveUpper",
                    "计数派生取数的上界必须为正，实得 " + Diag.Num(exclusiveUpper)
                    + "（空集或总权重为 0 应由调用方先行处置，不许悄悄退化）。");
            }

            unchecked
            {
                ulong span = (ulong)exclusiveUpper;
                ulong threshold = (0UL - span) % span;   // 等于 2^64 mod span
                int sub = coord.Sub;
                for (int i = 0; i <= MaxRejectionRetries; i++)
                {
                    ulong sample = Raw(seed, salt, coord.WithSub(sub));
                    if (sample >= threshold)
                    {
                        return (long)(sample % span);
                    }

                    sub++;
                }
            }

            throw new InvalidOperationException(
                "计数派生流的拒绝采样超过 " + Diag.Num(MaxRejectionRetries)
                + " 次，上界 " + Diag.Num(exclusiveUpper) + "：这在数学上不可能，实现有误。");
        }

        /// <summary>
        /// 把坐标折成一个 64 位量。四个分量按固定序参与，**顺序即规格**：
        /// 换序＝换取值序列。
        /// </summary>
        /// <param name="coord">事件坐标。</param>
        /// <returns>折叠值。</returns>
        public static ulong PackCoord(DerivedRngCoord coord)
        {
            unchecked
            {
                ulong v = (ulong)(uint)coord.Purpose;
                v = (v * CoordMixPrime) ^ (ulong)(uint)coord.A;
                v = (v * CoordMixPrime) ^ (ulong)(uint)coord.B;
                v = (v * CoordMixPrime) ^ (ulong)(uint)coord.Sub;
                return v;
            }
        }

        /// <summary>splitmix64 的一步（输入即状态，故是无状态的纯函数）。</summary>
        /// <param name="input">输入量。</param>
        /// <returns>混合后的量。</returns>
        private static ulong SplitMix64(ulong input)
        {
            unchecked
            {
                ulong z = input + SplitMixGamma;
                z = (z ^ (z >> 30)) * SplitMixMul1;
                z = (z ^ (z >> 27)) * SplitMixMul2;
                return z ^ (z >> 31);
            }
        }
    }
}
