namespace Wayward.ScaleCalc
{
    /// <summary>
    /// 主入口与公共收尾。**零 Unity 依赖**（由 <c>noEngineReferences: true</c> 由编译器强制，程序集里搜不到 <c>UnityEngine</c>）。
    /// <para>运算顺序与 <c>CanvasScaler</c> 逐语句对齐：不重排、不合并、不"顺手优化"——
    /// 浮点加乘不满足结合律，改一处顺序就会让"逐位一致"这条对账结论失效。
    /// 走 <c>log</c>/<c>pow</c> 的 <c>MatchWidthOrHeight</c> 尤其敏感（那一支只能按 <c>1e-3</c> 比较，做不到逐位）。</para>
    /// </summary>
    public static partial class ScaleCalc
    {
        /// <summary>
        /// 镜像 <c>CanvasScaler.Handle()</c>：两处早退 + 三种 <see cref="ScaleMode"/>。
        /// **不修改调用方传入的输入**（先归一化到内部副本）。
        /// </summary>
        public static ScaleCalcResult Evaluate(in ScaleCalcInput raw)
        {
            Normalized n = Normalize(in raw);
            ScaleCalcInput input = n.Input;

            // 早退①：m_Canvas == null || !m_Canvas.isRootCanvas ⇒ return
            //   （null 由适配层保证：CanvasScaler 带 [RequireComponent(typeof(Canvas))]）
            if (!input.IsRootCanvas)
                return ScaleCalcResult.EarlyExit(ScaleBranch.EarlyExit_NotRootCanvas);

            // 早退②：renderMode == WorldSpace ⇒ HandleWorldCanvas(); return
            if (input.RenderMode == CanvasRenderMode.WorldSpace)
                return EvaluateWorldSpace(in n);

            switch (input.Mode)
            {
                case ScaleMode.ConstantPixelSize: return EvaluateConstantPixelSize(in n);
                case ScaleMode.ScaleWithScreenSize: return EvaluateScaleWithScreenSize(in n);
                case ScaleMode.ConstantPhysicalSize: return EvaluateConstantPhysicalSize(in n);
                default: return ScaleCalcResult.EarlyExit(ScaleBranch.EarlyExit_NotRootCanvas);
            }
        }

        /// <summary>
        /// 三模式共用的收尾：算画布尺寸（定义域外**不产出**、更不产 <c>NaN</c>）、判有限性、带出夹取诊断。
        /// </summary>
        internal static ScaleCalcResult Finish(
            in Normalized n, ScaleBranch branch, float scaleFactor, float refPpu,
            bool usedFallback = false, float effectiveDpi = 0f, float targetDpi = 0f)
        {
            ScaleCalcInput i = n.Input;

            // 画布尺寸的定义域：三条任一成立 ⇒ 不计算
            bool screensPositive = i.ScreenSize.Width > 0f && i.ScreenSize.Height > 0f;
            bool hasCanvas = branch != ScaleBranch.WorldSpace && screensPositive && scaleFactor != 0f;
            ScaleSize canvas = hasCanvas
                ? new ScaleSize(i.ScreenSize.Width / scaleFactor, i.ScreenSize.Height / scaleFactor)
                : default;

            bool finite = IsFinite(scaleFactor) && IsFinite(refPpu);

            return new ScaleCalcResult(
                branch,
                hasResult: true,
                scaleFactor: scaleFactor,
                referencePixelsPerUnit: refPpu,
                hasCanvasSize: hasCanvas,
                canvasSize: canvas,
                isFinite: finite,
                usedFallbackDpi: usedFallback,
                effectiveDpi: effectiveDpi,
                targetDpi: targetDpi,
                clampedReferenceResolution: n.ClampedReferenceResolution,
                clampedScaleFactor: n.ClampedScaleFactor,
                clampedDefaultSpriteDPI: n.ClampedDefaultSpriteDPI);
        }

        /// <summary>有限性判定（<c>NaN</c> / <c>±∞</c> 都不算有限）。</summary>
        internal static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        /// <summary>镜像 <c>Mathf.Clamp01</c>（<c>NaN</c> 原样返回，与引擎一致）。</summary>
        internal static float Clamp01(float v)
        {
            if (v < 0f) return 0f;
            if (v > 1f) return 1f;
            return v;
        }

        /// <summary>镜像 <c>Mathf.Sign</c>：<c>Sign(0) == +1</c>。</summary>
        internal static float Sign(float v) => v < 0f ? -1f : 1f;

        /// <summary>镜像 <c>Mathf.Log(x, 2)</c>。**与 <c>(float)Math.Log((double)x, 2.0)</c> 逐位一致**（21 组样本实测）。</summary>
        internal static float Log2(float x) => (float)System.Math.Log(x, 2.0);

        /// <summary>镜像 <c>Mathf.Pow(2, x)</c>。**与 <c>(float)Math.Pow(2.0, (double)x)</c> 逐位一致**（实测）。</summary>
        internal static float Pow2(float x) => (float)System.Math.Pow(2.0, x);
    }
}
