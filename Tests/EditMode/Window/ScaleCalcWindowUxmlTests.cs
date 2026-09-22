using NUnit.Framework;
using Wayward.ScaleCalc.Editor;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine.UIElements;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// 🔴 **界面的真实加载断言**（来由是一次真实故障）：包内 <c>.uxml</c> **必须能加载并实例化成功**，
    /// 且代码里 <c>Query</c> 的每个控件名都得在。
    /// <para><b>为什么单独一个文件</b>：职责不同——<see cref="ScaleCalcOfflineBuildTests"/> 管"副作用语义"
    /// （场景/真值/导出），本文件管"**界面能不能真的建起来**"。</para>
    /// <para><b>故障实录</b>：<c>&lt;uie:Toggle&gt;</c>（<c>UnityEditor.UIElements</c>）在 UI Toolkit 里**没有注册工厂**
    /// ——<c>Toggle</c> 是**运行时**控件（<c>UnityEngine.UIElements</c>）。开窗即报
    /// <c>Element 'UnityEditor.UIElements.Toggle' is missing a UxmlElementAttribute and has no registered factory method</c>。
    /// 而当时的源码级判据只看"文件里有没有这个字符串"⇒ 完全拦不住。</para>
    /// <para><b>教训</b>：**"文件里有这个字符串"不等于"这个界面能建起来"**。
    /// 凡"界面元素"类改动，断言必须走到 <c>CloneTree</c> 这一步；只读文件的判据在这里是假保护。</para>
    /// </summary>
    public sealed class ScaleCalcWindowUxmlTests
    {
        private static string PackageRootAssetPath()
        {
            // 必须写全名：`UnityEditor.PackageInfo` 与 `UnityEditor.PackageManager.PackageInfo` 同名（CS0104）
            UnityEditor.PackageManager.PackageInfo info =
                UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(ScaleCalcWindowUxmlTests).Assembly);
            Assert.That(info, Is.Not.Null, "PackageInfo.FindForAssembly 应当取到包信息");
            return info.assetPath;
        }

        /// <summary>
        /// `.uxml` 能加载 + **能实例化**（元素工厂全部注册得上），且两套 `.uxml`/`.uss` 都在。
        /// </summary>
        [Test]
        public void WindowUxml_LoadsAndInstantiates_WithEveryNamedControlPresent()
        {
            string assetPath = PackageRootAssetPath() + "/Editor/UI/ScaleCalcWindow.uxml";
            VisualTreeAsset uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(assetPath);
            Assert.That(uxml, Is.Not.Null, "包内 .uxml 必须能被 AssetDatabase 加载：" + assetPath);
            Assert.That(AssetDatabase.LoadAssetAtPath<StyleSheet>(PackageRootAssetPath() + "/Editor/UI/ScaleCalcWindow.uss"),
                Is.Not.Null, "包内 .uss 必须能被加载");

            // 这一步才是真判据：任何元素工厂注册不上（命名空间写错、控件名打错）都会在这里抛
            var host = new VisualElement();
            Assert.DoesNotThrow(() => uxml.CloneTree(host),
                "CloneTree 必须成功——元素工厂问题（如把运行时控件写成 uie: 前缀）会在这里暴露");

            // 逐个确认代码里 Query 的那些名字都在（名字打错 ⇒ 控件静默为 null ⇒ 用户点了没反应）
            // 🔴 底部两段的两个名字**已删**——它们对应的节点整条撤除（"这些文本详情没必要"）。
            //    **删断言必须同时删节点**（只删断言不删节点 = 界面上留个没人用的空块）。
            foreach (string name in new[]
            {
                "table-host", "ref-width", "ref-height", "match-slider", "match-value",
                "reconcile-button", "auto-reconcile-toggle", "export-button", "export-pack-button",
                // 档位管理区：名字打错 ⇒ 控件静默为 null ⇒ 用户看不到管理表
                "tier-foldout", "add-tier-button", "tier-host", "tier-status",
                // 表格口径图例：窗口在 `CreateGUI` 里 Query 这个名字（打错 ⇒ 图例永远是占位文本）
                "table-legend",
            })
            {
                Assert.That(host.Q<VisualElement>(name), Is.Not.Null,
                    "界面里缺少名为「" + name + "」的控件（代码在 Query 它）");
            }

            // 图例必须是 **Label** 且**不许留空**（空控件会被挤成 3.3px、还占一条宽度，
            // 当年是靠用户截图才发现的）。正文由 `ScaleFit.TableLegend()` 写入。
            Label legend = host.Q<Label>("table-legend");
            Assert.That(legend, Is.Not.Null, "表格口径图例必须是 Label");
            Assert.That(legend.text, Is.Not.Empty, "图例控件不许留空——.uxml 里放非空占位文本");
        }

        /// <summary>
        /// 🔴 **布局预算的落点级守卫**（2026-09-19 实测事故：`K1`/`K1a` 全绿、控件类型全对，
        /// 而窗口里**文字叠成一团**——用户截图才发现）。
        /// <para>根因是**高度预算超了**：总 min-height 之和 &gt; 窗口高度 ⇒ flex 把每一段压到内容高度以下，
        /// 而 `overflow` 默认 `visible` ⇒ 文字画到邻居身上。所以钉两条**落点**事实：
        /// ① 会挤压的三个容器必须声明 `overflow: hidden`（外溢只能被裁，不许画到别人身上）；
        /// ② 管理表挂载点必须**定死高度**（不许只给 min/max —— 那正是它当年溢出父框 54px 的原因）。</para>
        /// </summary>
        [Test]
        public void Layout_ContainersClipInsteadOfSpilling()
        {
            string uss = System.IO.File.ReadAllText(
                System.IO.Path.Combine(PackageRootAssetPath(), "Editor", "UI", "ScaleCalcWindow.uss"));

            foreach (string selector in new[] { ".root", ".tier-host", ".table-host" })
                Assert.That(RuleOf(uss, selector), Does.Contain("overflow: hidden"),
                    selector + " 必须声明 overflow: hidden —— 否则高度不够时内容会画到邻居身上（实测过）");

            string tierHost = RuleOf(uss, ".tier-host");
            // 🔴 **行容量必须装得下内置档位**（"行数没对上"就是这么来的——定死 110px 时只显示 4 行）。
            // 预算 = 内置档位数 × 行高 + 表头估算；**预算要可推导，不能靠散落的魔数**。
            float declaredTierHeight = PxOf(tierHost, "min-height");
            float needed = ScreenProfiles.All.Length * ScreenTierColumns.RowHeight + ScreenTierColumns.HeaderHeightEstimate;
            Assert.That(declaredTierHeight, Is.GreaterThanOrEqualTo(needed),
                "管理表的 min-height 必须装得下内置 " + ScreenProfiles.All.Length + " 档（需要 " + needed + "px，声明了 " + declaredTierHeight + "px）");
        }

        /// <summary>取规则块里某条属性的像素值（`min-height: 200px` ⇒ 200）。取不到给 <c>-1</c>。</summary>
        private static float PxOf(string rule, string property)
        {
            string head = property + ":";
            int at = rule.IndexOf(head, System.StringComparison.Ordinal);
            if (at < 0) return -1f;
            int end = rule.IndexOf('p', at + head.Length);
            if (end < 0) return -1f;
            return float.TryParse(rule.Substring(at + head.Length, end - at - head.Length).Trim(),
                System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v)
                ? v : -1f;
        }

        /// <summary>取 `.uss` 里某个选择器的规则块（从选择器行到下一个 `}`）——只为上面那条断言用。</summary>
        private static string RuleOf(string uss, string selector)
        {
            int at = uss.IndexOf(selector + " {", System.StringComparison.Ordinal);
            if (at < 0) return string.Empty;
            int end = uss.IndexOf('}', at);
            return end < 0 ? uss.Substring(at) : uss.Substring(at, end - at);
        }

        /// <summary>
        /// 档位管理区：<c>Foldout</c> 与「新增一行」按钮必须是**运行时**命名空间里的控件
        /// （写回 <c>uie:</c> 前缀这条就会挂——同一个坑）。
        /// </summary>
        [Test]
        public void TierSection_HasAFoldoutAndAnAddButton()
        {
            VisualTreeAsset uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                PackageRootAssetPath() + "/Editor/UI/ScaleCalcWindow.uxml");
            var host = new VisualElement();
            Assert.DoesNotThrow(() => uxml.CloneTree(host), "加了档位管理区之后，CloneTree 仍必须成功");

            Assert.That(host.Q<Foldout>("tier-foldout"), Is.Not.Null, "管理表区应当是个 Foldout");
            Assert.That(host.Q<Button>("add-tier-button"), Is.Not.Null, "「新增一行」应当是 Button");
            Assert.That(host.Q<VisualElement>("tier-host"), Is.Not.Null, "管理表的挂载点");
            Assert.That(host.Q<Label>("tier-status"), Is.Not.Null, "操作反馈行（Report 写这里）");
        }

        /// <summary>
        /// 「自动对账」确实是 <see cref="Toggle"/> 且**默认关**（界面侧判据）。
        /// <para>顺带钉住它走的是**运行时**命名空间——写回 <c>uie:</c> 前缀这条就会挂。</para>
        /// <para>【已删】原先还有一条守《物理覆盖表》折叠区的断言：那张 40 行矩阵已**整条撤除**
        /// （<c>.uxml</c> 节点与 <c>.uss</c> 规则同时删）。
        /// **删它时必须同时删 <c>.uxml</c> 里的节点**——只删断言会留下"界面上还有个没人用的折叠区"。</para>
        /// </summary>
        [Test]
        public void AutoReconcileToggle_IsARuntimeToggle_AndDefaultsOff()
        {
            VisualTreeAsset uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                PackageRootAssetPath() + "/Editor/UI/ScaleCalcWindow.uxml");
            var host = new VisualElement();
            uxml.CloneTree(host);

            Toggle auto = host.Q<Toggle>("auto-reconcile-toggle");
            Assert.That(auto, Is.Not.Null, "「自动对账」应当是 UnityEngine.UIElements.Toggle 控件");
            Assert.That(auto.value, Is.False, "「自动对账」默认必须是关的（开窗与拖控件都不对账）");
            Assert.That(host.Q<Button>("reconcile-button"), Is.Not.Null, "「现场对账」应当是 Button");
            Assert.That(host.Q<Button>("export-pack-button"), Is.Not.Null, "证据包导出应当是 Button");
        }

        /// <summary>两个新按钮**真的加载得出来**、是 <c>Button</c>、**文本非空**，且与「新增一行」**同处 <c>tier-toolbar</c>**——同一行是硬要求（另起一行多占 ≈23px，主表会少显示一行）。</summary>
        [Test]
        public void TierTransferButtons_LoadAsButtonsWithNonEmptyText_InsideTheSameToolbar()
        {
            VisualTreeAsset uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                PackageRootAssetPath() + "/Editor/UI/ScaleCalcWindow.uxml");
            var host = new VisualElement();
            Assert.DoesNotThrow(() => uxml.CloneTree(host), "加了两个按钮之后，CloneTree 仍必须成功");

            VisualElement toolbar = host.Q<VisualElement>("tier-toolbar");
            Assert.That(toolbar, Is.Not.Null, "三个按钮必须共处 tier-toolbar（同一行）");
            foreach (string name in new[] { "add-tier-button", "export-tiers-button", "import-tiers-button" })
            {
                Button button = host.Q<Button>(name);
                Assert.That(button, Is.Not.Null, name + " 应当是 Button（写回 uie: 前缀这条就会挂）");
                Assert.That(button.text, Is.Not.Empty, name + " 的文本不许为空");
                Assert.That(toolbar.Contains(button), Is.True, name + " 必须在 tier-toolbar 里，而不是另起一行");
            }
        }

        /// <summary>**可发现性守卫**：两个新按钮藏在档位折叠区里 ⇒ 可见性依赖 <c>ScaleCalcWindow.Rows.cs</c> 里那行**显式展开**（<c>Foldout</c> 的默认值不是契约），而全仓**没有别的断言守它**（既有断言只查 <c>tier-foldout</c> 存在）⇒ 谁把折叠区改成默认收起，这条就会红，而不是让两个按钮**静默消失**（要改折叠默认值，必须**同时**把按钮挪出折叠区）。</summary>
        [Test]
        public void TierFoldout_IsExplicitlyExpandedInSource_SoTheButtonsStayDiscoverable()
        {
            string source = System.IO.File.ReadAllText(System.IO.Path.Combine(
                PackageRootAssetPath(), "Editor", "Window", "ScaleCalcWindow.Rows.cs"));
            string token = "m_TierFoldout.value" + " = true;";
            Assert.That(source, Does.Contain(token),
                "折叠区必须由代码显式展开——两个导入/导出按钮就在里面，收起即静默消失");
        }
    }
}
