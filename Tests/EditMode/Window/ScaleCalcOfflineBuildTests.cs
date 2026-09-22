using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Wayward.ScaleCalc.Editor;
using Wayward.ScaleCalc.Unity;
using UnityEditor.PackageManager;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// **副作用的显式化与节流**：让"会动场景"这件事只说真话，
    /// 并且只在用户要求时做。
    /// <para>分两类判据（**两条都做、不假装**）：</para>
    /// <list type="bullet">
    /// <item><b>行为层</b>：离线入口跑一次，场景集合逐项不变（继承 <see cref="ScaleCalcCleanupInvariantTestBase"/>，
    /// 用夹具自建宿主 ⇒ **不碰活动场景**）；</item>
    /// <item><b>源码层</b>：<c>Rebuild</c> 路径不出现任何测量调用、现场入口有且只有一个、它自己的注释写明会建/拆场景。</item>
    /// </list>
    /// <para>纪律：凡"要求搜不到某词"的目标 token 一律**字符拼接**构造，绝不写字面量。</para>
    /// </summary>
    public sealed class ScaleCalcOfflineBuildTests : ScaleCalcCleanupInvariantTestBase
    {
        private static string PackageRoot =>
            PackageInfo.FindForAssembly(typeof(ScaleCalcOfflineBuildTests).Assembly).resolvedPath;

        /// <summary>
        /// （行为层）：**离线入口 <c>BuildCsv</c> 一个场景操作都不做**——跑一次前后，
        /// 场景集合（路径逐项）与宿主根对象数必须完全相同。
        /// <para>这是正题：原来那个公开的 <c>Build()</c> 名字像纯函数，实际建/拆临时场景。</para>
        /// </summary>
        [Test]
        public void OfflineBuildCsv_DoesNotTouchAnyScene()
        {
            string[] scenesBefore = CurrentScenePaths();
            int hostRootsBefore = Host.GetRootGameObjects().Length;
            bool hostDirtyBefore = Host.isDirty;

            string csv = EvidencePackCommand.BuildCsv();
            Assert.That(csv, Is.Not.Empty, "离线入口仍应产出完整 CSV");

            Assert.That(CurrentScenePaths(), Is.EqualTo(scenesBefore), "场景集合不该有任何变化（不建、不拆、不换）");
            Assert.That(Host.GetRootGameObjects().Length, Is.EqualTo(hostRootsBefore), "宿主根对象数不该变");
            Assert.That(Host.isDirty, Is.EqualTo(hostDirtyBefore), "宿主脏标记不该被这条路径改动");
        }

        /// <summary>
        /// `J4`/`J9`（行为层）：带真值参数的重载同样是纯函数——**传进去的真值只影响文本，不影响场景**。
        /// </summary>
        [Test]
        public void OfflineBuildCsvWithTruth_DoesNotTouchAnyScene()
        {
            string[] scenesBefore = CurrentScenePaths();
            int hostRootsBefore = Host.GetRootGameObjects().Length;

            // 真值由调用方给出（不取），本用例只关心"传参这条路也不动场景"
            var truth = new LiveTruth(1f, 100f, new ScaleSize(1920f, 1080f), new ScaleSize(1920f, 1080f),
                                      new ScaleSize(1920f, 1080f), ScreenSizeSource.CanvasRenderingDisplaySize,
                                      measuredReference: EvidencePackCommand.LiveReference, measuredMatch: EvidencePackCommand.LiveMatch, measuredScreenMatch: ScreenMatchMode.MatchWidthOrHeight);
            string csv = EvidencePackCommand.BuildCsv(out int rows, truth, true);

            Assert.That(rows, Is.GreaterThan(0));
            Assert.That(csv, Does.Contain("已取到"), "给了真值就要如实说取到了");
            Assert.That(CurrentScenePaths(), Is.EqualTo(scenesBefore), "场景集合不该有任何变化");
            Assert.That(Host.GetRootGameObjects().Length, Is.EqualTo(hostRootsBefore), "宿主根对象数不该变");
        }

        /// <summary>
        /// `J5`（源码层）：**重算路径里不许出现任何对账/测量调用**——参数一变只重算离线表（廉价、纯函数）。
        /// <para>判据按 `R-B1` 写死为源码级：`ScaleCalcWindow.cs`（`Rebuild` 所在文件）里搜不到测量调用形态。</para>
        /// </summary>
        [Test]
        public void RebuildPath_NeverMeasures()
        {
            string window = File.ReadAllText(Path.Combine(PackageRoot, "Editor", "Window", "ScaleCalcWindow.cs"));

            // 不写字面量（否则断言命中自己）
            string measureCall = "Measure" + "Live(";
            string reconcileCall = "Try" + "Measure(";
            Assert.That(window, Does.Not.Contain(measureCall),
                "窗口的重算路径（ScaleCalcWindow.cs）里不该有现场测量调用——对账只走 Measure 章节的显式入口");
            Assert.That(window, Does.Not.Contain(reconcileCall),
                "窗口主体不该直接碰协议；现场取真值只允许经 EvidencePackCommand 的显式现场入口");
        }

        /// <summary>
        /// 且它的 XML 注释**第一段就写明会建/拆场景**，
        /// 并且它确实调用了协议。
        /// </summary>
        [Test]
        public void LiveEntryIsSingleAndSaysItMutatesScenes()
        {
            var files = new List<string>();
            foreach (string f in Directory.GetFiles(Path.Combine(PackageRoot, "Editor", "Evidence"), "EvidencePackCommand*.cs"))
                files.Add(f);
            files.Sort();

            string all = string.Empty;
            var callers = new List<string>();
            string protocolCall = "Try" + "Measure(";
            foreach (string f in files)
            {
                string text = File.ReadAllText(f);
                all += text;
                if (text.Contains(protocolCall)) callers.Add(Path.GetFileName(f));
            }

            Assert.That(callers.Count, Is.EqualTo(1),
                "调协议的只允许有一个文件（现场入口所在的落点/入口章节）：" + string.Join(" , ", callers.ToArray()));
            Assert.That(Path.GetFileName(callers[0]), Is.EqualTo("EvidencePackCommand.cs"),
                "现场入口应当落在落点/入口章节，而不是 CSV 产出章节");

            string entry = "public static bool Measure" + "Live(";
            Assert.That(all, Does.Contain(entry), "应当有显式的现场入口");
            Assert.That(all, Does.Contain("会建/拆临时场景"),
                "现场入口的注释必须第一句就说清它会建/拆场景（名字与注释都要与副作用一致）");
        }

        /// <summary>
        /// （源码层）：**导出路径不取真值**——导出按钮把**缓存**的真值喂给纯函数。
        /// <para>两条导出入口都不许出现测量调用；证据包那条必须把 `m_Truth`/`m_TruthOk` 传下去。</para>
        /// </summary>
        [Test]
        public void ExportPathFeedsCachedTruth_AndNeverMeasures()
        {
            string export = File.ReadAllText(Path.Combine(PackageRoot, "Editor", "Window", "ScaleCalcWindow.Export.cs"));

            string measureCall = "Measure" + "Live(";
            string protocolCall = "Try" + "Measure(";
            Assert.That(export, Does.Not.Contain(measureCall), "导出路径不许顺手对账（导出零场景操作）");
            Assert.That(export, Does.Not.Contain(protocolCall), "导出路径不许直接碰协议");
            Assert.That(export, Does.Contain("m_Truth"), "证据包导出要把窗口缓存的那份真值喂给纯函数");
            Assert.That(export, Does.Contain("m_TruthOk"), "同时要传真值可得性（否则会假装对过账）");
        }

        /// <summary>
        /// （源码层）：**开窗不对账**，且对账入口只挂在「按钮点击」与「防抖回调」上；
        /// "自动对账"开关**默认关**（UXML 里 `value="false"`，代码里不做程序化打开）。
        /// </summary>
        [Test]
        public void ReconcileIsWiredOnlyToExplicitIntent_AndAutoToggleDefaultsOff()
        {
            string window = File.ReadAllText(Path.Combine(PackageRoot, "Editor", "Window", "ScaleCalcWindow.cs"));
            string measure = File.ReadAllText(Path.Combine(PackageRoot, "Editor", "Window", "ScaleCalcWindow.Measure.cs"));
            string uxml = File.ReadAllText(Path.Combine(PackageRoot, "Editor", "UI", "ScaleCalcWindow.uxml"));

            // ① 开窗路径不许排自动对账：**排程只有两处**——`OnParameterChanged`（控件值变化）与
            //    `OnAutoReconcileChanged`（开关被打开）。`CreateGUI` 里若偷偷排一次就会变 3。
            //    ⚠️ 这两处**跨 partial 文件**（参数变化回调在 …ScaleCalcWindow.cs，开关回调在 …Measure.cs）⇒ 合并数
            //    🔴 **扫全目录**（不是 `ScaleCalcWindow*.cs`）：误名的那个文件被正名为
            //    `PhysicsFallbackDiagnostic.cs` 之后，按前缀 glob 会**少扫一个文件**——而下面是"恰好 2 处"的**精确计数**，
            //    少扫一个文件就可能把本该报出来的违规藏起来。
            string windowAll = string.Empty;
            foreach (string f in Directory.GetFiles(Path.Combine(PackageRoot, "Editor", "Window"), "*.cs"))
                windowAll += File.ReadAllText(f);

            int scheduled = CountOccurrences(windowAll, "ScheduleAutoReconcile();");
            Assert.That(scheduled, Is.EqualTo(2),
                "排自动对账只允许两处：参数变化 / 开关打开；开窗路径不许排");
            //    它两处都是**方法组**（`clicked += ReconcileNow` / `schedule.Execute(ReconcileNow)`），不写成调用形态
            //    ⇒ 去掉声明行之后，全窗口**搜不到** `ReconcileNow()` 这个调用形态。这条判据一箭双雕：
            //    既证明没人直接喊它，也保证它只能经按钮或防抖排程到达。
            string bodyOnly = string.Empty;
            foreach (string line in windowAll.Split('\n'))
                if (!line.Contains("private void ")) bodyOnly += line + "\n";
            Assert.That(CountOccurrences(bodyOnly, "ReconcileNow()"), Is.EqualTo(0),
                "现场对账不许被直接调用（`ReconcileNow()`）：只能经按钮 clicked 或防抖排程到达");

            // ② 对账入口只挂两处：按钮 clicked + 防抖回调
            Assert.That(window, Does.Contain("m_ReconcileButton.clicked += ReconcileNow"),
                "「现场对账」按钮是显式意图入口");
            Assert.That(measure, Does.Contain("schedule.Execute(ReconcileNow).StartingIn(AutoReconcileDebounceMs)"),
                "自动对账必须经防抖排程（拖一次滑块最多 1 次对账）");

            // ③ 开关默认关
            Assert.That(uxml, Does.Contain("auto-reconcile-toggle"), "开关要在界面上");
            //    🔴 口径改准：原来断言的是"整个 .uxml 里不许出现 value=\"true\""——那是个**过宽的代理**：
            //    档位管理区加了 Foldout，它**合法地**需要"默认展开"。断言的真实意图是
            //    "「自动对账」这个开关默认关"，所以改成**针对那一行**判（改口径不删断言）。
            string toggleLine = null;
            foreach (string line in uxml.Split('\n'))
                if (line.Contains("auto-reconcile-toggle")) { toggleLine = line; break; }
            Assert.That(toggleLine, Is.Not.Null, "界面上必须有「自动对账」开关");
            Assert.That(toggleLine, Does.Not.Contain("value=\"true\""),
                "「自动对账」默认必须是关的（开窗与拖控件都不对账）");
        }

        /// <summary>数一个 token 在文本里出现几次（自己拼的断言用，避免正则/转义坑）。</summary>
        private static int CountOccurrences(string text, string token)
        {
            int count = 0, at = 0;
            while ((at = text.IndexOf(token, at, System.StringComparison.Ordinal)) >= 0)
            {
                count++;
                at += token.Length;
            }
            return count;
        }
    }
}
