using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Wayward.ScaleCalc.Unity;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// 判定模型本身——<c>Ok</c> / <c>WriteSkippedButOverwritten</c> / <c>Mismatch</c>，以及
    /// "**精度降级是并列标志位、不是判定值**"。
    /// <para>后半（优先级与稳定帧）在 <see cref="ScaleCalcProbePriorityTests"/>。</para>
    /// </summary>
    public sealed class ScaleCalcProbeVerdictTests : ScaleCalcProbeTestBase
    {
        [Test]
        public void Verdict_Ok_WhenKernelMatchesTruth()
        {
            ProbeReport r = Probe.Capture(SampleTiming.Manual);

            Assert.That(r.ReflectionAvailable, Is.True, "本版本两个 m_Prev* 字段都存在");
            Assert.That(r.Verdict, Is.EqualTo(ProbeVerdict.Ok));
            Assert.That(r.Kernel.HasResult, Is.True);
            Assert.That(r.Kernel.ScaleFactor, Is.EqualTo(TargetCanvas.scaleFactor), "内核目标值 = 引擎当前值");
            Assert.That(r.DeltaScaleFactor, Is.EqualTo(0f));
            Assert.That(r.ComponentEnabled, Is.True);
        }

        /// <summary>三号坑：引擎跳过写回、但画布上的值不是它上次写的 ⇒ 有人手改，**不许报成 Mismatch**。</summary>
        [Test]
        public void Verdict_WriteSkippedButOverwritten_IsNotMismatch()
        {
            TargetCanvas.scaleFactor = 42f;      // 手改：真值 42，而 m_PrevScaleFactor 仍是 1、内核目标值也是 1

            ProbeReport r = Probe.Capture(SampleTiming.Manual);

            Assert.That(r.Verdict, Is.EqualTo(ProbeVerdict.WriteSkippedButOverwritten),
                "门限跳过 + 真值 != 上次写回值 ⇒ 判「有人手改」，而不是「内核算错」");
            Assert.That(r.Verdict, Is.Not.EqualTo(ProbeVerdict.Mismatch), "**优先级**：不许被报成对账失败");
            Assert.That(r.DeltaScaleFactor, Is.EqualTo(-41f), "差值本身很大（-41 = 内核 1 − 真值 42），但判定不是 Mismatch");
            Assert.That(r.ReflectionAvailable, Is.True);
        }

        [Test]
        public void Verdict_Mismatch_WhenWriteGateDoesNotSkip()
        {
            EngineSetScaleFactor(3f);            // 把 m_PrevScaleFactor 推远 ⇒ 门限**不**跳过 ⇒ 才轮到比差
            TargetCanvas.scaleFactor = 5f;       // 真值 5，内核目标 1，上次写回值 3

            ProbeReport r = Probe.Capture(SampleTiming.Manual);

            Assert.That(r.PrevScaleFactor, Is.EqualTo(3f), "m_Prev* 已被推远");
            Assert.That(ScaleWriteGate.WouldSkipScale(r.Kernel.ScaleFactor, r.PrevScaleFactor), Is.False);
            Assert.That(r.Verdict, Is.EqualTo(ProbeVerdict.Mismatch));
            Assert.That(System.Math.Abs(r.DeltaScaleFactor), Is.GreaterThan(1e-3f));
        }

        [Test]
        public void Verdict_ReflectionDegraded_IsParallelFlag_AndVerdictCanStillBeOk()
        {
            Probe.OverridePrevFields("no_such_field_scale", "no_such_field_ppu");   // 人为触发降级分支

            ProbeReport r = Probe.Capture(SampleTiming.Manual);

            Assert.That(r.ReflectionAvailable, Is.False, "两个字段都读不到 ⇒ 降级");
            Assert.That(r.DegradedFields, Is.Not.Empty, "降级必须留痕（写清是哪个字段）");
            Assert.That(r.Verdict, Is.EqualTo(ProbeVerdict.Ok),
                "降级是**并列标志位**：它不该把「对账通过」变成失败");
        }

        [Test]
        public void Verdict_ReflectionDegraded_DoesNotSuppressMismatch()
        {
            Probe.OverridePrevFields("no_such_field_scale", "no_such_field_ppu");
            TargetCanvas.scaleFactor = 42f;

            ProbeReport r = Probe.Capture(SampleTiming.Manual);

            Assert.That(r.ReflectionAvailable, Is.False);
            Assert.That(r.Verdict, Is.EqualTo(ProbeVerdict.Mismatch),
                "降级时无法判「是否被手改」⇒ 退回比差；降级与「对账失败」可以同时成立");
        }

        [Test]
        public void Verdict_DegradedFields_NamesTheMissingOne()
        {
            Probe.OverridePrevFields("m_PrevScaleFactor", "no_such_field_ppu");      // 只缺一个

            ProbeReport r = Probe.Capture(SampleTiming.Manual);

            Assert.That(r.ReflectionAvailable, Is.False);
            Assert.That(r.DegradedFields, Is.EqualTo("m_PrevReferencePixelsPerUnit"),
                "只缺 refPPU 那个字段时，报告必须**只**报它（两个字段各自独立降级）");
        }

        [Test]
        public void ProbeText_CarriesSampleInputAndVerdict()
        {
            ProbeReport r = Probe.Capture(SampleTiming.Manual);
            string text = ProbeText.Format(in r);

            Assert.That(text, Does.Contain("判定="), "文本层必须能被 grep（一行一件事）");
            Assert.That(text, Does.Contain("输入"));
            Assert.That(text, Does.Contain("内核"));
            Assert.That(text, Does.Contain("真值"));
            Assert.That(text, Does.Contain("门限"));
            Assert.That(text, Does.Contain("反射=OK"));
        }
    }
}
