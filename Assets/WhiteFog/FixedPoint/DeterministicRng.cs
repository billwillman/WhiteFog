// 确定性随机数生成器。战斗核平台层，netstandard2.1，零引擎依赖。
//
// 落地形态真源：Docs/design/host-runtime.md v2 §一「确定性 RNG」行
//               （splitmix64 播种 ＋ xoshiro256++，纯 ulong/long 运算，常量钉死）。
// 调用面真源：Docs/design/frame-core.md §5.2（[0,1) 定点 / [a,b) 定点 / [a,b] 闭区间整数）。
//
// 分工边界：本类型只管**单条流**。世界层持有两条流并从同一入参种子派生两个不同种子
// （seedLogic = seed，seedPresentation = seed 异或常量盐），见 frame-core.md §5.1；
// 那一层与观感盐常量随帧世界落地，本批不建（rng.yaml P0-RNG-008）。

using System;

namespace Dig.Framework
{
    /// <summary>
    /// 确定性伪随机数生成器：splitmix64 播种 ＋ xoshiro256++ 主循环，全程 64 位整数运算。
    /// 同种子必产生逐项相同的序列，这是复算一致的前提。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 战斗域内**禁止出现任何第三个随机源**（宿主随机、哈希取模伪随机、时钟派生）。
    /// </para>
    /// <para>
    /// 算法与常量一旦变更，同一种子就换出另一条取数序列。
    /// </para>
    /// </remarks>
    public sealed class DeterministicRng
    {
        // splitmix64 的三个钉死常量（Steele/Lea 的 SplittableRandom）：
        // 黄金分割增量，与两轮 xor-shift-multiply 的乘子。
        private const ulong SplitMixGamma = 0x9E3779B97F4A7C15UL;
        private const ulong SplitMixMul1 = 0xBF58476D1CE4E5B9UL;
        private const ulong SplitMixMul2 = 0x94D049BB133111EBUL;

        // xoshiro256++ 的三个钉死移位/旋转量（Blackman/Vigna）。
        private const int PlusPlusRotate = 23;
        private const int StateShift = 17;
        private const int StateRotate = 45;

        private ulong _s0;
        private ulong _s1;
        private ulong _s2;
        private ulong _s3;

        /// <summary>
        /// 以一个 64 位种子构造。种子先经 splitmix64 连出 4 个 64 位量作为 xoshiro256++ 的初态，
        /// 因此**种子 0 也是合法种子**（不会退化成全零态）。
        /// </summary>
        /// <param name="seed">世界种子。</param>
        public DeterministicRng(ulong seed)
        {
            ulong state = seed;
            _s0 = SplitMixNext(ref state);
            _s1 = SplitMixNext(ref state);
            _s2 = SplitMixNext(ref state);
            _s3 = SplitMixNext(ref state);
        }

        /// <summary>
        /// 抽取下一个 64 位原始随机量（xoshiro256++ 一步）。所有其他抽取面都由它派生。
        /// </summary>
        /// <returns>均匀分布于全 64 位的随机量。</returns>
        public ulong NextRaw()
        {
            unchecked
            {
                ulong result = RotateLeft(_s0 + _s3, PlusPlusRotate) + _s0;
                ulong t = _s1 << StateShift;

                _s2 ^= _s0;
                _s3 ^= _s1;
                _s1 ^= _s2;
                _s0 ^= _s3;
                _s2 ^= t;
                _s3 = RotateLeft(_s3, StateRotate);

                return result;
            }
        }

        /// <summary>
        /// 抽取 <b>[0, 1)</b> 区间的定点数：取一次 <see cref="NextRaw"/> 的**高 32 位**作为小数部分，
        /// 因此取值恰好均匀落在 2^32 个可表示值上，且**永远取不到 1**。
        /// </summary>
        /// <returns>落在 [0, 1) 的定点数。</returns>
        public Fix NextFix01()
        {
            return Fix.FromRawInt64((long)(NextRaw() >> 32));
        }

        /// <summary>
        /// 抽取 <b>[a, b)</b> 区间的定点数：a + (b - a) * NextFix01()。
        /// 乘法向零截断且系数落在 [0, 1)，故结果**含下界、不含上界**；a 与 b 相等时恒返回 a。
        /// </summary>
        /// <param name="a">下界（可取到）。</param>
        /// <param name="b">上界（取不到），不得小于 a。</param>
        /// <returns>落在 [a, b) 的定点数。</returns>
        /// <exception cref="ArgumentException">上界小于下界。</exception>
        public Fix NextFixBetween(Fix a, Fix b)
        {
            if (b < a)
            {
                throw new ArgumentException("DeterministicRng.NextFixBetween: 上界小于下界。", "b");
            }

            return a + ((b - a) * NextFix01());
        }

        /// <summary>
        /// 抽取 <b>[a, b] 闭区间</b>的整数，**两个端点都可能取到**。
        /// 用「取模 ＋ 拒绝采样」消偏：落进尾部残段的抽取被丢弃并重抽，
        /// 因此消耗的原始抽取次数可能多于一次——这不影响确定性（同种子同样丢弃、同样重抽）。
        /// </summary>
        /// <param name="a">下界，可取到。</param>
        /// <param name="b">上界，可取到，不得小于 a。</param>
        /// <returns>落在 [a, b] 的整数。</returns>
        /// <exception cref="ArgumentException">上界小于下界。</exception>
        public int NextIntBetween(int a, int b)
        {
            if (b < a)
            {
                throw new ArgumentException("DeterministicRng.NextIntBetween: 上界小于下界。", "b");
            }

            unchecked
            {
                ulong span = (ulong)((long)b - (long)a) + 1UL;
                ulong threshold = (0UL - span) % span;   // 等于 2^64 mod span
                ulong sample;
                do
                {
                    sample = NextRaw();
                }
                while (sample < threshold);

                return (int)((long)a + (long)(sample % span));
            }
        }

        /// <summary>splitmix64 一步，用于把单个种子摊开成 xoshiro256++ 的 4 个状态字。</summary>
        private static ulong SplitMixNext(ref ulong state)
        {
            unchecked
            {
                state += SplitMixGamma;
                ulong z = state;
                z = (z ^ (z >> 30)) * SplitMixMul1;
                z = (z ^ (z >> 27)) * SplitMixMul2;
                return z ^ (z >> 31);
            }
        }

        /// <summary>64 位循环左移。netstandard2.1 无 BitOperations，故自实现。</summary>
        private static ulong RotateLeft(ulong value, int count)
        {
            return (value << count) | (value >> (64 - count));
        }
    }
}
