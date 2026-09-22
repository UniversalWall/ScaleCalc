using System.Collections.Generic;
using NUnit.Framework;
using Wayward.ScaleCalc.Editor;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// **跨档统计 + 结论条 + 推荐值**：纯函数断言，不用开窗。
    /// <para>与逐档同源 · <c>Expand</c>/<c>Shrink</c> 的结构性保证 · 推荐值 · 安全区随勾选 ·
    /// 结论条同源 · 逐字限定语 · 0 档。逐档断言在 <see cref="ScaleFitTests"/>。</para>
    /// </summary>
    public sealed class ScaleFitSummaryTests
    {
        private static readonly ScaleSize Reference = new ScaleSize(1920f, 1080f);
        private const float Dpi = 144f;
        private const float Match = 0.5f;

        private static IReadOnlyList<ScreenProfile> BuiltIn => ScreenProfiles.All;

        private static ScaleCalcResult Evaluate(ScaleSize screen)
        {
            ScaleCalcInput input = ScaleCalcInput.Default;
            input.Mode = ScaleMode.ScaleWithScreenSize;
            input.ScreenSize = screen;
            input.ReferenceResolution = Reference;
            input.ScreenMatch = ScreenMatchMode.MatchWidthOrHeight;
            input.MatchWidthOrHeight = Match;
            input.ScreenDpi = Dpi;
            return ScaleCalc.Evaluate(in input);
        }

        /// <summary>`PK5`：统计与逐档**同源**——逐档读数逐个核，聚合值必须由同一套 `ScaleFit.Of` 推得。</summary>
        [Test]
        public void PK5_Stats_ReuseThePerTierFit()
        {
            ScaleFit.ModeStats stats = ScaleFit.Stats(BuiltIn, Reference, ScreenMatchMode.MatchWidthOrHeight, Match, Dpi);

            int cropped = 0;
            float worst = 0f;
            foreach (ScreenProfile profile in BuiltIn)
            {
                ScaleCalcResult perTier = Evaluate(profile.Size);
                ScaleFit.TierFit fit = ScaleFit.Of(in perTier, Reference);
                if (fit.Horizontal.IsCropped || fit.Vertical.IsCropped) cropped++;
                if (fit.Horizontal.CropRatio > worst) worst = fit.Horizontal.CropRatio;
                if (fit.Vertical.CropRatio > worst) worst = fit.Vertical.CropRatio;
            }

            Assert.That(stats.CroppedCount, Is.EqualTo(cropped), "会裁档位数必须与逐档判定一致");
            Assert.That(stats.WorstCropRatio, Is.EqualTo(worst).Within(1e-6f), "最坏裁切必须与逐档最大值一致");
        }

        /// <summary>`PK6`：`Expand` ⇒ **会裁档位 0**（结构性保证，带容差）——内置 7 档 + 注入档位都成立。</summary>
        [Test]
        public void PK6_Expand_NeverCrops_OnBuiltInAndInjectedTiers()
        {
            ScaleFit.ModeStats stats = ScaleFit.Stats(BuiltIn, Reference, ScreenMatchMode.Expand, Match, Dpi);
            Assert.That(stats.TierCount, Is.EqualTo(ScreenProfiles.All.Length));
            Assert.That(stats.CroppedCount, Is.EqualTo(0), "Expand = 画布永不小于参考 ⇒ 一档都不裁");
            Assert.That(stats.SafeArea.Width, Is.EqualTo(1920f).Within(0.5f), "安全设计区就是整张参考画布");
            Assert.That(stats.SafeArea.Height, Is.EqualTo(1080f).Within(0.5f));

            var injected = new List<ScreenProfile>
            {
                new ScreenProfile("奇葩 5:3", 2000f, 1200f, "注入"),
                new ScreenProfile("小屏 4:3", 640f, 480f, "注入"),
                new ScreenProfile("超宽 32:9", 5120f, 1440f, "注入"),
            };
            Assert.That(ScaleFit.Stats(injected, Reference, ScreenMatchMode.Expand, Match, Dpi).CroppedCount, Is.EqualTo(0));
        }

        /// <summary><c>Shrink</c> ⇒ **留白档位 0**（画布永不大于参考）。</summary>
        [Test]
        public void PK7_Shrink_NeverLeavesSpare()
        {
            ScaleFit.ModeStats stats = ScaleFit.Stats(BuiltIn, Reference, ScreenMatchMode.Shrink, Match, Dpi);
            Assert.That(stats.WorstSpareRatio, Is.EqualTo(0f), "Shrink = 画布永不大于参考");
            Assert.That(stats.CroppedCount, Is.GreaterThan(0), "代价是裁得最狠");
        }

        /// <summary>推荐值可复现、优于 <c>match=0.5</c>，且**两个目标函数都有断言**（不许留"没人调用的预留值"）。</summary>
        [Test]
        public void PK8_Recommendation_IsDeterministic_AndBeatsTheHardcodedMatch()
        {
            bool okCrop = ScaleFit.TryRecommendMatch(BuiltIn, Reference, Dpi, ScaleFit.Objective.MinWorstCrop,
                                                     out float best, out ScaleFit.ModeStats atBest);
            Assert.That(okCrop, Is.True);
            Assert.That(best, Is.EqualTo(0.17f).Within(0.06f), "设计册 §3.3 实测 ≈0.17");

            ScaleFit.ModeStats atHalf = ScaleFit.Stats(BuiltIn, Reference, ScreenMatchMode.MatchWidthOrHeight, 0.5f, Dpi);
            Assert.That(atBest.WorstCropRatio, Is.LessThanOrEqualTo(atHalf.WorstCropRatio), "推荐值的最坏裁切不许比 0.5 差");

            bool okAgain = ScaleFit.TryRecommendMatch(BuiltIn, Reference, Dpi, ScaleFit.Objective.MinWorstCrop,
                                                      out float again, out _);
            Assert.That(okAgain && again == best, Is.True, "同输入同输出");

            bool okSpare = ScaleFit.TryRecommendMatch(BuiltIn, Reference, Dpi, ScaleFit.Objective.MinWorstSpare,
                                                      out float spareBest, out ScaleFit.ModeStats atSpare);
            Assert.That(okSpare, Is.True, "另一个目标函数也得真的能跑（否则「预留」是假的）");
            Assert.That(atSpare.WorstSpareRatio, Is.LessThanOrEqualTo(atBest.WorstSpareRatio), "留白目标的最小留白 ≤ 裁切目标的");
            Assert.That(spareBest, Is.GreaterThan(0.7f),
                "🔴 留白最小落在**高 match 一侧**（≈0.83）——不是朴素想象的 match=0："
                + "match=0 时竖屏族的画布被拉得极高、留白 285%");
            Assert.That(ScaleFit.RecommendationText(atBest, best, ScaleFit.Objective.MinWorstCrop), Does.StartWith("推荐 match ").And.Contains("留白代价 +"));
        }

        /// <summary>安全设计区 = 逐轴 min(实际画布)，**只对当前参与档位**；改清单 ⇒ 随之变。</summary>
        [Test]
        public void PK9_SafeArea_TracksTheIncludedTiers()
        {
            ScaleFit.ModeStats all = ScaleFit.Stats(BuiltIn, Reference, ScreenMatchMode.MatchWidthOrHeight, Match, Dpi);

            float minWidth = float.MaxValue, minHeight = float.MaxValue;
            foreach (ScreenProfile profile in BuiltIn)
            {
                ScaleCalcResult result = Evaluate(profile.Size);
                if (!result.HasCanvasSize) continue;
                if (result.CanvasSize.Width < minWidth) minWidth = result.CanvasSize.Width;
                if (result.CanvasSize.Height < minHeight) minHeight = result.CanvasSize.Height;
            }
            Assert.That(all.SafeArea.Width, Is.EqualTo(minWidth).Within(1e-3f), "安全区 = 逐轴 min（与逐档读数同源）");
            Assert.That(all.SafeArea.Height, Is.EqualTo(minHeight).Within(1e-3f));

            var landscapeOnly = new List<ScreenProfile>();
            foreach (ScreenProfile profile in BuiltIn)
                if (profile.Size.Width >= profile.Size.Height) landscapeOnly.Add(profile);
            ScaleFit.ModeStats narrower = ScaleFit.Stats(landscapeOnly, Reference, ScreenMatchMode.MatchWidthOrHeight, Match, Dpi);
            Assert.That(narrower.SafeArea.Width, Is.GreaterThan(all.SafeArea.Width), "只勾横屏族 ⇒ 安全区变宽");
            Assert.That(narrower.TierCount, Is.EqualTo(landscapeOnly.Count), "档位数如实跟着清单走");
        }

        /// <summary>`PK11` + `PK19` + `PK22`：结论条与统计**同源**、逐字限定语、0 档不产出数值。</summary>
        [Test]
        public void PK11_PK19_PK22_Headline_IsSourcedFromStats_AndSaysNothingItCannotKnow()
        {
            ScaleFit.ModeStats stats = ScaleFit.Stats(BuiltIn, Reference, ScreenMatchMode.MatchWidthOrHeight, Match, Dpi);
            string headline = ScaleFit.Headline(stats, Reference);

            Assert.That(stats.CroppedCount, Is.EqualTo(6), "现场实测：match 0.5 ⇒ 6/7 档会裁");
            Assert.That(stats.WorstCropTier, Is.EqualTo("手机竖屏超长 9:19.5"), "最坏的是它（不是次坏的 9:16）");
            Assert.That(stats.WorstCropRatio, Is.EqualTo(0.49f).Within(0.005f));
            Assert.That(stats.WorstCropAxis, Is.EqualTo(ScaleFit.Axis.Horizontal));
            Assert.That(stats.SafeArea.Width, Is.EqualTo(978f).Within(2f), "现场实测 978×935");
            Assert.That(stats.SafeArea.Height, Is.EqualTo(935f).Within(2f));

            Assert.That(headline, Does.Contain("参与 7 档"));
            Assert.That(headline, Does.Contain("6 档会裁"));
            Assert.That(headline, Does.Contain("手机竖屏超长 9:19.5 横向"));
            Assert.That(headline, Does.Contain("左右各 −471px"), "px 是**单侧**、且与设计册 §5.1 的实测一致");
            Assert.That(headline, Does.Contain(ScaleFit.SizeText(stats.SafeArea)), "与统计同源（同一个数）");
            Assert.That(headline, Does.Contain("（参考的 51%×87%）"), "整数百分比**手拼**（`P0` 会插空格并再乘一次 100）");
            Assert.That(headline, Does.Contain("结论只对当前勾选的 7 档负责"), "PK19 的**逐字**限定语");

            string zero = ScaleFit.Headline(
                ScaleFit.Stats(new List<ScreenProfile>(), Reference, ScreenMatchMode.MatchWidthOrHeight, Match, Dpi), Reference);
            Assert.That(zero, Is.EqualTo("当前 0 档参与计算 ⇒ 无结论（上表可勾回）"));

            string badReference = ScaleFit.Headline(stats, new ScaleSize(0f, 0f));
            Assert.That(badReference, Does.Contain("—"), "参考无效 ⇒ 一律显示 —（不编数）");

            string draft = ScaleFit.Headline(stats, Reference, builtInDraftCount: 7);
            Assert.That(draft, Does.Contain("（含 7 档内置草案）"), "内置草案档如实标注（W-ScaleCalc-10）");
        }

        /// <summary>三方式并排（"三选一"小结的数据源）：`Expand` 那条必须说"无裁切"。</summary>
        [Test]
        public void AcrossModes_ProducesTheThreeLinesOfTheComparison()
        {
            ScaleFit.ModeStats[] modes = ScaleFit.AcrossModes(BuiltIn, Reference, Match, Dpi);
            Assert.That(modes.Length, Is.EqualTo(3));
            Assert.That(modes[0].Mode, Is.EqualTo(ScreenMatchMode.MatchWidthOrHeight));
            Assert.That(modes[1].Mode, Is.EqualTo(ScreenMatchMode.Expand));
            Assert.That(modes[2].Mode, Is.EqualTo(ScreenMatchMode.Shrink));

            Assert.That(ScaleFit.ModeLine(modes[0], Match), Does.StartWith("Match 0.50：会裁 6/7"));
            Assert.That(ScaleFit.ModeLine(modes[1], Match), Does.StartWith("Expand：会裁 0/7"));
            Assert.That(ScaleFit.ModeLine(modes[0], Match), Does.Contain("安全设计区 978×935"));
        }
    }
}
