using NUnit.Framework;
using Wayward.ScaleCalc.Editor;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// **档位管理表宿主高度**（"它跟着条目数长"）：<see cref="ScreenTierColumns.HostHeightFor"/> 是纯函数
    /// ⇒ 不用开窗即可断言。
    /// <para>三条性质：① **内置 7 档仍是 200**（与更早定下的下限**逐位一致**，不回归）；
    /// ② 条目变多 ⇒ 宿主变高（9 档要能一次看全）；③ **有上限**（可用高度 × 30%）——上限的意义是
    /// 不把主表与底部顶出窗口（"预算没算"就是这么来的），超出的靠列表自己滚。</para>
    /// </summary>
    public sealed class ScreenTierHostHeightTests
    {
        /// <summary>窗口底线的可用高度（与 `ScaleCalcWindow.MinWindowSize` 一致：900×880 ⇒ 高 880）。</summary>
        private const float WindowFloorHeight = 880f;

        /// <summary>① 下限：内置 7 档仍是 200（旧口径逐位保留，改这条要同时改 `.uss` 的 `min-height`）。</summary>
        [Test]
        public void SevenBuiltInTiers_StillTakeTheMinimum()
        {
            Assert.That(ScreenProfiles.All.Length, Is.EqualTo(7), "前置：内置就是 7 档");
            Assert.That(ScreenTierColumns.HostHeightFor(7, WindowFloorHeight), Is.EqualTo(ScreenTierColumns.HostMinHeight),
                "7 档 = 下限 200px（2026-09-19 的口径不回归）");
            Assert.That(ScreenTierColumns.HostHeightFor(1, WindowFloorHeight), Is.EqualTo(ScreenTierColumns.HostMinHeight),
                "只有 1 档也给下限——否则窗口会来回跳");
        }

        /// <summary>② 跟着条目数长：9 档要能**一次看全**（当前工作集就是 9 档，用户当场发现只显示 7 行）。</summary>
        [Test]
        public void NineTiers_GrowSoThatEveryRowFits()
        {
            float nine = ScreenTierColumns.HostHeightFor(9, WindowFloorHeight);
            float needed = ScreenTierColumns.HeaderHeightEstimate + 9 * ScreenTierColumns.RowHeight + ScreenTierColumns.HostSlack;

            Assert.That(nine, Is.GreaterThan(ScreenTierColumns.HostMinHeight), "9 档必须比下限高");
            Assert.That(nine, Is.EqualTo(needed).Within(0.01f), "正好给到「表头 + 9 行 + 余量」（不高不低）");
            Assert.That(nine, Is.LessThan(WindowFloorHeight * ScreenTierColumns.HostMaxHeightRatio + 0.01f), "还没碰到上限");
        }

        /// <summary>③ 上限：条目再多也不许把主表与底部顶出窗口（超出部分由列表自己滚）。</summary>
        [Test]
        public void ManyTiers_AreCapped_SoTheMainTableKeepsItsFloor()
        {
            const float mainTableMin = 184f;      // `.table-host` 的 min-height
            float cap = WindowFloorHeight * ScreenTierColumns.HostMaxHeightRatio;
            float host = ScreenTierColumns.HostHeightFor(39, WindowFloorHeight);   // 内置 7 + 自定义上限 32

            Assert.That(host, Is.EqualTo(cap).Within(0.01f), "39 档 ⇒ 吃满上限");
            Assert.That(host + mainTableMin, Is.LessThan(WindowFloorHeight), "上限之下仍给主表留得下它的下限");
        }

        /// <summary>未布局（可用高度未知 / NaN）⇒ **给下限**：宁可首帧少显示一行，也不把别人顶出去。</summary>
        [Test]
        public void UnknownAvailableHeight_FallsBackToTheMinimum()
        {
            Assert.That(ScreenTierColumns.HostHeightFor(12, 0f), Is.EqualTo(ScreenTierColumns.HostMinHeight));
            Assert.That(ScreenTierColumns.HostHeightFor(12, -5f), Is.EqualTo(ScreenTierColumns.HostMinHeight));
            Assert.That(ScreenTierColumns.HostHeightFor(12, float.NaN), Is.EqualTo(ScreenTierColumns.HostMinHeight));
        }

        /// <summary>窗口很高时**照样有上限**（`0.30 × 高度`），不是"越高越长到没边"。</summary>
        [Test]
        public void TallWindow_StillCapsAtTheRatio()
        {
            Assert.That(ScreenTierColumns.HostHeightFor(39, 1600f), Is.EqualTo(480f).Within(0.01f), "1600 × 0.30");
            Assert.That(ScreenTierColumns.HostHeightFor(9, 1600f), Is.EqualTo(
                ScreenTierColumns.HeaderHeightEstimate + 9 * ScreenTierColumns.RowHeight + ScreenTierColumns.HostSlack).Within(0.01f),
                "条目少时按需要给，不跟着窗口无限长");
        }
    }
}
