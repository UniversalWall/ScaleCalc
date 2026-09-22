using System.Collections.Generic;
using NUnit.Framework;
using Wayward.ScaleCalc.Editor;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// **列定义单**的落点断言：列数/列序/宽度区间/取值/模式列声明全部读 <see cref="ScaleTableColumns.All"/>。
    /// <para>🔴 为什么**另立文件**：<c>ScaleCalcTableColumnTests.cs</c> 已 **200/200**（红线满格）——新列的条数/宽度断言
    /// 只能另起一个（行数按完整路径单独量，不靠印象）。</para>
    /// </summary>
    public sealed class ScaleCalcColumnVerdictTests
    {
        /// <summary>最小窗口（900×680）下的可用宽：900 − root 左右 padding 6×2 − 表格左右边框各 1。</summary>
        private const float MinimumWindowUsableWidth = 886f;

        /// <summary>每列都有表头/区间/取值；**模式列由声明认，不靠表头字符串猜**。</summary>
        [Test]
        public void PK10_ColumnDefinitions_AreTheSingleSourceOfTruth()
        {
            ScaleTableColumns.Spec[] columns = ScaleTableColumns.All;

            Assert.That(columns.Length, Is.EqualTo(ScaleTableColumns.All.Length), "列数以列定义单为准（**不写字面量**：加列后此处无需再改）");
            Assert.That(columns.Length, Is.GreaterThanOrEqualTo(10), "两个行首判据列（对账 · 裁留图）+ 原有 8 列");
            foreach (ScaleTableColumns.Spec spec in columns)
            {
                Assert.That(spec.Title, Is.Not.Null.And.Not.Empty, "每列都得有表头（宽度算法也读它）");
                Assert.That(spec.MinWidth, Is.GreaterThan(0f), spec.Title + "：下限必须为正（否则会塌成 0 宽）");
                Assert.That(spec.MaxWidth, Is.GreaterThanOrEqualTo(spec.MinWidth), spec.Title + "：上限不许小于下限");
                Assert.That(spec.ValueOf(Row()), Is.Not.Null, spec.Title + "：取值函数必须能对任何一行给出文本");
            }

            int declared = 0;
            foreach (ScaleTableColumns.Spec spec in columns) if (spec.KernelMode.HasValue) declared++;
            Assert.That(declared, Is.EqualTo(1), "只声明了一个模式列（sf）");
            Assert.That(ScaleTableColumns.KernelModeColumnCount(), Is.EqualTo(declared), "那个数读的就是声明");
            Assert.That(ScaleTableColumns.KernelModeColumnCount(), Is.EqualTo(1), "收窄后只剩一个模式列");

            // 🔴 前两列都是判据列（对账 · 裁留图）⇒ sf 列的下标 5 → 6 → **7**（列下标是**多处**约定的，改一处要全改）
            var modeColumn = columns[7];
            Assert.That(modeColumn.KernelMode, Is.EqualTo(ScaleMode.ScaleWithScreenSize), "sf 列声明的是内核的画布缩放分支");
        }

        /// <summary>（源码级反例）：**表头文本与"是不是模式列"解耦** —— 表头不以成员名开头也必须能数对。</summary>
        [Test]
        public void PK10_ModeColumnCount_DoesNotDependOnTheHeaderText()
        {
            string header = ScaleTableColumns.All[7].Title;
            Assert.That(header, Does.StartWith("sf（"), "设计册 §4.1 定的表头");
            Assert.That(header, Does.Not.Contain("ScaleWithScreenSize"), "表头里**没有**模式成员名");
            Assert.That(ScaleTableColumns.KernelModeColumnCount(), Is.EqualTo(1),
                "靠字符串认就会数成 0（状态栏写「0 模式」）——所以必须靠 Spec.KernelMode 声明");
        }

        /// <summary>`PK18`：列序 = 判据列在前（2026-09-21 起**前两列**都是判据列：对账状态 · 逐档裁留图）；
        /// 最小窗口下**前 5 列**（含「横向」——"会不会裁"那一列）的 MinWidth 之和 ≤ 可用宽。</summary>
        [Test]
        public void PK18_DecisionColumnsComeFirst_AndFitTheMinimumWindow()
        {
            ScaleTableColumns.Spec[] columns = ScaleTableColumns.All;
            string[] order = { "对账", "裁留图", "屏幕档位", "比例", "横向", "纵向", "实际画布", "sf", "屏幕尺寸", "差值" };

            Assert.That(columns.Length, Is.EqualTo(order.Length), "列序表的项数必须与列定义一致（改了列就得改这里）");
            for (int i = 0; i < order.Length; i++)
                Assert.That(columns[i].Title, Does.StartWith(order[i]),
                    "第 " + i + " 列（两个行首判据列 + 原有列序）");

            // 🔴 行首第二列插入「裁留图」（52）⇒ 前五列 = 44 + 52 + 130 + 60 + 130 = **416**
            //    （判据的**意图不变**：不滚动就要能看到"会不会裁"——那由「横向」列给出）
            float firstFive = 0f;
            for (int i = 0; i < 5; i++) firstFive += columns[i].MinWidth;
            Assert.That(firstFive, Is.EqualTo(416f), "44 + 52 + 130 + 60 + 130（对账 · 裁留图 · 档位 · 比例 · 横向）");
            Assert.That(firstFive, Is.LessThanOrEqualTo(MinimumWindowUsableWidth),
                "最小窗口下不滚动就能读到「会不会裁」这个结论");
        }

        /// <summary>的呈现落点：方向对是「比例」列的**后缀** <c>⇄</c>，**不另立列**
        /// （修订：行首多了「对账」与「裁留图」两列，所以「方向对」更不可能单占一列）。</summary>
        [Test]
        public void PK21_OrientationPair_IsASuffixOfTheRatioColumn_NotANinthColumn()
        {
            ScreenProfile portrait = ScreenProfiles.All[0];
            ScreenProfile landscape = ScreenProfiles.All[2];

            Assert.That(ScaleFit.AspectText(landscape, ScaleFit.SameAspect(landscape.Size, LandscapeReference())),
                Does.Not.Contain("⇄"), "没有方向对时不加后缀");
            Assert.That(ScaleFit.AspectText(portrait, false, orientationPair: true), Does.Contain("⇄"));

            Assert.That(ScaleTableColumns.All.Length, Is.EqualTo(10), "10 列：两个判据列 + 原有 8 列");
            Assert.That(ScaleTableColumns.All[3].Title, Is.EqualTo("比例"), "「比例」列在第 4 位（方向对的后缀挂在它上面）");
            foreach (ScaleTableColumns.Spec spec in ScaleTableColumns.All)
                Assert.That(spec.Title, Does.Not.Contain("⇄"), "任何列的**表头**都不许出现方向对记号（它是单元格后缀）");
        }

        /// <summary>
        /// 🔴 **列集可传**是硬要求（现场探针抓到的真 bug）：`ComputeWidths`/`Distribute` 原来按 `All` 定长读每列的
        /// `[min, max]`，**列集比 `All` 长时**后面几列会 `IndexOutOfRange`（当年是"更多列一开就炸"）。
        /// <para>🔴 **2026-09-21 修订**（《界面精简与对账状态列》批 1）：C 批的「更多列」开关与 `Extra` 三列
        /// **整条撤除**，但**"列集可传"这个能力保留**（`specs == null` ⇒ 默认列集）——本用例因此改成
        /// **自己造一个比 `All` 长的列集**，判据强度不降（它守的是"按元素读区间"，与"更多列"这个功能无关）。</para>
        /// </summary>
        [Test]
        public void WidthHelpers_AcceptAnExplicitColumnSet()
        {
            var rows = new List<ScaleTableRow> { Row() };
            ScaleTableColumns.Spec[] explicitSet = ExplicitColumnSet();
            string[] titles = System.Array.ConvertAll(explicitSet, spec => spec.Title);
            Assert.That(titles.Length, Is.GreaterThan(ScaleTableColumns.All.Length), "本用例必须用**更长**的列集才有效");

            float[] content = ScaleTableColumns.ComputeWidths(rows, text => text == null ? 0f : text.Length * 8f,
                                                              explicitSet);
            Assert.That(content.Length, Is.EqualTo(titles.Length), "宽度条数 = 列集条数（不是 All 的长度）");

            float[] distributed = ScaleTableColumns.Distribute(content, 200f, explicitSet);
            Assert.That(distributed.Length, Is.EqualTo(titles.Length));
            for (int i = 0; i < distributed.Length; i++)
            {
                ScaleTableColumns.Spec spec = explicitSet[i];
                Assert.That(distributed[i], Is.GreaterThanOrEqualTo(spec.MinWidth - 0.01f), spec.Title + "：不许小于下限");
                Assert.That(distributed[i], Is.LessThanOrEqualTo(spec.MaxWidth + 0.01f), spec.Title + "：不许大于上限");
            }

            // 默认调用（不传列集）行为不变：仍是列定义单的条数
            Assert.That(ScaleTableColumns.ComputeWidths(rows, _ => 10f).Length, Is.EqualTo(ScaleTableColumns.All.Length));
            Assert.That(ScaleTableColumns.Distribute(new[] { 10f, 10f }, 100f).Length, Is.EqualTo(2));
        }

        /// <summary>造一个**比 `All` 长**的列集：`All` 原样 + 两列测试专用的假列（不碰生产列定义单）。</summary>
        private static ScaleTableColumns.Spec[] ExplicitColumnSet()
        {
            ScaleTableColumns.Spec[] all = ScaleTableColumns.All;
            var set = new ScaleTableColumns.Spec[all.Length + 2];
            System.Array.Copy(all, set, all.Length);
            set[all.Length] = new ScaleTableColumns.Spec("测试列甲", 80f, 120f, row => "甲");
            set[all.Length + 1] = new ScaleTableColumns.Spec("测试列乙", 150f, 230f, row => "乙");
            return set;
        }

        private static ScaleSize LandscapeReference() => new ScaleSize(1920f, 1080f);

        private static ScaleTableRow Row()
        {
            ScaleCalcInput input = Input();
            return new ScaleTableRow
            {
                Profile = new ScreenProfile("手机横屏 16:9", 1920f, 1080f, "档位草案"),
                Reference = new ScaleSize(1920f, 1080f),
                ScreenSize = ScaleCalc.Evaluate(in input),
            };
        }

        private static ScaleCalcInput Input()
        {
            ScaleCalcInput input = ScaleCalcInput.Default;
            input.Mode = ScaleMode.ScaleWithScreenSize;
            input.ScreenSize = new ScaleSize(1920f, 1080f);
            input.ReferenceResolution = new ScaleSize(1920f, 1080f);
            input.ScreenMatch = ScreenMatchMode.MatchWidthOrHeight;
            input.MatchWidthOrHeight = 0.5f;
            return input;
        }
    }
}
