using System;
using System.Globalization;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// **逐档呈现的文本**：单元格、实际画布、比例列。
    /// <para>🔴 三条口径：**px 一律单侧**（文案写"左右各 / 上下各"）· **百分比 1 位小数** ·
    /// **无定义就给标记**（<c>—</c> / <c>（无画布尺寸）</c>，**不编 0**）。</para>
    /// <para>纯字符串 ⇒ 可在 EditMode 逐字断言。几何判定在 `ScaleFit.cs`，跨档统计在 `ScaleFit.Summary.cs`。</para>
    /// </summary>
    public static partial class ScaleFit
    {
        /// <summary>
        /// 单元格文本：`裁 −13.4%（左右各 −128px）` / `留 +15.5%（上下各 +84px）` / `正好`。
        /// <para>参考分辨率无效 ⇒ <c>—</c>（**不编 0、不编 ∞**）；画布无定义 ⇒ <c>（无画布尺寸）</c>。</para>
        /// </summary>
        public static string Cell(in TierFit fit, Axis axis)
        {
            AxisFit value = axis == Axis.Horizontal ? fit.Horizontal : fit.Vertical;
            // 🔴 参考"无效"是**整列**的事，不是单轴的事（参考 ≤ 0 / 非有限 ⇒ 一律显示 —）
            if (!IsUsable(fit.Horizontal.Reference) || !IsUsable(fit.Vertical.Reference)) return "—";
            if (!fit.HasCanvas) return "（无画布尺寸）";

            string side = axis == Axis.Horizontal ? "左右各" : "上下各";
            if (value.IsCropped)
                return "裁 −" + Percent1(value.CropRatio)
                     + "（" + side + " −" + value.CropPxPerSide.ToString("F0", CultureInfo.InvariantCulture) + "px）";
            if (value.IsSpare)
                return "留 +" + Percent1(value.SpareRatio)
                     + "（" + side + " +" + value.SparePxPerSide.ToString("F0", CultureInfo.InvariantCulture) + "px）";
            return "正好";
        }

        /// <summary>
        /// 1 位小数百分比（**手拼 <c>%</c>，不用 <c>P1</c> 格式**）。
        /// <para>🔴 两个理由：① .NET 的 <c>P</c> 格式**自己会乘 100**（先乘一次再套 <c>P1</c> ⇒ 数值翻一百倍）；
        /// ② 它还会在数字与 <c>%</c> 之间插一个空格、并按千位分组——与要的
        /// <c>裁 −13.4%（左右各 −128px）</c> 逐字不符。</para>
        /// </summary>
        public static string Percent1(float ratio)
            => (ratio * 100f).ToString("0.0", CultureInfo.InvariantCulture) + "%";

        /// <summary>实际画布文本：`1663×1247`；无画布 ⇒ `（无画布尺寸）`。</summary>
        public static string CanvasText(in TierFit fit)
            => fit.HasCanvas ? SizeText(fit.Canvas) : "（无画布尺寸）";

        /// <summary>
        /// 表格上方的**口径图例**：把"灰字 = 离线列"这条语义从 CSS 类里搬到明面上。
        /// <para>来由：**不许假装整张表都对过账**。实现本来是诚实的（灰字 + 差值列写
        /// 「未经引擎对账（离线列）」），但**表头一声不吭** ⇒ 读者第一眼会以为每列都过了引擎对账。</para>
        /// <para>纯字符串 ⇒ 可逐字断言；窗口那一行**只读本函数**，不在 `.uxml` 里另抄一份字面量。</para>
        /// <para>🔴 **还承担「裁留图」的读图约定**：图格里**没有文字**（行高 20~30px
        /// 装不下），而"哪块是画布、哪块是参考、红黄各是什么意思"是**约定**不是自明的 ⇒ 必须有一处明说。
        /// 放这里而不是列头：列头只有 3 字、挤不下，而这一行本来就常显。
        /// **必须仍是一行**（窗口底线 900 时不许换行占高，那会顶破高度预算）。</para>
        /// </summary>
        public static string TableLegend()
            => "灰字 = 内核离线算的（未经引擎对账）；真值只对「现场对账」测过的那一行成立"
             + "；裁留图：灰框=参考画布 · 蓝块=本档画布 · 红=被裁 · 黄=留白";

        /// <summary>比例列的文本：`16:9` · 同比例带 `✅同比例` · 方向对带 `⇄`（不另立列——列集固定 8 列）。</summary>
        public static string AspectText(ScreenProfile profile, bool sameAspect, bool orientationPair = false)
        {
            string text = RatioText(profile.AspectRatio);
            if (sameAspect) text += " ✅同比例";
            if (orientationPair) text += " ⇄";
            return text;
        }

        /// <summary>
        /// 宽高比文本：**由像素算**的最小整数比（`1920×1080 → 16:9`、`2048×1536 → 4:3`、`2560×1080 → 64:27`），
        /// 试不出就退回小数比（`aspect:1`）。**纯函数**，同输入同输出。
        /// <para>🔴 **不许从档位名里取比例**：档位名可以被用户改，而且内置 7 档里**就有两档的名字与像素比不一致**
        /// ——`超宽 21:9` 实为 **64:27**、`手机竖屏超长 9:19.5` 实为 **6:13**。列上显示的是**这台设备的真实像素比**，
        /// 与名字的出入由两列并排如实呈现。</para>
        /// </summary>
        public static string RatioText(float aspect)
        {
            if (!IsUsable(aspect)) return "—";

            for (int a = 1; a <= 128; a++)
            {
                float b = a / aspect;
                if (!IsUsable(b)) continue;
                float rounded = (float)Math.Round(b);
                if (rounded < 1f || Math.Abs(b - rounded) > 1e-3f) continue;
                return a.ToString(CultureInfo.InvariantCulture) + ":"
                     + rounded.ToString("0", CultureInfo.InvariantCulture);
            }
            return aspect.ToString("0.###", CultureInfo.InvariantCulture) + ":1";
        }

        /// <summary>
        /// 尺寸文本（新列/结论条用 <c>×</c>）：**取整到整数 px**。
        /// <para>为什么取整：现场读数就是整数（`978×935` / `1663×1247` / `1527×851`），而实际算出来是
        /// 小数（`978.287×935.307`）——**读的人要的是"多大的区"，不是浮点尾巴**。结论条与表格**共用本函数**。</para>
        /// </summary>
        public static string SizeText(ScaleSize size)
            => size.Width.ToString("F0", CultureInfo.InvariantCulture) + "×"
             + size.Height.ToString("F0", CultureInfo.InvariantCulture);
    }
}
