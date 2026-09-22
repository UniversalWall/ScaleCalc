using UnityEngine;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// 选型台的**列定义单**（列序固定，**决定性列在前**——不滚动就能读到"会不会裁"）。
    /// <para>🔴 **这是唯一的列定义单**：装配（<see cref="ScaleCalcTableBinder"/>）与断言（Tests）都读它一份。
    /// 它从 `ScaleTableColumns.cs` 里搬出来**不是新增设计**，是 200 行红线逼的。</para>
    /// <para>🔴 **现行列序（10 列）**：行首两列是**两个"一眼判据"列**，其余 8 列沿用原列序：
    /// `①对账`（三态）· `②裁留图`（逐档示意图）· 屏幕档位 · 比例 · 横向 · 纵向 ·
    /// 实际画布 · sf · 屏幕尺寸 · 差值。<b>⚠️ 这 8 列的相对次序**一个都没动**</b>——「裁留图」插在它们之前，
    /// 不是挪进它们中间（有一条断言就是这么守的）。</para>
    /// <para>🔴 沿革：早先定 8 列；插入「对账」⇒ 9 列；追加「裁留图」⇒ 10 列；
    /// 后来现场裁定把「裁留图」**从末列挪到第 2 列** ⇒ 仍是 10 列。
    /// 「同比例」标记并进**比例**列、「方向对」是同一列的后缀 <c>⇄</c>（不许为它另立列）。</para>
    /// </summary>
    public static partial class ScaleTableColumns
    {
        /// <summary>
        /// 10 列：**两个行首判据列**（对账状态 · 逐档裁留图）+ 原来那 8 列。
        /// <para>取值一律走 <see cref="ScaleFit"/> / <see cref="ReconcileVerdict"/>（纯函数）+ 行上的既有字段；
        /// **不许**在这里再算一遍几何量或对账判定。</para>
        /// </summary>
        public static readonly Spec[] All =
        {
            // 🔴 第 1 列：**对账状态**——文字走 `ReconcileVerdict.Text`（纯函数），
            //    悬停与三态配色由装配层按 `ReconcileTitle` 认列后补（`BindReconcileCell`）。
            //    宽度 44~60：单字/`—`，表头两字。
            new Spec(ReconcileTitle, 44f, 60f, row => ReconcileVerdict.Text(row)),

            // 🔴 第 2 列：**逐档裁留示意图**。
            //    取值 = 纯文本摘要（两轴，**与悬停同源**）——它只服务「导出当前视图」的 CSV 与无障碍兜底；
            //    **格内不显示文字**（行高只有 20~30px，长读数在格内必然换行溢出被裁）。
            //    🔴 宽度**不随内容变**（`Spec.FixedWidth` ⇒ `ComputeWidths` 直接取 `MaxWidth = 60`）：
            //    作画区（信封）最宽 ≈ **29px**（超宽 21:9 在 22px 高的作画区里），60 还容得下表头
            //    「裁留图」（3 字 ≈ 36px + `CellPadding` 18px = 54px）⇒ 表头不会被截。
            new Spec(DiagramTitle, 52f, 60f,
                row => ScaleFit.DiagramColumnOf(ScaleFit.Of(in row.ScreenSize, row.Reference)).Text,
                cellKind: ScaleCellKind.Diagram),

            new Spec("屏幕档位", 130f, 280f, row => row.Profile.Name),
            // 上限 130 而不是 90：本列最长单元格是 `16:9 ✅同比例 ⇄`（11 字 ≈ 88px + 18 余量）
            // ⇒ 上限若只有 90，它会**常驻上限**，"按内容推 + 分摊"对它失效，且既有断言「每列都分到一份」会挂
            new Spec("比例", 60f, 130f, row => ScaleFit.AspectText(
                row.Profile,
                ScaleFit.SameAspect(row.Profile.Size, row.Reference),
                row.IsOrientationPair)),
            new Spec(HorizontalTitle, 130f, 240f, row => ScaleFit.Cell(
                ScaleFit.Of(in row.ScreenSize, row.Reference), ScaleFit.Axis.Horizontal), barCell: true),
            new Spec(VerticalTitle, 130f, 240f, row => ScaleFit.Cell(
                ScaleFit.Of(in row.ScreenSize, row.Reference), ScaleFit.Axis.Vertical), barCell: true),
            new Spec("实际画布", 110f, 170f, row => ScaleFit.CanvasText(
                ScaleFit.Of(in row.ScreenSize, row.Reference))),
            // "这是模式列"由第 5 个参数**声明**，不靠表头字符串认
            new Spec("sf（1 画布单位 = ? px）", 200f, 320f,
                row => AxisVerdict.Format(row.ScreenSize.ScaleFactor), ScaleMode.ScaleWithScreenSize),
            new Spec("屏幕尺寸", 90f, 160f, row => row.Profile.Size.ToString()),
            new Spec("差值（仅当前渲染尺寸行）", 160f, 460f, row => row.DeltaText),
        };

        /// <summary>
        /// 列定义里**模式列**的个数（状态栏那句"K 模式"读它，**不写死**）。
        /// <para>🔴 **靠 <see cref="Spec.KernelMode"/> 声明，不靠表头字符串**：模式列的表头
        /// <c>sf（1 画布单位 = ? px）</c> **不以模式成员名开头** ⇒ <c>Enum.TryParse</c> 与"前缀匹配"都会数成 **0**
        /// （状态栏写「0 模式」，是回归）。改成"猜字符串"的两种写法都错，所以改成**声明**。</para>
        /// <para>只扫 <see cref="All"/>。</para>
        /// </summary>
        public static int KernelModeColumnCount()
        {
            int count = 0;
            foreach (Spec spec in All)
                if (spec.KernelMode.HasValue) count++;
            return count;
        }

        /// <summary>
        /// 「对账」列的标题：**装配层按它认列**（<c>BindReconcileCell</c> 要判断"这一列是不是
        /// 状态列"），与 <see cref="HorizontalTitle"/>/<see cref="VerticalTitle"/> 的用法**同构** —— 列定义单是唯一真相源，
        /// 别处不许重抄字面量。
        /// <para>🔴 若要改判成别的字（例如「对账状态」），**只需改这一个常量**（这正是把它做成常量的收益）。</para>
        /// </summary>
        public const string ReconcileTitle = "对账";

        /// <summary>
        /// 「裁留图」列的标题（**3 字**是刻意的：列宽只有 60px）。
        /// <para>🔴 限定语（"只画几何，不承诺具体 UI 不被裁"）与"内框 = 本档画布"**都进单元格悬停**——
        /// 列头只有 3 字，挤不下。</para>
        /// </summary>
        public const string DiagramTitle = "裁留图";

        /// <summary>「横向」列的标题（**带底衬的格按它取轴**——装配层只认这一个常量，不重抄字面量）。</summary>
        public const string HorizontalTitle = "横向";

        /// <summary>「纵向」列的标题。</summary>
        public const string VerticalTitle = "纵向";
    }
}
