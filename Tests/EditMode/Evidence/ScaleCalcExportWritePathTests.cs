using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Wayward.ScaleCalc.Editor;
using UnityEditor.PackageManager;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// **写入链路收紧的落盘契约**：向使用方工程写文件之前，
    /// 必须先有一个"明确的落点"——包内不选址、不造目录树、不静默覆盖。
    /// <para>与 <see cref="ScaleCalcEvidencePackTests"/>（产物内容：BOM / 行数 / 真值范围）分文件：
    /// 职责不同（那里管**内容对不对**，这里管**写不写、写到哪**）。</para>
    /// <para>落点全部用 <c>Path.GetTempPath()</c> 下的临时目录，**不碰仓库**（破坏性动作只在自己沙箱内）。</para>
    /// </summary>
    public sealed class ScaleCalcExportWritePathTests
    {
        private static string PackageRoot =>
            PackageInfo.FindForAssembly(typeof(ScaleCalcExportWritePathTests).Assembly).resolvedPath;

        private static string NewTempRoot()
            => Path.Combine(Path.GetTempPath(), "scalecalc-write-" + Guid.NewGuid().ToString("N"));

        /// <summary>
        /// **不自动建目录**：父目录不存在 ⇒ 抛可读异常，且**磁盘上不出现新目录**。
        /// <para>断言口径：跑前跑后**目录集合相同**——不是"没有那个目录"而已，
        /// 而是整个临时根下没多出任何东西（<c>Directory.CreateDirectory</c> 连中间层都会造，所以要全量比对）。</para>
        /// </summary>
        [Test]
        public void Export_DoesNotCreateMissingDirectories_AndThrows()
        {
            string root = NewTempRoot();
            string missing = Path.Combine(root, "does-not-exist");
            string path = Path.Combine(missing, "pack.csv");
            try
            {
                Assert.That(Directory.Exists(root), Is.False, "前置：临时根一开始就不存在");

                var ex = Assert.Throws<DirectoryNotFoundException>(
                    () => EvidenceExporter.Export("表头\n数据\n", path, true),
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

        /// <summary>边界：路径里**根本没有目录部分**（光一个文件名）也按"父目录不存在"拒绝，不许退化到当前工作目录。</summary>
        [Test]
        public void Export_WithNoDirectoryPart_IsRejected()
        {
            var ex = Assert.Throws<DirectoryNotFoundException>(
                () => EvidenceExporter.Export("表头\n数据\n", "just-a-name.csv", true));
            Assert.That(ex.Message, Does.Contain("目录不存在"));
        }

        /// <summary>
        /// **不静默覆盖**：目标文件已存在且未获许可 ⇒ 抛可读异常，且**文件逐字节未变**。
        /// <para>用"内容 + 最后写入时间"两条互证：只要真的写了盘，两者至少有一条会变。</para>
        /// </summary>
        [Test]
        public void Export_RefusesToOverwriteExistingFileWithoutPermission()
        {
            string root = NewTempRoot();
            string path = Path.Combine(root, "existing.csv");
            try
            {
                Directory.CreateDirectory(root);
                const string original = "原有内容,不许动\n";
                File.WriteAllText(path, original);
                DateTime before = File.GetLastWriteTimeUtc(path);

                Assert.Throws<IOException>(
                    () => EvidenceExporter.Export("新内容\n", path, false),
                    "已存在且未获许可 ⇒ 必须抛可读异常");

                Assert.That(File.ReadAllText(path), Is.EqualTo(original), "文件内容必须逐字节未变（不许静默覆盖）");
                Assert.That(File.GetLastWriteTimeUtc(path), Is.EqualTo(before), "连写入时间都不该变");
                Assert.That(Directory.GetFiles(root).Length, Is.EqualTo(1), "没有多出临时/备份文件");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        /// <summary>
        /// 覆盖许可**是**一条真开关——显式传 <c>true</c> 时确实允许覆盖。
        /// <para>否则上一条断言可能只是"这个 API 根本不写盘"造成的假通过。</para>
        /// </summary>
        [Test]
        public void Export_OverwritesWhenPermissionIsExplicitlyGiven()
        {
            string root = NewTempRoot();
            string path = Path.Combine(root, "existing.csv");
            try
            {
                Directory.CreateDirectory(root);
                File.WriteAllText(path, "旧内容\n");

                // 窗口那条路（保存对话框已获用户确认）就是这么传的
                EvidenceExporter.Export("新内容\n", path, true);

                Assert.That(File.ReadAllText(path), Does.Contain("新内容"));
                // 带 BOM 仍成立（中文 Excel 不乱码这条不能被覆盖开关改坏）
                byte[] head = File.ReadAllBytes(path);
                Assert.That(head[0], Is.EqualTo(0xEF), "UTF-8 BOM 第 1 字节");
                Assert.That(head[1], Is.EqualTo(0xBB), "UTF-8 BOM 第 2 字节");
                Assert.That(head[2], Is.EqualTo(0xBF), "UTF-8 BOM 第 3 字节");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        /// <summary>
        /// **不猜落点**的源码级判据：公开入口里**不再有**"无参导出、自行选址"的那一个。
        /// <para>纪律：目标 token 用字符拼接构造，**绝不写字面量**（注释里也不写），否则断言会命中自己。</para>
        /// <para>同时守住否命题：<c>ExportTo</c> 这个入口**必须还在**（否则本条断言会因"整个文件被删"而假通过）。</para>
        /// </summary>
        [Test]
        public void NoPublicEntrySelectsTheLandingPathOnItsOwn()
        {
            string source = File.ReadAllText(Path.Combine(PackageRoot, "Editor", "Evidence", "EvidencePackCommand.cs"));
            string noArgEntry = "public static string Export" + "();";

            Assert.That(source, Does.Not.Contain(noArgEntry),
                "包内不得再有「无参导出」这种自行选址的入口；落点只能来自对话框或调用方显式传的路径");
            Assert.That(source, Does.Contain("public static string ExportTo("), "显式落点的入口必须还在");
            Assert.That(source, Does.Contain("public static partial class EvidencePackCommand"),
                "本类按章节拆 partial（落点章节与 CSV 产出章节分开）");
        }

        /// <summary>机械守卫：拆出来的每个文件都必须在 200 行红线内（拆文件的**目的**就是这个）。</summary>
        [Test]
        public void SplitFiles_AllStayUnderTheLineLimit()
        {
            var names = new List<string> { "EvidencePackCommand.cs", "EvidencePackCommand.Csv.cs" };
            foreach (string name in names)
            {
                int lines = File.ReadAllLines(Path.Combine(PackageRoot, "Editor", "Evidence", name)).Length;
                Assert.That(lines, Is.LessThanOrEqualTo(200), name + " 超过 200 行红线：" + lines);
            }
        }
    }
}
