using NUnit.Framework;
using Wayward.ScaleCalc.Editor;
using Wayward.ScaleCalc.Unity;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// **真值条件带匹配方式**的落点断言：反例（<c>Match</c> 下测的真值不许用在 <c>Expand</c> 表上）与
    /// <c>Expand</c>/<c>Shrink</c> 不读 <c>match</c>、**默认构造的真值永不适用**。
    /// <para>🔴 为什么另立文件：这是"条件三件套 × 守门"的**语义**判据（<see cref="LiveTruth"/> 层），
    /// 既不属列定义也不属工作集；<c>ScaleCalcTableModelTests.cs</c> 只剩二十几行余量，塞进去会当场贴线。</para>
    /// </summary>
    public sealed class ScaleCalcTruthConditionTests
    {
        private static readonly ScaleSize Reference = new ScaleSize(1920f, 1080f);
        private static readonly ScaleSize Live = new ScaleSize(1080f, 1920f);

        private static LiveTruth Truth(ScreenMatchMode mode, float match)
            => new LiveTruth(1f, 100f, Live, Live, Live, ScreenSizeSource.CanvasRenderingDisplaySize,
                             measuredReference: Reference, measuredMatch: match, measuredScreenMatch: mode);

        /// <summary>`PK17`：`Expand`/`Shrink` 下 **`match` 不参与条件**（内核根本不读它）。</summary>
        [Test]
        public void PK17_ExpandAndShrink_IgnoreMatch()
        {
            LiveTruth expand = Truth(ScreenMatchMode.Expand, 0.5f);
            Assert.That(expand.AppliesTo(Reference, 0f, ScreenMatchMode.Expand), Is.True, "同参考同方式、match 不同 ⇒ 仍适用");
            Assert.That(expand.AppliesTo(Reference, 0.99f, ScreenMatchMode.Expand), Is.True);
            Assert.That(expand.AppliesTo(Reference, 0.5f, ScreenMatchMode.Shrink), Is.False, "方式不同 ⇒ 不适用");
            Assert.That(expand.AppliesTo(new ScaleSize(1280f, 720f), 0.5f, ScreenMatchMode.Expand), Is.False, "参考不同 ⇒ 不适用");

            LiveTruth match = Truth(ScreenMatchMode.MatchWidthOrHeight, 0.5f);
            Assert.That(match.AppliesTo(Reference, 0.5f, ScreenMatchMode.MatchWidthOrHeight), Is.True);
            Assert.That(match.AppliesTo(Reference, 0.25f, ScreenMatchMode.MatchWidthOrHeight), Is.False, "Match 下 match 是条件之一");
        }

        /// <summary>`PK17`（兜底）：**默认构造的真值不适用于任何条件**——参数带默认值 ⇒ 漏改不报错。</summary>
        [Test]
        public void PK17_DefaultConstructedTruth_NeverApplies()
        {
            var empty = default(LiveTruth);

            Assert.That(empty.AppliesTo(Reference, 0.5f, ScreenMatchMode.MatchWidthOrHeight), Is.False);
            Assert.That(empty.AppliesTo(Reference, 0f, ScreenMatchMode.Expand), Is.False);
            Assert.That(empty.AppliesTo(Reference, 0f, ScreenMatchMode.Shrink), Is.False);
            Assert.That(empty.AppliesTo(default, 0f, ScreenMatchMode.Expand), Is.False, "连参考都没有 ⇒ 更不许适用");
        }

        /// <summary>条件文案带方式：`Match` 带数值、`Expand`/`Shrink` 不带。</summary>
        [Test]
        public void ConditionText_CarriesTheMatchMode()
        {
            Assert.That(Truth(ScreenMatchMode.MatchWidthOrHeight, 0.5f).ConditionText,
                Is.EqualTo("1920x1080 × Match 0.50"));
            Assert.That(Truth(ScreenMatchMode.Expand, 0.5f).ConditionText, Is.EqualTo("1920x1080 × Expand"));
            Assert.That(Truth(ScreenMatchMode.Shrink, 0.5f).ConditionText, Is.EqualTo("1920x1080 × Shrink"));
        }

        /// <summary>
        /// `PK16`（**反例**）：`Expand` 表 + `Match` 真值 ⇒ **整表离线**，且 `TruthNote` **不自相矛盾**
        /// （本表条件是 `Expand`，就不该写 `match`）。
        /// </summary>
        [Test]
        public void PK16_MatchTruth_DoesNotApplyToAnExpandTable()
        {
            LiveTruth truth = Truth(ScreenMatchMode.MatchWidthOrHeight, 0.5f);
            // 🔴 2026-09-21 批 3：本用例测的正是"真值条件与本表条件不符"⇒ **顺手把整表语境也传对**
            //    （`AppliesToTable = false`）——这样它同时守住新列的"条件不符"那一句悬停文案。
            var context = new ReconcileContext(
                hasSession: true, measuredOk: true, appliesToTable: false,
                measuredConditionText: truth.ConditionText,
                tableConditionText: LiveTruth.ConditionTextOf(Reference, ScreenMatchMode.Expand, 0.5f),
                measuredScreenSize: truth.ScreenSize,
                truthError: null,
                provenance: "本次会话第 1 次现场对账（条件 1920x1080 × Match 0.50）");

            ScaleTable table = ScaleTable.Build(Reference, ScreenMatchMode.Expand, 0.5f, 0f,
                                                ScaleMode.ScaleWithScreenSize, truth, true, context);

            Assert.That(table.HasTruthRow, Is.False, "条件不符 ⇒ 不补现场行");
            foreach (ScaleTableRow row in table.Rows) Assert.That(row.HasTruth, Is.False, "不许出现假差值");
            Assert.That(table.TruthNote, Does.Contain("与本表条件（1920x1080 × Expand）"), "本表条件**带方式**");
            Assert.That(table.TruthNote, Does.Not.Contain("与本表条件（1920x1080 × Match"), "不许把本表条件写成 Match");

            // 新列口径：条件不符**不能**被说成"还没对过账"——那是最容易被误读的一种沉默
            Assert.That(ReconcileVerdict.CauseOf(table.Rows[0]), Is.EqualTo(ReconcileVerdict.NoTruthCause.ConditionsDiffer),
                "成因必须是「条件不符」，不是「没对过账」");
            Assert.That(ReconcileVerdict.Tooltip(table.Rows[0]), Does.Contain("与本表条件"));
        }

        /// <summary>同一份真值用在**同条件**的 `Expand` 表上 ⇒ 照样可用（否则修过头了）。</summary>
        [Test]
        public void PK16_ExpandTruth_AppliesToAnExpandTable()
        {
            LiveTruth truth = Truth(ScreenMatchMode.Expand, 0.5f);
            // 条件相符 ⇒ 语境必须说"适用"（否则新列会把可用的真值说成"条件不符"，属于修过头）
            var context = new ReconcileContext(
                hasSession: true, measuredOk: true, appliesToTable: true,
                measuredConditionText: truth.ConditionText,
                tableConditionText: LiveTruth.ConditionTextOf(Reference, ScreenMatchMode.Expand, 0.5f),
                measuredScreenSize: truth.ScreenSize,
                truthError: null,
                provenance: "本次会话第 1 次现场对账（条件 1920x1080 × Expand）");

            ScaleTable table = ScaleTable.Build(Reference, ScreenMatchMode.Expand, 0.5f, 0f,
                                                ScaleMode.ScaleWithScreenSize, truth, true, context);

            Assert.That(table.HasTruthRow, Is.True, "现场尺寸在清单里 ⇒ 那一行带真值");
            Assert.That(table.Rows[0].Reconcile.AppliesToTable, Is.True, "整表语境随行带出且为「适用」");
        }
    }
}
