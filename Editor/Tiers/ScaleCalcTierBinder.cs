using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// 把 <see cref="ScreenTierSet"/> 的条目绑到**档位管理表**（<c>MultiColumnListView</c>）。
    /// <para>与选型表的 <see cref="ScaleCalcTableBinder"/> 分开：那张表**只读呈现**，这张表**带交互**
    /// （勾选框 · 就地编辑 · 删除按钮）。</para>
    /// <para>🔴 **两处刻意的结构性规避**（不靠"应该没事"）：
    /// ① <c>selectionType = None</c> ⇒ 管理表**没有行选中**这回事，"点按钮会不会同时切换行选中"**失去前提**；
    /// ② 变更回调一律注册在 <c>makeCell</c> 里一次、绑定时只 <c>SetValueWithoutNotify</c> ⇒
    /// 不依赖"<c>bindCell</c> 会被调用几次"，也不会在绑定期**误触发**一次真实变更（虚拟化回收时尤其致命）。</para>
    /// <para>⚠️ **不改任何数据**：本类只把用户动作转给 <see cref="ITierTableHost"/>。数据归属在宿主那里。</para>
    /// <para>🔴 **四个事件处理体是公开的**（<see cref="NotifyIncluded"/> / <see cref="NotifyDeleted"/> /
    /// <see cref="CommitSize"/> / <see cref="CommitName"/>）：理由（<c>SendEvent</c> 在**未接 panel** 的元素上根本不派发）
    /// 与用法写在 `ScaleCalcTierBinder.Handlers.cs` 的类注释里 —— 那一章就是为 200 行红线拆出去的。</para>
    /// </summary>
    public static partial class ScaleCalcTierBinder
    {
        /// <summary>
        /// 造一张管理表。<paramref name="tiers"/> 会**复制一份快照**：视图与数据不同步改（列表在改动中被换掉时更安全）。
        /// </summary>
        public static MultiColumnListView Build(ITierTableHost host, IReadOnlyList<ScreenTier> tiers)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            var snapshot = new List<ScreenTier>();
            if (tiers != null) foreach (ScreenTier tier in tiers) snapshot.Add(tier);

            var listView = new MultiColumnListView
            {
                itemsSource = snapshot,
                fixedItemHeight = ScreenTierColumns.RowHeight,
                selectionType = SelectionType.None,      // 规避 ①
                name = "tier-table",
            };
            listView.style.flexGrow = 1f;

            foreach (ScreenTierColumns.Spec spec in ScreenTierColumns.All)
                listView.columns.Add(ColumnOf(spec, snapshot, host));
            return listView;
        }

        private static Column ColumnOf(ScreenTierColumns.Spec spec, List<ScreenTier> snapshot, ITierTableHost host)
        {
            var column = new Column
            {
                title = spec.Title,
                width = spec.Width,
                minWidth = spec.MinWidth,
                maxWidth = spec.MaxWidth,
                stretchable = false,
            };

            switch (spec.Kind)
            {
                case ScreenTierColumns.CellKind.Include:
                    column.makeCell = () => IncludeCell(host);
                    column.bindCell = (element, index) => BindInclude((Toggle)element, snapshot, index);
                    break;

                case ScreenTierColumns.CellKind.Name:
                    column.makeCell = () => NameCell(host);
                    column.bindCell = (element, index) => BindName((TextField)element, snapshot, host, index);
                    break;

                case ScreenTierColumns.CellKind.Size:
                    column.makeCell = () => SizeCell(host);
                    column.bindCell = (element, index) => BindSize((TextField)element, snapshot, host, index);
                    break;

                case ScreenTierColumns.CellKind.Actions:
                    column.makeCell = () => DeleteCell(host);
                    column.bindCell = (element, index) => ((Button)element).userData = IdAt(snapshot, index);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(spec), spec.Kind, "没见过的单元格形态");
            }
            return column;
        }

        /// <summary>规避 ②：变更回调只在**造单元格**时注册一次；绑定期只写值、不发通知。</summary>
        private static Toggle IncludeCell(ITierTableHost host)
        {
            var toggle = new Toggle();
            toggle.RegisterValueChangedCallback(evt => NotifyIncluded(toggle, evt.newValue, host));
            return toggle;
        }

        // 🔴 四个公开处理体（NotifyIncluded / NotifyDeleted / CommitSize / CommitName）**不在这里**：
        //    它们住在 `ScaleCalcTierBinder.Handlers.cs`（拆的判据 = 200 行红线），本文件只管装配与绑定。

        private static void BindInclude(Toggle toggle, List<ScreenTier> snapshot, int index)
        {
            int at = Clamp(index, snapshot);
            toggle.userData = at < 0 ? null : snapshot[at].Id;
            toggle.SetValueWithoutNotify(at >= 0 && snapshot[at].Included);
        }

        /// <summary><c>isDelayed = true</c> ⇒ **回车 / 失焦才提交**（同工具栏输入框的口径）。</summary>
        private static TextField SizeCell(ITierTableHost host)
        {
            var field = new TextField { isDelayed = true };
            field.RegisterValueChangedCallback(evt => CommitSize(field, evt.newValue, host));
            return field;
        }

        private static void BindSize(TextField field, List<ScreenTier> snapshot, ITierTableHost host, int index)
        {
            int at = Clamp(index, snapshot);
            field.userData = at < 0 ? null : snapshot[at].Id;
            field.SetValueWithoutNotify(at < 0 ? string.Empty : host.TierSizeText(field.userData as string));
        }

        /// <summary><c>isDelayed = true</c> ⇒ **回车 / 失焦才提交**（与尺寸列同口径）。</summary>
        private static TextField NameCell(ITierTableHost host)
        {
            var field = new TextField { isDelayed = true };
            field.RegisterValueChangedCallback(evt => CommitName(field, evt.newValue, host));
            return field;
        }

        private static void BindName(TextField field, List<ScreenTier> snapshot, ITierTableHost host, int index)
        {
            int at = Clamp(index, snapshot);
            field.userData = at < 0 ? null : snapshot[at].Id;
            field.SetValueWithoutNotify(at < 0 ? string.Empty : host.TierNameText(field.userData as string));
        }

        /// <summary>
        /// 删除单元格：**内置档也可点**（禁用按钮不触发悬停提示，而这里要的正是"为什么点不动"）。
        /// <para><c>StopPropagation</c> 是规避 ① 的第二道保险（真选了行也不让事件继续冒泡）。</para>
        /// </summary>
        private static Button DeleteCell(ITierTableHost host)
        {
            var button = new Button { text = "删除" };
            button.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                NotifyDeleted(button, host);
            });
            return button;
        }

        private static string IdAt(List<ScreenTier> snapshot, int index)
        {
            int at = Clamp(index, snapshot);
            return at < 0 ? null : snapshot[at].Id;
        }

        /// <summary>越界一律给 <c>-1</c>（虚拟化在列表换掉的那一瞬会拿旧下标来问，不许因此抛）。</summary>
        private static int Clamp(int index, List<ScreenTier> snapshot)
            => index < 0 || index >= snapshot.Count ? -1 : index;
    }
}
