namespace Wayward.ScaleCalc.Unity
{
    /// <summary>采样时机——输出里**必须**标注本行读数是在哪个时机取的。</summary>
    public enum SampleTiming
    {
        /// <summary>组件 <c>Awake</c>：此时渲染尺寸可能还是"未渲染"的默认值。</summary>
        Awake,

        /// <summary>组件 <c>Start</c>。</summary>
        Start,

        /// <summary>每帧末（协程 <c>WaitForEndOfFrame</c>）。</summary>
        EndOfFrame,

        /// <summary>外部显式调用（命令行 / 窗口按钮）。</summary>
        Manual,
    }
}
