using System;
using System.Collections.Generic;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// **呈现层的排序与筛选**。**全部是纯函数**，且**只作用于"看的顺序"**——
    /// 🔴 输入清单（<see cref="ScaleTable.Rows"/>）**一个元素都不动**：模型仍是"清单顺序 + 现场行在首"。
    /// <para>三条纪律：① **现场行（若有）恒在首行**——真值只对它有意义，被排走/筛掉就等于把对账读数藏了；
    /// ② 排序**只按裁切侧**（两种量不许合成）——"最坏裁切"列排的就是裁切量；③ 筛选**只改呈现**，
    /// 结论条/安全设计区**仍按勾选的工作集算**（否则会造出"表里 3 行、结论说参与 7 档"的自欺）。</para>
    /// <para>为什么另立文件：<see cref="ScaleFit.Summary"/> 已 150+ 行，而"排序/筛选"与"跨档统计"是两件事。</para>
    /// </summary>
    public static partial class ScaleFit
    {
        /// <summary>可排序列（与列表的列序一一对应；<c>None</c> = 保持清单顺序，**默认**）。</summary>
        public enum SortKey
        {
            None,
            Name,
            Ratio,
            CropHorizontal,
            CropVertical,
            CanvasWidth,
            ScaleFactor,
            ScreenSize,
            Delta,
        }

        /// <summary>
        /// 列下标 → 排序键（**前两列都是"无排序语义"的判据列** ⇒ 可排的是 **2~9**）。
        /// <para>🔴 下标 <c>0</c>（对账状态）与 <c>1</c>（裁留图）返回 <see cref="SortKey.None"/>：这两列**没有对应的排序
        /// 语义**（一个是三态文字、一个是几何图），点它们的列头只会高亮、不会重排（<c>Column.sortable</c> 在装配层
        /// 是全局 <c>true</c>，且有一条源码级断言要求它"点得动" ⇒ 不改 <c>sortable</c>，如实登记这个无副作用的小瑕疵）。</para>
        /// <para>⚠️ 后 8 列的下标**整体 +2**（那 8 列的相对次序没变，只是前面插了两列）：改列序时
        /// 这张映射与 <c>ScaleFitOrderTests.SortKeys_MapToTheFixedColumnOrder</c> **必须一起改**。</para>
        /// </summary>
        public static SortKey SortKeyOfColumn(int columnIndex)
        {
            switch (columnIndex)
            {
                case 2: return SortKey.Name;
                case 3: return SortKey.Ratio;
                case 4: return SortKey.CropHorizontal;
                case 5: return SortKey.CropVertical;
                case 6: return SortKey.CanvasWidth;
                case 7: return SortKey.ScaleFactor;
                case 8: return SortKey.ScreenSize;
                case 9: return SortKey.Delta;
                default: return SortKey.None;      // 含 0（对账）· 1（裁留图）与越界
            }
        }

        /// <summary>
        /// 首次点列头时的方向：**裁切量列默认降序**（"最坏在前"才是选型要看的东西），
        /// 其余列升序（名字/尺寸从小到大最自然）。同列再点一次 ⇒ 反向（由调用方翻转）。
        /// </summary>
        public static bool DefaultAscending(SortKey key)
            => key != SortKey.CropHorizontal && key != SortKey.CropVertical;

        /// <summary>该行某轴的裁切率（**裁切侧专用**：留白不算进来）。</summary>
        public static float CropRatioOf(ScaleTableRow row, Axis axis)
        {
            if (row == null) return 0f;
            TierFit fit = Of(in row.ScreenSize, row.Reference);
            return axis == Axis.Horizontal ? fit.Horizontal.CropRatio : fit.Vertical.CropRatio;
        }

        /// <summary>该行两轴里更坏的那个裁切率（<see cref="ModeStats.WorstCropRatio"/> 的逐行版，供排序用）。</summary>
        public static float WorstCropRatioOf(ScaleTableRow row)
            => Math.Max(CropRatioOf(row, Axis.Horizontal), CropRatioOf(row, Axis.Vertical));

        /// <summary>
        /// **视图行** = 筛选 + 排序后的**副本**（输入清单原样不动）。
        /// <para>步骤：① 筛选（现场行**永不筛掉**）；② 现场行钉在首行；③ 其余行按 <paramref name="key"/> 排序，
        /// 同键时以**档位名**为次要键（同输入同输出）。</para>
        /// </summary>
        public static List<ScaleTableRow> ViewRows(IReadOnlyList<ScaleTableRow> rows, SortKey key, bool ascending,
                                                   bool onlyCropped, bool onlyPortrait)
        {
            var view = new List<ScaleTableRow>();
            if (rows == null) return view;

            for (int i = 0; i < rows.Count; i++)
            {
                ScaleTableRow row = rows[i];
                if (row == null) continue;
                if (row.IsCurrentRenderSize) { view.Add(row); continue; }        // 现场行恒在首行、且永不筛掉
                if (onlyCropped && WorstCropRatioOf(row) <= 0f) continue;
                if (onlyPortrait && !IsPortrait(row)) continue;
                view.Add(row);
            }

            int start = view.Count > 0 && view[0].IsCurrentRenderSize ? 1 : 0;
            if (key != SortKey.None && view.Count - start > 1)
                SortRange(view, start, key, ascending);
            return view;
        }

        /// <summary>竖屏族：高 ≥ 宽（方屏归竖屏一侧——它既不是横屏也没有"超宽"的意思）。</summary>
        public static bool IsPortrait(ScaleTableRow row)
            => row != null && row.Profile.Size.Height >= row.Profile.Size.Width;

        /// <summary>把 <paramref name="view"/> 的 <c>[start, end)</c> 段排序（同键按档位名，保证确定性）。</summary>
        private static void SortRange(List<ScaleTableRow> view, int start, SortKey key, bool ascending)
        {
            var slice = view.GetRange(start, view.Count - start);
            slice.Sort((a, b) =>
            {
                int order = Compare(a, b, key);
                if (order == 0) order = string.CompareOrdinal(a.Profile.Name, b.Profile.Name);
                return ascending ? order : -order;
            });
            for (int i = 0; i < slice.Count; i++) view[start + i] = slice[i];
        }

        private static int Compare(ScaleTableRow a, ScaleTableRow b, SortKey key)
        {
            switch (key)
            {
                case SortKey.Name: return string.CompareOrdinal(a.Profile.Name, b.Profile.Name);
                case SortKey.Ratio: return a.Profile.AspectRatio.CompareTo(b.Profile.AspectRatio);
                case SortKey.CropHorizontal: return CropRatioOf(a, Axis.Horizontal).CompareTo(CropRatioOf(b, Axis.Horizontal));
                case SortKey.CropVertical: return CropRatioOf(a, Axis.Vertical).CompareTo(CropRatioOf(b, Axis.Vertical));
                case SortKey.CanvasWidth: return CanvasSide(a).CompareTo(CanvasSide(b));
                case SortKey.ScaleFactor: return a.ScreenSize.ScaleFactor.CompareTo(b.ScreenSize.ScaleFactor);
                case SortKey.ScreenSize: return Pixels(a).CompareTo(Pixels(b));
                case SortKey.Delta: return Math.Abs(a.DeltaScaleFactor).CompareTo(Math.Abs(b.DeltaScaleFactor));
                default: return 0;
            }
        }

        private static float CanvasSide(ScaleTableRow row)
            => row.ScreenSize.HasCanvasSize ? row.ScreenSize.CanvasSize.Width : 0f;

        private static float Pixels(ScaleTableRow row) => row.Profile.Size.Width * row.Profile.Size.Height;

        /// <summary>
        /// **会裁档位的下标**（对齐传入清单；"一键取消会裁档位"用它）。
        /// <para>判定复用 <see cref="Of"/>（与表格/结论条**同源**）；无画布或参考不可用的档位**不算会裁**（不编数）。</para>
        /// </summary>
        public static List<int> CroppedIndices(IReadOnlyList<ScreenProfile> tiers, ScaleSize reference,
                                               ScreenMatchMode mode, float match, float dpi)
        {
            var indices = new List<int>();
            if (tiers == null) return indices;
            for (int i = 0; i < tiers.Count; i++)
            {
                ScaleCalcResult result = Evaluate(tiers[i], reference, mode, match, dpi);   // 与 Stats 同一个求值门
                TierFit fit = Of(in result, reference);
                if (fit.HasCanvas && (fit.Horizontal.IsCropped || fit.Vertical.IsCropped)) indices.Add(i);
            }
            return indices;
        }
    }
}
