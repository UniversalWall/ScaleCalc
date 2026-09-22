namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// **逐档裁留示意图的几何**。
    /// <para>🔴 **现行画法 =「同一比例尺 + 双色带」**。它替换掉的那一版有三条硬伤，**都有读数**：</para>
    /// <list type="number">
    /// <item>🔴 **"作画区 = 参考框"是错的**：作画区 96×22 的宽高比 **4.36**，而参考画布是 16:9（**1.78**）
    /// ⇒ 那个"外框"根本不是参考画布的形状；</item>
    /// <item>🔴 **"画布 &gt; 参考"的轴在 26px 的行里装不下**：需要 <c>1.78 × 22 = 39px</c> ⇒ 要么出血到邻行
    /// （实测内框 <c>y 573.3~619.3</c> 而行只有 <c>583.3~609.3</c>），要么把留白夹掉——后者正是跨档图犯过的错法；</item>
    /// <item>🔴 **一格只有一种颜色**（红=裁 / 黄=留）：<c>手机竖屏 9:16</c> 是"左右被裁 **+** 上下留白"⇒ 留白被吃掉。</item>
    /// </list>
    /// <para>🔴 **现行口径**：坐标系 = **信封**（参考框与画布框的**逐轴并集**）⇒ 两框**同一比例尺**、
    /// **都 ⊆ 信封** ⇒ 图**永不出血**、宽高比**真实**（装配层按 <see cref="DiagramColumnLayout.EnvelopeAspect"/> 定作画区像素尺寸，**绝不许拉伸**）；
    /// 两框的**差**按轴画成**色带**（**裁 = 红**：参考有画布没有 · **留 = 黄**：画布有参考没有）
    /// ⇒ **裁与留能在同一格里同时看见**。裁/留的判定**复用既有逐轴结论**（<see cref="AxisFit.IsCropped"/>）·
    /// 两轴文字**逐字复用** <see cref="Cell"/>，**不自己重判、不另写文案**。</para>
    /// <para>🔴 **与结论区那张图不是一回事**：那张图的内框 = **安全设计区**（各档画布的**交集**，
    /// 永远 ≤ 参考）；本图的画布框 = **本档实际画布**。两张图表观同形、语义不同 ⇒ 文案必须点名「本档画布」，
    /// 且**不许**出现「安全设计区」。</para>
    /// <para>值对象与布局结构（<see cref="DiagramRect"/> / <see cref="DiagramBand"/> /
    /// <see cref="DiagramColumnLayout"/> / <see cref="DiagramColumnLayoutBox"/>）住在
    /// `ScaleFit.DiagramShape.cs`（红线拆分）。</para>
    /// </summary>
    public static partial class ScaleFit
    {
        /// <summary>无画布时的统一标记（与量值列的"不许编 0"口径同族）。</summary>
        public const string NoCanvasMarker = "（无画布尺寸）";

        /// <summary>图列的悬停里那句限定语（几何口径 ≠ 布局承诺）。</summary>
        public const string DiagramLimiter = "只画几何，不承诺具体 UI 不被裁";

        /// <summary>该轴比例 = 画布 ÷ 参考（任一不可用 ⇒ 1，即"无差别"）。</summary>
        private static float RatioOf(float canvas, float reference)
            => IsUsable(canvas) && IsUsable(reference) ? canvas / reference : 1f;

        /// <summary>在信封里**居中**放一个 `width × height` 的框（传入值必须 ≤ 1 ⇒ 结果 ⊆ [0,1]）。</summary>
        private static DiagramRect Centred(float width, float height)
            => new DiagramRect((1f - width) * 0.5f, (1f - height) * 0.5f, width, height);

        /// <summary>一条差带（退化 ⇒ <c>Present == false</c>；**零尺寸的带不许画**，否则会留下 1px 脏线）。</summary>
        private static DiagramBand Band(bool crop, float left, float top, float width, float height)
            => width > 1e-5f && height > 1e-5f
                ? new DiagramBand(true, crop, new DiagramRect(left, top, width, height))
                : default;

        /// <summary>
        /// 按**已算好的整档拟合**求逐档图布局。**只读 <see cref="TierFit"/>**（与「横向」/「纵向」两列**同一个对象**）
        /// ——本函数**不许**再调求值门。
        /// </summary>
        public static DiagramColumnLayout DiagramColumnOf(in TierFit fit)
        {
            // 参考的**真实像素**：信封宽高比必须乘回它（归一化坐标里"参考 = 1×1"并不是 16:9）。
            float referenceWidth = fit.Horizontal.Reference;
            float referenceHeight = fit.Vertical.Reference;

            if (!fit.HasCanvas)
            {
                // 无画布 ⇒ **只画参考框**（信封 = 参考 = 满格），不编画布框、不编色带（与量值列"不许编 0"同族）。
                // 🔴 信封宽高比仍取**参考的**：没有画布也要画成参考的形状，不许拍一个 1:1
                //    （否则那个"参考框"又是假形状——初版 96×22 就是这么错的）。
                float refAspect = IsUsable(referenceWidth) && IsUsable(referenceHeight) && referenceHeight > 0f
                    ? referenceWidth / referenceHeight : 1f;

                return new DiagramColumnLayout(false, false,
                    new DiagramRect(0f, 0f, 1f, 1f), new DiagramRect(0f, 0f, 0f, 0f),
                    default, default, default, default, refAspect,
                    NoCanvasMarker, NoCanvasMarker,
                    "本档没有画布尺寸可比（参考分辨率无效或屏幕尺寸未设置）⇒ 只画参考框\n" + DiagramLimiter);
            }

            float width = RatioOf(fit.Canvas.Width, fit.Horizontal.Reference);
            float height = RatioOf(fit.Canvas.Height, fit.Vertical.Reference);

            // 🔴 **信封 = 逐轴并集**（`max(参考, 画布)`）：两框摆进同一个坐标系、**同一比例尺** ⇒
            //    既不用"允许内框超出 [0,1]"（出血），也不用夹回 1（把留白删掉）。
            float envelopeWidth = System.Math.Max(1f, width);
            float envelopeHeight = System.Math.Max(1f, height);

            DiagramRect reference = Centred(1f / envelopeWidth, 1f / envelopeHeight);
            DiagramRect canvas = Centred(width / envelopeWidth, height / envelopeHeight);

            // 差带（每轴最多两条）：**参考有、画布没有 ⇒ 裁（红）** · **画布有、参考没有 ⇒ 留（黄）**。
            // 带的另一维取"存在那一方"的跨度——这样带子的形状就是"多出来/少掉的那块"本身，不是随手截的一条。
            DiagramBand left, right, top, bottom;
            if (width < 1f)
            {
                left = Band(true, reference.Left, reference.Top, canvas.Left - reference.Left, reference.Height);
                right = Band(true, canvas.Right, reference.Top, reference.Right - canvas.Right, reference.Height);
            }
            else if (width > 1f)
            {
                left = Band(false, canvas.Left, canvas.Top, reference.Left - canvas.Left, canvas.Height);
                right = Band(false, reference.Right, canvas.Top, canvas.Right - reference.Right, canvas.Height);
            }
            else { left = default; right = default; }

            if (height < 1f)
            {
                top = Band(true, reference.Left, reference.Top, reference.Width, canvas.Top - reference.Top);
                bottom = Band(true, reference.Left, canvas.Bottom, reference.Width, reference.Bottom - canvas.Bottom);
            }
            else if (height > 1f)
            {
                top = Band(false, canvas.Left, canvas.Top, canvas.Width, reference.Top - canvas.Top);
                bottom = Band(false, canvas.Left, reference.Bottom, canvas.Width, canvas.Bottom - reference.Bottom);
            }
            else { top = default; bottom = default; }

            // 裁的判定**复用**既有逐轴结论（`AxisFit`），不自己重判
            bool cropped = fit.Horizontal.IsCropped || fit.Vertical.IsCropped;

            string horizontal = Cell(in fit, Axis.Horizontal);
            string vertical = Cell(in fit, Axis.Vertical);
            // 🔴 措辞纪律：本图画布框叫「**本档画布**」，**不许**出现「安全设计区」（跨档图的名字）。
            string tooltip = "外框 = 参考画布、内框 = 本档画布（**同一比例尺**，逐档各画各的，1 行 = 1 档）"
                           + "\n红 = 被裁（参考有、画布没有）· 黄 = 留白（画布比参考大）"
                           + "\n横向：" + horizontal
                           + "\n纵向：" + vertical
                           + "\n" + DiagramLimiter;

            // 🔴 **信封的真实宽高比**：`envelopeWidth/Height` 是**归一化**单位（那里"参考 = 1×1"，并不是 16:9！）
            //    ⇒ 必须乘回参考的真实像素。少了这一步，"参考框"又被画成假形状
            //    （实测抓到过：1920×1080 的档位算出的信封宽高比是 **1.0** 而不是 1.778）。
            float envelopeAspect = IsUsable(referenceWidth) && IsUsable(referenceHeight) && referenceHeight > 0f
                ? envelopeWidth * referenceWidth / (envelopeHeight * referenceHeight)
                : envelopeWidth / envelopeHeight;

            return new DiagramColumnLayout(true, cropped, reference, canvas, left, right, top, bottom,
                                            envelopeAspect, horizontal, vertical, tooltip);
        }
    }
}
