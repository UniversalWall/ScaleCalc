using UnityEngine;
using UnityEngine.UIElements;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// <see cref="ScaleCalcWindow"/> 的**字符条块章节**。
    /// <para>⚠️ 文件名带着历史：这里原来还住着**分支栏**与**总结行**两个方法，连同它们的 <c>.uxml</c> 节点与
    /// <c>.uss</c> 规则**整条撤除**了——"这些文本详情没必要"。**信息没消失**：真值来历与"为什么整表没有真值"
    /// 改由**对账状态列的悬停**承担（见 <see cref="ReconcileVerdict"/> / <see cref="ReconcileContext"/>），
    /// 兜底链诊断仍在证据包 CSV（<see cref="PhysicsFallbackDiagnostic"/>）里。</para>
    /// <para>⚠️ 因此**本文件的职责只有"字符条"一件事**——按这个来读它，别被文件名误导。</para>
    /// </summary>
    public sealed partial class ScaleCalcWindow
    {
        /// <summary>字符条块标题里写的档位数（<see cref="VerdictBarsText"/> 只画最坏的这么多档）。</summary>
        public const int BarRows = 3;

        /// <summary>
        /// **字符条块**：一行一档，只画"最需要注意的 <see cref="BarRows"/> 档"。
        /// <para>🔴 **为什么只画最坏的 3 档**（而不是逐档一行）：一行 ≈18px，39 档就是 **700px**
        /// ——高度预算是硬约束，而这条的用途本来就只是"一眼看出哪档最惨"，**最坏的几档就是那几档**
        /// （精确读数永远看数值列，字符条不参与任何判据）。</para>
        /// <para>横向条（按宽轴）；排序 = 裁切量降序、裁切之后才是留白量降序（<see cref="ScaleFit.BarSeverity"/>，**两种量不合成分**）。</para>
        /// <para>⚠️ **已知缺陷（未解决）**：本方法算得出 156 字符，而界面上 <c>verdict-bars</c> 的
        /// <c>worldBound.height</c> 实测为 **0**（且把窗口拉到 900×1400 仍为 0 ⇒ 排除高度预算与 flex 收缩）
        /// ⇒ 用户看不见它。**尚未做针对性修复**，只做"改动前后各测一次"的如实记录。</para>
        /// </summary>
        public static string VerdictBarsText(System.Collections.Generic.IReadOnlyList<ScreenProfile> tiers,
                                             ScaleSize reference, ScreenMatchMode mode, float match, float dpi,
                                             int rows = BarRows, int cells = ScaleFit.BarDefaultCells)
        {
            if (tiers == null || tiers.Count == 0) return "字符条：无参与档位（上表可勾回）";
            if (!ScaleFit.IsUsable(reference)) return "字符条：参考分辨率无效 ⇒ 不画";

            var order = new System.Collections.Generic.List<ScaleFit.TierFit>();
            var names = new System.Collections.Generic.List<string>();
            foreach (ScreenProfile profile in tiers)
            {
                order.Add(ScaleFit.OfTier(profile, reference, mode, match, dpi));
                names.Add(profile.Name);
            }

            var indices = new System.Collections.Generic.List<int>();
            for (int i = 0; i < order.Count; i++) indices.Add(i);
            indices.Sort((a, b) =>
            {
                int cmp = ScaleFit.BarSeverity(order[b], ScaleFit.Axis.Horizontal)
                    .CompareTo(ScaleFit.BarSeverity(order[a], ScaleFit.Axis.Horizontal));
                return cmp != 0 ? cmp : string.CompareOrdinal(names[a], names[b]);
            });

            int take = System.Math.Min(System.Math.Max(1, rows), indices.Count);
            string text = "字符条（最需要注意的 " + take + " 档 · 横向：短 = 裁、越过 ┊ = 留）";
            for (int i = 0; i < take; i++)
            {
                int index = indices[i];
                ScaleFit.TierFit fit = order[index];
                text += "\n" + names[index] + "  " + ScaleFit.Bar(in fit, ScaleFit.Axis.Horizontal, cells)
                      + "  " + ScaleFit.BarLabel(in fit, ScaleFit.Axis.Horizontal);
            }
            return text;
        }
    }
}
