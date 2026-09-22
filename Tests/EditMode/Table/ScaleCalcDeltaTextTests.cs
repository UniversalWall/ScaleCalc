using NUnit.Framework;
using Wayward.ScaleCalc.Editor;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// **差值列的显示精度**：判"对不对得上"的容差是 <c>1e-3</c>，而差值原来只用两位小数
    /// ⇒ <c>|Δ|</c> 落在 0.001~0.005 时**判失败、显示 <c>0.00</c>**（看表的人只会觉得"差 0 却在报警"）。
    /// <para>🔴 判据是**两件事的关系**：<see cref="ScaleFit"/> 的差值格式化位数 ≥ 容差的量级，且**与普通格式化不是同一个函数**
    /// （同一个就会退回去）。另：**证据包那条链路不动**（它有自己的逐字节基线）——所以这里只断言界面这条线。</para>
    /// <para>为什么另立文件：<c>ScaleFitTests</c>（逐档）加完这条就 204 行（200 行红线当场拦下，且**这次是拦在提交前**）。</para>
    /// </summary>
    public sealed class ScaleCalcDeltaTextTests
    {
        private static readonly ScaleSize Reference = new ScaleSize(1920f, 1080f);

        private static ScaleCalcResult Canvas()
        {
            ScaleCalcInput input = ScaleCalcInput.Default;
            input.Mode = ScaleMode.ScaleWithScreenSize;
            input.ScreenSize = Reference;
            input.ReferenceResolution = Reference;
            input.ScreenMatch = ScreenMatchMode.MatchWidthOrHeight;
            input.MatchWidthOrHeight = 0.5f;
            return ScaleCalc.Evaluate(in input);
        }

        /// <summary>`0.0012`（**已经被判成"对账失败"的量级**）必须在差值列看得见。</summary>
        [Test]
        public void DeltaText_ShowsValuesBelowTheOldTwoDecimalCutoff()
        {
            var row = new ScaleTableRow
            {
                Profile = ScreenProfiles.All[2],
                Reference = Reference,
                ScreenSize = Canvas(),
                HasTruth = true,
                DeltaScaleFactor = 0.0012f,
                DeltaReferencePpu = 0f,
                DeltaCanvasWidth = 0f,
                DeltaCanvasHeight = 0f,
            };

            Assert.That(row.DeltaText, Does.Contain("Δsf=0.0012"), "0.0012 必须看得见（它已被判成失败）");
            Assert.That(row.DeltaText, Does.Not.Contain("Δsf=0.00 "), "不许再出现「判失败却显示 0.00」");
        }

        /// <summary>精度口径本身：`FormatDelta` 4 位、`Format` 2 位，**两者就是不一样**；0 也要补齐位数（列里对齐）。</summary>
        [Test]
        public void FormatDelta_IsCoarserOrEqualThanTheTolerance_ButFinerThanFormat()
        {
            Assert.That(AxisVerdict.FormatDelta(0.0012f), Is.Not.EqualTo(AxisVerdict.Format(0.0012f)),
                "差值这条线不许退回 F2");
            Assert.That(AxisVerdict.FormatDelta(0.0012f), Is.EqualTo("0.0012"));
            Assert.That(AxisVerdict.FormatDelta(0f), Is.EqualTo("0.0000"), "0 也补齐位数");
            Assert.That(AxisVerdict.Format(1.4719601f), Is.EqualTo("1.47"), "sf / refPPU 那些值仍用 F2（没变）");
        }
    }
}
