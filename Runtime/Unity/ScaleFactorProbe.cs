using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace Wayward.ScaleCalc.Unity
{
    /// <summary>
    /// 常驻探针：逐帧读引擎真值、跑内核、比差、给判定。
    /// <para>**采样循环本身不做自动化验收**（不要拿 Play 当验收手段）。可被机械验证的部分
    /// （判定优先级、门限、降级分支）都在 EditMode 断言里；采样循环的正确性由现场对账的读数与判定体现。</para>
    /// <para>⚠️ 开销口径：<c>Capture()</c> **零分配**（只读结构体 + 静态只读字符串常量，不拼字符串）；
    /// 文本输出由「变化驱动」，稳定帧不打印，连续稳定后按 <see cref="StableSummaryInterval"/> 限频打一行摘要。</para>
    /// <para>本文件只放：字段 / 生命周期 / 输出策略 / 稳定判据；**采集与判定在 `ScaleFactorProbe.Capture.cs`**（拆文件的判据是 200 行红线）。</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class ScaleFactorProbe : MonoBehaviour
    {
        /// <summary>引擎私有字段名（可覆盖：跨版本改名时应急，也是"降级分支可人为触发"的入口）。</summary>
        [SerializeField] private string prevScaleFactorField = DefaultPrevScaleFactorField;

        /// <inheritdoc cref="prevScaleFactorField"/>
        [SerializeField] private string prevReferencePpuField = DefaultPrevReferencePpuField;

        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private CanvasScaler targetScaler;

        /// <summary>对账容差。⚠️ 与写回门限 <c>5e-6</c> **不是一回事**（差两个数量级）。</summary>
        [SerializeField] private float tolerance = 1e-3f;

        /// <summary>调试用：**每帧输出会每帧分配字符串**（默认关）。</summary>
        [SerializeField] private bool logEveryFrame;

        private const string DefaultPrevScaleFactorField = "m_PrevScaleFactor";
        private const string DefaultPrevReferencePpuField = "m_PrevReferencePixelsPerUnit";
        private const int StableSummaryInterval = 300;

        // 静态只读常量：Capture 路径上**不许**出现运行时字符串拼接
        private static readonly string NoDegradation = string.Empty;
        private static readonly string DegradedScaleOnly = DefaultPrevScaleFactorField;
        private static readonly string DegradedReferenceOnly = DefaultPrevReferencePpuField;
        private static readonly string DegradedBoth = DefaultPrevScaleFactorField + " + " + DefaultPrevReferencePpuField;

        private FieldInfo m_ScaleField;
        private FieldInfo m_ReferenceField;

        private bool m_HasEmitted;
        private ProbeVerdict m_LastVerdict;
        private bool m_LastReflectionAvailable;
        private float m_LastTruthScaleFactor;
        private float m_LastTruthReferencePpu;
        private int m_StableFrames;
        private int m_LastSummaryFrame = -1;

        /// <summary>绑定目标（测试与窗口用；不依赖 Inspector 拖拽）。</summary>
        public void Bind(Canvas canvas, CanvasScaler scaler)
        {
            targetCanvas = canvas;
            targetScaler = scaler;
            m_ScaleField = null;
            m_ReferenceField = null;
            m_HasEmitted = false;
        }

        /// <summary>覆盖要反射的私有字段名（**只给降级分支与跨版本应急用**）。</summary>
        public void OverridePrevFields(string scaleFactorField, string referencePpuField)
        {
            prevScaleFactorField = scaleFactorField;
            prevReferencePpuField = referencePpuField;
            m_ScaleField = null;
            m_ReferenceField = null;
        }

        private void Awake() => Sample(SampleTiming.Awake);

        private void Start() => Sample(SampleTiming.Start);

        private void OnEnable()
        {
            if (Application.isPlaying) StartCoroutine(EndOfFrameLoop());
        }

        private IEnumerator EndOfFrameLoop()
        {
            var endOfFrame = new WaitForEndOfFrame();
            while (true)
            {
                yield return endOfFrame;
                Sample(SampleTiming.EndOfFrame);
            }
        }

        /// <summary>采样一次并按"变化驱动"决定是否输出。</summary>
        public void Sample(SampleTiming timing)
        {
            ProbeReport report = Capture(timing);
            Emit(report);
        }

        /// <summary>变化驱动输出：判定/降级/读数变化或手动请求时打印；稳定帧限频打摘要。</summary>
        public void Emit(in ProbeReport report)
        {
            bool changed = !m_HasEmitted
                || report.Verdict != m_LastVerdict
                || report.ReflectionAvailable != m_LastReflectionAvailable
                || report.TruthScaleFactor != m_LastTruthScaleFactor
                || report.TruthReferencePpu != m_LastTruthReferencePpu;

            if (changed || logEveryFrame || report.Timing == SampleTiming.Manual)
            {
                Debug.Log(ProbeText.Format(in report));
                m_StableFrames = 0;
                m_LastSummaryFrame = report.FrameCount;
            }
            else
            {
                m_StableFrames++;
                if (m_StableFrames >= StableSummaryInterval && report.FrameCount - m_LastSummaryFrame >= StableSummaryInterval)
                {
                    Debug.Log(ProbeText.FormatSummary(in report));
                    m_LastSummaryFrame = report.FrameCount;
                    m_StableFrames = 0;
                }
            }

            m_HasEmitted = true;
            m_LastVerdict = report.Verdict;
            m_LastReflectionAvailable = report.ReflectionAvailable;
            m_LastTruthScaleFactor = report.TruthScaleFactor;
            m_LastTruthReferencePpu = report.TruthReferencePpu;
        }

        /// <summary>
        /// 稳定帧判据：三条件同时成立才算"稳定帧读数"——
        /// ① 两个真值读数连续两帧不变；② 两个 <c>m_Prev*</c> 已等于目标值（读不到 ⇒ 降级为只看 ① + 记降级）；
        /// ③ 组件 <c>enabled</c>（<c>OnDisable</c> 会把两字段复位成 <c>1</c>/<c>100</c>，"读数不变"会**假性满足**）。
        /// </summary>
        public bool IsStable(in ProbeReport previous, in ProbeReport current)
            => current.ComponentEnabled
            && previous.TruthScaleFactor == current.TruthScaleFactor
            && previous.TruthReferencePpu == current.TruthReferencePpu
            && (!current.ReflectionAvailable
                || (current.PrevScaleFactor == current.Kernel.ScaleFactor
                    && current.PrevReferencePpu == current.Kernel.ReferencePixelsPerUnit));
    }
}
