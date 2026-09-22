using System.Globalization;
using System.Text;
using Wayward.ScaleCalc.Unity;
using UnityEngine;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// <see cref="EvidencePackCommand"/> 的 **CSV 产出章节**（落点/入口章节在同名的 `EvidencePackCommand.cs`）。
    /// <para>拆法 = **按职责分组**：本文件只管"表怎么算成文本"（表头注释 · 行数判据 · 逐行拼列），
    /// 不含任何"写哪儿"的判定——落点收敛在 <see cref="EvidenceExporter.Export"/> 的两条硬前置里。</para>
    /// <para>⚠️ 本文件里的 <see cref="BuildCsv(out int)"/> 是**离线纯函数**：**不取真值、不做任何场景操作**。
    /// 要现场真值请先走 <see cref="MeasureLive"/>（那一个入口的名字与注释都写明会建/拆场景），再把结果当参数传进来。</para>
    /// </summary>
    public static partial class EvidencePackCommand
    {
        /// <summary>
        /// 证据包 CSV 文本（**不要行数时用这个**）。
        /// <para>⚠️ 这是**离线**入口：不出真值 ⇒ 真值三列标「（离线列）」。要带现场真值请用
        /// <see cref="BuildCsv(out int, LiveTruth, bool)"/>，并**自己**先取真值（<see cref="MeasureLive"/>）。</para>
        /// </summary>
        public static string BuildCsv() => BuildCsv(out _);

        /// <summary>
        /// 证据包 CSV 文本（**离线纯函数**）：不出真值、**一个场景操作都不做**。
        /// <para>离线时每块 <c>ScreenProfiles.All.Length</c> 档 ⇒
        /// <paramref name="dataRows"/> = 参考档数 × match 档数 × 屏幕档数（内置清单下 = 6 × 3 × 7 = **126**），
        /// 表头注释写的也是这个实际值。</para>
        /// <para><b>为什么是"真值从参数进来"这个形状</b>：早先那个 <c>Build()</c> 的名字与签名都像纯函数，
        /// 内部却在建/拆临时场景 ⇒ 调用方以为只是在算字符串，场景已经被动了。现在真值只作为**参数**进来，
        /// 本方法不碰场景；需要现场真值的调用方先走显式入口 <see cref="MeasureLive"/>。</para>
        /// </summary>
        public static string BuildCsv(out int dataRows) => BuildCsv(out dataRows, default, false);

        /// <summary>
        /// 证据包 CSV 文本：<paramref name="truth"/> / <paramref name="truthOk"/> 由**调用方**给出
        /// （<paramref name="truthOk"/> 为 <c>false</c> 时真值三列标「（离线列）」）。**本方法不取真值、不碰场景。**
        /// <para>⚠️ <paramref name="dataRows"/> 是**实际**行数，由两条判据决定（见 <see cref="LiveRowIsSeparate"/>）：
        /// ① 只有「现场参考分辨率 × 现场 match」那一块带现场真值 ⇒ 只有它可能多出 1 行现场行；
        /// ② 现场渲染尺寸恰好落在某个档位里时连那一行也不补。表头注释写的必须是**实际**数——
        /// 曾经出现过"注释写 144、实际 127"的漂移，是靠测试抓出来的。</para>
        /// <para>真值的**条件匹配**不在这里判：由下游 <see cref="ScaleTable.Build"/> 用 <c>truth.AppliesTo</c> 守门，
        /// 条件不符时整表按离线列出。</para>
        /// </summary>
        public static string BuildCsv(out int dataRows, LiveTruth truth, bool truthOk)
        {
            // 先按 `ScaleTable.Build` 的同一判据算出**实际**行数（表头要写它）：
            //   每块 = 内置档位清单的档数；只有"现场条件那一块"可能多补 1 行"现场行"，且现场尺寸不在档位表里时才补
            bool liveRowSeparate = LiveRowIsSeparate(truthOk, in truth);
            dataRows = References.Length * MatchLadder.Length * ScreenProfiles.All.Length + (liveRowSeparate ? 1 : 0);

            var sb = new StringBuilder();
            sb.AppendLine("# ScaleCalc 换算证据包（com.wayward.scalecalc）");
            sb.AppendLine("# 复现：选型台窗口按钮「导出换算证据包（≥5 组）」，或脚本里调用 EvidencePackCommand.ExportTo(path)");
            sb.AppendLine("# 内容：" + References.Length + " 组参考分辨率 × " + MatchLadder.Length + " 个 match × "
                + ScreenProfiles.All.Length + " 档屏幕（现场条件那一块另补 1 行现场行）");
            sb.AppendLine("# 数据行数：" + dataRows + "（上限 " + MaxDataRows + " = " + References.Length + " × " + MatchLadder.Length
                + " × " + ScreenProfiles.All.Length + " + 1 现场行）");
            // 🔴 工作集与证据包**不同源**，必须显式标注，不能让人以为是 bug。
            // 这句是**常量**（不随勾选变）⇒ "改工作集前后逐字节相同"这条基线仍然成立。
            sb.AppendLine("# ⚠️ 证据包 = 内置 " + ScreenProfiles.All.Length + " 档全集，与当前勾选无关");
            sb.AppendLine("#    选型台上的勾选/增删只影响**那张表算哪些行**，不影响本文件——本文件要一直逐字节可复现");
            sb.AppendLine("# ⚠️ 真值只在「参考 " + LiveReference + " × match " + LiveMatch + "」那一块有效：现场只对账一次");
            sb.AppendLine("#    其余块整表为离线列——拿别的条件下的真值来减会得到**假差值**");
            sb.AppendLine("# 编码：UTF-8 带 BOM（中文 Excel 直接打开不乱码）");
            sb.AppendLine("# 口径：内核值 = Runtime/Core 纯函数；真值 = 现场 Canvas（显式反射 Handle() 后读数，容差 1e-3）");
            sb.AppendLine("#       输入源列区分「内核离线计算」与现场来源");
            sb.AppendLine("# 现场真值：" + (truthOk
                ? "已取到（渲染尺寸 " + truth.ScreenSize + "，源 " + truth.Source + "；测量条件 " + truth.ConditionText + "）"
                : "未取到（本次导出按离线列出）"));
            // 🔴 表头是**列定义单**里的那一条：与「当前表」导出共用内核三列 + 行尾七列，
            //    只有"最前的参考分辨率"与"中段的溢出/留白方向"是证据包多出来的两列。
            sb.AppendLine(EvidenceColumns.PackHeader);

            var ci = CultureInfo.InvariantCulture;
            foreach (ScaleSize reference in References)
            {
                foreach (float match in MatchLadder)
                {
                    // 🔴 整表真值语境：**逐块**构造 —— 证据包每块的参考分辨率/match 都不同，
                    //    所以"本表条件"必须用**这一块**的（拿窗口那份会让状态列/悬停自相矛盾）。
                    //    ⚠️ 语境只喂对账状态列与悬停；CSV 列定义来自 `EvidenceColumns`，**与本语境无关**
                    //    ⇒ 逐字节基线**不受影响**。
                    var context = new ReconcileContext(
                        hasSession: truthOk,
                        measuredOk: truthOk,
                        appliesToTable: truthOk && truth.AppliesTo(reference, match, ScreenMatchMode.MatchWidthOrHeight),
                        measuredConditionText: truthOk ? truth.ConditionText : null,
                        tableConditionText: LiveTruth.ConditionTextOf(reference, ScreenMatchMode.MatchWidthOrHeight, match),
                        measuredScreenSize: truth.ScreenSize,
                        truthError: null,
                        provenance: null);

                    ScaleTable table = ScaleTable.Build(reference, ScreenMatchMode.MatchWidthOrHeight, match,
                                                        Screen.dpi, ScaleMode.ScaleWithScreenSize, truth, truthOk, context);
                    // 表里已经包含"当前渲染尺寸（现场）"那一行 —— **一起导出**，别按档位过滤掉它
                    foreach (ScaleTableRow row in table.Rows)
                        AppendRow(sb, reference, row, match, ci);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// 现场行是否会**单独**占一行：只有现场对账成功、且现场渲染尺寸**不落在档位表里**时，
        /// <see cref="ScaleTable.Build"/> 才另补"当前渲染尺寸（现场）"那一行；否则真值直接落在对应档位行上。
        /// <para>这是与 `ScaleTable.Build` **同一判据的复述**，所以它算出来的行数必须等于实际写出的行数——
        /// 由断言 `EvidencePack_HeaderRowCount_MatchesActualDataRows` 机械守住。</para>
        /// </summary>
        private static bool LiveRowIsSeparate(bool truthOk, in LiveTruth truth)
        {
            if (!truthOk) return false;
            foreach (ScreenProfile profile in ScreenProfiles.All)
                if (profile.Size == truth.ScreenSize) return false;
            return true;
        }

        private static void AppendRow(StringBuilder sb, ScaleSize reference, ScaleTableRow row, float match, CultureInfo ci)
        {
            ScaleCalcResult kernel = row.ScreenSize;
            sb.Append(reference.ToString()).Append(',')
              .Append(row.Profile.Name).Append(',')
              .Append(row.Profile.Size.ToString()).Append(',')
              .Append(match.ToString("G4", ci)).Append(',')
              .Append("ScaleWithScreenSize").Append(',')
              .Append(kernel.ScaleFactor.ToString("G9", ci)).Append(',')
              .Append(kernel.ReferencePixelsPerUnit.ToString("G9", ci)).Append(',')
              .Append(kernel.HasCanvasSize ? kernel.CanvasSize.ToString() : "（无定义）").Append(',')
              .Append(AxisVerdict.Describe(in kernel, reference)).Append(',');
            // 真值三列 + 差值三列 + 输入源：**拼法只在 `EvidenceColumns` 一处**。
            // 谓词由本调用方给：`ScaleTable.Build` 已用测量条件守过门 ⇒ 这里只看行上有没有真值。
            EvidenceColumns.AppendTruthTail(sb, row, row.HasTruth, ci);
        }

        /// <summary>
        /// 数据行数的**上限**（= 现场条件那一块也确实补了 1 行现场行时）。
        /// 实际行数以 <see cref="BuildCsv(out int)"/> 的 <c>out</c> 为准（只有一块可能带现场真值）。
        /// </summary>
        public static int MaxDataRows =>
            References.Length * MatchLadder.Length * ScreenProfiles.All.Length + 1;
    }
}
