using NUnit.Framework;
using Wayward.ScaleCalc.Editor;
using UnityEngine.UIElements;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// **档位改名**（内置与自定义都能改名）。
    /// <para>两半：① **纯判据**（<see cref="ScreenTierSet.TryResolveRename"/> 与 <see cref="ScreenTierSet.TryParseSize"/>）
    /// —— 空名字的口径、截断、同名、解析都在这里钉住；② **界面接线**（Name 列真造出可编辑 <c>TextField</c>、
    /// 处理体把动作转给宿主、被拒时回滚显示）。</para>
    /// <para>⚠️ 与 <see cref="ScaleCalcTierUiTests"/> 同一口径：**直调处理体**而不是合成事件——实测 <c>SendEvent</c>
    /// 在未接 panel 的元素上根本不派发。</para>
    /// </summary>
    public sealed class ScaleCalcTierNameTests
    {
        private static VisualElement Cell(MultiColumnListView list, ScreenTierColumns.CellKind kind, int row)
        {
            Column column = list.columns[ScreenTierColumns.IndexOf(kind)];
            VisualElement element = column.makeCell();     // ← 真造一个单元格
            column.bindCell(element, row);
            return element;
        }

        /// <summary>口径：普通改名 ⇒ 落地；**同名 ⇒ 无事可做**（`false` + 无提示，别报"改好了"）。</summary>
        [Test]
        public void ResolveRename_PlainName_Applies_AndSameNameIsANoOp()
        {
            var size = new ScaleSize(1600f, 900f);

            Assert.That(ScreenTierSet.TryResolveRename(ScreenTierKind.Custom, size, "  我的手机  ", "自定义 1600x900",
                out string resolved, out string notice), Is.True, "换了名字 ⇒ 落地");
            Assert.That(resolved, Is.EqualTo("我的手机"), "名字要 Trim（NormalizeName 的口径）");
            Assert.That(notice, Is.Null, "正常改名不该有任何提示");

            Assert.That(ScreenTierSet.TryResolveRename(ScreenTierKind.Custom, size, "我的手机", "我的手机",
                out _, out string same), Is.False, "同名 ⇒ 不落地");
            Assert.That(same, Is.Null, "同名不是错误 ⇒ 不许报提示（否则每次回车都刷一行字）");
        }

        /// <summary>口径：**自定义行**清空 ⇒ 回落到自动名（与「新增一行」同源）。</summary>
        [Test]
        public void ResolveRename_EmptyName_OnCustom_FallsBackToAutoName()
        {
            var size = new ScaleSize(1600f, 900f);

            Assert.That(ScreenTierSet.TryResolveRename(ScreenTierKind.Custom, size, "   ", "我的手机",
                out string resolved, out string notice), Is.True, "自定义行清空 ⇒ 回落到自动名");
            Assert.That(resolved, Is.EqualTo(ScreenTierSet.AutoName(size)));
            Assert.That(notice, Is.EqualTo(ScreenTierText.NameFellBackToAuto(ScreenTierSet.AutoName(size))),
                "回落必须**如实说**（否则使用者以为清空是没生效）");

            // 本来就是自动名 ⇒ 清空等于没变，别报"已恢复"
            Assert.That(ScreenTierSet.TryResolveRename(ScreenTierKind.Custom, size, "", ScreenTierSet.AutoName(size),
                out _, out string none), Is.False);
            Assert.That(none, Is.Null);
        }

        /// <summary>口径：**内置档**清空 ⇒ 拒绝（内置档名对的是清单口径，没有自动名可回落）。</summary>
        [Test]
        public void ResolveRename_EmptyName_OnBuiltIn_IsRejected()
        {
            var size = new ScaleSize(1440f, 3120f);

            Assert.That(ScreenTierSet.TryResolveRename(ScreenTierKind.BuiltIn, size, " ",
                "手机竖屏超长 9:19.5", out string resolved, out string notice), Is.False, "内置档清空 ⇒ 拒绝");
            Assert.That(resolved, Is.EqualTo("手机竖屏超长 9:19.5"), "拒绝时返回**当前**名字（调用方据此回滚）");
            Assert.That(notice, Is.EqualTo(ScreenTierText.BuiltInNameCannotBeEmpty()));
        }

        /// <summary>口径：**内置档改名是可以的**，且超 32 字照旧截断并如实提示。</summary>
        [Test]
        public void ResolveRename_BuiltInCanBeRenamed_AndLongNamesAreTruncatedWithANotice()
        {
            var size = new ScaleSize(1440f, 3120f);
            Assert.That(ScreenTierSet.TryResolveRename(ScreenTierKind.BuiltIn, size, "大屏手机", "手机竖屏超长 9:19.5",
                out string renamed, out _), Is.True, "内置档也能改名（用户 2026-09-20 裁定）");
            Assert.That(renamed, Is.EqualTo("大屏手机"));

            string longName = new string('长', ScreenTierSet.MaxNameLength + 5);
            Assert.That(ScreenTierSet.TryResolveRename(ScreenTierKind.Custom, size, longName, "x",
                out string cut, out string notice), Is.True);
            Assert.That(cut.Length, Is.EqualTo(ScreenTierSet.MaxNameLength), "超 32 字要截断");
            Assert.That(notice, Is.Not.Null.And.Contains(ScreenTierSet.MaxNameLength.ToString()), "截断要**如实说**");
        }

        /// <summary>同名/改名之后**仍是同一行**：勾选态不丢——这正是 <c>Id</c> 与 <c>Name</c> 分开的理由。</summary>
        [Test]
        public void Rename_KeepsItTheSameRow()
        {
            ScreenTierSet set = ScreenTierSet.Default();
            string id = set.Tiers[0].Id;
            set.SetIncluded(id, false);

            Assert.That(set.Rename(id, "我改的名字"), Is.True);
            int at = set.IndexOf(id);
            Assert.That(at, Is.EqualTo(0), "改名后仍在原位置（标识没变）");
            Assert.That(set.Tiers[at].Name, Is.EqualTo("我改的名字"));
            Assert.That(set.Tiers[at].Included, Is.False, "勾选态不因为改名而丢");

            // 来源标注按 Id 里嵌的**原名**回清单取 ⇒ 改名不影响它
            Assert.That(set.Tiers[at].ToProfile().Source, Is.EqualTo(ScreenProfiles.All[0].Source),
                "内置档改名后，来源标注仍取得到清单原文（ScreenTiers.SourceOf 的口径）");
        }

        /// <summary>Name 列是**可编辑**的 <c>TextField</c>，且 <c>isDelayed</c>（回车/失焦才提交）。</summary>
        [Test]
        public void K1_NameCell_IsADelayedEditableField_AndBindsTheCurrentName()
        {
            ScreenTierSet set = ScreenTierSet.Default();
            var host = new TierFakeHost(set);
            MultiColumnListView list = ScaleCalcTierBinder.Build(host, set.Tiers);

            var field = (TextField)Cell(list, ScreenTierColumns.CellKind.Name, 1);
            Assert.That(field.isDelayed, Is.True, "名字列必须 isDelayed（每敲一个字符就改名会当场乱）");
            Assert.That(field.value, Is.EqualTo(set.Tiers[1].Name), "绑定时填的是**当前**名字");
            Assert.That(host.Calls, Is.Empty, "绑定本身不该产生任何回调（SetValueWithoutNotify）");

            Assert.That(ScaleCalcTierBinder.CommitName(field, "我的名字", host), Is.True);
            Assert.That(host.Calls, Is.EqualTo(new[] { "name:" + set.Tiers[1].Id + ":我的名字" }),
                "处理体要带着**这一行**的标识去找宿主");
        }

        /// <summary>宿主拒绝（内置档被清空）⇒ **视图回滚**，不许在框里留一个没生效的假值。</summary>
        [Test]
        public void K1_NameCell_RollsBackWhenTheHostRejects()
        {
            ScreenTierSet set = ScreenTierSet.Default();
            var host = new TierFakeHost(set);
            MultiColumnListView list = ScaleCalcTierBinder.Build(host, set.Tiers);
            var field = (TextField)Cell(list, ScreenTierColumns.CellKind.Name, 0);
            string original = set.Tiers[0].Name;

            host.NameCommitOk = false;
            Assert.That(ScaleCalcTierBinder.CommitName(field, "", host), Is.False);
            Assert.That(field.value, Is.EqualTo(original), "拒绝 ⇒ 回滚显示（数据本来就没改）");
        }
    }
}
