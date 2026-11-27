// Q32.32 定点数标量。战斗核平台层，netstandard2.1，零引擎依赖。
//
// 语义真源：Docs/design/fixed-point.md §1（格式与 {low,high} 规范形）、§2（逐条运算语义）、§5（出域规则）。
// 落地形态真源：Docs/design/host-runtime.md v2 §一「Fix」行（readonly struct + long 承载，
//               乘除用 32 位半字拆分做 128 位中间量，netstandard2.1 无 Math.BigMul）。
// 契约规范：Docs/design/contract-format.md v2 §二（public 面＝契约，实现细节 internal/private，
//           公共 API 全量 XML 注释）。
//
// 铁律：本文件里出现的每一个魔数都必须能被独立复算。查表常量的生成方法写在各表的注释里。

using System;
using System.Diagnostics;

namespace Dig.Framework
{
    /// <summary>
    /// Q32.32 有符号定点数：以一个 64 位整数承载，隐含缩放因子 2^32。
    /// 表示范围约 [-2^31, 2^31)，分辨率 2^-32。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 这是战斗域唯一的数值类型：战斗核禁用 float/double/decimal，两端（客户端与服务器）
    /// 共用同一个程序集、同一套舍入规则，因此逐帧复算逐位一致。语义逐条钉死于
    /// Docs/design/fixed-point.md §2，实现路线可换、语义不可换。
    /// </para>
    /// <para>
    /// 溢出策略：一律 unchecked 回绕，这是**已定义行为**而非未定义行为（fixed-point.md §2
    /// 「release 行为必须定义并测试」）；Debug 构建下额外 Debug.Assert 报警。
    /// </para>
    /// <para>
    /// 出入通道的规范形是 {low, high} 双 int32，见 <see cref="ToRaw"/> / <see cref="FromRaw(int, int)"/>。
    /// 哈希、序列化、跨端传输、录像编解码一律走规范形，不得先转十进制字符串或浮点。
    /// </para>
    /// </remarks>
    public readonly struct Fix : IEquatable<Fix>, IComparable<Fix>
    {
        // ------------------------------------------------------------------
        // 内部常量
        // ------------------------------------------------------------------

        /// <summary>小数部分的位数（Q32.<b>32</b>）。</summary>
        public const int FractionalBits = 32;

        /// <summary>数值 1 的 raw 值，即缩放因子 2^32。</summary>
        internal const long OneRaw = 1L << FractionalBits;

        /// <summary>低 32 位掩码。</summary>
        private const long LowMask = 0xFFFFFFFFL;

        // 角度与超越函数常量。全部由高精度十进制按「四舍五入到最近的 raw 整数」生成，
        // 生成方法：raw = round(value * 2^32)。逐个可独立复算，测试 Fix_Constants_RawValuesArePinned 钉死。
        private const long PiRaw = 13493037705L;        // round(pi        * 2^32)
        private const long TwoPiRaw = 26986075409L;     // round(2*pi      * 2^32)
        private const long HalfPiRaw = 6746518852L;     // round(pi/2      * 2^32)
        private const long Ln2Raw = 2977044472L;        // round(ln 2      * 2^32)
        private const long DegToRadRaw = 74961321L;     // round(pi/180    * 2^32)
        private const long RadToDegRaw = 246083499208L; // round(180/pi    * 2^32)

        // CORDIC 圆周模式的增益倒数 1/K，K = prod_{i=0..31} sqrt(1 + 2^-2i)。
        // 生成方法：raw = round(2^32 / prod_{i=0..31} sqrt(1 + 4^-i))。
        private const long CordicInvGainRaw = 2608131496L;

        // CORDIC 迭代级数。表尺寸与级数是平台常量（fixed-point.md §2 表后注）。
        private const int CordicIterations = 32;

        /// <summary>
        /// CORDIC 角度表：AtanTable[i] = round(atan(2^-i) * 2^32)，i = 0..31。
        /// i 大于等于 11 之后 atan(2^-i) 与 2^-i 在 Q32.32 下不可分辨，故尾部即 2^(32-i)。
        /// 表内容变更＝哈希域变更。
        /// </summary>
        private static readonly long[] AtanTable =
        {
            3373259426L, 1991351318L, 1052175346L, 534100635L,
            268086748L, 134174063L, 67103403L, 33553749L,
            16777131L, 8388597L, 4194303L, 2097152L,
            1048576L, 524288L, 262144L, 131072L,
            65536L, 32768L, 16384L, 8192L,
            4096L, 2048L, 1024L, 512L,
            256L, 128L, 64L, 32L,
            16L, 8L, 4L, 2L,
        };

        /// <summary>
        /// 对数分解表：Log1pTable[j] = round(ln(1 + 2^-j) * 2^32)，j = 1..31（下标 0 不用，置 0）。
        /// Exp 与 Log 共用同一张表，两者互为逆运算这一点因此在表级别自洽。
        /// </summary>
        private static readonly long[] Log1pTable =
        {
            0L,
            1741459379L, 958394255L, 505874286L, 260380768L,
            132163268L, 66589974L, 33424039L, 16744533L,
            8380427L, 4192257L, 2096640L, 1048448L,
            524256L, 262136L, 131070L, 65536L,
            32768L, 16384L, 8192L, 4096L,
            2048L, 1024L, 512L, 256L,
            128L, 64L, 32L, 16L,
            8L, 4L, 2L,
        };

        // ------------------------------------------------------------------
        // 状态与构造
        // ------------------------------------------------------------------

        private readonly long _raw;

        private Fix(long raw)
        {
            _raw = raw;
        }

        /// <summary>承载值的 64 位整数：realValue = RawValue / 2^32。</summary>
        public long RawValue
        {
            get { return _raw; }
        }

        // ------------------------------------------------------------------
        // 常量（fixed-point.md §3.1）
        // ------------------------------------------------------------------

        /// <summary>定点数 0。</summary>
        public static Fix Zero
        {
            get { return new Fix(0L); }
        }

        /// <summary>定点数 1。</summary>
        public static Fix One
        {
            get { return new Fix(OneRaw); }
        }

        /// <summary>圆周率 pi，raw 值 13493037705 钉死。</summary>
        public static Fix Pi
        {
            get { return new Fix(PiRaw); }
        }

        /// <summary>2*pi，raw 值 26986075409 钉死。</summary>
        public static Fix TwoPi
        {
            get { return new Fix(TwoPiRaw); }
        }

        /// <summary>pi/2，raw 值 6746518852 钉死。注意它与 Pi 各自独立取整，故 2*HalfPi 比 Pi 小 1 个 raw。</summary>
        public static Fix HalfPi
        {
            get { return new Fix(HalfPiRaw); }
        }

        /// <summary>可表示的最大定点数，raw 值即 long.MaxValue。</summary>
        public static Fix MaxValue
        {
            get { return new Fix(long.MaxValue); }
        }

        /// <summary>可表示的最小定点数，raw 值即 long.MinValue。</summary>
        public static Fix MinValue
        {
            get { return new Fix(long.MinValue); }
        }

        /// <summary>最小正定点数，raw 值 1，即分辨率 2^-32。</summary>
        public static Fix Epsilon
        {
            get { return new Fix(1L); }
        }

        /// <summary>角度转弧度的比例常量 pi/180，raw 值 74961321 钉死。</summary>
        public static Fix DegToRad
        {
            get { return new Fix(DegToRadRaw); }
        }

        /// <summary>弧度转角度的比例常量 180/pi，raw 值 246083499208 钉死。</summary>
        public static Fix RadToDeg
        {
            get { return new Fix(RadToDegRaw); }
        }

        // ------------------------------------------------------------------
        // 构造与规范形（fixed-point.md §1.2、§1.3）
        // ------------------------------------------------------------------

        /// <summary>由 64 位 raw 值直接构造。跨端通道请优先用 {low, high} 规范形。</summary>
        /// <param name="raw">承载值，realValue = raw / 2^32。</param>
        /// <returns>对应的定点数。</returns>
        public static Fix FromRawInt64(long raw)
        {
            return new Fix(raw);
        }

        /// <summary>
        /// 由 {low, high} 规范形构造。high 是整个 64 位值的高半（带符号），
        /// 因此负数是**整体二补**而不是「符号加绝对值」：FromRaw(-2147483648, -1) 即 -0.5。
        /// </summary>
        /// <param name="low">低 32 位（小数部分），按位读取。</param>
        /// <param name="high">高 32 位（整数部分），带符号。</param>
        /// <returns>对应的定点数。</returns>
        public static Fix FromRaw(int low, int high)
        {
            return new Fix(((long)high << FractionalBits) | ((long)(uint)low));
        }

        /// <summary>
        /// 拆成 {low, high} 规范形，用于序列化、哈希输入、跨端传输、录像编解码。
        /// 与 <see cref="FromRaw(int, int)"/> 对全定义域成对可逆。
        /// </summary>
        /// <returns>低半与高半两个 int32。</returns>
        public (int Low, int High) ToRaw()
        {
            return ((int)(_raw & LowMask), (int)(_raw >> FractionalBits));
        }

        /// <summary>由整数构造，等价于 raw 左移 32 位。</summary>
        /// <param name="value">整数值，须落在 [-2^31, 2^31) 内。</param>
        /// <returns>对应的定点数。</returns>
        public static Fix FromInt(int value)
        {
            return new Fix((long)value << FractionalBits);
        }

        /// <summary>
        /// 由分数构造，语义等价于 numerator / denominator 后**向零截断**。
        /// 战斗域禁小数字面量，需要 0.5 就写 FromFraction(1, 2)。
        /// </summary>
        /// <param name="numerator">分子。</param>
        /// <param name="denominator">分母，不得为 0。</param>
        /// <returns>对应的定点数。</returns>
        /// <exception cref="DivideByZeroException">分母为 0。</exception>
        public static Fix FromFraction(long numerator, long denominator)
        {
            if (denominator == 0L)
            {
                throw new DivideByZeroException("Fix.FromFraction: 分母为 0。");
            }

            bool negative = (numerator < 0L) != (denominator < 0L);
            ulong un = AbsToUnsigned(numerator);
            ulong ud = AbsToUnsigned(denominator);
            ulong magnitude = DivideU128By64(un >> (64 - FractionalBits), un << FractionalBits, ud);
            return new Fix(ApplySign(magnitude, negative));
        }

        /// <summary>
        /// 十进制字符串转定点数：**纯整数逐位解析，不经任何浮点**。
        /// 接受形如 <c>[+-]?d+(.d*)?</c> 与 <c>[+-]?.d+</c>；小数部分只取前 18 位有效数字，
        /// 结果按绝对值**向零截断**。不接受指数记号、千分位、空白与任何 culture 相关写法。
        /// </summary>
        /// <param name="text">十进制文本。</param>
        /// <returns>对应的定点数。</returns>
        /// <exception cref="ArgumentNullException">入参为 null。</exception>
        /// <exception cref="FormatException">文本不符合上述文法。</exception>
        /// <exception cref="OverflowException">整数部分超出 Q32.32 表示范围。</exception>
        public static Fix Parse(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException("text");
            }

            int i = 0;
            int n = text.Length;
            bool negative = false;
            if (i < n && (text[i] == '+' || text[i] == '-'))
            {
                negative = text[i] == '-';
                i++;
            }

            long integerPart = 0L;
            int intDigits = 0;
            while (i < n && text[i] >= '0' && text[i] <= '9')
            {
                if (integerPart > 4294967295L)
                {
                    throw new OverflowException("Fix.Parse: 整数部分超出 Q32.32 表示范围。");
                }

                integerPart = (integerPart * 10L) + (text[i] - '0');
                intDigits++;
                i++;
            }

            long fractionRaw = 0L;
            int fracDigits = 0;
            if (i < n && text[i] == '.')
            {
                i++;
                ulong numerator = 0UL;
                ulong denominator = 1UL;
                while (i < n && text[i] >= '0' && text[i] <= '9')
                {
                    // 只吃前 18 位：10^18 小于 2^63，纯整数路径不会溢出；多余位按向零截断丢弃。
                    if (fracDigits < 18)
                    {
                        numerator = (numerator * 10UL) + (ulong)(text[i] - '0');
                        denominator *= 10UL;
                    }

                    fracDigits++;
                    i++;
                }

                if (numerator != 0UL)
                {
                    fractionRaw = (long)DivideU128By64(
                        numerator >> (64 - FractionalBits), numerator << FractionalBits, denominator);
                }
            }

            if (i != n || (intDigits == 0 && fracDigits == 0))
            {
                throw new FormatException("Fix.Parse: 文本不是合法的十进制定点数写法。");
            }

            if (integerPart > 2147483648L)
            {
                throw new OverflowException("Fix.Parse: 整数部分超出 Q32.32 表示范围。");
            }

            ulong magnitude = ((ulong)integerPart << FractionalBits) + (ulong)fractionRaw;
            if (negative)
            {
                if (magnitude > 9223372036854775808UL)
                {
                    throw new OverflowException("Fix.Parse: 结果超出 Q32.32 表示范围。");
                }
            }
            else if (magnitude > 9223372036854775807UL)
            {
                throw new OverflowException("Fix.Parse: 结果超出 Q32.32 表示范围。");
            }

            return new Fix(ApplySign(magnitude, negative));
        }

