using UnityEngine.UIElements;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// <see cref="ScaleCalcTierBinder"/> 的**处理体章节**（装配与列定义在主文件）。
    /// <para>🔴 **四个处理体为什么是 <c>public</c>**（实测的一条 Unity 事实）：
    /// <b><c>SendEvent</c> 在**未接 panel** 的元素上根本不派发</b>（它走 <c>panel</c>，
    /// 而 EditMode 里裸造的单元格没有 panel）⇒ "造个勾选框、<c>value = true</c>、看宿主收不收到"这条路**走不通**。
    /// 所以把处理体抽成可直调的静态方法（断言覆盖"映射对不对"），
    /// **而"注册"那一步（<c>RegisterValueChangedCallback</c> / <c>RegisterCallback</c> 那几行）由现场人工验一次** ——
    /// 如实登记，不假装断言覆盖了。</para>
    /// <para>约定：处理体只做**转达 + 失败回滚显示**，一律不改数据（数据归属在宿主）。</para>
    /// </summary>
    public static partial class ScaleCalcTierBinder
    {
        /// <summary>勾选框的处理体：把「这个单元格属于哪一条 + 新值」转给宿主（断言直接调它）。</summary>
        public static void NotifyIncluded(Toggle cell, bool included, ITierTableHost host)
        {
            string id = cell == null ? null : cell.userData as string;
            if (host != null && id != null) host.OnTierIncludedChanged(id, included);
        }

        /// <summary>删除按钮的处理体（断言直接调它）。</summary>
        public static void NotifyDeleted(VisualElement cell, ITierTableHost host)
        {
            string id = cell == null ? null : cell.userData as string;
            if (host != null && id != null) host.OnTierDeleteClicked(id);
        }

        /// <summary>
        /// 尺寸提交的处理体：宿主说不行就**回滚显示**（数据本来没改，界面上不许留着那个假值）。
        /// </summary>
        public static bool CommitSize(TextField cell, string text, ITierTableHost host)
        {
            string id = cell == null ? null : cell.userData as string;
            if (host == null || id == null) return true;
            if (host.TryCommitTierSize(id, text)) return true;
            cell.SetValueWithoutNotify(host.TierSizeText(id));
            return false;
        }

        /// <summary>
        /// 改名提交的处理体：与 <see cref="CommitSize"/> 同口径 ——
        /// 宿主说不行（内置档被清空 / 越界）就**回滚显示**；<c>notice</c> 那类提示由宿主自己 <c>Report</c>（这里看不到文案）。
        /// </summary>
        public static bool CommitName(TextField cell, string text, ITierTableHost host)
        {
            string id = cell == null ? null : cell.userData as string;
            if (host == null || id == null) return true;
            if (host.TryCommitTierName(id, text)) return true;
            cell.SetValueWithoutNotify(host.TierNameText(id));
            return false;
        }
    }
}
