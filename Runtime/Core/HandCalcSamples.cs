namespace Wayward.ScaleCalc
{
    /// <summary>
    /// **手算常量表**：测试与选型台共用的"期望值"。与断言代码分离——改常量不会碰断言逻辑。
    /// <para>放在 Core 是因为它是唯一能同时被 <c>Tests</c> 与 <c>Editor</c> 引用的位置（Editor 不能引用 Tests）。</para>
    /// <para>每一组期望值都是照 <c>CanvasScaler</c> 的公式**手算**出来的（保留 5 位有效），比对面按容差 <see cref="Tolerance"/>。</para>
    /// </summary>
    public static class HandCalcSamples
    {
        /// <summary>验收容差。⚠️ 与写回门限 <c>0.000005</c> **不是一回事**（差两个数量级，见 <see cref="ScaleWriteGate"/>）。</summary>
        public const float Tolerance = 1e-3f;

        // ── 参考 1920×1080、屏幕 1440×3120、match = 0.5 ─────────────────────
        // logW = log2(0.75) = -0.4150374993 · logH = log2(2.8888888889) = 1.5305147167
        // weighted = logW + (logH - logW) * 0.5 = 0.5577386087 ⇒ sf = 2^0.5577386087 = 1.4719601444
        // 画布 = 1440 / sf = 978.2873575 · 3120 / sf = 2119.6226079
        // ⚠️ 常量存 **0.1ppm 级**：5 位展示值（978.29 / 2119.62）配 1e-3 容差本身就不够（差 2.6e-3）。
        public const float MatchHalf_1440x3120_ScaleFactor = 1.4719601f;
        public const float MatchHalf_1440x3120_CanvasWidth = 978.2874f;
        public const float MatchHalf_1440x3120_CanvasHeight = 2119.6226f;

        /// <summary>上面这组输入的构造（后面 3 个常数就是它的手算期望）。</summary>
        public static readonly ScaleCalcInput MatchHalf_1440x3120 = WithScreen(
            new ScaleSize(1920f, 1080f), new ScaleSize(1440f, 3120f), ScreenMatchMode.MatchWidthOrHeight, 0.5f);

        // ── 同一输入（参考 1920×1080、屏幕 1080×1920）在 Expand / Shrink 下 ────
        // ratioW = 1080/1920 = 0.5625 · ratioH = 1920/1080 = 1.77778
        // Expand = min = 0.5625 ⇒ 画布 1080/0.5625 = 1920 · 1920/0.5625 = 3413.33
        // Shrink = max = 1.77778 ⇒ 画布 1080/1.77778 = 607.5 · 1920/1.77778 = 1080
        public const float AspectSwapped_Expand_ScaleFactor = 0.5625f;
        public const float AspectSwapped_Expand_CanvasWidth = 1920f;
        public const float AspectSwapped_Expand_CanvasHeight = 3413.3333f;
        public const float AspectSwapped_Shrink_ScaleFactor = 1.7777778f;
        public const float AspectSwapped_Shrink_CanvasWidth = 607.5f;
        public const float AspectSwapped_Shrink_CanvasHeight = 1080f;

        public static readonly ScaleCalcInput AspectSwapped_Expand = WithScreen(
            new ScaleSize(1920f, 1080f), new ScaleSize(1080f, 1920f), ScreenMatchMode.Expand, 0f);

        public static readonly ScaleCalcInput AspectSwapped_Shrink = WithScreen(
            new ScaleSize(1920f, 1080f), new ScaleSize(1080f, 1920f), ScreenMatchMode.Shrink, 0f);

        // ── 反例：参考 1920×1080、屏幕 960×2160、match = 0.5 ────────────────
        // ratioW = 0.5（一轴减半）· ratioH = 2.0（一轴翻倍）
        // 几何平均 ⇒ 2^((log2 0.5 + log2 2.0) / 2) = 2^0 = 1.0000
        // ⚠️ 算术平均会给 (0.5 + 2.0) / 2 = 1.25 —— 这条就是"match 不是算术平均"的反例判据
        public const float ReciprocalAxes_GeometricMean_ScaleFactor = 1.0f;
        public const float ReciprocalAxes_ArithmeticMean_WrongAnswer = 1.25f;

        public static readonly ScaleCalcInput ReciprocalAxes_960x2160 = WithScreen(
            new ScaleSize(1920f, 1080f), new ScaleSize(960f, 2160f), ScreenMatchMode.MatchWidthOrHeight, 0.5f);

        // ── 物理尺寸兜底链（Points ⇒ targetDPI = 72；refPPU = 100 * 72 / 96 = 75）──
        public const float Physical_Points_TargetDpi = 72f;
        public const float Physical_ReportedDpi160_ScaleFactor = 2.2222222f;   // 160 / 72
        public const float Physical_FallbackDpi96_ScaleFactor = 1.3333333f;    //  96 / 72
        public const float Physical_RefPixelsPerUnit = 75f;                   // 100 * 72 / 96（Points + defaultSpriteDPI 96）

        /// <summary>真报分支：<c>ScreenDpi = 160</c>（非 0 ⇒ 不走兜底）。</summary>
        public static readonly ScaleCalcInput Physical_ReportedDpi160 = WithPhysical(new ScaleSize(800f, 600f), 160f);

        /// <summary>兜底分支：<c>ScreenDpi = 0</c> ⇒ 退化为 <c>FallbackScreenDPI = 96</c>。</summary>
        public static readonly ScaleCalcInput Physical_FallbackDpi96 = WithPhysical(new ScaleSize(800f, 600f), 0f);

        // ── 三处夹取的手算期望 ────────────────────────────────────────────────
        public const float ClampedResolutionComponent = 0.00001f;   // |v| < 1e-5 ⇒ 1e-5 * Sign(v)
        public const float ClampedConstantScaleFactor = 0.01f;      // < 0.01 ⇒ 0.01
        public const float ClampedDefaultSpriteDpi = 1f;            // < 1 ⇒ 1

        // ── 构造工具 ──────────────────────────────────────────────────────────
        /// <summary>随屏幕尺寸模式 + 指定 match 的输入（其余字段取内核默认值）。</summary>
        public static ScaleCalcInput WithScreen(ScaleSize reference, ScaleSize screen,
                                                ScreenMatchMode match, float matchWidthOrHeight)
        {
            ScaleCalcInput i = ScaleCalcInput.Default;
            i.Mode = ScaleMode.ScaleWithScreenSize;
            i.ReferenceResolution = reference;
            i.ScreenSize = screen;
            i.ScreenMatch = match;
            i.MatchWidthOrHeight = matchWidthOrHeight;
            return i;
        }

        /// <summary>常量物理尺寸模式 + 指定本机 dpi 的输入（其余字段取内核默认值：Points / 96 / 96 / 100）。</summary>
        public static ScaleCalcInput WithPhysical(ScaleSize reference, float screenDpi)
        {
            ScaleCalcInput i = ScaleCalcInput.Default;
            i.Mode = ScaleMode.ConstantPhysicalSize;
            i.ReferenceResolution = reference;
            i.ScreenSize = reference;
            i.ScreenDpi = screenDpi;
            return i;
        }
    }
}
