using NUnit.Framework;
using Wayward.ScaleCalc;
using Wayward.ScaleCalc.Editor;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// **逐档裁留示意图的几何**（纯函数层）。
    /// <para>🔴 **从 <c>ScaleFitDiagramColumnTests.cs</c> 拆出**（那个文件重写后到 **250 行 &gt; 200 红线**，
    /// 行数闸门当场会拦）——拆的判据 = **红线 + 职责不同**：
    /// 本文件管"**几何算得对不对**"（信封 · 同一比例尺 · 色带位置 · 不出血），
    /// <c>ScaleFitDiagramColumnTests.cs</c> 管"**列定义与文案挂得对不对**"（列序 · 固定宽 · 悬停措辞 · 源码级单一几何来源）。</para>
    /// <para>🔴 坐标系 = **信封**（参考框与画布框的**逐轴并集**）：两框同一比例尺、都 ⊆ [0,1] ⇒
    /// ① 留白看得见（画布框比参考框大）；② 图**永不出血**（初版让内框画出作画区之外，在 26px 的行里必然出血到邻行）。</para>
    /// </summary>
    public sealed class ScaleFitDiagramGeometryTests
    {
        private static readonly ScaleSize Reference = new ScaleSize(1920f, 1080f);

        /// <summary>造一个整档拟合：**只给"画布 vs 参考"**（几何只吃这两个量，与 scaleFactor 无关）。</summary>
        private static ScaleFit.TierFit FitOf(float canvasWidth, float canvasHeight, bool hasCanvas = true)
            => new ScaleFit.TierFit(hasCanvas, new ScaleSize(canvasWidth, canvasHeight),
                                    ScaleFit.OfAxis(canvasWidth, Reference.Width),
                                    ScaleFit.OfAxis(canvasHeight, Reference.Height),
                                    false);

        /// <summary>两框 + 四条带**全部** ⊆ [0,1] —— "图永不出血"的机械判据（下面多条共用）。</summary>
        private static void AssertAllInside(ScaleFit.DiagramColumnLayout layout)
        {
            ScaleFit.DiagramRect[] rects =
            {
                layout.Reference, layout.Canvas,
                layout.Left.Rect, layout.Right.Rect, layout.Top.Rect, layout.Bottom.Rect,
            };
            foreach (ScaleFit.DiagramRect rect in rects)
            {
                Assert.That(rect.Left, Is.GreaterThanOrEqualTo(-1e-5f), "左边界不许出信封");
                Assert.That(rect.Top, Is.GreaterThanOrEqualTo(-1e-5f), "上边界不许出信封");
                Assert.That(rect.Right, Is.LessThanOrEqualTo(1f + 1e-5f), "右边界不许出信封");
                Assert.That(rect.Bottom, Is.LessThanOrEqualTo(1f + 1e-5f), "下边界不许出信封");
            }
        }

        /// <summary>
        /// **两框同一比例尺** —— 画布框 ÷ 参考框 = 画布 ÷ 参考（**逐位**）；
        /// 信封 = 两框的**逐轴并集**；两框各自在信封里居中。
        /// </summary>
        [Test]
        public void DK1_BothBoxesShareOneScale_AndAreCentredInTheEnvelope()
        {
            ScaleFit.DiagramColumnLayout layout = ScaleFit.DiagramColumnOf(FitOf(1663f, 1247f));

            Assert.That(layout.HasCanvas, Is.True);
            float envelopeWidth = 1f;                    // 1663 < 1920 ⇒ 宽这一维的并集就是参考
            float envelopeHeight = 1247f / 1080f;         // 高这一维画布更大 ⇒ 并集就是画布

            Assert.That(layout.Reference.Width, Is.EqualTo(1f / envelopeWidth).Within(1e-5f), "参考框宽 = 1 ÷ 信封宽");
            Assert.That(layout.Reference.Height, Is.EqualTo(1f / envelopeHeight).Within(1e-5f));
            Assert.That(layout.Canvas.Width, Is.EqualTo(1663f / 1920f / envelopeWidth).Within(1e-5f), "画布框宽 = 比例 ÷ 信封宽");
            Assert.That(layout.Canvas.Height, Is.EqualTo(1247f / 1080f / envelopeHeight).Within(1e-5f));
            Assert.That(layout.EnvelopeAspect, Is.EqualTo(envelopeWidth * 1920f / (envelopeHeight * 1080f)).Within(1e-5f),
                "信封宽高比 = **真实像素**的并集之比（归一化单位里的 1×1 不是 16:9 ⇒ 必须乘回参考）");

            Assert.That(layout.Reference.Left, Is.EqualTo((1f - layout.Reference.Width) * 0.5f).Within(1e-5f), "参考框居中");
            Assert.That(layout.Reference.Top, Is.EqualTo((1f - layout.Reference.Height) * 0.5f).Within(1e-5f));
            Assert.That(layout.Canvas.Left, Is.EqualTo((1f - layout.Canvas.Width) * 0.5f).Within(1e-5f), "画布框居中");
            Assert.That(layout.Canvas.Top, Is.EqualTo((1f - layout.Canvas.Height) * 0.5f).Within(1e-5f));
        }

        /// <summary>
        /// **画布大于参考的轴，画布框就比参考框大** —— 留白不再被夹掉
        /// （初版"让内框比例 &gt; 1、画出作画区之外"在 26px 的行里必然出血；现行口径用**信封**同时满足
        /// "留白看得见"与"不出血"）。
        /// </summary>
        [Test]
        public void DK7_SpareAxis_MakesTheCanvasBoxBigger_AndNothingEscapesTheEnvelope()
        {
            ScaleFit.DiagramColumnLayout wide = ScaleFit.DiagramColumnOf(FitOf(2560f, 1080f));

            Assert.That(wide.Canvas.Width, Is.GreaterThan(wide.Reference.Width),
                "画布更宽 ⇒ 画布框必须更宽（夹回 1 就是跨档图犯过的错法）");
            Assert.That(wide.Canvas.Width / wide.Reference.Width, Is.EqualTo(2560f / 1920f).Within(1e-5f),
                "两框之比 = 画布 ÷ 参考（同一比例尺）");
            Assert.That(wide.Left.Present, Is.True, "横向留白 ⇒ 左右各有一条带");
            Assert.That(wide.Left.Crop, Is.False, "留白是**黄**（不是红）");
            Assert.That(wide.Left.Rect.Width, Is.EqualTo((wide.Canvas.Width - wide.Reference.Width) * 0.5f).Within(1e-5f),
                "带宽 = 单侧多出来的那一条");
            Assert.That(wide.Top.Present || wide.Bottom.Present, Is.False, "纵向正好 ⇒ 上下无带");
            AssertAllInside(wide);
        }

        /// <summary>裁/留的判定**复用**既有逐轴结论（<see cref="ScaleFit.AxisFit.Verdict"/>），不许自己重判。</summary>
        [Test]
        public void DK2_CroppedFlag_ComesFromTheExistingAxisVerdicts()
        {
            ScaleFit.TierFit cropBoth = FitOf(1000f, 800f);
            ScaleFit.TierFit wider = FitOf(2560f, 1080f);      // 横向留、纵向正好

            ScaleFit.DiagramColumnLayout a = ScaleFit.DiagramColumnOf(cropBoth);
            ScaleFit.DiagramColumnLayout b = ScaleFit.DiagramColumnOf(wider);

            Assert.That(a.Cropped, Is.True, "两轴都裁 ⇒ Cropped");
            Assert.That(b.Cropped, Is.False, "横向留白不算裁（含「留」的档位不该被标成裁）");

            // 与既有判定逐个一致（不许另立一套阈值）
            Assert.That(a.Cropped, Is.EqualTo(cropBoth.Horizontal.IsCropped || cropBoth.Vertical.IsCropped));
            Assert.That(b.Cropped, Is.EqualTo(wider.Horizontal.IsCropped || wider.Vertical.IsCropped));
        }

        /// <summary>无画布 ⇒ **只画参考框**、不编画布框、不编色带（**不许编 0**）。</summary>
        [Test]
        public void DK3_NoCanvas_DrawsOnlyTheReferenceBox()
        {
            ScaleFit.DiagramColumnLayout none = ScaleFit.DiagramColumnOf(FitOf(0f, 0f, hasCanvas: false));

            Assert.That(none.HasCanvas, Is.False);
            Assert.That(none.Text, Is.EqualTo(ScaleFit.NoCanvasMarker));
            Assert.That(none.Text, Does.Contain("无画布尺寸"), "沿用既有标记（与量值列同一套）");
            Assert.That(none.Reference.Width, Is.EqualTo(1f).Within(1e-5f), "只画参考框 ⇒ 它占满信封");
            Assert.That(none.Reference.Height, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(none.Canvas.IsEmpty, Is.True, "不编画布框");
            Assert.That(none.Left.Present || none.Right.Present || none.Top.Present || none.Bottom.Present, Is.False,
                "不编色带");
            Assert.That(none.EnvelopeAspect, Is.EqualTo(1920f / 1080f).Within(1e-5f),
                "信封宽高比仍取**参考的**（没有画布也要画成参考的形状，不许拍一个 1:1）");
        }

        /// <summary>
        /// **两框一样大时没有色带**——"正好"就是没有差，
        /// 不许留下零尺寸的带（那会在界面上变成 1px 脏线）。
        /// </summary>
        [Test]
        public void DK10_ExactMatch_DrawsNoBands()
        {
            ScaleFit.DiagramColumnLayout exact = ScaleFit.DiagramColumnOf(FitOf(1920f, 1080f));

            Assert.That(exact.Left.Present || exact.Right.Present || exact.Top.Present || exact.Bottom.Present,
                Is.False, "与参考同尺寸 ⇒ 四条带都不画");
            Assert.That(exact.Reference.Width, Is.EqualTo(1f).Within(1e-5f), "信封 = 参考自己");
            Assert.That(exact.Canvas.Width, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(exact.EnvelopeAspect, Is.EqualTo(1920f / 1080f).Within(1e-5f), "信封宽高比 = 参考的宽高比");
        }

        /// <summary>
        /// **"左右被裁 + 上下留白"的档位，两种颜色同时出现**
        /// —— 这是现场反馈「不能很好展现留白和裁剪」的直接克星（初版一格只有一种颜色，留白被吃掉）。
        /// </summary>
        [Test]
        public void DK11_CropAndSpare_CanAppearInTheSameCell()
        {
            ScaleFit.DiagramColumnLayout portrait = ScaleFit.DiagramColumnOf(FitOf(1080f, 1920f));   // 竖屏 9:16

            Assert.That(portrait.Left.Present && portrait.Right.Present, Is.True, "左右被裁 ⇒ 左右两条带");
            Assert.That(portrait.Left.Crop && portrait.Right.Crop, Is.True, "左右是**裁**（红）");
            Assert.That(portrait.Top.Present && portrait.Bottom.Present, Is.True, "上下留白 ⇒ 上下两条带");
            Assert.That(portrait.Top.Crop || portrait.Bottom.Crop, Is.False, "上下是**留**（黄），不是裁");
            Assert.That(portrait.Cropped, Is.True, "摘要字段：至少一轴被裁");
            AssertAllInside(portrait);
        }
    }
}
