using NUnit.Framework;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary><c>ConstantPhysicalSize</c> 的**兜底链**（真报 / 兜底两条分支）+ targetDPI 常量表。</summary>
    public sealed class ScaleCalcPhysicalTests
    {
        [Test]
        public void FallbackDpiChain_96To160()
        {
            // ── 兜底分支：ScreenDpi == 0 ⇒ 退化为 FallbackScreenDPI(96)，静默失真 ──
            ScaleCalcResult fallback = ScaleCalc.Evaluate(in HandCalcSamples.Physical_FallbackDpi96);
            Assert.That(fallback.Branch, Is.EqualTo(ScaleBranch.ConstantPhysicalSize));
            Assert.That(fallback.UsedFallbackDpi, Is.True, "ScreenDpi == 0 必须被标记为走了兜底链");
            Assert.That(fallback.EffectiveDpi, Is.EqualTo(96f), "兜底后实际参与算式的 dpi");
            Assert.That(fallback.TargetDpi, Is.EqualTo(HandCalcSamples.Physical_Points_TargetDpi));
            Assert.That(fallback.ScaleFactor,
                Is.EqualTo(HandCalcSamples.Physical_FallbackDpi96_ScaleFactor).Within(HandCalcSamples.Tolerance),
                "96 / 72 的手算值");

            // ── 真报分支：ScreenDpi == 160 ⇒ 不走兜底 ──
            ScaleCalcResult reported = ScaleCalc.Evaluate(in HandCalcSamples.Physical_ReportedDpi160);
            Assert.That(reported.UsedFallbackDpi, Is.False, "非 0 的 dpi 不该被当成兜底");
            Assert.That(reported.EffectiveDpi, Is.EqualTo(160f));
            Assert.That(reported.ScaleFactor,
                Is.EqualTo(HandCalcSamples.Physical_ReportedDpi160_ScaleFactor).Within(HandCalcSamples.Tolerance),
                "160 / 72 的手算值");
        }

        [Test]
        public void ReferencePixelsPerUnit_IsRewritten_100x72Over96()
        {
            // 三模式都会写 refPPU；物理模式还会**改写**它：100 * 72 / 96 = 75
            ScaleCalcResult reported = ScaleCalc.Evaluate(in HandCalcSamples.Physical_ReportedDpi160);
            Assert.That(reported.ReferencePixelsPerUnit,
                Is.EqualTo(HandCalcSamples.Physical_RefPixelsPerUnit).Within(HandCalcSamples.Tolerance),
                "Points 下 refPPU 应被改写为 75（不是常量 100）");

            ScaleCalcResult fallback = ScaleCalc.Evaluate(in HandCalcSamples.Physical_FallbackDpi96);
            Assert.That(fallback.ReferencePixelsPerUnit,
                Is.EqualTo(HandCalcSamples.Physical_RefPixelsPerUnit).Within(HandCalcSamples.Tolerance),
                "兜底分支同样改写 refPPU（改写量与 dpi 无关）");
        }

        [TestCase(PhysicalUnit.Centimeters, 2.54f)]
        [TestCase(PhysicalUnit.Millimeters, 25.4f)]
        [TestCase(PhysicalUnit.Inches, 1f)]
        [TestCase(PhysicalUnit.Points, 72f)]
        [TestCase(PhysicalUnit.Picas, 6f)]
        public void TargetDpi_MatchesEngineConstantTable(PhysicalUnit unit, float expectedTargetDpi)
        {
            ScaleCalcInput input = HandCalcSamples.WithPhysical(new ScaleSize(800f, 600f), 96f);
            input.PhysicalUnit = unit;

            ScaleCalcResult r = ScaleCalc.Evaluate(in input);
            Assert.That(r.TargetDpi, Is.EqualTo(expectedTargetDpi).Within(1e-6f), $"{unit} 的 targetDPI 常量不符");
            Assert.That(r.ScaleFactor, Is.EqualTo(96f / expectedTargetDpi).Within(HandCalcSamples.Tolerance));
        }

        [Test]
        public void TargetDpi_DefaultUnit_IsPoints()
        {
            // 组件默认 physicalUnit == Points ⇒ 默认 targetDPI 就是 72（"为什么我什么都没改，scaleFactor 是 1.33"）
            Assert.That(ScaleCalcInput.Default.PhysicalUnit, Is.EqualTo(PhysicalUnit.Points));
            ScaleCalcResult r = ScaleCalc.Evaluate(in HandCalcSamples.Physical_FallbackDpi96);
            Assert.That(r.TargetDpi, Is.EqualTo(72f));
        }
    }
}
