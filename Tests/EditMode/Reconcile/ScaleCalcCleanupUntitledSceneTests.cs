using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wayward.ScaleCalc.Editor;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// 行为不变量断言（二）：**挡路的未保存场景与诊断**。
    /// <para>判据：旁观未保存场景仍在 · 宿主未保存 ⇒ 拒绝且零副作用 · 诊断如实点名。
    /// 夹具与场景还原纪律见 <see cref="ScaleCalcCleanupInvariantTestBase"/>。</para>
    /// </summary>
    public sealed class ScaleCalcCleanupUntitledSceneTests : ScaleCalcCleanupInvariantTestBase
    {
        /// <summary>旁观者「未命名 + 有内容 + 非活动」——跑完**必须仍在且内容不变**，且诊断点名它。</summary>
        [Test]
        public void Measure_LeavesUntitledBystanderSceneAlone_AndNamesIt()
        {
            Scene bystander = NewUntitledScene();
            var content = new GameObject("BystanderContent");
            SceneManager.MoveGameObjectToScene(content, bystander);
            string bystanderName = bystander.name;
            BackToHost();   // 见夹具注释：additive 会把新场景设为活动场景

            bool ok = Measure(out LiveTruth _, out string error);

            string diag = " · 挡路场景='" + bystanderName + "' · 错误=" + error;
            Assert.That(ok, Is.False, "有未保存场景挡路时应如实失败" + diag);
            Assert.That(bystander.IsValid(), Is.True, "未命名旁观场景不许被关掉" + diag);
            Assert.That(bystander.rootCount, Is.EqualTo(1), "旁观场景里的对象不许被删" + diag);
            Assert.That(content == null, Is.False, "旁观场景里的对象引用必须还有效" + diag);
            Assert.That(SceneManager.GetSceneByName(bystanderName).IsValid(), Is.True, "它必须还在已加载清单里" + diag);
            Assert.That(error, Does.Contain("不会替你关闭或删除"), "必须明说不会替用户关/删" + diag);
            Assert.That(error, Does.Contain(bystanderName), "诊断必须**点名**挡路的场景" + diag);
        }

        /// <summary>
        /// **宿主不是当前活动场景** ⇒ 直接拒绝，且**零副作用**（协议不改动活动场景，
        /// 所以"宿主 ≠ 活动场景"这种情况只能拒绝，不能替调用方切换）。
        /// </summary>
        [Test]
        public void Measure_OnHostThatIsNotActive_RejectsWithNoSideEffect()
        {
            Scene other = NewUntitledScene();          // 未保存、且**不是**夹具宿主
            EditorSceneManager.SetActiveScene(other);

            var content = new GameObject("OtherSceneContent");
            SceneManager.MoveGameObjectToScene(content, other);

            int countBefore = SceneManager.sceneCount;
            int rootsBefore = other.rootCount;
            string[] pathsBefore = CurrentScenePaths();

            bool ok = Measure(out LiveTruth _, out string error);

            Assert.That(ok, Is.False, "宿主不是活动场景时必须被拒绝");
            Assert.That(error, Does.Contain("宿主与当前活动场景不一致"), "拒绝原因必须可读且可操作");
            Assert.That(error, Does.Contain("没有创建、删除或关闭任何东西"), "必须明说零副作用");
            Assert.That(SceneManager.sceneCount, Is.EqualTo(countBefore), "拒绝路径不许建/拆场景");
            Assert.That(CurrentScenePaths(), Is.EqualTo(pathsBefore), "拒绝路径不许改场景集合");
            Assert.That(other.rootCount, Is.EqualTo(rootsBefore), "拒绝路径不许动该场景内容");
            Assert.That(content == null, Is.False, "拒绝路径不许删该场景里的对象");
        }

        /// <summary>
        /// `G6` 的第三段（**"活动且未保存"的宿主**）在**单进程内构造不出来**：把未命名场景设为活动 ⇒
        /// 夹具宿主就不再是活动场景，于是先撞上"宿主与活动场景不一致"那条判据。
        /// <para>⇒ 按平台规矩**如实降级**为源码级判据：那条拒绝分支必须存在且可读
        /// （真正的保证仍是"协议只建/拆自己的临时场景"，由 <c>ScaleCalcCleanupSourceTests</c> 守住）。</para>
        /// </summary>
        [Test]
        public void UnsavedHostRejectionBranchExists_AndIsReadable()
        {
            string reconcileFolder = System.IO.Path.Combine(
                UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(ScaleCalcInput).Assembly).resolvedPath, "Editor", "Reconcile");
            string source = string.Empty;
            foreach (string f in System.IO.Directory.GetFiles(reconcileFolder, "*.cs"))   // 🔴 扫全目录：`LiveTruth.cs` 也得在内
                source += System.IO.File.ReadAllText(f);

            Assert.That(source, Does.Contain("宿主场景尚未保存"), "「宿主未保存」这条拒绝分支必须存在");
            Assert.That(source, Does.Contain("没有创建、删除或关闭任何东西"), "该分支必须明说零副作用");
        }
    }
}
