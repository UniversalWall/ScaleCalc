using System.Collections.Generic;
using NUnit.Framework;
using Wayward.ScaleCalc.Editor;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// **推荐行**：**纯函数断言，不用开窗**。
    /// <para>四件与 <see cref="ScaleFit.ModeLine"/> 同源 · 反例「会裁 6/7」 · 永不编数 · 术语全称 · 目标文案。</para>
    /// <para>夹具与 <see cref="ScaleFitSummaryTests"/> **同值**（那条反例的 <c>6/7</c> 才能复现）。</para>
    /// </summary>
    public sealed class ScaleFitRecommendationTests
    {
        private static readonly ScaleSize Reference = new ScaleSize(1920f, 1080f);
        private const float Dpi = 144f;
        private const float Match = 0.5f;

        private static IReadOnlyList<ScreenProfile> BuiltIn => ScreenProfiles.All;

        /// <summary>推荐行四件齐全，且**数值与 <see cref="ScaleFit.ModeLine"/> 的对应字段逐字一致**（同一 <c>ModeStats</c> 的两种呈现）。</summary>
        [Test]
        public void TK1_RecommendationLine_CarriesTheFourFields_SameAsModeLine()
        {
            Assert.That(ScaleFit.TryRecommendMatch(BuiltIn, Reference, Dpi, ScaleFit.Objective.MinWorstCrop,
                                                   out float t, out ScaleFit.ModeStats stats), Is.True);

            string line = ScaleFit.RecommendationText(stats, t, ScaleFit.Objective.MinWorstCrop);
            string mode = ScaleFit.ModeLine(stats, t);

            Assert.That(line, Does.Contain("会裁 " + stats.CroppedCount + "/" + stats.TierCount), "四件之一：会裁 X/N");
            Assert.That(line, Does.Contain("最坏裁切 "), "四件之二：最坏裁切");
            Assert.That(line, Does.Contain("留白代价 +"), "四件之三：留白代价");
            Assert.That(line, Does.Contain("安全设计区 "), "四件之四：安全设计区（**全称**）");

            Assert.That(Group(line, @"会裁 \d+/\d+"), Is.EqualTo(Group(mode, @"会裁 \d+/\d+")), "「会裁 X/N」两处同源");
            Assert.That(Group(line, @"安全设计区 \d+×\d+"), Is.EqualTo(Group(mode, @"安全设计区 \d+×\d+")), "「安全设计区 W×H」两处同源");
            Assert.That(Num(line, @"最坏裁切 ([\d.]+)%"), Is.EqualTo(Num(mode, @"最坏 [\u2212-]([\d.]+)%")), "「最坏」数值两处同源");
            Assert.That(Num(line, @"留白代价 \+([\d.]+)%"), Is.EqualTo(Num(mode, @"留白 \+([\d.]+)%")), "「留白」数值两处同源");
        }

        /// <summary>
        /// 🔴 **反例**——内置 7 档下推荐行的「会裁」必须**逐字是 `6/7`**。
        /// 把"为把最坏裁切降 3.8pp，让会裁档数从 1/7 涨到 6/7"这句话从文档里变成**机器守的事实**。
        /// </summary>
        [Test]
        public void TK2_RecommendedLine_SaysSixOfSevenCropped()
        {
            Assert.That(ScaleFit.TryRecommendMatch(BuiltIn, Reference, Dpi, ScaleFit.Objective.MinWorstCrop,
                                                   out float t, out ScaleFit.ModeStats stats), Is.True);

            Assert.That(t, Is.EqualTo(0.17f).Within(0.005f), "推荐仍是 0.17（现场实测）");
            Assert.That(ScaleFit.RecommendationText(stats, t, ScaleFit.Objective.MinWorstCrop),
                Does.Contain("会裁 6/7"), "推荐值让 6/7 档挨裁 —— 这个代价必须出现在推荐行上");
            Assert.That(stats.CroppedCount, Is.EqualTo(6), "6/7 必须来自同一 stats，不是硬编码");
        }

        /// <summary>合法前置 ⇒ 四件齐全且**无占位符**；违反前置 ⇒ **不抛异常、不编数**。</summary>
        [Test]
        public void TK3_RecommendedLine_NeverPrintsPlaceholders()
        {
            Assert.That(ScaleFit.TryRecommendMatch(BuiltIn, Reference, Dpi, ScaleFit.Objective.MinWorstCrop,
                                                   out float t, out ScaleFit.ModeStats stats), Is.True);
            string line = ScaleFit.RecommendationText(stats, t, ScaleFit.Objective.MinWorstCrop);
            foreach (string bad in new[] { "—", "NaN", "Infinity", "?" })
                Assert.That(line, Does.Not.Contain(bad), "合法前置下不许出现占位符 / 编数：" + bad);

            // ① 0 档参与
            ScaleFit.ModeStats none = ScaleFit.Stats(new List<ScreenProfile>(), Reference,
                                                     ScreenMatchMode.MatchWidthOrHeight, Match, Dpi);
            string guardNone = string.Empty;
            Assert.DoesNotThrow(() => guardNone = ScaleFit.RecommendationText(none, 0f, ScaleFit.Objective.MinWorstCrop));
            Assert.That(guardNone, Does.Contain("不适用"), "0 档 ⇒ 如实说不适用");
            Assert.That(guardNone, Does.Not.Contain("安全设计区"), "0 档 ⇒ 不产出安全设计区（不编数）");

            // ② 参考无效 ⇒ 全部档位都没有画布
            ScaleFit.ModeStats noCanvas = ScaleFit.Stats(BuiltIn, new ScaleSize(0f, 0f),
                                                         ScreenMatchMode.MatchWidthOrHeight, Match, Dpi);
            string guardCanvas = ScaleFit.RecommendationText(noCanvas, 0f, ScaleFit.Objective.MinWorstCrop);
            Assert.That(guardCanvas, Does.Contain("不适用"), "无画布 ⇒ 如实说不适用");
            Assert.That(guardCanvas, Does.Not.Contain("安全设计区"), "无画布 ⇒ 不产出安全设计区（不编数）");
        }

        /// <summary>术语**每一处都是全称**——🔴 实测确认「安全区」**不是**「安全设计区」的子串（安全·**设·计**·区），
        /// 所以"不含裸简称"这条**否定断言不自嵌套**（原先写的"计数比对"是多余且错的）。</summary>
        [Test]
        public void TK4_TermIsAlwaysFullForm()
        {
            Assert.That(ScaleFit.TryRecommendMatch(BuiltIn, Reference, Dpi, ScaleFit.Objective.MinWorstCrop,
                                                   out float t, out ScaleFit.ModeStats stats), Is.True);

            var texts = new List<string>
            {
                ScaleFit.ModeLine(stats, t),
                ScaleFit.RecommendationText(stats, t, ScaleFit.Objective.MinWorstCrop),
            };
            foreach (ScaleFit.ModeStats m in ScaleFit.AcrossModes(BuiltIn, Reference, Match, Dpi))
                texts.Add(ScaleFit.ModeLine(m, Match));

            foreach (string text in texts)
            {
                Assert.That(text, Does.Contain("安全设计区"), "每一行都必须出现**全称**：「" + text + "」");
                Assert.That(text, Does.Not.Contain("安全区"), "出现了裸简称：「" + text + "」");
            }
        }

        /// <summary>目标文案逐字、两个值**互不相同**；**未知枚举值不静默产出空串**。</summary>
        [Test]
        public void TK5_ObjectiveLabel_Verbatim_AndNeverEmpty()
        {
            Assert.That(ScaleFit.TryRecommendMatch(BuiltIn, Reference, Dpi, ScaleFit.Objective.MinWorstCrop,
                                                   out float t, out ScaleFit.ModeStats stats), Is.True);

            string byCrop = ScaleFit.RecommendationText(stats, t, ScaleFit.Objective.MinWorstCrop);
            string bySpare = ScaleFit.RecommendationText(stats, t, ScaleFit.Objective.MinWorstSpare);
            Assert.That(byCrop, Does.Contain("目标：最坏裁切最小"), "裁切目标逐字");
            Assert.That(bySpare, Does.Contain("目标：最坏留白最小"), "留白目标逐字");
            Assert.That(byCrop, Is.Not.EqualTo(bySpare), "两个目标函数的文案必须不同");

            // 未知值：如实打枚举名（`(Objective)999` ⇒ "999"），**不许是空串**
            Assert.That(ScaleFit.RecommendationText(stats, t, (ScaleFit.Objective)999), Does.Contain("目标：999"));
        }

        /// <summary>取正则命中的**原文**（两行文本形态相同的字段用它逐字比对）。</summary>
        private static string Group(string text, string pattern)
        {
            System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(text, pattern);
            return m.Success ? m.Value : string.Empty;
        }

        /// <summary>取正则**第 1 个捕获组**（标签不同、只有数值可比的两个字段用它）。</summary>
        private static string Num(string text, string pattern)
        {
            System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(text, pattern);
            return m.Success && m.Groups.Count > 1 ? m.Groups[1].Value : string.Empty;
        }

    }
}
