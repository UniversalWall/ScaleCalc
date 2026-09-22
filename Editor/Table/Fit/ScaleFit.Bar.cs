using System;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// **逐行字符条**：用纯字符串画"一眼看出哪档最惨"——零自定义控件 ⇒ 可逐字符断言。
    /// <para>读法：外框 <c>├…┤</c> 一格 = 一个 <c>cells</c> 分之一；**实心 <c>█</c>** = 实际画布；
    /// **<c>·</c>** = 还差的那截（⇒ 裁切）；**<c>┊</c>** = 参考画布到此为止（⇒ 它右边多出来的 <c>█</c> 就是留白）。
    /// 所以：条短 = 裁、条长过 <c>┊</c> = 留。</para>
    /// <para>🔴 **它不参与任何判据**：精确读数永远看数值列——字符条只负责"哪档最惨"。
    /// 比例尺 = <c>max(实际画布, 参考画布)</c>（两种量都不出框；用 <c>min</c> 会让裁切侧永远满格、看不出差别）。</para>
    /// </summary>
    public static partial class ScaleFit
    {
        /// <summary>`cells` 的下限（再少就看不出差别）。</summary>
        public const int BarMinCells = 4;

        /// <summary>`cells` 的上限（再多就换行/溢出）。</summary>
        public const int BarMaxCells = 64;

        /// <summary>默认格数（8~16 格量级就够看出差别）。</summary>
        public const int BarDefaultCells = 16;

        /// <summary>字符条本体：`├────────████████┤`。无画布/参考无效 ⇒ 与量值列**同一套标记**（不编图）。</summary>
        public static string Bar(in TierFit fit, Axis axis, int cells = BarDefaultCells)
        {
            AxisFit value = axis == Axis.Horizontal ? fit.Horizontal : fit.Vertical;
            if (!IsUsable(fit.Horizontal.Reference) || !IsUsable(fit.Vertical.Reference)) return "—";
            if (!fit.HasCanvas) return "（无画布尺寸）";

            int width = Math.Min(BarMaxCells, Math.Max(BarMinCells, cells));
            float denominator = Math.Max(value.Canvas, value.Reference);
            if (!IsUsable(denominator)) return "—";

            int filled = ClampCells((int)Math.Round(width * (double)value.Canvas / denominator), width);
            int mark = ClampCells((int)Math.Round(width * (double)value.Reference / denominator), width);

            string inner = string.Empty;
            for (int i = 1; i <= width; i++)
            {
                if (i == mark && mark < width) inner += "┊";   // 参考到此为止：右边多出来的是留白
                else if (i <= filled) inner += "█";
                else inner += "·";
            }
            return "├" + inner + "┤";
        }

        /// <summary>
        /// 字符条后面那个量（<c>留 +15.5%</c> / <c>裁 −43.8%</c> / <c>正好</c>）——**百分比手拼**（<c>P</c> 格式会再乘 100，见 <see cref="Percent1"/>）。
        /// </summary>
        public static string BarLabel(in TierFit fit, Axis axis)
        {
            AxisFit value = axis == Axis.Horizontal ? fit.Horizontal : fit.Vertical;
            if (!IsUsable(fit.Horizontal.Reference) || !IsUsable(fit.Vertical.Reference)) return "—";
            if (!fit.HasCanvas) return "（无画布尺寸）";
            if (value.IsCropped) return "裁 −" + Percent1(value.CropRatio);
            if (value.IsSpare) return "留 +" + Percent1(value.SpareRatio);
            return "正好";
        }

        /// <summary>该档某轴的"最需要注意的程度"（裁切优先、其次留白）——字符条排序用，**只按量、不合成**。</summary>
        public static float BarSeverity(in TierFit fit, Axis axis)
        {
            AxisFit value = axis == Axis.Horizontal ? fit.Horizontal : fit.Vertical;
            return value.IsCropped ? value.CropRatio : -value.SpareRatio;   // 负数只为让"留白"排在"裁切"之后
        }

        private static int ClampCells(int value, int width)
            => value < 1 ? 1 : (value > width ? width : value);
    }
}
