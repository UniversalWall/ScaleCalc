using System;

namespace Wayward.ScaleCalc
{
    /// <summary>
    /// **两道写回门限的单点导出**。
    /// <para>探针、窗口与断言都从这里取数，**别处不许重抄字面量**——两个数是有区别的，
    /// 混用会让"引擎到底写没写回"判错：<c>0.000005</c> = 第一道写回门限（带 epsilon）· 精确相等 = 第二道门限。</para>
    /// <para>⚠️ 另有一个数**不属于本类**：<c>1e-3</c> 是**验收容差**（见 <see cref="HandCalcSamples.Tolerance"/>），
    /// 与写回门限差了两个数量级，不要拿它当门限用。</para>
    /// </summary>
    public static class ScaleWriteGate
    {
        /// <summary><c>CanvasScaler.SetScaleFactor</c>：<c>Mathf.Abs(v - m_PrevScaleFactor) &lt; 0.000005f</c> ⇒ 跳过写回。</summary>
        public const float ScaleWriteThreshold = 0.000005f;

        /// <summary><c>SetReferencePixelsPerUnit</c> 用**精确相等**判跳过（无 epsilon）。</summary>
        public const bool ReferenceWriteUsesExactEquality = true;

        /// <summary>第一道门限：差值小于 epsilon ⇒ 引擎**不会**写回 <c>Canvas.scaleFactor</c>。</summary>
        public static bool WouldSkipScale(float target, float prev) => Math.Abs(target - prev) < ScaleWriteThreshold;

        /// <summary>第二道门限：**精确相等**才跳过（差一点点也会写）。</summary>
        public static bool WouldSkipReference(float target, float prev) => target == prev;
    }
}
