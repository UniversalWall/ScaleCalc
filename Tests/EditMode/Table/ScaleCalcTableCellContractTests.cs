using NUnit.Framework;
using Wayward.ScaleCalc.Editor;
using UnityEngine.UIElements;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// **单元格契约**的落点断言：每一列的 <c>makeCell()</c> → <c>bindCell()</c> → <c>unbindCell()</c>
    /// 都必须**真能跑通**，而且图格绑完之后**里面真有几何**。
    /// <para>🔴 **为什么必须有这条**：图格的绑定分支当时判的是 <c>element.childCount == 3</c>，而画法改版把
    /// 图格从 3 个子元素减到 2 ⇒ 分支永不命中，元素掉到普通格的 <c>((Label)element)</c> 强转上 ⇒ **每绑一格就抛一次
    /// <c>InvalidCastException</c>**；异常从 <c>VisualTreeLayoutUpdater</c> 一路冒出去，**整个布局回合被打断**
    /// （用户看到的表象是"图没画出来"，真因是"绑定根本没跑完"）。而当时**没有任何判据碰过 <c>bindCell</c>**
    /// （既有断言全是读源码字符串）⇒ 它一路绿着上线。**"内框停在 0×0"与"抛异常"是同一个根因**，不是两个 bug。</para>
    /// <para>本文件走**真调用**（与 <see cref="ScaleCalcTierUiTests"/> 同一手法）：那一课是——
    /// "源码里有这个分支"不等于"这一格绑得上"。</para>
    /// <para>⚠️ 判据读的是**元素自己声明的样式**（<c>style.width</c>），**不读 <c>worldBound</c>**：离屏元素没有几何
    /// （后台窗口里量不到渲染值，能断言的只有"我们写进去的那一份"）；"画出来到底什么样"仍是现场读数。</para>
    /// </summary>
    public sealed class ScaleCalcTableCellContractTests
    {
        private static readonly ScaleSize Reference = new ScaleSize(1920f, 1080f);

        private static ScaleTable BuildTable()
            => ScaleTable.Build(Reference, ScreenMatchMode.MatchWidthOrHeight, 0.5f, 144f,
                                ScaleMode.ScaleWithScreenSize, default, false, default);

        /// <summary>列下标按**表头**找（列序是定义单的事，判据不重抄下标）。</summary>
        private static int ColumnIndexOf(string title)
        {
            for (int i = 0; i < ScaleTableColumns.All.Length; i++)
                if (ScaleTableColumns.All[i].Title == title) return i;
            return -1;
        }

        /// <summary>
        /// 的正题：**逐列 × 逐行**都真的绑一次、清一次。只要有一条列的分派判据与元素形态脱钩，
        /// 这条就当场红——它是那次事故的**直接克星**（事故当天一条这样的判据都不存在）。
        /// </summary>
        [Test]
        public void EveryCellBindsAndUnbinds_WithoutThrowing()
        {
            ScaleTable table = BuildTable();
            MultiColumnListView list = ScaleCalcTableBinder.Build(table);

            Assert.That(table.Rows.Count, Is.GreaterThan(0), "空表的判据没有意义");
            Assert.That(list.columns.Count, Is.EqualTo(ScaleTableColumns.All.Length), "列集 = 列定义单");

            for (int c = 0; c < list.columns.Count; c++)
            {
                Column column = list.columns[c];
                for (int r = 0; r < table.Rows.Count; r++)
                {
                    int index = r;
                    VisualElement cell = column.makeCell();
                    Assert.DoesNotThrow(() => column.bindCell(cell, index),
                        "第 " + c + " 列「" + column.title + "」第 " + r + " 行**绑不上**（分派判据与元素形态脱钩）");
                    Assert.DoesNotThrow(() => column.unbindCell(cell, index),
                        "第 " + c + " 列「" + column.title + "」第 " + r + " 行**清不掉**（清格抛异常同样打断布局回合）");
                }
            }
        }

        /// <summary>越界索引（列表虚拟化会拿它清格）也必须**安全**：走清格、不抛。</summary>
        [Test]
        public void OutOfRangeIndex_ClearsInsteadOfThrowing()
        {
            ScaleTable table = BuildTable();
            MultiColumnListView list = ScaleCalcTableBinder.Build(table);

            foreach (int outOfRange in new[] { -1, table.Rows.Count, table.Rows.Count + 7 })
            {
                for (int c = 0; c < list.columns.Count; c++)
                {
                    int index = outOfRange;
                    Column column = list.columns[c];
                    VisualElement cell = column.makeCell();
                    Assert.DoesNotThrow(() => column.bindCell(cell, index),
                        "第 " + c + " 列：越界索引 " + index + " 要能安全清格");
                }
            }
        }

        /// <summary>
        /// 图格绑完之后**里面真有几何 + 悬停文本**，清格后复位。这条同时守住事故的另一半：
        /// 那次图**从来没绑上过**（所以"内框 0×0"不是另一个 bug，而是同一个根因的表象）。
        /// <para>🔴 画法改版后元素结构变了（宿主 → 作画区（信封）→ 四条带 + 两框）：
        /// 作画区是**像素**尺寸（按信封宽高比拟合），格内元素是**百分比**（相对作画区）。</para>
        /// </summary>
        [Test]
        public void DiagramCell_GetsGeometryAndTooltip_ThenResets()
        {
            ScaleTable table = BuildTable();
            MultiColumnListView list = ScaleCalcTableBinder.Build(table);
            int diagram = ColumnIndexOf(ScaleTableColumns.DiagramTitle);
            Assert.That(diagram, Is.GreaterThanOrEqualTo(0), "第 2 列「裁留图」必须在列定义单里");

            VisualElement cell = list.columns[diagram].makeCell();
            Assert.That(cell.childCount, Is.EqualTo(1), "宿主格只有一个子元素 = 作画区（信封）");
            VisualElement area = cell[ScaleCalcTableBinder.DiagramAreaIndex];
            Assert.That(area.childCount, Is.EqualTo(ScaleCalcTableBinder.DiagramAreaChildCount),
                "作画区内 = 四条差带 + 参考框 + 画布框（**顺序是契约**）");

            string tooltip = null;
            for (int r = 0; r < table.Rows.Count; r++)
            {
                list.columns[diagram].bindCell(cell, r);
                if (area[ScaleCalcTableBinder.DiagramCanvasIndex].style.width.value.value > 0f) tooltip = cell.tooltip;
            }

            Assert.That(tooltip, Is.Not.Null.And.Not.Empty, "至少有一档要算出画布 ⇒ 画布框宽度 > 0 且悬停文本已写上");
            Assert.That(tooltip, Does.Contain("本档画布"), "画布框 = 本档画布（不许用跨档图的「安全设计区」）");
            Assert.That(tooltip, Does.Contain(ScaleFit.DiagramLimiter), "限定语不能省");
            Assert.That(area.style.width.value.unit, Is.EqualTo(LengthUnit.Pixel),
                "作画区宽 = **像素**（按信封宽高比拟合 ⇒ 比例不被拉伸）");
            Assert.That(area.style.width.value.value, Is.GreaterThan(0f), "作画区要真的给了宽");
            Assert.That(area.style.height.value.value, Is.GreaterThan(0f), "作画区要真的给了高");
            Assert.That(area[ScaleCalcTableBinder.DiagramCanvasIndex].style.width.value.unit,
                Is.EqualTo(LengthUnit.Percent), "画布框按**百分比**摆在信封里");

            list.columns[diagram].unbindCell(cell, 0);
            Assert.That(string.IsNullOrEmpty(cell.tooltip), Is.True,
                "清格要撤悬停（清空后读回来是**空串**，不是 null）");
            Assert.That(area[ScaleCalcTableBinder.DiagramCanvasIndex].style.width.value.value,
                Is.EqualTo(0f).Within(0.001f), "元素会被复用 ⇒ 画布框必须复位（宽）");
            Assert.That(area[ScaleCalcTableBinder.DiagramCanvasIndex].style.height.value.value,
                Is.EqualTo(0f).Within(0.001f), "同上（高）");
        }

        /// <summary>
        /// 普通格与底衬格各自把内容写进**自己的**子元素：分派改动不许把文字一起吞掉，也不许把底衬
        /// 当普通格处理（同一族事故的另一个形态）。
        /// </summary>
        [Test]
        public void PlainAndBarCells_WriteIntoTheirOwnChildren()
        {
            ScaleTable table = BuildTable();
            MultiColumnListView list = ScaleCalcTableBinder.Build(table);

            int plain = ColumnIndexOf("屏幕档位");
            VisualElement plainCell = list.columns[plain].makeCell();
            list.columns[plain].bindCell(plainCell, 0);
            Assert.That(plainCell, Is.InstanceOf<Label>(), "普通格 = 一个 `Label`");
            Assert.That(((Label)plainCell).text, Is.EqualTo(ScaleTableColumns.All[plain].ValueOf(table.Rows[0])),
                "普通格要写上本列在该行上的取值（`Spec.ValueOf` 是唯一取值入口）");

            int bar = ColumnIndexOf(ScaleTableColumns.HorizontalTitle);
            VisualElement barCell = list.columns[bar].makeCell();
            list.columns[bar].bindCell(barCell, 0);
            Assert.That(barCell.childCount, Is.EqualTo(2), "底衬格 = 色块 + 文字");
            Assert.That(barCell[0], Is.Not.InstanceOf<Label>(), "第 0 个是色块");
            Assert.That(barCell[1], Is.InstanceOf<Label>(), "第 1 个是文字");
            Assert.That(((Label)barCell[1]).text, Is.EqualTo(ScaleTableColumns.All[bar].ValueOf(table.Rows[0])),
                "底衬格的文字要写上（分派改动不许把它落进普通格分支）");
        }
    }
}
