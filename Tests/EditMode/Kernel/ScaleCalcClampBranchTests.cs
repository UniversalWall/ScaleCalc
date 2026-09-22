using NUnit.Framework;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>三处夹取 + <see cref="ScaleBranch"/> 四支；恒有限 + 非正屏尺寸不产 <c>NaN</c>。</summary>
    public sealed class ScaleCalcClampBranchTests
    {
        [Test]
        public void ReferenceResolution_Zero_ClampsToEpsilon()
        {
            ScaleCalcInput input = HandCalcSamples.WithScreen(
                new ScaleSize(0f, 0f), new ScaleSize(1920f, 1080f), ScreenMatchMode.Expand, 0f);

            ScaleCalcResult r = ScaleCalc.Evaluate(in input);
            Assert.That(r.ClampedReferenceResolution, Is.True, "参考分辨率被夹过就要如实标记");
            Assert.That(r.IsFinite, Is.True, "夹取后必须有限（否则 Log(0) 会产 -∞）");
            // 参考分辨率被夹成 1e-5 ⇒ ratioH = 1080/1e-5 = 1.08e8，Expand 取小 ⇒ 也是这个量级
            Assert.That(r.ScaleFactor, Is.EqualTo(1080f / HandCalcSamples.ClampedResolutionComponent).Within(16f),
                "1e-5 级别的参考分辨率下 ratio 应为 1.08e8（float 精度内）");
        }

        [Test]
        public void ReferenceResolution_Negative_ClampsToEpsilon()
        {
            ScaleCalcInput input = HandCalcSamples.WithScreen(
                new ScaleSize(-100f, 1080f), new ScaleSize(1920f, 1080f), ScreenMatchMode.Expand, 0f);

            ScaleCalcResult r = ScaleCalc.Evaluate(in input);
            Assert.That(r.ClampedReferenceResolution, Is.True);
            Assert.That(r.IsFinite, Is.True, "负参考分辨率必须被兜底，不许产 NaN");
            Assert.That(r.ScaleFactor, Is.GreaterThan(0f));
        }

        [Test]
        public void ConstantScaleFactor_BelowMinimum_ClampsTo001()
        {
            ScaleCalcInput input = ScaleCalcInput.Default;      // ConstantPixelSize
            input.ConstantScaleFactor = 0.001f;
            input.ScreenSize = new ScaleSize(1920f, 1080f);

            ScaleCalcResult r = ScaleCalc.Evaluate(in input);
            Assert.That(r.Branch, Is.EqualTo(ScaleBranch.ConstantPixelSize));
            Assert.That(r.ClampedScaleFactor, Is.True);
            Assert.That(r.ScaleFactor, Is.EqualTo(HandCalcSamples.ClampedConstantScaleFactor));
        }

        [Test]
        public void DefaultSpriteDpi_BelowOne_ClampsToOne()
        {
            ScaleCalcInput input = HandCalcSamples.WithPhysical(new ScaleSize(800f, 600f), 96f);
            input.DefaultSpriteDPI = 0.5f;

            ScaleCalcResult r = ScaleCalc.Evaluate(in input);
            Assert.That(r.ClampedDefaultSpriteDPI, Is.True);
            Assert.That(r.ReferencePixelsPerUnit, Is.EqualTo(100f * 72f / HandCalcSamples.ClampedDefaultSpriteDpi)
                .Within(HandCalcSamples.Tolerance), "夹到 1 之后 refPPU = 100*72/1 = 7200");
        }

        [Test]
        public void NoClamp_WhenValuesAreLegal()
        {
            ScaleCalcResult r = ScaleCalc.Evaluate(in HandCalcSamples.MatchHalf_1440x3120);
            Assert.That(r.ClampedReferenceResolution, Is.False);
            Assert.That(r.ClampedScaleFactor, Is.False);
            Assert.That(r.ClampedDefaultSpriteDPI, Is.False);
        }

        [Test]
        public void Branch_CoversAllFourNonEarlyExitPaths()
        {
            Assert.That(ScaleCalc.Evaluate(in HandCalcSamples.MatchHalf_1440x3120).Branch,
                Is.EqualTo(ScaleBranch.ScaleWithScreenSize));

            ScaleCalcInput pixel = ScaleCalcInput.Default;
            pixel.ScreenSize = new ScaleSize(1920f, 1080f);
            Assert.That(ScaleCalc.Evaluate(in pixel).Branch, Is.EqualTo(ScaleBranch.ConstantPixelSize));

            Assert.That(ScaleCalc.Evaluate(in HandCalcSamples.Physical_FallbackDpi96).Branch,
                Is.EqualTo(ScaleBranch.ConstantPhysicalSize));

            ScaleCalcInput world = ScaleCalcInput.Default;
            world.RenderMode = CanvasRenderMode.WorldSpace;
            Assert.That(ScaleCalc.Evaluate(in world).Branch, Is.EqualTo(ScaleBranch.WorldSpace));
        }
    }
}
