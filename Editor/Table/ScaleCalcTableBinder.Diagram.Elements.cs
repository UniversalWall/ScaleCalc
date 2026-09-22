using UnityEngine.UIElements;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// <see cref="ScaleCalcTableBinder"/> 的**逐档示意图：元素形态与常量**章节。
    /// <para>拆的判据 = **200 行红线** + 职责不同：本文件管"**图格长什么样**"（常量 · 造元素 · 悬停写入时机），
    /// `ScaleCalcTableBinder.Diagram.cs` 管"**怎么绑与怎么摆**"（<c>BindDiagramCell</c> / <c>LayoutDiagram</c> / 复位）。</para>
    /// <para>元素契约（**顺序是契约**）：宿主格 <c>cell-diagram</c> → 1 个子元素 = 作画区 <c>cell-diagram-area</c>（= **信封**）；
    /// 作画区内 6 个**绝对定位**子元素：<c>[0..3]</c> 四条差带（左/右/上/下）· <c>[4]</c> 参考框 · <c>[5]</c> 画布框。
    /// 后画的压住先画的 ⇒ **两框描边永远清晰**、色带填在它们之间。</para>
    /// <para>🔴 **不许按"子元素个数"分派**：分派只认**列的种类**（<c>bindCell</c> 用 <c>spec.Kind</c>）；
    /// 子元素个数是各形态的内部实现（图格从 3 个改成 2 个，就让按个数分派的绑定分支整条失效）。
    /// 但**契约仍然要被断言**：<c>ScaleCalcTableCellContractTests</c> 逐列 × 逐行真调 <c>makeCell</c>/<c>bindCell</c>/<c>unbindCell</c>。</para>
    /// <para>🔴 **格内不放文字**（用户裁定）：行高只有 20~30px，两轴读数是
    /// <c>裁 −43.8%（左右各 −420px）</c> 这种长串 ⇒ 必然换行溢出被裁。读数本来就在「横向」/「纵向」两列里，
    /// 且**悬停文本里也有**（带限定语）⇒ 图格只留图，不重复抄数。</para>
    /// </summary>
    public static partial class ScaleCalcTableBinder
    {
        /// <summary>宿主的唯一子元素（作画区 = 信封）的下标。</summary>
        public const int DiagramAreaIndex = 0;

        /// <summary>
        /// 作画区内**两框 + 四条差带**的下标（**顺序是契约**，见类注释；<c>public</c> 是为了让断言按名字取，
        /// 而不是在测试里抄魔数——"契约只写在注释里"正是那类事故的成因）。
        /// </summary>
        public const int DiagramCanvasIndex = 0;
        public const int DiagramBandLeftIndex = 1;
        public const int DiagramBandRightIndex = 2;
        public const int DiagramBandTopIndex = 3;
        public const int DiagramBandBottomIndex = 4;
        public const int DiagramRefIndex = 5;

        /// <summary>作画区内的子元素个数（**契约**：四条带 + 两框；改这里就要同步 <c>MakeDiagramCell</c> 与断言）。</summary>
        public const int DiagramAreaChildCount = 6;

        /// <summary>
        /// 作画区（信封）的**最大宽度**（px）：最宽的信封是「超宽 21:9」那种（**真实宽高比 2.37**）——
        /// 若让高度顶满，宽度会超过列宽。列宽声明 **60**，两侧各留 4px 呼吸位 ⇒ 收到 **52**。
        /// </summary>
        private const float DiagramAreaMaxWidth = 52f;

        /// <summary>
        /// 作画区上下各留的呼吸位（px）：可用高度 = 行高 − 2 × 本值。
        /// <para>🔴 「图再大一点」的调整：由 **2 收到 1**（每一点高度都直接变成图的高度：
        /// 行高 30 时作画区 **28px**，而 2 的话只有 26px）。</para>
        /// </summary>
        private const float DiagramVerticalMargin = 1f;

        /// <summary>作画区的最小高度（px）：行高算不出来时的护栏，**不许出现 0 高**。</summary>
        private const float MinDiagramHeight = 8f;

        private static VisualElement MakeAbsolute(string name, string className)
        {
            var element = new VisualElement { name = name };
            // 🔴 **必须显式 `position: absolute`**（现场实测踩到）：只设 `left/top/width/height`
            //    而漏了 `position` ⇒ 它留在**文档流**里 ⇒ 尺寸/位置都不是"叠在信封里"的那一套。
            element.style.position = Position.Absolute;
            element.AddToClassList(className);
            return element;
        }

        /// <summary>
        /// 造一个**逐档裁留示意图**格：宿主（= 单元格本身，flex 居中）→ 作画区（= 信封）→ 四条差带 + 两框。
        /// <para>🔴 **作画区用 <c>Position.Relative</c> 而不是 <c>Absolute</c>**：它要参与宿主的 flex 居中（横向 + 纵向），
        /// 同时**必须是子元素的定位祖**——<c>InvalidCastException</c> 与"内框出血到邻行"两次都出在
        /// "子元素挂错了父节点"上（百分比是按**父节点**解析的）。</para>
        /// </summary>
        private static VisualElement MakeDiagramCell()
        {
            var host = new VisualElement { name = "cell-diagram" };
            host.style.flexGrow = 1f;
            // 信封在格内横竖居中：**不写 px 偏移** ⇒ 列宽被拖动、行高变化都不会失准
            host.style.justifyContent = Justify.Center;
            host.style.alignItems = Align.Center;

            var area = new VisualElement { name = "cell-diagram-area" };
            area.style.position = Position.Relative;
            area.style.flexShrink = 0f;                 // 显式像素尺寸，不许被 flex 压扁
            area.AddToClassList("cell-diagram-area");
            host.Add(area);

            // 🔴 顺序 = 契约（可读性优化后的口径）：**画布实心块在下** ⇒ **差带在中**
            //    （"留白"的黄段落在画布块**之内**，必须压在实心块上面才看得见）⇒ **参考框描边在最上**
            //    （它是尺子，永远清晰）。旧口径"两框都在最上、带在底下"会把黄段埋进实心画布里。
            area.Add(MakeAbsolute("cell-diagram-canvas", "cell-diagram-canvas"));
            area.Add(MakeAbsolute("cell-diagram-band-l", "cell-diagram-band"));
            area.Add(MakeAbsolute("cell-diagram-band-r", "cell-diagram-band"));
            area.Add(MakeAbsolute("cell-diagram-band-t", "cell-diagram-band"));
            area.Add(MakeAbsolute("cell-diagram-band-b", "cell-diagram-band"));
            area.Add(MakeAbsolute("cell-diagram-ref", "cell-diagram-ref"));

            // 🔴 **弹提示前那一刻再写一次**（现场实测的硬约束）：列表虚拟化会在绑完之后
            //    再 `unbindCell` 一次元素，而 `ClearCell` 会把 `tooltip` 清空 ⇒ **"绑定时设的悬停"
            //    到用户真正悬停时已经没了**。两条路**都挂**（都幂等、成本为零）。
            //    ⚠️ **实测边界**：这两条路都**没法在自动通道里验证**（派发合成事件后读数仍为空）
            //    ⇒ "鼠标悬停会不会真弹"**留给人工一次**。
            host.RegisterCallback<PointerEnterEvent>(_ => ApplyDiagramTooltip(host));
            host.RegisterCallback<TooltipEvent>(evt =>
            {
                ApplyDiagramTooltip(host);
                evt.tooltip = host.tooltip;
            });
            return host;
        }

        /// <summary>把该格缓存的悬停文本写到宿主上（无缓存则不动）。</summary>
        private static void ApplyDiagramTooltip(VisualElement host)
        {
            if (s_Layouts.TryGetValue(host, out ScaleFit.DiagramColumnLayoutBox box))
                host.tooltip = box.Layout.Tooltip;
        }
    }
}