        // ------------------------------------------------------------------
        // 四则运算（fixed-point.md §2「加/减」「乘」「除」行）
        // ------------------------------------------------------------------

        /// <summary>加法：64 位整数加法。溢出以 unchecked 回绕，这是已定义行为。</summary>
        /// <param name="left">被加数。</param>
        /// <param name="right">加数。</param>
        /// <returns>和。</returns>
        public static Fix operator +(Fix left, Fix right)
        {
            unchecked
            {
                long result = left._raw + right._raw;
                Debug.Assert(((left._raw ^ result) & (right._raw ^ result)) >= 0L,
                    "Fix 加法溢出：结果已按 unchecked 回绕。");
                return new Fix(result);
            }
        }

        /// <summary>减法：64 位整数减法。溢出以 unchecked 回绕，这是已定义行为。</summary>
        /// <param name="left">被减数。</param>
        /// <param name="right">减数。</param>
        /// <returns>差。</returns>
        public static Fix operator -(Fix left, Fix right)
        {
            unchecked
            {
                long result = left._raw - right._raw;
                Debug.Assert(((left._raw ^ right._raw) & (left._raw ^ result)) >= 0L,
                    "Fix 减法溢出：结果已按 unchecked 回绕。");
                return new Fix(result);
            }
        }

        /// <summary>取负。MinValue 取负按 unchecked 回绕仍为 MinValue。</summary>
        /// <param name="value">操作数。</param>
        /// <returns>相反数。</returns>
        public static Fix operator -(Fix value)
        {
            unchecked
            {
                Debug.Assert(value._raw != long.MinValue, "Fix 取负溢出：MinValue 取负回绕为自身。");
                return new Fix(-value._raw);
            }
        }

