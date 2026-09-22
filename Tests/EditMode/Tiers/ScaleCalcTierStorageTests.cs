using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Wayward.ScaleCalc.Editor;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// **工作集的载荷与容错**（验收 `K5`）：编解码往返、**坏载荷整份回退且不抛异常**、落盘失败如实报告。
    /// <para>拆的判据 = 200 行红线 + 职责不同：本文件管**载荷本身**；"写到哪个键、会不会往工程里落文件"
    /// 在 `ScaleCalcTierStoreTests`。</para>
    /// <para>⚠️ 全程用假存储 ⇒ 不依赖真 `EditorPrefs`，也就测得到"坏载荷怎么处理"（实现册 §2.1 的理由）。</para>
    /// </summary>
    public sealed class ScaleCalcTierStorageTests
    {
        /// <summary>`K5`：存入后按同一套读取逻辑重建 ⇒ 条目、顺序、勾选态**逐项一致**。</summary>
        [Test]
        public void K5_RoundTrip_PreservesOrder_Flags_AndEscapedNames()
        {
            var storage = new FakeStorage();
            ScreenTierSet set = ScreenTierSet.Default();
            set.SetIncluded(set.Tiers[2].Id, false);
            Assert.That(set.TryAdd(new ScaleSize(1600f, 900f), "带\t制表符\n和换行\\反斜杠", out ScreenTier custom, out string error),
                Is.True, error);

            Assert.That(ScreenTierStorage.TrySave(set, storage, out string message), Is.True, message);
            ScreenTierSet back = ScreenTierStorage.LoadTiers(storage, out string fallback);

            Assert.That(fallback, Is.Null, "好载荷不该回退");
            AssertSameShape(set, back);
            Assert.That(back.Tiers[set.IndexOf(custom.Id)].Name, Is.EqualTo("带\t制表符\n和换行\\反斜杠"),
                "转义必须原样往返（一个制表符就能把整份载荷写坏）");
            Assert.That(back.IncludedCount, Is.EqualTo(set.IncludedCount), "勾选态一致");
        }

        /// <summary>
        /// `K5`：**每一种坏载荷都整份回退到内置 7 档、不抛异常**，且理由逐字可读（`R-P4`：恶数据不许让窗口打不开）。
        /// <para>理由清单是**封闭的 7 条**（实现册 §2.3），本用例逐条覆盖。</para>
        /// </summary>
        [Test]
        public void K5_CorruptPayloads_AllFallBackToBuiltInDefaults_WithAReadableReason()
        {
            var cases = new List<KeyValuePair<string, string>>
            {
                Case("v2\n", "版本不认"),
                Case("", "版本不认"),
                Case("v1\n只有一列\n", "第 2 行字段数不对"),
                Case("v1\nc:abcd1234\t甲\t宽\t高\t1\n", "第 2 行尺寸不是数"),
                Case("v1\nc:abcd1234\t甲\t0\t900\t1\n", "第 2 行尺寸越界"),
                Case("v1\nc:abcd1234\t甲\tNaN\t1080\t1\n", "第 2 行尺寸越界"),
                Case("v1\nc:abcd1234\t甲\t1920\t1080\t2\n", "第 2 行勾选态无法识别"),
                Case("v1\n\t甲\t1920\t1080\t1\n", "第 2 行缺标识"),
                Case(TooManyCustomRows(), "自定义行超上限 32"),
            };

            foreach (KeyValuePair<string, string> one in cases)
            {
                var storage = new FakeStorage { Payload = one.Key, HasKey = true };
                ScreenTierSet set = null;
                Assert.DoesNotThrow(() => set = ScreenTierStorage.LoadTiers(storage, out _), "坏载荷不许抛异常：" + Show(one.Key));
                Assert.That(set.Tiers.Count, Is.EqualTo(ScreenProfiles.All.Length), "必须整份回退到内置 7 档：" + Show(one.Key));
                Assert.That(set.IncludedCount, Is.EqualTo(ScreenProfiles.All.Length), "回退后的初值 = 全部纳入");

                ScreenTierStorage.LoadTiers(storage, out string fallback);
                Assert.That(fallback, Is.EqualTo(one.Value), "回退理由：" + Show(one.Key));
                Assert.That(ScreenTierStorage.DescribeFallback(fallback),
                    Is.EqualTo("工作集配置无法识别（" + one.Value + "），已回到内置 7 档"), "给使用者看的整句（§五）");
            }
        }

        /// <summary>`K5`：**没存过**不是错误——返回内置初值且**不给回退理由**（否则状态栏会喊冤）。</summary>
        [Test]
        public void K5_NoStoredPayload_MeansDefaultsWithoutAFallbackNotice()
        {
            ScreenTierSet set = ScreenTierStorage.LoadTiers(new FakeStorage(), out string reason);
            Assert.That(reason, Is.Null, "没存过 ⇒ 不是回退，不许报回退");
            Assert.That(set.Tiers.Count, Is.EqualTo(ScreenProfiles.All.Length));

            Assert.That(ScreenTierStorage.LoadTiers(null, out string nullReason), Is.Not.Null, "空存储也不许抛");
            Assert.That(nullReason, Is.Null);
        }

        /// <summary>
        /// 载荷里**标识重复** ⇒ 整份回落内置 7 档并**说清原因**。
        /// <para>为什么必须挡：<see cref="ScreenTierSet"/> 的构造**按 <c>Id</c> 去重、只留第一条** ⇒ 照常载入会**静默少一档**
        /// （以为 9 档、实际 8 档，界面上少一行而没人会去数）。<c>TryDecode</c> 的 7 条封闭理由**一个字没改**，
        /// 这道检查补在它**之后**。</para>
        /// </summary>
        [Test]
        public void S19_DuplicateIdsInThePayload_FallBackAndSayWhy()
        {
            string payload = ScreenTierStorage.FormatVersion
                + "\nc:1\t甲\t1920\t1080\t1\nc:2\t乙\t1600\t900\t1\nc:1\t丙\t1280\t720\t0\n";
            var storage = new FakeStorage { Payload = payload, HasKey = true };

            ScreenTierSet set = ScreenTierStorage.LoadTiers(storage, out string fallback);

            Assert.That(set.Tiers.Count, Is.EqualTo(ScreenProfiles.All.Length), "必须整份回落，而不是悄悄少一档");
            Assert.That(fallback, Is.EqualTo(ScreenTierStorage.DuplicateIdMessage("c:1")), "理由要指名那个重复的标识");
            Assert.That(ScreenTierStorage.DescribeFallback(fallback), Does.Contain("重复的档位标识"), "给使用者看的整句要能读懂");
        }

        /// <summary>`K5`：落盘失败的两条路径都**如实报告**（且都**不回滚内存**）。</summary>
        [Test]
        public void K5_SaveFailures_AreReportedVerbatim()
        {
            ScreenTierSet set = ScreenTierSet.Default();

            Assert.That(ScreenTierStorage.TrySave(set, new FakeStorage { SaveFails = true }, out string message), Is.False);
            Assert.That(message, Is.EqualTo(ScreenTierStorage.SaveFailedMessage));
            Assert.That(ScreenTierStorage.TrySave(set, null, out string nullMessage), Is.False);
            Assert.That(nullMessage, Is.EqualTo(ScreenTierStorage.SaveFailedMessage));

            Assert.That(ScreenTierStorage.TrySave(set, new FakeStorage(), out string ok), Is.True);
            Assert.That(ok, Is.Null, "成功 ⇒ 没有话要说");
        }

        /// <summary>
        /// 的长度闸门：**独立纯函数**才测得到。自定义行上限（32 行 × 名字 ≤32 字）让真实载荷
        /// **目前够不到** 8192 ⇒ 它是**纵深防御**，不是日常路径（实测后再定这个数）。
        /// </summary>
        [Test]
        public void K5_PayloadLengthGate_IsTestable_AndCurrentlyOutOfReach()
        {
            Assert.That(ScreenTierStorage.IsPayloadTooLarge(new string('x', ScreenTierStorage.MaxPayloadLength)), Is.False);
            Assert.That(ScreenTierStorage.IsPayloadTooLarge(new string('x', ScreenTierStorage.MaxPayloadLength + 1)), Is.True);
            Assert.That(ScreenTierStorage.IsPayloadTooLarge(null), Is.False);

            var worst = new ScreenTierSet(new List<ScreenTier>
            {
                new ScreenTier("builtin:" + new string('长', 32), new string('\t', 32),
                               new ScaleSize(3.40282347e38f, 3.40282347e38f), ScreenTierKind.BuiltIn, true),
            });
            Assert.That(ScreenTierStorage.IsPayloadTooLarge(ScreenTierStorage.Encode(worst)), Is.False,
                "实测最坏单行仍远低于上限 ⇒ 闸门只在防御意义上存在");
        }

        private static KeyValuePair<string, string> Case(string payload, string reason)
            => new KeyValuePair<string, string>(payload, reason);

        /// <summary>把换行/制表符显示出来，失败信息才读得懂。</summary>
        private static string Show(string payload) => "载荷「" + payload.Replace("\n", "\\n").Replace("\t", "\\t") + "」";

        /// <summary>33 条自定义行（越上限 1 条）——上限判据必须整份回退，**不许静默截到 32**。</summary>
        private static string TooManyCustomRows()
        {
            var sb = new StringBuilder(ScreenTierStorage.FormatVersion).Append('\n');
            for (int i = 0; i <= ScreenTierSet.MaxCustomTiers; i++)
                sb.Append("c:").Append(i.ToString("x8")).Append("\t第").Append(i).Append("行\t1600\t900\t1\n");
            return sb.ToString();
        }

        /// <summary>逐项比对（条目数、顺序、五个字段）——只比"存了什么"，不比对象身份。</summary>
        private static void AssertSameShape(ScreenTierSet expected, ScreenTierSet actual)
        {
            Assert.That(actual.Tiers.Count, Is.EqualTo(expected.Tiers.Count), "条目数必须一致");
            for (int i = 0; i < expected.Tiers.Count; i++)
            {
                ScreenTier e = expected.Tiers[i], a = actual.Tiers[i];
                Assert.That(a.Id, Is.EqualTo(e.Id), "第 " + i + " 条 Id");
                Assert.That(a.Name, Is.EqualTo(e.Name), "第 " + i + " 条 Name");
                Assert.That(a.Size, Is.EqualTo(e.Size), "第 " + i + " 条 Size");
                Assert.That(a.Kind, Is.EqualTo(e.Kind), "第 " + i + " 条 Kind");
                Assert.That(a.Included, Is.EqualTo(e.Included), "第 " + i + " 条 Included");
            }
        }

        /// <summary>假存储：`K5` 要测"坏载荷怎么处理"，只测 `EditorPrefs` 往返是测不到的（实现册 §2.1）。</summary>
        private sealed class FakeStorage : IScreenTierStorage
        {
            public string Payload;
            public bool HasKey;
            public bool SaveFails;

            public bool TryLoad(out string payload)
            {
                payload = Payload;
                return HasKey;
            }

            public bool TrySave(string payload)
            {
                if (SaveFails) return false;
                Payload = payload;
                HasKey = true;
                return true;
            }

            public void Delete()
            {
                Payload = null;
                HasKey = false;
            }
        }
    }
}
