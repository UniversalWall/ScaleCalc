namespace Wayward.ScaleCalc.Editor
{
    /// <see cref="ScaleCalcWindow"/> 的**选中身份章节**。
    /// <para>🔴 **为什么单独一章**：这里住着**选中身份**（<see cref="m_SelectedTierName"/>）——<c>Rebuild()</c>
    /// 会把整张表重建、<c>selectedIndex</c> 归零，而排序/筛选会让同一档位换行号 ⇒ 窗口只存档位名，重建后交给
    /// 装配层回填（<see cref="ScaleCalcTableBinder.ViewOptions.SelectName"/>）。
    /// **它服务的是"高亮在重建后不丢"**，与任何详情展示无关。</para>
    /// <para>拆出来的理由：把它并进 `ScaleCalcWindow.cs` 会顶破 200 行红线 ⇒ 按职责单立一章
    /// （与 `…Template.cs` / `…Filter.cs` 同一套拆法）。</para>
    /// </summary>
    public sealed partial class ScaleCalcWindow
    {
        /// <summary>当前选中的档位名（**身份，不是行号**）——<c>Rebuild()</c> 之后拿它去恢复选中态。</summary>
        private string m_SelectedTierName;

        /// <summary>选中行变了（或没了）：只记身份。</summary>
        private void OnRowSelectionChanged(ScaleTableRow row)
        {
            m_SelectedTierName = row == null ? null : row.Profile.Name;
        }
    }
}
