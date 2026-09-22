using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Wayward.ScaleCalc.Editor;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// **对账状态列**的落点断言。
    /// <para>🔴 为什么另立文件：既有测试文件已贴 200 行红线（行数按完整路径单独量，不靠印象），
    /// 且"状态列的口径"与"列定义单的形状"是两件事。</para>
    /// <para>命名（<c>Reconcile</c> 而不是 <c>Verdict</c>）：<c>Window/ScaleCalcVerdictUiTests.cs</c> 管的是
    /// **结论条那一片 UI**，与"对账状态列"是两件事 —— 一名一物。</para>
    /// </summary>
    public sealed class ScaleCalcReconcileColumnTests
    {
        private static string PackageRoot =>
            UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(ScaleCalcReconcileColumnTests).Assembly).resolvedPath;

        private static ScaleTableRow Row(bool hasTruth, float deltaScaleFactor, ReconcileContext context)
        {
            var row = new ScaleTableRow
            {
                Profile = new ScreenProfile("测试档", 1920f, 1080f, "测试"),
                Reference = new ScaleSize(1920f, 1080f),
                HasTruth = hasTruth,
                DeltaScaleFactor = deltaScaleFactor,
                Reconcile = context,
            };
            return row;
        }

        private static ReconcileContext Context(bool hasSession, bool measuredOk, bool applies)
            => new ReconcileContext(hasSession, measuredOk, applies,
                                    measuredConditionText: "1920x1080 × Match 0.50",
                                    tableConditionText: "1920x1080 × Expand",
                                    measuredScreenSize: new ScaleSize(2560f, 1440f),
                                    truthError: "编辑期未渲染",
                                    provenance: "本次会话第 1 次现场对账（条件 1920x1080 × Match 0.50）");

        /// <summary>列集 = 10 · 前两列是**两个行首判据列**（对账 · 裁留图）· 后 8 列与原来那 8 列逐位一致（「裁留图」**插在**它们之前，不是挪进里面）。</summary>
        [Test]
        public void K1_ReconcileIsTheFirstColumn_AndTheRestKeepPD10Order()
        {
            Assert.That(ScaleTableColumns.All.Length, Is.EqualTo(10), "对账 + 裁留图 + 原有 8 列");
            Assert.That(ScaleTableColumns.All[0].Title, Is.EqualTo(ScaleTableColumns.ReconcileTitle));
            Assert.That(ScaleTableColumns.ReconcileTitle, Is.EqualTo("对账"), "表头常量（改判只动这一个常量）");
            Assert.That(ScaleTableColumns.All[1].Title, Is.EqualTo(ScaleTableColumns.DiagramTitle), "第 2 列 = 裁留图（用户 2026-09-21 裁定「放到前面」）");

            string[] rest = { "屏幕档位", "比例", "横向", "纵向", "实际画布", "sf", "屏幕尺寸", "差值" };
            for (int i = 0; i < rest.Length; i++)
                Assert.That(ScaleTableColumns.All[i + 2].Title, Does.StartWith(rest[i]), "第 " + (i + 3) + " 列");
        }

        /// <summary>三态**只由 <c>HasTruth</c> + 四个 <c>Delta*</c> 决定**，且覆盖全部组合；含边界（<c>== Tolerance</c> 算通过）。</summary>
        [Test]
        public void K2_ThreeStates_CoverEveryCombination_AndTheBoundaryIsInclusive()
        {
            ReconcileContext ctx = Context(true, true, true);

            Assert.That(ReconcileVerdict.StateOf(Row(false, 99f, ctx)), Is.EqualTo(ReconcileVerdict.State.NoTruth),
                "没有真值 ⇒ 一律 `NoTruth`（差值字段再离谱也不看）");
            Assert.That(ReconcileVerdict.Text(Row(false, 99f, ctx)), Is.EqualTo("—"));

            Assert.That(ReconcileVerdict.StateOf(Row(true, 0f, ctx)), Is.EqualTo(ReconcileVerdict.State.Pass));
            Assert.That(ReconcileVerdict.Text(Row(true, 0f, ctx)), Is.EqualTo("过"));

            Assert.That(ReconcileVerdict.StateOf(Row(true, ReconcileVerdict.Tolerance + 0.0001f, ctx)),
                Is.EqualTo(ReconcileVerdict.State.Fail), "超一点点也算失");
            Assert.That(ReconcileVerdict.Text(Row(true, 0.5f, ctx)), Is.EqualTo("失"));

            Assert.That(ReconcileVerdict.StateOf(Row(true, ReconcileVerdict.Tolerance, ctx)),
                Is.EqualTo(ReconcileVerdict.State.Pass), "🔴 边界：|Δ| == 容差算**通过**（判据是「大于才算失」）");

            // 另外三个差值分量各自也能单独把状态打成 Fail（不许只看 sf）
            var ppu = Row(true, 0f, ctx); ppu.DeltaReferencePpu = 1f;
            var cw = Row(true, 0f, ctx); cw.DeltaCanvasWidth = 1f;
            var ch = Row(true, 0f, ctx); ch.DeltaCanvasHeight = 1f;
            Assert.That(ReconcileVerdict.StateOf(ppu), Is.EqualTo(ReconcileVerdict.State.Fail), "ΔrefPPU 也要判");
            Assert.That(ReconcileVerdict.StateOf(cw), Is.EqualTo(ReconcileVerdict.State.Fail), "Δcanvas 宽也要判");
            Assert.That(ReconcileVerdict.StateOf(ch), Is.EqualTo(ReconcileVerdict.State.Fail), "Δcanvas 高也要判");
        }

        /// <summary><c>—</c> 的**四种成因互不相同**，且优先级 = 条件不符 &gt; 没取到 &gt; 没对过账。</summary>
        [Test]
        public void K4_NoTruthCauses_AreFourDistinctSentences()
        {
            var never = Row(false, 0f, default);                                    // ① 默认语境 = 没对过账
            var failed = Row(false, 0f, Context(true, false, false));               // ② 对过但没取到
            var differ = Row(false, 0f, Context(true, true, false));                // ③ 取到了、条件不符
            var size = Row(false, 0f, Context(true, true, true));                   // ④ 条件相符但尺寸不在表里

            Assert.That(ReconcileVerdict.CauseOf(never), Is.EqualTo(ReconcileVerdict.NoTruthCause.NotMeasuredThisSession));
            Assert.That(ReconcileVerdict.CauseOf(failed), Is.EqualTo(ReconcileVerdict.NoTruthCause.MeasurementFailed));
            Assert.That(ReconcileVerdict.CauseOf(differ), Is.EqualTo(ReconcileVerdict.NoTruthCause.ConditionsDiffer),
                "🔴 条件不符**优先于**「没取到」——它才是最容易被误读成「没对过账」的那一种");
            Assert.That(ReconcileVerdict.CauseOf(size), Is.EqualTo(ReconcileVerdict.NoTruthCause.SizeNotInTable));

            string[] texts =
            {
                ReconcileVerdict.Tooltip(never), ReconcileVerdict.Tooltip(failed),
                ReconcileVerdict.Tooltip(differ), ReconcileVerdict.Tooltip(size),
            };
            Assert.That(new HashSet<string>(texts).Count, Is.EqualTo(4), "四句必须互不相同（否则用户分不出成因）");
            Assert.That(texts[0], Does.Contain("还没点过"));
            Assert.That(texts[1], Does.Contain("未取到"));
            Assert.That(texts[2], Does.Contain("与本表条件"));
            Assert.That(texts[3], Does.Contain("不在档位表里"));
        }

        /// <summary>失败行的悬停**逐字以** <c>"对账失败：" + DeltaText</c> 结尾（同一个事实不许两处各拼一遍）。</summary>
        [Test]
        public void K4_FailTooltip_ReusesDeltaTextVerbatim()
        {
            ScaleTableRow row = Row(true, 0.25f, Context(true, true, true));
            Assert.That(ReconcileVerdict.Tooltip(row), Is.EqualTo("对账失败：" + row.DeltaText),
                "失败原因与差值列**同源**（显示精度的口径也一并继承）");
        }

        /// <summary><c>HasTruth</c> 为真的行**全表最多一行**（结构性不变量）。</summary>
        [Test]
        public void K3_AtMostOneRowHasTruth_InAnyBuiltTable()
        {
            ScaleTable table = ScaleTable.Build(new ScaleSize(1920f, 1080f), ScreenMatchMode.MatchWidthOrHeight,
                                                0.5f, 144f, ScaleMode.ScaleWithScreenSize,
                                                default, false, default);

            int withTruth = 0;
            foreach (ScaleTableRow row in table.Rows) if (row.HasTruth) withTruth++;
            Assert.That(withTruth, Is.EqualTo(0), "没对过账 ⇒ 一行都没有");
            Assert.That(table.Rows.Count, Is.GreaterThan(0), "分母不为零（防「扫空集 = 假绿」）");
        }

        /// <summary>的兄弟判据（兜底）：**默认语境的行**必须如实说"还没点过现场对账"——
        /// 这条防的是"调用点漏传 <see cref="ReconcileContext"/> ⇒ 界面说假话"。</summary>
        [Test]
        public void K_DefaultContext_MeansNeverReconciled_NotSilentSuccess()
        {
            var row = new ScaleTableRow { Profile = new ScreenProfile("裸行", 1920f, 1080f, "测试") };
            Assert.That(ReconcileVerdict.Text(row), Is.EqualTo("—"), "漏赋值 ⇒ `—`，不许编出「过」");
            // 🔴 断言用**实际措辞里的连续子串**（初稿写 `还没点过现场对账`，而文案是
            //    `本次会话还没点过「现场对账」`——中间插了词 ⇒ 逐字子串断言当场红。同那次"逐字断言钉真实文本"的教训）
            Assert.That(ReconcileVerdict.Tooltip(row), Does.Contain("还没点过"));
        }

        /// <summary>撤除的完整性（源码级）——「更多列」链路全仓搜不到。
        /// <para>🔴 **边界**：<c>ComputeWidths</c>/<c>Distribute</c> 的 <c>specs</c> **形参是保留项**（与"更多列"无关），
        /// 所以这里禁的是 <c>Extra</c> 成员与 <c>VisibleColumns</c> 方法，**不碰 <c>specs</c>**。</para></summary>
        [Test]
        public void K6_MoreColumnsPipelineIsGone()
        {
            string source = ReadAllPackageSources();
            Assert.That(source.Length, Is.GreaterThan(100000), "分母保护：真的读到了包内源码");
            Assert.That(source, Does.Not.Contain("More" + "Columns"));
            Assert.That(source, Does.Not.Contain("Visible" + "Columns"));
            Assert.That(source, Does.Not.Contain("more" + "-columns"));
        }

        /// <summary>删除的完整性——底部两段（<c>.uxml</c> 节点 + <c>.uss</c> 规则 + 控件名）全仓搜不到。</summary>
        [Test]
        public void K7_FooterAndSummaryNodesAreGone()
        {
            string uxml = File.ReadAllText(Path.Combine(PackageRoot, "Editor", "UI", "ScaleCalcWindow.uxml"));
            string uss = File.ReadAllText(Path.Combine(PackageRoot, "Editor", "UI", "ScaleCalcWindow.uss"));

            Assert.That(uxml, Does.Not.Contain("branch" + "-status"), "节点名不许留");
            Assert.That(uxml, Does.Not.Contain("name=\"" + "summary" + "\""), "总结行节点不许留");
            Assert.That(uss, Does.Not.Contain("\n.branch-"), "四条 branch-* 配色规则不许留");
            Assert.That(uss, Does.Not.Contain("\n.footer"), "`.footer` 规则不许留");
            Assert.That(uss, Does.Not.Contain("\n.summary"), "`.summary` 规则不许留");
        }

        /// <summary>的**新家**（原守卫读窗口状态章节的 <c>hasVerdict &amp;&amp; mismatch</c>，
        /// 而分支栏已撤 ⇒ 判定搬到 <see cref="ReconcileVerdict"/>）：状态**只认 <c>Delta*</c> 数值字段**，不许拿文案反推。
        /// <para>它守的是一件真事：把「对账失败」改一个字，配色/状态不会静默失效（编译不报错、断言也不报错）。</para></summary>
        [Test]
        public void S7_StateComesFromNumericDeltas_NotFromDisplayText()
        {
            string source = File.ReadAllText(Path.Combine(PackageRoot, "Editor", "Table", "ReconcileVerdict.cs"));

            Assert.That(source, Does.Not.Contain("StartsWith(\"对账"), "不许拿显示文案判状态");
            Assert.That(source, Does.Not.Contain("Contains(\"对账"), "同上");
            foreach (string field in new[] { "DeltaScaleFactor", "DeltaReferencePpu", "DeltaCanvasWidth", "DeltaCanvasHeight" })
                Assert.That(source, Does.Contain(field), "判据必须读数值字段：" + field);
        }

        /// <summary>把包内 <c>Editor</c> 的 <c>.cs</c>/<c>.uxml</c>/<c>.uss</c> 全读成一个大字符串（上面那条的取样器）。</summary>
        private static string ReadAllPackageSources()
        {
            var builder = new System.Text.StringBuilder();
            string[] extensions = { "*.cs", "*.uxml", "*.uss" };
            foreach (string extension in extensions)
                foreach (string file in Directory.GetFiles(Path.Combine(PackageRoot, "Editor"), extension,
                                                           SearchOption.AllDirectories))
                    builder.Append(File.ReadAllText(file)).Append('\n');
            return builder.ToString();
        }
    }
}
