using System.Globalization;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>单轴的溢出/留白方向（**几何口径**）。</summary>
    public enum OverflowVerdict
    {
        /// <summary>画布比参考小 ⇒ 贴边元素会被挤出屏。</summary>
        Cropped,

        /// <summary>画布与参考正好相等。</summary>
        Exactly,

        /// <summary>画布比参考大 ⇒ 参考设计之外还有余量。</summary>
        Spare,
    }
}
