using System;
using NUnit.Framework;
using Wayward.ScaleCalc.Editor;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// **列宽算法**的落点断言：宽度 = "该列最长文本 + <c>CellPadding</c>" 再夹到声明的区间。
    /// <para>🔴 **为什么从 <c>ScaleCalcTableColumnTests.cs</c> 拆出来**：
    /// 那个文件**本来正好 200/200**，而本批要给列定义单加"行首对账列"的断言 ⇒ 一追加就 **236 行**，
    /// 当场被行数闸门拦下。职责不同：
    /// 本文件管"**宽度算法**"（纯算法 + 注入的假测量器），<c>ScaleCalcTableColumnTests.cs</c> 管"**列定义单的形状**"
    /// （列数/列序/取值）。</para>
    /// </summary>
    public sealed class ScaleCalcColumnWidthTests
    {
        private static readonly ScaleSize Reference = new ScaleSize(1920f, 1080f);

        /// <summary>假测量器：一个字符 8px（与真实字体无关，只验算法）。</summary>
        private static float Fake(string text) => text == null ? 0f : text.Length * 8f;

        private static ScaleTable BuildTable()
            => ScaleTable.Build(Reference, ScreenMatchMode.MatchWidthOrHeight, 0.5f, 144f,
                                ScaleMode.ScaleWithScreenSize, default, false, default);

        private static ScaleTableRow LiveRow(string name, float width, float height)
            => new ScaleTableRow { Profile = new ScreenProfile(name, width, height, "现场 Canvas") };

        /// <summary>按表头找列下标（列序会变 ⇒ 判据不许写死下标）。</summary>
        private static int ColumnIndexOf(string title)
        {
            for (int i = 0; i < ScaleTableColumns.All.Length; i++)
                if (ScaleTableColumns.All[i].Title == title) return i;
            return -1;
        }

        [Test]
        public void Widths_FitOwnContent_OrHitDeclaredCap()
        {
            ScaleTable table = BuildTable();
            float[] widths = ScaleTableColumns.ComputeWidths(table.Rows, Fake);

            Assert.That(widths.Length, Is.EqualTo(ScaleTableColumns.All.Length));
            for (int i = 0; i < widths.Length; i++)
            {
                ScaleTableColumns.Spec spec = ScaleTableColumns.All[i];
                float longest = Fake(spec.Title);
                foreach (ScaleTableRow row in table.Rows)
                {
                    float w = Fake(spec.ValueOf(row));
                    if (w > longest) longest = w;
                }

                Assert.That(widths[i], Is.GreaterThanOrEqualTo(spec.MinWidth), spec.Title + "：不许小于声明下限");
                Assert.That(widths[i], Is.LessThanOrEqualTo(spec.MaxWidth), spec.Title + "：不许大于声明上限");
                Assert.That(widths[i], Is.GreaterThanOrEqualTo(Math.Min(spec.MaxWidth, longest + ScaleTableColumns.CellPadding)),
                    spec.Title + "：要么容得下最长内容，要么已到声明上限（不许无理由截断）");
            }
        }

        [Test]
        public void Widths_TrackContent_InsteadOfFixedMagicNumbers()
        {
            ScaleTable table = BuildTable();
            float[] narrow = ScaleTableColumns.ComputeWidths(table.Rows, Fake);
            float[] wide = ScaleTableColumns.ComputeWidths(table.Rows, text => Fake(text) * 2f);

            int grew = 0;
            for (int i = 0; i < narrow.Length; i++)
            {
                Assert.That(wide[i], Is.GreaterThanOrEqualTo(narrow[i]), ScaleTableColumns.All[i].Title + "：内容变长不许反而变窄");
                if (wide[i] > narrow[i]) grew++;
            }
            Assert.That(grew, Is.GreaterThan(0), "把内容测量值翻倍后应当有列变宽（否则就是写死的宽度）");
        }

        [Test]
        public void Widths_UseTheLongestRow_NotTheFirstOne()
        {
            var shortName = LiveRow("方屏 1:1", 1080f, 1080f);
            var longName = LiveRow("手机竖屏超长 9:19.5", 1440f, 3120f);
            var rows = new[] { shortName, longName };

            float[] widths = ScaleTableColumns.ComputeWidths(rows, Fake);

            // 🔴 行首插了「对账」⇒ 档位名不再固定在第 1 列。**按标题找列下标**，
            //    不许再写 `widths[0]`（那正是"列序一变、判据静默测错对象"的形态）。
            int nameColumn = ColumnIndexOf("屏幕档位");
            Assert.That(nameColumn, Is.GreaterThanOrEqualTo(0), "找不到「屏幕档位」列 ⇒ 判据必须失败，不许默认 0");
            Assert.That(widths[nameColumn], Is.GreaterThanOrEqualTo(Fake(longName.Profile.Name) + ScaleTableColumns.CellPadding),
                "档位名列的宽度由**最长**的那一行决定（不是第一行）");
        }

        [Test]
        public void Widths_SurviveNaNMeasure()
        {
            ScaleTable table = BuildTable();
            float[] widths = ScaleTableColumns.ComputeWidths(table.Rows, _ => float.NaN);

            foreach (float w in widths)
            {
                Assert.That(float.IsNaN(w), Is.False, "NaN 会穿过 Math.Min/Max（两侧比较都是 false），必须在算宽前兜住");
                Assert.That(w, Is.GreaterThan(0f));
            }
        }

        [Test]
        public void Widths_FallBackToDeclaredMinimum_WhenMeasureGivesNothing()
        {
            ScaleTable table = BuildTable();
            float[] widths = ScaleTableColumns.ComputeWidths(table.Rows, _ => 0f);

            Assert.That(widths.Length, Is.EqualTo(ScaleTableColumns.All.Length));
            for (int i = 0; i < widths.Length; i++)
            {
                ScaleTableColumns.Spec spec = ScaleTableColumns.All[i];
                // 🔴 2026-09-21：`FixedWidth` 列（裁留图）**不参与**"按内容推 + 分摊"——它的宽是**声明**的
                //    （画布作画区 96 + 两轴文字余量）⇒ 测量器给 0 时它**仍然**是 `MaxWidth`，不是 `MinWidth`。
                float expected = spec.FixedWidth ? spec.MaxWidth : spec.MinWidth;
                Assert.That(widths[i], Is.EqualTo(expected),
                    spec.Title + "：测量不出来时的期望宽（图列取声明宽，其余退到下限，**都不许塌成 0**）");
            }
        }

        [Test]
        public void Estimate_IsOnlyAFallback_ButNeverZeroForNonEmptyText()
        {
            Assert.That(ScaleTableColumns.Estimate(string.Empty), Is.EqualTo(0f));
            Assert.That(ScaleTableColumns.Estimate("ab"), Is.GreaterThan(0f));
            Assert.That(ScaleTableColumns.Estimate("中文"), Is.GreaterThan(ScaleTableColumns.Estimate("ab")),
                "CJK 按更宽的字符估算");
        }

        /// <summary>这一条是"一列很宽"的**回归断言**：剩余宽度必须摊给所有列，不许全塞给最后一列。</summary>
        [Test]
        public void Distribute_SpreadsExtraWidth_InsteadOfDumpingItOnTheLastColumn()
        {
            ScaleTable table = BuildTable();
            float[] content = ScaleTableColumns.ComputeWidths(table.Rows, Fake);
            float contentSum = 0f;
            foreach (float w in content) contentSum += w;

            float available = contentSum + 130f;
            float[] widths = ScaleTableColumns.Distribute(content, available);

            float sum = 0f;
            foreach (float w in widths) sum += w;
            Assert.That(sum, Is.EqualTo(available).Within(0.5f), "分摊后应当正好填满可用宽度");

            int grew = 0;
            for (int i = 0; i < widths.Length; i++) if (widths[i] > content[i]) grew++;
            Assert.That(grew, Is.GreaterThan(1), "多出来的宽度要摊给**多列**，不是全给最后一列");
        }
    }
}