        /// <summary>
        /// 乘法：(a * b) 右移 32 位，中间量走 32 位半字拆分出的 128 位路径，结果**向零截断**。
        /// 溢出（乘积绝对值超出 64 位）以 unchecked 回绕。
        /// </summary>
        /// <param name="left">被乘数。</param>
        /// <param name="right">乘数。</param>
        /// <returns>积。</returns>
        public static Fix operator *(Fix left, Fix right)
        {
            bool negative = (left._raw < 0L) != (right._raw < 0L);
            ulong high;
            ulong low = MultiplyU64(AbsToUnsigned(left._raw), AbsToUnsigned(right._raw), out high);

            // 128 位右移 32：低半取高 32 位，高半补进来。高半再高出的部分即溢出位。
            ulong magnitude = (low >> FractionalBits) | (high << (64 - FractionalBits));
            Debug.Assert((high >> FractionalBits) == 0UL, "Fix 乘法溢出：结果已按 unchecked 回绕。");
            return new Fix(ApplySign(magnitude, negative));
        }

        /// <summary>
        /// 除法：(a 左移 32 位) / b，结果**向零截断**。
        /// 商超出 64 位时以 unchecked 回绕。
        /// </summary>
        /// <param name="left">被除数。</param>
        /// <param name="right">除数，不得为 0。</param>
        /// <returns>商。</returns>
        /// <exception cref="DivideByZeroException">除数为 0。</exception>
        public static Fix operator /(Fix left, Fix right)
        {
            if (right._raw == 0L)
            {
                throw new DivideByZeroException("Fix 除法：除数为 0。");
            }

            bool negative = (left._raw < 0L) != (right._raw < 0L);
            ulong un = AbsToUnsigned(left._raw);
            ulong magnitude = DivideU128By64(
                un >> (64 - FractionalBits), un << FractionalBits, AbsToUnsigned(right._raw));
            return new Fix(ApplySign(magnitude, negative));
        }

