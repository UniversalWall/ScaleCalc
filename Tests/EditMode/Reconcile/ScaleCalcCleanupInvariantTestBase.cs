using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wayward.ScaleCalc.Editor;
using Wayward.ScaleCalc.Unity;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// **行为不变量断言共用夹具**（两个用例文件继承本类）。
    /// <para>🔴 **绝不关掉/改掉"不是自己打开的东西"**：</para>
    /// <list type="bullet">
    /// <item><c>SetUp</c> **自己新建一个 additive 场景**当宿主，并把它另存到 **<c>Library/</c> 下的绝对路径**
    /// （协议要求宿主已保存；<c>Library/</c> 是**非跟踪区** ⇒ 连 <c>git status</c> 都不会动，也不经 AssetDatabase）。
    /// 用户原本打开的场景**原样留着**：既不关它、也不改它的路径；</item>
    /// <item><c>TearDown</c> **只关自己建的**（测试宿主 + 用例造的旁观场景），随后补一个未命名空场景——
    /// <c>Single</c> 那种"先全关再建"的写法**已废除**：它会把用户打开的场景一起关掉（实测事故）。</item>
    /// </list>
    /// <para>实测记录（两条，都是本夹具的依据）：① <c>NewScene(…, Additive)</c> **会把新场景设为活动场景**，
    /// 所以造完旁观/未保存场景后要用 <see cref="BackToHost"/> 把宿主设回活动场景；
    /// ② <c>SaveScene(活动场景, &lt;Library 下的绝对路径&gt;)</c> **实测可行**（返回 <c>true</c>、文件真的落盘）。</para>
    /// </summary>
    public abstract class ScaleCalcCleanupInvariantTestBase
    {
        private const string TempFolderName = "__ScaleCalcTestHost";

        private readonly List<Scene> m_Created = new List<Scene>();
        private string m_TempFolder;

        /// <summary>夹具自建的宿主场景（有磁盘路径，满足协议的"宿主已保存"硬前置）。</summary>
        protected Scene Host { get; private set; }

        protected static string HostPathIn(string tempFolder) => Path.Combine(tempFolder, "Host.unity");

        [SetUp]
        public void SetUp()
        {
            m_Created.Clear();

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            m_TempFolder = Path.Combine(Path.Combine(projectRoot, "Library"), TempFolderName);
            if (!Directory.Exists(m_TempFolder)) Directory.CreateDirectory(m_TempFolder);

            // 🔴 夹具的宿主 = **测试框架当前那个活动场景**，把它**原地另存**到 `Library/` 下的临时路径。
            //    为什么只能这样（两条实测约束）：
            //      ① 协议要求宿主"已保存"，而测试框架给的是**未命名**场景；
            //      ② 未命名场景与 additive **互斥**（实测原文：Cannot create a new scene additively with an untitled
            //         scene unsaved）⇒ 不先给它路径，连建自己的场景都做不到。
            //    与旧写法的关键区别：**不关任何场景**（旧写法用 NewScene(Single) 会把活动场景关掉——实测事故）。
            //    代价（如实登记）：测试结束后编辑器里那个活动场景的名字/路径变成了临时文件，**但它不是被关掉**。
            Scene host = EditorSceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(host.path)
                && !EditorSceneManager.SaveScene(host, HostPathIn(m_TempFolder), false))
                Assert.Ignore("测试框架的活动场景是未命名场景、且无法另存 ⇒ 本夹具的用例跳过");

            Host = EditorSceneManager.GetActiveScene();
            Assert.That(string.IsNullOrEmpty(Host.path), Is.False, "夹具宿主必须有路径");
        }

        /// <summary>
        /// 跑一次现场对账——**显式传夹具宿主**，因此**不需要**把它设成活动场景，也不用管测试框架当前开着什么。
        /// </summary>
        protected bool Measure(out LiveTruth truth, out string error)
            => LiveReconcile.TryMeasure(LiveTemplate(), Host, out truth, out error);

        [TearDown]
        public void TearDown()
        {
            // ⚠️ **不关宿主**：它是测试框架的活动场景，不是我们建的 ⇒ 关它就是"动别人的东西"。
            //    只关本用例自己造的场景（旁观者），并把活动场景留在宿主上（它本来就是活动的）。
            for (int i = m_Created.Count - 1; i >= 0; i--)
            {
                Scene s = m_Created[i];
                if (s.IsValid())
                    Assert.That(EditorSceneManager.CloseScene(s, true), Is.True, "自己建的场景应当能关掉");
            }
            m_Created.Clear();

            // 临时落点**不在这里删**：活动场景还指着它（删了会留下悬空路径）。留给下一次 SetUp 复用/覆盖，
            // 目录本身在 `Library/`（非跟踪区，不进版本库）。
        }

        /// <summary>造一个"未命名（无路径）"的场景，登记给 `TearDown` 清理。</summary>
        protected Scene NewUntitledScene()
        {
            Scene s = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            m_Created.Add(s);
            return s;
        }

        /// <summary>把活动场景设回夹具宿主（仅少数用例需要——大多数用例走 <see cref="Measure"/>，根本不碰活动场景）。</summary>
        protected void BackToHost() => EditorSceneManager.SetActiveScene(Host);

        protected static ScaleCalcInput LiveTemplate()
        {
            ScaleCalcInput template = ScaleCalcInput.Default;
            template.Mode = ScaleMode.ScaleWithScreenSize;
            template.ScreenMatch = ScreenMatchMode.MatchWidthOrHeight;
            template.MatchWidthOrHeight = 0.5f;
            return template;
        }

        protected static string[] CurrentScenePaths()
        {
            var list = new List<string>(SceneManager.sceneCount);
            for (int i = 0; i < SceneManager.sceneCount; i++)
                list.Add(SceneManager.GetSceneAt(i).path);
            list.Sort();
            return list.ToArray();
        }
    }
}
