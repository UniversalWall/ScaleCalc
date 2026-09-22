namespace Wayward.ScaleCalc
{
    /// <summary>
    /// 缩放换算的**唯一输入面**（四个面，15 字段）。
    /// <para>字段默认值 = <c>CanvasScaler</c> 组件**新建时的序列化默认值**，便于"只改关心的字段"地构造输入。</para>
    /// <para>⚠️ 唯一例外是 <see cref="ScreenSize"/>：<c>(0,0)</c> 的语义是「**屏幕尺寸尚未设置**」，
    /// 内核**不替调用方猜**，调用方必须显式赋值——忘了设也不会产出 <c>NaN</c>，
    /// 而是走 <c>HasCanvasSize == false</c> 那条路径（"画布尺寸无定义"）。</para>
    /// </summary>
    public struct ScaleCalcInput
    {
        // ── ① 选模式面 ─────────────────────────────────────────────
        public ScaleMode Mode;
        public ScreenMatchMode ScreenMatch;
        public float MatchWidthOrHeight;

        // ── ② 尺寸面 ───────────────────────────────────────────────
        public ScaleSize ReferenceResolution;
        public ScaleSize ScreenSize;
        public int TargetDisplay;

        // ── ③ 参数面 ───────────────────────────────────────────────
        public float ScreenDpi;
        public float ConstantScaleFactor;
        public float FallbackScreenDPI;
        public float DefaultSpriteDPI;
        public float ReferencePixelsPerUnit;
        public PhysicalUnit PhysicalUnit;

        // ── ④ 分支位面（没有它们，分支判定只能长在窗口里）────────────
        public bool IsRootCanvas;
        public CanvasRenderMode RenderMode;
        public float DynamicPixelsPerUnit;

        /// <summary>引擎侧 <c>CanvasScaler</c> 新建时的默认值；<see cref="ScreenSize"/> 为「未设置」。</summary>
        public static ScaleCalcInput Default => new ScaleCalcInput
        {
            Mode = ScaleMode.ConstantPixelSize,
            ScreenMatch = ScreenMatchMode.MatchWidthOrHeight,
            MatchWidthOrHeight = 0f,
            ReferenceResolution = new ScaleSize(800f, 600f),
            ScreenSize = new ScaleSize(0f, 0f),
            TargetDisplay = 0,
            ScreenDpi = 0f,
            ConstantScaleFactor = 1f,
            FallbackScreenDPI = 96f,
            DefaultSpriteDPI = 96f,
            ReferencePixelsPerUnit = 100f,
            PhysicalUnit = PhysicalUnit.Points,
            IsRootCanvas = true,
            RenderMode = CanvasRenderMode.ScreenSpaceOverlay,
            DynamicPixelsPerUnit = 1f,
        };
    }
}
