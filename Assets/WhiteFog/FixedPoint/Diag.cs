// 诊断文本的整数格式化。战斗核平台层，netstandard2.1，零引擎依赖。
//
// 真源：docs/design/host-runtime.md §二（战斗域禁运行时数值文本解析，数值文本一律走不依赖
//       culture 的路径）、docs/design/determinism-guards.md「规矩四」的禁地址派生
//       （默认的 object.ToString() 打印类型名与引用，不许进诊断文本）。
//
// 为什么自己写一遍十进制：战斗域里唯一需要数值文本的地方是**异常消息**（frame-core.md §3.1
// 要求过期帧号抛异常时带上 curFrame 与 frameNum）。`int.ToString()` 的负号与数字形状随
// CurrentCulture 变；本项目不接受「诊断信息随机器区域设置变形」，也不愿为一句异常消息
// 把 System.Globalization 拉进战斗核的引用面（纯净门只许 netstandard.dll）。
// 这里的实现只用整数除余与 ASCII 数字，与任何 culture 无关。

// ★**`internal` → `public`**。模拟内核（World／FrameWorld／ECA…）
// 随本窗迁进 `Dig.Battle`，而它们一直在用这一处格式化——同程序集时 `internal` 够，跨程序集就不够了。
// **不给内核再抄一份**（那会是第七份 `*Diag`，正是 E20「六份格式化并一」要消灭的东西）；
// 开放访问级是 E20 那条并轨的第一步，也是唯一不制造新副本的走法。
// 公开面因此多一个类型——它是**零状态、零依赖的纯函数**，与 `Fix` 同族，不破「纯底座」的定义
//（这一条原由 `tests/FrameCoreTests.cs` 的 `Framework_Assembly_IsPureBase` 钉成机检面，
// 该支已随机制单元档退役；引用面那一半今天同样靠 review 兜）。

namespace Dig.Framework
{
    /// <summary>诊断文本工具：把整数格成与 culture 无关的十进制文本。</summary>
    public static class Diag
    {
        /// <summary>把一个 64 位整数格成十进制文本（负号为 ASCII '-'）。</summary>
        /// <param name="value">整数值。</param>
        /// <returns>十进制文本。</returns>
        public static string Num(long value)
        {
            if (value == 0L)
            {
                return "0";
            }

            // 先在负数域上算，避免 long.MinValue 取负溢出。
            bool negative = value < 0L;
            long rest = negative ? value : -value;

            char[] digits = new char[20];
            int index = digits.Length;
            while (rest != 0L)
            {
                long digit = -(rest % 10L);
                rest /= 10L;
                index--;
                digits[index] = (char)('0' + (int)digit);
            }

            if (negative)
            {
                index--;
                digits[index] = '-';
            }

            return new string(digits, index, digits.Length - index);
        }
    }
}
