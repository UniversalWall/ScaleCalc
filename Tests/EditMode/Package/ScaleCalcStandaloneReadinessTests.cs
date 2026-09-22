using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor.PackageManager;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// **可脱离宿主**的静态前提。⚠️ 动态实测（复制进空工程真编译一遍）**按用户 2026-09-17 指令不做**
    /// （本机沙箱拒绝启动第二个编辑器实例，权限升级亦被拒）——因此这里只覆盖**能机械验的那一半**：
    /// 作品包内不许出现指向宿主 / 平台 / 其它作品的引用，四个 asmdef 的引用只能指向包内程序集 + 引擎程序集。
    /// </summary>
    public sealed class ScaleCalcStandaloneReadinessTests
    {
        private static readonly string[] ForbiddenTokens =
        {
            "Wayward.Platform",           // 平台程序集（作品不许反向引用）
            "Assets/Platform",          // 平台目录
            "ConsolePro",               // 宿主里用户自装的辅助插件
            "Wayward.Chess", "Wayward.Card", "Wayward.Tamecraft",   // 其它作品（作品之间禁止相互引用）
            "Library/PackageCache",     // 引擎只读区
            "..\\..\\Tools", "Tools/unity-cli",   // 平台工具
        };

        private static readonly string[] AllowedUsingPrefixes =
        {
            "Wayward.ScaleCalc",          // 本包四个命名空间（前缀匹配）
            "UnityEngine",
            "UnityEditor",
            "NUnit",
            "System",
        };

        private static string PackageRoot =>
            PackageInfo.FindForAssembly(typeof(ScaleCalcStandaloneReadinessTests).Assembly).resolvedPath;

        private static IEnumerable<string> SourceFiles()
        {
            foreach (string folder in new[] { "Runtime", "Editor", "Tests" })
            {
                string path = Path.Combine(PackageRoot, folder);
                if (!Directory.Exists(path)) continue;
                foreach (string file in Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories))
                    yield return file;
            }
        }

        [Test]
        public void NoSourceReferencesHostOrOtherWorks()
        {
            int scanned = 0;
            foreach (string file in SourceFiles())
            {
                // 跳过本文件：禁词表本身就得把那些 token 写出来（扫自己必然误报）
                if (Path.GetFileName(file) == "ScaleCalcStandaloneReadinessTests.cs") continue;
                scanned++;
                string text = File.ReadAllText(file);
                foreach (string token in ForbiddenTokens)
                {
                    // 允许出现在注释里的"解释性提及"（比如"不引用 Assets/Platform"）——只拦真正的代码引用
                    foreach (Match match in Regex.Matches(text, Regex.Escape(token)))
                    {
                        string line = LineOf(text, match.Index);
                        string trimmed = line.TrimStart();
                        if (trimmed.StartsWith("//") || trimmed.StartsWith("///") || trimmed.StartsWith("*")) continue;
                        Assert.Fail(Path.GetFileName(file) + " 出现了宿主/其它作品引用「" + token + "」：" + line.Trim());
                    }
                }
            }
            Assert.That(scanned, Is.GreaterThan(20), "至少要扫到 20 个源文件（Runtime/Editor/Tests 三处，不含本文件）");
        }

        [Test]
        public void UsingsResolveInsideThePackageOrTheEngine()
        {
            foreach (string file in SourceFiles())
            {
                foreach (Match match in Regex.Matches(File.ReadAllText(file), @"^\s*using\s+([A-Za-z0-9_.]+)\s*;",
                                                      RegexOptions.Multiline))
                {
                    string ns = match.Groups[1].Value;
                    bool allowed = false;
                    foreach (string prefix in AllowedUsingPrefixes)
                        if (ns == prefix || ns.StartsWith(prefix + ".")) { allowed = true; break; }
                    Assert.That(allowed, Is.True, Path.GetFileName(file) + " 引用了包外命名空间：" + ns);
                }
            }
        }

        [Test]
        public void AssemblyDefinitionsReferenceOnlyPackageAndEngineAssemblies()
        {
            var allowed = new HashSet<string>
            {
                "Wayward.ScaleCalc", "Wayward.ScaleCalc.Unity", "Wayward.ScaleCalc.Editor", "Wayward.ScaleCalc.Tests",
                "UnityEngine.UI", "UnityEngine.TestRunner", "UnityEditor.TestRunner",
            };

            int asmdefs = 0;
            foreach (string file in Directory.GetFiles(PackageRoot, "*.asmdef", SearchOption.AllDirectories))
            {
                asmdefs++;
                string text = File.ReadAllText(file);
                Match block = Regex.Match(text, @"""references""\s*:\s*\[(?<items>[^\]]*)\]", RegexOptions.Singleline);
                if (!block.Success) continue;
                foreach (Match item in Regex.Matches(block.Groups["items"].Value, @"""(?<name>[^""]+)"""))
                {
                    string name = item.Groups["name"].Value;
                    if (name.StartsWith("GUID:")) continue;      // GUID 引用由编辑器维护，不在此判
                    Assert.That(allowed.Contains(name), Is.True,
                        Path.GetFileName(file) + " 引用了包外程序集：" + name + "（可脱离宿主的前提是只引包内 + 引擎）");
                }
            }
            Assert.That(asmdefs, Is.EqualTo(4), "四个 asmdef：Core / Unity / Editor / Tests");
        }

        [Test]
        public void PackageJsonDeclaresEveryEngineAssemblyWeUse()
        {
            string json = File.ReadAllText(Path.Combine(PackageRoot, "package.json"));
            // UnityEngine.UI 来自 com.unity.ugui；TestRunner 程序集来自 com.unity.test-framework
            Assert.That(json, Does.Contain("com.unity.ugui"), "用到 UnityEngine.UI ⇒ 必须声明 com.unity.ugui");
            Assert.That(json, Does.Contain("com.unity.test-framework"),
                "Tests 程序集**显式引用** UnityEngine.TestRunner / UnityEditor.TestRunner ⇒ 必须声明 com.unity.test-framework，"
                + "否则消费方没装测试框架时，本包的 Tests 程序集引用会悬空（可脱离宿主的前提之一）");
        }

        private static string LineOf(string text, int index)
        {
            int start = text.LastIndexOf('\n', System.Math.Max(0, index - 1)) + 1;
            int end = text.IndexOf('\n', index);
            if (end < 0) end = text.Length;
            return text.Substring(start, end - start);
        }
    }
}
