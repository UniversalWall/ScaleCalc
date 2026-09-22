using UnityEngine;
using UnityEngine.UIElements;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// <see cref="ScaleCalcTableBinder"/> 的**逐档示意图：绑定与摆位**章节。
    /// <para>几何**全部**来自 <see cref="ScaleFit.DiagramColumnOf"/>（纯函数），本文件只做
    /// "按信封宽高比定作画区 + 摆元素 + 悬停"。</para>
    /// <para>🔴 **比例绝不被拉伸**：作画区的像素宽高按 <see cref="ScaleFit.DiagramColumnLayout.EnvelopeAspect"/>
    /// 拟合（行高是硬上限、宽度另有上限），两框与四条带在作画区内按**百分比**摆放 ⇒ 形状始终是真实的
    /// "参考 vs 本档画布"。</para>
    /// <para>🔴 **只有一处几何来源**：本文件**不许**再调求值门（<see cref="ScaleFit.TierFit"/> 只由
    /// <c>BindDiagramCell</c> 造一次）。</para>
    /// </summary>
    public static partial class ScaleCalcTableBinder
    {
        /// <summary>
        /// 图格的**布局缓存**（键 = 图格元素）。
        /// <para>🔴 **为什么不用 <c>VisualElement.userData</c>**：<c>ClearCell</c>（由 <c>unbindCell</c> 调用）会在
        /// **可见行**上把 <c>userData</c> 清空 ⇒ 绑定时写进去的悬停文本，到用户真正悬停时已经没了。
        /// ⇒ 换成**表外缓存**（弱键，元素被回收即自动清理），与 <c>userData</c> 完全解耦。</para>
        /// </summary>
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<VisualElement,
            ScaleFit.DiagramColumnLayoutBox> s_Layouts =
            new System.Runtime.CompilerServices.ConditionalWeakTable<VisualElement, ScaleFit.DiagramColumnLayoutBox>();

        /// <summary>行高：取列表**当前**的 <c>fixedItemHeight</c>（还没算出来时用上限兜底，**不许拿 0/NaN 去算**）。</summary>
        private static float RowHeightOf(MultiColumnListView list)
        {
            float height = list == null ? 0f : list.fixedItemHeight;
            return height > 1f ? height : ScaleTableRowHeight.Max;
        }

        /// <summary>
        /// 绑一格示意图：几何**全部**来自 <see cref="ScaleFit.DiagramColumnOf"/>，这里只做
        /// "按信封宽高比定作画区 + 摆元素 + 悬停 + 配色"。
        /// <para>🔴 **只有一处几何来源**：本方法里 <see cref="ScaleFit.Of"/> 只调一次
        /// （<see cref="ScaleFit.TierFit"/> 由它造出来，后面全部读这一份）。</para>
        /// </summary>
        private static void BindDiagramCell(VisualElement cell, ScaleTableRow row, MultiColumnListView list)
        {
            ScaleFit.TierFit fit = ScaleFit.Of(in row.ScreenSize, row.Reference);   // 唯一一次几何求值
            ScaleFit.DiagramColumnLayout layout = ScaleFit.DiagramColumnOf(fit);
            s_Layouts.Remove(cell);            // 同一元素复用 ⇒ 先撇掉上一行的缓存
            s_Layouts.Add(cell, new ScaleFit.DiagramColumnLayoutBox(layout));

            cell.tooltip = layout.Tooltip;
            LayoutDiagram(cell, in layout, RowHeightOf(list));
        }

        /// <summary>
        /// **摆图**：① 作画区（信封）的像素尺寸 = 按 <see cref="ScaleFit.DiagramColumnLayout.EnvelopeAspect"/>
        /// 在"行高 − 呼吸位"里拟合（**不许拉伸比例**）；② 两框与四条带全部写**百分比**（相对作画区）。
        /// <para>🔴 **两条硬约束**（都有实测代价）：① 百分比是按**父节点**解析的 ⇒ 子元素必须挂在**作画区**下
        /// （挂到宿主上就会按整格解析 ⇒ 图比格还大、出血到邻行）；② 作画区高度**必须**跟着行高走
        /// （行高在 20~30 之间摊），写死 22 会在 20 高的行上出血。</para>
        /// </summary>
        private static void LayoutDiagram(VisualElement cell, in ScaleFit.DiagramColumnLayout layout, float rowHeight)
        {
            if (cell.childCount <= DiagramAreaIndex || !(cell[DiagramAreaIndex] is VisualElement area)) return;

            float maxHeight = Mathf.Max(MinDiagramHeight, rowHeight - 2f * DiagramVerticalMargin);
            float aspect = layout.EnvelopeAspect;
            if (float.IsNaN(aspect) || float.IsInfinity(aspect) || aspect <= 0f) aspect = 1f;

            float width, height;
            if (aspect >= DiagramAreaMaxWidth / maxHeight)      // 宽的那一维先顶到上限
            {
                width = DiagramAreaMaxWidth;
                height = DiagramAreaMaxWidth / aspect;
            }
            else
            {
                height = maxHeight;
                width = maxHeight * aspect;
            }
            area.style.width = width;
            area.style.height = height;

            if (area.childCount < DiagramAreaChildCount) return;
            // 🔴 **差带的"至少 1px"下限**（可读性优化）：差 0.6% 时按比例算出来是 **0.1px**
            //    ⇒ 界面上等于没有。细的那一维（左右带看宽、上下带看高）给 **1px 的地板**，
            //    另一维不动 ⇒ "极小差异也看得见"，而**真实的量仍然在悬停里逐字给出**（几何不撒谎：
            //    地板只影响像素，不改纯函数的数）。1px 换算成百分比要除以作画区的像素尺寸。
            float onePixelWidth = width > 0f ? 100f / width : 0f;
            float onePixelHeight = height > 0f ? 100f / height : 0f;

            SetBand((VisualElement)area[DiagramBandLeftIndex], in layout.Left, onePixelWidth, 0f);
            SetBand((VisualElement)area[DiagramBandRightIndex], in layout.Right, onePixelWidth, 0f);
            SetBand((VisualElement)area[DiagramBandTopIndex], in layout.Top, 0f, onePixelHeight);
            SetBand((VisualElement)area[DiagramBandBottomIndex], in layout.Bottom, 0f, onePixelHeight);
            SetRect((VisualElement)area[DiagramRefIndex], in layout.Reference, 0f, 0f);
            // 无画布 ⇒ 画布框收成 0（**不编内框**；参考框仍如实画出来）
            ScaleFit.DiagramRect canvas = layout.HasCanvas ? layout.Canvas : default;
            SetRect((VisualElement)area[DiagramCanvasIndex], in canvas, 0f, 0f);
        }

        private static void SetRect(VisualElement element, in ScaleFit.DiagramRect rect,
                                    float minWidthPercent, float minHeightPercent)
        {
            element.style.left = Length.Percent(rect.Left * 100f);
            element.style.top = Length.Percent(rect.Top * 100f);
            element.style.width = Length.Percent(Mathf.Max(rect.Width * 100f, minWidthPercent));
            element.style.height = Length.Percent(Mathf.Max(rect.Height * 100f, minHeightPercent));
        }

        private static void SetBand(VisualElement element, in ScaleFit.DiagramBand band,
                                    float minWidthPercent, float minHeightPercent)
        {
            if (!band.Present)
            {
                ZeroRect(element);
                element.EnableInClassList("cell-diagram-crop", false);
                element.EnableInClassList("cell-diagram-spare", false);
                return;
            }
            SetRect(element, in band.Rect, minWidthPercent, minHeightPercent);
            element.EnableInClassList("cell-diagram-crop", band.Crop);
            element.EnableInClassList("cell-diagram-spare", !band.Crop);
        }

        private static void ZeroRect(VisualElement element)
        {
            element.style.left = Length.Percent(0f);
            element.style.top = Length.Percent(0f);
            element.style.width = Length.Percent(0f);
            element.style.height = Length.Percent(0f);
        }

        /// <summary>
        /// 复位一个示意图格（<c>ClearCell</c> 调用）：元素会被列表**复用**（虚拟化）⇒ 不清的话上一档的图
        /// 会跟到下一行。**清三样**：缓存条目 · 六个子元素的矩形 · 两条带来的配色类。
        /// </summary>
        private static void ResetDiagramCell(VisualElement cell)
        {
            s_Layouts.Remove(cell);
            // 🔴 **清格路径不许抛**（清理抛异常同样会打断布局回合）⇒ 取子元素前先判形态
            if (cell.childCount <= DiagramAreaIndex || !(cell[DiagramAreaIndex] is VisualElement area)) return;
            for (int i = 0; i < area.childCount; i++)
            {
                VisualElement child = area[i];
                ZeroRect(child);
                child.EnableInClassList("cell-diagram-crop", false);
                child.EnableInClassList("cell-diagram-spare", false);
            }
        }
    }
}
