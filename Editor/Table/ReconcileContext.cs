namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// **整表真值语境**：回答"**这一张表为什么有 / 没有真值**"。
    /// <para>🔴 **为什么需要它**：<see cref="ScaleTableRow.HasTruth"/> 为 <c>false</c> 有**四种**成因——
    /// ① 本次会话从没点过「现场对账」· ② 点过但没取到真值 · ③ 取到了但**测量条件与本表条件不同**
    /// （<see cref="LiveTruth.AppliesTo"/> 守门）· ④ 条件相符但现场渲染尺寸不在档位表里。
    /// 这四种原因本来只写在 <see cref="ScaleTable.TruthNote"/> 那一句话里，而那句话随底部四行文本一起被删
    /// ⇒ 若不另找落点，界面上的 <c>—</c> 会把"**对过账、但条件变了**"和"**根本没对过账**"混成一种沉默。</para>
    /// <para>🔴 **它挂在每一行上**（<see cref="ScaleTableRow.Reconcile"/>）：这样"状态列 / 导出"都能在
    /// **只有一行**的上下文里回答整表问题 —— 与 <see cref="ScaleTableRow.Reference"/> 的既有做法一致
    /// （列方向共用，行上留一份便于呈现）。</para>
    /// <para>**纯数据、零逻辑、零 Unity 依赖**；构造点在 <see cref="ScaleTable.Build"/> 与窗口的
    /// <c>BuildReconcileContext</c>。⚠️ **默认构造的语境 = "本次会话什么都不知道"**（三个 bool 全 <c>false</c>）
    /// ⇒ 漏赋值的行如实说"还没点过现场对账"，有断言专门钉住这一点。</para>
    /// </summary>
    public readonly struct ReconcileContext
    {
        /// <summary>本次会话是否对过账（= 窗口的 <c>m_TruthStamp</c> 非空）。</summary>
        public readonly bool HasSession;

        /// <summary>那次对账是否**取到了**真值（= 窗口的 <c>m_TruthOk</c>）。</summary>
        public readonly bool MeasuredOk;

        /// <summary>
        /// 取到的真值**是否适用于本表条件**（= <see cref="LiveTruth.AppliesTo"/> 对（本表参考, 本表 match, 本表方式）的结果）。
        /// <para>🔴 **只在 <see cref="MeasuredOk"/> 为真时有意义**；为假时本字段恒为 <c>false</c>
        /// （窗口侧显式短路，不依赖"默认构造的真值永不适用"这条远端不变量）。</para>
        /// </summary>
        public readonly bool AppliesToTable;

        /// <summary>真值的**测量条件**文案（<see cref="LiveTruth.ConditionText"/>；未取到时为 <c>null</c>）。</summary>
        public readonly string MeasuredConditionText;

        /// <summary>**本表条件**文案（<see cref="LiveTruth.ConditionTextOf"/>）——与测量条件同一来源，
        /// 免得两处各写一套。</summary>
        public readonly string TableConditionText;

        /// <summary>当次现场屏幕尺寸（用于"尺寸不在档位表里"那一句）。</summary>
        public readonly ScaleSize MeasuredScreenSize;

        /// <summary>对账失败原因（= 窗口的 <c>m_TruthError</c>；成功时为 <c>null</c>）。</summary>
        public readonly string TruthError;

        /// <summary>真值来历（= 窗口的 <c>m_TruthStamp</c>，例如"本次会话第 1 次现场对账（条件 …）"；
        /// 没对过账时为 <c>null</c>）。**现在喂状态列的悬停**。</summary>
        public readonly string Provenance;

        public ReconcileContext(bool hasSession, bool measuredOk, bool appliesToTable,
                               string measuredConditionText, string tableConditionText,
                               ScaleSize measuredScreenSize, string truthError, string provenance)
        {
            HasSession = hasSession;
            MeasuredOk = measuredOk;
            AppliesToTable = appliesToTable;
            MeasuredConditionText = measuredConditionText;
            TableConditionText = tableConditionText;
            MeasuredScreenSize = measuredScreenSize;
            TruthError = truthError;
            Provenance = provenance;
        }
    }
}
