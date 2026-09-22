using System.IO;
using NUnit.Framework;
using Wayward.ScaleCalc.Editor;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// 选型台的**列定义单**（列数 · 列序 · 取值 · 表头口径）与"那两类整幅宽的诊断列不许回来"。
    /// <para>🔴 **本文件只管"列定义单的形状"**：**宽度与分摊那一族（8 条用例）已整族搬到**
    /// <c>Tests/EditMode/Table/ScaleCalcColumnWidthTests.cs</c>。职责不同——
    /// 本文件原本正好 **200/200**，而要给列定义单加"行首对账列"的断言 ⇒ 一追加就 **236 行**、
    /// 被行数闸门当场拦下（**行数按完整路径单独量，不靠印象**）。
    /// **搬走 ≠ 少守**：8 条用例一条不少，只是换了住处。</para>
    /// <para>**行高**摊开的断言在 <see cref="ScaleTableRowHeightTests"/>（同一批、按职责分文件）。</para>
    /// </summary>
    public sealed class ScaleCalcTableColumnTests
    {
        private static string PackageRoot =>
            UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(ScaleCalcTableColumnTests).Assembly).resolvedPath;

        private static ScaleTableRow LiveRow(string name, float width, float height)
            => new ScaleTableRow { Profile = new ScreenProfile(name, width, height, "现场 Canvas") };

        [Test]
        public void Columns_AreTen_AndDecisionColumnsComeFirst()
        {
            ScaleTableColumns.Spec[] columns = ScaleTableColumns.All;
            string[] titles = { "对账", "裁留图", "屏幕档位", "比例", "横向", "纵向", "实际画布", "sf（1 画布单位 = ? px）", "屏幕尺寸", "差值" };

            Assert.That(columns.Length, Is.EqualTo(titles.Length), "列数 = 列序表的项数（改列就改这里）");
            for (int i = 0; i < titles.Length; i++)
                Assert.That(columns[i].Title, Does.StartWith(titles[i]), "第 " + i + " 列的标题/次序");
            Assert.That(ScaleTableColumns.KernelModeColumnCount(), Is.EqualTo(1), "N 模式 = 列定义声明的模式列个数（不写死）");

            ScaleTableRow live = LiveRow("当前渲染尺寸（现场）", 2560f, 1440f);
            Assert.That(columns[0].ValueOf(live), Is.EqualTo("—"), "第 1 列是对账状态（无真值 ⇒ —）");
            Assert.That(columns[2].ValueOf(live), Is.EqualTo("当前渲染尺寸（现场）"), "第 3 列只有档位名（不塞尺寸）");
            Assert.That(columns[8].ValueOf(live), Is.EqualTo("2560x1440"), "屏幕尺寸列只有尺寸（与证据包 CSV 同构）");
        }

        /// <summary>（**源码级**）：列定义单里搜不到那几列（目标 token 字符拼接构造）。
        /// <para>🔴 <c>All</c> 已搬到 <c>ScaleTableColumns.Verdict.cs</c> ⇒ **两条路径一起查**——只查主文件会变成
        /// "查一个不再定义列的文件"（**假绿**）。</para>
        /// <para>🔴 2026-09-21 扩到「更多列」那三列诊断 ⇒ **必须跳过注释行**：`.Verdict.cs` 的修订注释里
        /// 正当地写着被撤除的列名（记录"撤了什么"），**含注释的搜索会把自己的说明文字当成违规**（本轮实跑踩到）。
        /// ⚠️ 两条口径的差别是有意的：`ConstantPixelSize`/`refPPU` 那些**连注释里也不许有**（它们是漂移隐患），
        /// 而"撤除留痕"那一处**允许**出现 —— 所以只有后者用"跳过注释"的取样器。</para></summary>
        [Test]
        public void K8_TwoFullWidthModeColumns_AreGoneFromTheColumnDefinitions()
        {
            string source = File.ReadAllText(Path.Combine(PackageRoot, "Editor", "Table", "ScaleTableColumns.Verdict.cs"))
                          + File.ReadAllText(Path.Combine(PackageRoot, "Editor", "Table", "ScaleTableColumns.cs"));

            Assert.That(source, Does.Not.Contain("Constant" + "PixelSize"));
            Assert.That(source, Does.Not.Contain("Constant" + "PhysicalSize"));

            // 撤除留痕在注释里 ⇒ 用**只留代码行**的取样器（判据必须对准它真正要守的东西）
            string codeOnly = CodeLinesOf(source);
            Assert.That(codeOnly.Length, Is.GreaterThan(1000), "分母保护：真的剥出了代码");
            Assert.That(codeOnly, Does.Not.Contain("夹取" + "诊断"));
            Assert.That(codeOnly, Does.Not.Contain("ref" + "PPU"));
        }

        /// <summary>剥掉注释行（`//` 与 `///`）后的源码 —— "撤除留痕写在注释里"时用它取样。</summary>
        private static string CodeLinesOf(string source)
        {
            var builder = new System.Text.StringBuilder();
            foreach (string line in source.Split('\n'))
            {
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith("//", System.StringComparison.Ordinal)) continue;
                builder.Append(line).Append('\n');
            }
            return builder.ToString();
        }

        // 🔴 **宽度与分摊那一族（8 条用例 + 2 个助手）已整族搬到**
        //    `Tests/EditMode/Table/ScaleCalcColumnWidthTests.cs`。
        //    职责不同：本文件管"**列定义单的形状**"（列数/列序/取值/表头），
        //    那个文件管"**宽度算法**"（纯算法 + 注入的假测量器）。
        //    触发原因：本批次要给列定义单加"行首对账列"的断言，而本文件**原本正好 200/200**
        //    ⇒ 一追加就 236 行、被行数闸门当场拦下。
        //    **搬走 ≠ 少守**：8 条用例一条不少（只是换了住处），其中 `Widths_UseTheLongestRow_*`
        //    顺势改成**按标题找列下标**（列序一变，写死 `widths[0]` 的判据会静默测错对象）。
    }
}
