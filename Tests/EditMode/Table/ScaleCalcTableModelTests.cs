using System.Collections.Generic;
using NUnit.Framework;
using Wayward.ScaleCalc.Editor;
using Wayward.ScaleCalc.Unity;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>批次 3 步 3.3/3.4：表格装配（真值可得性口径 `W-ScaleCalc-21`）与溢出/留白几何口径（`W-ScaleCalc-7`）。</summary>
    public sealed class ScaleCalcTableModelTests
    {
        private static readonly ScaleSize Reference = new ScaleSize(1920f, 1080f);

        private static ScaleTable BuildWithoutTruth()
            => ScaleTable.Build(Reference, ScreenMatchMode.MatchWidthOrHeight, 0.5f, 0f,
                                ScaleMode.ScaleWithScreenSize, default, false, default);

        [Test]
        public void Build_ProducesOneRowPerPreset()
        {
            ScaleTable table = BuildWithoutTruth();

            Assert.That(table.Rows.Count, Is.EqualTo(ScreenProfiles.All.Length));
            Assert.That(ScreenProfiles.All.Length, Is.GreaterThanOrEqualTo(6), "档位表至少 6 档（W-ScaleCalc-10 的机型覆盖）");
            Assert.That(ScreenProfiles.DistinctAspectRatioCount(), Is.GreaterThanOrEqualTo(3),
                "保守性质断言要求 ≥3 组宽高比不一致的屏幕");
            Assert.That(table.HasTruthRow, Is.False);
            Assert.That(table.TruthNote, Does.Contain("整表都是离线列"));
        }

        [Test]
        public void Build_WithoutTruth_MarksEveryRowOffline()
        {
            ScaleTable table = BuildWithoutTruth();
            foreach (ScaleTableRow row in table.Rows)
            {
                Assert.That(row.HasTruth, Is.False);
                Assert.That(row.DeltaText, Is.EqualTo("未经引擎对账（离线列）"));
            }
        }

        [Test]
        public void Build_TruthOnlyOnTheCurrentRenderSizeRow()
        {
            // 拿"手机横屏 16:9"那一档当现场尺寸，真值就用该行内核值构造 ⇒ 差值应当全 0
            ScreenProfile live = ScreenProfiles.All[2];
            Assert.That(live.Size, Is.EqualTo(new ScaleSize(1920f, 1080f)), "档位表第 3 档应为 1920x1080");

            var truth = new LiveTruth(1f, 100f, new ScaleSize(1920f, 1080f), new ScaleSize(1920f, 1080f),
                                      live.Size, ScreenSizeSource.CanvasRenderingDisplaySize,
                                      measuredReference: Reference, measuredMatch: 0.5f, measuredScreenMatch: ScreenMatchMode.MatchWidthOrHeight);   // 测量条件必须与本表一致
            ScaleTable table = ScaleTable.Build(Reference, ScreenMatchMode.MatchWidthOrHeight, 0.5f, 0f,
                                                ScaleMode.ScaleWithScreenSize, truth, true, default);

            int withTruth = 0;
            foreach (ScaleTableRow row in table.Rows)
            {
                if (!row.HasTruth) continue;
                withTruth++;
                Assert.That(row.Profile.Size, Is.EqualTo(live.Size), "只有现场尺寸那一行能带真值");
                Assert.That(row.IsCurrentRenderSize, Is.True);
                Assert.That(row.DeltaCanvasWidth, Is.EqualTo(0f), "同一尺寸下 内核画布 == 真值画布");
                Assert.That(row.DeltaCanvasHeight, Is.EqualTo(0f));
            }
            Assert.That(withTruth, Is.EqualTo(1), "**恰好一行**带真值（不许假装整表对过账）");
            Assert.That(table.HasTruthRow, Is.True);
            Assert.That(table.TruthNote, Does.Contain("其余"));
        }

        [Test]
        public void Build_AppendsLiveRow_WhenRenderSizeIsNotInPresets()
        {
            var liveSize = new ScaleSize(977.3334f, 493f);          // 本机编辑器 Game View 的真实渲染尺寸
            var truth = new LiveTruth(0.482039154f, 100f, new ScaleSize(2027.498f, 1022.739f), liveSize,
                                      liveSize, ScreenSizeSource.CanvasRenderingDisplaySize,
                                      measuredReference: Reference, measuredMatch: 0.5f, measuredScreenMatch: ScreenMatchMode.MatchWidthOrHeight);   // 测量条件必须与本表一致
            ScaleTable table = ScaleTable.Build(Reference, ScreenMatchMode.MatchWidthOrHeight, 0.5f, 0f,
                                                ScaleMode.ScaleWithScreenSize, truth, true, default);

            Assert.That(table.Rows.Count, Is.EqualTo(ScreenProfiles.All.Length + 1), "现场尺寸不在档位表里 ⇒ 表头补一行");
            Assert.That(table.Rows[0].HasTruth, Is.True, "现场行补在表头");
            Assert.That(table.Rows[0].Profile.Size, Is.EqualTo(liveSize));
            Assert.That(table.Rows[0].TruthSource, Is.EqualTo(ScreenSizeSource.CanvasRenderingDisplaySize));

            int withTruth = 0;
            foreach (ScaleTableRow row in table.Rows) if (row.HasTruth) withTruth++;
            Assert.That(withTruth, Is.EqualTo(1));
        }

        [Test]
        public void Build_Direction_UsesGeometricOverflowVerdict()
        {
            ScaleTable table = BuildWithoutTruth();
            ScaleTableRow phone = null;
            foreach (ScaleTableRow row in table.Rows)
                if (row.Profile.Size == new ScaleSize(1080f, 1920f)) { phone = row; break; }

            Assert.That(phone, Is.Not.Null);
            // 1080×1920 + 参考 1920×1080 + match=0.5 ⇒ 几何平均恰好 1.0 ⇒ 画布 = 1080×1920（比参考窄、比参考高）
            Assert.That(phone.ScreenSize.ScaleFactor, Is.EqualTo(1f).Within(1e-6f));
            Assert.That(phone.Direction, Is.EqualTo("横裁切 / 纵留白"), "几何口径：画布窄于参考=裁切、高于参考=留白");
        }

        [Test]
        public void AxisVerdict_ThreeCases()
        {
            Assert.That(AxisVerdict.Of(100f, 50f), Is.EqualTo(OverflowVerdict.Spare));
            Assert.That(AxisVerdict.Of(50f, 100f), Is.EqualTo(OverflowVerdict.Cropped));
            Assert.That(AxisVerdict.Of(100f, 100f), Is.EqualTo(OverflowVerdict.Exactly));
            Assert.That(AxisVerdict.Text(OverflowVerdict.Spare), Is.EqualTo("留白"));
            Assert.That(AxisVerdict.Text(OverflowVerdict.Cropped), Is.EqualTo("裁切"));
            Assert.That(AxisVerdict.Text(OverflowVerdict.Exactly), Is.EqualTo("正好"));
        }

        [Test]
        public void AxisVerdict_NoCanvasSize_SaysSoInsteadOfGuessing()
        {
            ScaleCalcInput input = ScaleCalcInput.Default;      // ScreenSize = (0,0) ⇒ 画布尺寸无定义
            ScaleCalcResult result = ScaleCalc.Evaluate(in input);
            Assert.That(AxisVerdict.Describe(in result, Reference), Does.Contain("无画布尺寸"));
        }

        /// <summary>
        /// 的**纯数据层那一半**（不必开窗）：行从**注入的** <c>profiles</c> 来。
        /// <c>null</c> ⇒ 沿用内置清单（证据包与既有调用**零改动**）；注入几档就渲染几行；注入 0 档是合法状态。
        /// </summary>
        [Test]
        public void Build_RowsComeFromTheInjectedProfiles()
        {
            var three = new List<ScreenProfile>
            {
                new ScreenProfile("甲", 1920f, 1080f, "测试"),
                new ScreenProfile("乙", 1080f, 1920f, "测试"),
                new ScreenProfile("丙", 2560f, 1080f, "测试"),
            };
            ScaleTable table = ScaleTable.Build(Reference, ScreenMatchMode.MatchWidthOrHeight, 0.5f, 0f,
                                                ScaleMode.ScaleWithScreenSize, default, false, default, three);
            Assert.That(table.Rows.Count, Is.EqualTo(3), "注入 3 档 ⇒ 3 行");
            Assert.That(table.Rows[0].Profile.Name, Is.EqualTo("甲"), "行序 = 注入顺序（实现册 §四⑫）");

            ScaleTable none = ScaleTable.Build(Reference, ScreenMatchMode.MatchWidthOrHeight, 0.5f, 0f,
                                               ScaleMode.ScaleWithScreenSize, default, false, default, new List<ScreenProfile>());
            Assert.That(none.Rows.Count, Is.EqualTo(0), "注入 0 档 ⇒ 0 行，且**不报错**（实现册 §四①）");

            Assert.That(BuildWithoutTruth().Rows.Count, Is.EqualTo(ScreenProfiles.All.Length),
                "不传（null）⇒ 沿用内置清单");
        }

        /// <summary>
        /// 的**纯数据层那一半**：现场行**只看现场尺寸在不在注入的这份清单里**——与勾选状态无关。
        /// <para>所以"取消勾选等于现场尺寸的那一档"也不会让现场行与真值列消失。</para>
        /// </summary>
        [Test]
        public void K7a_LiveRowDependsOnlyOnTheInjectedList_NotOnInclusion()
        {
            var liveSize = new ScaleSize(977.3334f, 493f);
            var truth = new LiveTruth(0.482039154f, 100f, new ScaleSize(2027.498f, 1022.739f), liveSize,
                                      liveSize, ScreenSizeSource.CanvasRenderingDisplaySize,
                                      measuredReference: Reference, measuredMatch: 0.5f, measuredScreenMatch: ScreenMatchMode.MatchWidthOrHeight);
            // 工作集收窄到两档，且**都不等于现场尺寸** ⇒ 现场行照样补得出来
            var two = new List<ScreenProfile>
            {
                new ScreenProfile("甲", 1920f, 1080f, "测试"),
                new ScreenProfile("乙", 1080f, 1920f, "测试"),
            };
            ScaleTable table = ScaleTable.Build(Reference, ScreenMatchMode.MatchWidthOrHeight, 0.5f, 0f,
                                                ScaleMode.ScaleWithScreenSize, truth, true, default, two);

            Assert.That(table.Rows.Count, Is.EqualTo(3), "两档 + 一行现场行");
            Assert.That(table.Rows[0].Profile.Size, Is.EqualTo(liveSize), "现场行仍插到最前");
            Assert.That(table.Rows[0].HasTruth, Is.True, "真值仍落在现场行上");
        }
    }
}
