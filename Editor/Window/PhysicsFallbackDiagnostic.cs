using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// 兜底链诊断（旧《物理覆盖表》的 40 行矩阵已作废）。
    /// <para>它只回答一件事的两面：**本机读数是多少**（若读得到）与**一旦不报会退化成多少**。两条的来源**必须能区分**：
    /// <c>本机实测</c> / <c>兜底常量</c>——不许拿兜底常量冒充本机读数。</para>
    /// <para>🔴 **本机读数缺位（<paramref name="localDpi"/> &lt;= 0）时本机那条根本不产出**，只出兜底那条：
    /// 缺位时两条的输入**都是 <c>0</c>**、<c>实际dpi</c> 也**都是兜底常量** ⇒ 摆两条就是**语义重复**，读者分不出哪条在说什么。
    /// **少一条比多一条假条诚实。**</para>
    /// <para>🔴 **两条给的是两个"情形"，不是两个"结论"**：本机那条的 <c>走兜底=否</c> 只说明"**假如**引擎读到这个数"，
    /// 与这台真机将来报不报 dpi **无关**。措辞里**不许**出现"这一台不会走兜底"这类推断。</para>
    /// <para>与探针分工：探针 <see cref="Wayward.ScaleCalc.Unity.ProbeText"/> 报的是"**眼下这一帧**引擎真值走了没有"
    /// （要挂组件、要读真值）；本诊断报的是"把 dpi **假设成 0** 会退化成什么"（纯内核推演，不要真值）。
    /// **两边同用"走兜底"这个词，但各说一件事。**</para>
    /// </summary>
    public static class PhysicsFallbackDiagnostic
    {
        /// <summary>一条诊断的 dpi **来源**（两种，且必须能区分）。</summary>
        public enum DpiOrigin
        {
            /// <summary>本机现场读数（编辑器/桌面上不可信，只作一次读数记录）。</summary>
            MeasuredLocal,

            /// <summary>兜底常量（内核按 <c>fallbackScreenDPI</c> 推演出来的情形）。</summary>
            FallbackConstant,
        }

        /// <summary>诊断的一条。字段全部来自**内核产出**，诊断不自己复算。</summary>
        public readonly struct FallbackRow
        {
            public readonly DpiOrigin Origin;

            /// <summary>喂给内核的那个数：本机条 = 现场读数；兜底条 = <c>0</c>（注入值，原样保留便于阅读）。</summary>
            public readonly float InputDpi;

            /// <summary>兜底之后**实际参与算式**的 dpi（内核 <c>EffectiveDpi</c>）。</summary>
            public readonly float EffectiveDpi;

            /// <summary>**兜底常量本身**（走出的是哪个常量要说清——不许只写"走了兜底"四个字）。</summary>
            public readonly float FallbackScreenDpi;

            /// <summary>内核 <c>UsedFallbackDpi</c>，**不由诊断自己判** <c>dpi == 0</c>（同一条纪律：判定只在内核做）。</summary>
            public readonly bool UsedFallback;

            public readonly float ScaleFactor;
            public readonly float ReferencePixelsPerUnit;

            public FallbackRow(DpiOrigin origin, float inputDpi, float effectiveDpi, float fallbackScreenDpi,
                               bool usedFallback, float scaleFactor, float referencePixelsPerUnit)
            {
                Origin = origin;
                InputDpi = inputDpi;
                EffectiveDpi = effectiveDpi;
                FallbackScreenDpi = fallbackScreenDpi;
                UsedFallback = usedFallback;
                ScaleFactor = scaleFactor;
                ReferencePixelsPerUnit = referencePixelsPerUnit;
            }
        }

        /// <summary>界面与 CSV 共用的一列（标题 + 取值函数）——**单一真相源**：表头就是标题拼的，格式只有一处。</summary>
        public sealed class Spec
        {
            public readonly string Title;
            private readonly System.Func<FallbackRow, string> m_Value;

            public Spec(string title, System.Func<FallbackRow, string> value)
            {
                Title = title;
                m_Value = value;
            }

            public string ValueOf(FallbackRow row) => m_Value(row);
        }

        /// <summary>六列（从旧覆盖表的 7 列降到这里；**列数是实测的，不写死**）。</summary>
        public static readonly Spec[] All =
        {
            new Spec("dpi来源", row => OriginText(row.Origin)),
            new Spec("输入dpi", row => G(row.InputDpi)),
            new Spec("实际dpi", row => G(row.EffectiveDpi)),
            new Spec("兜底常量", row => G(row.FallbackScreenDpi)),
            new Spec("走兜底", row => row.UsedFallback ? "是" : "否"),
            new Spec("scaleFactor", row => G(row.ScaleFactor)),
        };

        /// <summary>列数（界面与断言都读它，不写死 6）。</summary>
        public static int ColumnCount => All.Length;

        /// <summary>CSV 表头 = 列标题拼起来 ⇒ 与列定义**想不一致都做不到**。</summary>
        public static string CsvHeader
        {
            get
            {
                var sb = new StringBuilder();
                for (int i = 0; i < All.Length; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(All[i].Title);
                }
                return sb.ToString();
            }
        }

        /// <summary>造诊断（**1~2 条**）：本机条（仅当 <paramref name="localDpi"/> &gt; 0）+ 兜底条（恒在）。
        /// <para>固定假设单位 <c>Points</c>（<c>targetDPI = 72</c>）：诊断问的是"退化多少"，单位选哪个只是给退化幅度一个统一标尺。</para>
        /// </summary>
        public static List<FallbackRow> Build(float localDpi, float fallbackScreenDpi,
                                             float defaultSpriteDpi, float referencePixelsPerUnit)
        {
            var rows = new List<FallbackRow>(2);

            if (localDpi > 0f)
                rows.Add(Evaluate(DpiOrigin.MeasuredLocal, localDpi, fallbackScreenDpi, defaultSpriteDpi, referencePixelsPerUnit));

            rows.Add(Evaluate(DpiOrigin.FallbackConstant, 0f, fallbackScreenDpi, defaultSpriteDpi, referencePixelsPerUnit));
            return rows;
        }

        /// <summary>
        /// 诊断的一句话形态（**纯函数**：不开窗即可断言）。
        /// <para>🔴 用词与探针**同词**（"走兜底"）；且**只陈述读数、不给结论**——见类头注那条边界。</para>
        /// </summary>
        public static string FallbackInfoText(float localDpi, float fallbackScreenDpi,
                                              float defaultSpriteDpi, float referencePixelsPerUnit)
        {
            List<FallbackRow> rows = Build(localDpi, fallbackScreenDpi, defaultSpriteDpi, referencePixelsPerUnit);
            if (rows.Count == 0) return "兜底链：诊断未产出（不该发生的空结果）";

            var sb = new StringBuilder();
            sb.Append("兜底链：本机读数 ");
            sb.Append(localDpi > 0f ? G(localDpi) : "未取得（Screen.dpi 读数为 0 或不可用）");
            sb.Append("（编辑器读数不可信）；");

            foreach (FallbackRow row in rows)
            {
                if (row.Origin != DpiOrigin.FallbackConstant) continue;
                sb.Append("若报 0 则走兜底 ");
                sb.Append(G(row.FallbackScreenDpi));
                sb.Append(" ⇒ scaleFactor ");
                sb.Append(G(row.ScaleFactor));
                sb.Append("（这才是**这一台真退化时**会看到的数）");
            }
            return sb.ToString();
        }

        public static string OriginText(DpiOrigin origin)
            => origin == DpiOrigin.MeasuredLocal ? "本机实测" : "兜底常量";

        /// <summary>CSV（列定义驱动：表头与逐格都走 <see cref="Spec"/> ⇒ 格式只有一处）。</summary>
        public static string ToCsv(List<FallbackRow> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine(CsvHeader);
            if (rows == null) return sb.ToString();
            foreach (FallbackRow row in rows)
            {
                for (int i = 0; i < All.Length; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(All[i].ValueOf(row));
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private static FallbackRow Evaluate(DpiOrigin origin, float screenDpi, float fallbackScreenDpi,
                                            float defaultSpriteDpi, float referencePixelsPerUnit)
        {
            ScaleCalcInput input = ScaleCalcInput.Default;
            input.Mode = ScaleMode.ConstantPhysicalSize;
            input.PhysicalUnit = PhysicalUnit.Points;
            // 这一支**不读屏幕尺寸**：给参考分辨率只是为了让输入结构完整，给什么都不影响结果
            input.ReferenceResolution = new ScaleSize(800f, 600f);
            input.ScreenSize = input.ReferenceResolution;
            input.ScreenDpi = screenDpi;
            input.FallbackScreenDPI = fallbackScreenDpi;
            input.DefaultSpriteDPI = defaultSpriteDpi;
            input.ReferencePixelsPerUnit = referencePixelsPerUnit;

            ScaleCalcResult result = ScaleCalc.Evaluate(in input);
            return new FallbackRow(origin, screenDpi, result.EffectiveDpi, fallbackScreenDpi,
                                   result.UsedFallbackDpi, result.ScaleFactor, result.ReferencePixelsPerUnit);
        }

        private static string G(float value) => value.ToString("G9", CultureInfo.InvariantCulture);
    }
}
