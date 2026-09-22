namespace Wayward.ScaleCalc.Unity
{
    /// <summary>
    /// 判定值（优先级**从上到下短路**）：
    /// <c>EarlyExit</c> → <c>TruthNotFinite</c> → <c>WriteSkippedButOverwritten</c> → <c>Mismatch</c> → <c>Ok</c>。
    /// <para>「精度降级」**不是**判定值：见 <see cref="ProbeReport.ReflectionAvailable"/> / <see cref="ProbeReport.DegradedFields"/>。</para>
    /// </summary>
    public enum ProbeVerdict
    {
        /// <summary>对账通过。</summary>
        Ok,

        /// <summary>内核与引擎真值（含画布尺寸）差超过容差。</summary>
        Mismatch,

        /// <summary>引擎真值非有限（`∞`/`NaN`）——输入非法（Inspector 绕开夹取），**合法差异**。</summary>
        TruthNotFinite,

        /// <summary>引擎本帧跳过写回，而画布上的值不是它上次写的 ⇒ **有人手改了**。</summary>
        WriteSkippedButOverwritten,

        /// <summary>本组件不生效（子 Canvas / 世界空间无关输入）：**不参与比差**。</summary>
        EarlyExit,
    }
}
