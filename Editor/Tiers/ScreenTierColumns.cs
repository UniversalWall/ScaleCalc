using System;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// **档位管理表**的列定义（左栏：全量清单，勾选/删除/新增都在这张表上）。
    /// <para>与选型表（<see cref="ScaleTableColumns"/>）刻意分开：行类型不同（<see cref="ScreenTier"/> vs
    /// <see cref="ScaleTableRow"/>）、职责不同（**维护"要算哪些"** vs **看结果**）。</para>
    /// <para>🔴 **本表不参与列宽分摊**（固定宽度）：两张表同处一个窗口，各自接一个 <c>GeometryChangedEvent</c>
    /// 会互相触发 ⇒ 这里**干脆不接任何回调**，那条风险**结构性消除**。
    /// 代价：右侧可能留白（如实登记）。<see cref="Spec.MinWidth"/>/<see cref="Spec.MaxWidth"/> 仍声明——它们管的是**表头手动拉伸**时的夹取。</para>
    /// </summary>
    public static class ScreenTierColumns
    {
        /// <summary>这一列的单元格里放什么控件（**装配与断言共用同一个判据**——界面断言就按它取列）。</summary>
        public enum CellKind
        {
            /// <summary><c>Toggle</c>：是否参与计算。</summary>
            Include,

            /// <summary><c>TextField</c>：档位名（**就地编辑**，回车/失焦才提交；**内置档也可改名**）。</summary>
            Name,

            /// <summary><c>TextField</c>：屏幕尺寸（就地编辑；**回车/失焦才提交**）。</summary>
            Size,

            /// <summary><c>Button</c>：删除。**内置档也可点**——点了给可读说明（禁用按钮不触发悬停提示）。</summary>
            Actions,
        }

        /// <summary>一列的定义：表头 · 固定宽度 · 夹取区间 · 单元格形态。</summary>
        public sealed class Spec
        {
            public readonly string Title;
            public readonly float Width;
            public readonly float MinWidth;
            public readonly float MaxWidth;
            public readonly CellKind Kind;

            public Spec(string title, CellKind kind, float width, float minWidth, float maxWidth)
            {
                Title = title;
                Kind = kind;
                Width = width;
                MinWidth = minWidth;
                MaxWidth = maxWidth;
            }
        }

        /// <summary>管理表**行高**（装配与高度预算共用这一个数——布局预算不能靠散落的魔数）。</summary>
        public const float RowHeight = 22f;

        /// <summary>
        /// 表头的**经验高度**（用于高度预算）。⚠️ 实测**取不到**内部表头元素（Unity 6 的内部命名与旧版不同），
        /// 所以这是个**保守估算**——宁可少算一行，也不给"把表头也算成能放行"的乐观数。
        /// </summary>
        public const float HeaderHeightEstimate = 24f;

        /// <summary>四列：包含 · 屏幕档位 · 屏幕尺寸 · 操作。</summary>
        public static readonly Spec[] All =
        {
            new Spec("包含", CellKind.Include, 64f, 48f, 90f),
            new Spec("屏幕档位", CellKind.Name, 300f, 130f, 460f),
            new Spec("屏幕尺寸", CellKind.Size, 200f, 120f, 280f),
            new Spec("操作", CellKind.Actions, 150f, 90f, 200f),
        };

        /// <summary>宿主高度**下限**：表头 + 内置 7 行 + 一行余量（低于它连内置档位都看不全——"行数没对上"就是这么来的）。</summary>
        public const float HostMinHeight = 200f;

        /// <summary>宿主高度**最多占可用高度的比例**：给主表（下限 184）与固定段留出位置（预算纪律）。</summary>
        public const float HostMaxHeightRatio = 0.30f;

        /// <summary>行尾余量（一行文字的高度量级；只用于把"刚好装下"算成"装得舒服"）。</summary>
        public const float HostSlack = 20f;

        /// <summary>
        /// 按**条目数**摊管理表宿主的高度（纯函数，可离线断言）：需要多少给多少，
        /// 夹在 `[<see cref="HostMinHeight"/>, 可用高度 × <see cref="HostMaxHeightRatio"/>]`。
        /// <para>🔴 **为什么要夹上限**（它要跟着条目数长，但条目数无上界）：
        /// 内置 7 + 自定义 ≤32 = **≤39** ⇒ 39 行要 900+px，不夹上限就会把主表与底部各段**顶出窗口**
        /// （高度预算的成因正是"预算没算"）。超出上限的部分**由列表自己滚动**。</para>
        /// <para>可用高度未知（尚未布局）⇒ 给下限：宁可先少显示一行，也不在首帧把别人顶出去。</para>
        /// </summary>
        public static float HostHeightFor(int itemCount, float availableHeight)
        {
            float rows = Math.Max(1, itemCount);
            float needed = HeaderHeightEstimate + rows * RowHeight + HostSlack;
            if (float.IsNaN(availableHeight) || availableHeight <= 0f) return HostMinHeight;
            float cap = Math.Max(HostMinHeight, availableHeight * HostMaxHeightRatio);
            return Math.Min(Math.Max(needed, HostMinHeight), cap);
        }

        /// <summary>按单元格形态找列的位置——装配与断言都用它，**不按下标写死**（列序变了也不假通过）。</summary>
        public static int IndexOf(CellKind kind)
        {
            for (int i = 0; i < All.Length; i++) if (All[i].Kind == kind) return i;
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "列定义里没有这种单元格形态");
        }

        /// <summary>取一列的定义。</summary>
        public static Spec Of(CellKind kind) => All[IndexOf(kind)];
    }
}
