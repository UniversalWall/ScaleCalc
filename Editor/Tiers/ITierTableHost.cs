using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// 管理表要宿主**回答或执行**的事情。抽成接口而不是一串委托：
    /// ① 装配代码只认这几件事，不认窗口；② 断言拿个假宿主就能把整张表驱动起来（不必开窗）。
    /// <para>⚠️ 这些成员都在**宿主**里实现（窗口或假宿主），binder 自己**不持有任何状态**。</para>
    /// </summary>
    public interface ITierTableHost
    {
        /// <summary>勾选态变了。<paramref name="included"/> 是**新**值；条目一定存在。</summary>
        void OnTierIncludedChanged(string id, bool included);

        /// <summary>删除按钮被点。**内置档也会走到这里**——由宿主给出可读说明。</summary>
        void OnTierDeleteClicked(string id);

        /// <summary>
        /// 尺寸就地编辑提交（`isDelayed` ⇒ 回车 / 失焦才到）。返回 <c>false</c> ⇒ 视图**回滚**到
        /// <see cref="TierSizeText"/>（非法输入不许留在框里假装生效）。
        /// </summary>
        bool TryCommitTierSize(string id, string text);

        /// <summary>该条目尺寸的当前显示文本（绑定时填、回滚时复位——**同一个来源**，不会两处不一致）。</summary>
        string TierSizeText(string id);

        /// <summary>
        /// 改名就地编辑提交（`isDelayed` ⇒ 回车 / 失焦才到）。返回 <c>false</c> ⇒ 视图**回滚**到
        /// <see cref="TierNameText"/>（内置档被清空、或名字本来就没变，都算"不落地"）。
        /// <para>口径不在这里：空名字**自定义回落自动名、内置档拒绝**，见 <see cref="ScreenTierSet.TryResolveRename"/>。</para>
        /// </summary>
        bool TryCommitTierName(string id, string text);

        /// <summary>该条目名字的当前显示文本（绑定时填、回滚时复位——**同一个来源**，不会两处不一致）。</summary>
        string TierNameText(string id);
    }
}
