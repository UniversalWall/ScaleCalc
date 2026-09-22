using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Wayward.ScaleCalc.Unity;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// 批次 3 步 3.8：**三个真实 Canvas 各对账一次**（内核 vs 引擎真值），按 `W-ScaleCalc-13` 分档：
    /// <list type="bullet">
    /// <item><c>ConstantPixelSize</c> / <c>ConstantPhysicalSize</c> **不需要渲染** ⇒ 走 EditMode（本文件）；</item>
    /// <item><c>ScaleWithScreenSize</c> **需要渲染** ⇒ 走现场协议（`Evidence/ScaleCalc-002-现场对账.md`，三项逐位）。</item>
    /// </list>
    /// </summary>
    public sealed class ScaleCalcRealCanvasTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        private static (GameObject go, Canvas canvas, CanvasScaler scaler) CreateReal(ScaleMode mode)
        {
            var go = new GameObject("__ScaleCalc_Tests_RealCanvas");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = (CanvasScaler.ScaleMode)(int)mode;
            return (go, canvas, scaler);
        }

        private static void InvokeHandle(CanvasScaler scaler) =>
            typeof(CanvasScaler).GetMethod("Handle", Private).Invoke(scaler, null);

        [Test]
        public void ConstantPixelSize_RealCanvas_MatchesKernel()
        {
            (GameObject go, Canvas canvas, CanvasScaler scaler) = CreateReal(ScaleMode.ConstantPixelSize);
            try
            {
                scaler.scaleFactor = 2.25f;
                scaler.referencePixelsPerUnit = 137f;
                canvas.scaleFactor = 1f;                 // 故意先放一个旧值，逼 Handle() 真的写回
                InvokeHandle(scaler);

                ScaleCalcInput input = ScaleInputCollector.Collect(canvas, scaler);
                ScaleCalcResult kernel = ScaleCalc.Evaluate(in input);

                Assert.That(kernel.Branch, Is.EqualTo(ScaleBranch.ConstantPixelSize));
                Assert.That(kernel.ScaleFactor, Is.EqualTo(canvas.scaleFactor),
                    "常量像素模式：内核 scaleFactor 应逐位等于引擎真值");
                Assert.That(kernel.ReferencePixelsPerUnit, Is.EqualTo(canvas.referencePixelsPerUnit));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ConstantPhysicalSize_RealCanvas_MatchesKernel()
        {
            (GameObject go, Canvas canvas, CanvasScaler scaler) = CreateReal(ScaleMode.ConstantPhysicalSize);
            try
            {
                scaler.physicalUnit = CanvasScaler.Unit.Points;
                scaler.fallbackScreenDPI = 96f;
                scaler.defaultSpriteDPI = 96f;
                scaler.referencePixelsPerUnit = 100f;
                scaler.scaleFactor = 1f;                  // 让第一道门限不跳过
                canvas.referencePixelsPerUnit = 100f;     // 让第二道门限不跳过
                InvokeHandle(scaler);

                ScaleCalcInput input = ScaleInputCollector.Collect(canvas, scaler);
                ScaleCalcResult kernel = ScaleCalc.Evaluate(in input);

                Assert.That(kernel.Branch, Is.EqualTo(ScaleBranch.ConstantPhysicalSize));
                Assert.That(input.ScreenDpi, Is.EqualTo(Screen.dpi), "物理模式的 dpi 直接来自 Screen.dpi（编辑器读数不可信，但内核照用）");
                Assert.That(kernel.ScaleFactor, Is.EqualTo(canvas.scaleFactor),
                    "物理模式：内核 scaleFactor 应逐位等于引擎真值（兜底与否都一样）");
                Assert.That(kernel.ReferencePixelsPerUnit, Is.EqualTo(canvas.referencePixelsPerUnit),
                    "物理模式也会改写 refPPU：内核必须同样改写");
                Assert.That(kernel.UsedFallbackDpi, Is.EqualTo(input.ScreenDpi == 0f),
                    "兜底标志必须如实反映「Screen.dpi 是否为 0」");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void WorldSpace_RealCanvas_MatchesKernel_WithoutRendering()
        {
            (GameObject go, Canvas canvas, CanvasScaler scaler) = CreateReal(ScaleMode.ScaleWithScreenSize);
            try
            {
                canvas.renderMode = RenderMode.WorldSpace;      // 早退②：世界空间不读渲染尺寸
                scaler.dynamicPixelsPerUnit = 3.5f;
                canvas.scaleFactor = 1f;
                InvokeHandle(scaler);

                ScaleCalcInput input = ScaleInputCollector.Collect(canvas, scaler);
                ScaleCalcResult kernel = ScaleCalc.Evaluate(in input);

                Assert.That(kernel.Branch, Is.EqualTo(ScaleBranch.WorldSpace));
                Assert.That(kernel.ScaleFactor, Is.EqualTo(canvas.scaleFactor),
                    "世界空间：内核 = dynamicPixelsPerUnit，且不需要渲染就能对账");
                Assert.That(kernel.HasCanvasSize, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
