using System.Collections.Generic;
using NUnit.Framework;
using Wayward.ScaleCalc.Editor;
using Wayward.ScaleCalc.Unity;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// **工作集 ⇒ 选型表**。
    /// <para>走**真实路径**：行来源 = <c>ScreenTierSet.IncludedTiers()</c> → <c>ToProfile()</c>（与窗口 <c>Rebuild</c> 同一条），
    /// 而不是手搓一份假清单——"<c>Build</c> 本身吃不吃注入的 profiles"那半边在 <see cref="ScaleCalcTableModelTests"/>。</para>
    /// <para>为什么不并进界面文件：这两条判的是**表算哪些行**，与"单元格里有没有勾选框"是两件事（也是行数红线的要求）。</para>
    /// </summary>
    public sealed class ScaleCalcTierSelectionTests
    {
        private static readonly ScaleSize Reference = new ScaleSize(1920f, 1080f);

        /// <summary>按当前工作集装配选型表（与窗口 `Rebuild` 同一条路径）。</summary>
        private static ScaleTable SelectionTableFor(ScreenTierSet set, LiveTruth truth, bool truthOk)
        {
            var profiles = new List<ScreenProfile>();
            foreach (ScreenTier tier in set.IncludedTiers()) profiles.Add(tier.ToProfile());
            return ScaleTable.Build(Reference, ScreenMatchMode.MatchWidthOrHeight, 0.5f, 0f,
                                    ScaleMode.ScaleWithScreenSize, truth, truthOk, default, profiles);
        }

        /// <summary>`K2`：取消勾选 ⇒ 那一条**不出现在选型表里**（不参与计算）。</summary>
        [Test]
        public void K2_UncheckedTier_DropsOutOfTheSelectionTable()
        {
            ScreenTierSet set = ScreenTierSet.Default();
            Assert.That(SelectionTableFor(set, default, false).Rows.Count, Is.EqualTo(ScreenProfiles.All.Length),
                "初值 = 内置全部纳入");

            Assert.That(set.SetIncluded(set.Tiers[0].Id, false), Is.True);
            Assert.That(SelectionTableFor(set, default, false).Rows.Count, Is.EqualTo(ScreenProfiles.All.Length - 1),
                "取消勾选 ⇒ 选型表少一行");
            Assert.That(set.Tiers.Count, Is.EqualTo(ScreenProfiles.All.Length), "管理表一条都不少（K1a 的另一半）");
        }

        /// <summary>
        /// 把"等于现场尺寸"的那一档**取消勾选**之后，选型表**仍有现场行与真值列**。
        /// <para>现场行锚定的是"当前渲染尺寸"这个**客观事实**，不是"档位选择"——两者不该被勾选状态耦合。</para>
        /// </summary>
        [Test]
        public void K7a_LiveRowSurvives_EvenWhenTheMatchingTierIsUnchecked()
        {
            ScreenTierSet set = ScreenTierSet.Default();
            var liveSize = new ScaleSize(1080f, 1920f);            // = 内置「手机竖屏 9:16」

            string liveId = null;
            foreach (ScreenTier tier in set.Tiers) if (tier.Size == liveSize) { liveId = tier.Id; break; }
            Assert.That(liveId, Is.Not.Null, "前置：内置清单里确实有一档等于现场尺寸");
            Assert.That(set.SetIncluded(liveId, false), Is.True, "把它取消勾选");

            var truth = new LiveTruth(1f, 100f, liveSize, liveSize, liveSize,
                                      ScreenSizeSource.CanvasRenderingDisplaySize, measuredReference: Reference, measuredMatch: 0.5f, measuredScreenMatch: ScreenMatchMode.MatchWidthOrHeight);
            ScaleTable table = SelectionTableFor(set, truth, true);

            ScaleTableRow withTruth = null;
            foreach (ScaleTableRow row in table.Rows) if (row.HasTruth) { withTruth = row; break; }
            Assert.That(withTruth, Is.Not.Null, "K7a：取消勾选之后真值列**不许**消失");
            Assert.That(withTruth.Profile.Size, Is.EqualTo(liveSize));
            Assert.That(table.HasTruthRow, Is.True);
        }

        /// <summary>`K2` 的边界：**全不选** ⇒ 选型表可以是空表，且**不报错**（实现册 §四①）。</summary>
        [Test]
        public void K2b_NoTierIncluded_YieldsAnEmptyTable_WithoutThrowing()
        {
            ScreenTierSet set = ScreenTierSet.Default();
            foreach (ScreenTier tier in new List<ScreenTier>(set.Tiers)) set.SetIncluded(tier.Id, false);

            Assert.That(set.IncludedCount, Is.EqualTo(0));
            ScaleTable table = null;
            Assert.DoesNotThrow(() => table = SelectionTableFor(set, default, false));
            Assert.That(table.Rows.Count, Is.EqualTo(0), "0 档参与计算 ⇒ 0 行，**不是**报错");
        }
    }
}