        // ------------------------------------------------------------------
        // 比较（fixed-point.md §2「比较」行：直接比 64 位整数，无 epsilon）
        // ------------------------------------------------------------------

        /// <summary>相等：直接比较 raw 值，**无 epsilon**，不提供近似相等。</summary>
        /// <param name="left">左操作数。</param>
        /// <param name="right">右操作数。</param>
        /// <returns>两者 raw 值相同则为真。</returns>
        public static bool operator ==(Fix left, Fix right)
        {
            return left._raw == right._raw;
        }

        /// <summary>不等：直接比较 raw 值，无 epsilon。</summary>
        /// <param name="left">左操作数。</param>
        /// <param name="right">右操作数。</param>
        /// <returns>两者 raw 值不同则为真。</returns>
        public static bool operator !=(Fix left, Fix right)
        {
            return left._raw != right._raw;
        }

        /// <summary>小于。</summary>
        /// <param name="left">左操作数。</param>
        /// <param name="right">右操作数。</param>
        /// <returns>左小于右则为真。</returns>
        public static bool operator <(Fix left, Fix right)
        {
            return left._raw < right._raw;
        }

        /// <summary>大于。</summary>
        /// <param name="left">左操作数。</param>
        /// <param name="right">右操作数。</param>
        /// <returns>左大于右则为真。</returns>
        public static bool operator >(Fix left, Fix right)
        {
            return left._raw > right._raw;
        }

        /// <summary>小于等于。</summary>
        /// <param name="left">左操作数。</param>
        /// <param name="right">右操作数。</param>
        /// <returns>左不大于右则为真。</returns>
        public static bool operator <=(Fix left, Fix right)
        {
            return left._raw <= right._raw;
        }

        /// <summary>大于等于。</summary>
        /// <param name="left">左操作数。</param>
        /// <param name="right">右操作数。</param>
        /// <returns>左不小于右则为真。</returns>
        public static bool operator >=(Fix left, Fix right)
        {
            return left._raw >= right._raw;
        }

        /// <summary>与另一个定点数比较，逐位相等即相等。</summary>
        /// <param name="other">另一个定点数。</param>
        /// <returns>raw 值相同则为真。</returns>
        public bool Equals(Fix other)
        {
            return _raw == other._raw;
        }

