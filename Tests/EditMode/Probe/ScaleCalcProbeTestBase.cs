using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Wayward.ScaleCalc.Unity;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// 探针类断言的公共夹具（拆文件判据 = 200 行红线）。
    /// <para>刻意用 `ConstantPixelSize`：它**不依赖渲染尺寸**，所以判定与门限能在 EditMode 里稳定复现。</para>
    /// </summary>
    public abstract class ScaleCalcProbeTestBase
    {
        protected GameObject CanvasGo;
        protected Canvas TargetCanvas;
        protected CanvasScaler TargetScaler;
        protected GameObject ProbeGo;
        protected ScaleFactorProbe Probe;

        [SetUp]
        public void SetUp()
        {
            CanvasGo = new GameObject("__ScaleCalc_Tests_Probe");
            TargetCanvas = CanvasGo.AddComponent<Canvas>();
            TargetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            TargetScaler = CanvasGo.AddComponent<CanvasScaler>();
            TargetScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            ProbeGo = new GameObject("__ScaleCalc_Tests_ProbeComponent");
            Probe = ProbeGo.AddComponent<ScaleFactorProbe>();
            Probe.Bind(TargetCanvas, TargetScaler);
        }

        [TearDown]
        public void TearDown()
        {
            if (ProbeGo != null) Object.DestroyImmediate(ProbeGo);
            if (CanvasGo != null) Object.DestroyImmediate(CanvasGo);
        }

        /// <summary>反射调引擎的 <c>protected SetScaleFactor</c>（把 <c>m_PrevScaleFactor</c> 推到位）。</summary>
        protected void EngineSetScaleFactor(float value) =>
            typeof(CanvasScaler)
                .GetMethod("SetScaleFactor", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(TargetScaler, new object[] { value });
    }
}
