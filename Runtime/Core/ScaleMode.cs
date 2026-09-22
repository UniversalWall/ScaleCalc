namespace Wayward.ScaleCalc
{
    /// <summary>缩放模式。**数值与 <c>UnityEngine.UI.CanvasScaler.ScaleMode</c> 一致**（映射即恒等转换）。</summary>
    public enum ScaleMode
    {
        ConstantPixelSize = 0,
        ScaleWithScreenSize = 1,
        ConstantPhysicalSize = 2,
    }
}
