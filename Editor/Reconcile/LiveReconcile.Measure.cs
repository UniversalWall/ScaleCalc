using System;
using System.Reflection;
using Wayward.ScaleCalc.Unity;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// `LiveReconcile` 的**测量章节**（拆文件的判据 = 200 行红线；真值类型在主文件、清场与指纹在 `LiveReconcile.Cleanup.cs`）。
    /// <para>协议六步、硬前置与副作用边界见主文件 `LiveReconcile.cs` 的类注释；本文件只放"怎么测"。</para>
    /// </summary>
    public static partial class LiveReconcile
    {
        /// <summary>临时对象/场景的命名——**只用于命名与诊断可读性，绝不作为"归属"判据**。</summary>
        private const string TempObjectName = "ReconcileTemp";

        /// <summary>硬前置拒绝文案（宿主不可用 ⇒ 直接拒绝，零副作用）。</summary>
        private const string RejectHostUntitled =
            "宿主场景尚未保存（活动场景没有路径），本协议需要 additive 临时场景才能取真值 ⇒ 本次对账不进行。"
            + "请先保存/另存宿主场景再重试。**本协议没有创建、删除或关闭任何东西。**";

        /// <summary>硬前置拒绝文案（有未保存场景挡路 ⇒ 拒绝，而不是替用户关掉它）。</summary>
        private const string ReportBlockedByUntitled =
            "已加载的场景里有未保存的场景，additive 模式无法与之共存（Cannot create a new scene additively with an "
            + "untitled scene unsaved）⇒ 本次对账不进行。**本协议不会替你关闭或删除它们**，请自行保存或关闭后重试。";

        /// <summary>宿主与活动场景不一致时的拒绝文案（协议不改动活动场景）。</summary>
        private const string RejectHostNotActive =
            "宿主与当前活动场景不一致 ⇒ 本次对账不进行（协议不改动活动场景）。"
            + "**本协议没有创建、删除或关闭任何东西。**";

        /// <summary>
        /// 现场测一次（宿主 = **当前活动场景**）。**失败也返回可读原因**，
        /// 且无论成败都把**自己创建的**临时场景清干净。
        /// </summary>
        public static bool TryMeasure(ScaleCalcInput template, out LiveTruth truth, out string error)
            => TryMeasure(template, EditorSceneManager.GetActiveScene(), out truth, out error);

        /// <summary>
        /// 现场测一次（**显式指定宿主**）。
        /// <para>为什么要有宿主参数：协议若依赖"活动场景"这个全局状态，测试与调用方就**必须先改动
        /// 编辑器的活动场景**才算数——那是动别人的东西。显式传宿主之后，测试夹具可以直接用自己那个已保存的
        /// 临时场景，**全程不碰活动场景**。</para>
        /// <para>宿主三条硬前置：有效 · 有磁盘路径 · **就是当前活动场景**。
        /// 第三条把"活动场景被换走"这件事**从根上排除**（协议只建/拆自己的临时场景，从不改活动场景），
        /// 三条都在任何建/删动作**之前**判定 ⇒ 拒绝路径零副作用。</para>
        /// </summary>
        public static bool TryMeasure(ScaleCalcInput template, Scene host, out LiveTruth truth, out string error)
        {
            truth = default;
            error = null;

            // ── 硬前置：宿主未保存才拒绝；宿主"脏"不拒绝（改由收尾指纹一比一复原）
            if (!host.IsValid() || string.IsNullOrEmpty(host.path))
            {
                error = RejectHostUntitled;
                return false;
            }
            if (EditorSceneManager.GetActiveScene() != host)
            {
                error = RejectHostNotActive;
                return false;
            }

            SceneFingerprint before = SceneFingerprint.Capture();
            if (before.HasUntitledScene)
            {
                error = ReportBlockedByUntitled + " 挡路的是：" + before.UntitledSceneList();
                return false;
            }

            Scene tempScene = default(Scene);
            try
            {
                // ② 建临时场景（**只关自己建的**：这一步建出来的 scene 就是本协议唯一的清理对象）
                tempScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                tempScene.name = TempObjectName;

                // 建对象放进临时场景（不依赖活动场景 ⇒ 与"活动场景不受影响"这条保证互不干扰）
                // `DontSave`：万一中途崩了，用户保存自己的场景时**不会**把临时对象捎带存进去；
                // 它**不是**删除判据，本协议任何地方都不靠它认领归属。
                var go = new GameObject(TempObjectName);
                go.hideFlags = HideFlags.DontSave;
                if (go.scene != tempScene) SceneManager.MoveGameObjectToScene(go, tempScene);
                Canvas canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.targetDisplay = template.TargetDisplay;

                var scaler = go.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = (CanvasScaler.ScaleMode)(int)template.Mode;
                scaler.screenMatchMode = (CanvasScaler.ScreenMatchMode)(int)template.ScreenMatch;
                scaler.matchWidthOrHeight = template.MatchWidthOrHeight;
                scaler.referenceResolution = new Vector2(template.ReferenceResolution.Width, template.ReferenceResolution.Height);
                scaler.scaleFactor = template.ConstantScaleFactor;
                scaler.fallbackScreenDPI = template.FallbackScreenDPI;
                scaler.defaultSpriteDPI = template.DefaultSpriteDPI;
                scaler.referencePixelsPerUnit = template.ReferencePixelsPerUnit;
                scaler.physicalUnit = (CanvasScaler.Unit)(int)template.PhysicalUnit;
                scaler.dynamicPixelsPerUnit = template.DynamicPixelsPerUnit;

                // ④ 活动场景**不需要**恢复：宿主自己就是活动场景（硬前置第三条已判定），
                //    而本协议只动自己的临时场景 ⇒ 活动场景从头到尾没被换走。
                //    早先的实现曾在这里把宿主设回活动场景——那一步已随"协议不改活动场景"这条口径一并删除。

                // 实测：`RepaintAllViews` / `QueuePlayerLoopUpdate` **都不会**让缩放器重算 ⇒ 必须显式调 Handle()
                MethodInfo handle = typeof(CanvasScaler).GetMethod("Handle",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (handle == null)
                {
                    error = "反射取不到 CanvasScaler.Handle()（引擎私有 API 改名 ⇒ 现场对账不可用；EditMode 断言仍有效）";
                    return false;
                }
                handle.Invoke(scaler, null);

                ScaleCalcInput input = ScaleInputCollector.Collect(canvas, scaler, out ScreenSizeSource source);
                var rect = (RectTransform)canvas.transform;
                truth = new LiveTruth(
                    canvas.scaleFactor,
                    canvas.referencePixelsPerUnit,
                    new ScaleSize(rect.rect.size.x, rect.rect.size.y),
                    new ScaleSize(canvas.renderingDisplaySize.x, canvas.renderingDisplaySize.y),
                    input.ScreenSize,
                    source,
                    // 测量条件随真值一起带出去：调用方靠它判断"这份真值能不能用在这一块上"
                    template.ReferenceResolution,
                    template.MatchWidthOrHeight,
                    template.ScreenMatch);

                if (truth.RenderingDisplaySize.Width <= 0f || truth.RenderingDisplaySize.Height <= 0f)
                {
                    error = "真值不可得：编辑期未渲染（renderingDisplaySize = " + truth.RenderingDisplaySize + "），本行只落内核列";
                    return false;
                }
                return true;
            }
            catch (Exception e)
            {
                error = "现场对账抛出异常：" + e.GetType().Name + " / " + e.Message;
                // 最常见的成因：活动场景是"未保存的未命名场景"⇒ additive 建不起来。
                // 这条对账失败**不是错误**（真值标"未取到"、离线列照出），但原因必须说清、还得可操作。
                if (string.IsNullOrEmpty(EditorSceneManager.GetActiveScene().path))
                    error += " · 提示：当前活动场景**尚未保存**（没有路径），additive 临时场景建不起来 ⇒ 先保存宿主场景再对账";
                return false;
            }
            finally
            {
                // ⑤ 收尾，按固定次序（判据要在**动作之后**取，否则会把正常状态当成异常）：
                //    ① 先关掉**自己建的**那一个场景；对象随场景关闭一起没（不做任何按名删除）
                LateClose(tempScene, ref error);
                //    ② 再看它有没有真的关掉、里面还剩没剩东西
                string ownResidue = OwnResidueReport(tempScene, TempObjectName);
                //    ③ 最后核对指纹：宿主场景与其内容必须一比一复原；任何一项不同都如实点名
                SceneFingerprint after = SceneFingerprint.Capture();
                string drift = SceneFingerprint.DescribeDrift(in before, in after, host);
                string report = ownResidue + drift;
                if (!string.IsNullOrEmpty(report))
                    error = (error == null ? string.Empty : error + " · ") + "⚠️ " + report;
            }
        }
    }
}
