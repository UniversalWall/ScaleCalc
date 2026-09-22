using NUnit.Framework;
using Wayward.ScaleCalc.Editor;
using Wayward.ScaleCalc.Unity;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// **逐档**拟合与呈现文本：纯函数断言，**不用开窗**。
    /// <para>量值逐轴/不合成/不编数 · 容差边界 · 同比例精确 · 方向对 · 比例文本。
    /// **跨档统计**（安全区/推荐值/结论条）另立 <see cref="ScaleFitSummaryTests"/>。</para>
    /// </summary>
    public sealed class ScaleFitTests
    {
        private static readonly ScaleSize Reference = new ScaleSize(1920f, 1080f);
        private const float Dpi = 144f;
        private const float Match = 0.5f;

        private static ScaleCalcResult Evaluate(ScaleSize screen)
        {
            ScaleCalcInput input = ScaleCalcInput.Default;
            input.Mode = ScaleMode.ScaleWithScreenSize;
            input.ScreenSize = screen;
            input.ReferenceResolution = Reference;
            input.ScreenMatch = ScreenMatchMode.MatchWidthOrHeight;
            input.MatchWidthOrHeight = Match;
            input.ScreenDpi = Dpi;
            return ScaleCalc.Evaluate(in input);
        }

        /// <summary>`PK2`：裁切与留白**逐轴**给、**不合成单值**；同一轴上两者不可能同时 &gt; 0。</summary>
        [Test]
        public void PK2_AxisFit_IsPerAxis_AndNeverMixesCropWithSpare()
        {
            // 用**内置「平板 4:3」的真实尺寸**（2048×1536）——它的画布实测是 1663×1247；
            // 换成手写 1663×1247 会因浮点误差把 83.5 变成 83.4999。
            ScaleCalcResult canvas = Evaluate(new ScaleSize(2048f, 1536f));
            ScaleFit.TierFit mixed = ScaleFit.Of(in canvas, Reference);

            Assert.That(mixed.HasCanvas, Is.True);
            Assert.That(ScaleFit.SizeText(mixed.Canvas), Is.EqualTo("1663×1247"), "实际画布（**取整显示**）");
            Assert.That(mixed.Horizontal.IsCropped, Is.True, "横向：画布窄于参考 ⇒ 裁切");
            Assert.That(mixed.Horizontal.SpareRatio, Is.EqualTo(0f), "同一轴不许同时有留白");
            Assert.That(mixed.Vertical.IsSpare, Is.True, "纵向：画布高于参考 ⇒ 留白");
            Assert.That(mixed.Vertical.CropRatio, Is.EqualTo(0f));
            Assert.That(mixed.Horizontal.CropRatio, Is.EqualTo(0.134f).Within(0.001f),
                "画布宽实测 1662.77（界面上取整显示成 1663）⇒ 裁 13.4%");
            Assert.That(mixed.Horizontal.CropPxPerSide, Is.EqualTo(128.6f).Within(0.5f), "px 一律**单侧**（左右各）");
            Assert.That(mixed.Vertical.SparePxPerSide, Is.EqualTo(83.7f).Within(0.5f), "上下各（不是总差 167）");
            // 🔴 逐字钉住**真实档位**（平板 4:3 的画布实测就是 1663×1247）看到的文本：
            //    px 四舍五入、**半像素进位**（128.5 → 129；.NET Core 的 `F0` 是「远离零」）
            Assert.That(ScaleFit.Cell(in mixed, ScaleFit.Axis.Horizontal), Is.EqualTo("裁 −13.4%（左右各 −129px）"),
                "逐字：px 四舍五入到整数（−129px 是 128.5 按「远离零」进位的结果）");
            Assert.That(ScaleFit.Cell(in mixed, ScaleFit.Axis.Vertical), Is.EqualTo("留 +15.5%（上下各 +84px）"));
            Assert.That(ScaleFit.Percent1(0.509375f), Is.EqualTo("50.9%"), "百分比坑：不许出现 5,095 %");

            ScaleCalcResult exact = Evaluate(Reference);
            ScaleFit.TierFit same = ScaleFit.Of(in exact, Reference);
            Assert.That(same.Horizontal.Verdict, Is.EqualTo(OverflowVerdict.Exactly));
            Assert.That(same.SameAspect, Is.True);
            Assert.That(ScaleFit.Cell(in same, ScaleFit.Axis.Horizontal), Is.EqualTo("正好"));
        }

        /// <summary>`PK2`（反例）：参考 ≤ 0 / 非有限 / 无画布 ⇒ **不编数**。</summary>
        [Test]
        public void PK2_UndefinedInputs_ProduceMarkersInsteadOfNumbers()
        {
            ScaleCalcResult result = Evaluate(Reference);

            ScaleFit.TierFit zeroReference = ScaleFit.Of(in result, new ScaleSize(0f, 1080f));
            Assert.That(ScaleFit.Cell(zeroReference, ScaleFit.Axis.Horizontal), Is.EqualTo("—"), "参考 ≤ 0 ⇒ 量值显示 —");
            Assert.That(ScaleFit.Cell(zeroReference, ScaleFit.Axis.Vertical), Is.EqualTo("—"));

            ScaleFit.TierFit nanReference = ScaleFit.Of(in result, new ScaleSize(float.NaN, float.NaN));
            Assert.That(ScaleFit.Cell(nanReference, ScaleFit.Axis.Horizontal), Is.EqualTo("—"), "NaN 参考不许扩散成 NaN 文本");

            ScaleCalcInput worldInput = ScaleCalcInput.Default;
            worldInput.Mode = ScaleMode.ScaleWithScreenSize;
            worldInput.RenderMode = CanvasRenderMode.WorldSpace;
            worldInput.ScreenSize = Reference;
            worldInput.ReferenceResolution = Reference;
            ScaleCalcResult world = ScaleCalc.Evaluate(in worldInput);
            ScaleFit.TierFit noCanvas = ScaleFit.Of(in world, Reference);

            Assert.That(noCanvas.HasCanvas, Is.False, "WorldSpace ⇒ 画布尺寸无定义");
            Assert.That(ScaleFit.Cell(noCanvas, ScaleFit.Axis.Horizontal), Is.EqualTo("（无画布尺寸）"));
            Assert.That(ScaleFit.CanvasText(noCanvas), Is.EqualTo("（无画布尺寸）"));
        }

        /// <summary>`PK3`：浮点边界（`1919.9999` 这类）**不许**报"裁切"——这是设计期实测过的假警报。</summary>
        [Test]
        public void PK3_ToleranceSwallowsFloatingPointBoundary()
        {
            ScaleFit.AxisFit justUnder = ScaleFit.OfAxis(1919.9999f, 1920f);
            Assert.That(justUnder.IsCropped, Is.False, "差 1e-4 在容差 0.5 内 ⇒ 正好");
            Assert.That(justUnder.IsSpare, Is.False);
            Assert.That(ScaleFit.OfAxis(1919.4f, 1920f).IsCropped, Is.True, "差 0.6 > 容差 ⇒ 真裁");
            Assert.That(ScaleFit.OfAxis(1920.6f, 1920f).IsSpare, Is.True);

            var swallowed = new ScaleFit.TierFit(true, new ScaleSize(1919.9999f, 1080f),
                ScaleFit.OfAxis(1919.9999f, 1920f), ScaleFit.OfAxis(1080f, 1080f), false);
            Assert.That(ScaleFit.Cell(in swallowed, ScaleFit.Axis.Horizontal), Is.EqualTo("正好"), "单元格文案也走同一个容差");
        }

        /// <summary>`PK23`：同比例是**精确**口径（绝对阈值 &lt; 1）——`1921×1081` 必须判**不同**比例。</summary>
        [Test]
        public void PK23_SameAspect_IsExact_NotApproximate()
        {
            Assert.That(ScaleFit.SameAspect(new ScaleSize(1920f, 1080f), Reference), Is.True);
            Assert.That(ScaleFit.SameAspect(new ScaleSize(1921f, 1081f), Reference), Is.False, "看着像 16:9，但不是");
            Assert.That(ScaleFit.SameAspect(new ScaleSize(1080f, 1920f), Reference), Is.False, "换向**不算**同比例");
            Assert.That(ScaleFit.SameAspect(new ScaleSize(0f, 0f), Reference), Is.False, "无定义 ⇒ 不许判同比例");
        }

        /// <summary>`PK21`：方向对（同像素数 + 宽高互换）是纯函数，内置清单里那一对必须命中。</summary>
        [Test]
        public void PK21_OrientationPair_IsPureAndHitsTheBuiltInPair()
        {
            ScreenProfile portrait = ScreenProfiles.All[0];   // 手机竖屏 9:16 = 1080×1920
            ScreenProfile landscape = ScreenProfiles.All[2];  // 手机横屏 16:9 = 1920×1080

            Assert.That(portrait.Size, Is.EqualTo(new ScaleSize(1080f, 1920f)), "前置：内置清单第 1 档");
            Assert.That(landscape.Size, Is.EqualTo(new ScaleSize(1920f, 1080f)), "前置：内置清单第 3 档");
            Assert.That(ScaleFit.AreOrientationPair(portrait, landscape), Is.True);
            Assert.That(ScaleFit.AreOrientationPair(landscape, portrait), Is.True, "对称");
            Assert.That(ScaleFit.AreOrientationPair(portrait, portrait), Is.False);
            Assert.That(ScaleFit.AreOrientationPair(ScreenProfiles.All[4], ScreenProfiles.All[4]), Is.False, "1:1 方屏没有两个方向");
            Assert.That(ScaleFit.AreOrientationPair(portrait, ScreenProfiles.All[1]), Is.False);
        }

        /// <summary>比例文本（「比例」列）：**由像素算**的最小整数比——档位名与像素比不一致时，以像素为准。</summary>
        [Test]
        public void RatioText_ComesFromPixels_NotFromTheTierName()
        {
            Assert.That(ScaleFit.RatioText(16f / 9f), Is.EqualTo("16:9"));
            Assert.That(ScaleFit.RatioText(4f / 3f), Is.EqualTo("4:3"));
            Assert.That(ScaleFit.RatioText(1f), Is.EqualTo("1:1"));
            Assert.That(ScaleFit.RatioText(9f / 16f), Is.EqualTo("9:16"));

            Assert.That(ScaleFit.RatioText(2560f / 1080f), Is.EqualTo("64:27"),
                "内置「超宽 21:9」的**像素比**是 64:27（21:9 是名字，不是像素）");
            Assert.That(ScaleFit.RatioText(1440f / 3120f), Is.EqualTo("6:13"),
                "内置「手机竖屏超长 9:19.5」的**像素比**是 6:13");
            Assert.That(ScaleFit.RatioText(float.NaN), Is.EqualTo("—"));
            Assert.That(ScaleFit.AspectText(ScreenProfiles.All[2], true, orientationPair: true), Does.Contain("✅同比例").And.Contains("⇄"));
        }

        /// <summary>
        /// 口径图例：把"灰字 = 离线列"从 CSS 类搬到**明面上的那一行**（不许假装整张表都对过账）。
        /// 逐字钉关键成分——改文案时这几条不能一起丢。
        /// </summary>
        [Test]
        public void TableLegend_SaysWhatGrayMeans_AndWhereTruthComesFrom()
        {
            string legend = ScaleFit.TableLegend();

            Assert.That(legend, Does.Contain("灰字"), "必须说清**灰字是什么**（这是它存在的唯一理由）");
            Assert.That(legend, Does.Contain("未经引擎对账"), "必须说清灰字的**口径**（离线，不是「没数据」）");
            Assert.That(legend, Does.Contain("现场对账"), "必须给出**出路**：真值从哪来（可操作）");
            // 🔴 可读性优化：裁留图格内**没有文字** ⇒ 两块是什么、红黄各是什么必须在这一行里有处可查
            Assert.That(legend, Does.Contain("裁留图"), "图例要覆盖裁留图（它是唯一「看图说话」的列）");
            Assert.That(legend, Does.Contain("灰框").And.Contains("蓝块"), "必须写明**哪块是参考、哪块是画布**");
            Assert.That(legend, Does.Contain("红").And.Contains("黄"), "必须写明红黄各是什么意思");
            Assert.That(legend, Does.Not.Contain("**"), "界面文案不走 Markdown（Label 不渲染它）");
        }
    }
}
