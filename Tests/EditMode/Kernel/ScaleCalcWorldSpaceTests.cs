using NUnit.Framework;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>世界空间分支（唯一"不需要渲染就能对账"的早退）。</summary>
    public sealed class ScaleCalcWorldSpaceTests
    {
        [Test]
        public void WorldSpace_UsesDynamicPixelsPerUnit_AndHasNoCanvasSize()
        {
            ScaleCalcInput input = HandCalcSamples.WithScreen(
                new ScaleSize(1920f, 1080f), new ScaleSize(1920f, 1080f), ScreenMatchMode.Expand, 0f);
            input.RenderMode = CanvasRenderMode.WorldSpace;
            input.DynamicPixelsPerUnit = 2.5f;

            ScaleCalcResult r = ScaleCalc.Evaluate(in input);

            Assert.That(r.Branch, Is.EqualTo(ScaleBranch.WorldSpace));
            Assert.That(r.HasResult, Is.True, "世界空间是「能对账」的早退，不是不生效");
            Assert.That(r.ScaleFactor, Is.EqualTo(2.5f), "= SetScaleFactor(m_DynamicPixelsPerUnit)");
            Assert.That(r.ReferencePixelsPerUnit, Is.EqualTo(100f), "仍会写 refPPU");
            Assert.That(r.HasCanvasSize, Is.False, "世界空间不产出画布尺寸");
            Assert.That(r.IsFinite, Is.True);
        }

        [Test]
        public void WorldSpace_IgnoresScreenMode()
        {
            // 世界空间下 uiScaleMode 根本不参与 ⇒ 换成 ScaleWithScreenSize 结果一样
            ScaleCalcInput a = ScaleCalcInput.Default;
            a.RenderMode = CanvasRenderMode.WorldSpace;
            a.Mode = ScaleMode.ScaleWithScreenSize;
            a.ScreenSize = new ScaleSize(3840f, 2160f);

            ScaleCalcInput b = a;
            b.Mode = ScaleMode.ConstantPhysicalSize;

            Assert.That(ScaleCalc.Evaluate(in a).ScaleFactor, Is.EqualTo(ScaleCalc.Evaluate(in b).ScaleFactor));
        }
    }
}
