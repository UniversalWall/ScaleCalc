using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// 把 <see cref="ScaleTable"/> 绑到 UI Toolkit 的 <see cref="MultiColumnListView"/>。
    /// <para>**只做 bind/呈现**：所有数字都取自内核结果（窗口里没有算法，也没有 <c>if (canvas.isRootCanvas)</c> 这类判定）。</para>
    /// <para>列定义与宽度都来自 <see cref="ScaleTableColumns"/>（单一真相源）；这里只负责"按规格建列 +
    /// 在容器尺寸变化时重新分摊宽度 + **把行高铺开填满可用高度**"。</para>
    /// <para>🔴 **行高为什么要算**：条目只有 7 条而主视图有 400 多 px，
    /// 固定 20px 行高会在表格**内部**留一大片空白。本类改成"按可用高度摊行高"，并**夹在
    /// [<see cref="MinRowHeight"/>, <see cref="MaxRowHeight"/>]**：下限保住"一行装得下一个 Label"，
    /// 上限保住"窗口拉很高时行不会变成一条条巨带"（超出的余量留给下方，不再无脑摊大）。</para>
    /// <para><c>MultiColumnListView</c> **没有"按容器自动撑行"的开关**（实测枚举只有 <c>FixedHeight</c> / <c>DynamicHeight</c>，
    /// 没有 <c>None</c>）⇒ 只能自己算 <c>fixedItemHeight</c>。</para>
    /// <para>🔴 **<c>public</c> 而不是 <c>internal</c>**：单元格契约必须能被
    /// **真调用断言**（<c>Column.makeCell()</c> → <c>bindCell()</c>），只读源码字符串的判据拦不住"分派判据与元素形态脱钩"
    /// ——那种缺陷就是这么一路绿着上线的。同包先例 = <see cref="ScaleCalcTierBinder"/>（<c>public</c>，界面测试直接调）。</para>
    /// </summary>
    public static partial class ScaleCalcTableBinder
    {
        /// <summary>行高下限（转发 <see cref="ScaleTableRowHeight.Min"/>，常量只有一份）。</summary>
        public const float MinRowHeight = ScaleTableRowHeight.Min;

        /// <summary>行高上限（转发 <see cref="ScaleTableRowHeight.Max"/>）。</summary>
        public const float MaxRowHeight = ScaleTableRowHeight.Max;

        /// <summary>表头高度估算（转发 <see cref="ScaleTableRowHeight.HeaderEstimate"/>）。</summary>
        public const float HeaderHeightEstimate = ScaleTableRowHeight.HeaderEstimate;

        // 🔴 纯函数 `For` **不在这里**：本类对纯函数层不可见（测试程序集够不着，会 CS0122）。
        //    它住在 `ScaleTableRowHeight`（`public`）——常量与算法**都只有一份**。

        /// <summary>
        /// 按规格建装配整张表。
        /// <para>🔴 **排序与筛选只影响"看的顺序"**——<see cref="ScaleFit.ViewRows"/> 返回**副本**，
        /// <see cref="ScaleTable.Rows"/>（模型）一个元素都不动（模型仍是"清单顺序 + 现场行在首"）。</para>
        /// <para>🔴 **选中态按身份恢复**（<see cref="ViewOptions.SelectName"/>）：实测 <c>Rebuild()</c> 之后
        /// <c>selectedIndex = -1</c>（整张表是新建的），所以选中态只能自己按档位名回填。</para>
        /// </summary>
        public static MultiColumnListView Build(ScaleTable table, ViewOptions options = default)
        {
            List<ScaleTableRow> view = ScaleFit.ViewRows(table.Rows, options.SortKey, options.Ascending,
                                                         options.OnlyCropped, options.OnlyPortrait);
            var listView = new MultiColumnListView
            {
                itemsSource = view,
                fixedItemHeight = MinRowHeight,       // 初值；拿到几何后按可用高度重算（见下）
                selectionType = SelectionType.Single,
                name = "scale-table",
                // ⚠️ 不写 `sortingEnabled`（Unity 6 已标 obsolete：它由 `sortingMode` 取代）
                sortingMode = ColumnSortingMode.Custom,
            };
            listView.style.flexGrow = 1f;
            if (options.OnSortChanged != null)
                listView.columnSortingChanged += () => NotifySortChanged(listView, options, options.OnSortChanged);
            if (options.OnSelectionChanged != null)
                listView.selectionChanged += _ => options.OnSelectionChanged(listView.selectedItem as ScaleTableRow);

            // 🔴 列集 = **列定义单**（唯一真相源）。
            //    每次 Rebuild 都重建整张表 ⇒ 重新分摊天然发生（实测：只把 `column.visible` 设 false
            //    **不会**触发重新分摊，所以这里刻意走"重建"这条路）。
            ScaleTableColumns.Spec[] specs = ScaleTableColumns.All;
            float[] content = ScaleTableColumns.ComputeWidths(view, ScaleTableColumns.MeasureWithTextElement, specs);
            for (int i = 0; i < specs.Length; i++)
                listView.columns.Add(ColumnOf(specs[i], content[i], view, listView));

            RestoreSelection(listView, view, options.SelectName);

            // 容器变宽/变窄时**由我们自己**按内容比例重新分摊（不用 `Columns.stretchMode`：见 ScaleTableColumns.Distribute 的实测注）
            bool applying = false;
            listView.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                if (applying) return;                 // 改宽度/行高会再触发一次几何变化 ⇒ 防重入
                applying = true;
                try
                {
                    ApplyWidths(listView, content, evt.newRect.width);
                    ApplyRowHeight(listView, view.Count, evt.newRect.height);
                }
                finally { applying = false; }
            });

            return listView;
        }

        /// <summary>
        /// 把行高铺开到可用高度（夹在上下限之间；值没变就不写，避免几何回调自激）。
        /// <para>🔴 **改完 <c>fixedItemHeight</c> 必须 <c>Rebuild()</c>**（现场实测「点最后一行、高亮落到倒数第三行」抓出来的）：
        /// 只写这个属性**不会重排已经建出来的行元素**——实测固定行高写 <c>26</c>、<c>contentContainer</c> 也按 <c>9 × 26 = 234</c> 记，
        /// 而九个行元素**仍停在创建当刻的 <c>20px</c>**（<c>Build()</c> 里的初值 <see cref="MinRowHeight"/>）⇒ 列表"y ⇒ 行号"的折算用 <c>26</c>、
        /// 屏幕像素却是 <c>20</c>，越靠底部错得越多：点第 8 行（视觉中心 y≈541）被算成 <c>floor((541 − 371.3) / 26) = 6</c>。
        /// <c>Rebuild()</c> = "清掉视图、按当前行高重建可见元素、重新绑定" ⇒ 声明值与渲染值重新一致。</para>
        /// <para>它同时**顺手治好了"行高铺开"这个功能本身**：此前只改了声明值，界面上的行**从来没长高过**（"太空了"其实没修好）。</para>
        /// <para>不会自激：重建后再次进入本方法时目标行高不变，上面的 0.5px 判据直接返回。</para>
        /// </summary>
        private static void ApplyRowHeight(MultiColumnListView listView, int itemCount, float availableHeight)
        {
            float target = ScaleTableRowHeight.For(itemCount, availableHeight);
            if (Math.Abs(listView.fixedItemHeight - target) < 0.5f) return;
            listView.fixedItemHeight = target;
            listView.Rebuild();
        }

        private static void ApplyWidths(MultiColumnListView listView, float[] content, float availableWidth)
        {
            float[] widths = ScaleTableColumns.Distribute(content, availableWidth);
            int count = Math.Min(listView.columns.Count, widths.Length);
            for (int i = 0; i < count; i++)
            {
                if (Math.Abs(listView.columns[i].width.value - widths[i]) < 0.5f) continue;
                listView.columns[i].width = widths[i];
            }
        }

        /// <summary>
        /// 按规格造一列（含单元格形态）。
        /// <para>⚠️ 单元格细节（含**比例底衬**）在 <c>ScaleCalcTableBinder.Cell.cs</c>（拆的判据 = 200 行红线）。</para>
        /// <para>🔴 <paramref name="list"/> 是**传进去给图格用的**：作画区的像素尺寸要按**当前行高**拟合，
        /// 而 <c>bindCell</c> 那一刻 <c>element.resolvedStyle.height</c> 是 **NaN** ⇒ 只能问列表的
        /// <c>fixedItemHeight</c>（行高变化时 <c>ApplyRowHeight</c> 会 <c>Rebuild()</c> ⇒ 行会重新绑，拿得到新值）。</para>
        /// </summary>
        private static Column ColumnOf(ScaleTableColumns.Spec spec, float width, List<ScaleTableRow> view,
                                       MultiColumnListView list)
        {
            return new Column
            {
                title = spec.Title,
                width = width,
                // 交给上面的分摊逻辑管，不让表头自己拉伸（实测：`stretchable` + `GrowAndFill` 仍会把剩余全给最后一列）
                stretchable = false,
                sortable = true,                      // 列头可点排序（方向与键见 `NotifySortChanged`）
                minWidth = spec.MinWidth,
                maxWidth = spec.MaxWidth,
                makeCell = () => spec.Kind switch
                {
                    ScaleTableColumns.ScaleCellKind.Bar     => MakeBarCell(),
                    ScaleTableColumns.ScaleCellKind.Diagram => MakeDiagramCell(),
                    _                                       => new Label(),
                },
                bindCell = (element, index) =>
                {
                    // ⚠️ 索引进的是**视图副本**（排序/筛选后的），不是模型列表——两者顺序可以不同
                    if (index < 0 || index >= view.Count) { ClearCell(element, spec.Kind); return; }
                    ScaleTableRow row = view[index];
                    string text = spec.ValueOf(row);

                    // 🔴 **分派判据 = 列的种类**（`spec.Kind`），**不是子元素个数**。
                    //    现场实录：图格的分支当时判的是 `element.childCount == 3`，而画法改版把图格
                    //    从 3 个子元素减到 2 ⇒ 分支永不命中，元素掉到下面的 `((Label)element)` 强转上 ⇒
                    //    **每绑一格就抛一次 `InvalidCastException`**（栈顶是 `VisualTreeLayoutUpdater`）⇒
                    //    整个布局回合被打断——看到的景象是"图没画出来"，真因是"绑定根本没跑完"。
                    //    个数是各形态的**内部实现**（形态一改它就跟不上）⇒ 判据只许用**声明**（列定义单）。
                    switch (spec.Kind)
                    {
                        case ScaleTableColumns.ScaleCellKind.Bar:
                            BindBarCell(element, row, spec, text);
                            return;

                        // 🔴 逐档裁留示意图格：几何全在 `ScaleFit.DiagramColumnOf`，
                        //    这里只做"× 作画区尺寸 + 摆元素 + 悬停"（不再调求值门）
                        case ScaleTableColumns.ScaleCellKind.Diagram:
                            BindDiagramCell(element, row, list);
                            return;

                        default:
                            // 普通格：**用 `is` 而不是强转**——契约万一再破裂，"少显示一格"远好过
                            // "抛异常打断整个布局回合"。同时"离线列"用灰字注明：真值只对当前渲染尺寸
                            // 那一行有值（不许假装整表对过账）
                            if (element is Label label)
                            {
                                label.text = text;
                                label.EnableInClassList("cell-offline", row.HasTruth == false);
                            }

                            // 🔴 对账状态列：补三态配色 + 悬停。
                            //    判据 = 列标题常量（列定义单是唯一真相源，与 `HorizontalTitle` 的用法同构）。
                            if (spec.Title == ScaleTableColumns.ReconcileTitle) BindReconcileCell(element, row);
                            return;
                    }
                },
                unbindCell = (element, _) => ClearCell(element, spec.Kind),
            };
        }
    }
}
