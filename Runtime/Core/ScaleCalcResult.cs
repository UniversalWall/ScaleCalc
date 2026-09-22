using System;

namespace Wayward.ScaleCalc
{
    /// <summary>
    /// 换算结果：**两个引擎会写回的产出字段** + 画布尺寸 + 一组可断言的诊断。
    /// </summary>
    public readonly struct ScaleCalcResult
    {
        /// <summary>走了哪条分支（窗口只消费它）。</summary>
        public readonly ScaleBranch Branch;

        /// <summary>早退① ⇒ <c>false</c>：内核**未产出**可对账的数字。</summary>
        public readonly bool HasResult;

        /// <summary>引擎会写进 <c>Canvas.scaleFactor</c> 的目标值。</summary>
        public readonly float ScaleFactor;

        /// <summary>引擎会写进 <c>Canvas.referencePixelsPerUnit</c> 的目标值（三模式都会写）。</summary>
        public readonly float ReferencePixelsPerUnit;

        /// <summary>画布尺寸是否有定义（WorldSpace / 屏幕尺寸非正 / <c>scaleFactor == 0</c> ⇒ <c>false</c>）。</summary>
        public readonly bool HasCanvasSize;

        /// <summary>= <c>ScreenSize / ScaleFactor</c>，仅在 <see cref="HasCanvasSize"/> 为 <c>true</c> 时有意义。</summary>
        public readonly ScaleSize CanvasSize;

        /// <summary>
        /// **只覆盖内核直接产出的两个字段**（<see cref="ScaleFactor"/> / <see cref="ReferencePixelsPerUnit"/>，
        /// 二者在任何输入下都有限）。画布尺寸是**派生值**，它的定义域另由 <see cref="HasCanvasSize"/> 界定。
        /// </summary>
        public readonly bool IsFinite;

        /// <summary><c>ConstantPhysicalSize</c> 且 <c>ScreenDpi == 0</c> ⇒ <c>true</c>（走了兜底链）。</summary>
        public readonly bool UsedFallbackDpi;

        /// <summary>实际参与算式的 dpi（兜底之后）。</summary>
        public readonly float EffectiveDpi;

        /// <summary>物理单位对应的 targetDPI；其余模式为 <c>0</c>。</summary>
        public readonly float TargetDpi;

        /// <summary>三处夹取是否**真的改过值**（让"输入被夹了"这件事可断言，而不是静默发生）。</summary>
        public readonly bool ClampedReferenceResolution;

        /// <inheritdoc cref="ClampedReferenceResolution"/>
        public readonly bool ClampedScaleFactor;

        /// <inheritdoc cref="ClampedReferenceResolution"/>
        public readonly bool ClampedDefaultSpriteDPI;

        /// <summary>语义别名：1 画布单位 = 多少屏幕像素（与 <see cref="ScaleFactor"/> 同值，仅为可读性）。</summary>
        public float PixelsPerCanvasUnit => ScaleFactor;

        public ScaleCalcResult(
            ScaleBranch branch, bool hasResult, float scaleFactor, float referencePixelsPerUnit,
            bool hasCanvasSize, ScaleSize canvasSize, bool isFinite,
            bool usedFallbackDpi, float effectiveDpi, float targetDpi,
            bool clampedReferenceResolution, bool clampedScaleFactor, bool clampedDefaultSpriteDPI)
        {
            Branch = branch;
            HasResult = hasResult;
            ScaleFactor = scaleFactor;
            ReferencePixelsPerUnit = referencePixelsPerUnit;
            HasCanvasSize = hasCanvasSize;
            CanvasSize = canvasSize;
            IsFinite = isFinite;
            UsedFallbackDpi = usedFallbackDpi;
            EffectiveDpi = effectiveDpi;
            TargetDpi = targetDpi;
            ClampedReferenceResolution = clampedReferenceResolution;
            ClampedScaleFactor = clampedScaleFactor;
            ClampedDefaultSpriteDPI = clampedDefaultSpriteDPI;
        }

        /// <summary>
        /// 早退工厂：**未产出任何可对账的数字**。
        /// <c>IsFinite = true</c> 是"真空真"——两个产出字段根本没产出，不做有限性判定。
        /// </summary>
        public static ScaleCalcResult EarlyExit(ScaleBranch branch) => new ScaleCalcResult(
            branch,
            hasResult: false,
            scaleFactor: 0f,
            referencePixelsPerUnit: 0f,
            hasCanvasSize: false,
            canvasSize: default,
            isFinite: true,
            usedFallbackDpi: false,
            effectiveDpi: 0f,
            targetDpi: 0f,
            clampedReferenceResolution: false,
            clampedScaleFactor: false,
            clampedDefaultSpriteDPI: false);
    }
}
