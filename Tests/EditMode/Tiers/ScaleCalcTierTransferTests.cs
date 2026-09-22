using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using Wayward.ScaleCalc.Editor;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// **档位清单文件的导入 / 导出编排**：不建目录 · 不静默覆盖 · **不带 BOM** ·
    /// 默认落点退化口径，以及"32 档能往返、33 档被响亮拒绝"。
    /// <para>落点全部在 <c>Path.GetTempPath()</c> 下的临时目录，**不碰仓库**（破坏性动作只在自己沙箱内）。</para>
    /// <para>⚠️ 本类**不做工作集替换、不碰窗口、不碰场景**——那是 <c>ScaleCalcWindow.Transfer.cs</c> 与
    /// <see cref="ScaleCalcTierTransferSceneTests"/> 的事。这里只回答"文件读写成不成、纪律守没守住"。</para>
    /// </summary>
    public sealed class ScaleCalcTierTransferTests
    {
        private static string NewTempRoot()
            => Path.Combine(Path.GetTempPath(), "scalecalc-tier-" + Guid.NewGuid().ToString("N"));

        /// <summary>
        /// 父目录不存在 ⇒ 抛可读异常，且**磁盘上什么都不多**。断言口径 = 跑前跑后**整个临时根都不存在**
        /// （<c>Directory.CreateDirectory</c> 连中间层都会造，所以不能只查"目标目录没有"）。
        /// </summary>
        [Test]
        public void XK6_ExportTo_MissingParentDirectory_RefusesAndCreatesNothing()
        {
            string root = NewTempRoot();
            string path = Path.Combine(root, "not-there", ScreenTierTransfer.DefaultFileName);
            try
            {
                Assert.That(Directory.Exists(root), Is.False, "前置：临时根一开始就不存在");

                var ex = Assert.Throws<DirectoryNotFoundException>(
                    () => ScreenTierTransfer.ExportTo(ScreenTierSet.Default(), path, true),
                    "父目录不存在时应当抛可读异常，而不是替调用方造目录");

                Assert.That(ex.Message, Does.Contain("目录不存在"), "异常消息要能读出「目录不存在」");
                Assert.That(Directory.Exists(root), Is.False, "拒绝路径零副作用：连临时根都不许被造出来");
                Assert.That(File.Exists(path), Is.False);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        /// <summary>目标已存在且未获许可 ⇒ 拒绝，且**文件逐字节未变**（内容 + 写入时间两条互证）。</summary>
        [Test]
        public void XK6_ExportTo_ExistingFileWithoutPermission_LeavesTheBytesUntouched()
        {
            string root = NewTempRoot();
            string path = Path.Combine(root, "已有清单.txt");
            try
            {
                Directory.CreateDirectory(root);
                const string original = "原有内容，一个字节都不许动\n";
                File.WriteAllText(path, original, new UTF8Encoding(false));
                DateTime before = File.GetLastWriteTimeUtc(path);

                Assert.Throws<IOException>(
                    () => ScreenTierTransfer.ExportTo(ScreenTierSet.Default(), path, false),
                    "已存在且未获许可 ⇒ 必须如实拒绝");

                Assert.That(File.ReadAllText(path, new UTF8Encoding(false)), Is.EqualTo(original), "文件内容必须逐字节未变");
                Assert.That(File.GetLastWriteTimeUtc(path), Is.EqualTo(before), "连写入时间都不该变");
                Assert.That(Directory.GetFiles(root).Length, Is.EqualTo(1), "没有多出临时 / 备份文件");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        /// <summary>
        /// 正向：获许可 ⇒ 真的写盘、返回实际路径、开头就是注释头，且**不带 BOM**
        /// （这份文件要进版本库、要被人手编辑，多出的 3 个字节会被 git 当内容差异）。
        /// <para>顺带钉住落点口径：有工程根 ⇒ 根下；**没有工程根 ⇒ 只给文件名**（消费者工程里也不能崩）。</para>
        /// </summary>
        [Test]
        public void XK6_ExportTo_WithPermission_WritesUtf8WithoutBom_AndDefaultPathStaysUnderTheRoot()
        {
            string root = NewTempRoot();
            string path = Path.Combine(root, ScreenTierTransfer.DefaultFileName);
            try
            {
                Directory.CreateDirectory(root);
                string returned = ScreenTierTransfer.ExportTo(SampleSet(), path, true);

                Assert.That(returned, Is.EqualTo(path), "返回实际写入路径（窗口拿它拼成功文案）");
                Assert.That(File.Exists(path), Is.True);

                byte[] head = File.ReadAllBytes(path);
                Assert.That(head.Length, Is.GreaterThan(3));
                Assert.That(head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF, Is.False,
                    "清单文件不许带 BOM");
                Assert.That(File.ReadAllText(path), Does.StartWith(ScreenTierListFile.FileTitle), "文件开头就是注释头");

                Assert.That(ScreenTierTransfer.DefaultFileName, Is.EqualTo("ScaleCalc-屏幕档位清单.txt"), "默认文件名逐字");
                Assert.That(ScreenTierTransfer.DefaultPath("/proj"), Is.EqualTo(Path.Combine("/proj", ScreenTierTransfer.DefaultFileName)));
                Assert.That(ScreenTierTransfer.DefaultPath(null), Is.EqualTo(ScreenTierTransfer.DefaultFileName), "没有工程根 ⇒ 只给文件名");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        /// <summary>
        /// 的 IO 侧：**32 个自定义档**经文件完整往返（这是界面上真能造出来的极限）；
        /// **33 个**在界面上加不出来，但文件可以被人手改出来、被 git 合并出来 ⇒ 导入必须**响亮拒绝**，
        /// 而不是静默按 32 截断。
        /// </summary>
        [Test]
        public void XK7_ThirtyTwoCustomTiers_RoundTripThroughTheFile_AndThirtyThreeAreRejectedOnImport()
        {
            string root = NewTempRoot();
            try
            {
                Directory.CreateDirectory(root);

                string path32 = Path.Combine(root, "32.txt");
                ScreenTierTransfer.ExportTo(new ScreenTierSet(CustomRows(ScreenTierSet.MaxCustomTiers)), path32, true);
                Assert.That(ScreenTierTransfer.TryImport(path32, out List<ScreenTier> tiers, out string why), Is.True, why);
                Assert.That(tiers.Count, Is.EqualTo(ScreenTierSet.MaxCustomTiers), "极限集必须完整往返");
                Assert.That(tiers[ScreenTierSet.MaxCustomTiers - 1].Name, Is.EqualTo("第 " + (ScreenTierSet.MaxCustomTiers - 1) + " 档"));
                Assert.That(tiers[0].Kind, Is.EqualTo(ScreenTierKind.Custom), "自定义性由 Id 前缀派生");

                string path33 = Path.Combine(root, "33.txt");
                ScreenTierTransfer.ExportTo(new ScreenTierSet(CustomRows(ScreenTierSet.MaxCustomTiers + 1)), path33, true);
                Assert.That(ScreenTierTransfer.TryImport(path33, out List<ScreenTier> none, out string reason), Is.False);
                Assert.That(none, Is.Null, "拒绝时不许交出半份候选 ⇒ 调用方无从替换");
                Assert.That(reason, Is.EqualTo("自定义行超上限 " + ScreenTierSet.MaxCustomTiers));

                Assert.That(ScreenTierTransfer.TryImport(null, out _, out string noPath), Is.False, "没选文件不是异常");
                Assert.That(noPath, Is.EqualTo(ScreenTierTransfer.NoPathMessage));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static ScreenTierSet SampleSet()
        {
            ScreenTierSet set = ScreenTierSet.Default();
            set.SetIncluded(set.Tiers[0].Id, false);
            return set;
        }

        private static List<ScreenTier> CustomRows(int count)
        {
            var rows = new List<ScreenTier>();
            for (int i = 0; i < count; i++)
                rows.Add(new ScreenTier("c:" + i.ToString("x8"), "第 " + i + " 档", new ScaleSize(1600f + i, 900f), ScreenTierKind.Custom, true));
            return rows;
        }
    }
}
