using NUnit.Framework;
using Wayward.ScaleCalc.Editor;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// **换算证据包的落盘与自述**（编码 / 行数 / 真值可得范围）。
    /// <para>那条 CSV 导出分文件：拆法 = 按"导出产物"分组（这里是**证据包**，那里是**当前表**）。</para>
    /// <para>⚠️ **这里分成两类**：**离线**断言（不需要引擎真值 ⇒ 可脱离宿主成立）与
    /// **现场**断言（必须先显式走 <see cref="EvidencePackCommand.MeasureLive"/> 取真值）。
    /// 现场那条**如实声明**：<c>MeasureLive</c> 是"宿主 = 当前活动场景"的生产入口，所以它会读活动场景；
    /// 但它只建/拆**自己那个**临时场景——活动场景本身不被换走、不被关闭、不被另存。
    /// 活动场景未保存时对账会被协议直接拒绝，本用例按契约**如实跳过**（不是通过）。</para>
    /// </summary>
    public sealed class ScaleCalcEvidencePackTests
    {
        private static readonly ScaleSize Reference = new ScaleSize(1920f, 1080f);

        /// <summary>
        /// 两份证据格式**共用同一份列定义**的**机械判据**（<see cref="EvidenceColumns"/>）——
        /// 列数分别 **15 / 16**（差的两列是有意的：证据包最前多「参考分辨率」、中段多「溢出/留白方向」）、
        /// **末尾七列逐字相同**；并且**每一条数据行的列数都等于它自己表头的列数**
        /// ⇒ "写入器多拼一列、表头没跟上"这种漂移会当场红（要防的正是"下游不知道该按哪份解析"）。
        /// </summary>
        [Test]
        public void S3_BothEvidenceFormats_ShareOneColumnDefinition()
        {
            string[] tableCols = EvidenceColumns.TableHeader.Split(',');
            string[] packCols = EvidenceColumns.PackHeader.Split(',');

            Assert.That(tableCols.Length, Is.EqualTo(15), "「当前表」导出 = 15 列");
            Assert.That(packCols.Length, Is.EqualTo(16), "「换算证据包」= 16 列（多的两列：最前 + 中段）");
            Assert.That(EvidenceExporter.CsvColumns, Is.EqualTo(EvidenceColumns.TableHeader),
                "当前表导出的表头必须**就是**列定义单里那一条（不许再在导出器里手写字面量）");

            int tail = EvidenceColumns.TruthTail.Split(',').Length;
            Assert.That(tail, Is.EqualTo(7), "共用行尾 = 真值三列 + 差值三列 + 输入源");
            Assert.That(TailOf(packCols, tail), Is.EqualTo(TailOf(tableCols, tail)),
                "两份格式的末尾七列必须逐字相同——下游按同一套列解析");
            Assert.That(TailOf(tableCols, 1), Is.EqualTo("输入源"), "行尾最后一列是「输入源」（W-ScaleCalc-11 的第 4 列）");

            ScaleTable table = ScaleTable.Build(Reference, ScreenMatchMode.MatchWidthOrHeight, 0.5f, 144f,
                                                ScaleMode.ScaleWithScreenSize, default, false, default);
            // 分母**由模型推导**（7 档 × 3 模式 = 21 条；现场行只增加"模型行"，不额外多一行）：写死数会随档位数变而红在别处
            AssertDataRowsMatchHeader(EvidenceExporter.BuildCsv(table), EvidenceColumns.TableHeader, table.Rows.Count * 3, "当前表");
            AssertDataRowsMatchHeader(EvidencePackCommand.BuildCsv(), EvidenceColumns.PackHeader, 126, "换算证据包");
        }

        /// <summary>取列数组的末尾 <paramref name="count"/> 列（拼回一行，便于逐字比较）。</summary>
        private static string TailOf(string[] columns, int count)
            => string.Join(",", columns, columns.Length - count, count);

        /// <summary>
        /// 核对**实际表头逐字等于列定义单**、随后逐行核对列数，并**同时钉住分母**
        /// （数据行数 = <paramref name="expectedRows"/>：今天刚吃过"扫空集 = 假绿"的亏，分母也要断言）。
        /// <para>第一条非注释行 = 表头（与 `Offline_BuildCsv_HeaderRowCountMatchesActualRows_AndCarriesNoTruth` 同一口径）。</para>
        /// </summary>
        private static void AssertDataRowsMatchHeader(string csv, string declaredHeader, int expectedRows, string name)
        {
            int columns = declaredHeader.Split(',').Length;
            int headers = 0, rows = 0;
            foreach (string raw in csv.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Length == 0 || line.StartsWith("#", System.StringComparison.Ordinal)) continue;
                if (headers == 0)
                {
                    headers = 1;
                    Assert.That(line, Is.EqualTo(declaredHeader), name + "：实际表头必须逐字等于列定义单里的那一条");
                    continue;
                }
                rows++;
                Assert.That(line.Split(',').Length, Is.EqualTo(columns),
                    name + "：这条数据行的列数与表头不一致（列定义漂移）：" + line);
            }
            Assert.That(headers, Is.EqualTo(1), name + "：必须正好一行表头");
            Assert.That(rows, Is.EqualTo(expectedRows), name + "：数据行数（分母也要断言，否则空扫描 = 假绿）");
        }
        /// <summary>
        /// 落盘必须是 **UTF-8 带 BOM**——中文 Windows 的 Excel 打开"无 BOM 的 UTF-8"会按 GBK 猜，全表中文乱码。
        /// </summary>
        [Test]
        public void Export_WritesUtf8Bom_SoExcelDoesNotMojibake()
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "scalecalc-bom-" + System.Guid.NewGuid().ToString("N") + ".csv");
            try
            {
                // 覆盖许可显式传 true：本用例在临时目录里写自己的独占文件名（默认不覆盖）
                EvidenceExporter.Export("中文列名,值\n档位,1\n", path, true);
                byte[] head = System.IO.File.ReadAllBytes(path);
                Assert.That(head.Length, Is.GreaterThan(3));
                Assert.That(head[0], Is.EqualTo(0xEF), "UTF-8 BOM 第 1 字节");
                Assert.That(head[1], Is.EqualTo(0xBB), "UTF-8 BOM 第 2 字节");
                Assert.That(head[2], Is.EqualTo(0xBF), "UTF-8 BOM 第 3 字节");
                Assert.That(System.IO.File.ReadAllText(path), Does.StartWith("中文列名"), "BOM 不影响按文本读取");
            }
            finally
            {
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }
        }

        /// <summary>
        /// **离线入口**（不给真值）也能出完整证据包，且**行数自述仍然等于实际行数**。
        /// <para>离线时每块 <c>7</c> 档、"现场行"不补 ⇒ 实际 = 6 × 3 × 7 = **126**。这条断言刻意不依赖引擎真值，
        /// 因此**可脱离宿主**成立（消费者工程里也能跑过）。</para>
        /// </summary>
        [Test]
        public void Offline_BuildCsv_HeaderRowCountMatchesActualRows_AndCarriesNoTruth()
        {
            Assert.That(EvidencePackCommand.MaxDataRows, Is.EqualTo(127), "上限 = 6 组参考 × 3 个 match × 7 档 + 1 现场行");
            Assert.That(EvidencePackCommand.ReferenceCount, Is.GreaterThanOrEqualTo(5), "≥5 组参考分辨率（设计口径）");

            string csv = EvidencePackCommand.BuildCsv(out int actual);
            Assert.That(actual, Is.EqualTo(126), "离线没有现场行 ⇒ 6 × 3 × 7 = 126");
            Assert.That(actual, Is.LessThanOrEqualTo(EvidencePackCommand.MaxDataRows));

            int comments = 0, headerLines = 0, data = 0;
            foreach (string raw in csv.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Length == 0) continue;
                if (line.StartsWith("#", System.StringComparison.Ordinal)) { comments++; continue; }
                if (headerLines == 0) { headerLines = 1; continue; }   // 第一条非注释行 = 表头
                data++;
            }

            Assert.That(comments, Is.GreaterThan(0), "文件头是注释行（含复现命令与口径）");
            Assert.That(headerLines, Is.EqualTo(1), "只有一行表头");
            Assert.That(data, Is.EqualTo(actual), "实际数据行数 = BuildCsv(out) 报的数");
            Assert.That(csv, Does.Contain("# 数据行数：" + actual + "（上限 "), "表头注释必须写实际行数（不许再手写乘式）");
            Assert.That(csv, Does.Contain("未取到"), "离线导出要如实说没取到真值");
            Assert.That(csv, Does.Not.Contain("已取到"), "离线导出不许自称取到了真值");
        }

        /// <summary>
        /// 只有"现场条件那一块"（对账用的参考分辨率 × match）能带现场真值的回归断言（**现场类**）。
        /// <para>此前真值只测一次却写进全部 6 组参考块 ⇒ 别的块里 <c>差值 = 两组不同参考分辨率相减</c>
        /// （实测 <c>1280×720</c> 块出现 <c>Δsf=0.667</c> 这种假失败信号）。这条断言逐行检查：凡参考分辨率不是
        /// 对账条件的行，输入源列必须是「内核离线计算」。</para>
        /// <para>走**显式**现场入口取真值，宿主 = 夹具自建场景。</para>
        /// </summary>
        [Test]
        public void EvidencePack_TruthOnlyInTheLiveBlock()
        {
            if (!EvidencePackCommand.MeasureLive(out LiveTruth truth, out string error))
                Assert.Ignore("本次环境取不到现场真值（如实跳过，不是通过）：" + error);

            string csv = EvidencePackCommand.BuildCsv(out _, truth, true);
            string live = truth.MeasuredReference.ToString();
            int truthRows = 0, checkedRows = 0;

            foreach (string raw in csv.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Length == 0 || line.StartsWith("#", System.StringComparison.Ordinal)) continue;

                string[] cols = line.Split(',');
                if (cols.Length < 16 || cols[0] == "参考分辨率") continue;   // 注释/表头
                checkedRows++;

                if (cols[0] == live) { if (cols[15] != "内核离线计算") truthRows++; continue; }
                Assert.That(cols[15], Is.EqualTo("内核离线计算"),
                    "参考分辨率 " + cols[0] + " 那一块不该带现场真值（真值是在 " + live + " 下测的）：" + line);
            }

            Assert.That(checkedRows, Is.GreaterThan(0));
            Assert.That(truthRows, Is.LessThanOrEqualTo(1), "最多一行带现场真值（现场尺寸那一行）");
        }
    }
}
