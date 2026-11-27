using System;

namespace Dig.Framework
{
    /// <summary>三维 Q32.32 向量。分量运算沿用 Fix 的舍入和溢出规则。</summary>
    public readonly struct Fix3 : IEquatable<Fix3>
    {
        public readonly Fix X;
        public readonly Fix Y;
        public readonly Fix Z;

        public Fix3(Fix x, Fix y, Fix z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static Fix3 Zero => default;
        public static Fix3 One => new Fix3(Fix.One, Fix.One, Fix.One);

        public static Fix3 operator +(Fix3 left, Fix3 right) =>
            new Fix3(left.X + right.X, left.Y + right.Y, left.Z + right.Z);

        public static Fix3 operator -(Fix3 left, Fix3 right) =>
            new Fix3(left.X - right.X, left.Y - right.Y, left.Z - right.Z);

        public static Fix3 operator -(Fix3 value) => new Fix3(-value.X, -value.Y, -value.Z);

        public static Fix3 operator *(Fix3 value, Fix scalar) =>
            new Fix3(value.X * scalar, value.Y * scalar, value.Z * scalar);

        public static Fix3 operator /(Fix3 value, Fix scalar) =>
            new Fix3(value.X / scalar, value.Y / scalar, value.Z / scalar);

        public static bool operator ==(Fix3 left, Fix3 right) => left.Equals(right);
        public static bool operator !=(Fix3 left, Fix3 right) => !left.Equals(right);

        public static Fix Dot(Fix3 a, Fix3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        /// <summary>右手系叉积。X 轴单位向量叉乘 Y 轴单位向量得到正 Z 轴。</summary>
        public static Fix3 Cross(Fix3 a, Fix3 b)
        {
            return new Fix3(
                a.Y * b.Z - a.Z * b.Y,
                a.Z * b.X - a.X * b.Z,
                a.X * b.Y - a.Y * b.X);
        }

        public Fix LengthSquared() => Dot(this, this);
        public Fix Length() => Fix.Sqrt(LengthSquared());

        public Fix3 Normalize()
        {
            Fix length = Length();
            return length == Fix.Zero ? Zero : this / length;
        }

        public static Fix3 Delta(Fix3 from, Fix3 to) => to - from;
        public static Fix DistanceSquared(Fix3 a, Fix3 b) => (b - a).LengthSquared();
        public static Fix Distance(Fix3 a, Fix3 b) => (b - a).Length();

        /// <summary>逐分量插值，t 可以超出 [0, 1]。</summary>
        public static Fix3 Lerp(Fix3 a, Fix3 b, Fix t) =>
            new Fix3(Fix.Lerp(a.X, b.X, t), Fix.Lerp(a.Y, b.Y, t), Fix.Lerp(a.Z, b.Z, t));

        /// <summary>按 X、Y、Z 顺序输出各分量的低、高 32 位。</summary>
        public (int XLow, int XHigh, int YLow, int YHigh, int ZLow, int ZHigh) ToRaw()
        {
            var x = X.ToRaw();
            var y = Y.ToRaw();
            var z = Z.ToRaw();
            return (x.Low, x.High, y.Low, y.High, z.Low, z.High);
        }

        public static Fix3 FromRaw(int xLow, int xHigh, int yLow, int yHigh, int zLow, int zHigh) =>
            new Fix3(Fix.FromRaw(xLow, xHigh), Fix.FromRaw(yLow, yHigh), Fix.FromRaw(zLow, zHigh));

        public bool Equals(Fix3 other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object obj) => obj is Fix3 other && Equals(other);

        public override int GetHashCode()
        {
            long combined = unchecked(X.RawValue ^ (Y.RawValue * 31L) ^ (Z.RawValue * 961L));
            return unchecked((int)combined ^ (int)(combined >> 32));
        }

        public string ToStringInvariant() =>
            "(" + X.ToStringInvariant() + ", " + Y.ToStringInvariant() + ", " + Z.ToStringInvariant() + ")";
    }
}
