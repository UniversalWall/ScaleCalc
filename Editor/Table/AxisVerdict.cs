using System.Globalization;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// 溢出/留白判定：**只跟"画布尺寸 vs 参考分辨率"比，不碰具体布局**（使用口径不属本作品的观测面）。
    /// <para>逐轴独立判定：横向与纵向可以一个"留白"一个"裁切"。</para>
    /// <para>🔴 **选型台的两列已改用 <see cref="ScaleFit.Cell"/>**（量值 + **单侧**像素 + 容差）；本类的
    /// <see cref="Describe"/> 现在只服务**证据包 CSV** 与 <see cref="ScaleTableRow.Direction"/>，
    /// **不在界面上出现**——但**必须保留**：<see cref="EvidencePackCommand"/> 的 CSV 章节在用，
    /// 删它就会改变那份导出的逐字节内容。</para>
    /// </summary>
    public static class AxisVerdict
    {
        public static OverflowVerdict Of(float canvas, float reference)
            => canvas > reference ? OverflowVerdict.Spare
             : canvas < reference ? OverflowVerdict.Cropped
             : OverflowVerdict.Exactly;

        public static string Text(OverflowVerdict verdict)
        {
            switch (verdict)
            {
                case OverflowVerdict.Spare: return "留白";
                case OverflowVerdict.Cropped: return "裁切";
                default: return "正好";
            }
        }

        /// <summary>两轴合成的一句话（供表格"溢出/留白方向"列）。参考分辨率由调用方给——内核结果里不带它。</summary>
        public static string Describe(in ScaleCalcResult result, ScaleSize reference)
        {
            if (!result.HasCanvasSize) return "（无画布尺寸 ⇒ 无方向）";

            OverflowVerdict horizontal = Of(result.CanvasSize.Width, reference.Width);
            OverflowVerdict vertical = Of(result.CanvasSize.Height, reference.Height);
            string text = "横" + Text(horizontal) + " / 纵" + Text(vertical);
            if (horizontal == vertical && horizontal != OverflowVerdict.Exactly)
                text = Text(horizontal) + "（两轴一致）";
            if (horizontal == OverflowVerdict.Exactly && vertical == OverflowVerdict.Exactly)
                text = "正好（两轴一致）";
            return text;
        }

        public static string Format(float value) => value.ToString("F2", CultureInfo.InvariantCulture);

        /// <summary>
        /// **差值**的格式：4 位小数 —— **显示的位数不得低于判定容差**（容差 <c>1e-3</c>）。
        /// <para>原来差值也用 <c>F2</c>：<c>|Δ|</c> 落在 0.001~0.005 时**判定是"对账失败"、显示却是 <c>Δsf=0.00</c>**
        /// ⇒ 看表的人只会觉得"差 0 却在报警"（把容差当写回门限的另一种形态）。
        /// 证据包那条链路**不动**（它有自己的逐字节基线）。</para>
        /// </summary>
        public static string FormatDelta(float value) => value.ToString("F4", CultureInfo.InvariantCulture);
    }
}
