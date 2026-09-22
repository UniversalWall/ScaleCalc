using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// <see cref="ScaleCalcWindow"/> 的**导出章节**（窗口主体在 `ScaleCalcWindow.cs`、
    /// 对账章节在 `ScaleCalcWindow.Measure.cs`）。
    /// <para>两条导出入口**都走保存对话框**（不做路径输入框），区别在**内容**——
    /// 按钮标签必须能一眼分开：</para>
    /// <list type="bullet">
    /// <item>「导出换算证据包（≥5 组）」= <see cref="EvidencePackCommand"/>（6 组参考分辨率 ×（内置档 + 现场行）× 3 个 <c>match</c>，
    /// 16 列；默认目录 = 工程内的证据目录落点，默认文件名 = <c>ScaleCalc-005-换算证据包.csv</c>）；</item>
    /// <item>「导出当前表（CSV…）」= 当前选型台这一张表（15 列），默认文件名 <c>ScaleCalcTable.csv</c>。</item>
    /// </list>
    /// <para>🔴 **导出零副作用**：真值走窗口里**缓存的那一份**（上次对账的结果），
    /// 导出按钮**不再**顺手对一次账 ⇒ 同一次会话连点 10 次导出，场景操作次数 = **0**。
    /// 没对过账就导 ⇒ 真值三列如实写「（离线列）」，并在状态栏说明（**不假装**已对过账）。</para>
    /// <para>两条都写 **UTF-8 带 BOM**（中文 Windows 的 Excel 打开无 BOM 的 UTF-8 会按 GBK 猜 ⇒ 全表乱码，见 <see cref="EvidenceExporter.Export"/>）。</para>
    /// </summary>
    public sealed partial class ScaleCalcWindow
    {
        /// <summary>
        /// 换算证据包 → 保存对话框（默认目录 = 工程内的证据目录落点，默认文件名 = <c>ScaleCalc-005-换算证据包.csv</c>）。
        /// <para>失败把可读原因写进状态栏（不许静默）；用户取消 ⇒ 什么都不做、也不改状态栏。</para>
        /// <para>🔴 覆盖许可 = <c>true</c>：保存对话框的"替换吗？"就是用户的显式确认——**只有这条窗口路径**才这么传，
        /// 脚本路径默认不覆盖。落点由对话框给出，本类不替用户选址。</para>
        /// <para>🔴 真值用**缓存的那一份**；没对过账就是 <c>truthOk = false</c>，导出的是全离线列。</para>
        /// </summary>
        private void OnExportEvidencePack()
        {
            string path = EditorUtility.SaveFilePanel("导出换算证据包（≥5 组）",
                Path.GetDirectoryName(EvidencePackCommand.DefaultPath),
                EvidencePackCommand.EvidenceFileName, "csv");
            if (string.IsNullOrEmpty(path)) return;                 // 取消

            try
            {
                string written = EvidencePackCommand.ExportTo(path, out int rows, m_Truth, m_TruthOk, allowOverwrite: true);
                Report("已导出换算证据包：" + written
                    + "（" + rows + " 条数据 + 注释/表头；UTF-8 带 BOM，Excel 可直接开）"
                    + (m_TruthOk ? " · 真值取自：" + m_TruthStamp : " · ⚠️ 本次会话未对账 ⇒ 真值三列均为离线列"));
            }
            catch (Exception e)
            {
                Report("导出换算证据包失败：" + e.GetType().Name + " / " + e.Message + "（路径：" + path + "）");
            }
        }

        /// <summary>
        /// 当前选型台这一张表 → 保存对话框。
        /// <para>🔴 覆盖许可 = <c>true</c>：同 <see cref="OnExportEvidencePack"/>，依据是保存对话框里用户的确认。</para>
        /// </summary>
        private void OnExport()
        {
            if (m_Table == null) { Report("还没算出表，先等一次重算"); return; }

            string csv = EvidenceExporter.BuildCsv(m_Table);
            string path = EditorUtility.SaveFilePanel("导出当前表（选型台 CSV）", "", "ScaleCalcTable.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;
            EvidenceExporter.Export(csv, path, allowOverwrite: true);
            Report("已导出当前表：" + path + "（" + csv.Split('\n').Length + " 行）");
        }

        /// <summary>
        /// 「导出设计约束单」→ 保存对话框。
        /// <para>内容 = **结论条同源**的一句话 + 安全设计区 + 危险带（左右各 / 上下各）——**给美术/策划的交付物**，
        /// 不是给工程看的台账（那份是证据包）。四行全部来自 <see cref="ScaleFit"/> 的纯函数 ⇒ 与界面上的数一字不差。</para>
        /// <para>🔴 编码 = **UTF-8 带 BOM**（复用 <see cref="EvidenceExporter.Export"/>：无 BOM 会被 Excel 按 GBK 猜）。
        /// 落点由对话框给出（不替用户选址、不建目录），覆盖需用户在对话框里确认。</para>
        /// </summary>
        private void OnExportConstraints()
        {
            List<ScreenProfile> tiers = IncludedProfiles();
            ScaleCalcInput template = CurrentTemplate();
            ScaleSize reference = template.ReferenceResolution;
            ScaleFit.ModeStats stats = ScaleFit.Stats(tiers, reference, template.ScreenMatch,
                                                      template.MatchWidthOrHeight, template.ScreenDpi);

            string path = EditorUtility.SaveFilePanel("导出设计约束单（几何口径）", "",
                                                      "ScaleCalc-设计约束单.txt", "txt");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                EvidenceExporter.Export(BuildConstraintsText(stats, reference, tiers.Count), path, allowOverwrite: true);
                Report("已导出设计约束单：" + path + "（几何口径；UTF-8 带 BOM）");
            }
            catch (Exception e)
            {
                Report("导出设计约束单失败：" + e.GetType().Name + " / " + e.Message + "（路径：" + path + "）");
            }
        }

        /// <summary>
        /// 《设计约束单》的四行文本（与结论条**同源**；**静态纯函数** ⇒ 可逐字断言）。
        /// <para>措辞纪律：**几何口径 ≠ 布局保证**——限定语必须与数字同屏，
        /// 所以文件头那句「几何口径，不代表具体布局」**不是可选项**。</para>
        /// </summary>
        public static string BuildConstraintsText(ScaleFit.ModeStats stats, ScaleSize reference, int tierCount)
        {
            string head = "# ScaleCalc 设计约束单（几何口径，不代表具体布局；结论只对当前勾选的 "
                        + tierCount + " 档负责）";
            return head
                 + "\n" + ScaleFit.Headline(stats, reference)
                 + "\n" + ScaleFit.SafeAreaLine(stats, reference)
                 + "\n" + ScaleFit.BandLine(stats, reference);
        }

        /// <summary>
        /// 「导出当前视图」→ 保存对话框。
        /// <para>🔴 **这是第三条独立链路，与既有一条不同源**：它导的是**你现在看到的这张表**（列数 = <see cref="ScaleTableColumns.All"/> 的长度，
        /// 即界面上的列定义单，含行首「对账」列与第 2 列「裁留图」）、**当前的排序与筛选**、真值列照旧如实标注。
        /// 既有的「导出当前表（CSV…）」是 <see cref="EvidenceExporter"/> 的 **15 列**固定格式（给工程看的台账）
        /// ⇒ 两条**都不是**对方的替代品（标签必须自带口径）。</para>
        /// <para>🔴 UTF-8 带 BOM（复用 <see cref="EvidenceExporter.Export"/>）、走保存对话框。</para>
        /// </summary>
        private void OnExportCurrentView()
        {
            if (m_Table == null) { Report("还没算出表，先等一次重算"); return; }

            List<ScaleTableRow> view = ScaleFit.ViewRows(m_Table.Rows, m_SortKey, m_SortAscending, OnlyCropped, OnlyPortrait);
            string path = EditorUtility.SaveFilePanel(
                "导出当前视图（所见 " + ScaleTableColumns.All.Length + " 列）", "", "ScaleCalc-当前视图.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                EvidenceExporter.Export(BuildViewCsv(view), path, allowOverwrite: true);
                Report("已导出当前视图：" + path + "（" + view.Count + " 行 × " + ScaleTableColumns.All.Length + " 列；筛选/排序已计入）");
            }
            catch (Exception e)
            {
                Report("导出当前视图失败：" + e.GetType().Name + " / " + e.Message + "（路径：" + path + "）");
            }
        }

        /// <summary>
        /// 当前视图的 CSV（**静态纯函数** ⇒ 可逐字断言）：表头与取值都从**列定义单**读 ⇒ 与界面同源。
        /// </summary>
        public static string BuildViewCsv(IReadOnlyList<ScaleTableRow> view)
        {
            ScaleTableColumns.Spec[] specs = ScaleTableColumns.All;
            var builder = new System.Text.StringBuilder();
            for (int i = 0; i < specs.Length; i++)
            {
                if (i > 0) builder.Append(',');
                builder.Append(CsvField(specs[i].Title));
            }
            if (view != null)
            {
                foreach (ScaleTableRow row in view)
                {
                    builder.Append('\n');
                    for (int i = 0; i < specs.Length; i++)
                    {
                        if (i > 0) builder.Append(',');
                        builder.Append(CsvField(specs[i].ValueOf(row)));
                    }
                }
            }
            return builder.ToString();
        }

        /// <summary>CSV 字段转义（含逗号/引号/换行的值套引号；引号翻倍）——本导出是**给别人开**的，必须守规矩。</summary>
        private static string CsvField(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            bool quote = value.IndexOf(',') >= 0 || value.IndexOf('"') >= 0
                      || value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0;
            return quote ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
        }

        /// <summary>
        /// 把结果/失败原因写到**操作反馈行**（<c>tier-status</c>）。
        /// <para>🔴 **它在界面上是唯一的反馈出口**（总结行与分支栏两个节点已整条撤除）⇒ 反馈行没取到时**消息会丢**，
        /// 这是如实行为，不再退回别的控件。</para>
        /// </summary>
        private void Report(string text)
        {
            if (m_TierStatus != null) m_TierStatus.text = text;
        }
    }
}
