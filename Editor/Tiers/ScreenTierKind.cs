using System;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// 档位的来源：**内置不可删、自定义可删**。
    /// <para>刻意**不存进载荷**——它由 <see cref="ScreenTier.Id"/> 前缀派生（<c>builtin:</c> ⇒ 内置）
    /// ⇒ 载荷少一列，旧格式不会因此失效。</para>
    /// </summary>
    public enum ScreenTierKind
    {
        /// <summary>内置档位（来自 <see cref="ScreenProfiles.All"/>）：要"不算它"只能取消勾选。</summary>
        BuiltIn,

        /// <summary>使用者新增：可删、可改名、可改尺寸。</summary>
        Custom,
    }
}
