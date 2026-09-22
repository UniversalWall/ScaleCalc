using NUnit.Framework;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// 内核 vs 手算：<c>Match</c> 对数加权 · <c>Expand</c>/<c>Shrink</c> 取小取大 · 保守性质 · **反例**（对数平均而非算术平均）。
    /// 期望值全部取自 <see cref="HandCalcSamples"/> ——**常量与断言分离**。
    /// </summary>
    public sealed class ScaleCalcHandCalcTests
    {
        private static readonly ScaleSize Reference = new ScaleSize(1920f, 1080f);

        /// <summary>≥3 组宽高比不一致的屏幕（保守性质那条用）。</summary>
        private static readonly ScaleSize[] Screens =
        {
            new ScaleSize(1080f, 1920f),    // 竖屏（比值 0.5625 / 1.77778）
            new ScaleSize(2400f, 1080f),    // 超宽（1.25 / 1.0）
            new ScaleSize(3120f, 1440f),    // 平板横屏（1.625 / 1.33333）
        };

        [Test]
        public void MatchHalf_1440x3120_Equals147196()
        {
            ScaleCalcResult r = ScaleCalc.Evaluate(in HandCalcSamples.MatchHalf_1440x3120);

            Assert.That(r.HasResult, Is.True, "ScaleWithScreenSize 不该早退");
            Assert.That(r.Branch, Is.EqualTo(ScaleBranch.ScaleWithScreenSize));
            Assert.That(r.ScaleFactor,
                Is.EqualTo(HandCalcSamples.MatchHalf_1440x3120_ScaleFactor).Within(HandCalcSamples.Tolerance),
                "scaleFactor 与手算 2^0.55774 不符");
            Assert.That(r.HasCanvasSize, Is.True);
            Assert.That(r.CanvasSize.Width,
                Is.EqualTo(HandCalcSamples.MatchHalf_1440x3120_CanvasWidth).Within(HandCalcSamples.Tolerance));
            Assert.That(r.CanvasSize.Height,
                Is.EqualTo(HandCalcSamples.MatchHalf_1440x3120_CanvasHeight).Within(HandCalcSamples.Tolerance));
            Assert.That(r.ReferencePixelsPerUnit, Is.EqualTo(100f), "随屏幕尺寸模式不该改写 refPPU");
        }

        [Test]
        public void Expand_IsMinOfRatios()
        {
            ScaleCalcResult r = ScaleCalc.Evaluate(in HandCalcSamples.AspectSwapped_Expand);

            Assert.That(r.Branch, Is.EqualTo(ScaleBranch.ScaleWithScreenSize));
            Assert.That(r.ScaleFactor,
                Is.EqualTo(HandCalcSamples.AspectSwapped_Expand_ScaleFactor).Within(HandCalcSamples.Tolerance));
            Assert.That(r.CanvasSize.Width,
                Is.EqualTo(HandCalcSamples.AspectSwapped_Expand_CanvasWidth).Within(HandCalcSamples.Tolerance));
            Assert.That(r.CanvasSize.Height,
                Is.EqualTo(HandCalcSamples.AspectSwapped_Expand_CanvasHeight).Within(HandCalcSamples.Tolerance));
        }

        [Test]
        public void Shrink_IsMaxOfRatios()
        {
            ScaleCalcResult r = ScaleCalc.Evaluate(in HandCalcSamples.AspectSwapped_Shrink);

            Assert.That(r.Branch, Is.EqualTo(ScaleBranch.ScaleWithScreenSize));
            Assert.That(r.ScaleFactor,
                Is.EqualTo(HandCalcSamples.AspectSwapped_Shrink_ScaleFactor).Within(HandCalcSamples.Tolerance));
            Assert.That(r.CanvasSize.Width,
                Is.EqualTo(HandCalcSamples.AspectSwapped_Shrink_CanvasWidth).Within(HandCalcSamples.Tolerance));
            Assert.That(r.CanvasSize.Height,
                Is.EqualTo(HandCalcSamples.AspectSwapped_Shrink_CanvasHeight).Within(HandCalcSamples.Tolerance));
        }

        [Test]
        public void ExpandShrink_ConservativeProperty_3AspectRatios()
        {
            foreach (ScaleSize screen in Screens)
            {
                ScaleCalcResult expand = ScaleCalc.Evaluate(
                    HandCalcSamples.WithScreen(Reference, screen, ScreenMatchMode.Expand, 0f));
                Assert.That(expand.HasCanvasSize, Is.True);
                Assert.That(expand.CanvasSize.Width, Is.GreaterThanOrEqualTo(Reference.Width),
                    $"Expand 应「画布只增不减」（{screen}）");
                Assert.That(expand.CanvasSize.Height, Is.GreaterThanOrEqualTo(Reference.Height),
                    $"Expand 应「画布只增不减」（{screen}）");

                ScaleCalcResult shrink = ScaleCalc.Evaluate(
                    HandCalcSamples.WithScreen(Reference, screen, ScreenMatchMode.Shrink, 0f));
                Assert.That(shrink.HasCanvasSize, Is.True);
                Assert.That(shrink.CanvasSize.Width, Is.LessThanOrEqualTo(Reference.Width),
                    $"Shrink 应「画布只减不增」（{screen}）");
                Assert.That(shrink.CanvasSize.Height, Is.LessThanOrEqualTo(Reference.Height),
                    $"Shrink 应「画布只减不增」（{screen}）");
            }
        }

        /// <summary>**反例断言**（一号坑）：一轴减半、一轴翻倍时几何平均给 1.0，算术平均会给 1.25。</summary>
        [Test]
        public void LogAverage_ReciprocalAxes_EqualsOne()
        {
            ScaleCalcResult r = ScaleCalc.Evaluate(in HandCalcSamples.ReciprocalAxes_960x2160);

            Assert.That(r.ScaleFactor,
                Is.EqualTo(HandCalcSamples.ReciprocalAxes_GeometricMean_ScaleFactor).Within(HandCalcSamples.Tolerance),
                "match=0.5 必须是对数（几何）平均 ⇒ 1.0000");
            Assert.That(r.ScaleFactor,
                Is.Not.EqualTo(HandCalcSamples.ReciprocalAxes_ArithmeticMean_WrongAnswer).Within(HandCalcSamples.Tolerance),
                "算成算术平均（1.25）就是一号坑");
        }
    }
}
