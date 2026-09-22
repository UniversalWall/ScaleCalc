using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Wayward.ScaleCalc.Editor;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// **档位清单文件的拒绝面**：坏输入矩阵十条、闸④ 的行号口径、以及"数据里的 <c>#</c> 不许被剥掉"
    /// 这组反例。
    /// <para>职责不同：合法输入照契约产出什么在 <see cref="ScaleCalcTierListFileTests"/>；
    /// 本文件只管"**坏输入怎么被拒、理由指到哪一行**"。</para>
    /// <para>⚠️ 期望理由带 <c>（该行：…）</c> 后缀、行号是**文件物理行号**（= 注释头 5 行 + 载荷内行号）。</para>
    /// </summary>
    public sealed class ScaleCalcTierListFileRejectTests
    {
        /// <summary>
        /// **坏输入矩阵（10 条 = 文件层 3 + 载荷层 7）**：逐条"拒绝 + 理由含行号"，且**一个候选都不产出**
        /// （调用方据此保证"坏输入零副作用"）。载荷层那 7 条措辞来自 `TryDecode` 的封闭清单，**一个字没改**。
        /// </summary>
        [Test]
        public void XK3_BadInputMatrix_TenCases_AllRejectedWithALocatedReason_AndNoCandidate()
        {
            string tooLong = ScreenTierStorage.FormatVersion + "\n" + new string('x', ScreenTierStorage.MaxPayloadLength + 1);
            var cases = new List<KeyValuePair<string, string>>
            {
                // 文件层 3 条
                Case("# 只有注释与空行\n\n", ScreenTierListFile.NoDataMessage),
                Case(tooLong, "文件太大（数据部分 " + tooLong.Length + " 字符 > " + ScreenTierStorage.MaxPayloadLength + "）"),
                Case(DuplicatePayload(), "第 7 行与第 9 行标识重复「c:1」"),
                // 载荷层 7 条
                Case("v2\n", "版本不认"),
                Case("v1\n只有一列\n", "第 7 行字段数不对（该行：只有一列）"),
                Case("v1\n\t甲\t1920\t1080\t1\n", "第 7 行缺标识（该行：\t甲\t1920\t1080\t1）"),
                Case("v1\nc:aaaaaaaa\t甲\t宽\t高\t1\n", "第 7 行尺寸不是数（该行：c:aaaaaaaa\t甲\t宽\t高\t1）"),
                Case("v1\nc:aaaaaaaa\t甲\t0\t900\t1\n", "第 7 行尺寸越界（该行：c:aaaaaaaa\t甲\t0\t900\t1）"),
                Case("v1\nc:aaaaaaaa\t甲\t1920\t1080\t2\n", "第 7 行勾选态无法识别（该行：c:aaaaaaaa\t甲\t1920\t1080\t2）"),
                Case(TooManyCustomRows(), "自定义行超上限 " + ScreenTierSet.MaxCustomTiers),
            };

            foreach (KeyValuePair<string, string> one in cases)
            {
                string text = ScreenTierListFile.CommentHeader() + one.Key;   // 带上注释头 ⇒ 行号必须换算
                Assert.That(ScreenTierListFile.TryParseFile(text, out List<ScreenTier> tiers, out string reason), Is.False, Show(one.Key));
                Assert.That(tiers, Is.Null, "拒绝时不许留下半份候选：" + Show(one.Key));
                Assert.That(reason, Is.EqualTo(one.Value), "理由（含行号与原文）：" + Show(one.Key));
            }
        }

        /// <summary>
        /// 闸④ 的行号口径：**数据行里的空行会被 <c>TryDecode</c> 跳过，但仍占物理行号** ⇒ 两处行号必须按
        /// "载荷里第几条非空行"去数，而不是按下标数。数错就等于把用户指到别人的那一行。
        /// </summary>
        [Test]
        public void XK3_DuplicateIds_ReportPhysicalLineNumbers_EvenWithBlankLinesInsideThePayload()
        {
            string withBlank = ScreenTierStorage.FormatVersion
                + "\nc:1\t甲\t1920\t1080\t1\n\nc:1\t丙\t1280\t720\t0\n";
            string text = ScreenTierListFile.CommentHeader() + withBlank;

            Assert.That(ScreenTierListFile.TryParseFile(text, out List<ScreenTier> tiers, out string reason), Is.False);
            Assert.That(tiers, Is.Null);
            Assert.That(reason, Is.EqualTo("第 7 行与第 9 行标识重复「c:1」"),
                "空行占物理行号、但不占数据行序号：第二个重复项在载荷第 4 行 = 文件第 9 行");
        }

        /// <summary>
        /// 的**反例**：名字里带 <c>#</c> / 制表符 / 换行 **不许**被当成注释剥掉——数据行行首永远是 <c>Id</c>
        /// （<c>builtin:</c> / <c>c:</c>，永不含 <c>#</c>），所以"以 <c>#</c> 开头"只可能是注释。
        /// <para>🔴 末尾那条钉住**全文过滤**：数据之后加的 <c>#</c> 行**也**被跳过 ——
        /// 这才是注释头里那句"以 # 开头的行只是说明，导入时会被跳过"对使用者的承诺。
        /// （原实现是**只剥开头连续段**，会把中间注释判成「字段数不对」；已按裁定改成全文过滤。）</para>
        /// </summary>
        [Test]
        public void XK3_DataRowsHoldingHashOrTabs_AreNotEatenByTheCommentStripper()
        {
            var set = new ScreenTierSet(new List<ScreenTier>
            {
                new ScreenTier("c:aaaaaaa1", "#1 屏", new ScaleSize(1920f, 1080f), ScreenTierKind.Custom, true),
                new ScreenTier("builtin:内置档", "带\t制表符\n和换行", new ScaleSize(1280f, 720f), ScreenTierKind.BuiltIn, false),
            });
            string text = ScreenTierListFile.Describe(set);

            Assert.That(ScreenTierListFile.TryParseFile(text, out List<ScreenTier> tiers, out string reason), Is.True, reason);
            Assert.That(tiers.Count, Is.EqualTo(2));
            Assert.That(tiers[0].Name, Is.EqualTo("#1 屏"), "名字以 # 开头不会被误剥（它在第 2 列，不在行首）");
            Assert.That(tiers[1].Name, Is.EqualTo("带\t制表符\n和换行"), "转义进出各一次，必须原样回来");

            Assert.That(ScreenTierListFile.TryParseFile(text + "# 中间注释\n", out List<ScreenTier> commented, out string tail), Is.True, tail);
            Assert.That(commented.Count, Is.EqualTo(2), "任意位置的 # 行都被跳过 ⇒ 档数不变（全文过滤）");

            // 🔴 实测边界：`Encode` **只转义名字、不转义 Id**（Id 由机器按 `builtin:<档名>` / `c:<8 位十六进制>` 造，
            //    本来就含不了分隔符）⇒ 手改出一个带制表符的 Id 会多切一列。要求是**响亮拒绝**（而不是静默错位）：
            var badId = new ScreenTierSet(new List<ScreenTier>
            {
                new ScreenTier("c:带\t制表符", "甲", new ScaleSize(1920f, 1080f), ScreenTierKind.Custom, true),
            });
            Assert.That(ScreenTierListFile.TryParseFile(ScreenTierListFile.Describe(badId), out _, out string badIdReason), Is.False);
            Assert.That(badIdReason, Does.Contain("字段数不对"), "Id 里出现分隔符 ⇒ 靠字段数判据响亮拒绝");
        }

        private static KeyValuePair<string, string> Case(string payload, string reason)
            => new KeyValuePair<string, string>(payload, reason);

        private static string Show(string payload) => "载荷「" + payload.Replace("\n", "\\n").Replace("\t", "\\t") + "」";

        private static string DuplicatePayload()
            => ScreenTierStorage.FormatVersion + "\nc:1\t甲\t1920\t1080\t1\nc:2\t乙\t1600\t900\t1\nc:1\t丙\t1280\t720\t0\n";

        private static string TooManyCustomRows()
        {
            var sb = new StringBuilder(ScreenTierStorage.FormatVersion).Append('\n');
            for (int i = 0; i <= ScreenTierSet.MaxCustomTiers; i++)
                sb.Append("c:").Append(i.ToString("x8")).Append("\t第").Append(i).Append("档\t1600\t900\t1\n");
            return sb.ToString();
        }
    }
}
