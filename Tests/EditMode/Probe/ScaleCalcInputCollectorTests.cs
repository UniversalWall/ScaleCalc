using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Wayward.ScaleCalc.Unity;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>批次 2 步 2.2：输入采集与**屏幕尺寸源决策**（`W-ScaleCalc-2`：决策留在适配层）。</summary>
    public sealed class ScaleCalcInputCollectorTests
    {
        [Test]
        public void Collect_RoundTripsEveryScalerField()
        {
            var go = new GameObject("__ScaleCalc_Tests_Collector");
            try
            {
                Canvas canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;   // ScreenSpaceCamera 无相机时会被引擎改回 Overlay，不用它做断言
                var scaler = go.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Shrink;
                scaler.matchWidthOrHeight = 0.37f;
                scaler.referenceResolution = new Vector2(1024f, 768f);
                scaler.fallbackScreenDPI = 120f;
                scaler.defaultSpriteDPI = 80f;
                scaler.referencePixelsPerUnit = 250f;
                scaler.dynamicPixelsPerUnit = 3f;
                scaler.physicalUnit = CanvasScaler.Unit.Millimeters;

                ScaleCalcInput input = ScaleInputCollector.Collect(canvas, scaler, out ScreenSizeSource source);

                Assert.That(input.IsRootCanvas, Is.EqualTo(canvas.isRootCanvas));
                Assert.That(input.RenderMode, Is.EqualTo(CanvasRenderMode.WorldSpace));
                Assert.That(input.Mode, Is.EqualTo(ScaleMode.ScaleWithScreenSize));
                Assert.That(input.ScreenMatch, Is.EqualTo(ScreenMatchMode.Shrink));
                Assert.That(input.MatchWidthOrHeight, Is.EqualTo(0.37f));
                Assert.That(input.ReferenceResolution, Is.EqualTo(new ScaleSize(1024f, 768f)));
                Assert.That(input.FallbackScreenDPI, Is.EqualTo(120f));
                Assert.That(input.DefaultSpriteDPI, Is.EqualTo(80f));
                Assert.That(input.ReferencePixelsPerUnit, Is.EqualTo(250f));
                Assert.That(input.DynamicPixelsPerUnit, Is.EqualTo(3f));
                Assert.That(input.PhysicalUnit, Is.EqualTo(PhysicalUnit.Millimeters));
                Assert.That(input.TargetDisplay, Is.EqualTo(canvas.targetDisplay));
                Assert.That(source, Is.EqualTo(ScreenSizeSource.CanvasRenderingDisplaySize));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ResolveScreenSize_MainDisplay_UsesRenderingDisplaySize()
        {
            var go = new GameObject("__ScaleCalc_Tests_ScreenSource");
            try
            {
                Canvas canvas = go.AddComponent<Canvas>();
                canvas.targetDisplay = 0;

                ScaleSize size = ScaleInputCollector.ResolveScreenSize(canvas, out ScreenSizeSource source);
                Vector2 engine = canvas.renderingDisplaySize;

                Assert.That(source, Is.EqualTo(ScreenSizeSource.CanvasRenderingDisplaySize),
                    "主 Display 必须走 renderingDisplaySize（引擎 HandleScaleWithScreenSize 的主路径）");
                Assert.That(size.Width, Is.EqualTo(engine.x), "屏幕尺寸必须逐位等于引擎读数");
                Assert.That(size.Height, Is.EqualTo(engine.y));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ResolveScreenSize_DisplayIndexOutOfRange_FallsBackToCanvas()
        {
            var go = new GameObject("__ScaleCalc_Tests_ScreenSource2");
            try
            {
                Canvas canvas = go.AddComponent<Canvas>();
                canvas.targetDisplay = 99;      // 任何真实机器都不会有 100 个 Display

                ScaleInputCollector.ResolveScreenSize(canvas, out ScreenSizeSource source);

                Assert.That(source, Is.EqualTo(ScreenSizeSource.CanvasRenderingDisplaySize),
                    "越界的 targetDisplay 必须回落，不许越界访问 Display.displays");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Collect_NullInputs_DoesNotThrow()
        {
            ScaleCalcInput input = ScaleInputCollector.Collect(null, null, out ScreenSizeSource source);
            Assert.That(source, Is.EqualTo(ScreenSizeSource.CallerOverride));
            Assert.That(input.ScreenSize, Is.EqualTo(new ScaleSize(0f, 0f)), "取不到就保持「未设置」语义，不编数");
        }

        /// <summary>枚举映射是**双向恒等**（数值对齐 ⇒ 引擎改顺序会立刻炸，不会悄悄错）。</summary>
        [Test]
        public void EnumMap_IsIdentityBothWays()
        {
            Assert.That(ScaleEnumMap.ToCore(RenderMode.WorldSpace), Is.EqualTo(CanvasRenderMode.WorldSpace));
            Assert.That(ScaleEnumMap.ToCore(RenderMode.ScreenSpaceCamera), Is.EqualTo(CanvasRenderMode.ScreenSpaceCamera));
            Assert.That(ScaleEnumMap.ToCore(RenderMode.ScreenSpaceOverlay), Is.EqualTo(CanvasRenderMode.ScreenSpaceOverlay));
            Assert.That((int)ScaleEnumMap.ToEngine(CanvasRenderMode.WorldSpace), Is.EqualTo((int)RenderMode.WorldSpace));

            Assert.That((int)ScaleEnumMap.ToCore(CanvasScaler.ScaleMode.ConstantPhysicalSize),
                Is.EqualTo((int)ScaleMode.ConstantPhysicalSize));
            Assert.That((int)ScaleEnumMap.ToCore(CanvasScaler.ScreenMatchMode.Shrink),
                Is.EqualTo((int)ScreenMatchMode.Shrink));
            Assert.That((int)ScaleEnumMap.ToCore(CanvasScaler.Unit.Picas), Is.EqualTo((int)PhysicalUnit.Picas));
            Assert.That((int)ScaleEnumMap.ToEngine(PhysicalUnit.Centimeters), Is.EqualTo((int)CanvasScaler.Unit.Centimeters));
        }
    }
}
