namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// 选型表**行高**的常量与算法。
    /// <para>**为什么单独一个类**：<see cref="ScaleTableColumns"/> 已贴 200 行红线，塞不下；
    /// 而"列宽"与"行高"本就是两件事（一个横向分摊、一个纵向分摊）——**按职责拆，不按行数硬压注释**。</para>
    /// <para>🔴 **为什么不放进 <see cref="ScaleCalcTableBinder"/>**：那个类是 <c>internal</c>，测试程序集够不着
    /// （编译报 CS0122）；纯函数住在这里才能在 EditMode 里断言。</para>
    /// <para>背景：条目只有 7 条而主视图有 400 多 px，固定 20px 行高会在表格**内部**留一大片空白。
    /// 改成"按可用高度摊行高"后，条目少而视图高时行会**铺开填满**。</para>
    /// </summary>
    public static class ScaleTableRowHeight
    {
        /// <summary>行高下限：20px —— 与迁移前的固定值一致（一个 Label 的最小舒适高度）。</summary>
        public const float Min = 20f;

        /// <summary>
        /// 行高上限：**30px**。
        /// <para>为什么是 30：逐档裁留图的作画区高度 = 行高 − 2×呼吸位 ⇒ <c>26</c> 时只有 **22px**，
        /// 竖屏档位（信封是正方形）被卡在 22×22；抬到 <c>30</c> 后作画区 **28px**（+27%）⇒ 图明显变大。
        /// **上限的意义不变**：只在**有余量**时铺开，超出的余量仍故意留白，不摊给行（不成"巨带"）。</para>
        /// <para>⚠️ 代价（如实记）：同样高度的表格里**可见行数会少 1 行左右**（<c>360px</c> 的 <c>table-host</c>：
        /// <c>26</c> 时 12 行、<c>30</c> 时 11 行）；挤的时候仍落到 <see cref="Min"/> 20。</para>
        /// </summary>
        public const float Max = 30f;

        /// <summary>表头高度估算（表头取不到实测值，用与档位管理表同一个口径）。</summary>
        public const float HeaderEstimate = 24f;

        /// <summary>
        /// 按可用高度摊行高，夹在 <see cref="Min"/>~<see cref="Max"/>（**纯函数**，不开窗即可断言）。
        /// <para>条目少而视图高 ⇒ 铺开填满；挤不下 ⇒ 不低于下限（宁可让列表自己滚，也不把行压扁）；
        /// 很高 ⇒ 封顶。</para>
        /// <para>🔴 <c>NaN</c> 必须先拦：<c>NaN &lt; x</c> 与 <c>NaN &gt; x</c> **都是 false**，不拦的话 NaN 会原样落到
        /// <c>fixedItemHeight</c> 上（行高变 NaN ⇒ 布局整段报废）。这一条是断言先抓出来的。</para>
        /// </summary>
        public static float For(int itemCount, float availableHeight)
        {
            if (itemCount <= 0 || float.IsNaN(availableHeight)) return Min;
            float perRow = (availableHeight - HeaderEstimate) / itemCount;
            if (perRow < Min) return Min;
            return perRow > Max ? Max : perRow;
        }
    }
}
