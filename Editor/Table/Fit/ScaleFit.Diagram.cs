namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// **安全区示意图的几何**：把"参考画布 / 安全设计区 / 危险带"三样东西算成**归一化比例**
    /// （外框恒 = 1×1），窗口只负责按像素缩放去摆元素 ⇒ 算法可在 EditMode 断言。
    /// <para>读法：**外框 = 参考画布**、**内框 = 安全设计区**（居中）、**四条边带 = 危险带**（参考相对安全区
    /// 被裁掉的那一圈）；边带**红 = 至少一档会裁、黄 = 全都不裁（只有留白）**——与结论条同源（同一个 <see cref="ModeStats"/>）。</para>
    /// <para>🔴 **它只画几何**：不承诺"某个具体 UI 不会被裁"；<c>HasSafeArea == false</c>（0 档 / 无画布）
    /// ⇒ 窗口只画外框并如实说明，**不编内框**。</para>
    /// </summary>
    public static partial class ScaleFit
    {
        /// <summary>示意图的归一化布局（外框 = 1×1；内框居中）。</summary>
        public readonly struct DiagramLayout
        {
            /// <summary>有安全设计区可画（= <see cref="ModeStats.HasSafeArea"/>）。</summary>
            public readonly bool HasSafeArea;

            /// <summary>内框宽 / 外框宽（=`安全区宽 / 参考宽`，夹在 (0, 1]）。</summary>
            public readonly float InnerWidthRatio;

            /// <summary>内框高 / 外框高。</summary>
            public readonly float InnerHeightRatio;

            /// <summary>至少一档会裁 ⇒ 边带用"裁"的颜色（窗口侧：红）；否则用"留"的颜色（黄）。</summary>
            public readonly bool Cropped;

            public DiagramLayout(bool hasSafeArea, float innerWidthRatio, float innerHeightRatio, bool cropped)
            {
                HasSafeArea = hasSafeArea;
                InnerWidthRatio = innerWidthRatio;
                InnerHeightRatio = innerHeightRatio;
                Cropped = cropped;
            }
        }

        /// <summary>按跨档统计算示意图布局。**只吃 <see cref="ModeStats"/>** ⇒ 与结论条/危险带同源。</summary>
        public static DiagramLayout DiagramOf(ModeStats stats, ScaleSize reference)
        {
            if (!stats.HasSafeArea || !IsUsable(reference))
                return new DiagramLayout(false, 1f, 1f, false);

            // Expand 下安全区可能**大于**参考（画布比参考大）⇒ 内框夹到外框以内：示意图画的是"设计要保护的那块"，
            // 它不会比参考更大（多出来的空间不是"要留的边"，而是留白本身）——窗口另有"留白"一处如实标注。
            float width = Clamp01(stats.SafeArea.Width / reference.Width);
            float height = Clamp01(stats.SafeArea.Height / reference.Height);
            return new DiagramLayout(true, width, height, stats.CroppedCount > 0);
        }

        private static float Clamp01(float value)
            => !IsUsable(value) ? 1f : (value > 1f ? 1f : value);
    }
}
