using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Wayward.ScaleCalc.Editor;
using UnityEngine;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// **工作集落到哪儿**（验收 `K6` · 风险 `R-P7`）：走真 `EditorPrefs` 跑完整条流程，
    /// 仓库内**不许新增任何文件**；键必须**工程隔离**且**确定性**。
    /// <para>⚠️ 用**一次性键**（`…+ tests.<guid>`）⇒ 不碰使用者的真实工作集，跑完即删。</para>
    /// </summary>
    public sealed class ScaleCalcTierStoreTests
    {
        /// <summary>
        /// `K6`：**不落使用方工程目录**——增删 / 勾选 / 存读全跑一遍，`Works/ScaleCalc/` 与 `Assets/` 下
        /// 的文件清单必须**逐项不变**。
        /// </summary>
        [Test]
        public void K6_FullTierFlow_WritesNoFileIntoTheConsumerProject()
        {
            string[] roots = { PackageRoot, Application.dataPath };
            string[] before = Snapshot(roots);
            Assert.That(before.Length, Is.GreaterThan(0), "防空转：两个根都得真的存在，否则快照恒为空、这条断言就是假保护");

            var storage = new EditorPrefsScreenTierStorage(
                EditorPrefsScreenTierStorage.KeyPrefix + "tests." + Guid.NewGuid().ToString("N"));
            try
            {
                ScreenTierSet set = ScreenTierStorage.LoadTiers(storage, out string reason);
                Assert.That(reason, Is.Null, "一次性键 ⇒ 一定是「没存过」");

                Assert.That(set.TryAdd(new ScaleSize(1600f, 900f), "临时行", out ScreenTier custom, out _), Is.True);
                Assert.That(set.SetIncluded(set.Tiers[0].Id, false), Is.True);
                Assert.That(ScreenTierStorage.TrySave(set, storage, out string message), Is.True, message);

                ScreenTierSet back = ScreenTierStorage.LoadTiers(storage, out _);
                Assert.That(ScreenTierStorage.Encode(back), Is.EqualTo(ScreenTierStorage.Encode(set)),
                    "真 EditorPrefs 往返也要逐字一致");

                Assert.That(set.Delete(custom.Id), Is.True);
                Assert.That(ScreenTierStorage.TrySave(set, storage, out _), Is.True);
            }
            finally
            {
                storage.Delete();
            }

            Assert.That(storage.TryLoad(out _), Is.False, "一次性键必须清干净");
            Assert.That(Snapshot(roots), Is.EqualTo(before), "K6：整条流程跑完，仓库内新增文件数必须为 0");
        }

        /// <summary>
        /// `R-P7`：键必须**确定性**——**不能**用 `string.GetHashCode()`（.NET Core 上每次进程启动都变
        /// ⇒ 存了就再也读不回来，是静默的数据丢失）；且**不同工程不许共用键**（同机会互相覆盖工作集）。
        /// </summary>
        [Test]
        public void R_P7_StorageKey_IsDeterministic_AndProjectScoped()
        {
            string a = EditorPrefsScreenTierStorage.KeyForProjectPath(@"D:\Work\MyGame");
            string b = EditorPrefsScreenTierStorage.KeyForProjectPath(@"D:/work/mygame");   // 分隔符 + 大小写不同
            string c = EditorPrefsScreenTierStorage.KeyForProjectPath(@"D:\Work\OtherGame");

            Assert.That(a, Is.EqualTo(b), "同一工程的写法差异必须归一到同一个键");
            Assert.That(a, Is.Not.EqualTo(c), "R-P7：不同工程不许共用键（否则同机互相覆盖工作集）");
            Assert.That(a.StartsWith(EditorPrefsScreenTierStorage.KeyPrefix, StringComparison.Ordinal), Is.True);
            Assert.That(EditorPrefsScreenTierStorage.KeyForProjectPath(null), Is.Not.Null, "空路径也不许抛");
            Assert.That(EditorPrefsScreenTierStorage.KeyForProjectPath(@"D:\Work\MyGame"), Is.EqualTo(a),
                "同一输入必须永远得到同一个键（否则既有工作集全部变成孤儿键）");
        }

        private static string PackageRoot =>
            UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(ScaleCalcTierStoreTests).Assembly).resolvedPath;

        /// <summary>两个根下所有文件的相对路径（排序后）——只比"多了/少了哪些文件"。</summary>
        private static string[] Snapshot(string[] roots)
        {
            var files = new List<string>();
            foreach (string root in roots)
                if (Directory.Exists(root))
                    foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                        files.Add(file.Substring(root.Length));
            files.Sort(StringComparer.Ordinal);
            return files.ToArray();
        }
    }
}
