using System.Collections.Generic;
using NUnit.Framework;
using Wayward.ScaleCalc.Editor;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// **呈现层的排序与筛选**：全部纯函数断言，不用开窗。
    /// <para>三条硬性质：① **输入清单一个元素都不动**（视图是副本）；② **现场行恒在首、且永不筛掉**；
    /// ③ 排序**只按裁切侧**（裁切与留白是两种量，不许合成）。</para>
    /// </summary>
    public sealed class ScaleFitOrderTests
    {
        private static readonly ScaleSize Reference = new ScaleSize(1920f, 1080f);
        private const float Dpi = 144f;
        private const float Match = 0.5f;

        /// <summary>按内置档位造一张"模型行"清单（真值列空着；排序/筛选不看真值）。</summary>
        private static List<ScaleTableRow> BuiltInRows()
        {
            var rows = new List<ScaleTableRow>();
            foreach (ScreenProfile profile in ScreenProfiles.All)
            {
                ScaleCalcInput input = ScaleCalcInput.Default;
                input.Mode = ScaleMode.ScaleWithScreenSize;
                input.ScreenSize = profile.Size;
                input.ReferenceResolution = Reference;
                input.ScreenMatch = ScreenMatchMode.MatchWidthOrHeight;
                input.MatchWidthOrHeight = Match;
                input.ScreenDpi = Dpi;
                rows.Add(new ScaleTableRow
                {
                    Profile = profile,
                    Reference = Reference,
                    ScreenSize = ScaleCalc.Evaluate(in input),
                });
            }
            return rows;
        }

        private static List<ScaleTableRow> WithLiveRowAtTop()
        {
            List<ScaleTableRow> rows = BuiltInRows();
            ScaleCalcInput liveInput = Input(1234f, 567f);
            var live = new ScaleTableRow
            {
                Profile = new ScreenProfile("当前渲染尺寸（现场）", 1234f, 567f, "现场 Canvas"),
                Reference = Reference,
                IsCurrentRenderSize = true,
                ScreenSize = ScaleCalc.Evaluate(in liveInput),
            };
            rows.Insert(0, live);
            return rows;
        }

        private static ScaleCalcInput Input(float width, float height)
        {
            ScaleCalcInput input = ScaleCalcInput.Default;
            input.Mode = ScaleMode.ScaleWithScreenSize;
            input.ScreenSize = new ScaleSize(width, height);
            input.ReferenceResolution = Reference;
            input.ScreenMatch = ScreenMatchMode.MatchWidthOrHeight;
            input.MatchWidthOrHeight = Match;
            input.ScreenDpi = Dpi;
            return input;
        }

        /// <summary>默认（<c>None</c>）：**保持清单顺序**——模型顺序就是呈现顺序（默认不排序）。</summary>
        [Test]
        public void DefaultOrder_KeepsTheListOrder()
        {
            List<ScaleTableRow> rows = BuiltInRows();
            List<ScaleTableRow> view = ScaleFit.ViewRows(rows, ScaleFit.SortKey.None, true, false, false);

            Assert.That(view.Count, Is.EqualTo(rows.Count));
            for (int i = 0; i < rows.Count; i++) Assert.That(view[i], Is.SameAs(rows[i]), "第 " + i + " 行原样保留顺序");
        }

        /// <summary>排序**不修改输入清单**（视图是副本）——模型仍是"清单顺序 + 现场行在首"。</summary>
        [Test]
        public void Sorting_NeverMutatesTheModelList()
        {
            List<ScaleTableRow> rows = BuiltInRows();
            string[] before = new string[rows.Count];
            for (int i = 0; i < rows.Count; i++) before[i] = rows[i].Profile.Name;

            List<ScaleTableRow> view = ScaleFit.ViewRows(rows, ScaleFit.SortKey.CropHorizontal, false, false, false);

            for (int i = 0; i < rows.Count; i++)
                Assert.That(rows[i].Profile.Name, Is.EqualTo(before[i]), "输入清单第 " + i + " 行不许被排序动过");
            Assert.That(view.Count, Is.EqualTo(rows.Count), "视图只是换了个顺序");
        }

        /// <summary>按「横向裁切」降序 ⇒ 最坏的在前；**只按裁切侧**（留白不参与）。</summary>
        [Test]
        public void WorstCropDescending_PutsTheWorstFirst()
        {
            List<ScaleTableRow> view = ScaleFit.ViewRows(BuiltInRows(), ScaleFit.SortKey.CropHorizontal, false, false, false);

            Assert.That(view[0].Profile.Name, Is.EqualTo("手机竖屏超长 9:19.5"), "最坏：横向 −49.0%");
            for (int i = 1; i < view.Count; i++)
                Assert.That(ScaleFit.WorstCropRatioOf(view[i - 1]), Is.GreaterThanOrEqualTo(ScaleFit.WorstCropRatioOf(view[i]) - 1e-6f),
                    "降序：前一行不许比后一行好（留 1 ULP：平板 4:3 与小屏 4:3 比例相同 ⇒ 裁切量相等，靠次要键定序）");
        }

        /// <summary>**现场行恒在首行**：排序不把它排走（真值只对它有意义）。</summary>
        [Test]
        public void LiveRow_StaysOnTop_EvenWhenSorting()
        {
            List<ScaleTableRow> rows = WithLiveRowAtTop();
            List<ScaleTableRow> view = ScaleFit.ViewRows(rows, ScaleFit.SortKey.CropHorizontal, false, false, false);

            Assert.That(view[0].IsCurrentRenderSize, Is.True, "现场行还在首行");
            Assert.That(view[0].Profile.Name, Is.EqualTo("当前渲染尺寸（现场）"));
        }

        /// <summary>筛选：只看会裁 / 只看竖屏 —— **现场行永不筛掉**；其余按条件过滤。</summary>
        [Test]
        public void Filters_NarrowTheView_ButNeverDropTheLiveRow()
        {
            List<ScaleTableRow> rows = WithLiveRowAtTop();

            List<ScaleTableRow> cropped = ScaleFit.ViewRows(rows, ScaleFit.SortKey.None, true, onlyCropped: true, onlyPortrait: false);
            Assert.That(cropped[0].IsCurrentRenderSize, Is.True, "现场行不会被筛掉（它就是那个 1234×567）");
            Assert.That(cropped.Count, Is.LessThan(rows.Count), "确实筛掉了一些");
            foreach (ScaleTableRow row in cropped)
                Assert.That(row.IsCurrentRenderSize || ScaleFit.WorstCropRatioOf(row) > 0f, row.Profile.Name + "：只看会裁");

            List<ScaleTableRow> portrait = ScaleFit.ViewRows(rows, ScaleFit.SortKey.None, true, onlyCropped: false, onlyPortrait: true);
            Assert.That(portrait[0].IsCurrentRenderSize, Is.True);
            foreach (ScaleTableRow row in portrait)
                Assert.That(row.IsCurrentRenderSize || ScaleFit.IsPortrait(row), row.Profile.Name + "：只看竖屏（高 ≥ 宽）");
        }

        /// <summary>`SortKeyOfColumn`（列序固定：前两列是「对账」与「裁留图」两个判据列 ⇒ 可排的是 **2~9**）
        /// 与默认方向：**裁切量列默认降序**。</summary>
        [Test]
        public void SortKeys_MapToTheFixedColumnOrder()
        {
            Assert.That(ScaleFit.SortKeyOfColumn(0), Is.EqualTo(ScaleFit.SortKey.None), "行首「对账」列没有排序语义");
            Assert.That(ScaleFit.SortKeyOfColumn(1), Is.EqualTo(ScaleFit.SortKey.None), "第 2 列「裁留图」也没有");
            Assert.That(ScaleFit.SortKeyOfColumn(2), Is.EqualTo(ScaleFit.SortKey.Name));
            Assert.That(ScaleFit.SortKeyOfColumn(4), Is.EqualTo(ScaleFit.SortKey.CropHorizontal));
            Assert.That(ScaleFit.SortKeyOfColumn(5), Is.EqualTo(ScaleFit.SortKey.CropVertical));
            Assert.That(ScaleFit.SortKeyOfColumn(9), Is.EqualTo(ScaleFit.SortKey.Delta));
            Assert.That(ScaleFit.SortKeyOfColumn(-1), Is.EqualTo(ScaleFit.SortKey.None), "越界 ⇒ 不排序");
            Assert.That(ScaleFit.SortKeyOfColumn(99), Is.EqualTo(ScaleFit.SortKey.None));

            Assert.That(ScaleFit.DefaultAscending(ScaleFit.SortKey.CropHorizontal), Is.False, "最坏在前才是选型要看的");
            Assert.That(ScaleFit.DefaultAscending(ScaleFit.SortKey.CropVertical), Is.False);
            Assert.That(ScaleFit.DefaultAscending(ScaleFit.SortKey.Name), Is.True);
            Assert.That(ScaleFit.DefaultAscending(ScaleFit.SortKey.ScaleFactor), Is.True);
        }

        /// <summary>`CroppedIndices` 与结论条**同源**：内置 7 档 match 0.5 ⇒ 6 档；`Expand` ⇒ 0 档。</summary>
        [Test]
        public void CroppedIndices_AgreeWithTheHeadline()
        {
            IReadOnlyList<ScreenProfile> tiers = ScreenProfiles.All;
            List<int> cropped = ScaleFit.CroppedIndices(tiers, Reference, ScreenMatchMode.MatchWidthOrHeight, Match, Dpi);
            ScaleFit.ModeStats stats = ScaleFit.Stats(tiers, Reference, ScreenMatchMode.MatchWidthOrHeight, Match, Dpi);

            Assert.That(cropped.Count, Is.EqualTo(stats.CroppedCount), "逐档判定与跨档统计必须数得一样多");
            Assert.That(cropped.Count, Is.EqualTo(6));
            Assert.That(ScaleFit.CroppedIndices(tiers, Reference, ScreenMatchMode.Expand, Match, Dpi), Is.Empty,
                "Expand ⇒ 一档都不裁（结构性保证）");
        }

        /// <summary>同键时按档位名 ⇒ **同输入同输出**（两次调用结果一致）。</summary>
        [Test]
        public void Sorting_IsDeterministic_OnTies()
        {
            List<ScaleTableRow> first = ScaleFit.ViewRows(BuiltInRows(), ScaleFit.SortKey.Ratio, false, false, false);
            List<ScaleTableRow> second = ScaleFit.ViewRows(BuiltInRows(), ScaleFit.SortKey.Ratio, false, false, false);

            for (int i = 0; i < first.Count; i++)
                Assert.That(second[i].Profile.Name, Is.EqualTo(first[i].Profile.Name), "同输入同输出");
        }
    }
}
