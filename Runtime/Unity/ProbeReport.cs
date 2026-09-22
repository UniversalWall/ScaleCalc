namespace Wayward.ScaleCalc.Unity
{
    /// <summary>
    /// 结构化报告（**只读结构体，可断言**）。
    /// <para>⚠️ 零分配约定：<see cref="DegradedFields"/> **只允许**指向静态只读字符串常量，
    /// 禁止在采样路径上运行时拼接（实测：连续 1000 帧采样，<c>GC.GetTotalMemory</c> 无单调增长）。</para>
    /// </summary>
    public readonly struct ProbeReport
    {
        // ── 环境 ───────────────────────────────────────────────────
        public readonly SampleTiming Timing;
        public readonly int FrameCount;
        public readonly bool ComponentEnabled;
        public readonly bool SawFallbackDpi;

        // ── 反射降级：**并列标志位**，不是判定值 ─────────────────────
        public readonly bool ReflectionAvailable;
        public readonly string DegradedFields;

        // ── 输入（含来源标注）──────────────────────────────────────
        public readonly ScaleSize ScreenSize;
        public readonly ScreenSizeSource ScreenSource;
        public readonly float ScreenDpi;
        public readonly ScaleMode Mode;
        public readonly ScreenMatchMode ScreenMatch;
        public readonly float MatchWidthOrHeight;
        public readonly ScaleSize ReferenceResolution;

        // ── 内核 vs 真值 vs 差值（三项对账）──────────────────────────
        public readonly ScaleCalcResult Kernel;
        public readonly float TruthScaleFactor;
        public readonly float TruthReferencePpu;
        public readonly ScaleSize TruthCanvasSize;
        public readonly ScaleSize TruthRenderingDisplaySize;
        public readonly float DeltaScaleFactor;
        public readonly float DeltaReferencePpu;
        public readonly float DeltaCanvasWidth;
        public readonly float DeltaCanvasHeight;
        public readonly float PrevScaleFactor;
        public readonly float PrevReferencePpu;

        // ── 早退专用：读数继承自根 Canvas，仅作说明、**不参与比差** ──────
        public readonly bool EarlyExitHasInheritedReading;
        public readonly float InheritedScaleFactor;
        public readonly float InheritedReferencePpu;

        // ── 判定 ───────────────────────────────────────────────────
        public readonly ProbeVerdict Verdict;

        public ProbeReport(
            SampleTiming timing, int frameCount, bool componentEnabled, bool sawFallbackDpi,
            bool reflectionAvailable, string degradedFields,
            ScaleSize screenSize, ScreenSizeSource screenSource, float screenDpi,
            ScaleMode mode, ScreenMatchMode screenMatch, float matchWidthOrHeight, ScaleSize referenceResolution,
            ScaleCalcResult kernel,
            float truthScaleFactor, float truthReferencePpu, ScaleSize truthCanvasSize, ScaleSize truthRenderingDisplaySize,
            float deltaScaleFactor, float deltaReferencePpu, float deltaCanvasWidth, float deltaCanvasHeight,
            float prevScaleFactor, float prevReferencePpu,
            bool earlyExitHasInheritedReading, float inheritedScaleFactor, float inheritedReferencePpu,
            ProbeVerdict verdict)
        {
            Timing = timing;
            FrameCount = frameCount;
            ComponentEnabled = componentEnabled;
            SawFallbackDpi = sawFallbackDpi;
            ReflectionAvailable = reflectionAvailable;
            DegradedFields = degradedFields;
            ScreenSize = screenSize;
            ScreenSource = screenSource;
            ScreenDpi = screenDpi;
            Mode = mode;
            ScreenMatch = screenMatch;
            MatchWidthOrHeight = matchWidthOrHeight;
            ReferenceResolution = referenceResolution;
            Kernel = kernel;
            TruthScaleFactor = truthScaleFactor;
            TruthReferencePpu = truthReferencePpu;
            TruthCanvasSize = truthCanvasSize;
            TruthRenderingDisplaySize = truthRenderingDisplaySize;
            DeltaScaleFactor = deltaScaleFactor;
            DeltaReferencePpu = deltaReferencePpu;
            DeltaCanvasWidth = deltaCanvasWidth;
            DeltaCanvasHeight = deltaCanvasHeight;
            PrevScaleFactor = prevScaleFactor;
            PrevReferencePpu = prevReferencePpu;
            EarlyExitHasInheritedReading = earlyExitHasInheritedReading;
            InheritedScaleFactor = inheritedScaleFactor;
            InheritedReferencePpu = inheritedReferencePpu;
            Verdict = verdict;
        }
    }
}
