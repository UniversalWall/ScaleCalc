using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// <see cref="ScaleCalcTableBinder"/> 的**单元格形态章节**：
    /// 普通格（一个 <c>Label</c>）· **带比例底衬的格**（`横向`/`纵向`）· **逐档裁留示意图格**（`裁留图`）。
    /// <para>🔴 底衬是**绝对定位的色块叠在文字下面**，不是"文字 + 条"并排：行高只有 20~30px、列宽只有 130~240px，
    /// 并排必然挤掉数值；底衬**不占额外空间**，数值照旧逐字可读（已实测行高仍由 <c>fixedItemHeight</c> 控制）。</para>
    /// <para>颜色 = 裁（红）/ 留（黄）/ 正好（绿），与文字前缀 **双编码**（颜色不作唯一编码）。</para>
    /// <para>🔴 **判据 = 列的种类**（<see cref="ScaleTableColumns.ScaleCellKind"/>）：<c>bindCell</c> 与 <c>ClearCell</c> 都按它分派。
    /// **不许再按"子元素个数"分派**：个数是各形态的**内部实现**——画法改版把图格从 3 个子元素减到 2，
    /// 而那两处判据没跟着改 ⇒ 图格的绑定分支永不命中、掉到 <c>((Label)element)</c> 强转上抛
    /// <c>InvalidCastException</c>，**整个布局回合被打断**（表象是"图不见了"）。</para>
    /// <para>当前形态：普通格 = 一个 <c>Label</c> · 底衬格 = 色块 + 文字（**2** 个）·
    /// 示意图格 = 作画区 + 内框（**2** 个，由 `Diagram.cs` 造）。</para>
    /// <para>🔴 **图的读写都在 `ScaleCalcTableBinder.Diagram.cs`**（本文件当时 251 行 &gt; 200 红线，
    /// 而"底衬 / 对账 / 清格"与"画图"本就是两件事）。</para>
    /// </summary>
    public static partial class ScaleCalcTableBinder
    {
        /// <summary>造一个带比例底衬的单元格：`[0]` = 色块、`[1]` = 文字（顺序是契约，两处都按它取）。</summary>
        private static VisualElement MakeBarCell()
        {
            var host = new VisualElement { name = "cell-bar-host" };
            host.style.position = Position.Relative;
            host.style.flexGrow = 1f;

            var bar = new VisualElement { name = "cell-bar" };
            bar.style.position = Position.Absolute;
            bar.style.left = 0f;
            bar.style.top = 0f;
            bar.style.height = Length.Percent(100f);
            host.Add(bar);

            var label = new Label { name = "cell-bar-label" };
            label.style.flexGrow = 1f;
            host.Add(label);
            return host;
        }

        /// <summary>
        /// 绑一格：底衬宽度 = 画布 ÷ 参考（**夹到 [0,1]**——留白侧也满格，多出来的量由文字里的 `留 +x%` 说清）。
        /// <para>轴向取自**列标题**（`横向`/`纵向`）——列定义单是唯一真相源，这里不另立一套标识。</para>
        /// </summary>
        private static void BindBarCell(VisualElement cell, ScaleTableRow row, ScaleTableColumns.Spec spec, string text)
        {
            var bar = (VisualElement)cell[0];
            var label = (Label)cell[1];

            ScaleFit.TierFit fit = ScaleFit.Of(in row.ScreenSize, row.Reference);
            ScaleFit.Axis axis = spec.Title == ScaleTableColumns.HorizontalTitle
                ? ScaleFit.Axis.Horizontal : ScaleFit.Axis.Vertical;
            float canvas = axis == ScaleFit.Axis.Horizontal ? fit.Canvas.Width : fit.Canvas.Height;
            float reference = axis == ScaleFit.Axis.Horizontal ? row.Reference.Width : row.Reference.Height;

            float ratio = ScaleFit.IsUsable(reference) && ScaleFit.IsUsable(canvas) ? canvas / reference : 0f;
            bar.style.width = Length.Percent(Math.Min(1f, Math.Max(0f, ratio)) * 100f);

            ScaleFit.AxisFit value = axis == ScaleFit.Axis.Horizontal ? fit.Horizontal : fit.Vertical;
            bar.EnableInClassList("cell-bar-crop", value.IsCropped);
            bar.EnableInClassList("cell-bar-spare", value.IsSpare);
            bar.EnableInClassList("cell-bar-exact", !value.IsCropped && !value.IsSpare);

            label.text = text;
            label.EnableInClassList("cell-offline", row.HasTruth == false);
        }


        /// <summary>
        /// 绑**对账状态列**的一格：文字已由 <see cref="ScaleTableColumns.Spec.ValueOf"/> 写好，
        /// 这里只补**三态配色**与**悬停文本**——🔴 **绝不重算判定**（状态由 <see cref="ReconcileVerdict.StateOf"/>
        /// 唯一给出，不拿显示文案去反推）。
        /// <para>悬停走 <c>VisualElement.tooltip</c>（不新增控件、不挂自定义事件、不做浮层）。</para>
        /// </summary>
        private static void BindReconcileCell(VisualElement element, ScaleTableRow row)
        {
            ReconcileVerdict.State state = ReconcileVerdict.StateOf(row);
            element.EnableInClassList("cell-verdict-ok", state == ReconcileVerdict.State.Pass);
            element.EnableInClassList("cell-mismatch", state == ReconcileVerdict.State.Fail);
            element.EnableInClassList("cell-offline", state == ReconcileVerdict.State.NoTruth);
            element.tooltip = ReconcileVerdict.Tooltip(row);
        }

        /// <summary>
        /// 清格：三种形态都要清（<c>Label</c> 清文本；底衬格复位色块；示意图格复位内框与悬停缓存）。
        /// <para>🔴 **按列的种类分派**（<paramref name="kind"/> 由调用方从列定义单取），**不按子元素个数**：
        /// 个数是各形态的内部实现——图格从 3 个子元素减到 2 时，<c>bindCell</c> 与
        /// **本方法**的判据都停在旧个数上 ⇒ 图格既绑不上、也清不掉。传 <paramref name="kind"/> 进来就**不可能失配**。</para>
        /// </summary>
        private static void ClearCell(VisualElement element, ScaleTableColumns.ScaleCellKind kind)
        {
            // 🔴 元素会被列表**复用**（虚拟化）⇒ 悬停与所有状态类名必须一起复位，
            //    否则上一行的"失败原因"或"上一档的框"会跟着元素跑到下一行。
            element.tooltip = null;
            element.EnableInClassList("cell-verdict-ok", false);

            switch (kind)
            {
                // 示意图格：整格复位交给 `ResetDiagramCell`（图的读写都在
                // `ScaleCalcTableBinder.Diagram.cs`，本文件只负责"什么时候清"）
                case ScaleTableColumns.ScaleCellKind.Diagram:
                    ResetDiagramCell(element);
                    return;

                // 底衬格：色块收成 0 宽 + 文字清空（**清格路径不许抛** ⇒ 取子元素前先判个数）
                case ScaleTableColumns.ScaleCellKind.Bar:
                    if (element.childCount > 0) element[0].style.width = Length.Percent(0f);
                    if (element.childCount > 1 && element[1] is Label barLabel) barLabel.text = string.Empty;
                    return;

                default:
                    if (element is Label plain) plain.text = string.Empty;
                    return;
            }
        }
    }
}
