using UnityEngine.UIElements;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// <see cref="ScaleCalcWindow"/> 的**排序 / 筛选 / 工作集联动章节**。
    /// <para>三件事：① **排序**（点列头，<c>MultiColumnListView</c> 原生排序事件；**默认不排序** ⇒ 保持清单顺序 + 现场行在首）；
    /// ② **筛选**（"只看会裁" / "只看竖屏"，默认全关）；③ **一键取消会裁档位**（把"我不可能为它单独排一版"的决策一键落地）。</para>
    /// <para>🔴 **排序/筛选只改"看的顺序"**：口径全在 <see cref="ScaleFit.ViewRows"/>（纯函数），**模型不动**
    /// ⇒ 结论条、安全设计区、证据包、导出全都**不随筛选变**（否则就成了"表里 3 行、结论说参与 7 档"的自欺）。
    /// 🔴 **只有"一键取消会裁档位"改数据**，而它走的是既有的工作集落盘路径（零场景操作）。</para>
    /// </summary>
    public sealed partial class ScaleCalcWindow
    {
        private Toggle m_OnlyCropped;
        private Toggle m_OnlyPortrait;
        private Button m_ExcludeCropped;

        /// <summary>当前排序（窗口**持有**状态：每次 <c>Rebuild()</c> 都会重建一张新表，状态不能放在表里）。</summary>
        private ScaleFit.SortKey m_SortKey = ScaleFit.SortKey.None;
        private bool m_SortAscending = true;

        /// <summary>控件就位 + 接线（在第一次 <c>Rebuild()</c> **之前**调）。</summary>
        private void InitFilterSection()
        {
            m_OnlyCropped = rootVisualElement.Q<Toggle>("only-cropped");
            m_OnlyPortrait = rootVisualElement.Q<Toggle>("only-portrait");
            m_ExcludeCropped = rootVisualElement.Q<Button>("exclude-cropped-button");

            // 筛选只改呈现 ⇒ 重算一次即可（不落盘、不动工作集）
            if (m_OnlyCropped != null) m_OnlyCropped.RegisterValueChangedCallback(_ => Rebuild());
            if (m_OnlyPortrait != null) m_OnlyPortrait.RegisterValueChangedCallback(_ => Rebuild());
            if (m_ExcludeCropped != null) m_ExcludeCropped.clicked += ExcludeCroppedTiers;
        }

        private bool OnlyCropped => m_OnlyCropped != null && m_OnlyCropped.value;

        private bool OnlyPortrait => m_OnlyPortrait != null && m_OnlyPortrait.value;

        /// <summary>表头点了排序：**记住**方向与键，再重算（表是每次新建的，状态必须留在窗口）。</summary>
        private void OnSortChanged(ScaleFit.SortKey key, bool ascending)
        {
            m_SortKey = key;
            m_SortAscending = ascending;
            Rebuild();
        }

        /// <summary>
        /// **一键取消会裁档位**。
        /// <para>🔴 **顺序纪律**：① 改数据 → ② <c>Rebuild()</c>（结论随之更新）→ ③ **才**报文案。
        /// 先报后算会报出**旧结论**。零场景操作、只动内存 + <c>SaveTiers</c>。</para>
        /// <para>全被排除时**落到 0 档口径**（结论条如实说"当前 0 档参与计算 ⇒ 无结论"，**不报错、不自动勾回**）。</para>
        /// </summary>
        private void ExcludeCroppedTiers()
        {
            ScaleCalcInput template = CurrentTemplate();
            System.Collections.Generic.List<ScreenProfile> profiles = IncludedProfiles();
            System.Collections.Generic.List<int> cropped = ScaleFit.CroppedIndices(
                profiles, template.ReferenceResolution, template.ScreenMatch,
                template.MatchWidthOrHeight, template.ScreenDpi);

            if (cropped.Count == 0) { Report("当前勾选的档位里没有会裁的——不必排除"); return; }

            System.Collections.Generic.IReadOnlyList<ScreenTier> tiers = m_TierSet.IncludedTiers();
            int excluded = 0;
            foreach (int index in cropped)
                if (index >= 0 && index < tiers.Count && m_TierSet.SetIncluded(tiers[index].Id, false)) excluded++;

            SaveTiers();
            Rebuild();                                   // ② 先让结论跟着新工作集更新
            Report("已排除 " + excluded + " 档会裁档位（仍有 " + m_TierSet.IncludedCount + " 档参与计算）");
        }
    }
}
