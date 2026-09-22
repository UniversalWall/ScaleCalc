using System;
using UnityEngine.UIElements;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// <see cref="ScaleCalcWindow"/> 的**参数与模板章节**：
    /// 从控件读"当前条件"（参考分辨率 / <c>match</c> / 现场 dpi / **匹配方式**）并组成内核输入模板，
    /// 以及控件值变化时的入口（**只重算离线表 + 排一次防抖，绝不对账**）。
    /// <para>拆出去的东西是"**条件从哪来**"；主体文件留在"控件怎么绑、表怎么重算"。</para>
    /// </summary>
    public sealed partial class ScaleCalcWindow
    {
        private void OnMatchChanged(ChangeEvent<float> evt)
        {
            if (m_MatchValue != null) m_MatchValue.text = evt.newValue.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            OnParameterChanged();
        }

        /// <summary>
        /// 参数变了：① 立刻重算离线表（廉价，用户要的即时反馈）；② 若"自动对账"开着，把防抖重排一次。
        /// <para>⚠️ 这里**不**出现任何测量/对账调用（源码级判据就是看这条路径）——要动场景必须由
        /// <see cref="ReconcileNow"/> 走，而它只挂在按钮与防抖回调上。</para>
        /// </summary>
        private void OnParameterChanged()
        {
            Rebuild();
            ScheduleAutoReconcile();
        }

        /// <summary>当前控件条件（参考分辨率 / match / 现场 dpi / **匹配方式**）——重算与对账**共用同一个模板**，免得两边条件不一致。</summary>
        private ScaleCalcInput CurrentTemplate()
        {
            var reference = new ScaleSize(m_RefWidth != null ? m_RefWidth.value : 1920f,
                                         m_RefHeight != null ? m_RefHeight.value : 1080f);
            float match = m_MatchSlider != null ? m_MatchSlider.value : 0.5f;

            ScaleCalcInput template = ScaleCalcInput.Default;
            template.Mode = ScaleMode.ScaleWithScreenSize;
            template.ReferenceResolution = reference;
            template.ScreenMatch = CurrentScreenMatch();   // 来自工具栏的"匹配方式"三选一（不再写死）
            template.MatchWidthOrHeight = match;
            template.ScreenDpi = UnityEngine.Screen.dpi;   // 现场读数（编辑器不可信，但如实使用并标注出来）
            return template;
        }
    }
}
