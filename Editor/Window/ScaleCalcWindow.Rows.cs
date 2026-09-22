using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// <see cref="ScaleCalcWindow"/> 的**档位管理章节**（主体在 `ScaleCalcWindow.cs`、
    /// 对账在 `…Measure.cs`、导出在 `…Export.cs`）。
    /// <para>本文件是**工作集的唯一所有者**：载入/回落、三件操作（勾选 / 删除 / 新增）、落盘。
    /// 数据归属在 <see cref="ScreenTierSet"/>，视图装配在 <see cref="ScaleCalcTierBinder"/>，本文件只做**接线与顺序**。</para>
    /// <para>**就地编辑的两个提交口**（尺寸 / 名字）在 `ScaleCalcWindow.Editing.cs`。</para>
    /// <para>🔴 **本文件不碰场景**：三件操作只改数据 + 重算离线表 + 落盘，一个场景操作都没有。</para>
    /// <para>🔴 **刷新一律推迟到下一拍**（<c>ScheduleTierTableRebind</c>）：勾选/删除/编辑的回调都是**从管理表里的控件**发出来的，
    /// 当场 <c>Clear()</c> 掉那张表等于**在事件派发途中销毁派发者**。推迟一拍既避开这件事，也让"操作后表格跟上"照旧成立。</para>
    /// </summary>
    public sealed partial class ScaleCalcWindow : ITierTableHost
    {
        private Foldout m_TierFoldout;
        private VisualElement m_TierListHost;
        private MultiColumnListView m_TierListView;
        private Button m_AddTierButton;

        /// <summary>**操作反馈行**（<c>Report</c> 写这里）——界面上唯一的反馈出口。</summary>
        private Label m_TierStatus;

        /// <summary>档位工作集：载入时被替换，所以**不是** <c>readonly</c>。</summary>
        private ScreenTierSet m_TierSet = ScreenTierSet.Default();

        /// <summary>落盘实现（<c>EditorPrefs</c>，键带工程路径短哈希）。</summary>
        private IScreenTierStorage m_Storage;

        /// <summary><c>CreateGUI</c> 里调（在第一次 <c>Rebuild</c> 之前）：取控件 → 载工作集（容错回落并**如实报告**）→ 绑表。</summary>
        private void InitTierSection()
        {
            m_TierFoldout = rootVisualElement.Q<Foldout>("tier-foldout");
            m_TierListHost = rootVisualElement.Q<VisualElement>("tier-host");
            m_AddTierButton = rootVisualElement.Q<Button>("add-tier-button");
            m_TierStatus = rootVisualElement.Q<Label>("tier-status");

            // 默认**展开**（服务"全量清单常驻可见"）。
            // 显式写出来而不是靠 Foldout 的默认值——默认值不是契约，这里要的是明说。
            if (m_TierFoldout != null) m_TierFoldout.value = true;

            m_Storage = new EditorPrefsScreenTierStorage();
            m_TierSet = ScreenTierStorage.LoadTiers(m_Storage, out string fallback);
            if (fallback != null) Report(ScreenTierStorage.DescribeFallback(fallback));   // 回退不许静默

            if (m_AddTierButton != null) m_AddTierButton.clicked += OnAddTierClicked;

            // 宿主高度**跟着条目数长**：纯函数决定，夹在 [下限, 可用高度 × 30%]
            // ⇒ 9 档时 9 行全看得见；档位再多则由列表自己滚动，**不许**把主表与底部顶出窗口。
            if (m_TierListHost != null)
                rootVisualElement.RegisterCallback<GeometryChangedEvent>(_ => ApplyTierHostHeight());
            RebindTierTable();
        }

        /// <summary>
        /// 按当前条目数与应用窗口高重算宿主高度（值没变就不写，避免几何回调自激——与选型表的行高同一手法）。
        /// </summary>
        private void ApplyTierHostHeight()
        {
            if (m_TierListHost == null) return;
            float available = rootVisualElement.resolvedStyle.height;
            float target = ScreenTierColumns.HostHeightFor(m_TierSet.Tiers.Count, available);
            if (System.Math.Abs(m_TierListHost.resolvedStyle.height - target) < 0.5f) return;
            m_TierListHost.style.height = target;
        }

        /// <summary>重绑管理表（数据 ⇒ 视图的唯一方向）。</summary>
        private void RebindTierTable()
        {
            if (m_TierListHost == null) return;
            m_TierListHost.Clear();
            m_TierListView = ScaleCalcTierBinder.Build(this, m_TierSet.Tiers);
            m_TierListHost.Add(m_TierListView);
            ApplyTierHostHeight();          // 条目数变了 ⇒ 宿主高度跟着变
        }

        /// <summary>把重绑推迟到下一拍（见类注释的理由）；<paramref name="scrollToId"/> 非空时顺带把那一行滚进视野。</summary>
        private void ScheduleTierTableRebind(string scrollToId)
        {
            rootVisualElement.schedule.Execute(() =>
            {
                RebindTierTable();
                if (scrollToId == null || m_TierListView == null || m_TierListView.panel == null) return;
                int index = m_TierSet.IndexOf(scrollToId);
                if (index >= 0) m_TierListView.ScrollToItem(index);   // 没接 panel 时没有布局，滚也没意义
            });
        }

        /// <summary>选型表的行来源：**被勾选**的条目。</summary>
        private List<ScreenProfile> IncludedProfiles()
        {
            var profiles = new List<ScreenProfile>();
            foreach (ScreenTier tier in m_TierSet.IncludedTiers()) profiles.Add(tier.ToProfile());
            return profiles;
        }

        /// <summary>勾选 / 取消：改数据 → 重算选型表 → 报文案 → 落盘 → 重绑管理表。</summary>
        void ITierTableHost.OnTierIncludedChanged(string id, bool included)
        {
            if (!m_TierSet.SetIncluded(id, included)) return;      // 没找到 / 值没变 ⇒ 什么都不做
            Rebuild();
            Report(m_TierSet.IncludedCount == 0
                ? ScreenTierText.NoneIncluded()
                : included ? ScreenTierText.Included(TierNameOf(id)) : ScreenTierText.Excluded(TierNameOf(id)));
            SaveTiers();
            ScheduleTierTableRebind(null);
        }

        /// <summary>
        /// 删除：**内置档先给可读说明**（不弹框——那会让"为什么删不掉"变成一个要点的框）；
        /// 自定义行**加二次确认**——虽然可逆，但重建得手填一遍尺寸，**可逆 ≠ 不疼**。
        /// <para><c>DisplayDialog</c> 只在**这条 UI 路径**上；纯逻辑在 <see cref="PerformTierDelete"/>，可在断言里直接调。</para>
        /// </summary>
        void ITierTableHost.OnTierDeleteClicked(string id)
        {
            string name = TierNameOf(id);
            if (!m_TierSet.CanDelete(id)) { Report(ScreenTierText.BuiltInNotDeletable(name)); return; }
            if (!EditorUtility.DisplayDialog("删除档位",
                    "删除「" + name + "」？删除后可用「新增一行」重建。", "删除", "取消")) return;
            PerformTierDelete(id);
        }

        /// <summary>删除的**纯逻辑**（无对话框）：供 UI 路径与断言共用。</summary>
        private void PerformTierDelete(string id)
        {
            string name = TierNameOf(id);
            if (!m_TierSet.Delete(id)) { Report(ScreenTierText.BuiltInNotDeletable(name)); return; }
            Rebuild();
            SaveTiers();
            Report(ScreenTierText.Deleted(name));
            ScheduleTierTableRebind(null);
        }

        // 🔴 **就地编辑的两个提交口不在这里**（`TryCommitTierSize` / `TryCommitTierName` + 两个显示文本）：
        //    它们住在 `ScaleCalcWindow.Editing.cs`，口径在 `ScreenTierSet` 的纯函数里。

        /// <summary>新增一行：默认 <c>1920x1080</c>，名字留空 ⇒ 自动名；新行滚进视野。</summary>
        private void OnAddTierClicked()
        {
            if (!m_TierSet.TryAdd(new ScaleSize(1920f, 1080f), null, out ScreenTier tier, out string error))
            {
                Report(error);                    // 达上限 / 尺寸非法：照抄可读原因，**不静默截断**
                return;
            }
            Rebuild();
            SaveTiers();
            Report(ScreenTierText.Added(tier.Name));
            ScheduleTierTableRebind(tier.Id);
        }

        /// <summary>落盘：失败**不回滚内存**——本会话照常可用，只如实告知。</summary>
        private void SaveTiers()
        {
            if (m_Storage == null) return;
            if (ScreenTierStorage.TrySave(m_TierSet, m_Storage, out string message)) return;
            Report(message);
        }

        private bool TryGetTier(string id, out ScreenTier tier)
        {
            int at = m_TierSet.IndexOf(id);
            tier = at >= 0 ? m_TierSet.Tiers[at] : default;
            return at >= 0;
        }

        /// <summary>找不到就给可读的占位（**不返回 <c>null</c>**：文案拼接不该生成 <c>「」</c> 这种半句话）。</summary>
        private string TierNameOf(string id) => TryGetTier(id, out ScreenTier tier) ? tier.Name : "(未知条目)";
    }
}
