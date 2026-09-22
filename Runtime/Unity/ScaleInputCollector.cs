using UnityEngine;
using UnityEngine.UI;

namespace Wayward.ScaleCalc.Unity
{
    /// <summary>
    /// 从 <c>Canvas</c> / <c>CanvasScaler</c> 采集内核输入。
    /// <para>**屏幕尺寸源决策留在这一层**（内核只收数）——这样内核可离线单测，多 Display 的分叉也只在这一个函数里。</para>
    /// </summary>
    public static class ScaleInputCollector
    {
        /// <summary>采集一份内核输入。<c>ScreenDpi</c> 直接读 <c>Screen.dpi</c>（编辑器读数不可信 ⇒ 由证据标注）。</summary>
        public static ScaleCalcInput Collect(Canvas canvas, CanvasScaler scaler)
        {
            return Collect(canvas, scaler, out _);
        }

        /// <inheritdoc cref="Collect(Canvas, CanvasScaler)"/>
        public static ScaleCalcInput Collect(Canvas canvas, CanvasScaler scaler, out ScreenSizeSource source)
        {
            var input = ScaleCalcInput.Default;
            input.ScreenSize = ResolveScreenSize(canvas, out source);
            if (canvas == null || scaler == null) return input;      // 已尽力采集，剩下的交给调用方判定

            input.IsRootCanvas = canvas.isRootCanvas;
            input.RenderMode = ScaleEnumMap.ToCore(canvas.renderMode);
            input.TargetDisplay = canvas.targetDisplay;
            input.ScreenDpi = Screen.dpi;

            input.Mode = ScaleEnumMap.ToCore(scaler.uiScaleMode);
            input.ScreenMatch = ScaleEnumMap.ToCore(scaler.screenMatchMode);
            input.MatchWidthOrHeight = scaler.matchWidthOrHeight;
            input.ReferenceResolution = new ScaleSize(scaler.referenceResolution.x, scaler.referenceResolution.y);
            input.ConstantScaleFactor = scaler.scaleFactor;
            input.FallbackScreenDPI = scaler.fallbackScreenDPI;
            input.DefaultSpriteDPI = scaler.defaultSpriteDPI;
            input.ReferencePixelsPerUnit = scaler.referencePixelsPerUnit;
            input.PhysicalUnit = ScaleEnumMap.ToCore(scaler.physicalUnit);
            input.DynamicPixelsPerUnit = scaler.dynamicPixelsPerUnit;
            return input;
        }

        /// <summary>只要屏幕尺寸（含来源标注）。</summary>
        public static ScaleSize ResolveScreenSize(Canvas canvas, out ScreenSizeSource source)
        {
            if (canvas == null)
            {
                source = ScreenSizeSource.CallerOverride;
                return default;
            }

            int index = canvas.targetDisplay;
            Display[] displays = Display.displays;
            if (index > 0 && displays != null && index < displays.Length)
            {
                source = ScreenSizeSource.DisplayRenderingSize;
                return new ScaleSize(displays[index].renderingWidth, displays[index].renderingHeight);
            }

            source = ScreenSizeSource.CanvasRenderingDisplaySize;
            Vector2 renderingDisplaySize = canvas.renderingDisplaySize;
            return new ScaleSize(renderingDisplaySize.x, renderingDisplaySize.y);
        }
    }
}
