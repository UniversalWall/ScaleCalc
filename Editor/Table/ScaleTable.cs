using System.Collections.Generic;
using System.Diagnostics;
using Wayward.ScaleCalc.Unity;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// 选型台的整张表。**算法全在内核**，这里只做装配（平台《编辑器工具规范》§3.1：窗口只做 bind/呈现）。
    /// </summary>
    public sealed class ScaleTable
    {
        public ScaleSize Reference;
        public ScreenMatchMode ScreenMatch;
        public float MatchWidthOrHeight;
        public float PhysicalDpi;

        public readonly List<ScaleTableRow> Rows = new List<ScaleTableRow>();

        /// <summary>本表里是否存在"当前渲染尺寸那一行"（没有 ⇒ 真值列整列为空）。</summary>
        public bool HasTruthRow;

        /// <summary>真值可得性口径的说明（**不许假装整张表都对过账**）。</summary>
        public string TruthNote;

        /// <summary>内核部分的全表重算耗时（>16ms 才考虑增量重算——先保持简单全算）。</summary>
        public double BuildMilliseconds;

        /// <summary>
        /// 装配整张表。<paramref name="truthValid"/> 为 <c>true</c>**且**这份真值的**测量条件与本表一致**
        /// （<c>truth.AppliesTo</c>）时：**尺寸等于当次现场屏幕尺寸**的那一行才带真值；若该尺寸不在档位表里，
        /// 会在表头**补一行现场行**（否则真值列永远是空的，"只对当前行有值"就无从体现）。
        /// <para>⚠️ 条件不符时**整表离线**——真值是在"某参考分辨率 × 某 match × 某匹配方式"下测出来的，
        /// 拿它去减别的一组条件会得到假差值。这时 <see cref="TruthNote"/> 会写明"真值是什么条件下测的"。</para>
        /// <para><b>行从哪来</b>：<paramref name="profiles"/> 为 <c>null</c> ⇒ 沿用内置清单
        /// <see cref="ScreenProfiles.All"/>（证据包与既有调用零改动）；传进来 ⇒ 就按它渲染
        /// （工作集收窄后选型表只渲染被勾选的档位）。<b>现场行的判据不受它影响</b>：只看现场尺寸在不在
        /// <b>这份</b>清单里。</para>
        /// <para>🔴 **<paramref name="reconcileContext"/> 不给默认值**：
        /// 它承载"整表为什么有/没有真值"，会逐行写进 <see cref="ScaleTableRow.Reconcile"/> 供**对账状态列
        /// 的悬停**使用。给默认值会让漏改的调用点**编译通过**却产出"所有行都没对过账"的假语境
        /// （与 <see cref="LiveTruth"/> 那条教训同族：默认构造的真值不适用于任何条件）。⇒ **编译器强制更新全部调用点**。</para>
        /// </summary>
        public static ScaleTable Build(ScaleSize reference, ScreenMatchMode screenMatch, float matchWidthOrHeight,
                                       float physicalDpi, ScaleMode truthMode, LiveTruth truth, bool truthValid,
                                       ReconcileContext reconcileContext,
                                       IReadOnlyList<ScreenProfile> profiles = null)
        {
            var watch = Stopwatch.StartNew();
            var table = new ScaleTable
            {
                Reference = reference,
                ScreenMatch = screenMatch,
                MatchWidthOrHeight = matchWidthOrHeight,
                PhysicalDpi = physicalDpi,
            };

            bool applicable = truthValid && truth.AppliesTo(reference, matchWidthOrHeight, screenMatch);
            bool usable = applicable && truthMode == ScaleMode.ScaleWithScreenSize;

            IReadOnlyList<ScreenProfile> list = profiles ?? ScreenProfiles.All;
            foreach (ScreenProfile profile in list)
            {
                ScaleTableRow row = BuildRow(profile, reference, screenMatch, matchWidthOrHeight, physicalDpi,
                                            ScaleFit.HasOrientationPair(profile, list), in reconcileContext);

                // 真值只给"当前渲染尺寸那一行"——尺寸比对是**逐位**的（档位表里那一行写的正是这个尺寸）
                if (usable && profile.Size == truth.ScreenSize)
                {
                    ApplyTruth(row, truth);
                    table.HasTruthRow = true;
                }
                table.Rows.Add(row);
            }

            if (usable && !table.HasTruthRow)
            {
                var live = new ScreenProfile("当前渲染尺寸（现场）", truth.ScreenSize.Width, truth.ScreenSize.Height, "现场 Canvas");
                ScaleTableRow liveRow = BuildRow(live, reference, screenMatch, matchWidthOrHeight, physicalDpi,
                                                 ScaleFit.HasOrientationPair(live, list), in reconcileContext);
                ApplyTruth(liveRow, truth);
                table.Rows.Insert(0, liveRow);
                table.HasTruthRow = true;
            }

            table.TruthNote = !truthValid
                ? "本次没取到现场真值（编辑期未渲染 / 反射不可用）⇒ 整表都是离线列"
                : !applicable
                    ? "现场真值是在「" + truth.ConditionText + "」下测的，与本表条件（"
                      + LiveTruth.ConditionTextOf(reference, screenMatch, matchWidthOrHeight)
                      + "）不同 ⇒ 整表离线列（不许拿别的条件的真值来比差）"
                    : table.HasTruthRow
                        ? "真值列只对「当前渲染尺寸」这一行有值（尺寸 " + truth.ScreenSize + "，源 " + truth.Source + "）；其余 " + (table.Rows.Count - 1) + " 行是离线列"
                        : "当次现场屏幕尺寸 " + truth.ScreenSize + " 不在档位表里 ⇒ 本表没有任何一行带真值（离线列）";

            watch.Stop();
            table.BuildMilliseconds = watch.Elapsed.TotalMilliseconds;
            return table;
        }

        private static ScaleTableRow BuildRow(ScreenProfile profile, ScaleSize reference, ScreenMatchMode screenMatch,
                                              float matchWidthOrHeight, float physicalDpi, bool isOrientationPair,
                                              in ReconcileContext reconcileContext)
        {
            ScaleCalcInput baseInput = ScaleCalcInput.Default;
            baseInput.ScreenSize = profile.Size;
            baseInput.ReferenceResolution = reference;
            baseInput.ScreenMatch = screenMatch;
            baseInput.MatchWidthOrHeight = matchWidthOrHeight;
            baseInput.ScreenDpi = physicalDpi;

            ScaleCalcInput pixel = baseInput;
            pixel.Mode = ScaleMode.ConstantPixelSize;

            ScaleCalcInput screen = baseInput;
            screen.Mode = ScaleMode.ScaleWithScreenSize;

            ScaleCalcInput physical = baseInput;
            physical.Mode = ScaleMode.ConstantPhysicalSize;

            return new ScaleTableRow
            {
                Profile = profile,
                Reference = reference,
                IsOrientationPair = isOrientationPair,
                Reconcile = reconcileContext,          // ← 整表语境随行带出（状态列的悬停要用它）
                PixelSize = ScaleCalc.Evaluate(in pixel),
                ScreenSize = ScaleCalc.Evaluate(in screen),
                PhysicalSize = ScaleCalc.Evaluate(in physical),
            };
        }

        private static void ApplyTruth(ScaleTableRow row, LiveTruth truth)
        {
            row.IsCurrentRenderSize = true;
            row.HasTruth = true;
            row.TruthScaleFactor = truth.ScaleFactor;
            row.TruthReferencePpu = truth.ReferencePixelsPerUnit;
            row.TruthCanvasSize = truth.CanvasSize;
            row.TruthSource = truth.Source;
            row.DeltaScaleFactor = row.ScreenSize.ScaleFactor - truth.ScaleFactor;
            row.DeltaReferencePpu = row.ScreenSize.ReferencePixelsPerUnit - truth.ReferencePixelsPerUnit;
            row.DeltaCanvasWidth = row.ScreenSize.CanvasSize.Width - truth.CanvasSize.Width;
            row.DeltaCanvasHeight = row.ScreenSize.CanvasSize.Height - truth.CanvasSize.Height;
        }
    }
}
