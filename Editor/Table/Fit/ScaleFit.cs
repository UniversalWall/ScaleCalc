using System;
using System.Collections.Generic;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// **逐档拟合**：把"实际画布 vs 参考画布"算成**逐轴**的裁切/留白量。
    /// <para>🔴 两条纪律：① **裁切与留白是两种量，绝不合成一个分**；② 判定**必须带容差**
    /// （否则浮点边界会时红时绿）。像素一律给**单侧**（文案写"左右各 / 上下各"）。</para>
    /// <para>**纯函数、零 Unity 依赖** ⇒ 全部可在 EditMode 断言。本类分册：
    /// 本文件（**几何**：逐档 + 同比例 + 方向对）· `ScaleFit.Text.cs`（**文本**：单元格与比例）·
    /// `ScaleFit.Summary.cs`（**跨档统计** + 推荐值）· `ScaleFit.Headline.cs`（**结论条/小结文案**）·
    /// `ScaleFit.Bar.cs`（字符条）· `ScaleFit.Order.cs`（排序/筛选）·
    /// `ScaleFit.Diagram.cs` / `ScaleFit.CellDiagram.cs` / `ScaleFit.DiagramShape.cs`（示意图几何）。
    /// 拆的判据 = 200 行红线 + 职责各不同。</para>
    /// <para>⚠️ 单轴方法**必须叫 <c>OfAxis</c> 而不是 <c>Axis</c>**：本类另有一个嵌套枚举 <see cref="Axis"/>，
    /// C# 里方法与嵌套类型同名是 CS0102（同一成员声明空间）。枚举名保持不变（<c>ScaleFit.Axis.Horizontal</c> 是公开用法）。</para>
    /// </summary>
    public static partial class ScaleFit
    {
        /// <summary>判定容差（**画布单位**）。0.5px —— ⚠️ 与对账容差 <c>1e-3</c> **量纲不同**，不可混用。</summary>
        public const float Tolerance = 0.5f;

        /// <summary>轴。</summary>
        public enum Axis
        {
            Horizontal,
            Vertical,
        }

        /// <summary>
        /// 单轴拟合。**<see cref="CropRatio"/> 与 <see cref="SpareRatio"/> 不可能同时 &gt; 0**（两种量）。
        /// </summary>
        public readonly struct AxisFit
        {
            /// <summary>实际画布该轴尺寸。</summary>
            public readonly float Canvas;

            /// <summary>参考画布该轴尺寸。</summary>
            public readonly float Reference;

            /// <summary>max(0, (ref − canvas) / ref)。</summary>
            public readonly float CropRatio;

            /// <summary>max(0, (canvas − ref) / ref)。</summary>
            public readonly float SpareRatio;

            /// <summary>三态判定（**复用** <see cref="OverflowVerdict"/>，不另立一套）。</summary>
            public readonly OverflowVerdict Verdict;

            public AxisFit(float canvas, float reference, float cropRatio, float spareRatio, OverflowVerdict verdict)
            {
                Canvas = canvas;
                Reference = reference;
                CropRatio = cropRatio;
                SpareRatio = spareRatio;
                Verdict = verdict;
            }

            public bool IsCropped => Verdict == OverflowVerdict.Cropped;

            public bool IsSpare => Verdict == OverflowVerdict.Spare;

            /// <summary>裁掉的量，**单侧**。</summary>
            public float CropPxPerSide => CropRatio * Reference * 0.5f;

            /// <summary>多出的量，**单侧**。</summary>
            public float SparePxPerSide => SpareRatio * Reference * 0.5f;
        }

        /// <summary>
        /// 单轴拟合（容差内一律算"正好"）。
        /// <para>⚠️ <paramref name="reference"/> ≤ 0 或非有限 ⇒ **调用方不要调它**（那是"无参考"，
        /// 不是"裁 100%"）；本方法仍返回中性值以免 NaN 扩散，但语义无定义。</para>
        /// </summary>
        public static AxisFit OfAxis(float canvas, float reference, float tolerance = Tolerance)
        {
            if (!IsUsable(reference) || !IsUsable(canvas))
                return new AxisFit(canvas, reference, 0f, 0f, OverflowVerdict.Exactly);

            float delta = canvas - reference;
            if (delta > tolerance)
                return new AxisFit(canvas, reference, 0f, delta / reference, OverflowVerdict.Spare);
            if (delta < -tolerance)
                return new AxisFit(canvas, reference, -delta / reference, 0f, OverflowVerdict.Cropped);
            return new AxisFit(canvas, reference, 0f, 0f, OverflowVerdict.Exactly);
        }

        /// <summary>整档拟合。</summary>
        public readonly struct TierFit
        {
            /// <summary>= <see cref="ScaleCalcResult.HasCanvasSize"/>。</summary>
            public readonly bool HasCanvas;

            /// <summary>实际画布（= 屏幕 ÷ scaleFactor）。</summary>
            public readonly ScaleSize Canvas;

            public readonly AxisFit Horizontal;
            public readonly AxisFit Vertical;

            /// <summary>**精确**同比例（见 <see cref="SameAspect"/>）。</summary>
            public readonly bool SameAspect;

            public TierFit(bool hasCanvas, ScaleSize canvas, AxisFit horizontal, AxisFit vertical, bool sameAspect)
            {
                HasCanvas = hasCanvas;
                Canvas = canvas;
                Horizontal = horizontal;
                Vertical = vertical;
                SameAspect = sameAspect;
            }
        }

        /// <summary>整档拟合。**内核产出当输入**，本方法不碰场景、不取真值。</summary>
        public static TierFit Of(in ScaleCalcResult result, ScaleSize reference)
        {
            if (!result.HasCanvasSize)
                return new TierFit(false, default,
                    OfAxis(0f, reference.Width), OfAxis(0f, reference.Height), false);

            ScaleSize canvas = result.CanvasSize;
            return new TierFit(
                true, canvas,
                OfAxis(canvas.Width, reference.Width),
                OfAxis(canvas.Height, reference.Height),
                SameAspect(canvas, reference));
        }

        /// <summary>
        /// **精确**同比例：`|屏宽×参考高 − 屏高×参考宽| &lt; 1`（**绝对阈值**、整数乘积）。
        /// <para>🔴 **不许**放宽成相对阈值：`1921×1081` 看着像 16:9，但**必须判不同比例**。</para>
        /// </summary>
        public static bool SameAspect(ScaleSize screen, ScaleSize reference)
        {
            if (!IsUsable(screen.Width) || !IsUsable(screen.Height)
                || !IsUsable(reference.Width) || !IsUsable(reference.Height)) return false;
            return Math.Abs(screen.Width * reference.Height - screen.Height * reference.Width) < 1f;
        }

        /// <summary>
        /// **方向对**：同像素数且宽高互换 ⇒ 同一台设备的两个方向（方屏自成一对，不算方向对）。
        /// <para>呈现落点 = 「比例」列后缀 <c>⇄</c>。**判定范围 = 当前参与档位清单**：
        /// 它要防的是"把同一台设备的两档当两台设备统计"。</para>
        /// </summary>
        public static bool AreOrientationPair(ScreenProfile a, ScreenProfile b)
        {
            if (!IsUsable(a.Size.Width) || !IsUsable(a.Size.Height)) return false;
            if (a.Size.Width == a.Size.Height) return false;                 // 方屏没有"两个方向"
            return a.Size.Width == b.Size.Height && a.Size.Height == b.Size.Width;
        }

        /// <summary>
        /// 清单里有没有"方向对"（**当前参与清单**，不是内置全表）：装配时算一次，写在
        /// <see cref="ScaleTableRow.IsOrientationPair"/> 上——那是唯一拿得到整份清单的地方。
        /// </summary>
        public static bool HasOrientationPair(ScreenProfile profile, IReadOnlyList<ScreenProfile> list)
        {
            if (list == null) return false;
            for (int i = 0; i < list.Count; i++)
            {
                ScreenProfile other = list[i];
                if (other.Size != profile.Size && AreOrientationPair(profile, other)) return true;
            }
            return false;
        }

        /// <summary>
        /// **对外单档入口**（窗口的字符条与示意图要用）：把"档位 + 条件"直接算成 <see cref="TierFit"/>。
        /// <para>与 <see cref="Stats"/> 走**同一个求值门**（<c>Evaluate</c>）⇒ 逐档读数与跨档统计永远同源。</para>
        /// </summary>
        public static TierFit OfTier(ScreenProfile profile, ScaleSize reference,
                                    ScreenMatchMode mode, float match, float dpi)
        {
            ScaleCalcResult result = Evaluate(profile, reference, mode, match, dpi);
            return Of(in result, reference);
        }

        /// <summary>有限且 &gt; 0（"这个数能不能参与几何判定"的唯一口径）。</summary>
        public static bool IsUsable(float value)
            => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;

        /// <summary>参考画布可用（两轴都要可用）——量值列与结论条的第一道门。</summary>
        public static bool IsUsable(ScaleSize reference)
            => IsUsable(reference.Width) && IsUsable(reference.Height);
    }
}