        /// <summary>与任意对象比较。</summary>
        /// <param name="obj">另一个对象。</param>
        /// <returns>对方是 Fix 且 raw 值相同则为真。</returns>
        public override bool Equals(object obj)
        {
            return obj is Fix && ((Fix)obj)._raw == _raw;
        }

        /// <summary>
        /// 值派生的哈希码。**不得用于任何影响世界状态或进哈希域的计算**
        /// （哈希域一律走 {low, high} 规范形，见 fixed-point.md §5.1）；此处只为满足容器与相等契约。
        /// </summary>
        /// <returns>由 raw 值高低半异或得到的哈希码。</returns>
        public override int GetHashCode()
        {
            return (int)_raw ^ (int)(_raw >> FractionalBits);
        }

        /// <summary>序关系比较。</summary>
        /// <param name="other">另一个定点数。</param>
        /// <returns>小于返回负数，相等返回 0，大于返回正数。</returns>
        public int CompareTo(Fix other)
        {
            if (_raw < other._raw)
            {
                return -1;
            }

            return _raw > other._raw ? 1 : 0;
        }

        // ------------------------------------------------------------------
        // 取整与转整数（fixed-point.md §2「取整」「转整数」行）
        // ------------------------------------------------------------------

        /// <summary>向负无穷取整。</summary>
        /// <returns>不大于本值的最大整数。</returns>
        public int FloorToInt()
        {
            return (int)(_raw >> FractionalBits);
        }

        /// <summary>向正无穷取整。</summary>
        /// <returns>不小于本值的最小整数。</returns>
        public int CeilToInt()
        {
            return (int)((_raw + (OneRaw - 1L)) >> FractionalBits);
        }

        /// <summary>四舍五入，**半值向上**（等价于 floor(x + 1/2)，负半值同样向上）。</summary>
        /// <returns>最近的整数，半值取较大者。</returns>
        public int RoundToInt()
        {
            return (int)((_raw + (OneRaw >> 1)) >> FractionalBits);
        }

        /// <summary>向零截断（fixed-point.md §2「转整数」行）。</summary>
        /// <returns>绝对值不大于本值的整数。</returns>
        public int TruncToInt()
        {
            return _raw >= 0L
                ? (int)(_raw >> FractionalBits)
                : -(int)((-_raw) >> FractionalBits);
        }

        // ------------------------------------------------------------------
        // 标量数学（fixed-point.md §3.2）
        // ------------------------------------------------------------------

        /// <summary>绝对值。MinValue 取绝对值按 unchecked 回绕仍为 MinValue。</summary>
        /// <param name="value">操作数。</param>
        /// <returns>绝对值。</returns>
        public static Fix Abs(Fix value)
        {
            unchecked
            {
                Debug.Assert(value._raw != long.MinValue, "Fix.Abs 溢出：MinValue 取绝对值回绕为自身。");
                return new Fix(value._raw < 0L ? -value._raw : value._raw);
            }
        }

        /// <summary>取较小者。</summary>
        /// <param name="a">操作数一。</param>
        /// <param name="b">操作数二。</param>
        /// <returns>两者中较小的一个。</returns>
        public static Fix Min(Fix a, Fix b)
        {
            return a._raw < b._raw ? a : b;
        }

        /// <summary>取较大者。</summary>
        /// <param name="a">操作数一。</param>
        /// <param name="b">操作数二。</param>
        /// <returns>两者中较大的一个。</returns>
        public static Fix Max(Fix a, Fix b)
        {
            return a._raw > b._raw ? a : b;
        }

        /// <summary>
        /// 夹取到 [min, max]。先夹下界再夹上界，因此**区间反了（min 大于 max）时返回 max**，
        /// 这一分支是钉死的行为而非未定义行为。
        /// </summary>
        /// <param name="value">待夹取的值。</param>
        /// <param name="min">下界。</param>
        /// <param name="max">上界。</param>
        /// <returns>夹取结果。</returns>
        public static Fix Clamp(Fix value, Fix min, Fix max)
        {
            long r = value._raw;
            if (r < min._raw)
            {
                r = min._raw;
            }

            if (r > max._raw)
            {
                r = max._raw;
            }

            return new Fix(r);
        }

        /// <summary>
        /// 线性插值 a + (b - a) * t，**不夹取 t**：t 出 [0, 1] 即外插。
        /// </summary>
        /// <param name="a">t = 0 时的取值。</param>
        /// <param name="b">t = 1 时的取值。</param>
        /// <param name="t">插值参数，不作夹取。</param>
        /// <returns>插值结果。</returns>
        public static Fix Lerp(Fix a, Fix b, Fix t)
        {
            return a + ((b - a) * t);
        }

        /// <summary>
        /// 取余：符号随被除数（向零截断的余数），等价于 raw 值的整数取余，因此是精确运算。
        /// </summary>
        /// <param name="a">被除数。</param>
        /// <param name="b">除数，不得为 0。</param>
        /// <returns>余数。</returns>
        /// <exception cref="DivideByZeroException">除数为 0。</exception>
        public static Fix Mod(Fix a, Fix b)
        {
            if (b._raw == 0L)
            {
                throw new DivideByZeroException("Fix.Mod: 除数为 0。");
            }

            return new Fix(a._raw % b._raw);
        }

