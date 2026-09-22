using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// 包内资源定位（`W-ScaleCalc-8` 未实测项的落地）：**作品是本地 `file:` 包**，
    /// 不能假设 `.uxml` 能被 `Resources` 找到，也不能硬编码绝对路径——
    /// 正解是 `PackageInfo.FindForAssembly` 拿到包根（`Packages/<包名>`），再 `AssetDatabase.LoadAssetAtPath`。
    /// </summary>
    internal static class ScaleCalcAssets
    {
        /// <summary>本包在 AssetDatabase 里的根（本地 `file:` 包形如 `Packages/com.wayward.scalecalc`）。</summary>
        public static string PackageRoot
        {
            get
            {
                // 必须写全名：`UnityEditor.PackageInfo` 与 `UnityEditor.PackageManager.PackageInfo` 同名（CS0104）
                UnityEditor.PackageManager.PackageInfo info =
                    UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(ScaleCalcAssets).Assembly);
                return info != null ? info.assetPath : null;
            }
        }

        /// <summary>
        /// 包内**界面资源**目录（`.uxml` / `.uss` 的家）：`Editor/UI/`。
        /// <para>2026-09-20 目录整顿：界面资源从 `Editor/` 平铺下沉到 `Editor/UI/`，与平台《UI Toolkit 与 Odin 开发指南》
        /// §4.1 的路径口径（`Packages/com.wayward.scalecalc/Editor/UI/X.uxml`）对齐。</para>
        /// <para>🔴 **资源落点只此一处**：改了这里等于改了 `.uxml`/`.uss` 的实际位置，测试侧 `ScaleCalcWindowUxmlTests`
        /// 会按硬路径去加载（假红即说明两处没对齐）。</para>
        /// </summary>
        public static string UiFolder => PackageRoot == null ? null : PackageRoot + "/Editor/UI";

        public static VisualTreeAsset LoadUxml(string fileName)
            => LoadAsset<VisualTreeAsset>(fileName);

        public static StyleSheet LoadUss(string fileName)
            => LoadAsset<StyleSheet>(fileName);

        /// <summary>定位结果（诊断用：窗口状态栏要能直说"为什么没加载"）。</summary>
        public static string Describe(string fileName)
        {
            string root = PackageRoot;
            if (root == null) return "PackageInfo.FindForAssembly 取不到包根（不在包内？）";
            string assetPath = UiFolder + "/" + fileName;
            string absolute = Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
            return assetPath + "（物理路径 " + absolute + "，" + (File.Exists(absolute) ? "存在" : "不存在") + "）";
        }

        private static T LoadAsset<T>(string fileName) where T : Object
        {
            string folder = UiFolder;
            if (folder == null) return null;
            return AssetDatabase.LoadAssetAtPath<T>(folder + "/" + fileName);
        }
    }
}
