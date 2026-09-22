using System;
using System.IO;
using NUnit.Framework;
using UnityEditor.PackageManager;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// **档位清单两个按钮的回调接线**——源码级守卫。
    /// <para><b>来由</b>：<see cref="ScaleCalcWindowUxmlTests"/> 只查"两个控件在、是 <c>Button</c>、文本非空"。谁把 <c>CreateGUI</c> 里那次接线调用
    /// 删掉、或把某个 <c>clicked +=</c> 去掉，都会**编译零错、断言全绿、按钮点了没反应**。</para>
    /// <para><b>为什么是源码级而不是行为级</b>：行为级要把窗口建起来再真发点击事件，而两个回调都走
    /// <c>EditorUtility.SaveFilePanel</c> / <c>OpenFilePanel</c> —— **模态对话框**，一点就把主线程卡住
    /// （同族现象：见 <c>Main thread operation timed out</c> 先看有没有模态框）。
    /// 同族先例：<c>ScaleCalcOfflineBuildTests.ReconcileIsWiredOnlyToExplicitIntent_AndAutoToggleDefaultsOff</c>。</para>
    /// <para>🔴 **本文件按"代码形态"判，不按子串判**：<c>Does.Contain</c> 会被**注释里的同一个词**满足——
    /// 把 <c>// InitTierTransferSection();</c> 注掉，子串断言照样通过 ⇒ **假绿**。所以这里逐行查、**跳过 <c>//</c> 行**。</para>
    /// <para>⚠️ **覆盖边界**：本条只守"接线存在、且一对一对得上"，**不守**"点下去真的能干活"——后者靠
    /// 现场端到端（手工三条 + live 反射）。**别把这条当成"功能已验"**。</para>
    /// <para>纪律：目标 token 一律**字符拼接**构造。</para>
    /// </summary>
    public sealed class ScaleCalcWindowTransferWiringTests
    {
        private static string PackageRoot =>
            PackageInfo.FindForAssembly(typeof(ScaleCalcWindowTransferWiringTests).Assembly).resolvedPath;

        private static string Read(string fileName)
            => File.ReadAllText(Path.Combine(PackageRoot, "Editor", "Window", fileName));

        /// <summary>在**非注释行**里查 token：注释行（`//` 与 XML 文档 `///`）一律跳过。</summary>
        private static bool InCode(string source, string token)
        {
            foreach (string line in source.Split('\n'))
            {
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith("//", StringComparison.Ordinal)) continue;
                if (trimmed.Contains(token)) return true;
            }
            return false;
        }

        /// <summary>① 接线方法**真的被调用**；② 两个按钮各挂**自己的**回调；③ 没有条件式摘挂。</summary>
        [Test]
        public void TierTransferButtons_AreWiredToTheirOwnHandlers()
        {
            string createGui = Read("ScaleCalcWindow.cs");
            Assert.That(InCode(createGui, "InitTierTransfer" + "Section()"), Is.True,
                "CreateGUI 必须调用接线方法——只定义不调用 ⇒ 两个按钮点了没反应，而编译与断言全绿");

            string transfer = Read("ScaleCalcWindow.Transfer.cs");
            Assert.That(InCode(transfer, "m_ExportTiersButton" + ".clicked += OnExport" + "TiersClicked"), Is.True,
                "「导出档位清单…」必须挂到 OnExportTiersClicked");
            Assert.That(InCode(transfer, "m_ImportTiersButton" + ".clicked += OnImport" + "TiersClicked"), Is.True,
                "「导入档位清单…」必须挂到 OnImportTiersClicked（写错配对 ⇒ 按了导出却在导入）");
            Assert.That(InCode(transfer, ".clicked -= "), Is.False,
                "本功能没有「运行期摘挂」的场景 ⇒ 出现 -= 说明接线被改成了条件式；改本条之前先确认意图");
        }
    }
}
