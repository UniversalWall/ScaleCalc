using System;
using System.Collections.Generic;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// <see cref="ScaleTableColumns"/> 的**宽度分摊章节**：
    /// 主文件管"**每列内容要多少宽**"（度量 + 夹取区间），本文件管"**把内容宽摊到可用宽**"。
    /// <para>🔴 **列集可传**（<paramref name="specs"/> 参数）：夹取要读每列的 <c>[min, max]</c>，而 <see cref="All"/>
    /// 只有 8 项——列集一变（例如打开"更多列"）按 <c>All[index]</c> 读会**当场越界**（现场探针抓到的真 bug）。
    /// <paramref name="specs"/> 传 <c>null</c> ⇒ 用默认列集（既有调用与断言零改动）。</para>
    /// </summary>
    public static partial class ScaleTableColumns
    {
        /// <summary>
        /// **把"内容宽度"分摊到"可用宽度"**（容器尺寸变化时调用）：先按内容比例整体缩放，再逐列夹到声明区间，
        /// 夹取省下的余量**再分给还有头寸的列**（等分，最多三轮）。
        /// <para>⚠️ 为什么不直接用 <c>Columns.stretchMode</c>：实测（编辑器 6000.3.15f1）——
        /// 对**显式设了像素宽度**的列，<c>Grow</c> 与 <c>GrowAndFill</c> **都不改它们**（把 7 列宽度全设成 <c>100</c>、
        /// 隔一帧再读仍是 <c>100</c>），剩余空间就白留在右侧；<c>Column.stretchable</c> 开关也拦不住这件事。
        /// 所以"按内容比例填满"这一层自己算：语义明确、可离线断言。</para>
        /// <para><paramref name="availableWidth"/> ≤ 0（尚未布局）⇒ 原样返回内容宽度。</para>
        /// </summary>
        public static float[] Distribute(IReadOnlyList<float> contentWidths, float availableWidth)
            => Distribute(contentWidths, availableWidth, null);

        /// <inheritdoc cref="Distribute(IReadOnlyList{float}, float)"/>
        public static float[] Distribute(IReadOnlyList<float> contentWidths, float availableWidth, Spec[] specs)
        {
            if (contentWidths == null) throw new ArgumentNullException(nameof(contentWidths));
            Spec[] set = specs ?? All;

            var result = new float[contentWidths.Count];
            float contentSum = 0f;
            for (int i = 0; i < contentWidths.Count; i++)
            {
                result[i] = Math.Max(0f, contentWidths[i]);
                contentSum += result[i];
            }
            if (availableWidth <= 0f || contentSum <= 0f || float.IsNaN(availableWidth)) return result;

            // ① 整体按比例缩放 + 夹取
            //    🔴 **`FixedWidth` 列不参与缩放**：图列的宽来自**固定作画区**，
            //    按内容比例缩会把 240 压成 150（现场实测踩到）⇒ 声明就用声明值（只受可用宽总和的自然约束）。
            float scale = availableWidth / contentSum;
            float used = 0f;
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = set[i].FixedWidth ? set[i].MaxWidth : Clamp(set, i, result[i] * scale);
                used += result[i];
            }

            // ② 夹取省下的余量分给**还有头寸**的列（等分；没人有头寸就到此为止 ⇒ 横向滚动）
            //    🔴 图列已有头寸也**不再加宽**（它是声明式固定宽，见上）
            for (int pass = 0; pass < 3 && used < availableWidth - 0.5f; pass++)
            {
                float remainder = availableWidth - used;
                int room = 0;
                for (int i = 0; i < result.Length; i++)
                    if (!set[i].FixedWidth && result[i] < MaxOf(set, i) - 0.5f) room++;
                if (room == 0) break;

                float share = remainder / room;
                for (int i = 0; i < result.Length; i++)
                {
                    if (set[i].FixedWidth || result[i] >= MaxOf(set, i) - 0.5f) continue;
                    float before = result[i];
                    result[i] = Math.Min(MaxOf(set, i), before + share);
                    used += result[i] - before;
                }
            }
            return result;
        }

        private static float Clamp(Spec[] set, int index, float value)
            => Math.Min(MaxOf(set, index), Math.Max(MinOf(set, index), value));

        private static float MinOf(Spec[] set, int index) => set[index].MinWidth;

        private static float MaxOf(Spec[] set, int index) => set[index].MaxWidth;
    }
}
