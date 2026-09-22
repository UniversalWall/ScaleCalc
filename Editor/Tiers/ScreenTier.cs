using System;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// 工作集里的一条档位 = **档位管理表**的一行。
    /// <para>🔴 <see cref="Id"/> 与 <see cref="Name"/>/<see cref="Size"/> **刻意分开**：改名、改尺寸之后仍是**同一行**（勾选态不丢），
    /// 所以标识**不许**由名字或尺寸派生。</para>
    /// <para>本结构只描述"一条档位长什么样"；**怎么增删改**在 <see cref="ScreenTierSet"/>。</para>
    /// </summary>
    public readonly struct ScreenTier
    {
        /// <summary>内置档的标识前缀（内置条目的 <see cref="Id"/> 一律这个前缀）。</summary>
        public const string BuiltInIdPrefix = "builtin:";

        /// <summary>自定义档的标识前缀（`"c:" + 8 位随机十六进制`）。</summary>
        public const string CustomIdPrefix = "c:";

        /// <summary>稳定标识（<see cref="ScreenTierSet"/> 造出来之后**不再变**）。</summary>
        public readonly string Id;

        /// <summary>显示名（内置 = 清单原名；自定义 = 使用者填的，或自动名）。</summary>
        public readonly string Name;

        /// <summary>宽 × 高。</summary>
        public readonly ScaleSize Size;

        /// <summary>内置 / 自定义（决定能不能删）。</summary>
        public readonly ScreenTierKind Kind;

        /// <summary>是否参与计算（管理表上的勾选态；初值 = 全部纳入）。</summary>
        public readonly bool Included;

        public ScreenTier(string id, string name, ScaleSize size, ScreenTierKind kind, bool included)
        {
            Id = id;
            Name = name;
            Size = size;
            Kind = kind;
            Included = included;
        }

        /// <summary>由内置清单造一条：<see cref="Id"/> 由**原始**名字派生（改名后 <see cref="Id"/> 不变，这正是要的语义）。</summary>
        public static ScreenTier FromProfile(ScreenProfile profile, bool included)
            => new ScreenTier(BuiltInIdPrefix + profile.Name, profile.Name, profile.Size, ScreenTierKind.BuiltIn, included);

        public ScreenTier WithIncluded(bool included) => new ScreenTier(Id, Name, Size, Kind, included);

        public ScreenTier WithName(string name) => new ScreenTier(Id, name, Size, Kind, Included);

        public ScreenTier WithSize(ScaleSize size) => new ScreenTier(Id, Name, size, Kind, Included);

        /// <summary>转成选型表吃的档位（来源列如实标注）。</summary>
        public ScreenProfile ToProfile() => new ScreenProfile(Name, Size.Width, Size.Height, SourceOf(this));

        /// <summary>
        /// 来源列的取值：自定义 ⇒ `"自定义"`；内置 ⇒ 到清单里取**原文**（不在这里重抄一遍）。
        /// <para>按 <see cref="Id"/> 里嵌的**原始**名字去查（不是当前 <see cref="Name"/>）⇒ 内置档被改过名也照样取得到原文。</para>
        /// </summary>
        private static string SourceOf(in ScreenTier tier)
        {
            if (tier.Kind == ScreenTierKind.Custom) return "自定义";
            string original = tier.Id != null && tier.Id.StartsWith(BuiltInIdPrefix, StringComparison.Ordinal)
                ? tier.Id.Substring(BuiltInIdPrefix.Length)
                : tier.Name;
            foreach (ScreenProfile profile in ScreenProfiles.All)
                if (profile.Name == original) return profile.Source;
            return "档位草案";
        }
    }
}
