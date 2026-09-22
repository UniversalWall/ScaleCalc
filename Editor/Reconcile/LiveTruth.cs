using System.Globalization;
using Wayward.ScaleCalc.Unity;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>一次现场读数（引擎真值 + 采集到的输入源 + **测量条件**）。</summary>
    public readonly struct LiveTruth
    {
        public readonly float ScaleFactor;
        public readonly float ReferencePixelsPerUnit;
        public readonly ScaleSize CanvasSize;
        public readonly ScaleSize RenderingDisplaySize;
        public readonly ScaleSize ScreenSize;
        public readonly ScreenSizeSource Source;

        /// <summary>**测量条件**之一：这次对账用的是哪个参考分辨率（脱离条件的真值不能拿去减别的参考块）。</summary>
        public readonly ScaleSize MeasuredReference;

        /// <summary>**测量条件**之二：这次对账用的 <c>matchWidthOrHeight</c>。</summary>
        public readonly float MeasuredMatch;

        /// <summary>
        /// **测量条件**之三：这次对账用的是哪种匹配方式。
        /// <para>🔴 缺了它，用户在 <c>Match</c> 下对账后把方式切到 <c>Expand</c>，滑块仍是 0.5 ⇒ 守门照样为真 ⇒
        /// 表按 <c>Expand</c> 算、真值却是 <c>Match</c> 下的 ⇒ **假差值**（条件从"参考"变成了"匹配方式"）。</para>
        /// </summary>
        public readonly ScreenMatchMode MeasuredScreenMatch;

        public LiveTruth(float scaleFactor, float referencePixelsPerUnit, ScaleSize canvasSize,
                         ScaleSize renderingDisplaySize, ScaleSize screenSize, ScreenSizeSource source,
                         ScaleSize measuredReference = default, float measuredMatch = 0f,
                         ScreenMatchMode measuredScreenMatch = ScreenMatchMode.MatchWidthOrHeight)
        {
            ScaleFactor = scaleFactor;
            ReferencePixelsPerUnit = referencePixelsPerUnit;
            CanvasSize = canvasSize;
            RenderingDisplaySize = renderingDisplaySize;
            ScreenSize = screenSize;
            Source = source;
            MeasuredReference = measuredReference;
            MeasuredMatch = measuredMatch;
            MeasuredScreenMatch = measuredScreenMatch;
        }

        /// <summary>
        /// 这份真值是否适用于该（参考分辨率 × match × **匹配方式**）条件（守门人）。
        /// <para>🔴 **默认构造的真值永不适用**：构造参数带默认值 ⇒ 漏改的调用点**编译不报错**，
        /// 所以判据必须自己兜住（<c>MeasuredReference</c> 为零 ⇒ 直接 <c>false</c>）。</para>
        /// <para><c>Expand</c>/<c>Shrink</c> **不读 <c>match</c>**（内核 <c>switch</c> 根本不看它）⇒ 只比"参考 × 方式"。</para>
        /// </summary>
        public bool AppliesTo(ScaleSize reference, float matchWidthOrHeight, ScreenMatchMode screenMatch)
        {
            if (!(MeasuredReference.Width > 0f && MeasuredReference.Height > 0f)) return false;
            if (MeasuredReference != reference || MeasuredScreenMatch != screenMatch) return false;
            if (screenMatch != ScreenMatchMode.MatchWidthOrHeight) return true;
            return MeasuredMatch == matchWidthOrHeight;
        }

        /// <summary>测量条件的一句话（<c>Match</c> 带 match 值，<c>Expand</c>/<c>Shrink</c> 不带）。</summary>
        public string ConditionText
            => ConditionTextOf(MeasuredReference, MeasuredScreenMatch, MeasuredMatch);

        /// <summary>
        /// **条件文案的唯一来源**（<see cref="ConditionText"/> 与 <see cref="ScaleTable.TruthNote"/> 的"**本表条件**"都走它）：
        /// 两处各写一套就会在切 <c>Expand</c> 之后**自相矛盾**（表明明不读 <c>match</c>，却写着 <c>match 0.50</c>）。
        /// </summary>
        public static string ConditionTextOf(ScaleSize reference, ScreenMatchMode mode, float match)
            => reference + " × " + ModeText(mode)
             + (mode == ScreenMatchMode.MatchWidthOrHeight
                 ? " " + match.ToString("0.00", CultureInfo.InvariantCulture)
                 : string.Empty);

        /// <summary>匹配方式的一句话（与 <see cref="ConditionTextOf"/> **共用**，免得两处各写一套）。</summary>
        public static string ModeText(ScreenMatchMode mode)
            => mode == ScreenMatchMode.MatchWidthOrHeight ? "Match" : mode.ToString();
    }
}
