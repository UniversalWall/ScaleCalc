using NUnit.Framework;
using Wayward.ScaleCalc.Editor;
using UnityEngine.UIElements;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// **档位管理表**。
    /// <para>🔴 **为什么这不是"只读文件"的假断言**：本文件走 <c>Column.makeCell()</c> **真的把单元格造出来**、
    /// 再 <c>bindCell</c> 绑上数据，断言"里面真有勾选框 / 输入框 / 删除按钮"，并直调**处理体**验证它把动作转给了正确的条目。
    /// 只 <c>File.ReadAllText</c> 找字符串拦不住"界面根本建不起来"。</para>
    /// <para>⚠️ 直调处理体而不是 <c>toggle.value = true</c>：实测 <b><c>SendEvent</c> 在未接 panel 的元素上不派发</b>
    /// ⇒ 裸造的单元格"设值看回调"是测不出来的（<c>RegisterValueChangedCallback</c> 那一行由现场人工验）。
    /// 工作集 ⇒ **选型表**那半边在 <see cref="ScaleCalcTierSelectionTests"/>。</para>
    /// </summary>
    public sealed class ScaleCalcTierUiTests
    {
        // 假宿主在 `Tests/EditMode/Support/TierFakeHost.cs`（抽出去：改名测试也要用它——
        // 同一份假宿主放两份必然漂移，一边加了接口成员另一边就编译不过或漏断）。

        private static VisualElement Cell(MultiColumnListView list, ScreenTierColumns.CellKind kind, int row)
        {
            Column column = list.columns[ScreenTierColumns.IndexOf(kind)];
            VisualElement element = column.makeCell();     // ← 真造一个单元格
            column.bindCell(element, row);
            return element;
        }

        /// <summary>`K1`：**每一行**的四个单元格里都是真控件——勾选框 / 名称 / 尺寸输入框 / 删除按钮。</summary>
        [Test]
        public void K1_EveryRowCell_HoldsTheRealControl()
        {
            ScreenTierSet set = ScreenTierSet.Default();
            MultiColumnListView list = ScaleCalcTierBinder.Build(new TierFakeHost(set), set.Tiers);

            Assert.That(list.itemsSource.Count, Is.EqualTo(set.Tiers.Count), "管理表渲染**全部**条目");

            for (int row = 0; row < list.itemsSource.Count; row++)
            {
                Assert.That(Cell(list, ScreenTierColumns.CellKind.Include, row), Is.InstanceOf<Toggle>(),
                    "第 " + row + " 行的『包含』列必须是真勾选框（K1）");
                Assert.That(Cell(list, ScreenTierColumns.CellKind.Name, row), Is.InstanceOf<TextField>(),
                    "名字列要可**就地编辑**（内置档也能改名）");
                Assert.That(Cell(list, ScreenTierColumns.CellKind.Size, row), Is.InstanceOf<TextField>(),
                    "尺寸列要可**就地编辑**");
                Assert.That(Cell(list, ScreenTierColumns.CellKind.Actions, row), Is.InstanceOf<Button>(),
                    "第 " + row + " 行的『操作』列必须是真删除按钮（K1）");
            }
        }

        /// <summary>内置档的删除按钮**也可点**（禁用按钮不触发悬停提示，使用者就问不出"为什么点不动"）。</summary>
        [Test]
        public void K1_BuiltInRow_StillHasAClickableDeleteButton()
        {
            ScreenTierSet set = ScreenTierSet.Default();
            MultiColumnListView list = ScaleCalcTierBinder.Build(new TierFakeHost(set), set.Tiers);

            var button = (Button)Cell(list, ScreenTierColumns.CellKind.Actions, 0);
            Assert.That(set.CanDelete(set.Tiers[0].Id), Is.False, "前置：第 0 行是**不可删**的内置档");
            Assert.That(button.enabledSelf, Is.True, "内置档的删除按钮**不许**禁用");
            Assert.That(button.userData as string, Is.EqualTo(set.Tiers[0].Id), "按钮要知道自己属于哪一条");
        }

        /// <summary>
        /// `K1`：勾选框**绑的是当前状态**，且它的处理体把用户动作转给宿主（映射到正确的条目）。
        /// <para>⚠️ 这里**直调处理体**而不是 `toggle.value = true`：本批实测
        /// <b>`SendEvent` 在未接 panel 的元素上不派发</b>（EditMode 里裸造的单元格没有 panel）⇒
        /// 靠"设值看回调"是**测不出来**的。<c>RegisterValueChangedCallback</c> 那一行由现场人工验。</para>
        /// </summary>
        [Test]
        public void K1_IncludeCell_BindsState_AndReportsToTheHost()
        {
            ScreenTierSet set = ScreenTierSet.Default();
            set.SetIncluded(set.Tiers[1].Id, false);                       // ⚠️ 先改数据
            var host = new TierFakeHost(set);
            MultiColumnListView list = ScaleCalcTierBinder.Build(host, set.Tiers);   // 再建表（binder 拿的是**快照**）

            var toggle = (Toggle)Cell(list, ScreenTierColumns.CellKind.Include, 1);
            Assert.That(toggle.value, Is.False, "勾选框要绑**当前**状态（不是恒 true）");
            Assert.That(host.Calls, Is.Empty, "绑定本身不该产生任何回调（SetValueWithoutNotify）");

            ScaleCalcTierBinder.NotifyIncluded(toggle, true, host);
            Assert.That(host.Calls, Is.EqualTo(new[] { "include:" + set.Tiers[1].Id + ":True" }),
                "处理体要带着**这一行**的标识去找宿主");
        }

        /// <summary>`K1`：删除按钮的处理体同样带着"自己属于哪一行"。</summary>
        [Test]
        public void K1_DeleteCell_ReportsTheRowItBelongsTo()
        {
            ScreenTierSet set = ScreenTierSet.Default();
            var host = new TierFakeHost(set);
            MultiColumnListView list = ScaleCalcTierBinder.Build(host, set.Tiers);

            ScaleCalcTierBinder.NotifyDeleted(Cell(list, ScreenTierColumns.CellKind.Actions, 3), host);
            Assert.That(host.Calls, Is.EqualTo(new[] { "delete:" + set.Tiers[3].Id }));
        }

        /// <summary>`K1` + 实现册 §3.3：尺寸提交被拒时**视图回滚**（不许在框里留一个没生效的假值）。</summary>
        [Test]
        public void K1_SizeCell_RollsBackWhenTheHostRejects()
        {
            ScreenTierSet set = ScreenTierSet.Default();
            var host = new TierFakeHost(set);
            MultiColumnListView list = ScaleCalcTierBinder.Build(host, set.Tiers);
            var field = (TextField)Cell(list, ScreenTierColumns.CellKind.Size, 2);
            string original = set.Tiers[2].Size.ToString();
            Assert.That(field.value, Is.EqualTo(original), "绑定时填的是**当前**尺寸");

            host.SizeCommitOk = false;
            bool ok = ScaleCalcTierBinder.CommitSize(field, "乱七八糟", host);
            Assert.That(ok, Is.False);
            Assert.That(field.value, Is.EqualTo(original), "非法输入 ⇒ 回滚显示（数据本来就没改）");
        }

        /// <summary>`K1a`：被取消勾选的条目**仍在管理表里**（否则就再也勾不回来——`L0`/`L4` 全落空）。</summary>
        [Test]
        public void K1a_UncheckedTier_StaysInTheManagementTable()
        {
            ScreenTierSet set = ScreenTierSet.Default();
            int all = set.Tiers.Count;
            string id = set.Tiers[2].Id;

            Assert.That(set.SetIncluded(id, false), Is.True);

            MultiColumnListView list = ScaleCalcTierBinder.Build(new TierFakeHost(set), set.Tiers);
            Assert.That(list.itemsSource.Count, Is.EqualTo(all), "管理表**一条都不少**（K1a）");
            Assert.That(((Toggle)Cell(list, ScreenTierColumns.CellKind.Include, 2)).value, Is.False, "它的勾选框是关的");
            Assert.That(set.IncludedCount, Is.EqualTo(all - 1));
        }

        /// <summary>
        /// `U-B2` 的**结构性消除**（不是"实测没问题"）：管理表 `selectionType = None` ⇒
        /// "点按钮会不会同时切换行选中"**没有前提**；绑定用 `SetValueWithoutNotify` ⇒ 回收再绑也不误报变更。
        /// </summary>
        [Test]
        public void BinderStructurallyRemovesRowSelection_AndBindIsSilent()
        {
            ScreenTierSet set = ScreenTierSet.Default();
            var host = new TierFakeHost(set);
            MultiColumnListView list = ScaleCalcTierBinder.Build(host, set.Tiers);

            Assert.That(list.selectionType, Is.EqualTo(SelectionType.None), "管理表没有行选中这回事（U-B2 消解）");
            for (int row = 0; row < list.itemsSource.Count; row++)
                Cell(list, ScreenTierColumns.CellKind.Include, row);     // 重绑一遍
            Assert.That(host.Calls, Is.Empty, "反复绑定不许产生任何回调（虚拟化回收时会反复发生）");
            // 越界下标（列表刚被换掉、虚拟化拿着旧下标来问）不许抛
            Column include = list.columns[ScreenTierColumns.IndexOf(ScreenTierColumns.CellKind.Include)];
            VisualElement orphan = include.makeCell();
            Assert.DoesNotThrow(() => include.bindCell(orphan, 9999));
        }

        /// <summary>`R-L5`：管理表**声明并夹取**列宽（固定宽度、不参与分摊 ⇒ 结构性规避 `U-B3`）。</summary>
        [Test]
        public void Columns_DeclareFixedWidthWithinTheDeclaredClamp()
        {
            foreach (ScreenTierColumns.Spec spec in ScreenTierColumns.All)
            {
                Assert.That(spec.Width, Is.GreaterThanOrEqualTo(spec.MinWidth), spec.Title + "：默认宽度不许小于下限");
                Assert.That(spec.Width, Is.LessThanOrEqualTo(spec.MaxWidth), spec.Title + "：默认宽度不许大于上限");
            }

            ScreenTierSet set = ScreenTierSet.Default();
            MultiColumnListView list = ScaleCalcTierBinder.Build(new TierFakeHost(set), set.Tiers);
            for (int i = 0; i < list.columns.Count; i++)
            {
                Assert.That(list.columns[i].width.value, Is.EqualTo(ScreenTierColumns.All[i].Width).Within(0.01f));
                Assert.That(list.columns[i].minWidth.value, Is.EqualTo(ScreenTierColumns.All[i].MinWidth).Within(0.01f));
                Assert.That(list.columns[i].maxWidth.value, Is.EqualTo(ScreenTierColumns.All[i].MaxWidth).Within(0.01f));
            }
        }
    }
}
