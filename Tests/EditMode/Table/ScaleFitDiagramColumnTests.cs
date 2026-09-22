using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Wayward.ScaleCalc;
using Wayward.ScaleCalc.Editor;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// **逐档裁留示意图列：列定义与文案**。
    /// <para>🔴 **按 200 行红线拆出**（重写后本文件到 250 行 &gt; 红线）：本文件管
    /// "**列定义挂得对不对**"（列序 · 固定宽 · 悬停措辞 · 单一几何来源），几何本身的断言在
    /// <c>ScaleFitDiagramGeometryTests.cs</c>。</para>
    /// <para>"图上到底画出来没有"是**现场读数**（算得出来 ≠ 看得见）。</para>
    /// </summary>
    public sealed class ScaleFitDiagramColumnTests
    {
        private static string PackageRoot =>
            UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(ScaleFitDiagramColumnTests).Assembly).resolvedPath;

        private static readonly ScaleSize Reference = new ScaleSize(1920f, 1080f);

        /// <summary>造一个整档拟合：**只给"画布 vs 参考"**（几何只吃这两个量，与 scaleFactor 无关）。</summary>
        private static ScaleFit.TierFit FitOf(float canvasWidth, float canvasHeight, bool hasCanvas = true)
            => new ScaleFit.TierFit(hasCanvas, new ScaleSize(canvasWidth, canvasHeight),
                                    ScaleFit.OfAxis(canvasWidth, Reference.Width),
                                    ScaleFit.OfAxis(canvasHeight, Reference.Height),
                                    false);

        /// <summary>两轴文字**逐字**等于 <see cref="ScaleFit.Cell"/>（与「横向」/「纵向」两列同源）。</summary>
        [Test]
        public void DK5_AxisTexts_AreVerbatimFromTheSharedCellFunction()
        {
            ScaleFit.TierFit fit = FitOf(1663f, 1247f);
            ScaleFit.DiagramColumnLayout layout = ScaleFit.DiagramColumnOf(fit);

            Assert.That(layout.HorizontalText, Is.EqualTo(ScaleFit.Cell(in fit, ScaleFit.Axis.Horizontal)));
            Assert.That(layout.VerticalText, Is.EqualTo(ScaleFit.Cell(in fit, ScaleFit.Axis.Vertical)));
        }

        /// <summary>
        /// **两图不同词** —— 本图画布框叫「本档画布」；且**不许**出现「安全设计区」
        /// （那是**跨档图**的内框名；两张图表观同形，混用会让读者把逐档图读成跨档结论）。
        /// </summary>
        [Test]
        public void DK8_TooltipNamesThisTierCanvas_AndAvoidsTheCrossTierWording()
        {
            ScaleFit.DiagramColumnLayout layout = ScaleFit.DiagramColumnOf(FitOf(1663f, 1247f));

            Assert.That(layout.Tooltip, Does.Contain("本档画布"), "必须点名内框是什么");
            Assert.That(layout.Tooltip, Does.Not.Contain("安全设计区"), "不许用跨档图的内框名");
            Assert.That(layout.Tooltip, Does.Contain(ScaleFit.DiagramLimiter), "限定语不能省（几何口径 ≠ 布局承诺）");
            Assert.That(layout.Tooltip, Does.Contain(layout.HorizontalText).And.Contains(layout.VerticalText),
                "悬停里要能看到两轴读数（列宽不足时的兜底）");
            Assert.That(layout.Tooltip, Does.Contain("红").And.Contains("黄"),
                "红黄的含义要写在悬停里（颜色是第三重编码，不能只靠颜色说话）");
        }

        /// <summary>
        /// 列集 = 10（**不写字面量**）· 「裁留图」是**第 2 列**（现场裁定「裁留图列放到前面」）
        /// · 原来那 8 列的**相对次序一个都没动**。
        /// </summary>
        [Test]
        public void DK6_DiagramIsTheSecondColumn_AndPD10OrderIsUntouched()
        {
            ScaleTableColumns.Spec[] columns = ScaleTableColumns.All;

            Assert.That(columns.Length, Is.EqualTo(10), "对账 + 裁留图 + 原有 8 列");
            Assert.That(columns[0].Title, Is.EqualTo(ScaleTableColumns.ReconcileTitle), "第 1 列 = 对账");
            Assert.That(columns[1].Title, Is.EqualTo(ScaleTableColumns.DiagramTitle), "第 2 列 = 裁留图");
            Assert.That(columns[1].Kind, Is.EqualTo(ScaleTableColumns.ScaleCellKind.Diagram));
            Assert.That(columns[1].FixedWidth, Is.True, "图列是**声明式固定宽**");

            string[] pd10 = { "屏幕档位", "比例", "横向", "纵向", "实际画布", "sf", "屏幕尺寸", "差值" };
            for (int i = 0; i < pd10.Length; i++)
                Assert.That(columns[i + 2].Title, Does.StartWith(pd10[i]),
                    "第 " + (i + 3) + " 列（原有 8 列的相对次序不许动）");

            // 两个行首判据列都**没有排序语义**（如实登记，不是缺陷）
            Assert.That(ScaleFit.SortKeyOfColumn(0), Is.EqualTo(ScaleFit.SortKey.None));
            Assert.That(ScaleFit.SortKeyOfColumn(1), Is.EqualTo(ScaleFit.SortKey.None));
        }

        /// <summary>图列宽度**不随文本长度变**（= 声明宽），而普通列**会变**。</summary>
        [Test]
        public void DK9_DiagramWidth_DoesNotTrackTextLength()
        {
            var rows = new List<ScaleTableRow>
            {
                new ScaleTableRow { Profile = new ScreenProfile("短", 1920f, 1080f, "测试"), Reference = Reference },
                new ScaleTableRow
                {
                    Profile = new ScreenProfile("很长很长很长很长很长很长很长的档位名", 2560f, 1440f, "测试"),
                    Reference = Reference,
                },
            };

            float[] narrow = ScaleTableColumns.ComputeWidths(rows, text => text == null ? 0f : text.Length * 8f);
            float[] wide = ScaleTableColumns.ComputeWidths(rows, text => text == null ? 0f : text.Length * 80f);

            int diagram = 1;                                     // 「裁留图」= 第 2 列
            Assert.That(ScaleTableColumns.All[diagram].FixedWidth, Is.True, "这条判据的前提：图列是固定宽");
            Assert.That(narrow[diagram], Is.EqualTo(ScaleTableColumns.All[diagram].MaxWidth), "图列 = 声明宽");
            Assert.That(wide[diagram], Is.EqualTo(narrow[diagram]), "测量值放大 10 倍，图列宽度**不许**变");
            Assert.That(wide[3], Is.GreaterThan(narrow[3]), "对照：档位名列随内容变宽（否则这条判据守不住「图列特例」）");
        }

        /// <summary>
        /// 图格的绑定**只有一处几何来源** —— 不许再调内核求值或整档拟合。
        /// <para>⚠️ 读的是 **`ScaleCalcTableBinder.Diagram.cs`**：拆文件时图那一章从
        /// `Cell.cs` 搬到了这里；同日重做又把它与"元素形态"拆成两个 partial
        /// （`…Diagram.cs` 管绑定与摆位，`…Diagram.Elements.cs` 管造元素与常量）。
        /// 判据里**顺带钉住"这个方法住在哪个文件"**：搬了家而断言没跟 ⇒ 断言会静默变成"查一个不含它的文件"
        /// （同一族教训：只查旧文件 = 假绿）。</para></summary>
        [Test]
        public void DK4_BindDiagramCell_HasASingleGeometrySource()
        {
            string source = File.ReadAllText(
                Path.Combine(PackageRoot, "Editor", "Table", "ScaleCalcTableBinder.Diagram.cs"));

            int at = source.IndexOf("private static void BindDiagramCell", System.StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThan(0), "绑定方法必须在**这个**文件里（搬家要同步这条断言，否则它是假绿）");

            // 取到下一个方法声明为止（本方法的正文）
            int next = source.IndexOf("private static void", at + 10, System.StringComparison.Ordinal);
            string body = next > 0 ? source.Substring(at, next - at) : source.Substring(at);

            Assert.That(body, Does.Contain("ScaleFit.Of("), "唯一一次几何求值");
            Assert.That(body, Does.Not.Contain("ScaleCalc.Evaluate"), "不许再走一遍内核求值");
            Assert.That(body, Does.Contain("ScaleFit.DiagramColumnOf"), "比例一律来自纯函数");
        }
    }
}