        /// <summary>
        /// 平方根：定点整数开方，结果**向零截断**，精确等于 floor(sqrt(raw * 2^32))。
        /// 逐位还原算法，与任何浮点实现无关，同输入必同输出。
        /// </summary>
        /// <param name="value">被开方数，不得为负。</param>
        /// <returns>算术平方根。</returns>
        /// <exception cref="ArgumentOutOfRangeException">入参为负。</exception>
        public static Fix Sqrt(Fix value)
        {
            if (value._raw < 0L)
            {
                throw new ArgumentOutOfRangeException("value", "Fix.Sqrt: 入参为负。");
            }

            ulong raw = (ulong)value._raw;
            return new Fix((long)SqrtU128((ulong)(raw >> (64 - FractionalBits)), raw << FractionalBits));
        }

        // ------------------------------------------------------------------
        // 三角函数（fixed-point.md §2「sin/cos/tan/asin/atan/atan2」行）
        // ------------------------------------------------------------------

        /// <summary>
        /// 正弦。先以钉死的 pi/2 常量做**纯整数**象限归约，再走 32 级 CORDIC 圆周旋转。
        /// 实测 |err| 不超过 2^-28（|x| 不超过 4pi）。不调用任何宿主浮点三角函数。
        /// </summary>
        /// <param name="angle">弧度。</param>
        /// <returns>正弦值。</returns>
        public static Fix Sin(Fix angle)
        {
            long sin;
            long cos;
            SinCosRaw(angle._raw, out sin, out cos);
            return new Fix(sin);
        }

        /// <summary>
        /// 余弦。与 <see cref="Sin"/> 同一次 CORDIC 求出，象限规则同。
        /// 实测 |err| 不超过 2^-28（|x| 不超过 4pi）。
        /// </summary>
        /// <param name="angle">弧度。</param>
        /// <returns>余弦值。</returns>
        public static Fix Cos(Fix angle)
        {
            long sin;
            long cos;
            SinCosRaw(angle._raw, out sin, out cos);
            return new Fix(cos);
        }

        // ------------------------------------------------------------------
        // 指数与对数（fixed-point.md §2「exp/log」行）
        // ------------------------------------------------------------------

        /// <summary>
        /// 自然指数。先以钉死的 ln2 常量做纯整数范围规约 x = k*ln2 + r，
        /// 再对 r 做 ln(1+2^-j) 表的二进制分解，最后按 2^k 移位。
        /// 实测绝对误差不超过 2^-21（|x| 不超过 6）；相对误差不超过 2^-28（x 落在 [0, 21]）。
        /// x 越负结果越小，相对误差最终由 Q32.32 的分辨率而非算法决定。
        /// </summary>
        /// <param name="value">指数。</param>
        /// <returns>e 的 value 次幂。</returns>
        /// <exception cref="OverflowException">结果超出 Q32.32 表示范围（约 value 大于 21.49）。</exception>
        public static Fix Exp(Fix value)
        {
            long k = FloorDiv(value._raw, Ln2Raw);

            // 先判上溢与下溢再算尾数：极端入参下 k * Ln2Raw 本身就会回绕，
            // 提前返回可以让后面的尾数计算永远处在良定义的量程里。
            if (k >= 31L)
            {
                throw new OverflowException("Fix.Exp: 结果超出 Q32.32 表示范围。");
            }

            if (k <= -64L)
            {
                return Zero;
            }

            long r = value._raw - (k * Ln2Raw);

            long y = OneRaw;
            for (int j = 1; j < 32; j++)
            {
                long step = Log1pTable[j];
                if (r >= step)
                {
                    r -= step;
                    y += RoundShift(y, j);
                }
            }

            // 残量 r 已小于 ln(1+2^-31)，用一阶项收尾：exp(r) 约等于 1 + r，误差 O(r^2) 即 2^-60 量级。
            y += (long)(((ulong)y * (ulong)r) >> FractionalBits);

            return new Fix(k >= 0L ? y << (int)k : y >> (int)(-k));
        }

