namespace Wayward.ScaleCalc
{
    /// <summary>
    /// <c>CanvasScaler</c> 这次到底走了哪条分支。两处早退各占一支。
    /// <para>判定**只在内核里做一次**：界面与对账都只读这个值，不再自己判
    /// （否则"设置明明改了、读数却没变"这种事没法一眼看出来是踩了早退）。</para>
    /// </summary>
    public enum ScaleBranch
    {
        /// <summary>早退①：子 Canvas 上的 <c>CanvasScaler</c> 完全不生效（读数继承自**根** Canvas）。</summary>
        EarlyExit_NotRootCanvas,

        /// <summary>早退②：世界空间画布，走 <c>dynamicPixelsPerUnit</c>，Screen 那套公式不参与。</summary>
        WorldSpace,

        ConstantPixelSize,
        ScaleWithScreenSize,
        ConstantPhysicalSize,
    }
}
