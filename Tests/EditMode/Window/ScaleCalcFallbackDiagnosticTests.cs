using System.Collections.Generic;
using NUnit.Framework;
using Wayward.ScaleCalc.Editor;
using Wayward.ScaleCalc.Unity;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// 兜底链诊断的两种来源可区分 · 本机读数缺位时不凑条。
    /// <para>职责不同：本文件管**物理兜底诊断**，<c>ScaleCalcTableExportTests</c> 管**选型台表的 CSV**。
    /// 本文件另承接原先挤在别处的两条导出断言。</para>
    /// <para>🔴 **常量引 <see cref="HandCalcSamples"/>，不重抄字面量**；🔴 **本机条只做结构性断言**——
    /// 现场读数是**平台的**（本机实测 144，换台机器就是别的数），写死它这条断言在别人机器上必挂。</para>
    /// </summary>
    public sealed class ScaleCalcFallbackDiagnosticTests
    {
        private static readonly ScaleSize Reference = new ScaleSize(1920f, 1080f);

        /// <summary>诊断用的三参数取内核默认（`96` / `96` / `100`），与窗口那条路一致。</summary>
        private static List<PhysicsFallbackDiagnostic.FallbackRow> BuildWithLocal(float localDpi)
            => PhysicsFallbackDiagnostic.Build(localDpi,
                ScaleCalcInput.Default.FallbackScreenDPI,
                ScaleCalcInput.Default.DefaultSpriteDPI,
                ScaleCalcInput.Default.ReferencePixelsPerUnit);

        /// <summary>取某来源的那一条；取不到即断言失败（比返回 null 更容易定位）。</summary>
        private static PhysicsFallbackDiagnostic.FallbackRow RowOf(
            List<PhysicsFallbackDiagnostic.FallbackRow> rows, PhysicsFallbackDiagnostic.DpiOrigin origin)
        {
            foreach (PhysicsFallbackDiagnostic.FallbackRow row in rows)
                if (row.Origin == origin) return row;

            Assert.Fail("诊断里缺少来源为 " + origin + " 的那一条（实测条数 " + rows.Count + "）");
            return default;
        }

        [Test]
        public void TwoRows_SeparateOriginStates()
        {
            // 用**手算样本**里那个真报值（160），不引现场读数
            float reportedDpi = HandCalcSamples.Physical_ReportedDpi160.ScreenDpi;
            List<PhysicsFallbackDiagnostic.FallbackRow> rows = BuildWithLocal(reportedDpi);

            Assert.That(rows.Count, Is.EqualTo(2), "本机读数可得 ⇒ 本机条 + 兜底条");
            PhysicsFallbackDiagnostic.FallbackRow measured =
                RowOf(rows, PhysicsFallbackDiagnostic.DpiOrigin.MeasuredLocal);
            PhysicsFallbackDiagnostic.FallbackRow fallback =
                RowOf(rows, PhysicsFallbackDiagnostic.DpiOrigin.FallbackConstant);

            // ── 两种来源必须能区分（原来是"手填/本机实测/未实测"三态，现在只有两态）──
            Assert.That(PhysicsFallbackDiagnostic.OriginText(measured.Origin), Is.EqualTo("本机实测"));
            Assert.That(PhysicsFallbackDiagnostic.OriginText(fallback.Origin), Is.EqualTo("兜底常量"));
            Assert.That(measured.Origin, Is.Not.EqualTo(fallback.Origin));

            // ── 本机条：**只做结构性断言**（不写死"等于 2"——那是"144 除以 72"的巧合）──
            Assert.That(measured.InputDpi, Is.EqualTo(reportedDpi));
            Assert.That(measured.UsedFallback, Is.False, "非 0 的 dpi 不该被当成兜底");
            Assert.That(measured.EffectiveDpi, Is.EqualTo(measured.InputDpi), "没走兜底 ⇒ 实际 dpi 就是输入 dpi");
            Assert.That(measured.ScaleFactor, Is.EqualTo(measured.InputDpi / 72f).Within(HandCalcSamples.Tolerance),
                "Points ⇒ targetDPI = 72");

            // ── 兜底条：引手算常量（96/72 与 100*72/96 都已有实测常量，不许重抄）──
            Assert.That(fallback.InputDpi, Is.EqualTo(0f));
            Assert.That(fallback.UsedFallback, Is.True, "ScreenDpi == 0 必须被标成走兜底");
            Assert.That(fallback.EffectiveDpi, Is.EqualTo(fallback.FallbackScreenDpi), "兜底后实际参与算式的是常量本身");
            Assert.That(fallback.FallbackScreenDpi, Is.EqualTo(ScaleCalcInput.Default.FallbackScreenDPI));
            Assert.That(fallback.ScaleFactor,
                Is.EqualTo(HandCalcSamples.Physical_FallbackDpi96_ScaleFactor).Within(HandCalcSamples.Tolerance));
            Assert.That(fallback.ReferencePixelsPerUnit,
                Is.EqualTo(HandCalcSamples.Physical_RefPixelsPerUnit).Within(HandCalcSamples.Tolerance),
                "refPPU 的改写量与 dpi 无关 ⇒ 两条同值");

            // ── 🔴 两条给的是两个"情形"，不是两个"结论"：本机条的 false 不代表这一台不会退化 ──
            Assert.That(fallback.ScaleFactor, Is.LessThan(measured.ScaleFactor),
                "160 报得出时兜底常量 96 更小 ⇒ 退化是**变小**（这句话只描述假设情形，不是对本机的结论）");
        }

        [Test]
        public void MissingLocalDpi_YieldsFallbackRowOnly()
        {
            List<PhysicsFallbackDiagnostic.FallbackRow> rows = BuildWithLocal(0f);

            Assert.That(rows.Count, Is.EqualTo(1), "本机读数缺位 ⇒ 只出兜底一条（多出的那条输入也是 0、语义重复）");
            PhysicsFallbackDiagnostic.FallbackRow only = rows[0];
            Assert.That(only.Origin, Is.EqualTo(PhysicsFallbackDiagnostic.DpiOrigin.FallbackConstant),
                "缺位时那一条是兜底常量，不是「未取得」的空条");

            // 🔴 不管本机读不读得到，「走了哪个常量 + 对应的 scaleFactor」都得拿得到
            Assert.That(only.FallbackScreenDpi, Is.EqualTo(ScaleCalcInput.Default.FallbackScreenDPI));
            Assert.That(only.EffectiveDpi, Is.EqualTo(only.FallbackScreenDpi));
            Assert.That(only.UsedFallback, Is.True);
            Assert.That(only.ScaleFactor,
                Is.EqualTo(HandCalcSamples.Physical_FallbackDpi96_ScaleFactor).Within(HandCalcSamples.Tolerance),
                "那个 scaleFactor 在任何环境都拿得到");

            // 负读数同样按"缺位"处理（内核会把非正 dpi 归一化，诊断不拿它当真有读数）
            Assert.That(BuildWithLocal(-1f).Count, Is.EqualTo(1), "负读数也算缺位");
        }

        [Test]
        public void FallbackInfoText_StatesReadingAndFallback_WithoutConcluding()
        {
            float reportedDpi = HandCalcSamples.Physical_ReportedDpi160.ScreenDpi;
            string withReading = PhysicsFallbackDiagnostic.FallbackInfoText(reportedDpi,
                ScaleCalcInput.Default.FallbackScreenDPI,
                ScaleCalcInput.Default.DefaultSpriteDPI,
                ScaleCalcInput.Default.ReferencePixelsPerUnit);

            Assert.That(withReading, Does.Contain("本机读数"));
            Assert.That(withReading, Does.Contain("兜底"));
            Assert.That(withReading, Does.Contain(ScaleCalcInput.Default.FallbackScreenDPI.ToString("G9",
                System.Globalization.CultureInfo.InvariantCulture)), "要打印**走了哪个常量**");
            // 🔴 本机那一侧的 `走兜底=否` 只是"假如读到这个数"的**情形**，
            //    不许写成对这台真机的结论（那正是这条保留项从"有用"变"误导"的开关）
            Assert.That(withReading, Does.Not.Contain("这一台不会走兜底"), "不许把假设情形写成对真机的结论");

            string withoutReading = PhysicsFallbackDiagnostic.FallbackInfoText(0f,
                ScaleCalcInput.Default.FallbackScreenDPI,
                ScaleCalcInput.Default.DefaultSpriteDPI,
                ScaleCalcInput.Default.ReferencePixelsPerUnit);
            Assert.That(withoutReading, Does.Contain("未取得"), "读数缺位要如实说，不许编一个数");
            Assert.That(withoutReading, Does.Contain("若报 0 则走兜底"));
        }

        [Test]
        public void FallbackCsv_ColumnsAndRowsComeFromTheColumnDefinition()
        {
            List<PhysicsFallbackDiagnostic.FallbackRow> rows = BuildWithLocal(
                HandCalcSamples.Physical_ReportedDpi160.ScreenDpi);
            string csv = PhysicsFallbackDiagnostic.ToCsv(rows);
            string[] lines = csv.Split('\n');

            Assert.That(PhysicsFallbackDiagnostic.ColumnCount, Is.EqualTo(6), "兜底诊断 6 列（原覆盖表 7 列已作废）");
            Assert.That(lines[0].TrimEnd('\r'), Is.EqualTo(PhysicsFallbackDiagnostic.CsvHeader),
                "CSV 表头必须**就是**列标题拼的");
            Assert.That(lines[0], Does.Contain("走兜底").And.Contain("兜底常量"), "两列的语义要能在表头读出来");

            // 逐格必须由列定义产生（不许第二套格式）
            for (int r = 0; r < rows.Count; r++)
            {
                string[] cells = lines[r + 1].TrimEnd('\r').Split(',');
                Assert.That(cells.Length, Is.EqualTo(PhysicsFallbackDiagnostic.ColumnCount), "第 " + r + " 条格数");
                for (int c = 0; c < cells.Length; c++)
                    Assert.That(cells[c], Is.EqualTo(PhysicsFallbackDiagnostic.All[c].ValueOf(rows[r])),
                        "第 " + r + " 条第 " + c + " 列必须由列定义产生");
            }
        }

        // ── 以下两条由 `ScaleCalcCoverageAndExportTests` 迁入（覆盖表作废后，只剩"当前表导出"要守）──

        [Test]
        public void Evidence_Csv_HasFourColumnGroupsAndOfflineMarkers()
        {
            ScaleTable table = ScaleTable.Build(Reference, ScreenMatchMode.MatchWidthOrHeight, 0.5f, 144f,
                                                ScaleMode.ScaleWithScreenSize, default, false, default);
            string csv = EvidenceExporter.BuildCsv(table);

            Assert.That(csv, Does.Contain("内核scaleFactor"));
            Assert.That(csv, Does.Contain("真值scaleFactor"));
            Assert.That(csv, Does.Contain("差值scaleFactor"));
            Assert.That(csv, Does.Contain("输入源"));
            Assert.That(csv, Does.Contain("内核离线计算"), "没有真值时，输入源列如实写「内核离线计算」");
            Assert.That(csv, Does.Contain("（离线列）"));

            string[] lines = csv.Split('\n');
            int dataLines = CountNonEmpty(lines) - 5;               // 4 行注释 + 1 行表头
            Assert.That(dataLines, Is.EqualTo(table.Rows.Count * 3), "每行档位 × 三种模式各一条");
        }

        [Test]
        public void Evidence_Csv_TruthRowCarriesNumbersAndSource()
        {
            var liveSize = new ScaleSize(1080f, 1920f);
            var truth = new LiveTruth(1f, 100f, new ScaleSize(1080f, 1920f), liveSize, liveSize,
                                      ScreenSizeSource.CanvasRenderingDisplaySize,
                                      measuredReference: Reference, measuredMatch: 0.5f, measuredScreenMatch: ScreenMatchMode.MatchWidthOrHeight);   // 测量条件必须与本表一致
            ScaleTable table = ScaleTable.Build(Reference, ScreenMatchMode.MatchWidthOrHeight, 0.5f, 144f,
                                                ScaleMode.ScaleWithScreenSize, truth, true, default);

            string csv = EvidenceExporter.BuildCsv(table);
            Assert.That(csv, Does.Contain("CanvasRenderingDisplaySize"), "真值行的输入源要写成现场来源");
            Assert.That(csv, Does.Contain("1080x1920"), "真值行要带真值画布尺寸");
            Assert.That(csv, Does.Contain("（离线列）"), "其余行仍然如实标离线");
        }

        private static int CountNonEmpty(string[] lines)
        {
            int count = 0;
            foreach (string line in lines) if (!string.IsNullOrWhiteSpace(line)) count++;
            return count;
        }
    }
}
