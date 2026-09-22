using System.Collections.Generic;
using Wayward.ScaleCalc.Editor;

namespace Wayward.ScaleCalc.Tests
{
    /// <summary>
    /// 档位管理表的**假宿主**：只记录"被调了什么"，不碰数据。
    /// <para>用途：装配层要验证的是**接线**（单元格 ↔ 宿主），不是窗口。两个提交口各有一个
    /// <c>*Ok</c> 开关，用来模拟"宿主拒绝" ⇒ 断言视图回滚。</para>
    /// <para>**为什么抽到 `Support/`**：改名测试与界面测试都要用它——同一份假宿主放两份
    /// 必然漂移（一边加了接口成员、另一边编译不过或者漏断）。它同时是 <see cref="ITierTableHost"/> 的**唯一**测试实现。</para>
    /// </summary>
    internal sealed class TierFakeHost : ITierTableHost
    {
        private readonly ScreenTierSet m_Set;

        /// <summary>按调用顺序记下"谁被调了、参数是什么"（断言逐条比对）。</summary>
        public readonly List<string> Calls = new List<string>();

        /// <summary>尺寸提交口是否放行（<c>false</c> 模拟宿主拒绝 ⇒ 视图该回滚）。</summary>
        public bool SizeCommitOk = true;

        /// <summary>改名提交口是否放行（同上）。</summary>
        public bool NameCommitOk = true;

        public TierFakeHost(ScreenTierSet set) { m_Set = set; }

        public void OnTierIncludedChanged(string id, bool included) => Calls.Add("include:" + id + ":" + included);

        public void OnTierDeleteClicked(string id) => Calls.Add("delete:" + id);

        public bool TryCommitTierSize(string id, string text)
        {
            Calls.Add("size:" + id + ":" + text);
            return SizeCommitOk;
        }

        public bool TryCommitTierName(string id, string text)
        {
            Calls.Add("name:" + id + ":" + text);
            return NameCommitOk;
        }

        public string TierSizeText(string id)
        {
            int at = m_Set.IndexOf(id);
            return at < 0 ? string.Empty : m_Set.Tiers[at].Size.ToString();
        }

        public string TierNameText(string id)
        {
            int at = m_Set.IndexOf(id);
            return at < 0 ? string.Empty : m_Set.Tiers[at].Name;
        }
    }
}
