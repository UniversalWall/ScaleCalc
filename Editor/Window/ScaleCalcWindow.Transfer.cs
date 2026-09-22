using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// <see cref="ScaleCalcWindow"/> 的**档位清单导入 / 导出章节**（工作集与三件操作在 `…Rows.cs`、
    /// 就地编辑在 `…Editing.cs`、导出在 `…Export.cs`）。
    /// <para>🔴 **本文件不碰场景**：导出只写文件、导入只换工作集 + 重算离线表，一个场景操作都没有。</para>
    /// <para>🔴 **导入的顺序本身就是判据**：先解析、**全过**才动数据 ⇒ 坏文件零副作用（工作集 / 磁盘 / 表 / 场景全不动）。</para>
    /// </summary>
    public sealed partial class ScaleCalcWindow
    {
        private Button m_ExportTiersButton;
        private Button m_ImportTiersButton;

        /// <summary>
        /// <c>CreateGUI</c> 里调（与 <c>InitTierSection</c> 并列、同样在第一次 <c>Rebuild</c> 之前）。
        /// <para>⚠️ 命名跟**既有的 <c>Init*Section</c> 一族**走——同一段接线在一个文件里叫两个名字，
        /// 下一个人会以为它们语义不同。</para>
        /// </summary>
        private void InitTierTransferSection()
        {
            m_ExportTiersButton = rootVisualElement.Q<Button>("export-tiers-button");
            m_ImportTiersButton = rootVisualElement.Q<Button>("import-tiers-button");

            // 🔴 控件为 null 也要能跑（老 `.uxml` / 加载失败）——与既有几个按钮同口径
            if (m_ExportTiersButton != null) m_ExportTiersButton.clicked += OnExportTiersClicked;
            if (m_ImportTiersButton != null) m_ImportTiersButton.clicked += OnImportTiersClicked;
        }

        /// <summary>
        /// 导出清单：落点由保存对话框给 ⇒ 那声"替换吗？"就是用户的**显式许可**，所以传 <c>allowOverwrite: true</c>。
        /// <para>取消对话框 ⇒ **立刻返回**：不写盘、**也不改状态栏**（状态栏是"刚才发生了什么"的如实记录，
        /// 什么都没发生就不该多一行字）。</para>
        /// </summary>
        private void OnExportTiersClicked()
        {
            string path = EditorUtility.SaveFilePanel("导出档位清单", "", ScreenTierTransfer.DefaultFileName, "txt");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                ScreenTierTransfer.ExportTo(m_TierSet, path, allowOverwrite: true);
                Report("已导出档位清单：" + path + "（" + m_TierSet.Tiers.Count + " 档，其中 "
                     + m_TierSet.IncludedCount + " 档参与计算；UTF-8 无 BOM，可直接入库）");
            }
            catch (Exception e)
            {
                Report("导出档位清单失败：" + e.GetType().Name + " / " + e.Message + "（路径：" + path + "）");
            }
        }

        /// <summary>
        /// 导入清单：**先解析、全部通过、才动数据**。
        /// <para>🔴 收尾顺序照抄既有档位操作（勾选 / 删除 / 新增）：<c>Rebuild → SaveTiers → Report →
        /// ScheduleTierTableRebind</c>——先让选型表与文案反映新状态，再落盘，最后整表重绑管理表。
        /// 落盘失败时 <c>SaveTiers</c> 会用自己的文案覆盖 <c>Report</c>（与既有行为一致）。</para>
        /// <para>★ **替换而不是就地改**：<c>m_TierSet = new ScreenTierSet(tiers)</c> 是既有手法（载入工作集就是这么写的）
        /// ⇒ 不必给 <see cref="ScreenTierSet"/> 加新 API；<c>tiers</c> 是解析新造的列表，不与旧对象共享引用。</para>
        /// <para>⚠️ 这里**不对账**：导入只重算离线表。</para>
        /// </summary>
        private void OnImportTiersClicked()
        {
            string path = EditorUtility.OpenFilePanel("导入档位清单", "", "txt");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                if (!ScreenTierTransfer.TryImport(path, out List<ScreenTier> tiers, out string message))
                {
                    Report("导入档位清单失败：" + message);       // 原因含行号；工作集未动
                    return;
                }

                int before = m_TierSet.Tiers.Count;
                m_TierSet = new ScreenTierSet(tiers);
                Rebuild();
                SaveTiers();
                Report("已用文件里的 " + tiers.Count + " 档替换原 " + before + " 档（其中 "
                     + m_TierSet.IncludedCount + " 档参与计算）");
                ScheduleTierTableRebind(null);
            }
            catch (Exception e)
            {
                Report("导入档位清单失败：" + e.GetType().Name + " / " + e.Message + "（路径：" + path + "）");
            }
        }
    }
}
