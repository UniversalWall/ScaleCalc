using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// EditMode 夹具：建**真实** <c>Canvas</c> + <c>CanvasScaler</c> 用于"引擎侧"断言（反射等价路径）。
    /// <para>每个用例自己负责 <see cref="TearDown"/>（<c>DestroyImmediate</c>），不留残留对象。</para>
    /// </summary>
    internal static class ScaleCalcTestFixture
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        /// <summary>建一个根 Canvas + CanvasScaler；<paramref name="tweak"/> 用于配置组件。</summary>
        public static (GameObject go, Canvas canvas, UnityEngine.UI.CanvasScaler scaler) CreateScaler(
            ScaleMode mode, Action<UnityEngine.UI.CanvasScaler> tweak = null)
        {
            var go = new GameObject("__ScaleCalc_Tests_Canvas");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = (UnityEngine.UI.CanvasScaler.ScaleMode)(int)mode;
            tweak?.Invoke(scaler);
            return (go, canvas, scaler);
        }

        /// <summary>收尾：销毁夹具对象（<c>null</c> 安全）。</summary>
        public static void TearDown(GameObject go)
        {
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }

        /// <summary>反射调 <c>protected CanvasScaler.Handle()</c>——编辑期等价路径的入口。</summary>
        public static void InvokeHandle(UnityEngine.UI.CanvasScaler scaler) =>
            Invoke(scaler, "Handle");

        /// <summary>反射调 <c>protected SetScaleFactor(float)</c>。</summary>
        public static void InvokeSetScaleFactor(UnityEngine.UI.CanvasScaler scaler, float value) =>
            Invoke(scaler, "SetScaleFactor", value);

        /// <summary>反射调 <c>protected SetReferencePixelsPerUnit(float)</c>。</summary>
        public static void InvokeSetReferencePixelsPerUnit(UnityEngine.UI.CanvasScaler scaler, float value) =>
            Invoke(scaler, "SetReferencePixelsPerUnit", value);

        /// <summary>反射读私有字段（缺字段时**抛出**，不静默返回默认值——降级分支由探针负责，不在夹具里）。</summary>
        public static float GetPrivateFloat(object target, string field)
        {
            FieldInfo f = target.GetType().GetField(field, Private);
            Assert.That(f, Is.Not.Null, $"反射找不到私有字段 {field}（引擎私有字段改名 ⇒ 需要降级分支）");
            return (float)f.GetValue(target);
        }

        private static void Invoke(object target, string method, params object[] args)
        {
            MethodInfo m = target.GetType().GetMethod(method, Private);
            Assert.That(m, Is.Not.Null, $"反射找不到 protected 方法 {method}（引擎私有 API 改名 ⇒ 需要降级分支）");
            m.Invoke(target, args);
        }
    }
}