        /// <summary>
        /// 自然对数。先按最高位分离出二进制指数 e（精确），再对尾数走同一张 ln(1+2^-j) 表，
        /// 残量用一次定点除法收尾。实测 |err| 不超过 2^-28（全定义域）。
        /// </summary>
        /// <param name="value">真数，必须为正。</param>
        /// <returns>自然对数。</returns>
        /// <exception cref="ArgumentOutOfRangeException">入参非正。</exception>
        public static Fix Log(Fix value)
        {
            if (value._raw <= 0L)
            {
                throw new ArgumentOutOfRangeException("value", "Fix.Log: 入参必须为正。");
            }

            int exponent = BitLength((ulong)value._raw) - 1 - FractionalBits;
            long mantissa = exponent > 0
                ? value._raw >> exponent
                : value._raw << (-exponent);

            long acc = exponent * Ln2Raw;
            long t = OneRaw;
            for (int j = 1; j < 32; j++)
            {
                long candidate = t + RoundShift(t, j);
                if (candidate <= mantissa)
                {
                    t = candidate;
                    acc += Log1pTable[j];
                }
            }

            acc += (new Fix(mantissa - t) / new Fix(t))._raw;
            return new Fix(acc);
        }

        // ------------------------------------------------------------------
        // 角度与弧度（fixed-point.md §2「角度/弧度」行）
        // ------------------------------------------------------------------

        /// <summary>角度转弧度，走钉死的 pi/180 常量做一次定点乘（向零截断），不出现除法。</summary>
        /// <param name="degrees">角度。</param>
        /// <returns>弧度。</returns>
        public static Fix Rad(Fix degrees)
        {
            return degrees * DegToRad;
        }

        /// <summary>弧度转角度，走钉死的 180/pi 常量做一次定点乘（向零截断），不出现除法。</summary>
        /// <param name="radians">弧度。</param>
        /// <returns>角度。</returns>
        public static Fix Deg(Fix radians)
        {
            return radians * RadToDeg;
        }

        // ------------------------------------------------------------------
        // 出域转换（fixed-point.md §2「转整数」行、§5.1）——只许表现与日志用
        // ------------------------------------------------------------------

        /// <summary>
        /// **出域转换，仅限表现层与日志**。战斗域内调用即判红。
        /// 结果一律不得回写世界状态，也不得进哈希域。
        /// </summary>
        /// <returns>对应的双精度近似值。</returns>
        public double ToDouble() => (double)_raw / (double)(1L << FractionalBits);   // nocheck: 定点→浮点唯一出域口，只给表现层读

        /// <summary>
        /// **出域转换，仅限日志与工具**：逐位整数格式化成十进制文本，不依赖任何 culture，
        /// 小数部分最多 10 位且不补尾零。**禁止把结果送进哈希域**（fixed-point.md §5.1）。
        /// </summary>
        /// <returns>形如 <c>-12.25</c> 的十进制文本。</returns>
        public string ToStringInvariant()
        {
            long raw = _raw;
            bool negative = raw < 0L;
            ulong magnitude = AbsToUnsigned(raw);
            ulong integerPart = magnitude >> FractionalBits;
            ulong fractionPart = magnitude & (ulong)LowMask;

            char[] buffer = new char[24];
            int end = buffer.Length;

            // 小数位：反复乘 10 取进位，最多 10 位（2^-32 约 2.3e-10，10 位即够分辨一个 raw）。
            if (fractionPart != 0UL)
            {
                char[] fraction = new char[10];
                int count = 0;
                while (fractionPart != 0UL && count < 10)
                {
                    fractionPart *= 10UL;
                    fraction[count] = (char)('0' + (int)(fractionPart >> FractionalBits));
                    fractionPart &= (ulong)LowMask;
                    count++;
                }

                for (int i = count - 1; i >= 0; i--)
                {
                    buffer[--end] = fraction[i];
                }

                buffer[--end] = '.';
            }

            if (integerPart == 0UL)
            {
                buffer[--end] = '0';
            }
            else
            {
                while (integerPart != 0UL)
                {
                    buffer[--end] = (char)('0' + (int)(integerPart % 10UL));
                    integerPart /= 10UL;
                }
            }

            if (negative)
            {
                buffer[--end] = '-';
            }

            return new string(buffer, end, buffer.Length - end);
        }

        // ------------------------------------------------------------------
        // 私有：整数原语
        // ------------------------------------------------------------------

        /// <summary>取绝对值并转成无符号；long.MinValue 的绝对值恰为 2^63，无符号下可精确表示。</summary>
        private static ulong AbsToUnsigned(long value)
        {
            unchecked
            {
                return value < 0L ? (ulong)(-value) : (ulong)value;
            }
        }

        /// <summary>把无符号量值按符号还原成 64 位有符号 raw，溢出即 unchecked 回绕。</summary>
        private static long ApplySign(ulong magnitude, bool negative)
        {
            unchecked
            {
                return negative ? -(long)magnitude : (long)magnitude;
            }
        }

        /// <summary>四舍五入右移（半值向上）。内部辅助移位统一走这一条规则。</summary>
        private static long RoundShift(long value, int bits)
        {
            if (bits <= 0)
            {
                return value;
            }

            return (value + (1L << (bits - 1))) >> bits;
        }

        /// <summary>向负无穷的整数除法（C# 的 / 是向零截断，这里要 floor）。</summary>
        private static long FloorDiv(long a, long b)
        {
            long q = a / b;
            if ((a % b != 0L) && ((a < 0L) != (b < 0L)))
            {
                q--;
            }

            return q;
        }

