using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wayward.ScaleCalc.Editor;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// 行为不变量断言（一）：**宿主场景与其中的对象**。
    /// <para>判据：一个对象都不删 · 脏宿主跑完一比一复原 · 场景集合与活动场景不变。
    /// 夹具与场景还原纪律见 <see cref="ScaleCalcCleanupInvariantTestBase"/>。</para>
    /// </summary>
    public sealed class ScaleCalcCleanupInvariantTests : ScaleCalcCleanupInvariantTestBase
    {
        /// <summary>宿主场景里的既有对象（哪怕名字像本协议的）**一个都不许少**。</summary>
        [Test]
        public void Measure_DoesNotDeleteAnyObject_EvenOnesNamedLikeOurs()
        {
            int rootsAtStart = Host.rootCount;
            var mine = new GameObject("__ScaleCalc_ResidueProbe");
            var plain = new GameObject("OrdinaryObjectProbe");
            try
            {
                int rootsBefore = Host.rootCount;
                bool ok = Measure(out LiveTruth _, out string error);
                Assert.That(ok, Is.True, "正常宿主下对账应当成功（" + error + "）");
                Assert.That(mine == null, Is.False, "名字像本协议的对象也不许被删（软标识不作归属判据）");
                Assert.That(plain == null, Is.False, "普通对象当然更不许被删");
                Assert.That(Host.rootCount, Is.EqualTo(rootsBefore), "宿主根对象数不许变");
            }
            finally
            {
                Object.DestroyImmediate(mine);
                Object.DestroyImmediate(plain);
            }

            Assert.That(Host.rootCount, Is.EqualTo(rootsAtStart), "清掉本用例自己的探针后应回到用例开始时的根对象数");
        }

        /// <summary>`G2`：宿主**本来就脏**时——协议不拒绝，但跑完脏状态与内容必须一比一不变。</summary>
        [Test]
        public void Measure_OnDirtyHost_WorksAndLeavesHostExactlyAsItWas()
        {
            var marker = new GameObject("__ScaleCalc_DirtyMarker");
            EditorSceneManager.MarkSceneDirty(Host);
            Assert.That(Host.isDirty, Is.True, "前置：宿主应当是脏的");

            int rootsBefore = Host.rootCount;
            try
            {
                bool ok = Measure(out LiveTruth _, out string error);
                Assert.That(ok, Is.True, "脏宿主不该被拒绝，失败原因：" + error);
                Assert.That(Host.isDirty, Is.True, "协议不许改变宿主的脏状态（本就脏 ⇒ 跑完仍脏）");
                Assert.That(Host.rootCount, Is.EqualTo(rootsBefore), "宿主内容不许被改动");
            }
            finally
            {
                Object.DestroyImmediate(marker);
            }
        }

        /// <summary>跑完**不许多出/少掉任何场景**，且活动场景仍是宿主、临时场景零残留。</summary>
        [Test]
        public void Measure_LeavesTheLoadedSceneSetAndActiveSceneUntouched()
        {
            int countBefore = SceneManager.sceneCount;
            string[] pathsBefore = CurrentScenePaths();

            bool ok = Measure(out LiveTruth _, out string error);

            Assert.That(ok, Is.True, "正常宿主下对账应当成功（" + error + "）");
            Assert.That(SceneManager.sceneCount, Is.EqualTo(countBefore), "场景数不许变（临时场景必须原子清掉）");
            Assert.That(CurrentScenePaths(), Is.EqualTo(pathsBefore), "已加载场景集合必须逐项相同");
            Assert.That(EditorSceneManager.GetActiveScene(), Is.EqualTo(Host), "活动场景必须还是宿主");
            Assert.That(error == null, Is.True, "成功路径不该带告警：" + error);
        }
    }
}
