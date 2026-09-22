using System.Globalization;
using System.Text;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// 两份证据 CSV 的**列定义单一真相源**：列头与"行尾那七列"的拼装**只在这里出现一次**。
    /// <para>🔴 **为什么要有这个类**：<see cref="EvidenceExporter"/>（当前表，**15 列**）与
    /// <see cref="EvidencePackCommand"/>（换算证据包，**16 列**）各自抄一遍列头、也各自实现一遍
    /// "真值三列 / 差值三列 / 输入源"的取数与占位。列数不同是**有意的**（证据包在最前多「参考分辨率」、
    /// 中段多「溢出/留白方向」），但**共用的那一段必须同源**——否则两份格式一旦漂移，
    /// 拿两份 CSV 一起做解析的下游工具就不知道该按哪份对齐。</para>
    /// <para>🔴 **机械判据**（见 <c>ScaleCalcEvidencePackTests</c>）：两份列头列数分别 **15 / 16**、
    /// **末尾七列逐字相同**；且两份产物**每一条数据行的列数都等于它自己表头的列数**
    /// （防"写入器多拼一列、表头没跟上"这种漂移）。</para>
    /// <para>⚠️ 本类**不含**"写哪儿"的判定——落点收敛在 <see cref="EvidenceExporter.Export"/> 的两条硬前置里。</para>
    /// </summary>
    public static class EvidenceColumns
    {
        /// <summary>内核三列：两份格式**位置相同**（紧跟「模式」之后）。</summary>
        public const string KernelTriplet = "内核scaleFactor,内核refPPU,内核画布";

        /// <summary>**共用行尾**七列：真值三列 · 差值三列 · 输入源——两份格式的末尾**逐字一致**。</summary>
        public const string TruthTail = "真值scaleFactor,真值refPPU,真值画布,差值scaleFactor,差值refPPU,差值画布,输入源";

        /// <summary>「当前表」导出的列头（<see cref="EvidenceExporter"/>）：**15 列**。</summary>
        public const string TableHeader = "屏幕档位,屏幕尺寸,参考分辨率,match,模式," + KernelTriplet + "," + TruthTail;

        /// <summary>「换算证据包」的列头（<see cref="EvidencePackCommand"/>）：**16 列** = 上面那份 + 最前的「参考分辨率」与中段的「溢出/留白方向」。</summary>
        public const string PackHeader = "参考分辨率,屏幕档位,屏幕尺寸,match,模式," + KernelTriplet + ",溢出/留白方向," + TruthTail;

        /// <summary>
        /// 追加**共用行尾**（真值三列 · 差值三列 · 输入源）并换行。
        /// <para><paramref name="truth"/> **由调用方判定**（两个调用方的口径本就不同，且各自有注释说明）：
        /// 当前表导出时"只有 <c>ScaleWithScreenSize</c> 那一行才可能有真值"（它一次导三种模式）；
        /// 证据包则整份只有现场条件那一块带真值（守门在 <see cref="ScaleTable.Build"/>）。
        /// **但拼法只有这一处**——占位文本 `（离线列）` / `内核离线计算` 与 `G9` 精度不许多处重抄。</para>
        /// </summary>
        public static void AppendTruthTail(StringBuilder sb, ScaleTableRow row, bool truth, CultureInfo ci)
        {
            sb.Append(truth ? row.TruthScaleFactor.ToString("G9", ci) : "（离线列）").Append(',')
              .Append(truth ? row.TruthReferencePpu.ToString("G9", ci) : "（离线列）").Append(',')
              .Append(truth ? row.TruthCanvasSize.ToString() : "（离线列）").Append(',')
              .Append(truth ? row.DeltaScaleFactor.ToString("G9", ci) : "").Append(',')
              .Append(truth ? row.DeltaReferencePpu.ToString("G9", ci) : "").Append(',')
              .Append(truth ? row.DeltaCanvasWidth.ToString("G9", ci) + "x" + row.DeltaCanvasHeight.ToString("G9", ci) : "").Append(',')
              // 第 4 列：**输入源**（内核列是离线算的，真值行才是现场读数）
              .Append(truth ? row.TruthSource.ToString() : "内核离线计算")
              .AppendLine();
        }
    }
}
