using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace Wayward.ScaleCalc.Unity
{
    /// <summary>
    /// <see cref="ScaleFactorProbe"/> 的**采集与判定**那一半（拆文件的判据 = 200 行红线；原文件 271 行已拆）。
    /// <para>这里只有三件事：读（零分配）→ 跑内核 → 定判定。**生命周期与输出策略在主文件**。</para>
    /// </summary>
    public sealed partial class ScaleFactorProbe
    {
        /// <summary>
        /// 读取 + 跑内核 + 组装报告（**零分配**；不做格式化、不打日志）。
        /// </summary>
        public ProbeReport Capture(SampleTiming timing)
        {
            Canvas canvas = targetCanvas;
            CanvasScaler scaler = targetScaler;

            ScaleCalcInput input = ScaleInputCollector.Collect(canvas, scaler, out ScreenSizeSource source);
            ScaleCalcResult kernel = ScaleCalc.Evaluate(in input);

            bool okScale = TryReadPrev(scaler, prevScaleFactorField, ref m_ScaleField, out float prevScale);
            bool okReference = TryReadPrev(scaler, prevReferencePpuField, ref m_ReferenceField, out float prevPpu);
            bool reflectionAvailable = okScale && okReference;
            string degraded = !okScale && !okReference ? DegradedBoth
                : !okScale ? DegradedScaleOnly
                : !okReference ? DegradedReferenceOnly
                : NoDegradation;

            float truthScale = 0f, truthPpu = 0f;
            ScaleSize truthCanvas = default;
            ScaleSize truthRendering = default;
            if (canvas != null)
            {
                truthScale = canvas.scaleFactor;
                truthPpu = canvas.referencePixelsPerUnit;
                truthRendering = new ScaleSize(canvas.renderingDisplaySize.x, canvas.renderingDisplaySize.y);

                // 画布尺寸真值 = **根 Canvas** 的 RectTransform.rect.size（缩放器只在根 Canvas 上生效）
                Canvas root = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;
                if (root.transform is RectTransform rootRect)
                    truthCanvas = new ScaleSize(rootRect.rect.size.x, rootRect.rect.size.y);
            }

            float deltaScale = kernel.ScaleFactor - truthScale;
            float deltaPpu = kernel.ReferencePixelsPerUnit - truthPpu;
            float deltaCanvasWidth = kernel.HasCanvasSize ? kernel.CanvasSize.Width - truthCanvas.Width : 0f;
            float deltaCanvasHeight = kernel.HasCanvasSize ? kernel.CanvasSize.Height - truthCanvas.Height : 0f;

            bool earlyExitHasInherited = !kernel.HasResult && canvas != null;
            float inheritedScale = 0f, inheritedPpu = 0f;
            if (earlyExitHasInherited)
            {
                Canvas root = canvas.rootCanvas;
                if (root != null)
                {
                    inheritedScale = root.scaleFactor;
                    inheritedPpu = root.referencePixelsPerUnit;
                }
            }

            ProbeVerdict verdict = DecideVerdict(
                in kernel, okScale, okReference,
                truthScale, truthPpu, prevScale, prevPpu,
                deltaScale, deltaPpu, deltaCanvasWidth, deltaCanvasHeight);

            var report = new ProbeReport(
                timing, Time.frameCount, scaler != null && scaler.enabled, kernel.UsedFallbackDpi,
                reflectionAvailable, degraded,
                input.ScreenSize, source, input.ScreenDpi,
                input.Mode, input.ScreenMatch, input.MatchWidthOrHeight, input.ReferenceResolution,
                kernel,
                truthScale, truthPpu, truthCanvas, truthRendering,
                deltaScale, deltaPpu, deltaCanvasWidth, deltaCanvasHeight,
                prevScale, prevPpu,
                earlyExitHasInherited, inheritedScale, inheritedPpu,
                verdict);

            return report;
        }

        /// <summary>判定（短路顺序就是这里的书写顺序，**不要重排**）。</summary>
        private ProbeVerdict DecideVerdict(
            in ScaleCalcResult kernel, bool okScale, bool okReference,
            float truthScale, float truthPpu, float prevScale, float prevPpu,
            float deltaScale, float deltaPpu, float deltaCanvasWidth, float deltaCanvasHeight)
        {
            if (!kernel.HasResult) return ProbeVerdict.EarlyExit;

            if (!IsFinite(truthScale) || !IsFinite(truthPpu)) return ProbeVerdict.TruthNotFinite;

            if (okScale && ScaleWriteGate.WouldSkipScale(kernel.ScaleFactor, prevScale) && truthScale != prevScale)
                return ProbeVerdict.WriteSkippedButOverwritten;
            if (okReference && ScaleWriteGate.WouldSkipReference(kernel.ReferencePixelsPerUnit, prevPpu) && truthPpu != prevPpu)
                return ProbeVerdict.WriteSkippedButOverwritten;

            if (Abs(deltaScale) > tolerance || Abs(deltaPpu) > tolerance
                || Abs(deltaCanvasWidth) > tolerance || Abs(deltaCanvasHeight) > tolerance)
                return ProbeVerdict.Mismatch;

            return ProbeVerdict.Ok;
        }

        /// <summary>
        /// 两个私有字段**各自独立**降级：读不到就返回 <c>false</c>，
        /// **不抛异常、也不把 0 当成功**——由调用方把"缺哪个字段"写进报告的并列标志位。
        /// </summary>
        private static bool TryReadPrev(object target, string fieldName, ref FieldInfo cache, out float value)
        {
            value = 0f;
            if (target == null || string.IsNullOrEmpty(fieldName)) return false;

            if (cache == null || cache.Name != fieldName)
            {
                cache = target.GetType().GetField(fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            }
            if (cache == null) return false;

            object raw = cache.GetValue(target);
            if (raw is float f)
            {
                value = f;
                return true;
            }
            return false;
        }

        private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        private static float Abs(float v) => v < 0f ? -v : v;
    }
}
