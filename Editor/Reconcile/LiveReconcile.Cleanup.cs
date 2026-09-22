using System.Collections.Generic;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// <see cref="LiveReconcile"/> 的**清场与指纹章节**（入口与真值在主文件 `LiveReconcile.cs`）。
    /// <para>🔴 本章节的四条硬纪律：</para>
    /// <list type="bullet">
    /// <item>**没有任何对象级销毁调用**（注释里也不写那个 API 名，免得扫描类断言命中自身）——清场只靠关闭自己建的临时场景；</item>
    /// <item>收尾**只关**句柄上记着的那个场景，其余未保存场景**一律不关**，只报告；</item>
    /// <item>报告只讲"挡路的事实"（哪个场景未保存），**不做全工程按名扫描**；</item>
    /// <item>归属判定**不许用名字前缀/软标识**，只认"这个句柄是我自己建的"。</item>
    /// </list>
    /// </summary>
    public static partial class LiveReconcile
    {
        /// <summary>一次场景快照（进协议前记一次、收尾后记一次，逐项比对）。</summary>
        private readonly struct SceneFingerprint
        {
            /// <summary>每个已加载场景的一条记录：`path|dirty|rootCount`（未保存场景的 `path` 段为空）。</summary>
            public readonly string[] Signatures;

            /// <summary>未保存（无路径）场景的名字。**显式字段**，不去解析 <see cref="Signatures"/> 的字符串（不用软标识判归属）。</summary>
            public readonly string[] UntitledNames;

            public readonly string ActivePath;
            public readonly int ActiveRootCount;
            public readonly bool ActiveIsDirty;

            private SceneFingerprint(string[] signatures, string[] untitledNames,
                                     string activePath, int activeRootCount, bool activeIsDirty)
            {
                Signatures = signatures;
                UntitledNames = untitledNames;
                ActivePath = activePath;
                ActiveRootCount = activeRootCount;
                ActiveIsDirty = activeIsDirty;
            }

            public int SceneCount => Signatures == null ? 0 : Signatures.Length;

            /// <summary>有没有"未保存（无路径）"的场景——additive 与它互斥，是唯一会挡住对账的情形。</summary>
            public bool HasUntitledScene => UntitledNames != null && UntitledNames.Length > 0;

            /// <summary>挡路的未保存场景清单（诊断用：只点名，不扫对象、不认领）。</summary>
            public string UntitledSceneList()
            {
                if (!HasUntitledScene) return "（无）";
                var sb = new StringBuilder();
                for (int i = 0; i < UntitledNames.Length; i++)
                {
                    if (i > 0) sb.Append(" · ");
                    sb.Append("场景「").Append(UntitledNames[i]).Append("」");
                }
                return sb.ToString();
            }

            public static SceneFingerprint Capture()
            {
                Scene active = EditorSceneManager.GetActiveScene();
                var signatures = new List<string>(SceneManager.sceneCount);
                var untitled = new List<string>();
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene s = SceneManager.GetSceneAt(i);
                    bool saved = !string.IsNullOrEmpty(s.path);
                    if (!saved) untitled.Add(s.name);
                    signatures.Add((saved ? s.path : "(未保存)") + "|" + (s.isDirty ? "1" : "0") + "|" + s.rootCount);
                }
                return new SceneFingerprint(signatures.ToArray(), untitled.ToArray(),
                                            active.path, active.rootCount, active.isDirty);
            }

            /// <summary>逐项比对；一致返回空串，不一致返回"初值 ⇒ 现值"的可读描述。</summary>
            public static string DescribeDrift(in SceneFingerprint before, in SceneFingerprint after, Scene host)
            {
                var sb = new StringBuilder();

                if (before.SceneCount != after.SceneCount)
                    sb.Append("已加载场景数 ").Append(before.SceneCount).Append(" ⇒ ").Append(after.SceneCount).Append("；");
                else if (!SameSet(before.Signatures, after.Signatures))
                    sb.Append("已加载场景清单发生了变化（").Append(string.Join(" , ", before.Signatures))
                      .Append(" ⇒ ").Append(string.Join(" , ", after.Signatures)).Append("）；");

                if (before.ActivePath != after.ActivePath)
                    sb.Append("活动场景 '").Append(before.ActivePath).Append("' ⇒ '").Append(after.ActivePath).Append("'；");

                Scene now = EditorSceneManager.GetActiveScene();
                if (now != host)
                    sb.Append("活动场景不再是宿主（现值 '").Append(now.name).Append("'）；");

                if (before.ActiveRootCount != after.ActiveRootCount)
                    sb.Append("宿主根对象数 ").Append(before.ActiveRootCount).Append(" ⇒ ").Append(after.ActiveRootCount).Append("；");

                if (before.ActiveIsDirty != after.ActiveIsDirty)
                    sb.Append("宿主 isDirty ").Append(before.ActiveIsDirty).Append(" ⇒ ").Append(after.ActiveIsDirty).Append("；");

                return sb.Length == 0
                    ? null
                    : "现场对账改变了环境（初值 ⇒ 现值）：" + sb + " 现场协议不许动宿主或其内容";
            }

            private static bool SameSet(string[] a, string[] b)
            {
                if (a == null || b == null || a.Length != b.Length) return false;
                var left = new List<string>(a);
                for (int i = 0; i < b.Length; i++)
                    if (!left.Remove(b[i])) return false;
                return left.Count == 0;
            }
        }

        /// <summary>
        /// 「自己建的对象有没有残留」的诊断。
        /// <para>判据是**本次自建的那个场景里还剩不剩根对象**——不是"全工程找同名的"（不做无谓内省、不用软标识认领）。
        /// 正常情况由关场景一起销毁（那时场景已失效 ⇒ 无残留可报）；**只有在"场景没能关掉"时才可能有残留**，
        /// 所以本方法应当在关闭**之后**调用——否则每次成功对账都会误报。</para>
        /// </summary>
        private static string OwnResidueReport(Scene tempScene, string expectedName)
        {
            if (!tempScene.IsValid()) return null;      // 场景已关闭 ⇒ 里面的对象随之销毁，无残留

            GameObject[] roots = tempScene.GetRootGameObjects();
            if (roots == null || roots.Length == 0) return null;

            var sb = new StringBuilder();
            sb.Append("临时场景「").Append(expectedName).Append("」未能关闭，其中还有 ").Append(roots.Length).Append(" 个对象：");
            for (int i = 0; i < roots.Length && i < 8; i++)
            {
                if (i > 0) sb.Append(" · ");
                sb.Append(roots[i] == null ? "(已销毁)" : roots[i].name);
            }
            sb.Append("；本协议不会自行删它们，请手工关闭该场景");
            return sb.ToString();
        }

        /// <summary>
        /// 收尾：关掉**本次自己建的**那一个临时场景。
        /// <para>对象随场景关闭一起消失，因此**不需要任何按名删除**；关不掉时如实写进诊断，绝不"就地清理"别的东西。</para>
        /// </summary>
        private static void LateClose(Scene tempScene, ref string error)
        {
            if (!tempScene.IsValid()) return;

            // 关闭后 `Scene` 会失效、连 name 都读不到 ⇒ 先在关前把名字取出来用于诊断
            string sceneName = tempScene.name;

            bool closed = EditorSceneManager.CloseScene(tempScene, true);
            if (!closed || tempScene.IsValid())
                error = (error == null ? string.Empty : error + " · ")
                    + "临时场景「" + sceneName + "」未能关闭（CloseScene 返回 " + closed
                    + "）：请手工关闭它；本协议不会删它里面的对象或关其它场景";
        }
    }
}
