using System;

namespace Wayward.ScaleCalc
{
    public static partial class ScaleCalc
    {
        /// <summary>
        /// 早退②：世界空间画布。**不读渲染尺寸** ⇒ 不需要渲染就能对账（这是它能在 EditMode 里断言的原因）。
        /// </summary>
        private static ScaleCalcResult EvaluateWorldSpace(in Normalized n) => Finish(
            in n,
            ScaleBranch.WorldSpace,
            scaleFactor: n.Input.DynamicPixelsPerUnit,        // = SetScaleFactor(m_DynamicPixelsPerUnit)
            refPpu: n.Input.ReferencePixelsPerUnit);          // = SetReferencePixelsPerUnit(m_ReferencePixelsPerUnit)

        /// <summary>常量像素模式：直接吐 <c>m_ScaleFactor</c>。</summary>
        private static ScaleCalcResult EvaluateConstantPixelSize(in Normalized n) => Finish(
            in n,
            ScaleBranch.ConstantPixelSize,
            scaleFactor: n.Input.ConstantScaleFactor,         // = SetScaleFactor(m_ScaleFactor)，已夹 ≥ 0.01
            refPpu: n.Input.ReferencePixelsPerUnit);

        /// <summary>
        /// 随屏幕尺寸缩放（**唯一依赖渲染尺寸的分支**）。三种 <see cref="ScreenMatchMode"/> 的差别全在这个 switch 里。
        /// </summary>
        private static ScaleCalcResult EvaluateScaleWithScreenSize(in Normalized n)
        {
            ScaleCalcInput i = n.Input;

            // 两轴比值：float 除法，顺序与引擎一致（screen / reference，不交换）
            float ratioW = i.ScreenSize.Width / i.ReferenceResolution.Width;
            float ratioH = i.ScreenSize.Height / i.ReferenceResolution.Height;

            float scaleFactor;
            switch (i.ScreenMatch)
            {
                case ScreenMatchMode.MatchWidthOrHeight:
                    // 输入域守卫：屏幕尺寸或参考分辨率非正 ⇒ 比值非正 ⇒ 落在对数定义域之外。
                    // 引擎在这一角会把 NaN 写进 Canvas.scaleFactor；内核**不产 NaN**，
                    // 而是产出 0（"未产出有效缩放"）——这是登记过的合法差异。
                    if (ratioW <= 0f || ratioH <= 0f)
                    {
                        scaleFactor = 0f;
                        break;
                    }

                    // 对数空间加权：这就是"match=0.5 是几何平均而不是算术平均"的全部实现。
                    // 反例：960×2160（一轴减半、一轴翻倍）正确结果是 1.0，算术平均会给 1.25。
                    float logW = Log2(ratioW);
                    float logH = Log2(ratioH);
                    float t = Clamp01(i.MatchWidthOrHeight);          // 引擎在 Mathf.Lerp 内部夹 t
                    float weighted = logW + (logH - logW) * t;        // 与 Mathf.Lerp(logW, logH, t) 同序
                    scaleFactor = Pow2(weighted);                     // 2^weighted
                    break;

                case ScreenMatchMode.Expand:                          // 取小 ⇒ 画布只增不减（宁可留白）
                    scaleFactor = Math.Min(ratioW, ratioH);
                    break;

                case ScreenMatchMode.Shrink:                          // 取大 ⇒ 画布只减不增（宁可裁切）
                    scaleFactor = Math.Max(ratioW, ratioH);
                    break;

                default:                                              // 不可达（枚举已穷尽）
                    scaleFactor = 0f;
                    break;
            }

            return Finish(in n, ScaleBranch.ScaleWithScreenSize, scaleFactor, i.ReferencePixelsPerUnit);
        }

        /// <summary>
        /// 常量物理尺寸：**兜底链** + <c>referencePixelsPerUnit</c> 改写。
        /// <para>兜底链是"静默失真"：<c>Screen.dpi == 0</c> 时退化为 <c>fallbackScreenDPI</c>（默认 96），
        /// 不报错、也不回退到别的模式；<c>UsedFallbackDpi</c> 就是给探针/窗口/证据打这个标记用的。</para>
        /// </summary>
        private static ScaleCalcResult EvaluateConstantPhysicalSize(in Normalized n)
        {
            ScaleCalcInput i = n.Input;

            float currentDpi = i.ScreenDpi;
            bool fallback = currentDpi == 0f;                         // 精确比较 0，与引擎一致
            float dpi = fallback ? i.FallbackScreenDPI : currentDpi;

            float targetDPI = TargetDpiOf(i.PhysicalUnit);            // 2.54 / 25.4 / 1 / 72 / 6

            float scaleFactor = dpi / targetDPI;                      // = SetScaleFactor(dpi / targetDPI)
            float refPpu = i.ReferencePixelsPerUnit * targetDPI / i.DefaultSpriteDPI;
            //              ↑ 严格按引擎书写顺序左结合：先乘 targetDPI，再除 defaultSpriteDPI

            return Finish(in n, ScaleBranch.ConstantPhysicalSize, scaleFactor, refPpu,
                          usedFallback: fallback, effectiveDpi: dpi, targetDpi: targetDPI);
        }

        /// <summary>物理单位 → targetDPI（镜像引擎的常量表）。</summary>
        internal static float TargetDpiOf(PhysicalUnit unit)
        {
            switch (unit)
            {
                case PhysicalUnit.Centimeters: return 2.54f;
                case PhysicalUnit.Millimeters: return 25.4f;
                case PhysicalUnit.Inches: return 1f;
                case PhysicalUnit.Points: return 72f;
                case PhysicalUnit.Picas: return 6f;
                default: return 1f;
            }
        }
    }
}
