using System.Globalization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// <see cref="ScaleCalcWindow"/> 的**对账章节**（窗口主体在 `ScaleCalcWindow.cs`、
    /// 导出章节在 `ScaleCalcWindow.Export.cs`）。
    /// <para>拆法 = **按"什么时候动场景"分组**：本文件是全窗口**唯一**会碰场景的地方——
    /// 用户点「现场对账」按钮、或（仅当"自动对账"开关打开时）改完参数停手一段之后，才会走一次
    /// <see cref="EvidencePackCommand.MeasureLive"/>。**开窗与拖控件都不对账**。</para>
    /// <para>测到的真值**缓存在窗口里**（<see cref="m_Truth"/> / <see cref="m_TruthOk"/>），导出按钮直接吃这份缓存
    /// ⇒ 同一次会话连点导出，场景操作 = **0**。</para>
    /// </summary>
    public sealed partial class ScaleCalcWindow
    {
        /// <summary>「自动对账」停手多久才算"停手"（毫秒）。只在开关打开时生效。</summary>
        private const long AutoReconcileDebounceMs = 400;

        /// <summary>最近一次现场对账取到的真值（未对账 / 未取到时配合 <see cref="m_TruthOk"/> 使用）。</summary>
        private LiveTruth m_Truth;
        private bool m_TruthOk;
        private string m_TruthError;

        /// <summary>这份真值是**哪一次对账**取到的（状态栏要如实说出真值的来历）。空 = 本次会话还没对过账。</summary>
        private string m_TruthStamp;

        /// <summary>本次会话对账过几次（只用于状态栏说清"这一份是哪一次取的"）。</summary>
        private int m_ReconcileCount;

        /// <summary>防抖用的定时器句柄。非空 = 已排了一次待执行的对账。</summary>
        private IVisualElementScheduledItem m_AutoReconcileItem;

        /// <summary>
        /// 按当前控件条件现场对账**一次**并缓存结果。**这是本窗口唯一会建/拆场景的路径**：
        /// 由「现场对账」按钮或"自动对账"的防抖回调触发，绝不由控件值变化直接触发。
        /// <para>⚠️ **条件要用控件当前值**（不是证据包的固定条件）：真值带 <c>MeasuredReference</c>/<c>MeasuredMatch</c>，
        /// 表那边靠 <c>truth.AppliesTo</c> 守门——条件不对就整表离线，而不是硬凑一个假差值。</para>
        /// </summary>
        private void ReconcileNow()
        {
            ScaleCalcInput template = CurrentTemplate();
            bool ok = EvidencePackCommand.MeasureLive(template, out LiveTruth truth, out string error);

            m_Truth = truth;
            m_TruthOk = ok;
            m_TruthError = error;
            m_TruthStamp = "本次会话第 " + (++m_ReconcileCount) + " 次现场对账（条件 " + template.ReferenceResolution
                + " × match " + template.MatchWidthOrHeight.ToString("0.00", CultureInfo.InvariantCulture) + "）";

            Rebuild();                       // 把这份真值画进表里（重算本身是离线纯函数，零场景操作）
            ReportTruthAction(ok, error);
        }

        /// <summary>
        /// 把窗口缓存的真值状态翻成**整表语境**：**纯读**，不碰场景、不问引擎。
        /// <para>🔴 **运算顺序**：<c>applies</c> 必须在 <c>m_TruthOk</c> 为真时才去算 —— 失败时 <c>m_Truth</c>
        /// 是默认值，<c>AppliesTo</c> 会因 <c>MeasuredReference</c> 为零而返回 false（结果也对），但**显式短路**更清楚，
        /// 且不依赖"默认构造的真值永不适用"这条远端不变量。</para>
        /// </summary>
        private ReconcileContext BuildReconcileContext(in ScaleCalcInput template)
        {
            bool hasSession = !string.IsNullOrEmpty(m_TruthStamp);
            bool applies = m_TruthOk && m_Truth.AppliesTo(template.ReferenceResolution,
                                                          template.MatchWidthOrHeight, template.ScreenMatch);
            return new ReconcileContext(
                hasSession,
                m_TruthOk,
                applies,
                m_TruthOk ? m_Truth.ConditionText : null,
                LiveTruth.ConditionTextOf(template.ReferenceResolution, template.ScreenMatch, template.MatchWidthOrHeight),
                m_Truth.ScreenSize,
                m_TruthError,
                m_TruthStamp);
        }

        /// <summary>
        /// 把"这次对账成没成"如实写进**操作反馈行**（<c>tier-status</c>）。
        /// <para>🔴 **它是唯一的界面反馈出口**：原来还有一行专门写"真值来历"喂总结行，而**总结行与分支栏已整条撤除**
        /// ⇒ 那个方法已删；"真值是哪一次取的、什么条件"现在由**对账状态列的悬停**承担（<see cref="ReconcileContext.Provenance"/>）。</para>
        /// </summary>
        private void ReportTruthAction(bool ok, string error)
        {
            Report(ok
                ? "已现场对账：" + m_Truth.ConditionText + " → 渲染尺寸 " + m_Truth.ScreenSize + "（" + m_Truth.Source + "）"
                : "现场对账未取到真值：" + error);
        }

        /// <summary>
        /// 抖一次就重排的**防抖**：只在"自动对账"开关打开时排程；停手
        /// <see cref="AutoReconcileDebounceMs"/> 毫秒后才真去对账一次。
        /// <para>拖一次滑块 = 连续多次值变化 ⇒ 每次都把上一次的排程撤掉重排 ⇒ 最终**最多对账 1 次**。</para>
        /// </summary>
        private void ScheduleAutoReconcile()
        {
            if (m_AutoReconcileToggle == null || !m_AutoReconcileToggle.value) return;   // 默认关 ⇒ 拖滑块 0 次对账

            CancelAutoReconcile();
            m_AutoReconcileItem = rootVisualElement.schedule.Execute(ReconcileNow).StartingIn(AutoReconcileDebounceMs);
        }

        /// <summary>撤掉还没执行的自动对账（开关被关掉、或用户又动了控件）。</summary>
        private void CancelAutoReconcile()
        {
            if (m_AutoReconcileItem == null) return;
            m_AutoReconcileItem.Pause();
            m_AutoReconcileItem = null;
        }

        /// <summary>
        /// 「自动对账」开关变化：刚打开 ⇒ 排一次防抖对账（现在就对账会让"打开开关"变成立即动场景的一次）；
        /// 刚关掉 ⇒ 撤掉待执行的那一次。
        /// </summary>
        private void OnAutoReconcileChanged(ChangeEvent<bool> evt)
        {
            if (evt.newValue) { ScheduleAutoReconcile(); return; }
            CancelAutoReconcile();
            Rebuild();                       // 只刷新表格与结论（"自动对账：关"），不碰场景
        }
    }
}
