using UnityEngine;
using UnityEngine.UI;

namespace Wayward.ScaleCalc.Unity
{
    /// <summary>屏幕尺寸取自哪里——**证据包里的"输入源"那一列**；只存在于适配层，不进内核输入面。</summary>
    public enum ScreenSizeSource
    {
        /// <summary>主 Display：取 <c>Canvas.renderingDisplaySize</c>（引擎 <c>HandleScaleWithScreenSize</c> 的主路径）。</summary>
        CanvasRenderingDisplaySize,

        /// <summary>非主 Display：取 <c>Display.displays[idx].rendering*</c>。</summary>
        DisplayRenderingSize,

        /// <summary>调用方直接给数（离线算表 / 测试注入）。</summary>
        CallerOverride,
    }
}
