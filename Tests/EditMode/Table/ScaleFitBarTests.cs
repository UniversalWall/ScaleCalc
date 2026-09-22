using System.Collections.Generic;
using NUnit.Framework;
using Wayward.ScaleCalc.Editor;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// **字符条**与**当前视图 CSV**：都是纯字符串 ⇒ 逐字符断言。
    /// <para>读法：<c>├…┤</c> 一格 = 1/cells；<c>█</c> = 实际画布；<c>·</c> = 还差的那截（裁切）；<c>┊</c> = 参考到此为止
    /// （它右边多出来的 <c>█</c> 就是留白）。🔴 **字符条不参与任何判据**——精确读数看数值列。</para>
    /// </summary>
    public sealed class ScaleFitBarTests
    {
        private static readonly ScaleSize Reference = new ScaleSize(1920f, 1080f);

        /// <summary>造一个 TierFit（只走公开构造：几何值由 `OfAxis` 算，容差口径与生产完全一致）。</summary>
        private static ScaleFit.TierFit Fit(float canvasWidth, float canvasHeight)
            => new ScaleFit.TierFit(true, new ScaleSize(canvasWidth, canvasHeight),
                ScaleFit.OfAxis(canvasWidth, Reference.Width),
                ScaleFit.OfAxis(canvasHeight, Reference.Height),
                false);

        /// <summary>正好：满格实心，既没有 `·` 也没有 `┊`。</summary>
        [Test]
        public void Exact_FillsEveryCell()
        {
            ScaleFit.TierFit fit = Fit(1920f, 1080f);
            string bar = ScaleFit.Bar(in fit, ScaleFit.Axis.Horizontal, 16);

            Assert.That(bar, Is.EqualTo("├████████████████┤"));
            Assert.That(ScaleFit.BarLabel(in fit, ScaleFit.Axis.Horizontal), Is.EqualTo("正好"));
        }

        /// <summary>裁切：条**比满格短**（`·` 收尾），没有 `┊`；标签写 `裁 −x.x%`。</summary>
        [Test]
        public void Cropped_LeavesTrailingDots()
        {
            ScaleFit.TierFit fit = Fit(1663f, 1247f);            // 横向裁 13.4%、纵向留 15.5%
            string horizontal = ScaleFit.Bar(in fit, ScaleFit.Axis.Horizontal, 16);

            Assert.That(horizontal, Is.EqualTo("├██████████████··┤"), "16 格 × (1663/1920) ≈ 14 格实心");
            Assert.That(horizontal, Does.Not.Contain("┊"), "裁切侧没有'参考到此为止'——参考就是满格");
            Assert.That(ScaleFit.BarLabel(in fit, ScaleFit.Axis.Horizontal), Is.EqualTo("裁 −13.4%"), "百分比**手拼**");
        }

        /// <summary>留白：满格实心，`┊` 标出参考的边界——它右边那截就是多出来的。</summary>
        [Test]
        public void Spare_MarksWhereTheReferenceEnds()
        {
            ScaleFit.TierFit fit = Fit(1663f, 1247f);
            string vertical = ScaleFit.Bar(in fit, ScaleFit.Axis.Vertical, 16);

            Assert.That(vertical, Is.EqualTo("├█████████████┊██┤"), "参考落在第 14 格 ⇒ 第 14 格画 ┊、右边 2 格是留白");
            Assert.That(vertical, Does.Not.Contain("·"), "留白侧不该出现'还差一截'");
            Assert.That(ScaleFit.BarLabel(in fit, ScaleFit.Axis.Vertical), Is.EqualTo("留 +15.5%"));
        }

        /// <summary>`cells` 夹到 `[4, 64]`；框长恒 = cells + 2（`├` `┤`）。</summary>
        [Test]
        public void Cells_AreClamped_AndTheFrameLengthIsStable()
        {
            ScaleFit.TierFit fit = Fit(1663f, 1247f);

            foreach (int cells in new[] { 1, 2, 4, 16, 64, 200 })
            {
                string bar = ScaleFit.Bar(in fit, ScaleFit.Axis.Horizontal, cells);
                int expected = System.Math.Min(ScaleFit.BarMaxCells, System.Math.Max(ScaleFit.BarMinCells, cells));
                Assert.That(bar.Length, Is.EqualTo(expected + 2), cells + " 格 ⇒ 条长 cells+2");
            }
            Assert.That(ScaleFit.Bar(in fit, ScaleFit.Axis.Horizontal, 0).Length, Is.EqualTo(ScaleFit.BarMinCells + 2));
            Assert.That(ScaleFit.Bar(in fit, ScaleFit.Axis.Horizontal, 999).Length, Is.EqualTo(ScaleFit.BarMaxCells + 2));
        }

        /// <summary>无画布 / 参考无效 ⇒ 与量值列**同一套标记**（不编图、不编数）。</summary>
        [Test]
        public void UndefinedInputs_ProduceMarkers()
        {
            var noCanvas = new ScaleFit.TierFit(false, default, ScaleFit.OfAxis(0f, 1920f), ScaleFit.OfAxis(0f, 1080f), false);
            Assert.That(ScaleFit.Bar(in noCanvas, ScaleFit.Axis.Horizontal), Is.EqualTo("（无画布尺寸）"));
            Assert.That(ScaleFit.BarLabel(in noCanvas, ScaleFit.Axis.Horizontal), Is.EqualTo("（无画布尺寸）"));

            ScaleCalcResult canvas = Canvas();          // ⚠️ `in` 只能传局部变量/字段（`in Canvas()` = CS8156）
            ScaleFit.TierFit badReference = ScaleFit.Of(in canvas, new ScaleSize(0f, 0f));
            Assert.That(ScaleFit.Bar(in badReference, ScaleFit.Axis.Horizontal), Is.EqualTo("—"));
            Assert.That(ScaleFit.BarLabel(in badReference, ScaleFit.Axis.Horizontal), Is.EqualTo("—"));
        }

        private static ScaleCalcResult Canvas()
        {
            ScaleCalcInput input = ScaleCalcInput.Default;
            input.Mode = ScaleMode.ScaleWithScreenSize;
            input.ScreenSize = Reference;
            input.ReferenceResolution = Reference;
            input.ScreenMatch = ScreenMatchMode.MatchWidthOrHeight;
            input.MatchWidthOrHeight = 0.5f;
            return ScaleCalc.Evaluate(in input);
        }

        /// <summary>字符条块：标题写明"画了几档"，**最需要注意的排第一**（裁切优先、留白在后）。</summary>
        [Test]
        public void VerdictBars_ShowTheWorstTiersFirst()
        {
            string text = ScaleCalcWindow.VerdictBarsText(ScreenProfiles.All, Reference,
                                                          ScreenMatchMode.MatchWidthOrHeight, 0.5f, 144f);
            string[] lines = text.Split('\n');

            Assert.That(lines.Length, Is.EqualTo(1 + ScaleCalcWindow.BarRows), "1 行标题 + 最坏的 N 档");
            Assert.That(lines[0], Does.Contain("最需要注意的 " + ScaleCalcWindow.BarRows + " 档").And.Contains("横向"),
                "标题必须说清'只画了几档'与轴向（不然会被读成全部）");
            Assert.That(lines[1], Does.StartWith("手机竖屏超长 9:19.5"), "最坏的排第一（横向 −49.0%）");
            Assert.That(lines[1], Does.Contain("├").And.Contains("┤"));
            Assert.That(lines[1], Does.Contain("裁 −49.0%"));
            Assert.That(lines[2], Does.StartWith("手机竖屏 9:16"), "次坏（横向 −43.8%）");
            Assert.That(lines[3], Does.StartWith("方屏 1:1"), "第三（横向 −25.0%）");
        }

        /// <summary>
        /// **安全区示意图的几何**（C 批，§6.3）：外框恒 = 1×1（参考画布），内框 = 安全区 / 参考（夹到 ≤1），
        /// 边带颜色看"有没有会裁的档位"；**没有安全区就只画外框**（不编内框）。
        /// </summary>
        [Test]
        public void Diagram_IsNormalizedToTheReference_AndSaysWhenThereIsNothingToDraw()
        {
            ScaleFit.ModeStats stats = ScaleFit.Stats(ScreenProfiles.All, Reference,
                                                      ScreenMatchMode.MatchWidthOrHeight, 0.5f, 144f);
            ScaleFit.DiagramLayout layout = ScaleFit.DiagramOf(stats, Reference);

            Assert.That(layout.HasSafeArea, Is.True);
            Assert.That(layout.InnerWidthRatio, Is.EqualTo(stats.SafeArea.Width / Reference.Width).Within(1e-4f));
            Assert.That(layout.InnerHeightRatio, Is.EqualTo(stats.SafeArea.Height / Reference.Height).Within(1e-4f));
            Assert.That(layout.InnerWidthRatio, Is.EqualTo(0.51f).Within(0.02f), "978/1920 ≈ 51%");
            Assert.That(layout.Cropped, Is.True, "有 6 档会裁 ⇒ 边带用'裁'的颜色");

            ScaleFit.DiagramLayout expand = ScaleFit.DiagramOf(
                ScaleFit.Stats(ScreenProfiles.All, Reference, ScreenMatchMode.Expand, 0.5f, 144f), Reference);
            Assert.That(expand.Cropped, Is.False, "Expand ⇒ 没有会裁的档位");
            Assert.That(expand.InnerWidthRatio, Is.EqualTo(1f).Within(1e-4f),
                "安全区 = 参考 ⇒ 内框与外框重合（**夹到 ≤1**，不画到框外；浮点上是 0.99999994）");

            var empty = new List<ScreenProfile>();
            ScaleFit.DiagramLayout none = ScaleFit.DiagramOf(
                ScaleFit.Stats(empty, Reference, ScreenMatchMode.MatchWidthOrHeight, 0.5f, 144f), Reference);
            Assert.That(none.HasSafeArea, Is.False, "0 档 ⇒ 没有安全区可画（窗口只画外框）");
            Assert.That(none.InnerWidthRatio, Is.EqualTo(1f));
        }

        /// <summary>C 批「单元格比例底衬」：**只有**横向/纵向两列声明了 `BarCell`（其余列还是纯 `Label`）。</summary>
        [Test]
        public void BarCells_AreDeclaredOnExactlyTwoColumns()
        {
            var declared = new List<string>();
            foreach (ScaleTableColumns.Spec spec in ScaleTableColumns.All)
                if (spec.BarCell) declared.Add(spec.Title);

            Assert.That(declared, Is.EqualTo(new[] { ScaleTableColumns.HorizontalTitle, ScaleTableColumns.VerticalTitle }),
                "声明在列定义单里（唯一真相源）——且只有这两列");
            Assert.That(ScaleTableColumns.HorizontalTitle, Is.EqualTo("横向"), "常量与表头一致（装配层按它取轴）");
        }
    }
}
