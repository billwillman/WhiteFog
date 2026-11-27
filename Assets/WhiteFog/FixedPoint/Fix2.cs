// Q32.32 定点二维向量。战斗核平台层，netstandard2.1，零引擎依赖。
//
// 语义真源：Docs/design/fixed-point.md §3.3（向量面）、§3.6（工具层不得自造数值技巧——
//           本类型的每一条运算都只由 Fix 的标量原语组合而成，绝不另起数值路径）。
// 规范形：§1.2「向量另有 to_rawtable：n 维向量展开为 2n 个整数」。

using System;

namespace Dig.Framework
{
    /// <summary>
    /// 二维定点向量，值类型、不分配。所有分量运算逐条落在 <see cref="Fix"/> 的标量语义上。
    /// </summary>
    public readonly struct Fix2 : IEquatable<Fix2>
    {
        /// <summary>X 分量。</summary>
        public readonly Fix X;

        /// <summary>Y 分量。</summary>
        public readonly Fix Y;

        /// <summary>按分量构造。</summary>
        /// <param name="x">X 分量。</param>
        /// <param name="y">Y 分量。</param>
        public Fix2(Fix x, Fix y)
        {
            X = x;
            Y = y;
        }

        /// <summary>零向量。</summary>
        public static Fix2 Zero
        {
            get { return new Fix2(Fix.Zero, Fix.Zero); }
        }

        /// <summary>两个分量都为 1 的向量。</summary>
        public static Fix2 One
        {
            get { return new Fix2(Fix.One, Fix.One); }
        }

        /// <summary>逐分量相加。</summary>
        /// <param name="left">左操作数。</param>
        /// <param name="right">右操作数。</param>
        /// <returns>和向量。</returns>
        public static Fix2 operator +(Fix2 left, Fix2 right)
        {
            return new Fix2(left.X + right.X, left.Y + right.Y);
        }

        /// <summary>逐分量相减。</summary>
        /// <param name="left">被减向量。</param>
        /// <param name="right">减向量。</param>
        /// <returns>差向量。</returns>
        public static Fix2 operator -(Fix2 left, Fix2 right)
        {
            return new Fix2(left.X - right.X, left.Y - right.Y);
        }

        /// <summary>逐分量取负。</summary>
        /// <param name="value">操作数。</param>
        /// <returns>反向向量。</returns>
        public static Fix2 operator -(Fix2 value)
        {
            return new Fix2(-value.X, -value.Y);
        }

        /// <summary>数乘。</summary>
        /// <param name="value">向量。</param>
        /// <param name="scalar">标量。</param>
        /// <returns>缩放后的向量。</returns>
        public static Fix2 operator *(Fix2 value, Fix scalar)
        {
            return new Fix2(value.X * scalar, value.Y * scalar);
        }

        /// <summary>数除。</summary>
        /// <param name="value">向量。</param>
        /// <param name="scalar">标量，不得为 0。</param>
        /// <returns>缩放后的向量。</returns>
        public static Fix2 operator /(Fix2 value, Fix scalar)
        {
            return new Fix2(value.X / scalar, value.Y / scalar);
        }

        /// <summary>逐分量相等（无 epsilon）。</summary>
        /// <param name="left">左操作数。</param>
        /// <param name="right">右操作数。</param>
        /// <returns>两个分量都相等则为真。</returns>
        public static bool operator ==(Fix2 left, Fix2 right)
        {
            return left.X == right.X && left.Y == right.Y;
        }

        /// <summary>逐分量不等（无 epsilon）。</summary>
        /// <param name="left">左操作数。</param>
        /// <param name="right">右操作数。</param>
        /// <returns>存在分量不等则为真。</returns>
        public static bool operator !=(Fix2 left, Fix2 right)
        {
            return !(left == right);
        }

        /// <summary>点积。</summary>
        /// <param name="a">向量一。</param>
        /// <param name="b">向量二。</param>
        /// <returns>点积标量。</returns>
        public static Fix Dot(Fix2 a, Fix2 b)
        {
            return (a.X * b.X) + (a.Y * b.Y);
        }

        /// <summary>长度的平方（不开方，热路径比较长度时用它）。</summary>
        /// <returns>长度平方。</returns>
        public Fix LengthSquared()
        {
            return (X * X) + (Y * Y);
        }

        /// <summary>长度，等价于 Sqrt(LengthSquared())，因此继承 Sqrt 的向零截断语义。</summary>
        /// <returns>长度。</returns>
        public Fix Length()
        {
            return Fix.Sqrt(LengthSquared());
        }

        /// <summary>
        /// 归一化。**零向量归一化返回零向量**（不抛，也不产生除零），这是钉死的分支。
        /// </summary>
        /// <returns>单位向量，或零向量。</returns>
        public Fix2 Normalize()
        {
            Fix length = Length();
            if (length == Fix.Zero)
            {
                return Zero;
            }

            return new Fix2(X / length, Y / length);
        }

        /// <summary>两点距离。</summary>
        /// <param name="a">点一。</param>
        /// <param name="b">点二。</param>
        /// <returns>距离。</returns>
        public static Fix Distance(Fix2 a, Fix2 b)
        {
            return (b - a).Length();
        }

        /// <summary>两点距离的平方。</summary>
        /// <param name="a">点一。</param>
        /// <param name="b">点二。</param>
        /// <returns>距离平方。</returns>
        public static Fix DistanceSquared(Fix2 a, Fix2 b)
        {
            return (b - a).LengthSquared();
        }

        /// <summary>逐分量线性插值，**不夹取 t**（与 <see cref="Fix.Lerp"/> 同口径）。</summary>
        /// <param name="a">t = 0 时的取值。</param>
        /// <param name="b">t = 1 时的取值。</param>
        /// <param name="t">插值参数。</param>
        /// <returns>插值结果。</returns>
        public static Fix2 Lerp(Fix2 a, Fix2 b, Fix t)
        {
            return new Fix2(Fix.Lerp(a.X, b.X, t), Fix.Lerp(a.Y, b.Y, t));
        }

        /// <summary>位移向量：从 from 指向 to，即 to - from。</summary>
        /// <param name="from">起点。</param>
        /// <param name="to">终点。</param>
        /// <returns>位移向量。</returns>
        public static Fix2 Delta(Fix2 from, Fix2 to)
        {
            return to - from;
        }

        /// <summary>
        /// 拆成 {low, high} 规范形，2 维展开为 4 个 int32，顺序为 X.low、X.high、Y.low、Y.high。
        /// </summary>
        /// <returns>四个 int32。</returns>
        public (int XLow, int XHigh, int YLow, int YHigh) ToRaw()
        {
            (int Low, int High) x = X.ToRaw();
            (int Low, int High) y = Y.ToRaw();
            return (x.Low, x.High, y.Low, y.High);
        }

        /// <summary>由 {low, high} 规范形构造，与 <see cref="ToRaw"/> 成对可逆。</summary>
        /// <param name="xLow">X 分量低半。</param>
        /// <param name="xHigh">X 分量高半。</param>
        /// <param name="yLow">Y 分量低半。</param>
        /// <param name="yHigh">Y 分量高半。</param>
        /// <returns>对应的向量。</returns>
        public static Fix2 FromRaw(int xLow, int xHigh, int yLow, int yHigh)
        {
            return new Fix2(Fix.FromRaw(xLow, xHigh), Fix.FromRaw(yLow, yHigh));
        }

        /// <summary>逐分量相等（无 epsilon）。</summary>
        /// <param name="other">另一个向量。</param>
        /// <returns>两个分量都相等则为真。</returns>
        public bool Equals(Fix2 other)
        {
            return X == other.X && Y == other.Y;
        }

        /// <summary>与任意对象比较。</summary>
        /// <param name="obj">另一个对象。</param>
        /// <returns>对方是 Fix2 且逐分量相等则为真。</returns>
        public override bool Equals(object obj)
        {
            return obj is Fix2 && Equals((Fix2)obj);
        }

        /// <summary>
        /// 值派生的哈希码。**不得用于任何影响世界状态或进哈希域的计算**（见 fixed-point.md §5.1）。
        /// </summary>
        /// <returns>由两个分量 raw 值派生的哈希码。</returns>
        public override int GetHashCode()
        {
            long combined = X.RawValue ^ (Y.RawValue * 31L);
            return (int)combined ^ (int)(combined >> 32);
        }

        /// <summary>**出域转换，仅限日志与工具**：形如 <c>(1.5, -2.25)</c>，不依赖 culture。</summary>
        /// <returns>十进制文本。</returns>
        public string ToStringInvariant()
        {
            return "(" + X.ToStringInvariant() + ", " + Y.ToStringInvariant() + ")";
        }
    }
}
