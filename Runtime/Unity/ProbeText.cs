using System.Globalization;
using System.Text;

namespace Wayward.ScaleCalc.Unity
{
    /// <summary>
    /// <see cref="ProbeReport"/> → 文本：**一行一件事**，便于检索与落证据。
    /// <para>⚠️ 本类型**会分配字符串**，只在「判定/降级/读数变化」或手动请求时调用——
    /// **不要在每帧采样路径上无条件调用**（那会毁掉探针的零分配前提）。</para>
    /// </summary>
    public static class ProbeText
    {
        /// <summary>对账容差（显示用；真值取自探针字段，这里是文案里的口径说明）。</summary>
        private const float Tolerance = 1e-3f;

        public static string Format(in ProbeReport r)
        {
            var sb = new StringBuilder(512);
            var ci = CultureInfo.InvariantCulture;

            sb.AppendFormat(ci,
                "[ScaleCalc] 采样={0} 帧={1} 组件={2} 分支={3} 判定={4}\n",
                r.Timing, r.FrameCount, r.ComponentEnabled ? "启用" : "停用", r.Kernel.Branch, r.Verdict);

            sb.AppendFormat(ci,
                "  输入   screen={0}({1}) dpi={2:G6}(Screen.dpi) mode={3} match={4:G4} ref={5}\n",
                r.ScreenSize, r.ScreenSource, r.ScreenDpi, r.Mode, r.MatchWidthOrHeight, r.ReferenceResolution);

            sb.AppendFormat(ci,
                "  内核   scaleFactor={0:F6} refPPU={1:F6} canvas={2} 走兜底={3}\n",
                r.Kernel.ScaleFactor, r.Kernel.ReferencePixelsPerUnit,
                r.Kernel.HasCanvasSize ? r.Kernel.CanvasSize.ToString() : "（无定义）", r.SawFallbackDpi ? "是" : "否");

            sb.AppendFormat(ci,
                "  真值   scaleFactor={0:F6} refPPU={1:F6} renderingDisplaySize={2} rect.size={3}\n",
                r.TruthScaleFactor, r.TruthReferencePpu, r.TruthRenderingDisplaySize, r.TruthCanvasSize);

            if (r.Kernel.HasResult)
            {
                sb.AppendFormat(ci,
                    "  差值   scaleFactor={0:F6}（容差 {1:G3}）refPPU={2:F6}（容差 {1:G3}）canvas={3:F2}x{4:F2}（容差 {1:G3}）\n",
                    r.DeltaScaleFactor, Tolerance, r.DeltaReferencePpu, r.DeltaCanvasWidth, r.DeltaCanvasHeight);
            }

            sb.AppendFormat(ci,
                "  门限   {0}={1:F6}(跳过写回={2}) {3}={4:F6}(跳过写回={5}) 反射={6}\n",
                "m_PrevScaleFactor", r.PrevScaleFactor, ScaleWriteGate.WouldSkipScale(r.Kernel.ScaleFactor, r.PrevScaleFactor),
                "m_PrevReferencePixelsPerUnit", r.PrevReferencePpu,
                ScaleWriteGate.WouldSkipReference(r.Kernel.ReferencePixelsPerUnit, r.PrevReferencePpu),
                r.ReflectionAvailable ? "OK" : "降级");

            sb.AppendFormat(ci, "  结论   {0}\n", VerdictText(in r));

            if (r.EarlyExitHasInheritedReading)
            {
                sb.AppendFormat(ci,
                    "  说明   上面两行读数是**根 Canvas**的值，不是本组件的产出，**不参与比差**\n");
            }

            if (!r.ReflectionAvailable)
            {
                sb.AppendFormat(ci, "  精度降级  未验证写回门限（缺字段 {0}）\n", r.DegradedFields);
            }

            if (r.Verdict == ProbeVerdict.Mismatch || r.Verdict == ProbeVerdict.TruthNotFinite)
            {
                sb.AppendFormat(ci,
                    "  复现   把「输入」行的数喂回内核（`ScaleCalc.Evaluate(in input)`），与「真值」行逐项比差；\n"
                    + "         同一组样本另有 EditMode 断言守着（见 Tests/EditMode/Kernel 与 Tests/EditMode/Probe）\n");
            }

            return sb.ToString();
        }

        /// <summary>判定文案（把「合法差异」与「真失败」分开——两类的处置完全不同）。</summary>
        public static string VerdictText(in ProbeReport r)
        {
            switch (r.Verdict)
            {
                case ProbeVerdict.EarlyExit:
                    return "本组件不生效：子 Canvas 上挂 CanvasScaler 完全不生效，且子 Canvas **继承根 Canvas 的 scaleFactor**"
                         + "（本行读数为「根 Canvas 实际读数 scaleFactor=" + r.InheritedScaleFactor.ToString("F6", CultureInfo.InvariantCulture)
                         + " refPPU=" + r.InheritedReferencePpu.ToString("F6", CultureInfo.InvariantCulture)
                         + "」——**不是本组件的产出，不参与比差**）";
                case ProbeVerdict.TruthNotFinite:
                    return "引擎真值非有限（输入非法，Inspector 绕开夹取）；内核按夹取口径输出有限值——**合法差异，非对账失败**";
                case ProbeVerdict.WriteSkippedButOverwritten:
                    return "引擎本帧**跳过写回**（差值在门限 5e-6 内），但 Canvas 上的值 ≠ 上一次写回值 ⇒ **有人在本帧手改了它**；"
                         + "**本判定不是内核算错**";
                case ProbeVerdict.Mismatch:
                    return "对账失败：差超容差 1e-3；先查输入源是否与引擎一致（renderingDisplaySize vs Screen.width）";
                default:
                    return "对账通过";
            }
        }

        /// <summary>单行摘要（稳定帧限频输出用，避免每帧分配大段文本）。</summary>
        public static string FormatSummary(in ProbeReport r) => string.Format(CultureInfo.InvariantCulture,
            "[ScaleCalc] 稳定 {0} 帧 · 分支={1} 判定={2} scaleFactor={3:F6}（内核=真值）",
            r.FrameCount, r.Kernel.Branch, r.Verdict, r.Kernel.ScaleFactor);
    }
}
