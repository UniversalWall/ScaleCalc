using System.Globalization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// ScaleCalc 选型台。
    /// <para>**铁律**：算法全在内核，窗口**只做 bind/呈现**——窗口里不许出现 <c>if (canvas.isRootCanvas)</c> 这类判定，
    /// 分支值一律来自 <see cref="ScaleCalcResult.Branch"/>。</para>
    /// <para>技术形态：UI Toolkit（<c>.uxml</c>/<c>.uss</c> 与窗口同程序集，经包根解析加载）。</para>
    /// <para>🔴 **意图驱动**：本窗口只在用户**明确要求**时才动场景——「现场对账」按钮，
    /// 或"自动对账"开关打开且停手之后。**开窗与拖控件都零场景操作**；对账细节与真值缓存见 `ScaleCalcWindow.Measure.cs`。</para>
    /// <para>本类按职责分文件（拆的判据 = 200 行红线 + 职责各不同）：本文件（主体：控件绑定与重算）·
    /// `…Measure.cs`（对账章节，**唯一会动场景的地方**）· `…Status.cs`（字符条）·
    /// `…Export.cs`（导出章节）· `…Rows.cs`（**档位管理章节**：工作集载入/三件操作/落盘）·
    /// `…Editing.cs`（**就地编辑的两个提交口**：尺寸与名字）·
    /// `…Verdict.cs`（**结论与匹配方式章节**：结论条 + 三选一 + 小结 + 复制）·
    /// `…Filter.cs`（排序 / 筛选 / 一键排除）· `…Diagram.cs`（安全区示意图）·
    /// `…Template.cs`（条件从哪来）· `…Transfer.cs`（档位清单导入/导出）· `…Selection.cs`（选中身份）。</para>
    /// </summary>
    public sealed partial class ScaleCalcWindow : EditorWindow
    {
        private const string UxmlName = "ScaleCalcWindow.uxml";
        private const string UssName = "ScaleCalcWindow.uss";

        /// <summary>结论区的样式表：与 <see cref="UssName"/> **各管一段**（拆的判据 = 200 行红线 + 职责不同）。</summary>
        private const string VerdictUssName = "ScaleCalcWindow.Verdict.uss";

        /// <summary>逐档裁留示意图列的样式表：同上，图那一列单独一张。</summary>
        private const string DiagramUssName = "ScaleCalcWindow.Diagram.uss";

        private VisualElement m_TableHost;

        /// <summary>表格上方的口径图例：文字**只从** <see cref="ScaleFit.TableLegend"/> 来，不在窗口里另写一份。</summary>
        private Label m_TableLegend;
        private FloatField m_RefWidth;
        private FloatField m_RefHeight;
        private Slider m_MatchSlider;
        private Label m_MatchValue;
        private Button m_ReconcileButton;
        private Toggle m_AutoReconcileToggle;
        private Button m_ExportButton;
        private Button m_ExportPackButton;

        private ScaleTable m_Table;

        /// <summary>
        /// 最近一次 <c>Rebuild()</c> 算出的**整表真值语境**：喂 <see cref="ScaleTable.Build"/>，逐行写进
        /// <see cref="ScaleTableRow.Reconcile"/>，供对账状态列的悬停使用。构造见 <c>BuildReconcileContext</c>。
        /// </summary>
        private ReconcileContext m_Reconcile;

        // 🔴 **选中身份**（`m_SelectedTierName` / `OnRowSelectionChanged`）住在 `ScaleCalcWindow.Selection.cs`
        //    （并进本文件会顶破 200 行红线）。

        /// <summary>
        /// 窗口底线（高度预算的逐项算式写在 `.uss` 的"高度预算"头注里）：低于这个高度，两张表就都装不下内置档位。
        /// <para>⚠️ 两次实测教训：① 500 高时总需求超过窗口 ⇒ flex 把每段压到内容高度以下、文字**叠在一起**；
        /// ② 620 高时管理表只能看见 **4 行**（内置却有 7 行）——用户当场发现"行数没对上"。</para>
        /// <para>历次抬升都是**加段落**造成的：结论区那一段（匹配方式一组 + 方式说明 + 结论条 + 3 行小结）
        /// ⇒ 680 → 810；危险带一行 + 两个筛选开关 + 「排除会裁档位」⇒ 810 → 880；
        /// 字符条块 + 「导出当前视图」⇒ 880 → **950**（实测 920 时主表只剩 197px ⇒ 只能看见 8 行，9 行需要 204）。</para>
        /// <para>🔴 **后来删掉了底部两段**（分支栏 + 总结行，约 128px 实测）与**选中行详情**（约 36px）
        /// ⇒ 固定段变短，但**底线不动**：底线是实测出来的，删内容不等于可以顺手下调；
        /// 那点余量留给"能多看见几行"。</para>
        /// </summary>
        private static readonly Vector2 MinWindowSize = new Vector2(900f, 950f);

        [MenuItem("Wayward/ScaleCalc/打开选型台", priority = 1000)]
        private static void Open()
        {
            ScaleCalcWindow window = GetWindow<ScaleCalcWindow>("ScaleCalc 选型台");
            window.minSize = MinWindowSize;
            window.Show();
        }

        private void CreateGUI()
        {
            // 也在这里设一次：`Open()` 只在菜单那条路上跑过，已经开着的窗口要能拿到新的底线
            minSize = MinWindowSize;

            VisualTreeAsset uxml = ScaleCalcAssets.LoadUxml(UxmlName);
            if (uxml == null)
            {
                rootVisualElement.Add(new Label("ScaleCalc：加载 " + UxmlName + " 失败 —— " + ScaleCalcAssets.Describe(UxmlName)));
                return;
            }
            uxml.CloneTree(rootVisualElement);
            StyleSheet uss = ScaleCalcAssets.LoadUss(UssName);
            if (uss != null) rootVisualElement.styleSheets.Add(uss);
            // 结论区的样式单独一张（加载失败不致命：那一段只是没上色）
            StyleSheet verdictUss = ScaleCalcAssets.LoadUss(VerdictUssName);
            if (verdictUss != null) rootVisualElement.styleSheets.Add(verdictUss);
            // 逐档示意图列的样式：同样单独一张，加载失败也不致命（图会少配色，但仍在）
            StyleSheet diagramUss = ScaleCalcAssets.LoadUss(DiagramUssName);
            if (diagramUss != null) rootVisualElement.styleSheets.Add(diagramUss);

            m_TableHost = rootVisualElement.Q<VisualElement>("table-host");
            // 口径图例：**只读纯函数**——`.uxml` 里那份只是占位文本，避免同一句文案两处字面量
            m_TableLegend = rootVisualElement.Q<Label>("table-legend");
            if (m_TableLegend != null) m_TableLegend.text = ScaleFit.TableLegend();
            m_RefWidth = rootVisualElement.Q<FloatField>("ref-width");
            m_RefHeight = rootVisualElement.Q<FloatField>("ref-height");
            m_MatchSlider = rootVisualElement.Q<Slider>("match-slider");
            m_MatchValue = rootVisualElement.Q<Label>("match-value");
            m_ReconcileButton = rootVisualElement.Q<Button>("reconcile-button");
            m_AutoReconcileToggle = rootVisualElement.Q<Toggle>("auto-reconcile-toggle");
            m_ExportButton = rootVisualElement.Q<Button>("export-button");
            m_ExportPackButton = rootVisualElement.Q<Button>("export-pack-button");

            // 输入框延迟提交：否则每敲一个字符都触发一次全表重算（输入 "1920" 会重算 1/19/192/1920 四次），
            // 中间值还会被当成合法参考分辨率算进表里。`isDelayed` ⇒ 回车 / 失焦才提交。
            if (m_RefWidth != null) m_RefWidth.isDelayed = true;
            if (m_RefHeight != null) m_RefHeight.isDelayed = true;

            // 控件值变化 ⇒ **只重算离线表**（廉价、纯函数），并把待执行的自动对账抖掉重排；**绝不直接对账**
            if (m_MatchSlider != null) m_MatchSlider.RegisterValueChangedCallback(OnMatchChanged);
            if (m_RefWidth != null) m_RefWidth.RegisterValueChangedCallback(_ => OnParameterChanged());
            if (m_RefHeight != null) m_RefHeight.RegisterValueChangedCallback(_ => OnParameterChanged());

            // 对账入口：只有用户点按钮、或"自动对账"开关打开时才动场景
            if (m_ReconcileButton != null) m_ReconcileButton.clicked += ReconcileNow;
            if (m_AutoReconcileToggle != null) m_AutoReconcileToggle.RegisterValueChangedCallback(OnAutoReconcileChanged);

            // 两个导出按钮都走保存对话框（不做路径输入框）；默认文件名/目录来自约定落点
            if (m_ExportButton != null) m_ExportButton.clicked += OnExport;
            if (m_ExportPackButton != null) m_ExportPackButton.clicked += OnExportEvidencePack;
            // 「导出设计约束单」：**另一条链路**（几何口径的交付物），与上面两条各自独立
            Button exportConstraints = rootVisualElement.Q<Button>("export-constraints-button");
            if (exportConstraints != null) exportConstraints.clicked += OnExportConstraints;
            // 「导出当前视图」：**第三条链路**——所见列 + 当前排序/筛选（标签自带口径，同族）
            Button exportView = rootVisualElement.Q<Button>("export-view-button");
            if (exportView != null) exportView.clicked += OnExportCurrentView;

            // 档位管理章节（工作集载入 + 管理表绑定）必须在第一次 Rebuild **之前**接好：
            // 选型表的行就是"被勾选的条目"，先建表再载工作集会白算一次。
            InitTierSection();
            // 档位清单导入 / 导出章节：同样是"取控件 + 挂回调"，也在第一次 Rebuild 之前
            InitTierTransferSection();
            // 结论与匹配方式章节：结论条/小结/三选一选择器 —— 同样要在第一次 Rebuild 之前接好
            InitVerdictSection();
            // 排序/筛选/工作集联动章节：只改"看的顺序"；排盘与取消勾选走既有路径
            InitFilterSection();
            // 安全区示意图章节：几何纯函数 + 摆元素；放在结论区右侧 ⇒ 不抬底线
            InitDiagramSection();

            Rebuild();                       // ⚠️ 只建表，**不对账** —— 开窗不再建/拆场景
        }

        // **"条件从哪来"住在 `ScaleCalcWindow.Template.cs`**
        // （`OnMatchChanged` / `OnParameterChanged` / `CurrentTemplate`）。本文件留"控件怎么绑、表怎么重算"。

        /// <summary>
        /// 重算整张表。**纯离线**：只吃**上次对账缓存**的真值，**一个场景操作都不做**。
        /// <para>**本窗口不做程序化写字段**（不写 <c>FloatField</c>/<c>Slider</c> 的 <c>value</c>），所以不需要"重入守卫"——
        /// 曾经有过一个从未被置为 true 的重入守卫字段，属同类死代码，已删除。
        /// 将来若要程序化写这些控件的值（<c>m_RefWidth.value = x</c> 会触发 <c>ChangeEvent</c>），**必须同时加回重入守卫**。</para>
        /// <para>⚠️ 这里原先还往底部两段文本写内容，**那两个节点已整条撤除** ⇒ 连同只为喂它们的计时与 dpi 局部量一起删掉
        /// （留一个算了不用的计时，就是同类死代码）。真值来历改由**对账状态列的悬停**承担。</para>
        /// </summary>
        private void Rebuild()
        {
            ScaleCalcInput template = CurrentTemplate();
            var reference = template.ReferenceResolution;
            float match = template.MatchWidthOrHeight;

            // 整表真值语境：**先算它再建表**——表要把它逐行带出去（对账状态列的悬停靠它）
            m_Reconcile = BuildReconcileContext(in template);

            m_Table = ScaleTable.Build(reference, CurrentScreenMatch(), match, template.ScreenDpi,
                                       ScaleMode.ScaleWithScreenSize, m_Truth, m_TruthOk, m_Reconcile, IncludedProfiles());

            if (m_TableHost != null)
            {
                m_TableHost.Clear();
                var options = new ScaleCalcTableBinder.ViewOptions(
                    sortKey: m_SortKey, ascending: m_SortAscending, onlyCropped: OnlyCropped, onlyPortrait: OnlyPortrait,
                    selectName: m_SelectedTierName, onSortChanged: OnSortChanged, onSelectionChanged: OnRowSelectionChanged);
                MultiColumnListView list = ScaleCalcTableBinder.Build(m_Table, options);
                m_TableHost.Add(list);
                // 选中行在这里不做事：重建后的高亮靠 `selectName`（按档位名）恢复，见 `ScaleCalcWindow.Selection.cs`。
            }

            // 结论条 + 三方式小结：**在数据全部改完之后**再算 ⇒ 报出的结论与表同源
            UpdateMatchModeUi();
            UpdateVerdictBar(in template);
        }
    }
}
