using NUnit.Framework;
using UnityEngine;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// **两道写回门限**。一半验内核导出的判据（纯函数），一半用**反射等价路径**验引擎真的这么跳/写
    /// （实测已证明编辑期可稳定读回，故不需要 PlayMode）。
    /// </summary>
    public sealed class ScaleCalcWriteThresholdTests
    {
        [Test]
        public void SkipWithinThreshold()
        {
            const float prev = 1f;
            Assert.That(ScaleWriteGate.WouldSkipScale(prev + 2e-6f, prev), Is.True, "差 2e-6 < 5e-6 ⇒ 跳过");
            Assert.That(ScaleWriteGate.WouldSkipScale(prev - 2e-6f, prev), Is.True, "门限看绝对值");
            Assert.That(ScaleWriteGate.WouldSkipScale(prev, prev), Is.True, "完全相等当然跳过");
        }

        [Test]
        public void WriteBeyondThreshold()
        {
            const float prev = 1f;
            Assert.That(ScaleWriteGate.WouldSkipScale(prev + 1e-3f, prev), Is.False, "差 1e-3 > 5e-6 ⇒ 写回");
            Assert.That(ScaleWriteGate.WouldSkipScale(prev + ScaleWriteGate.ScaleWriteThreshold, prev), Is.False,
                "判据是 `<` 而不是 `<=`：正好等于门限**会写**");
        }

        [Test]
        public void ReferenceExactEquality()
        {
            Assert.That(ScaleWriteGate.ReferenceWriteUsesExactEquality, Is.True);
            Assert.That(ScaleWriteGate.WouldSkipReference(100f, 100f), Is.True, "精确相等 ⇒ 跳过");
            Assert.That(ScaleWriteGate.WouldSkipReference(100.5f, 100f), Is.False, "差一点点也要写");
            Assert.That(ScaleWriteGate.ScaleWriteThreshold, Is.EqualTo(0.000005f), "门限常量不许被改");
        }

        [Test]
        public void Engine_SetScaleFactor_BothGates_MatchKernelGate()
        {
            (GameObject go, Canvas canvas, UnityEngine.UI.CanvasScaler scaler) = ScaleCalcTestFixture.CreateScaler(
                ScaleMode.ConstantPixelSize);
            try
            {
                float prev = ScaleCalcTestFixture.GetPrivateFloat(scaler, "m_PrevScaleFactor");

                // 门限内：哨兵值应原样保留（引擎没写回）
                canvas.scaleFactor = 42f;
                ScaleCalcTestFixture.InvokeSetScaleFactor(scaler, prev + 2e-6f);
                Assert.That(canvas.scaleFactor, Is.EqualTo(42f), "门限内引擎不该写回 canvas.scaleFactor");
                Assert.That(ScaleCalcTestFixture.GetPrivateFloat(scaler, "m_PrevScaleFactor"), Is.EqualTo(prev),
                    "门限内 m_PrevScaleFactor 不该变");
                Assert.That(ScaleWriteGate.WouldSkipScale(prev + 2e-6f, prev), Is.True, "内核判据与引擎一致（跳）");

                // 门限外：写回并读得回来
                canvas.scaleFactor = 42f;
                float target = prev + 1e-3f;
                ScaleCalcTestFixture.InvokeSetScaleFactor(scaler, target);
                Assert.That(canvas.scaleFactor, Is.EqualTo(target), "门限外引擎应写回且当场可读回");
                Assert.That(ScaleCalcTestFixture.GetPrivateFloat(scaler, "m_PrevScaleFactor"), Is.EqualTo(target));
                Assert.That(ScaleWriteGate.WouldSkipScale(target, prev), Is.False, "内核判据与引擎一致（写）");
            }
            finally
            {
                ScaleCalcTestFixture.TearDown(go);
            }
        }

        [Test]
        public void Engine_SetReferencePixelsPerUnit_ExactEquality_IsSecondGate()
        {
            (GameObject go, Canvas canvas, UnityEngine.UI.CanvasScaler scaler) = ScaleCalcTestFixture.CreateScaler(
                ScaleMode.ConstantPixelSize);
            try
            {
                float prev = ScaleCalcTestFixture.GetPrivateFloat(scaler, "m_PrevReferencePixelsPerUnit");

                canvas.referencePixelsPerUnit = 42f;
                ScaleCalcTestFixture.InvokeSetReferencePixelsPerUnit(scaler, prev);
                Assert.That(canvas.referencePixelsPerUnit, Is.EqualTo(42f), "精确相等 ⇒ 不写回（哨兵保留）");

                canvas.referencePixelsPerUnit = 42f;
                float target = prev + 0.5f;
                ScaleCalcTestFixture.InvokeSetReferencePixelsPerUnit(scaler, target);
                Assert.That(canvas.referencePixelsPerUnit, Is.EqualTo(target), "不等 ⇒ 写回");
            }
            finally
            {
                ScaleCalcTestFixture.TearDown(go);
            }
        }
    }
}
