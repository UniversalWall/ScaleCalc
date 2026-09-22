using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Wayward.ScaleCalc.Editor;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// **工作集数据层**：纯逻辑，**不开窗、不碰场景**。
    /// <para>职责不同：本文件管**条目与集合怎么改**与**证据包不受影响**；
    /// 载荷编解码与坏载荷容错在 <see cref="ScaleCalcTierStorageTests"/>。</para>
    /// <para>⚠️ "证据包不受影响"这条**必然通过**（证据包只读内置清单）——它的价值是拦住
    /// "顺手把工作集接进证据包"（一条机械守卫）。</para>
    /// </summary>
    public sealed class ScaleCalcTierTests
    {
        /// <summary>`K2`：取消勾选 ⇒ 不参与计算（不进选型表），但**仍在管理表里**（`K1a`）。</summary>
        [Test]
        public void K2_UncheckedTier_DropsOutOfCalculation_ButStaysInTheManagementTable()
        {
            ScreenTierSet set = ScreenTierSet.Default();
            ScreenTier first = set.Tiers[0];
            int all = set.Tiers.Count;

            Assert.That(set.SetIncluded(first.Id, false), Is.True, "取消勾选应当真的改了东西");
            Assert.That(set.IncludedCount, Is.EqualTo(all - 1));
            Assert.That(Contains(set.IncludedTiers(), first.Id), Is.False, "K2：取消勾选 ⇒ 该条目不参与计算");
            Assert.That(Contains(set.Tiers, first.Id), Is.True, "K2/K1a：仍在管理表里，可以勾回来");
            Assert.That(set.SetIncluded(first.Id, false), Is.False, "值本来就一样 ⇒ 不算改了");
            Assert.That(set.SetIncluded("builtin:不存在", false), Is.False, "找不到 ⇒ 什么都不做");
            Assert.That(set.SetIncluded(first.Id, true), Is.True, "能勾回来");
            Assert.That(set.IncludedCount, Is.EqualTo(all));
        }

        /// <summary>的边界：**全不选**是合法状态，不报错、不自动勾回。</summary>
        [Test]
        public void K2b_EmptySelection_IsAllowed_AndIsNotAnError()
        {
            ScreenTierSet set = ScreenTierSet.Default();
            foreach (ScreenTier tier in new List<ScreenTier>(set.Tiers)) set.SetIncluded(tier.Id, false);

            Assert.That(set.IncludedCount, Is.EqualTo(0));
            Assert.That(set.IncludedTiers().Count, Is.EqualTo(0), "选型表可以为空（§四①）");
            Assert.That(set.Tiers.Count, Is.EqualTo(ScreenProfiles.All.Length), "管理表一条都不许少");
        }

        /// <summary>`K3`：内置档不可删（且**不动任何东西**），自定义行可删。</summary>
        [Test]
        public void K3_BuiltInIsNotDeletable_CustomIs()
        {
            ScreenTierSet set = ScreenTierSet.Default();
            string builtIn = set.Tiers[0].Id;
            int before = set.Tiers.Count;

            Assert.That(builtIn.StartsWith(ScreenTier.BuiltInIdPrefix, StringComparison.Ordinal), Is.True);
            Assert.That(set.CanDelete(builtIn), Is.False, "K3：内置档不可删");
            Assert.That(set.Delete(builtIn), Is.False, "K3：删除动作对内置条目必须无效");
            Assert.That(set.Tiers.Count, Is.EqualTo(before), "无效 ⇒ 连顺序都不许动");
            Assert.That(set.Tiers[0].Id, Is.EqualTo(builtIn));

            Assert.That(set.TryAdd(new ScaleSize(1600f, 900f), null, out ScreenTier custom, out string error), Is.True, error);
            Assert.That(custom.Id.StartsWith(ScreenTier.CustomIdPrefix, StringComparison.Ordinal), Is.True);
            Assert.That(set.CanDelete(custom.Id), Is.True, "自定义行可删");
            Assert.That(set.Delete(custom.Id), Is.True);
            Assert.That(Contains(set.Tiers, custom.Id), Is.False, "删掉了");
        }

        /// <summary>`K4`：新增自定义行 ⇒ 多一条，且**能算出 scaleFactor**（走内核，不特判）。</summary>
        [Test]
        public void K4_AddedCustomTier_IsComputableByTheKernel()
        {
            ScreenTierSet set = ScreenTierSet.Default();
            var size = new ScaleSize(1600f, 900f);
            Assert.That(set.TryAdd(size, null, out ScreenTier tier, out string error), Is.True, error);

            Assert.That(tier.Name, Is.EqualTo(ScreenTierSet.AutoName(size)), "空名字 ⇒ 自动名");
            Assert.That(set.CustomCount, Is.EqualTo(1));
            Assert.That(set.IncludedCount, Is.EqualTo(ScreenProfiles.All.Length + 1), "新增行初值纳入");

            ScreenProfile profile = tier.ToProfile();
            Assert.That(profile.Source, Is.EqualTo("自定义"), "来源列要如实标自定义");
            Assert.That(KernelScaleFactor(profile.Size, new ScaleSize(1920f, 1080f)), Is.GreaterThan(0f),
                "K4：新档位必须真的过内核算得出 scaleFactor");
        }

        /// <summary>的边界：自定义行上限 32，达到后**拒绝且不静默截断**。</summary>
        [Test]
        public void K4b_CustomRowsAreCappedAt32()
        {
            ScreenTierSet set = ScreenTierSet.Default();
            for (int i = 0; i < ScreenTierSet.MaxCustomTiers; i++)
                Assert.That(set.TryAdd(new ScaleSize(1000f + i, 500f), null, out _, out string e), Is.True, e);

            Assert.That(set.CustomCount, Is.EqualTo(32));
            Assert.That(set.CanAdd, Is.False);
            Assert.That(set.TryAdd(new ScaleSize(1920f, 1080f), null, out _, out string error), Is.False, "达上限必须拒绝");
            Assert.That(error, Is.EqualTo(ScreenTierSet.LimitMessage), "文案逐字");
            Assert.That(set.CustomCount, Is.EqualTo(32), "拒绝 ⇒ 一条都不许多");
        }

        /// <summary>尺寸校验与名字归一化（含截断提示、空名字不覆盖）。</summary>
        [Test]
        public void SizeValidation_And_NameNormalization()
        {
            Assert.That(ScreenTierSet.IsValidSize(new ScaleSize(1f, 1f)), Is.True);
            Assert.That(ScreenTierSet.IsValidSize(new ScaleSize(0f, 100f)), Is.False);
            Assert.That(ScreenTierSet.IsValidSize(new ScaleSize(100f, -1f)), Is.False);
            Assert.That(ScreenTierSet.IsValidSize(new ScaleSize(float.NaN, 100f)), Is.False, "非有限必须拒绝");
            Assert.That(ScreenTierSet.IsValidSize(new ScaleSize(float.PositiveInfinity, 100f)), Is.False);

            Assert.That(ScreenTierSet.NormalizeName("  名字  ", out string notice), Is.EqualTo("名字"));
            Assert.That(notice, Is.Null);

            string long32 = new string('长', 40);
            Assert.That(ScreenTierSet.NormalizeName(long32, out string cutNotice).Length, Is.EqualTo(32));
            Assert.That(cutNotice, Is.EqualTo("名字超过 32 字，已截断为「" + new string('长', 32) + "」"));

            ScreenTierSet set = ScreenTierSet.Default();
            set.TryAdd(new ScaleSize(1600f, 900f), "原名", out ScreenTier tier, out _);
            Assert.That(set.Rename(tier.Id, "  "), Is.False, "空名字不许覆盖原值");
            Assert.That(set.IndexOf(tier.Id), Is.GreaterThanOrEqualTo(0));
            Assert.That(set.Tiers[set.IndexOf(tier.Id)].Name, Is.EqualTo("原名"));
            Assert.That(set.Rename(tier.Id, "改名后"), Is.True);
            Assert.That(set.Tiers[set.IndexOf(tier.Id)].Id, Is.EqualTo(tier.Id), "改名后仍是同一行");
            Assert.That(set.Resize(tier.Id, new ScaleSize(1f, 1f)), Is.True, "合法尺寸可改");
            Assert.That(set.Resize(tier.Id, new ScaleSize(0f, 1f)), Is.False, "非法尺寸保留原值");
            Assert.That(set.Tiers[set.IndexOf(tier.Id)].Size, Is.EqualTo(new ScaleSize(1f, 1f)));
        }

        /// <summary>`K7`：**证据包不受工作集影响**——把工作集改到面目全非，产物仍**逐字节相同**。</summary>
        [Test]
        public void K7_EvidencePack_IsByteIdentical_RegardlessOfTheWorkingSet()
        {
            byte[] before = Encoding.UTF8.GetBytes(EvidencePackCommand.BuildCsv(out int rowsBefore));

            ScreenTierSet set = ScreenTierSet.Default();
            foreach (ScreenTier tier in new List<ScreenTier>(set.Tiers)) set.SetIncluded(tier.Id, false);
            Assert.That(set.TryAdd(new ScaleSize(1600f, 900f), "自定义 1600x900", out ScreenTier custom, out _), Is.True);
            Assert.That(set.Delete(custom.Id), Is.True);
            Assert.That(set.TryAdd(new ScaleSize(2560f, 1440f), "自定义 2560x1440", out _, out _), Is.True);
            Assert.That(set.IncludedCount, Is.EqualTo(1), "前置：工作集确实已经被改到面目全非");

            byte[] after = Encoding.UTF8.GetBytes(EvidencePackCommand.BuildCsv(out int rowsAfter));

            Assert.That(rowsAfter, Is.EqualTo(rowsBefore), "K7：工作集变了，证据包行数不许变");
            Assert.That(after, Is.EqualTo(before), "逐字节相同（工作集与证据包**不同源**的机械守卫）");
            Assert.That(rowsBefore, Is.EqualTo(126), "离线 = 6 组参考 × 3 个 match × 7 档");
            Assert.That(EvidencePackCommand.MaxDataRows, Is.EqualTo(127), "上限 = 上式的 126 + 1 现场行");
        }

        /// <summary>
        /// `K` 模式读数（**列定义声明的模式列个数**）：2026-09-21（《界面精简与对账状态列》**批 2**）
        /// 把原来挂在"状态栏三个数"上的那条用例改成直接钉列定义——因为**状态栏（`summary`）已整条撤除**，
        /// 而这条判据本身（"K 读列定义、不写死"）仍然有效且重要。
        /// </summary>
        [Test]
        public void KernelModeColumnCount_ComesFromTheColumnDefinitions()
        {
            Assert.That(ScaleTableColumns.KernelModeColumnCount(), Is.EqualTo(1), "收窄后只剩一个模式列");
        }

        private static bool Contains(IReadOnlyList<ScreenTier> tiers, string id)
        {
            foreach (ScreenTier tier in tiers)
                if (string.Equals(tier.Id, id, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>直接过内核（不经过任何特判路径），用于 `K4` 的"真的算得出来"。</summary>
        private static float KernelScaleFactor(ScaleSize screen, ScaleSize reference)
        {
            ScaleCalcInput input = ScaleCalcInput.Default;
            input.ScreenSize = screen;
            input.ReferenceResolution = reference;
            input.ScreenMatch = ScreenMatchMode.MatchWidthOrHeight;
            input.MatchWidthOrHeight = 0.5f;
            input.Mode = ScaleMode.ScaleWithScreenSize;
            return ScaleCalc.Evaluate(in input).ScaleFactor;
        }
    }
}
