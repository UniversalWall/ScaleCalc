using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor.PackageManager;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// 批次 4 步 4.2/4.4/4.5 的**包卫生**断言：`package.json` 的 `samples` 路径真的存在、依赖只有引擎包、
    /// 许可证与 `LICENSE` 一致、版本与 `CHANGELOG` 一致——这些是"可脱离宿主"的前置条件（能离线静态验的部分）。
    /// </summary>
    public sealed class ScaleCalcPackageHygieneTests
    {
        private static string PackageRoot =>
            PackageInfo.FindForAssembly(typeof(ScaleCalcPackageHygieneTests).Assembly).resolvedPath;

        private static string ReadPackageFile(string relative) => File.ReadAllText(Path.Combine(PackageRoot, relative));

        [Test]
        public void Samples_AllPathsExistAndHoldScenes()
        {
            string json = ReadPackageFile("package.json");
            Assert.That(json, Does.Contain("\"samples\""), "README + samples 是发布口径的一部分");

            string samplesRoot = Path.Combine(PackageRoot, "Samples~");
            Assert.That(Directory.Exists(samplesRoot), Is.True);

            string[] folders = Directory.GetDirectories(samplesRoot);
            Assert.That(folders.Length, Is.EqualTo(2), "两个 samples 条目：三模式对照 + 早退反例");

            int sceneCount = Directory.GetFiles(samplesRoot, "*.unity", SearchOption.AllDirectories).Length;
            Assert.That(sceneCount, Is.EqualTo(4), "三模式各一个场景 + 早退反例一个");

            foreach (string folder in folders)
            {
                string name = Path.GetFileName(folder);
                Assert.That(json, Does.Contain("Samples~/" + name), "每个 samples 目录都要在 package.json 里声明");
                Assert.That(Directory.GetFiles(folder, "*.unity").Length, Is.GreaterThan(0), name + " 里要有场景");
            }
        }

        [Test]
        public void Samples_ScenesKeepTheirMetaPaired()
        {
            string samplesRoot = Path.Combine(PackageRoot, "Samples~");
            foreach (string scene in Directory.GetFiles(samplesRoot, "*.unity", SearchOption.AllDirectories))
                Assert.That(File.Exists(scene + ".meta"), Is.True, scene + " 缺 .meta（导入后 GUID 会变）");
        }

        [Test]
        public void Dependencies_ContainOnlyEnginePackages()
        {
            string json = ReadPackageFile("package.json");
            int start = json.IndexOf("\"dependencies\"", System.StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThan(0));
            int end = json.IndexOf('}', start);
            string block = json.Substring(start, end - start);

            Assert.That(block, Does.Contain("com.unity.ugui"), "唯一的引擎依赖是 uGUI（零第三方插件）");
            foreach (string line in block.Split(','))
            {
                if (!line.Contains(":")) continue;
                string key = line.Split(':')[0].Trim().Trim('"');
                if (key == "dependencies") continue;
                Assert.That(key, Does.StartWith("com.unity."), "依赖只能是引擎包，出现第三方就是违反平台红线：" + key);
            }
        }

        [Test]
        public void LicenseAndVersionAreConsistent()
        {
            string json = ReadPackageFile("package.json");
            string license = ReadPackageFile("LICENSE");
            string changelog = ReadPackageFile("CHANGELOG.md");

            Assert.That(json, Does.Contain("\"license\": \"MIT\""));
            Assert.That(license, Does.Contain("MIT License"));

            // 🔴 版本号**从 `package.json` 读出来**，再断言 CHANGELOG 有对应段（2026-09-20 修）：
            //    原写法硬编码 `"version": "0.1.0"` ⇒ 它测的不是"一致性"而是"**版本没变过**"——
            //    每次切版本都得反过来改测试，而**真正该守的**（切了版本却忘了开 CHANGELOG 段）它不管。
            Match m = Regex.Match(json, "\"version\"\\s*:\\s*\"([^\"]+)\"");
            Assert.That(m.Success, Is.True, "package.json 里必须能读到 version");
            string version = m.Groups[1].Value;
            Assert.That(version, Does.Match(@"^\d+\.\d+\.\d+$"), "版本号必须是三段 SemVer：" + version);
            Assert.That(changelog, Does.Contain("## [" + version + "]"),
                "CHANGELOG 必须有与 package.json 版本号一致的段落（包与发布规范 §三）：" + version);
            Assert.That(changelog, Does.Contain("[Unreleased]"), "未发布版本用 [Unreleased] 段累积（包与发布规范 §三）");
        }

        [Test]
        public void Readme_HasTheRequiredSections()
        {
            string readme = ReadPackageFile("README.md");
            foreach (string section in new[] { "## 一、这是什么", "## 二、安装", "## 三、怎么用", "## 四、与我有关" })
                Assert.That(readme, Does.Contain(section), "README 必备四节（边界声明自 `Q-15` 起不再必填）：" + section);
            Assert.That(readme, Does.Contain("MIT"), "「与我有关」至少要写许可证");
            Assert.That(readme, Does.Contain("com.wayward.scalecalc"), "安装段要给可照抄的包名");
        }

        /// <summary>
        /// 测试树里一个 <c>.cs</c> **只许有一个顶层类型，且文件名 = 该类名的机械化判据**。
        /// <para>来由：曾有一个测试文件装 3 个类、另一个装 2 个
        /// ⇒ "按文件名找不到类、按类名找不到文件"。曾议"改名成主题名"，但那样**判据无法机械化**
        /// （文件名永远不等于其中任何一个类名）⇒ 取"一事一类"，从此由这条断言替人守。</para>
        /// <para>顶层类型 = **缩进最浅**的那一层；嵌套类型缩进更深、不参与判定（例：几何内核里的 <c>ScaleFit.Axis</c>）。</para>
        /// </summary>
        [Test]
        public void EveryTestFile_HoldsExactlyOneTopLevelType_MatchingItsFileName()
        {
            string testsRoot = Path.Combine(PackageRoot, "Tests", "EditMode");
            // 🔴 必须递归：目录整顿后 Tests/EditMode 顶层**一个 .cs 都没有**（这条曾让另一处守卫静默变空）
            string[] files = Directory.GetFiles(testsRoot, "*.cs", SearchOption.AllDirectories);
            Assert.That(files.Length, Is.GreaterThan(30), "扫描面必须非空——扫到 0 个文件的断言等于没查");

            var problems = new List<string>();
            foreach (string file in files)
            {
                string self = Path.GetFileNameWithoutExtension(file);
                int best = int.MaxValue;
                var tops = new List<string>();
                foreach (string raw in File.ReadAllLines(file))
                {
                    // 🔴 取**宽松**形态：`readonly struct` 与**不带访问修饰符**的类型都要读得到
                    //    （旧写法要求 `(public|internal)` 紧跟缩进、修饰符表里又没有 `readonly` ⇒ 对它们是**盲的**）
                    Match m = Regex.Match(raw, @"^(\s*)(?:(?:public|internal|protected|private)\s+)?(?:(?:abstract|sealed|static|partial|readonly|unsafe|new)\s+)*(?:class|struct|enum|interface)\s+(\w+)");
                    if (!m.Success) continue;
                    int indent = m.Groups[1].Length;
                    if (indent < best) { best = indent; tops.Clear(); }
                    if (indent == best) tops.Add(m.Groups[2].Value);
                }

                if (tops.Count != 1) problems.Add(self + ".cs 里有 " + tops.Count + " 个顶层类型：" + string.Join(" / ", tops.ToArray()));
                else if (tops[0] != self) problems.Add(self + ".cs 的顶层类型叫 " + tops[0]);
            }

            Assert.That(problems.Count, Is.EqualTo(0),
                "测试文件必须「一事一类、文件名 = 类名」：" + string.Join(" ; ", problems.ToArray()));
        }

        /// <summary>
        /// 生产树里一个 <c>.cs</c> **只许有一个顶层类型**的机械化判据（与上面那条测试文件版同族），
        /// 且文件名 = 类型名（或 <c>类型名.后缀</c>，容纳 partial）。
        /// <para>来由：曾有文件一装 **5 个枚举**、有文件装 2 个类型、还有两个文件的文件名**谁都不匹配**
        /// ⇒ "按文件名找不到类、按类名找不到文件"。</para>
        /// <para>⚠️ partial 是**合法**的：<c>ScaleCalcWindow.Rows.cs</c> 里的类型是 <c>ScaleCalcWindow</c>
        /// ⇒ 文件名以「类型名 + <c>.</c>」开头即算通过。</para>
        /// </summary>
        [Test]
        public void EverySourceFile_HoldsExactlyOneTopLevelType_MatchingItsFileName()
        {
            var files = new List<string>();
            foreach (string dir in new[] { "Runtime", "Editor" })
                files.AddRange(Directory.GetFiles(Path.Combine(PackageRoot, dir), "*.cs", SearchOption.AllDirectories));
            Assert.That(files.Count, Is.GreaterThan(30), "扫描面必须非空——扫到 0 个文件的断言等于没查");

            var problems = new List<string>();
            foreach (string file in files)
            {
                string self = Path.GetFileNameWithoutExtension(file);
                int best = int.MaxValue;
                var tops = new List<string>();
                foreach (string raw in File.ReadAllLines(file))
                {
                    Match m = Regex.Match(raw, @"^(\s*)(?:(?:public|internal|protected|private)\s+)?(?:(?:abstract|sealed|static|partial|readonly|unsafe|new)\s+)*(?:class|struct|enum|interface)\s+(\w+)");
                    if (!m.Success) continue;
                    int indent = m.Groups[1].Length;
                    if (indent < best) { best = indent; tops.Clear(); }
                    if (indent == best) tops.Add(m.Groups[2].Value);
                }

                if (tops.Count != 1) problems.Add(self + ".cs 里有 " + tops.Count + " 个顶层类型：" + string.Join(" / ", tops.ToArray()));
                else if (tops[0] != self && !self.StartsWith(tops[0] + ".", System.StringComparison.Ordinal))
                    problems.Add(self + ".cs 的顶层类型叫 " + tops[0]);
            }

            Assert.That(problems.Count, Is.EqualTo(0),
                "生产文件必须「一事一类、文件名 = 类名（或 类名.后缀）」：" + string.Join(" ; ", problems.ToArray()));
        }
    }
}
