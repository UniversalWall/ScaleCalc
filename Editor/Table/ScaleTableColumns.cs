using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// 选型台表格的**列定义与默认宽度**（行标识拆两列 + 列宽按内容推）。
    /// <para>**单一真相源**：装配（<see cref="ScaleCalcTableBinder"/>）与断言（Tests）都读这一份——
    /// 不许再出现"表头在这里、取值在那里、宽度又是一个魔数"的三份口径。</para>
    /// <para>🔴 **列定义单住在** `ScaleTableColumns.Verdict.cs`（本文件当时 198/200 贴线）——
    /// 本文件只剩**宽度算法**；<see cref="All"/> 仍然只有一份（`partial` 的同一个类）。</para>
    /// <para>宽度算法是**纯函数 + 注入测量器**：生产用 <see cref="MeasureWithTextElement"/>，测试注入假测量器
    /// ⇒ 可在 EditMode 里断言「每列默认宽度 ≥ 该列最长文本的测量宽度」。</para>
    /// </summary>
    public static partial class ScaleTableColumns
    {
        /// <summary>单元格左右余量（MultiColumnListView 的 cell 自带内边距，这里再留呼吸位）。</summary>
        public const float CellPadding = 18f;

        /// <summary>文本测量器：给字符串，返回绘制宽度（px）。</summary>
        public delegate float Measure(string text);

        /// <summary>常驻度量探针（<c>MeasureTextSize</c> 是**实例**方法；这个元素不挂进任何 panel）。</summary>
        private static readonly Label MeasureProbe = new Label();

        /// <summary>
        /// **单元格形态**：照抄 `Editor/Tiers/ScreenTierColumns.cs` 的"枚举 + <c>Spec.Kind</c>"先例。
        /// <para>🔴 **为什么不叫 <c>CellKind</c>**：包里**已经有**一个 <c>ScreenTierColumns.CellKind</c>
        /// （档位管理表那一套，28 处引用）——两者在**同一个命名空间**且名字一样，读代码时
        /// "到底是哪张表的 kind"要靠上下文猜 ⇒ 按"一名一物"改叫 **<c>ScaleCellKind</c>**（选型表那一套）。</para>
        /// </summary>
        public enum ScaleCellKind
        {
            /// <summary>普通格：一个 <c>Label</c>（默认）。</summary>
            Label,

            /// <summary>带**比例底衬**的格（`横向`/`纵向` 两列）。</summary>
            Bar,

            /// <summary>**逐档裁留示意图**格：参考框 + 本档画布框（+ 两轴文字）。</summary>
            Diagram,
        }

        /// <summary>一列的定义：表头 · 取值 · 宽度区间（+ 可选的"本列是哪个内核模式"声明）。</summary>
        public sealed class Spec
        {
            public readonly string Title;
            public readonly float MinWidth;
            public readonly float MaxWidth;

            /// <summary>
            /// 本列呈现的是**哪个内核模式**的读数（其余列为 <c>null</c>）。
            /// <para>🔴 **为什么不靠表头字符串认**：本列的表头是 <c>sf（1 画布单位 = ? px）</c>，
            /// **不以模式成员名开头** ⇒ 无论 <c>Enum.TryParse</c> 还是"前缀匹配"都会数成 **0 模式**（状态栏回归）。
            /// 把它做成**声明**（而不是猜字符串）⇒ 列定义单仍是唯一真相源，且改表头不会再悄悄弄坏状态栏。</para>
            /// </summary>
            public readonly ScaleMode? KernelMode;

            /// <summary>本列的**单元格形态**：装配层按它分派，**不许猜表头字符串**。</summary>
            public readonly ScaleCellKind Kind;

            /// <summary>
            /// 本列要不要在单元格里画**一条比例底衬**：<c>横向</c>/<c>纵向</c> 两列为 <c>true</c>。
            /// <para>🔴 **派生属性，不许删**：它是既有断言的落点（<c>ScaleFitBarTests</c> 直接读 <c>spec.BarCell</c>），
            /// 删了会编译不过。新代码请用 <see cref="Kind"/>。</para>
            /// </summary>
            public bool BarCell => Kind == ScaleCellKind.Bar;

            /// <summary>
            /// 本列宽度**不随内容变**、固定用 <see cref="MaxWidth"/>（图列）。
            /// <para>为什么需要它：图的"宽"来自**固定作画区**，而 <see cref="ComputeWidths"/>
            /// 是按**文本长度**推宽的 ⇒ 照原路走会与声明的 150~240 打架（要么白占宽、要么截断）。</para>
            /// </summary>
            public bool FixedWidth => Kind == ScaleCellKind.Diagram;

            private readonly Func<ScaleTableRow, string> m_Value;

            public Spec(string title, float minWidth, float maxWidth, Func<ScaleTableRow, string> value,
                        ScaleMode? kernelMode = null, bool barCell = false, ScaleCellKind cellKind = ScaleCellKind.Label)
            {
                Title = title;
                MinWidth = minWidth;
                MaxWidth = maxWidth;
                m_Value = value;
                KernelMode = kernelMode;
                // 旧形参优先：`barCell: true` 的两个既有构造点因此**零改动**（写 `cellKind` 是新用法）
                Kind = barCell ? ScaleCellKind.Bar : cellKind;
            }

            /// <summary>该行在这一列上显示什么（装配与宽度推导**共用**这一个函数）。</summary>
            public string ValueOf(ScaleTableRow row) => m_Value(row);
        }

        // 批 C 拆（判据 = 200 行红线 + 职责不同）：**"把内容宽摊到可用宽"的算法搬到 `ScaleTableColumns.Width.cs`**
        // （`Distribute` 两个重载 + 夹取助手）。本文件只管"每列内容要多少宽"（度量 + 声明的 [min, max] 夹取）。

        /// <summary>
        /// 逐列算默认宽度：`该列最长文本（含表头）+ CellPadding`，再夹到该列声明的区间。
        /// <para>⚠️ 夹取只在**真超界**时生效：`MaxWidth` 是"内容太长的列也就这么宽（靠横向滚动）"的显式上限，
        /// 不是默认值；`MinWidth` 是"测不出来也不许塌成一列 0 宽"的保底。</para>
        /// </summary>
        public static float[] ComputeWidths(IReadOnlyList<ScaleTableRow> rows, Measure measure)
            => ComputeWidths(rows, measure, null);

        /// <summary>
        /// 同上，但**列集可传**（"更多列"模式下不能再按 <see cref="All"/> 定长返回——那会当场越界）。
        /// <paramref name="specs"/> 为 <c>null</c> ⇒ 默认列集（既有调用与断言零改动）。
        /// </summary>
        public static float[] ComputeWidths(IReadOnlyList<ScaleTableRow> rows, Measure measure, Spec[] specs)
        {
            if (measure == null) throw new ArgumentNullException(nameof(measure));
            Spec[] set = specs ?? All;

            var widths = new float[set.Length];
            for (int i = 0; i < set.Length; i++)
            {
                Spec spec = set[i];

                // 🔴 声明式固定宽：图列的"宽"来自**固定作画区**，不是文本长度
                //    ⇒ 直接取声明上限，**不参与**"按内容推 + 分摊"（否则列宽会变成"两轴文字有多长"）。
                if (spec.FixedWidth) { widths[i] = spec.MaxWidth; continue; }

                float longest = SafeMeasure(measure, spec.Title);
                if (rows != null)
                {
                    for (int r = 0; r < rows.Count; r++)
                    {
                        string text = spec.ValueOf(rows[r]);
                        float w = SafeMeasure(measure, text);
                        if (w > longest) longest = w;
                    }
                }

                float wanted = longest + CellPadding;
                // NaN 会穿过 Math.Min/Max 的比较（两侧都 false），所以先兜一次
                if (float.IsNaN(wanted) || float.IsInfinity(wanted)) wanted = spec.MinWidth;
                widths[i] = Math.Min(spec.MaxWidth, Math.Max(spec.MinWidth, wanted));
            }
            return widths;
        }

        /// <summary>
        /// 生产用测量器：官方 <c>TextElement.MeasureTextSize</c>。
        /// <para>⚠️ 两点实测约束：① <c>MeasureMode</c> 是 **<c>VisualElement</c> 的嵌套枚举**（写裸名 CS0103）；
        /// ② 它是**实例方法**（写成静态调用 CS0120）⇒ 用下面这个常驻的度量探针元素。</para>
        /// </summary>
        public static float MeasureWithTextElement(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0f;

            Vector2 size = MeasureProbe.MeasureTextSize(text, 0f, VisualElement.MeasureMode.Undefined,
                                                        0f, VisualElement.MeasureMode.Undefined);
            float width = size.x;
            // 量不出来（未挂 panel 导致字体度量不可用 / API 行为变化）⇒ 退到字符估算，
            // **绝不返回 0**：否则列会被夹到 MinWidth，而"为什么这么宽"就看不出来了
            return width > 0f && !float.IsNaN(width) && !float.IsInfinity(width) ? width : Estimate(text);
        }

        /// <summary>保底估算：CJK/全角按 12px、其余按 7px（只为"不许塌成 0"，精度不参与任何断言）。</summary>
        public static float Estimate(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0f;

            float width = 0f;
            foreach (char c in text) width += c > 0x2E7F ? 12f : 7f;
            return width;
        }

        private static float SafeMeasure(Measure measure, string text)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            float w = measure(text);
            return float.IsNaN(w) || float.IsInfinity(w) || w < 0f ? 0f : w;
        }
    }
}
