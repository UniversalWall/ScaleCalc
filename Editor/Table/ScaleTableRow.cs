using System.Collections.Generic;
using System.Diagnostics;
using Wayward.ScaleCalc.Unity;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// 选型台的一行 = **一个屏幕档位**（行 = 屏幕档位，列 = 三模式 + 溢出方向 + 差值；
    /// 行标识在界面上拆成「档位名 + 屏幕尺寸」两列，列定义见 <see cref="ScaleTableColumns"/>）。
    /// </summary>
    public sealed class ScaleTableRow
    {
        /// <summary>该行的屏幕档位（也是证据包的行标识）。</summary>
        public ScreenProfile Profile;

        /// <summary>该行的参考分辨率（列方向共用，但行上留一份便于呈现）。</summary>
        public ScaleSize Reference;

        /// <summary>三模式的内核列（离线可算，编辑期非 Play 也能出）。</summary>
        public ScaleCalcResult PixelSize;
        public ScaleCalcResult ScreenSize;
        public ScaleCalcResult PhysicalSize;

        /// <summary>真值列**只对"当前渲染尺寸那一行"有值**。</summary>
        public bool IsCurrentRenderSize;
        public bool HasTruth;
        public float TruthScaleFactor;
        public float TruthReferencePpu;
        public ScaleSize TruthCanvasSize;
        public ScreenSizeSource TruthSource;
        public float DeltaScaleFactor;
        public float DeltaReferencePpu;
        public float DeltaCanvasWidth;
        public float DeltaCanvasHeight;

        /// <summary>溢出/留白方向（几何口径）——对 <c>ScaleWithScreenSize</c> 那一列给。</summary>
        public string Direction => AxisVerdict.Describe(in ScreenSize, Reference);

        /// <summary>
        /// 本行的**整表真值语境**：由 <see cref="ScaleTable.Build"/>
        /// **统一赋值**，窗口与装配层**只读**。
        /// <para>🔴 为什么挂在行上：状态列/导出都只能在**一行**的上下文里工作，而"整表为什么没有真值"
        /// 是整表级的事实（原来只活在 <see cref="ScaleTable.TruthNote"/> 里，而底部那段文本已撤除）。</para>
        /// <para>⚠️ **默认构造 = "本次会话什么都不知道"**（三个 bool 全 false）⇒ 漏赋值的行会如实说
        /// "还没点过现场对账"，不编造成功的假象（有一条断言专门钉这一点）。</para>
        /// </summary>
        public ReconcileContext Reconcile;

        /// <summary>
        /// 该档在**当前参与清单**里有没有"方向对"（同像素数、宽高互换的另一个档位）——呈现落点是
        /// 「比例」列后缀 <c>⇄</c>。
        /// <para>判定范围**必须是当前清单**：它要防的是"把同一台设备的两个方向当成两台设备统计"。
        /// 由 <see cref="ScaleTable.Build"/> 在装配时算好（那是唯一拿得到整份清单的地方）。</para>
        /// </summary>
        public bool IsOrientationPair;

        /// <summary>差值的显示文本（没有真值时如实标"离线列"）。**精度必须 ≥ 判定容差**（否则会显示成 0.00 却报对账失败）。</summary>
        public string DeltaText => HasTruth
            ? "Δsf=" + AxisVerdict.FormatDelta(DeltaScaleFactor)
              + " ΔrefPPU=" + AxisVerdict.FormatDelta(DeltaReferencePpu)
              + " Δcanvas=" + AxisVerdict.FormatDelta(DeltaCanvasWidth) + "x" + AxisVerdict.FormatDelta(DeltaCanvasHeight)
            : "未经引擎对账（离线列）";
    }
}
