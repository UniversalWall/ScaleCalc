using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// <see cref="ScaleCalcTableBinder"/> 的**视图选项与选中/排序回调章节**。
    /// <para>拆出去的东西都是"**视图要怎么装配**"的输入与回调（不是"怎么造列/怎么算宽高"），
    /// 主文件因此回到 150 行上下。</para>
    /// </summary>
    public static partial class ScaleCalcTableBinder
    {
        /// <summary>
        /// **视图选项**：排序 · 筛选 · 重建后要恢复的选中项 · 回调。
        /// <para>🔴 收成一个结构是因为参数已经多到"按位置传必然出错"（一串位置参数 + 默认值 = 静默漂移）。</para>
        /// </summary>
        public readonly struct ViewOptions
        {
            public readonly ScaleFit.SortKey SortKey;
            public readonly bool Ascending;
            public readonly bool OnlyCropped;
            public readonly bool OnlyPortrait;

            /// <summary>重建后要**按身份**（档位名，不是行号）恢复选中的那一行——<c>Rebuild()</c> 会丢选中态。</summary>
            public readonly string SelectName;

            public readonly Action<ScaleFit.SortKey, bool> OnSortChanged;

            /// <summary>选中行变化（<c>null</c> = 没有选中）。</summary>
            public readonly Action<ScaleTableRow> OnSelectionChanged;

            public ViewOptions(ScaleFit.SortKey sortKey = ScaleFit.SortKey.None, bool ascending = true,
                               bool onlyCropped = false, bool onlyPortrait = false, string selectName = null,
                               Action<ScaleFit.SortKey, bool> onSortChanged = null,
                               Action<ScaleTableRow> onSelectionChanged = null)
            {
                SortKey = sortKey;
                Ascending = ascending;
                OnlyCropped = onlyCropped;
                OnlyPortrait = onlyPortrait;
                SelectName = selectName;
                OnSortChanged = onSortChanged;
                OnSelectionChanged = onSelectionChanged;
            }
        }

        /// <summary>按**档位名**把选中态放回去（同名的行若已不在视图里——比如被筛掉——就保持未选中）。</summary>
        private static void RestoreSelection(MultiColumnListView listView, List<ScaleTableRow> view, string selectName)
        {
            if (string.IsNullOrEmpty(selectName)) return;
            for (int i = 0; i < view.Count; i++)
            {
                if (view[i].Profile.Name != selectName) continue;
                listView.selectedIndex = i;
                return;
            }
        }

        /// <summary>
        /// 表头点排序 ⇒ 把"列下标 → 排序键"翻给窗口（**列序固定**，映射在 <see cref="ScaleFit.SortKeyOfColumn"/>）。
        /// <para>方向规则：切到**新列**时用该列的默认方向（裁切量列降序，其余升序）；**再点同一列**则反向
        /// ——与常见表格一致，且"最坏在前"才是选型要看的顺序。</para>
        /// </summary>
        private static void NotifySortChanged(MultiColumnListView listView, ViewOptions options,
                                              Action<ScaleFit.SortKey, bool> onSortChanged)
        {
            // `sortedColumns` 是 `IEnumerable<SortColumnDescription>`（实测）：只取第一个（单列排序）
            int index = -1;
            foreach (SortColumnDescription description in listView.sortedColumns)
            {
                index = description.columnIndex;
                break;
            }

            ScaleFit.SortKey key = ScaleFit.SortKeyOfColumn(index);
            if (key == ScaleFit.SortKey.None) return;
            bool ascending = key == options.SortKey ? !options.Ascending : ScaleFit.DefaultAscending(key);
            onSortChanged(key, ascending);
        }
    }
}
