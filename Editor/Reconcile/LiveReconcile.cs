using System.Globalization;
using Wayward.ScaleCalc.Unity;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// **现场对账协议的可复用实现**：additive 临时场景 → 根 Canvas + CanvasScaler
    /// → **显式反射 <c>Handle()</c>** → 读三项真值 → 关场景。**单次原子完成**，宿主场景不留脏标记、不留残骸。
    /// <para>🔴 **副作用所有权**：本协议**只**操作自己创建的临时场景，
    /// **不删除任何对象、不关闭任何不是自己建的场景、不改变活动场景**。
    /// 宿主三条硬前置：有效 · 已保存 · 就是当前活动场景——都在任何动作之前判定。</para>
    /// <para>本类分三个文件（拆的判据 = 200 行红线）：本文件（真值类型 + 类注释）·
    /// <c>LiveReconcile.Measure.cs</c>（测量章节）· <c>LiveReconcile.Cleanup.cs</c>（清场与指纹章节）。</para>
    /// <para>选型台的真值列、现场对账按钮与证据导出都吃这一个入口。</para>
    /// </summary>
    public static partial class LiveReconcile
    {
    }
}
