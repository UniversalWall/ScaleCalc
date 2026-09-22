using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Wayward.ScaleCalc.Editor;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// **档位清单导入 / 导出的"不碰场景"行为不变量**。
    /// <para>🔴 这条是"导入导出不碰场景"的**唯一机械证据**：光看代码说"没调场景 API"是源码级信心，
    /// 这里跑一遍**真实 IO 往返**再看场景集合——两者都要有。</para>
    /// <para>继承 <see cref="ScaleCalcCleanupInvariantTestBase"/>：夹具自己建宿主、**绝不关用户打开的场景**。</para>
    /// </summary>
    public sealed class ScaleCalcTierTransferSceneTests : ScaleCalcCleanupInvariantTestBase
    {
        private static string NewTempRoot()
            => Path.Combine(Path.GetTempPath(), "scalecalc-tierscene-" + Guid.NewGuid().ToString("N"));

        /// <summary>**导出**只写文件——已加载场景集合与活动场景**逐项不变**。</summary>
        [Test]
        public void XK5_ExportTo_LeavesTheLoadedSceneSetAndActiveSceneUntouched()
        {
            string root = NewTempRoot();
            string path = Path.Combine(root, ScreenTierTransfer.DefaultFileName);
            string[] before = CurrentScenePaths();
            string activeBefore = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            try
            {
                Directory.CreateDirectory(root);
                ScreenTierTransfer.ExportTo(ScreenTierSet.Default(), path, true);

                Assert.That(File.Exists(path), Is.True, "前置：文件确实写出去了（否则下面两条是空过）");
                Assert.That(CurrentScenePaths(), Is.EqualTo(before), "导出不许动已加载场景集合");
                Assert.That(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path, Is.EqualTo(activeBefore),
                    "导出不许切换活动场景");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        /// <summary>
        /// **导入**只把文本解析成候选——成功与失败两条路径都**逐项不动**场景。
        /// <para>失败那条尤其要守：坏文件是"零副作用"的强主张，场景是其中最贵的一种副作用。</para>
        /// </summary>
        [Test]
        public void XK5_TryImport_NeverTouchesTheSceneSet_OnSuccessOrOnABadFile()
        {
            string root = NewTempRoot();
            string good = Path.Combine(root, "good.txt");
            string bad = Path.Combine(root, "bad.txt");
            string[] before = CurrentScenePaths();
            try
            {
                Directory.CreateDirectory(root);
                ScreenTierTransfer.ExportTo(ScreenTierSet.Default(), good, true);
                File.WriteAllText(bad, "v1\n这不是五个字段\n");

                Assert.That(ScreenTierTransfer.TryImport(good, out List<ScreenTier> tiers, out string why), Is.True, why);
                Assert.That(tiers.Count, Is.EqualTo(ScreenProfiles.All.Length), "前置：好文件确实解析出了候选");
                Assert.That(CurrentScenePaths(), Is.EqualTo(before), "导入（成功路径）不许动场景");

                Assert.That(ScreenTierTransfer.TryImport(bad, out List<ScreenTier> none, out string reason), Is.False);
                Assert.That(none, Is.Null, "前置：坏文件确实被拒");
                Assert.That(reason, Does.Contain("字段数不对"));
                Assert.That(CurrentScenePaths(), Is.EqualTo(before), "导入（失败路径）不许动场景");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
    }
}
