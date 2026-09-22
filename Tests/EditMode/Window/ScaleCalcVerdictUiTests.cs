using NUnit.Framework;
using Wayward.ScaleCalc.Editor;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine.UIElements;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// **结论区**的界面落点：新控件是否**真的建得起来** · 位置是否守"工具栏下方、档位折叠区之上" ·
    /// 「三选一」是否逐字标注 · 设计约束单是否与结论条**同源**。
    /// <para>🔴 为什么另立文件：<c>ScaleCalcWindowUxmlTests.cs</c> 已 153 行，再塞这三条会破 200
    /// （行数按完整路径单独量）；而这三条的职责也不同——那份管"**整窗**能不能建起来"，本文件管"**结论区**"。</para>
    /// </summary>
    public sealed class ScaleCalcVerdictUiTests
    {
        private static string PackageRootAssetPath()
        {
            // 必须写全名：`UnityEditor.PackageInfo` 与 `UnityEditor.PackageManager.PackageInfo` 同名（CS0104）
            UnityEditor.PackageManager.PackageInfo info =
                UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(ScaleCalcVerdictUiTests).Assembly);
            Assert.That(info, Is.Not.Null, "PackageInfo.FindForAssembly 应当取到包信息");
            return info.assetPath;   // ⚠️ 必须是 assetPath（`resolvedPath` 是文件系统路径 ⇒ LoadAssetAtPath 取不到）
        }

        private static VisualTreeAsset LoadWindowUxml()
        {
            string assetPath = PackageRootAssetPath() + "/Editor/UI/ScaleCalcWindow.uxml";
            VisualTreeAsset uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(assetPath);
            Assert.That(uxml, Is.Not.Null, "包内 .uxml 必须能被 AssetDatabase 加载：" + assetPath);
            return uxml;
        }

        private static VisualElement CloneWindow()
        {
            var host = new VisualElement();
            LoadWindowUxml().CloneTree(host);
            return host;
        }

        /// <summary>
        /// 匹配方式**三选一** · 结论条 · 三方式小结 · 方式说明 · 复制结论 · 导出设计约束单
        /// ——**每一个都得真的建起来**（只查"文件里有这个名字"是假保护）。
        /// </summary>
        [Test]
        public void PK12_VerdictSection_HasTheSelectorBarSummaryAndTwoButtons()
        {
            var host = new VisualElement();
            VisualTreeAsset uxml = LoadWindowUxml();
            Assert.DoesNotThrow(() => uxml.CloneTree(host), "加结论条之后 CloneTree 仍必须成功");

            Assert.That(AssetDatabase.LoadAssetAtPath<StyleSheet>(
                    PackageRootAssetPath() + "/Editor/UI/ScaleCalcWindow.Verdict.uss"), Is.Not.Null,
                "结论区的样式表必须能被加载（拆表的代价：多一个资源就多一个可能失踪的路径）");

            Assert.That(host.Q<DropdownField>("match-mode"), Is.Not.Null,
                "匹配方式用运行时控件 DropdownField（`EnumField` 在 `.uxml` 里没有枚举类型可依 —— 实现期回写）");
            Assert.That(host.Q<Slider>("match-slider"), Is.Not.Null, "match 滑块仍在（三选一里只有 Match 用它）");
            Assert.That(host.Q<Label>("verdict-bar"), Is.Not.Null, "结论条是 Label（常显）");
            Assert.That(host.Q<Label>("verdict-modes"), Is.Not.Null, "三方式小结是 Label");
            Assert.That(host.Q<Label>("match-mode-note"), Is.Not.Null, "Expand/Shrink 下的说明是 Label");
            Assert.That(host.Q<Button>("verdict-copy"), Is.Not.Null, "「复制结论」是 Button");
            Assert.That(host.Q<Button>("export-constraints-button"), Is.Not.Null, "「导出设计约束单」是 Button");
            // 危险带那一行 + 两个筛选开关 + 「排除会裁档位」（**改工作集**的动作，与只看不改的筛选分开）
            Assert.That(host.Q<Label>("verdict-band"), Is.Not.Null, "危险带/安全设计区那一行是 Label");
            Assert.That(host.Q<Toggle>("only-cropped"), Is.Not.Null, "「只看会裁」是 Toggle");
            Assert.That(host.Q<Toggle>("only-portrait"), Is.Not.Null, "「只看竖屏」是 Toggle");
            Assert.That(host.Q<Button>("exclude-cropped-button"), Is.Not.Null, "「排除会裁档位」是 Button");
        }

        /// <summary>
        /// 列头排序接线：<c>Column.sortable</c> + <c>sortingMode = Custom</c> ⇒ 点列头排序（**默认不排序**）。
        /// <para>源码级判据：这两处一改回去，"点列头没反应"会**静默**发生（没有别的东西会报）。</para>
        /// </summary>
        [Test]
        public void HeaderSorting_IsWiredInTheBinder()
        {
            string source = System.IO.File.ReadAllText(
                System.IO.Path.Combine(PackageRootAssetPath(), "Editor", "Table", "ScaleCalcTableBinder.cs"));

            Assert.That(source, Does.Contain("sortable = true"), "列头要点得动");
            Assert.That(source, Does.Contain("ColumnSortingMode.Custom"), "自管排序（键与方向在 ScaleFit.Order.cs）");
            Assert.That(source, Does.Contain("ScaleFit.ViewRows"), "呈现走视图副本，**不**改模型顺序");
            // ⚠️ 查的是**代码形态**，不是这个词本身——注释里就写着"不写 `sortingEnabled`"（注释也会命中）
            Assert.That(source, Does.Not.Contain("sorting" + "Enabled ="), "Unity 6 已废弃该属性（实测 CS0618）");
        }

        /// <summary>位置判据：结论区在工具栏**之后**、档位折叠区**之前**（"工具栏下方、档位折叠区之上"）。</summary>
        [Test]
        public void PD6_VerdictBar_SitsBetweenToolbarAndTierSection()
        {
            VisualElement root = CloneWindow().Q<VisualElement>("scale-calc-root");
            int toolbar = IndexOfChild(root, "toolbar");
            int verdict = IndexOfChild(root, "verdict-row");     // 结论条在 `verdict-row` 里（右边是示意图）
            int tierFoldout = IndexOfChild(root, "tier-foldout");

            Assert.That(toolbar, Is.GreaterThanOrEqualTo(0), "工具栏必须在");
            Assert.That(verdict, Is.GreaterThan(toolbar), "结论区在工具栏下方");
            Assert.That(verdict, Is.LessThan(tierFoldout), "结论区在档位折叠区之上");

            // 结论条与示意图**同排**（横向排布是算平高度预算的手法：示意图的高度被左侧文本吸收）
            VisualElement row = root[verdict];
            Assert.That(IndexOfChild(row, "verdict-text"), Is.GreaterThanOrEqualTo(0), "左边是文本块（结论条在其中）");
            Assert.That(IndexOfChild(row, "safe-area-diagram"), Is.GreaterThanOrEqualTo(0), "右边是安全区示意图");
        }

        /// <summary>`PK14`：小结标题**逐字**含「三选一」；界面文案里不许出现**无修饰的**「不会被裁」。</summary>
        [Test]
        public void PK14_ThreeWayTitleAndNoNakedPromise()
        {
            Assert.That(ScaleCalcWindow.VerdictModesTitlePrefix, Does.Contain("三选一"), "标题必须显式标注三选一");

            string text = ScaleCalcWindow.VerdictModesText(ScreenProfiles.All, new ScaleSize(1920f, 1080f), 0.5f, 144f);
            string[] lines = text.Split('\n');
            Assert.That(lines.Length, Is.EqualTo(5), "标题 + 3 行 + 推荐值 1 行");
            Assert.That(lines[0], Does.Contain("三选一").And.Contains("7 档"));
            Assert.That(lines[1], Does.StartWith("Match 0.50"));
            Assert.That(lines[2], Does.StartWith("Expand"));
            Assert.That(lines[3], Does.StartWith("Shrink"));
            Assert.That(lines[4], Does.StartWith("推荐 match ").And.Contains("留白代价 +"), "推荐值必须连代价一起报");

            foreach (string line in lines)
                Assert.That(line, Does.Not.Contain("不会被裁"), "不许出现无限定语的「不会被裁」");
        }

        /// <summary>约束单四行**与结论条同源**，且首行限定语不可省（几何口径 ≠ 布局保证）。</summary>
        [Test]
        public void ConstraintsSheet_IsSourcedFromTheHeadline_AndCarriesTheLimiters()
        {
            var reference = new ScaleSize(1920f, 1080f);
            ScaleFit.ModeStats stats = ScaleFit.Stats(ScreenProfiles.All, reference, ScreenMatchMode.MatchWidthOrHeight, 0.5f, 144f);
            string[] lines = ScaleCalcWindow.BuildConstraintsText(stats, reference, 7).Split('\n');

            Assert.That(lines.Length, Is.EqualTo(4), "四行：文件头 / 结论行 / 安全设计区 / 危险带");
            Assert.That(lines[0], Does.StartWith("# ScaleCalc 设计约束单（几何口径，不代表具体布局"), "限定语与数字同屏");
            Assert.That(lines[0], Does.Contain("结论只对当前勾选的 7 档负责"));
            Assert.That(lines[1], Is.EqualTo(ScaleFit.Headline(stats, reference)), "结论行**逐字同源**");
            Assert.That(lines[2], Does.StartWith("安全设计区：978×935"));
            Assert.That(lines[3], Does.StartWith("危险带（左右各 / 上下各）：左右各 −471px · 上下各 −72px"),
                "单侧、且与现场实测一致");
        }

        /// <summary>`Expand`/`Shrink` 下 match 滑块必须**禁用 + 有说明**（`PK4` 的界面半）——源码级判据。</summary>
        [Test]
        public void PK4_SliderIsDisabledOutsideTheMatchMode_AndTheNoteTextIsPresent()
        {
            string source = System.IO.File.ReadAllText(
                System.IO.Path.Combine(PackageRootAssetPath(), "Editor", "Window", "ScaleCalcWindow.Verdict.cs"));

            Assert.That(source, Does.Contain("m_MatchSlider.SetEnabled(usesMatch)"), "禁用的是同一条判据（usesMatch）");
            Assert.That(source, Does.Contain("CurrentScreenMatch()"), "模板与 Build 都读它，不再写死");
            Assert.That(source, Does.Contain("该匹配方式不使用 match"), "禁用必须**同屏**给原因");
        }

        /// <summary>
        /// 「排除会裁档位」的顺序必须是 **改数据（含落盘）→ <c>Rebuild()</c> → 报文案**。
        /// <para>先报后算会报出**旧结论**——这类错误不会让任何数字变红，只会让人读到过期的话（同族）。</para>
        /// </summary>
        [Test]
        public void ExcludeCroppedTiers_OrderIsDataThenRebuildThenReport()
        {
            string source = System.IO.File.ReadAllText(
                System.IO.Path.Combine(PackageRootAssetPath(), "Editor", "Window", "ScaleCalcWindow.Filter.cs"));

            int method = source.IndexOf("private void ExcludeCroppedTiers()", System.StringComparison.Ordinal);
            Assert.That(method, Is.GreaterThan(0), "处理体必须在（名字改了就要同步这条断言）");

            int save = source.IndexOf("SaveTiers();", method, System.StringComparison.Ordinal);
            int rebuild = source.IndexOf("Rebuild();", method, System.StringComparison.Ordinal);
            int report = source.IndexOf("Report(\"已排除", method, System.StringComparison.Ordinal);

            Assert.That(save, Is.GreaterThan(method), "① 先改数据并落盘");
            Assert.That(rebuild, Is.GreaterThan(save), "② 再重算（结论随之更新）");
            Assert.That(report, Is.GreaterThan(rebuild), "③ **最后**才报文案");
        }

        // 【已搬迁】源码级守卫（"状态机只认 bool、不认显示文案"）**整条搬走**——
        //   它原来读窗口状态章节的 `hasVerdict && mismatch`（分支栏配色判据），
        //   而分支栏后来整条撤除 ⇒ 守卫正确变红、判定搬了家。
        //   新家 = `Tests/EditMode/Table/ScaleCalcReconcileColumnTests.cs`（改为"读四个 `Delta*` 数值字段、
        //   不读 `DeltaText`/`Tooltip` 文案"）。这里**不留空壳**（空壳 = "看着在守、其实没守"）。

        private static int IndexOfChild(VisualElement parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
                if (parent[i].name == name) return i;
            return -1;
        }
    }
}
