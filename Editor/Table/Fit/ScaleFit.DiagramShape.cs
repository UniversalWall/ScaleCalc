namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// 逐档裁留示意图的**值对象与布局结构**（"算出来的东西长什么样"）。
    /// <para>拆的判据 = **200 行红线** + 职责不同：本文件只管**数据形制**（矩形 · 色带 · 布局 · 装箱壳），
    /// `ScaleFit.CellDiagram.cs` 只管"**怎么算**"（信封 · 比例 · 色带位置 · 文案）。</para>
    /// <para>🔴 拆文件是**一次原子操作**：本文件现持有 <see cref="DiagramRect"/> · <see cref="DiagramBand"/> ·
    /// <see cref="DiagramColumnLayout"/> · <see cref="DiagramColumnLayoutBox"/> 四个类型；引用它们的只有
    /// `ScaleCalcTableBinder.Diagram*.cs` 与 `Tests/EditMode/Table/ScaleFitDiagram*Tests.cs`。</para>
    /// </summary>
    public static partial class ScaleFit
    {
        /// <summary>归一化矩形（**信封坐标**：原点左上、右下为 +，取值 ⊆ [0,1]）。</summary>
        public readonly struct DiagramRect
        {
            public readonly float Left;
            public readonly float Top;
            public readonly float Width;
            public readonly float Height;

            public DiagramRect(float left, float top, float width, float height)
            {
                Left = left;
                Top = top;
                Width = width;
                Height = height;
            }

            public float Right => Left + Width;
            public float Bottom => Top + Height;

            /// <summary>退化矩形（宽或高 ≤ 0 ⇒ 不画）。</summary>
            public bool IsEmpty => Width <= 0f || Height <= 0f;
        }

        /// <summary>
        /// 两框之差的一条**色带**：<see cref="Crop"/> = 红（被裁）· 否则黄（留白）；
        /// <see cref="Present"/> = <c>false</c> ⇒ 该侧无差、**不画**。
        /// </summary>
        public readonly struct DiagramBand
        {
            public readonly bool Present;
            public readonly bool Crop;
            public readonly DiagramRect Rect;

            public DiagramBand(bool present, bool crop, DiagramRect rect)
            {
                Present = present;
                Crop = crop;
                Rect = rect;
            }
        }

        /// <summary>
        /// <see cref="DiagramColumnLayout"/> 的**装箱壳**（给装配层的表外缓存用）。
        /// <para>为什么需要它：<c>ConditionalWeakTable&lt;,&gt;</c> 要求**引用类型**值，而布局是 <c>readonly struct</c>。</para>
        /// </summary>
        public sealed class DiagramColumnLayoutBox
        {
            public readonly DiagramColumnLayout Layout;
            public DiagramColumnLayoutBox(DiagramColumnLayout layout) { Layout = layout; }
        }

        /// <summary>
        /// 逐档图的**归一化布局**（信封坐标：<see cref="Reference"/> 与 <see cref="Canvas"/> 都 ⊆ [0,1]）。
        /// <para>🔴 两框**同一比例尺**——它们的大小关系就是"参考 vs 本档画布"的真实关系；两框之差由四条
        /// <see cref="DiagramBand"/> 表达（红 = 裁 / 黄 = 留）。</para>
        /// </summary>
        public readonly struct DiagramColumnLayout
        {
            /// <summary>有画布可比（<c>false</c> ⇒ 只画参考框，不编画布框）。</summary>
            public readonly bool HasCanvas;

            /// <summary>至少一轴被裁（**保留的摘要字段**：直接取自既有逐轴判定，不自己重判）。</summary>
            public readonly bool Cropped;

            /// <summary>参考框（信封坐标；**恒 ⊆ [0,1]**）。</summary>
            public readonly DiagramRect Reference;

            /// <summary>本档画布框（信封坐标；可比参考框大或小，但**仍 ⊆ [0,1]**）。</summary>
            public readonly DiagramRect Canvas;

            /// <summary>四条差带（左 / 右 / 上 / 下；无差 ⇒ <c>Present == false</c>）。</summary>
            public readonly DiagramBand Left;
            public readonly DiagramBand Right;
            public readonly DiagramBand Top;
            public readonly DiagramBand Bottom;

            /// <summary>信封宽 ÷ 高（**真实像素**之比；装配层按它定作画区像素尺寸 ⇒ **比例绝不被拉伸**）。</summary>
            public readonly float EnvelopeAspect;

            /// <summary>两轴文字（**逐字复用** <see cref="Cell"/> 的输出，与「横向」/「纵向」两列同源）。</summary>
            public readonly string HorizontalText;
            public readonly string VerticalText;

            /// <summary>悬停文本（点名"画布框 = 本档画布" + 红黄含义 + 两轴 + 限定语）。</summary>
            public readonly string Tooltip;

            public DiagramColumnLayout(bool hasCanvas, bool cropped, DiagramRect reference, DiagramRect canvas,
                                       DiagramBand left, DiagramBand right, DiagramBand top, DiagramBand bottom,
                                       float envelopeAspect, string horizontalText, string verticalText, string tooltip)
            {
                HasCanvas = hasCanvas;
                Cropped = cropped;
                Reference = reference;
                Canvas = canvas;
                Left = left;
                Right = right;
                Top = top;
                Bottom = bottom;
                EnvelopeAspect = envelopeAspect;
                HorizontalText = horizontalText;
                VerticalText = verticalText;
                Tooltip = tooltip;
            }

            /// <summary>
            /// 单元格里的**纯文本摘要**（图之外的兜底：「导出当前视图」CSV 的那一列、以及无障碍/文本场景）。
            /// 与悬停**同源**（都来自 <see cref="HorizontalText"/>/<see cref="VerticalText"/>）。
            /// </summary>
            public string Text => HasCanvas
                ? "横 " + HorizontalText + " · 纵 " + VerticalText
                : NoCanvasMarker;
        }
    }
}
