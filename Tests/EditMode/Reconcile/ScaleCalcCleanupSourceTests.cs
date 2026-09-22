using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor.PackageManager;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// **源码级静态断言**：本作品的现场对账实现里**不许再出现**破坏性动作。
    /// <para>⚠️ 纪律：要求"搜不到某词"的断言，**自己也不许把那个词写成字面量**——
    /// 本文件里的目标 token 全部用字符拼接间接构造（否则断言会命中自己）。</para>
    /// <para>这是**对自己包内源码的自查**（硬标识就是"我自己的源码文件"），
    /// **不是**去扫用户对象（那种内省明确禁止）。</para>
    /// </summary>
    public sealed class ScaleCalcCleanupSourceTests
    {
        private static string PackageRoot =>
            UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(ScaleCalcCleanupSourceTests).Assembly).resolvedPath;

        /// <summary>
        /// 本协议的全部实现文件。🔴 **扫整个目录**（不是按 `LiveReconcile*` 通配）：真值类型 <c>LiveTruth</c>
        /// 被拆进 `LiveTruth.cs` 之后，按前缀 glob 会**静默漏掉那个文件** ⇒ 本文件里那些"实现里搜不到某词"的判据
        /// 都会**悄悄少覆盖一个文件**（比断言失败更糟）。扫全目录还额外获得"将来新增的文件自动纳入"。
        /// </summary>
        private static string[] ImplementationFiles()
        {
            var files = new List<string>(Directory.GetFiles(Path.Combine(PackageRoot, "Editor", "Reconcile"), "*.cs"));
            files.Sort();
            return files.ToArray();
        }

        private static string ReadAll(out string fileList)
        {
            var sb = new System.Text.StringBuilder();
            var names = new List<string>();
            foreach (string f in ImplementationFiles())
            {
                names.Add(Path.GetFileName(f));
                sb.AppendLine(File.ReadAllText(f));
            }
            fileList = string.Join(" + ", names.ToArray());
            return sb.ToString();
        }

        [Test]
        public void ImplementationExists_AndIsSplitSoEachFileStaysUnderTheLineLimit()
        {
            string[] files = ImplementationFiles();
            Assert.That(files.Length, Is.EqualTo(4), "现场对账实现是四个文件：真值类型（`LiveTruth.cs`，拆出）/ 主入口 / 测量章节 / 清场与指纹章节");
            foreach (string f in files)
            {
                int lines = File.ReadAllLines(f).Length;
                Assert.That(lines, Is.LessThanOrEqualTo(200), Path.GetFileName(f) + " 超过 200 行红线：" + lines);
            }
        }

        /// <summary>实现里**不许有任何对象级销毁调用**，也不许有全工程对象扫描。</summary>
        [Test]
        public void ImplementationHasNoObjectDestruction()
        {
            string source = ReadAll(out string names);
            string destroyAll = "Destroy" + "Immediate";              // 不写字面量（否则断言命中自己）
            string findAll = "FindObjectsOf" + "TypeAll";
            Assert.That(source, Does.Not.Contain(destroyAll), names + " 里出现了对象级销毁调用");
            Assert.That(source, Does.Not.Contain(findAll), names + " 里出现了全工程对象扫描");
        }

        /// <summary>**不许用软标识认领归属**——名字前缀不得进入任何判据。</summary>
        [Test]
        public void ImplementationNeverJudgesOwnershipByName()
        {
            string source = ReadAll(out string names);
            string startsWith = "Starts" + "With";
            string legacyPrefix = "__Scale" + "Calc_";
            string legacyMagic = "Sample" + "Scene";
            Assert.That(source, Does.Not.Contain(startsWith), names + " 里用字符串前缀做判据");
            Assert.That(source, Does.Not.Contain(legacyPrefix), names + " 里仍残留旧命名前缀（前缀已退出判据）");
            Assert.That(source, Does.Not.Contain(legacyMagic), names + " 里仍有 " + legacyMagic + " 魔法串");
        }

        /// <summary>关闭动作**只允许**针对自己建的场景，且实现里只有一处"关闭"。</summary>
        [Test]
        public void ImplementationClosesOnlyTheSceneItCreated()
        {
            string source = ReadAll(out string names);
            string closeScene = "Close" + "Scene";
            Assert.That(source, Does.Contain(closeScene), names + " 应当通过关闭自己建的场景来清场");
            Assert.That(source, Does.Not.Contain("sceneCount - 1"), names + " 里出现了遍历式关闭（旧的 CloseTempScenes 写法）");
        }

        /// <summary>
        /// 协议**从不改动活动场景**——它只建/拆自己那个临时场景。
        /// 因此实现里**不该有** <c>SetActiveScene</c>；"宿主必须是活动场景"这条约束由硬前置直接拒绝来保证。
        /// </summary>
        [Test]
        public void ImplementationNeverChangesTheActiveScene()
        {
            string source = ReadAll(out string names);
            // 注释里会提到这个 API 名（说明"为什么不用它"）⇒ 只查**调用形态**（后跟左括号）
            string call = "Set" + "ActiveScene(";
            Assert.That(source, Does.Not.Contain(call),
                names + " 里出现了活动场景切换调用——协议只建/拆自己的临时场景，不改活动场景");
        }

        /// <summary>宿主三条硬前置必须在**任何建/删动作之前**判定——源码级判据看顺序。</summary>
        [Test]
        public void HostPreconditionsComeBeforeAnySceneMutation()
        {
            string source = ReadAll(out string names);
            int rejectAt = source.IndexOf("RejectHostUntitled", System.StringComparison.Ordinal);
            int newSceneAt = source.IndexOf("New" + "Scene", System.StringComparison.Ordinal);
            Assert.That(rejectAt, Is.GreaterThanOrEqualTo(0), names + " 应当有宿主硬前置");
            Assert.That(newSceneAt, Is.GreaterThan(rejectAt), "硬前置必须写在建临时场景**之前**（拒绝路径零副作用）");
        }

        /// <summary>
        /// 的**测试侧守卫**：测试夹具**不许用 <c>Single</c> 模式建场景**——
        /// 它会把用户打开的场景一起关掉（实测事故：跑一次 EditMode 测试就把编辑器里开着的场景换成空场景）。
        /// <para>本断言的目标 token 同样用字符拼接构造，不写成字面量。</para>
        /// </summary>
        [Test]
        public void TestFixtureNeverReplacesTheWholeSceneSet()
        {
            string testFolder = Path.Combine(PackageRoot, "Tests", "EditMode");
            string singleMode = "New" + "SceneMode.Single";
            // 🔴 必须递归：目录整顿把测试文件全下沉到子目录后，`Directory.GetFiles(folder, "*.cs")`
            //    （默认不递归）**扫到 0 个文件**，下面的"不许出现"就**静默为真**——守卫看着在、其实什么都没守。
            //    ⇒ 顺带把**分母**也钉住：扫到几个文件本身就是判据的一部分（分母为 0 的假绿）。
            string[] files = Directory.GetFiles(testFolder, "*.cs", SearchOption.AllDirectories);
            Assert.That(files.Length, Is.GreaterThan(30),
                "扫描面必须非空（测试文件都在 Tests/EditMode 的子目录里）：扫到 0 个文件时这条守卫等于没有");

            var offenders = new List<string>();
            foreach (string f in files)
            {
                if (File.ReadAllText(f).Contains(singleMode)) offenders.Add(Path.GetFileName(f));
            }
            Assert.That(offenders.Count, Is.EqualTo(0),
                "测试夹具不许用 " + singleMode + " 建场景（会关掉用户打开的场景）：" + string.Join(" , ", offenders.ToArray()));
        }
    }
}
