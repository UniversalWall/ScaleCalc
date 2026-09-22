using NUnit.Framework;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>早退（<c>HasResult == false</c>）时的字段语义表逐项断言。</summary>
    public sealed class ScaleCalcEarlyExitTests
    {
        private static ScaleCalcInput ChildCanvas()
        {
            ScaleCalcInput input = ScaleCalcInput.Default;
            input.IsRootCanvas = false;
            input.ScreenSize = new ScaleSize(1920f, 1080f);
            return input;
        }

        [Test]
        public void EarlyExit_HasResultFalse_AndZeroedOutputs()
        {
            ScaleCalcResult r = ScaleCalc.Evaluate(ChildCanvas());

            Assert.That(r.Branch, Is.EqualTo(ScaleBranch.EarlyExit_NotRootCanvas));
            Assert.That(r.HasResult, Is.False, "子 Canvas 上本组件不生效 ⇒ 内核不产出可对账的数字");
            Assert.That(r.ScaleFactor, Is.EqualTo(0f), "无意义占位（探针不做比差）");
            Assert.That(r.ReferencePixelsPerUnit, Is.EqualTo(0f));
            Assert.That(r.HasCanvasSize, Is.False);
            Assert.That(r.CanvasSize, Is.EqualTo(default(ScaleSize)));
        }

        [Test]
        public void EarlyExit_IsFiniteIsVacuouslyTrue()
        {
            ScaleCalcResult r = ScaleCalc.Evaluate(ChildCanvas());
            Assert.That(r.IsFinite, Is.True, "两个产出字段「未产出」 ⇒ 真空真");
        }

        [Test]
        public void EarlyExit_ClampDiagnostics_AreFalse()
        {
            ScaleCalcInput input = ChildCanvas();
            input.ReferenceResolution = new ScaleSize(0f, -5f);   // 就算输入非法，早退也不报夹取位
            input.ConstantScaleFactor = 0.0001f;
            input.DefaultSpriteDPI = 0.1f;

            ScaleCalcResult r = ScaleCalc.Evaluate(in input);
            Assert.That(r.ClampedReferenceResolution, Is.False);
            Assert.That(r.ClampedScaleFactor, Is.False);
            Assert.That(r.ClampedDefaultSpriteDPI, Is.False);
        }

        [Test]
        public void EarlyExit_BeforeWorldSpaceAndModes()
        {
            // 早退①优先于一切：子 Canvas + WorldSpace 仍然是"不生效"
            ScaleCalcInput input = ChildCanvas();
            input.RenderMode = CanvasRenderMode.WorldSpace;
            Assert.That(ScaleCalc.Evaluate(in input).Branch, Is.EqualTo(ScaleBranch.EarlyExit_NotRootCanvas));
        }

        [Test]
        public void EarlyExit_EffectiveDpiAndTargetDpi_AreZero()
        {
            ScaleCalcResult r = ScaleCalc.Evaluate(ChildCanvas());
            Assert.That(r.UsedFallbackDpi, Is.False);
            Assert.That(r.EffectiveDpi, Is.EqualTo(0f));
            Assert.That(r.TargetDpi, Is.EqualTo(0f));
        }
    }
}
