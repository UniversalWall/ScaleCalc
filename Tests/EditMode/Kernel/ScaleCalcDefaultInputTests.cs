using NUnit.Framework;
using UnityEngine;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary><see cref="ScaleCalcInput.Default"/> 与**引擎新建组件**的默认值对账，含"屏幕尺寸未设置"语义。</summary>
    public sealed class ScaleCalcDefaultInputTests
    {
        [Test]
        public void Default_MatchesEngineComponentDefaults()
        {
            (GameObject go, Canvas canvas, UnityEngine.UI.CanvasScaler scaler) = ScaleCalcTestFixture.CreateScaler(
                ScaleMode.ConstantPixelSize);
            try
            {
                ScaleCalcInput d = ScaleCalcInput.Default;

                Assert.That((int)d.Mode, Is.EqualTo((int)scaler.uiScaleMode), "默认 uiScaleMode = ConstantPixelSize");
                Assert.That((int)d.ScreenMatch, Is.EqualTo((int)scaler.screenMatchMode));
                Assert.That(d.MatchWidthOrHeight, Is.EqualTo(scaler.matchWidthOrHeight));
                Assert.That(d.ReferenceResolution.Width, Is.EqualTo(scaler.referenceResolution.x));
                Assert.That(d.ReferenceResolution.Height, Is.EqualTo(scaler.referenceResolution.y));
                Assert.That(d.ConstantScaleFactor, Is.EqualTo(scaler.scaleFactor));
                Assert.That(d.FallbackScreenDPI, Is.EqualTo(scaler.fallbackScreenDPI));
                Assert.That(d.DefaultSpriteDPI, Is.EqualTo(scaler.defaultSpriteDPI));
                Assert.That(d.ReferencePixelsPerUnit, Is.EqualTo(scaler.referencePixelsPerUnit));
                Assert.That((int)d.PhysicalUnit, Is.EqualTo((int)scaler.physicalUnit));
                Assert.That(d.DynamicPixelsPerUnit, Is.EqualTo(scaler.dynamicPixelsPerUnit));

                Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay), "默认 renderMode 与内核默认一致");
                Assert.That(d.RenderMode, Is.EqualTo(CanvasRenderMode.ScreenSpaceOverlay));
                Assert.That(d.IsRootCanvas, Is.True, "新建组件天然在根 Canvas 上 ⇒ 内核默认按「生效」算");
                Assert.That(d.TargetDisplay, Is.EqualTo(0));
            }
            finally
            {
                ScaleCalcTestFixture.TearDown(go);
            }
        }

        /// <summary>唯一的例外：<c>ScreenSize = (0,0)</c> 的语义是「**屏幕尺寸尚未设置**」——内核不替调用方猜。</summary>
        [Test]
        public void Default_ScreenSize_IsUnset_ButDoesNotProduceNaN()
        {
            Assert.That(ScaleCalcInput.Default.ScreenSize, Is.EqualTo(new ScaleSize(0f, 0f)));

            ScaleCalcResult r = ScaleCalc.Evaluate(ScaleCalcInput.Default);
            Assert.That(r.HasResult, Is.True, "默认输入不该早退");
            Assert.That(r.Branch, Is.EqualTo(ScaleBranch.ConstantPixelSize));
            Assert.That(r.ScaleFactor, Is.EqualTo(1f), "ConstantPixelSize 的 scaleFactor 与屏幕尺寸无关");
            Assert.That(r.HasCanvasSize, Is.False, "屏幕尺寸未设置 ⇒ 画布尺寸无定义（而不是 0 或 NaN）");
            Assert.That(float.IsNaN(r.CanvasSize.Width), Is.False);
        }
    }
}
