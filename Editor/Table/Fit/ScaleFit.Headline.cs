using System.Collections.Generic;
using System.Globalization;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// **结论条与"三选一"小结的文案**。**只吃 <see cref="ModeStats"/>**，
    /// 不自己再算一遍 —— 所以表里的数与条上的数**永远同源**。
    /// <para>🔴 三条硬要求：① 结论条**必须**含逐字限定语「结论只对当前勾选的 N 档负责」；② 参考无效 /
    /// 0 档 / 全无画布**各有专门文案，都不编数**；③ 推荐值**必须连代价一起报**
    /// （只报"裁切降一半"而**不报**"留白翻倍"就是只报好消息）。</para>
    /// </summary>
    public static partial class ScaleFit
    {
        /// <summary>
        /// 顶部结论条（**与表格同源**）。
        /// <para><paramref name="builtInDraftCount"/> = 清单里**内置草案档**的个数（&gt; 0 时如实标注）；
        /// 参考不可用 / 0 档 / 全无画布三态各有专门文案（**都不编数**）。</para>
        /// </summary>
        public static string Headline(ModeStats stats, ScaleSize reference, int builtInDraftCount = 0)
        {
            if (!IsUsable(reference)) return "参考分辨率无效（≤ 0 或非有限）⇒ 量值与结论一律显示 —";
            if (stats.TierCount == 0) return "当前 0 档参与计算 ⇒ 无结论（上表可勾回）";
            if (!stats.HasSafeArea) return "参与 " + Number(stats.TierCount) + " 档 · 全部档位都没有画布尺寸 ⇒ 无结论";

            string head = stats.CroppedCount > 0
                ? "参与 " + Number(stats.TierCount) + " 档 · " + Number(stats.CroppedCount) + " 档会裁 · 最坏：" + stats.WorstCropTier
                  + " " + AxisText(stats.WorstCropAxis) + " −" + Percent(stats.WorstCropRatio)
                  + "（" + SideText(stats.WorstCropAxis) + " −" + Pixels(stats.WorstCropRatio, reference, stats.WorstCropAxis) + "px）"
                : "参与 " + Number(stats.TierCount) + " 档 · 无档位会裁（最多留白 +" + Percent(stats.WorstSpareRatio) + "）";

            string safeArea = " · 安全设计区 " + SizeText(stats.SafeArea)
                + "（参考的 " + Ratio(stats.SafeArea.Width / reference.Width) + "×" + Ratio(stats.SafeArea.Height / reference.Height) + "）";
            string scope = " · 结论只对当前勾选的 " + Number(stats.TierCount) + " 档负责";
            string draft = builtInDraftCount > 0 ? "（含 " + Number(builtInDraftCount) + " 档内置草案）" : string.Empty;
            return head + safeArea + scope + draft;
        }

        /// <summary>
        /// 「匹配方式对比（三选一）」里的一行（"三选一"由标题承担，这里只出行）：
        /// `Match 0.5：会裁 6/7 · 最坏 −49.0% · 留白 +96.3% · 安全设计区 978×935`。
        /// <para>🔴 术语一律**全称**「安全设计区」（同词纪律；测试用"简称出现次数 == 全称出现次数"守它）。</para>
        /// </summary>
        public static string ModeLine(ModeStats stats, float match)
        {
            string label = stats.Mode == ScreenMatchMode.MatchWidthOrHeight
                ? "Match " + match.ToString("0.00", CultureInfo.InvariantCulture)
                : stats.Mode.ToString();

            string cropped = CropCountText(stats);
            string worst = stats.CroppedCount > 0 ? "最坏 −" + Percent(stats.WorstCropRatio) : "无裁切";
            if (!stats.HasSafeArea) return label + "：" + cropped + " · " + worst + " · 安全设计区 —";
            return label + "：" + cropped + " · " + worst
                 + " · 留白 +" + Percent(stats.WorstSpareRatio)
                 + " · 安全设计区 " + SizeText(stats.SafeArea);
        }

        /// <summary>
        /// 计数文案：<see cref="ModeLine"/> 与推荐行**共用同一处拼法** ⇒ 两行的「会裁 X/N」不可能漂移。
        /// </summary>
        private static string CropCountText(ModeStats stats)
            => "会裁 " + Number(stats.CroppedCount) + "/" + Number(stats.TierCount);

        /// <summary>
        /// <see cref="Objective"/> 的逐字文案。未知值**如实打枚举名**，不静默产出空串。
        /// </summary>
        private static string ObjectiveText(Objective objective)
        {
            switch (objective)
            {
                case Objective.MinWorstCrop: return "最坏裁切最小";
                case Objective.MinWorstSpare: return "最坏留白最小";
                default: return objective.ToString();
            }
        }

        /// <summary>
        /// 推荐值文案：
        /// `推荐 match 0.17（目标：最坏裁切最小 · 会裁 6/7 · 最坏裁切 21.2% · 留白代价 +206.3% · 安全设计区 1527×851）`。
        /// <para>「会裁 → 最坏 → 留白 → 安全设计区」的**字段顺序与 <see cref="ModeLine"/> 逐字对齐** ⇒ 两行可上下对照读。</para>
        /// <para>🔴 <b>不设默认参数</b>：调用方必须把**同一个**目标变量同时喂给扫描与这里，否则"扫的目标"与"标注的目标"会不一致。</para>
        /// <para>前置 = <see cref="ModeStats.HasSafeArea"/> 且 <see cref="ModeStats.TierCount"/> &gt; 0（<see cref="TryRecommendMatch"/> 的不变量）；
        /// 不满足时**不编数**，走两条守卫文案。</para>
        /// </summary>
        public static string RecommendationText(ModeStats atRecommended, float recommended, Objective objective)
        {
            string head = "推荐 match " + recommended.ToString("0.00", CultureInfo.InvariantCulture);
            if (atRecommended.TierCount == 0) return head + "（不适用：当前 0 档参与计算）";
            if (!atRecommended.HasSafeArea) return head + "（不适用：全部档位都没有画布尺寸）";

            return head + "（目标：" + ObjectiveText(objective)
                 + " · " + CropCountText(atRecommended)
                 + " · 最坏裁切 " + Percent(atRecommended.WorstCropRatio)
                 + " · 留白代价 +" + Percent(atRecommended.WorstSpareRatio)
                 + " · 安全设计区 " + SizeText(atRecommended.SafeArea) + "）";
        }

        /// <summary>清单里**内置草案档**的个数（来源列写 <see cref="ScreenProfiles.DraftSource"/> 的那些）——结论条如实标注读它。</summary>
        public static int BuiltInDraftCount(IReadOnlyList<ScreenProfile> tiers)
        {
            if (tiers == null) return 0;
            int count = 0;
            for (int i = 0; i < tiers.Count; i++)
                if (tiers[i].Source == ScreenProfiles.DraftSource) count++;
            return count;
        }

        /// <summary>《设计约束单》的"安全设计区"那一行（与 <see cref="Headline"/> **同源**；无结论时如实写 `—`）。</summary>
        public static string SafeAreaLine(ModeStats stats, ScaleSize reference)
        {
            if (!stats.HasSafeArea || !IsUsable(reference)) return "安全设计区：—（无结论）";
            return "安全设计区：" + SizeText(stats.SafeArea)
                 + "（参考的 " + Ratio(stats.SafeArea.Width / reference.Width)
                 + "×" + Ratio(stats.SafeArea.Height / reference.Height) + "）";
        }

        /// <summary>《设计约束单》的"危险带"那一行：参考相对安全区**被裁掉的边缘宽度**，**单侧**。</summary>
        public static string BandLine(ModeStats stats, ScaleSize reference)
        {
            if (!stats.HasSafeArea || !IsUsable(reference)) return "危险带（左右各 / 上下各）：—（无结论）";
            float left = (reference.Width - stats.SafeArea.Width) * 0.5f;
            float top = (reference.Height - stats.SafeArea.Height) * 0.5f;
            // `Expand` 下安全区 ≥ 参考 ⇒ 危险带为 0：如实说"无"，**不写 `−0px`**（那看着像"裁了 0 像素"）
            if (left <= 0.5f && top <= 0.5f) return "危险带（左右各 / 上下各）：无（安全设计区 = 参考画布）";
            return "危险带（左右各 / 上下各）：左右各 −" + Pixels2(left) + "px · 上下各 −" + Pixels2(top) + "px";
        }

        private static string Pixels2(float value) => value.ToString("F0", CultureInfo.InvariantCulture);

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

        /// <summary>1 位小数百分比（**手拼**，理由见 <see cref="Percent1"/>：`P` 格式会再乘一次 100 并插空格）。</summary>
        private static string Percent(float ratio) => Percent1(ratio);

        /// <summary>整数百分比（安全区占参考的比例；同样手拼，见 <see cref="Percent1"/>）。</summary>
        private static string Ratio(float value) => (value * 100f).ToString("0", CultureInfo.InvariantCulture) + "%";

        private static string AxisText(Axis axis) => axis == Axis.Horizontal ? "横向" : "纵向";

        private static string SideText(Axis axis) => axis == Axis.Horizontal ? "左右各" : "上下各";

        private static string Pixels(float ratio, ScaleSize reference, Axis axis)
        {
            float referenceSide = axis == Axis.Horizontal ? reference.Width : reference.Height;
            return (ratio * referenceSide * 0.5f).ToString("F0", CultureInfo.InvariantCulture);
        }
    }
}
