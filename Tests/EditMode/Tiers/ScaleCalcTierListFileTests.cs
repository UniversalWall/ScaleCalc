using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Wayward.ScaleCalc.Editor;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// **档位清单文件的格式契约**：往返与逐字节稳定、剥注释与载荷**同源**、
    /// 上限边界、长度闸门只算数据部分、注释头逐字。
    /// <para>职责不同：本文件管"**合法输入照契约产出什么**"；
    /// "**坏输入怎么被拒、理由指到哪一行**"在 <see cref="ScaleCalcTierListFileRejectTests"/>。</para>
    /// <para>全程纯函数 ⇒ 不开窗、不落盘、不碰场景。IO 编排与落盘纪律在 <see cref="ScaleCalcTierTransferTests"/>。</para>
    /// </summary>
    public sealed class ScaleCalcTierListFileTests
    {
        /// <summary>
        /// 一个"什么都有"的工作集 ⇒ 导出 → 解析 ⇒ **五个字段逐条一致**；并且**导出两次逐字节相同**、
        /// **导入后再导出仍逐字节相同**（这份文件要进版本库，抖动一个字节就是一行假差异）。
        /// </summary>
        [Test]
        public void XK1_DescribeThenParse_RoundTripsEveryField_AndIsByteStable()
        {
            ScreenTierSet set = SampleSet();
            string text = ScreenTierListFile.Describe(set);

            Assert.That(ScreenTierListFile.TryParseFile(text, out List<ScreenTier> tiers, out string reason), Is.True, reason);
            AssertSameShape(set.Tiers, tiers);

            Assert.That(ScreenTierListFile.Describe(set), Is.EqualTo(text), "同一工作集导出两次必须逐字节相同");
            Assert.That(ScreenTierListFile.Describe(new ScreenTierSet(tiers)), Is.EqualTo(text), "导入后再导出仍须逐字节相同");
        }

        /// <summary>**同源**：剥掉注释头之后必须与 <see cref="ScreenTierStorage.Encode"/> **逐字节相同**。</summary>
        [Test]
        public void XK2_StrippingTheCommentHeader_LeavesExactlyThePayload()
        {
            string payload = ScreenTierStorage.Encode(SampleSet());
            string header = ScreenTierListFile.CommentHeader();

            Assert.That(header, Does.StartWith(ScreenTierListFile.FileTitle), "注释头第一行就是标题");
            Assert.That(ScreenTierListFile.StripCommentHeader(header + payload), Is.EqualTo(payload), "剥注释后必须逐字节等于载荷");
            Assert.That(ScreenTierListFile.StripCommentHeader("\n\n" + header + payload), Is.EqualTo(payload), "开头的空行也属于注释段");
            Assert.That(ScreenTierListFile.StripCommentHeader(header), Is.EqualTo(string.Empty), "只剩注释 ⇒ 数据部分为空");
            Assert.That(ScreenTierListFile.StripCommentHeader(null), Is.EqualTo(string.Empty), "空输入不抛异常");
        }

        /// <summary>
        /// 上限边界：**32 档能过、33 档被拒**，且拒绝理由就是载荷层那一条（措辞不改、不带行号）。
        /// <para>33 档在界面上**加不出来**（`TryAdd` 有上限）⇒ 这里造的是"纵深"场景：文件可以被人手改出来、
        /// 被 git 合并出来，所以必须响亮拒绝，而不是静默按 32 截断。</para>
        /// </summary>
        [Test]
        public void XK7_ThirtyTwoCustomTiers_Pass_AndThirtyThreeAreRejectedByThePayloadLayer()
        {
            string text32 = ScreenTierListFile.Describe(new ScreenTierSet(CustomRows(ScreenTierSet.MaxCustomTiers)));
            Assert.That(ScreenTierListFile.TryParseFile(text32, out List<ScreenTier> tiers, out string ok), Is.True, ok);
            Assert.That(tiers.Count, Is.EqualTo(ScreenTierSet.MaxCustomTiers), "极限集必须完整往返");
            Assert.That(tiers[ScreenTierSet.MaxCustomTiers - 1].Name, Is.EqualTo("第 " + (ScreenTierSet.MaxCustomTiers - 1) + " 档"));

            string text33 = ScreenTierListFile.Describe(new ScreenTierSet(CustomRows(ScreenTierSet.MaxCustomTiers + 1)));
            Assert.That(ScreenTierListFile.TryParseFile(text33, out List<ScreenTier> none, out string reason), Is.False);
            Assert.That(none, Is.Null, "拒绝时不许留下半份候选");
            Assert.That(reason, Is.EqualTo("自定义行超上限 " + ScreenTierSet.MaxCustomTiers), "这一条没有行号可换算 ⇒ 原样转述");
        }

        /// <summary>
        /// 闸② 的作用对象：**只算数据部分**。注释头若计入，上限就会随说明书的字数漂移。
        /// <para>构造方式：把数据部分填到"刚好不超"，让**整份文件**一定超 ⇒ 必须仍然通过；再加一行就必须拒。</para>
        /// </summary>
        [Test]
        public void XK3_TheLengthGate_CountsTheDataPartOnly_NotTheCommentHeader()
        {
            string row = FillerRow(0);
            var sb = new StringBuilder(ScreenTierStorage.FormatVersion).Append('\n');
            while (sb.Length + row.Length <= ScreenTierStorage.MaxPayloadLength) sb.Append(FillerRow(sb.Length));

            string payload = sb.ToString();
            string whole = ScreenTierListFile.CommentHeader() + payload;

            Assert.That(ScreenTierStorage.IsPayloadTooLarge(payload), Is.False, "前置：数据部分本身没超");
            Assert.That(whole.Length, Is.GreaterThan(ScreenTierStorage.MaxPayloadLength), "前置：整份文件确实超了（注释头约 260 字）");
            Assert.That(ScreenTierListFile.TryParseFile(whole, out List<ScreenTier> tiers, out string ok), Is.True,
                "闸门只看数据部分，注释头不计入：" + ok);
            Assert.That(tiers.Count, Is.GreaterThan(0));

            Assert.That(ScreenTierListFile.TryParseFile(whole + FillerRow(999999), out _, out string reason), Is.False);
            Assert.That(reason, Does.StartWith("文件太大（数据部分 "), "超了就要报，且报的是数据部分的长度");
        }

        /// <summary>
        /// 的文案面：注释头是**逐字契约**，且**绝不含时间戳 / 机器名**——
        /// 否则"导出两次逐字节相同"会随天、随机器抖动。
        /// </summary>
        [Test]
        public void XK9_CommentHeader_IsFiveVerbatimLines_WithoutTimestampOrMachineName()
        {
            string expected = ScreenTierListFile.FileTitle + "\n"
                + "# 用途：与同事共享 / 入版本库；导入时会**整体替换**当前工作集（本机 EditorPrefs 用的是同一份数据）\n"
                + "# 格式：首行 v1；此后一行一档，制表符分列 —— 标识 · 名字 · 宽 · 高 · 是否参与计算(1/0)\n"
                + "# 可手工编辑：以 # 开头的行只是说明，导入时会被跳过；数据行改完直接导入即可\n"
                + "# 上限：自定义行 ≤ " + ScreenTierSet.MaxCustomTiers + " · 名字 ≤ " + ScreenTierSet.MaxNameLength + " 字（超限整份拒绝，不截断）\n";

            Assert.That(ScreenTierListFile.CommentHeader(), Is.EqualTo(expected), "注释头是逐字契约");
            Assert.That(ScreenTierListFile.CommentHeader(), Does.Not.Contain(DateTime.Now.Year.ToString()), "不许带日期");
            Assert.That(ScreenTierListFile.CommentHeader(), Does.Not.Contain(Environment.MachineName), "不许带机器名");
            Assert.That(ScreenTierListFile.Describe(null), Is.EqualTo(expected + "v1\n"), "null 工作集 ⇒ 合法的空清单（导入它 = 清空）");
        }

        /// <summary>样例工作集：内置 7 档（取消勾选第 3 档）+ 2 个自定义（一个未勾选），名字带 `\t`/`\n`/`#`/引号。</summary>
        private static ScreenTierSet SampleSet()
        {
            ScreenTierSet set = ScreenTierSet.Default();
            set.SetIncluded(set.Tiers[2].Id, false);

            Assert.That(set.TryAdd(new ScaleSize(1600f, 900f), "带\t制表符\n和换行", out ScreenTier first, out string e1), Is.True, e1);
            set.SetIncluded(first.Id, false);
            Assert.That(set.TryAdd(new ScaleSize(1280f, 720f), "#1 屏 \"引号\"", out ScreenTier second, out string e2), Is.True, e2);
            Assert.That(second.Id, Is.Not.EqualTo(first.Id));
            return set;
        }

        private static List<ScreenTier> CustomRows(int count)
        {
            var rows = new List<ScreenTier>();
            for (int i = 0; i < count; i++)
                rows.Add(new ScreenTier("c:" + i.ToString("x8"), "第 " + i + " 档", new ScaleSize(1600f + i, 900f), ScreenTierKind.Custom, true));
            return rows;
        }

        /// <summary>定长填充行（合法内置行）：用来把数据部分精确顶到 8192 附近。</summary>
        private static string FillerRow(int index)
            => "builtin:" + index.ToString("D8") + new string('x', 110) + "\t名\t1920\t1080\t1\n";

        private static void AssertSameShape(IReadOnlyList<ScreenTier> expected, List<ScreenTier> actual)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Count), "条目数必须一致");
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.That(actual[i].Id, Is.EqualTo(expected[i].Id), "第 " + i + " 条 Id");
                Assert.That(actual[i].Name, Is.EqualTo(expected[i].Name), "第 " + i + " 条 Name");
                Assert.That(actual[i].Size, Is.EqualTo(expected[i].Size), "第 " + i + " 条 Size");
                Assert.That(actual[i].Kind, Is.EqualTo(expected[i].Kind), "第 " + i + " 条 Kind");
                Assert.That(actual[i].Included, Is.EqualTo(expected[i].Included), "第 " + i + " 条 Included");
            }
        }
    }
}
