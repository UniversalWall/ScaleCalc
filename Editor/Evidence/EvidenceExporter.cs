using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// 证据导出：**四列**（内核值 / 真值 / 差值 / **输入源**）。
    /// <para>导出的东西必须**可复现**：命令与口径写在文件头注释行里，拿到文件的人照着能重跑一遍。</para>
    /// </summary>
    public static class EvidenceExporter
    {
        /// <summary>
        /// 15 列（**不是这里手写的字面量**）：列定义住在 <see cref="EvidenceColumns.TableHeader"/>，
        /// 与「换算证据包」共用同一份"内核三列 + 行尾七列"。本常量保留原名，既有引用零改动。
        /// </summary>
        public const string CsvColumns = EvidenceColumns.TableHeader;

        /// <summary>表格 → CSV 文本（纯函数，便于断言；不开窗口也能导）。</summary>
        public static string BuildCsv(ScaleTable table)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# ScaleCalc 证据包（com.wayward.scalecalc）");
            sb.AppendLine("# 复现：打开选型台（菜单 Wayward/ScaleCalc/打开选型台）→ 点「导出当前表（CSV…）」；或调用 EvidenceExporter.BuildCsv(ScaleTable) 取文本");
            sb.AppendLine("# 口径：内核值 = Runtime/Core 的纯函数；真值 = 现场 Canvas（显式反射 Handle() 后读数）；容差 1e-3");
            sb.AppendLine("# 输入源列区分「内核离线计算」与现场来源；真值只对现场测过的那一行成立");
            sb.AppendLine(CsvColumns);

            foreach (ScaleTableRow row in table.Rows)
            {
                AppendRow(sb, row, row.PixelSize, "ConstantPixelSize", table);
                AppendRow(sb, row, row.ScreenSize, "ScaleWithScreenSize", table);
                AppendRow(sb, row, row.PhysicalSize, "ConstantPhysicalSize", table);
            }
            return sb.ToString();
        }

        /// <summary>
        /// **兜底链诊断**的 CSV —— 6 列 × **1~2 行**（行数随本机读数是否可得，**不写死**）。
        /// <para>答的是"本机是多少 / 一旦不报退化成多少"。
        /// 早先还有一条 40 行的"物理单位 × dpi 档位"覆盖面矩阵，已整条删除——它答的是"几种纸面档位各算出什么"，
        /// 而其中 35 行永远停在"未实测"，与唯一一行真值并排只会让人误以为"覆盖了 8 种机型"。</para>
        /// </summary>
        public static string BuildFallbackCsv(float localDpi, float fallbackScreenDpi,
                                              float defaultSpriteDpi, float referencePixelsPerUnit)
            => PhysicsFallbackDiagnostic.ToCsv(PhysicsFallbackDiagnostic.Build(
                localDpi, fallbackScreenDpi, defaultSpriteDpi, referencePixelsPerUnit));

        /// <summary>
        /// 写文件（窗口按钮用；路径由调用方决定，测试写临时目录）。
        /// <para>⚠️ <b>写盘前两条硬前置</b>——落点只能来自**保存对话框**或**调用方传进来的路径**，
        /// 本方法**不替调用方选址、也不替它决定覆盖**：</para>
        /// <list type="number">
        /// <item><b>父目录必须已存在</b>：包内**不建目录链**（在别人的工程里凭空造目录正是越界）；
        /// 不存在或给不出父目录 ⇒ <see cref="DirectoryNotFoundException"/>。窗口的保存对话框天然只能选到已存在的目录，
        /// 所以这条不影响窗口体验。</item>
        /// <item><b>覆盖需显式许可</b>：目标文件已存在而未传 <paramref name="allowOverwrite"/>=<c>true</c> ⇒
        /// <see cref="IOException"/>，**文件一个字节都不动**（不静默覆盖）。窗口那条路传 <c>true</c>——保存对话框的
        /// "替换吗？"就是用户的显式许可。</item>
        /// </list>
        /// <para>两条判据都在写盘**之前**判定：拒绝路径零副作用（磁盘上不缺不多任何东西）。</para>
        /// <para>⚠️ **默认必须带 BOM**：中文 Windows 的 Excel 打开"无 BOM 的 UTF-8"会按 ANSI(GBK) 猜 ⇒ 全表中文乱码
        /// （现场实测截图确认过）。带 BOM 后 Excel 自动认 UTF-8；<c>File.ReadAllText</c> 会吃掉 BOM，不影响断言与再解析。</para>
        /// <para><paramref name="withBom"/>=<c>false</c> 是给**要入版本库、要被人手编辑**的文本用的（档位清单）——
        /// 那种文件里多出来的 3 个字节会被 git 当内容差异、被别的编辑器当怪字符。**默认值不变** ⇒ 既有 4 个调用点零改动。</para>
        /// </summary>
        /// <exception cref="ArgumentException"><paramref name="path"/> 为空。</exception>
        /// <exception cref="DirectoryNotFoundException">父目录不存在（或路径给不出父目录）。</exception>
        /// <exception cref="IOException">目标文件已存在且未获覆盖许可。</exception>
        public static void Export(string csv, string path, bool allowOverwrite, bool withBom = true)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("导出路径为空", nameof(path));

            // ① 不建目录：父目录必须已经存在。先于覆盖判定，保证两条拒绝路径都不写盘。
            string folder = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                throw new DirectoryNotFoundException("导出目录不存在：" + (string.IsNullOrEmpty(folder) ? "（路径里没有目录部分）" : folder)
                    + "——本作品不替你在别人的工程里造目录，请先选一个已存在的目录。");

            // ② 不静默覆盖：已存在而未获许可 ⇒ 如实抛错，文件保持原样。
            if (File.Exists(path) && !allowOverwrite)
                throw new IOException("目标文件已存在：" + Path.GetFileName(path)
                    + "——未获覆盖许可，已如实拒绝写盘（原文件未改动）。要覆盖请显式传 allowOverwrite: true。");

            File.WriteAllText(path, csv, new UTF8Encoding(withBom));
        }

        private static void AppendRow(StringBuilder sb, ScaleTableRow row, in ScaleCalcResult result, string mode, ScaleTable table)
        {
            var ci = CultureInfo.InvariantCulture;
            sb.Append(row.Profile.Name).Append(',')
              .Append(row.Profile.Size.ToString()).Append(',')
              .Append(row.Reference.ToString()).Append(',')
              .Append(table.MatchWidthOrHeight.ToString("G4", ci)).Append(',')
              .Append(mode).Append(',')
              .Append(result.ScaleFactor.ToString("G9", ci)).Append(',')
              .Append(result.ReferencePixelsPerUnit.ToString("G9", ci)).Append(',')
              .Append(result.HasCanvasSize ? result.CanvasSize.ToString() : "（无定义）").Append(',');

            // 真值三列 + 差值三列 + 输入源：**拼法在 `EvidenceColumns` 只有一处**。
            // 本导出一次给三种模式，而真值只可能落在 `ScaleWithScreenSize` 那一行 ⇒ 谓词由**本调用方**给。
            bool truth = row.HasTruth && mode == "ScaleWithScreenSize";
            EvidenceColumns.AppendTruthTail(sb, row, truth, ci);
        }
    }
}