        /// <summary>最高有效位位置（1 起数）；入参为 0 时返回 0。</summary>
        private static int BitLength(ulong value)
        {
            int n = 0;
            while (value != 0UL)
            {
                value >>= 1;
                n++;
            }

            return n;
        }

        /// <summary>
        /// 64x64 到 128 位无符号乘：拆成 32 位半字，四个部分积手工进位。
        /// netstandard2.1 没有 Math.BigMul，也不许用 System.Math，故自实现。
        /// </summary>
        private static ulong MultiplyU64(ulong a, ulong b, out ulong high)
        {
            ulong a0 = a & 0xFFFFFFFFUL;
            ulong a1 = a >> 32;
            ulong b0 = b & 0xFFFFFFFFUL;
            ulong b1 = b >> 32;

            ulong p00 = a0 * b0;
            ulong p01 = a0 * b1;
            ulong p10 = a1 * b0;
            ulong p11 = a1 * b1;

            ulong middle = p01 + (p00 >> 32);
            ulong middleWithCarry = middle + p10;
            ulong carry = middleWithCarry < middle ? 1UL : 0UL;

            high = p11 + (middleWithCarry >> 32) + (carry << 32);
            return (middleWithCarry << 32) | (p00 & 0xFFFFFFFFUL);
        }

        /// <summary>
        /// 128 位除以 64 位，返回商的低 64 位（商超出 64 位即回绕，与乘法溢出同口径）。
        /// 高半为 0 时走一次硬件整数除法；否则逐位还原除法，128 轮，无浮点。
        /// </summary>
        private static ulong DivideU128By64(ulong high, ulong low, ulong divisor)
        {
            if (high == 0UL)
            {
                return low / divisor;
            }

            ulong quotient = 0UL;
            ulong remainder = 0UL;
            for (int i = 127; i >= 0; i--)
            {
                bool remainderOverflow = (remainder & 0x8000000000000000UL) != 0UL;
                ulong bit = i >= 64 ? ((high >> (i - 64)) & 1UL) : ((low >> i) & 1UL);
                remainder = (remainder << 1) | bit;
                if (remainderOverflow || remainder >= divisor)
                {
                    remainder -= divisor;
                    if (i < 64)
                    {
                        quotient |= 1UL << i;
                    }
                }
            }

            return quotient;
        }

        /// <summary>128 位整数开方（逐位还原法），返回 floor(sqrt(high:low))。</summary>
        private static ulong SqrtU128(ulong high, ulong low)
        {
            ulong root = 0UL;
            ulong remainder = 0UL;
            for (int i = 0; i < 64; i++)
            {
                // 每轮吃掉最高的两位：位 (127-2i) 与 (126-2i)。移位量恒为偶数，
                // 因此这一对永远不会跨在 high/low 的边界上，分支只需二选一。
                int shift = 126 - (2 * i);
                ulong pair = shift >= 64
                    ? (high >> (shift - 64)) & 3UL
                    : (low >> shift) & 3UL;
                remainder = (remainder << 2) | pair;
                root <<= 1;
                if (remainder > (2UL * root))
                {
                    remainder -= (2UL * root) + 1UL;
                    root += 1UL;
                }
            }

            return root;
        }

        // ------------------------------------------------------------------
        // 私有：CORDIC
        // ------------------------------------------------------------------

        /// <summary>
        /// 象限归约 + CORDIC 圆周旋转，一次算出 sin 与 cos。
        /// 归约用钉死的 pi/2 raw 常量做纯整数 floor 除法与减法，绝无浮点；
        /// |x| 越大，归约累积的常量误差越大（每个 pi/2 约 0.35 个 raw），故精度界按 |x| 分段声明。
        /// </summary>
        private static void SinCosRaw(long angle, out long sin, out long cos)
        {
            long quadrant = FloorDiv(angle, HalfPiRaw);
            long residual = angle - (quadrant * HalfPiRaw);

            long x = CordicInvGainRaw;
            long y = 0L;
            long z = residual;
            for (int i = 0; i < CordicIterations; i++)
            {
                long dx = RoundShift(x, i);
                long dy = RoundShift(y, i);
                if (z >= 0L)
                {
                    x -= dy;
                    y += dx;
                    z -= AtanTable[i];
                }
                else
                {
                    x += dy;
                    y -= dx;
                    z += AtanTable[i];
                }
            }

            // x = cos(residual)，y = sin(residual)，residual 落在 [0, pi/2)。
            switch ((int)(quadrant & 3L))
            {
                case 0:
                    sin = y;
                    cos = x;
                    break;
                case 1:
                    sin = x;
                    cos = -y;
                    break;
                case 2:
                    sin = -y;
                    cos = -x;
                    break;
                default:
                    sin = -x;
                    cos = y;
                    break;
            }
        }
    }
}
