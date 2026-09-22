using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Wayward.ScaleCalc.Unity;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// **判定优先级短路**与**稳定帧第三条件**。
    /// <para>优先级顺序：<c>EarlyExit → TruthNotFinite → WriteSkippedButOverwritten → Mismatch → Ok</c>。</para>
    /// </summary>
    public sealed class ScaleCalcProbePriorityTests : ScaleCalcProbeTestBase
    {
        /// <summary>优先级 1 vs 2：早退压过"真值非有限"。</summary>
        [Test]
        public void Verdict_Priority_EarlyExitWinsOverTruthNotFinite()
        {
            var child = new GameObject("__ScaleCalc_Tests_ChildCanvas");
            child.transform.SetParent(CanvasGo.transform, false);            // 先挂到根 Canvas 之下
            try
            {
                var childCanvas = child.AddComponent<Canvas>();              // 现在是**子** Canvas ⇒ isRootCanvas == false
                var childScaler = child.AddComponent<CanvasScaler>();
                childScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                childCanvas.scaleFactor = float.PositiveInfinity;             // 同时满足"真值非有限"

                var probeGo = new GameObject("__ScaleCalc_Tests_ChildProbe");
                var probe = probeGo.AddComponent<ScaleFactorProbe>();
                try
                {
                    probe.Bind(childCanvas, childScaler);
                    ProbeReport r = probe.Capture(SampleTiming.Manual);

                    Assert.That(r.Kernel.HasResult, Is.False, "子 Canvas 上的缩放器不生效");
                    Assert.That(r.Verdict, Is.EqualTo(ProbeVerdict.EarlyExit), "早退优先级最高（压过真值非有限）");
                    Assert.That(r.EarlyExitHasInheritedReading, Is.True);
                    Assert.That(r.InheritedScaleFactor, Is.EqualTo(TargetCanvas.scaleFactor),
                        "早退时打印的是**根 Canvas 的读数**（说明性，不参与比差）");
                }
                finally
                {
                    Object.DestroyImmediate(probeGo);
                }
            }
            finally
            {
                Object.DestroyImmediate(child);
            }
        }

        /// <summary>优先级 2：引擎真值非有限（<c>∞</c>/<c>NaN</c>）——只有"Inspector 绕开夹取 / 屏幕尺寸非正"这类输入才会走到。
        /// <para>顺带实测记档：<c>Canvas.scaleFactor</c> **会把 <c>NaN</c> 清洗成 <c>1</c>**（<c>+∞</c> 却照存），
        /// 而 <c>Canvas.referencePixelsPerUnit</c> 连 <c>NaN</c> 都照存——所以两条路都要能判。</para>
        /// </summary>
        [Test]
        public void Verdict_TruthNotFinite_WhenEngineValueIsNotFinite()
        {
            TargetCanvas.scaleFactor = float.NaN;
            Assert.That(float.IsNaN(TargetCanvas.scaleFactor), Is.False,
                "实测：Canvas.scaleFactor 对 NaN 会清洗成 1（引擎自己的行为，不是我们测错）");

            TargetCanvas.scaleFactor = float.PositiveInfinity;
            ProbeReport infinite = Probe.Capture(SampleTiming.Manual);
            Assert.That(infinite.Verdict, Is.EqualTo(ProbeVerdict.TruthNotFinite), "+∞ 照存 ⇒ 判「真值非有限」");
            Assert.That(infinite.Kernel.HasResult, Is.True, "内核照常产出（它不知道引擎那侧坏了）");
            Assert.That(infinite.Kernel.IsFinite, Is.True, "内核产出仍然有限 ⇒ 这正是「合法差异」");
            Assert.That(infinite.Verdict, Is.Not.EqualTo(ProbeVerdict.WriteSkippedButOverwritten),
                "**优先级**：真值非有限也压过「跳过写回但被覆写」");

            TargetCanvas.scaleFactor = 1f;
            TargetCanvas.referencePixelsPerUnit = float.NaN;
            ProbeReport nanPpu = Probe.Capture(SampleTiming.Manual);
            Assert.That(nanPpu.Verdict, Is.EqualTo(ProbeVerdict.TruthNotFinite), "refPPU 的 NaN 照存 ⇒ 同样判非有限");
        }

        [Test]
        public void IsStable_RequiresEnabledComponent()
        {
            ProbeReport first = Probe.Capture(SampleTiming.Manual);
            ProbeReport second = Probe.Capture(SampleTiming.Manual);
            Assert.That(Probe.IsStable(in first, in second), Is.True, "读数与 m_Prev 都对齐 ⇒ 稳定");

            TargetScaler.enabled = false;        // OnDisable 会把两个字段复位成 1 / 100
            ProbeReport disabled = Probe.Capture(SampleTiming.Manual);
            Assert.That(disabled.ComponentEnabled, Is.False, "组件状态必须进报告");
            Assert.That(Probe.IsStable(in second, in disabled), Is.False,
                "禁用后「读数不变」是**假性稳定**，第三条件专门防它");
        }

        [Test]
        public void IsStable_RequiresPrevFieldsToMatchTarget()
        {
            EngineSetScaleFactor(3f);            // m_Prev* 与内核目标值不一致 ⇒ 不算稳定

            ProbeReport first = Probe.Capture(SampleTiming.Manual);
            ProbeReport second = Probe.Capture(SampleTiming.Manual);

            Assert.That(Probe.IsStable(in first, in second), Is.False,
                "第二条件：两个 m_Prev* 必须已等于目标值（否则「读数稳定」只是还没写回）");
        }
    }
}
