using System;
using System.Collections.Generic;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// **跨档统计**：一种匹配方式下，参与档位里有几档会裁、最坏裁多少/留多少、
    /// 安全设计区多大；外加 <c>match</c> 推荐值的**目标函数**。
    /// <para>🔴 **同源纪律**：统计**复用** <see cref="ScaleFit.Of"/>（同一个容差、同一套判定），结论条**只吃**
    /// <see cref="ModeStats"/> —— 不许两处各算一遍。</para>
    /// <para>🔴 **"零裁切"不是某个 <c>match</c> 值**：那是 <c>Expand</c> 的结构性保证 ⇒ 它不在
    /// <see cref="Objective"/> 里（把"零裁切"当成可搜索的目标，只会搜出一个假的最优值）。</para>
    /// </summary>
    public static partial class ScaleFit
    {
        /// <summary>一种匹配方式的**跨档统计**（"三种方式并排"那张表的一行）。</summary>
        public readonly struct ModeStats
        {
            public readonly ScreenMatchMode Mode;

            /// <summary>参与档位数（= 传入清单长度）。</summary>
            public readonly int TierCount;

            /// <summary>**任一轴**裁切的档位数。</summary>
            public readonly int CroppedCount;

            /// <summary>0 = 没有会裁的。</summary>
            public readonly float WorstCropRatio;

            /// <summary>档位名；无 ⇒ <c>null</c>。</summary>
            public readonly string WorstCropTier;

            /// <summary>无会裁时该值无意义（沿用 <see cref="Axis.Horizontal"/>）。</summary>
            public readonly Axis WorstCropAxis;

            public readonly float WorstSpareRatio;
            public readonly string WorstSpareTier;
            public readonly Axis WorstSpareAxis;

            /// <summary>= TierCount &gt; 0 **且**全部档位都有画布（**参考可用**也在内）。</summary>
            public readonly bool HasSafeArea;

            /// <summary>逐轴 min(实际画布)；<see cref="HasSafeArea"/> 为 <c>false</c> 时无意义。</summary>
            public readonly ScaleSize SafeArea;

            public ModeStats(ScreenMatchMode mode, int tierCount, int croppedCount,
                             float worstCropRatio, string worstCropTier, Axis worstCropAxis,
                             float worstSpareRatio, string worstSpareTier, Axis worstSpareAxis,
                             bool hasSafeArea, ScaleSize safeArea)
            {
                Mode = mode;
                TierCount = tierCount;
                CroppedCount = croppedCount;
                WorstCropRatio = worstCropRatio;
                WorstCropTier = worstCropTier;
                WorstCropAxis = worstCropAxis;
                WorstSpareRatio = worstSpareRatio;
                WorstSpareTier = worstSpareTier;
                WorstSpareAxis = worstSpareAxis;
                HasSafeArea = hasSafeArea;
                SafeArea = safeArea;
            }
        }

        /// <summary>
        /// 按给定匹配方式算跨档统计。**内核当输入**，本方法不碰场景、不取真值。
        /// <para>参考分辨率无效或某档没有画布 ⇒ 该档**不进聚合**（**不编 0 也不编 ∞**）；
        /// 只要有档位没画布就 <see cref="ModeStats.HasSafeArea"/> = <c>false</c>。</para>
        /// </summary>
        public static ModeStats Stats(IReadOnlyList<ScreenProfile> tiers, ScaleSize reference,
                                      ScreenMatchMode mode, float match, float dpi)
        {
            int count = tiers == null ? 0 : tiers.Count;
            if (count == 0 || !IsUsable(reference))
                return new ModeStats(mode, count, 0, 0f, null, Axis.Horizontal, 0f, null, Axis.Horizontal, false, default);

            int cropped = 0;
            float worstCrop = 0f, worstSpare = 0f;
            string cropTier = null, spareTier = null;
            Axis cropAxis = Axis.Horizontal, spareAxis = Axis.Horizontal;
            bool allCanvas = true;
            float minWidth = float.MaxValue, minHeight = float.MaxValue;

            foreach (ScreenProfile profile in tiers)
            {
                TierFit fit = Of(Evaluate(profile, reference, mode, match, dpi), reference);
                if (!fit.HasCanvas) { allCanvas = false; continue; }

                if (fit.Canvas.Width < minWidth) minWidth = fit.Canvas.Width;
                if (fit.Canvas.Height < minHeight) minHeight = fit.Canvas.Height;
                if (fit.Horizontal.IsCropped || fit.Vertical.IsCropped) cropped++;

                if (fit.Horizontal.CropRatio > worstCrop) { worstCrop = fit.Horizontal.CropRatio; cropTier = profile.Name; cropAxis = Axis.Horizontal; }
                if (fit.Vertical.CropRatio > worstCrop) { worstCrop = fit.Vertical.CropRatio; cropTier = profile.Name; cropAxis = Axis.Vertical; }
                if (fit.Horizontal.SpareRatio > worstSpare) { worstSpare = fit.Horizontal.SpareRatio; spareTier = profile.Name; spareAxis = Axis.Horizontal; }
                if (fit.Vertical.SpareRatio > worstSpare) { worstSpare = fit.Vertical.SpareRatio; spareTier = profile.Name; spareAxis = Axis.Vertical; }
            }

            bool hasSafeArea = allCanvas && minWidth != float.MaxValue;
            return new ModeStats(mode, count, cropped, worstCrop, cropTier, cropAxis, worstSpare, spareTier, spareAxis,
                                 hasSafeArea, hasSafeArea ? new ScaleSize(minWidth, minHeight) : default);
        }

        /// <summary>三种方式并排（<c>Match</c> / <c>Expand</c> / <c>Shrink</c>）——"三选一"小结的数据源。</summary>
        public static ModeStats[] AcrossModes(IReadOnlyList<ScreenProfile> tiers, ScaleSize reference, float match, float dpi)
            => new[]
            {
                Stats(tiers, reference, ScreenMatchMode.MatchWidthOrHeight, match, dpi),
                Stats(tiers, reference, ScreenMatchMode.Expand, match, dpi),
                Stats(tiers, reference, ScreenMatchMode.Shrink, match, dpi),
            };

        /// <summary>推荐值的目标函数（默认"最坏裁切最小"；另一个是"最坏留白最小"）。</summary>
        public enum Objective
        {
            MinWorstCrop,
            MinWorstSpare,
        }

        /// <summary>扫描步长（与滑块的 <c>step</c> 对齐）。</summary>
        public const float MatchStep = 0.01f;

        /// <summary>扫描 <c>t ∈ [0, 1]</c> 取最优；<c>false</c> ⇒ 无参与档位 / 参考不可用 ⇒ **不给推荐**。</summary>
        public static bool TryRecommendMatch(IReadOnlyList<ScreenProfile> tiers, ScaleSize reference, float dpi,
                                             Objective objective, out float recommended, out ModeStats atRecommended)
        {
            recommended = 0f;
            atRecommended = default;
            if (tiers == null || tiers.Count == 0 || !IsUsable(reference)) return false;

            bool found = false;
            for (int i = 0; i <= 100; i++)
            {
                float candidate = i * MatchStep;
                ModeStats stats = Stats(tiers, reference, ScreenMatchMode.MatchWidthOrHeight, candidate, dpi);
                if (!stats.HasSafeArea) continue;
                if (found && !IsBetter(stats, candidate, atRecommended, recommended, objective)) continue;
                recommended = candidate;
                atRecommended = stats;
                found = true;
            }
            return found;
        }

        /// <summary>最优判据：先按目标函数，再按次要量，最后取更小的 <c>t</c>（**同输入同输出**）。</summary>
        private static bool IsBetter(ModeStats candidate, float candidateMatch, ModeStats best, float bestMatch, Objective objective)
        {
            float primaryNow = objective == Objective.MinWorstCrop ? candidate.WorstCropRatio : candidate.WorstSpareRatio;
            float primaryBest = objective == Objective.MinWorstCrop ? best.WorstCropRatio : best.WorstSpareRatio;
            if (primaryNow != primaryBest) return primaryNow < primaryBest;

            float secondaryNow = objective == Objective.MinWorstCrop ? candidate.WorstSpareRatio : candidate.WorstCropRatio;
            float secondaryBest = objective == Objective.MinWorstCrop ? best.WorstSpareRatio : best.WorstCropRatio;
            if (secondaryNow != secondaryBest) return secondaryNow < secondaryBest;
            return candidateMatch < bestMatch;
        }

        /// <summary>该档在该条件下的内核结果（**唯一的调用形态**，统计与逐档共用）。</summary>
        private static ScaleCalcResult Evaluate(ScreenProfile profile, ScaleSize reference,
                                                ScreenMatchMode mode, float match, float dpi)
        {
            ScaleCalcInput input = ScaleCalcInput.Default;
            input.Mode = ScaleMode.ScaleWithScreenSize;
            input.ScreenSize = profile.Size;
            input.ReferenceResolution = reference;
            input.ScreenMatch = mode;
            input.MatchWidthOrHeight = match;
            input.ScreenDpi = dpi;
            return ScaleCalc.Evaluate(in input);
        }
    }
}
