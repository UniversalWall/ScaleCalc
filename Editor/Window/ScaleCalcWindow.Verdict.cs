using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// <see cref="ScaleCalcWindow"/> 的**结论与匹配方式章节**：
    /// 顶部**结论条**（常显，工具栏下方、档位折叠区之上）· **匹配方式三选一** ·
    /// **三方式对比小结** · **复制结论**。
    /// <para>🔴 **只呈现、不计算**：每一个数都来自 <see cref="ScaleFit"/> 的纯函数，本文件只做
    /// "把算好的字符串写进 Label"——包括那三行小结（<see cref="VerdictModesText"/> 是**静态纯函数**，可在 EditMode 断言）。</para>
    /// <para>🔴 **三个文本出口互不覆盖**：结论条说"选型结论是什么"；小结说"换一种匹配方式会怎样"；
    /// 操作反馈写 <c>tier-status</c>。</para>
    /// </summary>
    public sealed partial class ScaleCalcWindow
    {
        /// <summary>匹配方式选择器的选项顺序（<see cref="ScreenMatchMode"/> 的**展示名**）。</summary>
        private static readonly string[] MatchModeChoices =
        {
            "Match（按画布宽高加权）",
            "Expand（画布永不小于参考）",
            "Shrink（画布永不大于参考）",
        };

        /// <summary>「三选一」小结的**标题前缀**——要求**逐字**含"三选一"（防"以为三个能同时用"）。</summary>
        public const string VerdictModesTitlePrefix = "匹配方式对比（三选一 · 按你勾选的 ";

        private DropdownField m_MatchMode;
        private Label m_MatchModeNote;
        private Label m_VerdictBar;
        private Label m_VerdictBand;
        private Label m_VerdictBars;
        private Label m_VerdictModes;
        private Button m_VerdictCopy;

        /// <summary>最近一次 <c>Rebuild()</c> 算出的**当前方式**跨档统计（结论条/复制/约束单都读它，不各算一遍）。</summary>
        private ScaleFit.ModeStats m_Stats;

        /// <summary>控件就位 + 接线（在第一次 <c>Rebuild()</c> **之前**调）。</summary>
        private void InitVerdictSection()
        {
            m_MatchMode = rootVisualElement.Q<DropdownField>("match-mode");
            m_MatchModeNote = rootVisualElement.Q<Label>("match-mode-note");
            m_VerdictBar = rootVisualElement.Q<Label>("verdict-bar");
            m_VerdictBand = rootVisualElement.Q<Label>("verdict-band");
            m_VerdictBars = rootVisualElement.Q<Label>("verdict-bars");
            m_VerdictModes = rootVisualElement.Q<Label>("verdict-modes");
            m_VerdictCopy = rootVisualElement.Q<Button>("verdict-copy");

            if (m_MatchMode != null)
            {
                m_MatchMode.choices = new List<string>(MatchModeChoices);
                m_MatchMode.index = 0;                       // 默认 Match（与窗口此前的写死行为一致）
                m_MatchMode.RegisterValueChangedCallback(_ => OnMatchModeChanged());
            }
            if (m_VerdictCopy != null) m_VerdictCopy.clicked += CopyVerdict;
        }

        /// <summary>当前所选的匹配方式（**唯一取值口**：模板与 <see cref="ScaleTable.Build"/> 都读它）。</summary>
        private ScreenMatchMode CurrentScreenMatch()
            => m_MatchMode == null || m_MatchMode.index == (int)ScreenMatchMode.MatchWidthOrHeight
                ? ScreenMatchMode.MatchWidthOrHeight
                : (ScreenMatchMode)m_MatchMode.index;

        /// <summary>换匹配方式：整表重算（<c>Rebuild</c> 里已带方式）⇒ 表、结论条、小结一起跟着变。</summary>
        private void OnMatchModeChanged()
        {
            UpdateMatchModeUi();
            Rebuild();
        }

        /// <summary>
        /// <c>Expand</c>/<c>Shrink</c> 不读 <c>match</c>（内核 <c>switch</c> 根本不看它）⇒ 滑块**禁用**并给一句说明。
        /// <para>说明挨着工具栏放：禁用与原因**必须同屏**，否则用户只会看到"滑块点不动"。</para>
        /// </summary>
        private void UpdateMatchModeUi()
        {
            bool usesMatch = CurrentScreenMatch() == ScreenMatchMode.MatchWidthOrHeight;
            if (m_MatchSlider != null) m_MatchSlider.SetEnabled(usesMatch);
            if (m_MatchValue != null) m_MatchValue.SetEnabled(usesMatch);
            if (m_MatchModeNote != null)
                m_MatchModeNote.text = usesMatch
                    ? string.Empty
                    : "该匹配方式不使用 match：Expand = 画布永不小于参考、Shrink = 永不大于";
        }

        /// <summary>
        /// 刷结论条 + 三方式小结（在 <c>Rebuild()</c> 末尾调一次）。
        /// <para>🔴 顺序纪律：结论必须**在数据改完之后**算（"一键取消会裁档位"那条路径尤其：先报后算会报出旧结论）。</para>
        /// </summary>
        private void UpdateVerdictBar(in ScaleCalcInput template)
        {
            List<ScreenProfile> tiers = IncludedProfiles();
            ScaleSize reference = template.ReferenceResolution;
            float match = template.MatchWidthOrHeight;
            float dpi = template.ScreenDpi;

            m_Stats = ScaleFit.Stats(tiers, reference, template.ScreenMatch, match, dpi);

            if (m_VerdictBar != null)
            {
                m_VerdictBar.text = ScaleFit.Headline(m_Stats, reference, ScaleFit.BuiltInDraftCount(tiers));
                bool alert = m_Stats.CroppedCount > 0;
                m_VerdictBar.EnableInClassList("verdict-alert", alert);
                m_VerdictBar.EnableInClassList("verdict-ok", m_Stats.TierCount > 0 && !alert);
            }
            // 危险带：安全设计区相对参考**被裁掉的边缘宽度**（左右各 / 上下各，单侧）
            // 🔴 与结论条**同源**（同一个 `m_Stats`）⇒ 两处的数不会打架；无结论时如实写 `—`
            if (m_VerdictBand != null) m_VerdictBand.text = ScaleFit.BandLine(m_Stats, reference);
            // 字符条：只画最需要注意的 3 档（高度预算的取舍写在 `VerdictBarsText` 的注释里）
            if (m_VerdictBars != null)
                m_VerdictBars.text = VerdictBarsText(tiers, reference, template.ScreenMatch, match, dpi);
            if (m_VerdictModes != null) m_VerdictModes.text = VerdictModesText(tiers, reference, match, dpi);
            // 安全区示意图：与结论条/危险带**同源**（同一个 m_Stats）
            UpdateDiagram(reference);
        }

        /// <summary>
        /// 「匹配方式对比（三选一）」那一段文本：标题 + 3 行 +（仅 <c>Match</c> 时）推荐值一行。
        /// <para>**静态纯函数** ⇒ 可在 EditMode 逐字断言（"三选一"标题、档位口径、推荐值带代价）。</para>
        /// </summary>
        public static string VerdictModesText(IReadOnlyList<ScreenProfile> tiers, ScaleSize reference, float match, float dpi)
        {
            ScaleFit.ModeStats[] modes = ScaleFit.AcrossModes(tiers, reference, match, dpi);
            string title = VerdictModesTitlePrefix + modes[0].TierCount + " 档）";
            string text = title;
            foreach (ScaleFit.ModeStats stats in modes) text += "\n" + ScaleFit.ModeLine(stats, match);

            // 🔴 目标函数**只有一个取值口**：同一个变量同时喂"扫描"与"标注"⇒ 两者不可能不一致
            ScaleFit.Objective objective = ScaleFit.Objective.MinWorstCrop;
            if (ScaleFit.TryRecommendMatch(tiers, reference, dpi, objective,
                                           out float recommended, out ScaleFit.ModeStats atRecommended))
            {
                text += "\n" + ScaleFit.RecommendationText(atRecommended, recommended, objective);
            }
            return text;
        }

        /// <summary>「复制结论」：把结论条那句话放进系统剪贴板——贴进需求单/群里用。</summary>
        private void CopyVerdict()
        {
            if (m_VerdictBar == null || string.IsNullOrEmpty(m_VerdictBar.text)) { Report("还没有结论可复制，先等一次重算"); return; }
            EditorGUIUtility.systemCopyBuffer = m_VerdictBar.text;
            Report("已复制结论：" + m_VerdictBar.text);
        }
    }
}
