using System.IO;
using NUnit.Framework;
using Wayward.ScaleCalc.Editor;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// 选型表**行高摊开**：条目少而主视图高时把行铺开填满，
    /// 不再在表格内部留一大片空白；两头都要夹住（挤的时候不低于下限、很高时不高于上限）。
    /// <para>与 <see cref="ScaleCalcTableColumnTests"/>（列宽）分文件：职责不同
    /// （一个横向分摊、一个纵向分摊）。纯函数 ⇒ 不开窗即可断言。</para>
    /// </summary>
    public sealed class ScaleTableRowHeightTests
    {
        private static string PackageRoot =>
            UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(ScaleTableRowHeightTests).Assembly).resolvedPath;

        [Test]
        public void For_SpreadsToFill_WhenThereIsRoom()
        {
            // 7 行、可用高度正好够 25px 一行（减去表头估算）
            float h = ScaleTableRowHeight.For(7, ScaleTableRowHeight.HeaderEstimate + 7f * 25f);
            Assert.That(h, Is.EqualTo(25f).Within(1e-3f), "够高就摊到填满：这是「太空了」的直接修法");
            Assert.That(h, Is.LessThanOrEqualTo(ScaleTableRowHeight.Max), "摊出来的值必须还在上限之内");
        }

        [Test]
        public void For_AtTheWindowFloor_EqualsTheMinimum()
        {
            float min = ScaleTableRowHeight.Min;
            float header = ScaleTableRowHeight.HeaderEstimate;

            // 这条算式就是主表 `min-height: 184`（= 表头 24 + 7 行 × 20）——窗口底线 680 的另一头
            Assert.That(ScaleTableRowHeight.For(7, header + 7f * min), Is.EqualTo(min).Within(1e-3f),
                "窗口底线处的行高 = 下限（680 = 固定段 496 + 184）");
        }

        [Test]
        public void For_NeverGoesBelowTheFloor_EvenWhenCramped()
        {
            float min = ScaleTableRowHeight.Min;
            float header = ScaleTableRowHeight.HeaderEstimate;

            Assert.That(ScaleTableRowHeight.For(7, header + 7f * 8f), Is.EqualTo(min).Within(1e-3f),
                "挤不下宁可让列表自己滚，也不把行压扁");
            Assert.That(ScaleTableRowHeight.For(0, 400f), Is.EqualTo(min).Within(1e-3f), "没有条目");
            Assert.That(ScaleTableRowHeight.For(7, 0f), Is.EqualTo(min).Within(1e-3f), "还没布局（高 0）");
        }

        [Test]
        public void For_RejectsNaN_InsteadOfLeakingIt()
        {
            // 🔴 `NaN < x` 与 `NaN > x` **都是 false** ⇒ 不显式拦的话 NaN 会原样落到 `fixedItemHeight`
            Assert.That(ScaleTableRowHeight.For(7, float.NaN), Is.EqualTo(ScaleTableRowHeight.Min).Within(1e-3f),
                "布局尚未算出（NaN）必须退回下限，不许把 NaN 当天行高用");
        }

        [Test]
        public void For_CapsAtTheCeiling_AndLeavesTheRestBlank()
        {
            Assert.That(ScaleTableRowHeight.For(7, 4000f), Is.EqualTo(ScaleTableRowHeight.Max).Within(1e-3f),
                "窗口拉很高时封顶：余量故意留白，不摊成一条条巨带");
        }

        /// <summary>
        /// 🔴 **装配层真的接上了**（落点级）：纯函数测得再对，若 <see cref="ScaleCalcTableBinder"/> 不在几何变化时调它，
        /// 界面照样是老的固定 20px 行高——而**那种缺陷编译、单测、行数闸门全都拦不住**
        /// （曾经一天内两次靠人眼/现场探针才发现）。
        /// <para>⚠️ **这条断言的边界（别把它当"界面没问题"的证明）**：它读的是源码字符串，
        /// 证明"调用被写上了"，**不证明**运行期真的跑了、也不证明画出来好看。
        /// 真正的一手证据是现场探针读数。源码级判据在界面这件事上是**假保护**，这里只当作"回归提醒"，不当作验收。</para>
        /// </summary>
        [Test]
        public void Binder_WiresTheRowHeight_OnGeometryChange()
        {
            string source = File.ReadAllText(Path.Combine(PackageRoot, "Editor", "Table", "ScaleCalcTableBinder.cs"));

            Assert.That(source, Does.Contain("ApplyRowHeight(listView, view.Count, evt.newRect.height)"),
                "几何变化时必须按**高度**重算行高（撤掉这一行 ⇒ 界面退回固定 20px ⇒ 又是「太空了」）；"
                + "行数取的是**视图副本** `view.Count`（排序/筛选后的行数，不是模型行数）");
            Assert.That(source, Does.Contain("ScaleTableRowHeight.For("),
                "行高必须走公开纯函数（不许在装配层重写一份夹取逻辑）");
            Assert.That(source, Does.Contain("listView.Rebuild()"),
                "写 `fixedItemHeight` **之后必须 `Rebuild()`**：只改这个值不会重排已建出来的行元素"
                + "（实测：声明 26、九行仍是创建当刻的 20 ⇒ 底部「点第 8 行判成第 6 行」）");
        }
    }
}
