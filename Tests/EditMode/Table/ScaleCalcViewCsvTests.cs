using System.Collections.Generic;
using NUnit.Framework;
using Wayward.ScaleCalc.Editor;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// **「导出当前视图」的 CSV**（C 批，设计册 §6.5）：表头与取值都从**列定义单**读 ⇒ 与界面同源（`PK10`）；
    /// 转义必须守规矩（这份是**给别人开**的）。
    /// <para>🔴 为什么另立文件：它与"字符条/示意图"是两件事（**导出** vs **可视化**），
    /// 而 `ScaleFitBarTests` 加完这两条就到 202 行（200 行红线当场拦下）。</para>
    /// </summary>
    public sealed class ScaleCalcViewCsvTests
    {
        private static readonly ScaleSize Reference = new ScaleSize(1920f, 1080f);

        private static List<ScaleTableRow> BuiltInRows()
        {
            var rows = new List<ScaleTableRow>();
            foreach (ScreenProfile profile in ScreenProfiles.All)
            {
                ScaleCalcInput input = ScaleCalcInput.Default;
                input.Mode = ScaleMode.ScaleWithScreenSize;
                input.ScreenSize = profile.Size;
                input.ReferenceResolution = Reference;
                input.ScreenMatch = ScreenMatchMode.MatchWidthOrHeight;
                input.MatchWidthOrHeight = 0.5f;
                rows.Add(new ScaleTableRow { Profile = profile, Reference = Reference, ScreenSize = ScaleCalc.Evaluate(in input) });
            }
            return rows;
        }

        /// <summary>表头 = 列定义单的标题（同源）；行数 = 视图行数；顺序 = 视图顺序（排序已生效）。</summary>
        [Test]
        public void ViewCsv_HeaderComesFromTheColumnDefinitions()
        {
            List<ScaleTableRow> view = ScaleFit.ViewRows(BuiltInRows(), ScaleFit.SortKey.CropHorizontal, false, false, false);
            string csv = ScaleCalcWindow.BuildViewCsv(view);
            string[] lines = csv.Split('\n');

            Assert.That(lines.Length, Is.EqualTo(1 + view.Count), "表头 + 每个视图行");
            string expectedHeader = string.Join(",", System.Array.ConvertAll(ScaleTableColumns.All, spec => spec.Title));
            Assert.That(lines[0], Is.EqualTo(expectedHeader), "表头 = 列定义单的标题（同源，PK10）");
            Assert.That(lines[0].Split(',').Length, Is.EqualTo(ScaleTableColumns.All.Length), "列数读书定义单，不写字面量");
            // 🔴 **前两列是两个判据列**（对账 · 裁留图）⇒ 每行以「状态字, 图的文本摘要, 档位名」开头。
            //    期望值**从列定义单推**（不抄字面量）：抄字面量的话，改一次列序就要手改一次期望。
            ScaleTableRow first = view[0];
            string expectedHead = ScaleTableColumns.All[0].ValueOf(first) + ","
                                + ScaleTableColumns.All[1].ValueOf(first) + ",";
            Assert.That(expectedHead, Does.StartWith("—,"), "离线 ⇒ 状态列为 —（真值只对现场那一行有效）");
            Assert.That(lines[1], Does.StartWith(expectedHead), "排序后的第一行：前两格与列定义单同源");
            Assert.That(lines[1], Does.Contain(first.Profile.Name), "档位名在第 3 格（没有塞进前两格）");
        }

        /// <summary>转义：值里有逗号/引号必须套引号、引号翻倍。</summary>
        [Test]
        public void ViewCsv_EscapesFieldsWithCommasAndQuotes()
        {
            ScaleCalcInput input = ScaleCalcInput.Default;
            input.Mode = ScaleMode.ScaleWithScreenSize;
            input.ScreenSize = Reference;
            input.ReferenceResolution = Reference;
            var row = new ScaleTableRow
            {
                Profile = new ScreenProfile("怪名字, 带\"引号\"", 1920f, 1080f, "注入"),
                Reference = Reference,
                ScreenSize = ScaleCalc.Evaluate(in input),
            };
            string csv = ScaleCalcWindow.BuildViewCsv(new List<ScaleTableRow> { row });

            Assert.That(csv, Does.Contain("\"怪名字, 带\"\"引号\"\"\""), "逗号 ⇒ 套引号；内部引号 ⇒ 翻倍");
        }
    }
}
