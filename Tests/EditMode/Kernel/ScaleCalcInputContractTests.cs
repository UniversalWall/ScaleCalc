using NUnit.Framework;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary><c>Evaluate</c> **不修改调用方传入的输入**。</summary>
    public sealed class ScaleCalcInputContractTests
    {
        [Test]
        public void Evaluate_DoesNotMutateInput()
        {
            ScaleCalcInput input = HandCalcSamples.WithScreen(
                new ScaleSize(0f, -10f),                       // 会被夹的两个分量
                new ScaleSize(1440f, 3120f),
                ScreenMatchMode.MatchWidthOrHeight,
                3f);                                           // match 越界（引擎在 Lerp 内夹、内核在 Normalize 夹）
            input.ConstantScaleFactor = 0.001f;                // 会被夹
            input.DefaultSpriteDPI = 0.5f;                     // 会被夹
            input.Mode = ScaleMode.ConstantPhysicalSize;
            input.ScreenDpi = 0f;

            ScaleCalcInput before = input;
            ScaleCalcResult r = ScaleCalc.Evaluate(in input);

            Assert.That(r.ClampedReferenceResolution, Is.True, "先确认这次调用真的触发了夹取");
            AssertTouched(before.ReferenceResolution, input.ReferenceResolution, "ReferenceResolution");
            AssertTouched(before.ConstantScaleFactor, input.ConstantScaleFactor, "ConstantScaleFactor");
            AssertTouched(before.DefaultSpriteDPI, input.DefaultSpriteDPI, "DefaultSpriteDPI");
            AssertTouched(before.MatchWidthOrHeight, input.MatchWidthOrHeight, "MatchWidthOrHeight");
            Assert.That(input.Mode, Is.EqualTo(before.Mode));
            Assert.That(input.ScreenSize, Is.EqualTo(before.ScreenSize));
            Assert.That(input.ScreenDpi, Is.EqualTo(before.ScreenDpi));
            Assert.That(input.IsRootCanvas, Is.EqualTo(before.IsRootCanvas));
            Assert.That(input.RenderMode, Is.EqualTo(before.RenderMode));
            Assert.That(input.ReferencePixelsPerUnit, Is.EqualTo(before.ReferencePixelsPerUnit));
        }

        private static void AssertTouched(ScaleSize before, ScaleSize after, string name) =>
            Assert.That(after, Is.EqualTo(before), $"Evaluate 不该改调用方输入的 {name}");

        private static void AssertTouched(float before, float after, string name) =>
            Assert.That(after, Is.EqualTo(before), $"Evaluate 不该改调用方输入的 {name}");
    }
}
