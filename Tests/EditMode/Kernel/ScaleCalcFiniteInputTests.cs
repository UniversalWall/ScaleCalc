using NUnit.Framework;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>**任何输入下两个产出字段都有限**；屏幕尺寸非正 / <c>scaleFactor == 0</c> ⇒ 画布尺寸无定义且不产 <c>NaN</c>。</summary>
    public sealed class ScaleCalcFiniteInputTests
    {
        private static readonly ScaleSize[] Screens =
        {
            new ScaleSize(0f, 0f),
            new ScaleSize(-1920f, 1080f),
            new ScaleSize(1920f, -1080f),
            new ScaleSize(0f, 1080f),
            new ScaleSize(1f, 1f),
            new ScaleSize(3840f, 2160f),
        };

        private static readonly ScaleMode[] Modes =
        {
            ScaleMode.ConstantPixelSize, ScaleMode.ScaleWithScreenSize, ScaleMode.ConstantPhysicalSize,
        };

        [Test]
        public void ProducedFields_AreAlwaysFinite()
        {
            foreach (ScaleMode mode in Modes)
            {
                foreach (ScaleSize screen in Screens)
                {
                    ScaleCalcInput input = ScaleCalcInput.Default;
                    input.Mode = mode;
                    input.ScreenSize = screen;

                    ScaleCalcResult r = ScaleCalc.Evaluate(in input);
                    Assert.That(r.IsFinite, Is.True, $"{mode} / {screen}：IsFinite 必须为真");
                    Assert.That(float.IsNaN(r.ScaleFactor), Is.False, $"{mode} / {screen}：ScaleFactor 不许是 NaN");
                    Assert.That(float.IsInfinity(r.ScaleFactor), Is.False, $"{mode} / {screen}：ScaleFactor 不许是 ∞");
                    Assert.That(float.IsNaN(r.ReferencePixelsPerUnit), Is.False);
                    Assert.That(float.IsInfinity(r.ReferencePixelsPerUnit), Is.False);
                }
            }
        }

        [Test]
        public void NonPositiveScreenSize_HasNoCanvasSize_AndNoNaN()
        {
            foreach (ScaleSize screen in Screens)
            {
                bool positive = screen.Width > 0f && screen.Height > 0f;
                ScaleCalcInput input = HandCalcSamples.WithScreen(
                    new ScaleSize(1920f, 1080f), screen, ScreenMatchMode.MatchWidthOrHeight, 0.5f);

                ScaleCalcResult r = ScaleCalc.Evaluate(in input);
                if (!positive)
                {
                    Assert.That(r.HasCanvasSize, Is.False, $"{screen}：屏幕尺寸非正 ⇒ 画布尺寸无定义");
                    Assert.That(float.IsNaN(r.CanvasSize.Width), Is.False, "无定义时不许塞 NaN 进去");
                    Assert.That(float.IsNaN(r.CanvasSize.Height), Is.False);
                }
                else
                {
                    Assert.That(r.HasCanvasSize, Is.True, $"{screen}：屏幕尺寸为正 ⇒ 画布尺寸有定义");
                }
            }
        }

        /// <summary>
        /// 比值非正 ⇒ <c>log</c> 域外。引擎在这一角会算出 <c>NaN</c> 并写进 <c>Canvas.scaleFactor</c>；
        /// 内核同口径**不产 NaN**（产出 <c>0</c> = "未产出有效缩放"）。
        /// </summary>
        [Test]
        public void NonPositiveScreenSize_LogAverage_ProducesZeroNotNaN()
        {
            ScaleCalcInput zero = HandCalcSamples.WithScreen(
                new ScaleSize(1920f, 1080f), new ScaleSize(0f, 0f), ScreenMatchMode.MatchWidthOrHeight, 0.5f);
            ScaleCalcResult rZero = ScaleCalc.Evaluate(in zero);
            Assert.That(rZero.ScaleFactor, Is.EqualTo(0f), "0×0 + 对数平均：内核产出 0，不是 NaN");
            Assert.That(rZero.IsFinite, Is.True);
            Assert.That(rZero.HasCanvasSize, Is.False);

            ScaleCalcInput oneAxisZero = HandCalcSamples.WithScreen(
                new ScaleSize(1920f, 1080f), new ScaleSize(1440f, 0f), ScreenMatchMode.MatchWidthOrHeight, 0.5f);
            Assert.That(ScaleCalc.Evaluate(in oneAxisZero).ScaleFactor, Is.EqualTo(0f), "单轴为 0 同样守");
        }
    }
}
