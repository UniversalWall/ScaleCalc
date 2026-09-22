using UnityEngine;
using UnityEngine.UI;

namespace Wayward.ScaleCalc.Unity
{
    /// <summary>
    /// 引擎枚举 ↔ 内核枚举（**两套枚举之间唯一的转换点**）。
    /// <para>两套枚举的数值已在设计时对齐（声明顺序 = 引擎顺序）⇒ 映射是**恒等转换 + 类型转换**，
    /// 不维护字典：引擎若改了枚举顺序，这里会**编译不过/数值立刻不符**，而不是悄悄错。</para>
    /// </summary>
    public static class ScaleEnumMap
    {
        // ── 引擎 → 内核（采集输入用）────────────────────────────────
        public static ScaleMode ToCore(CanvasScaler.ScaleMode mode) => (ScaleMode)(int)mode;

        public static ScreenMatchMode ToCore(CanvasScaler.ScreenMatchMode mode) => (ScreenMatchMode)(int)mode;

        public static PhysicalUnit ToCore(CanvasScaler.Unit unit) => (PhysicalUnit)(int)unit;

        public static CanvasRenderMode ToCore(RenderMode mode)
        {
            switch (mode)
            {
                case RenderMode.ScreenSpaceOverlay: return CanvasRenderMode.ScreenSpaceOverlay;
                case RenderMode.ScreenSpaceCamera: return CanvasRenderMode.ScreenSpaceCamera;
                default: return CanvasRenderMode.WorldSpace;
            }
        }

        // ── 内核 → 引擎（写回 / 呈现用）──────────────────────────────
        public static CanvasScaler.ScaleMode ToEngine(ScaleMode mode) => (CanvasScaler.ScaleMode)(int)mode;

        public static CanvasScaler.ScreenMatchMode ToEngine(ScreenMatchMode mode) =>
            (CanvasScaler.ScreenMatchMode)(int)mode;

        public static CanvasScaler.Unit ToEngine(PhysicalUnit unit) => (CanvasScaler.Unit)(int)unit;

        public static RenderMode ToEngine(CanvasRenderMode mode)
        {
            switch (mode)
            {
                case CanvasRenderMode.ScreenSpaceOverlay: return RenderMode.ScreenSpaceOverlay;
                case CanvasRenderMode.ScreenSpaceCamera: return RenderMode.ScreenSpaceCamera;
                default: return RenderMode.WorldSpace;
            }
        }
    }
}
