using System.Globalization;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// **对账状态列的口径**：把"这一行的真值对比结果"与"整表为什么没有真值"收成**一个可单测的类型**
    /// （界面上的每个字都要能被断言复核）。
    /// <para>🔴 **纯函数、零 Unity 依赖、零场景访问**：只读 <see cref="ScaleTableRow"/> 上的既有字段
    /// （<c>HasTruth</c> + 四个 <c>Delta*</c>）与它的整表语境 <see cref="ReconcileContext"/>。</para>
    /// <para>🔴 **不许在这里重算差值**：判定用的四个 <c>Delta*</c> 与差值列显示的 <see cref="ScaleTableRow.DeltaText"/>
    /// **完全同源**，精度走 <see cref="AxisVerdict.FormatDelta"/>（显示位数不得低于容差）。</para>
    /// </summary>
    public static class ReconcileVerdict
    {
        /// <summary>
        /// 对账判定的**唯一容差**（与证据包口径一致：<c>1e-3</c>）。
        /// <para>🔴 **不许**复用 <see cref="ScaleFit.Tolerance"/>：后者是 **0.5 画布单位**，
        /// 与这里的 scaleFactor 差值**量纲不同**——混用会静默放宽判定。</para>
        /// </summary>
        public const float Tolerance = 1e-3f;

        /// <summary>三态（**互斥且覆盖全部组合**）。</summary>
        public enum State
        {
            /// <summary>本行没有现场真值可比（成因见 <see cref="NoTruthCause"/>）。</summary>
            NoTruth,

            /// <summary>有真值，且四个差值全部落在容差内。</summary>
            Pass,

            /// <summary>有真值，但至少一个差值超出容差。</summary>
            Fail,
        }

        /// <summary>
        /// <c>—</c> 的**四种成因**（窗口的悬停文案按这个分支给四句不同的话）。
        /// </summary>
        public enum NoTruthCause
        {
            /// <summary>本次会话从没点过「现场对账」。</summary>
            NotMeasuredThisSession,

            /// <summary>点过，但那次**没取到**真值（编辑期未渲染 / 反射不可用）。</summary>
            MeasurementFailed,

            /// <summary>🔴 取到了，但**条件与本表不同**——最容易被误读成"没对过账"的那一种。</summary>
            ConditionsDiffer,

            /// <summary>条件相符，但当次现场渲染尺寸不在档位表里。</summary>
            SizeNotInTable,
        }

        /// <summary>
        /// 这一行是什么状态。**运算顺序固定**：先看有没有真值 ⇒ 再逐项比四个差值 ⇒ 全在容差内才算通过。
        /// <para>⚠️ **边界**：<c>|Δ| == <see cref="Tolerance"/></c> 算**通过**（判据是"大于才算失败"）。</para>
        /// </summary>
        public static State StateOf(ScaleTableRow row)
        {
            if (row == null || !row.HasTruth) return State.NoTruth;

            if (System.Math.Abs(row.DeltaScaleFactor) > Tolerance) return State.Fail;
            if (System.Math.Abs(row.DeltaReferencePpu) > Tolerance) return State.Fail;
            if (System.Math.Abs(row.DeltaCanvasWidth) > Tolerance) return State.Fail;
            if (System.Math.Abs(row.DeltaCanvasHeight) > Tolerance) return State.Fail;
            return State.Pass;
        }

        /// <summary>单元格文字：`—` / `过` / `失`（三态**双编码**里的"文字"那一半，颜色只是第三重）。</summary>
        public static string Text(ScaleTableRow row)
        {
            switch (StateOf(row))
            {
                case State.Pass: return "过";
                case State.Fail: return "失";
                default: return "—";
            }
        }

        /// <summary>
        /// 单元格悬停文本：**成功/失败说过程，无真值说原因**。
        /// <para>🔴 失败那句里的差值**逐字复用** <see cref="ScaleTableRow.DeltaText"/>（同一个事实不许两处各拼一遍）。</para>
        /// </summary>
        public static string Tooltip(ScaleTableRow row)
        {
            switch (StateOf(row))
            {
                case State.Pass:
                    return "对账通过：Δ 全部 ≤ " + Tolerance.ToString("G3", CultureInfo.InvariantCulture)
                         + (string.IsNullOrEmpty(row.Reconcile.Provenance) ? "" : "（" + row.Reconcile.Provenance + "）");
                case State.Fail:
                    return "对账失败：" + row.DeltaText;
                default:
                    return NoTruthText(row);
            }
        }

        /// <summary>
        /// <c>—</c> 的成因（**唯一**把整表语境翻译成成因的地方）。
        /// <para>优先级：**条件不符 &gt; 没取到 &gt; 没对过账**，最后才是"尺寸不在表里"。
        /// 条件不符排第一，是因为它的用户**刚换过参考/match/方式**，他手里有两种误解
        /// （"我以为对过账了" / "我是不是点错了"），必须先告诉他"**对过、但条件变了**"。</para>
        /// </summary>
        public static NoTruthCause CauseOf(ScaleTableRow row)
        {
            ReconcileContext ctx = row == null ? default : row.Reconcile;
            if (!ctx.HasSession) return NoTruthCause.NotMeasuredThisSession;
            if (!ctx.MeasuredOk) return NoTruthCause.MeasurementFailed;
            if (!ctx.AppliesToTable) return NoTruthCause.ConditionsDiffer;
            return NoTruthCause.SizeNotInTable;
        }

        /// <summary>无真值时的悬停文案（四句**互不相同**；测试逐字钉住它们的关键词）。</summary>
        public static string NoTruthText(ScaleTableRow row)
        {
            ReconcileContext ctx = row == null ? default : row.Reconcile;
            switch (CauseOf(row))
            {
                case NoTruthCause.ConditionsDiffer:
                    return "本表没有真值可比：现场真值是在「" + ctx.MeasuredConditionText
                         + "」下测的，与本表条件（" + ctx.TableConditionText + "）不同 ⇒ 整表离线";
                case NoTruthCause.MeasurementFailed:
                    return "本表没有真值可比：本次会话的对账未取到真值（" + ctx.TruthError + "）";
                case NoTruthCause.NotMeasuredThisSession:
                    return "本表没有真值可比：本次会话还没点过「现场对账」";
                default:
                    return "本表没有真值可比：当次现场屏幕尺寸（" + ctx.MeasuredScreenSize
                         + "）不在档位表里（真值只对「当前渲染尺寸」那一行成立）";
            }
        }
    }
}
