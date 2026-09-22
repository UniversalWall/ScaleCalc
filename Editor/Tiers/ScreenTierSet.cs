using System;
using System.Collections.Generic;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// 档位**工作集**：管理表渲染 <see cref="Tiers"/>，选型表渲染 <see cref="IncludedTiers"/>。
    /// <para>纯数据层：**不碰 UnityEditor、不碰场景**，所有断言都能在 EditMode 里直接调。</para>
    /// <para>⚠️ <see cref="Tiers"/> 交出去的是内部列表本身；要改一律走本类的方法（它们在改的同时守住不变量）。
    /// 条目长什么样在 <see cref="ScreenTier"/>，怎么变成文本在 <see cref="ScreenTierStorage"/>。</para>
    /// </summary>
    public sealed partial class ScreenTierSet
    {
        /// <summary>自定义行上限：也给 <c>EditorPrefs</c> 的载荷体积兜了底。</summary>
        public const int MaxCustomTiers = 32;

        /// <summary>名字长度上限（超长截断并如实提示）。</summary>
        public const int MaxNameLength = 32;

        /// <summary>逐字文案：达自定上限。**跟着 <see cref="MaxCustomTiers"/> 走**，所以是 <c>readonly</c> 而不是 <c>const</c>。</summary>
        public static readonly string LimitMessage = "自定义行已达上限 " + MaxCustomTiers + "；请先删除一些";

        private readonly List<ScreenTier> m_Tiers = new List<ScreenTier>();

        /// <summary>按 <see cref="ScreenTier.Id"/> 去重（同 <c>Id</c> 只留**第一条**）。空 <c>Id</c> 丢弃——不变量要求 <c>Id</c> 非空。</summary>
        public ScreenTierSet(IReadOnlyList<ScreenTier> tiers)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (tiers == null) return;
            foreach (ScreenTier tier in tiers)
                if (!string.IsNullOrEmpty(tier.Id) && seen.Add(tier.Id)) m_Tiers.Add(tier);
        }

        /// <summary>内置 7 档、**全部纳入**（初值）。</summary>
        public static ScreenTierSet Default()
        {
            var tiers = new List<ScreenTier>();
            foreach (ScreenProfile profile in ScreenProfiles.All) tiers.Add(ScreenTier.FromProfile(profile, true));
            return new ScreenTierSet(tiers);
        }

        /// <summary>**全部**条目（管理表渲染它；被取消勾选的也在这里）。</summary>
        public IReadOnlyList<ScreenTier> Tiers => m_Tiers;

        /// <summary>被勾选的数量（状态栏"参与计算 N 档"用它）。</summary>
        public int IncludedCount => CountWhere(true);

        /// <summary>自定义行数（上限判据与诊断都用它）。</summary>
        public int CustomCount => CountCustom();

        /// <summary>还能不能再加自定义行（<see cref="MaxCustomTiers"/> 的上限）。</summary>
        public bool CanAdd => CustomCount < MaxCustomTiers;

        /// <summary>**被勾选**的条目（选型表按它渲染，**顺序 = 这里的顺序**）。</summary>
        public IReadOnlyList<ScreenTier> IncludedTiers()
        {
            var included = new List<ScreenTier>();
            foreach (ScreenTier tier in m_Tiers) if (tier.Included) included.Add(tier);
            return included;
        }

        /// <summary>按 <see cref="ScreenTier.Id"/> 找位置；找不到给 <c>-1</c>。</summary>
        public int IndexOf(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            for (int i = 0; i < m_Tiers.Count; i++)
                if (string.Equals(m_Tiers[i].Id, id, StringComparison.Ordinal)) return i;
            return -1;
        }

        /// <summary>勾选 / 取消。返回**是否真的改了**（没找到、或值本来就一样 ⇒ <c>false</c>）。</summary>
        public bool SetIncluded(string id, bool included)
        {
            int at = IndexOf(id);
            if (at < 0 || m_Tiers[at].Included == included) return false;
            m_Tiers[at] = m_Tiers[at].WithIncluded(included);
            return true;
        }

        /// <summary>只有自定义行可删。</summary>
        public bool CanDelete(string id)
        {
            int at = IndexOf(id);
            return at >= 0 && m_Tiers[at].Kind == ScreenTierKind.Custom;
        }

        /// <summary>删自定义行；内置 / 不存在 ⇒ <c>false</c> 且**不动任何东西**。</summary>
        public bool Delete(string id)
        {
            int at = IndexOf(id);
            if (at < 0 || m_Tiers[at].Kind != ScreenTierKind.Custom) return false;
            m_Tiers.RemoveAt(at);
            return true;
        }

        /// <summary>
        /// 追加一条自定义行（<c>Included = true</c>）。**先判上限**，再判尺寸，最后归一化名字。
        /// <para>失败时 <paramref name="error"/> 给**可读且可操作**的下一步。</para>
        /// </summary>
        public bool TryAdd(ScaleSize size, string name, out ScreenTier tier, out string error)
        {
            tier = default;
            error = null;
            if (!CanAdd) { error = LimitMessage; return false; }
            if (!IsValidSize(size)) { error = InvalidSizeMessage(size); return false; }

            string resolved = NormalizeName(name, out _);
            if (resolved.Length == 0) resolved = AutoName(size);
            tier = new ScreenTier(NewCustomId(), resolved, size, ScreenTierKind.Custom, true);
            m_Tiers.Add(tier);
            return true;
        }

        /// <summary>改名（就地编辑用）。名字经 <see cref="NormalizeName"/>；空名字**不覆盖**原值。</summary>
        public bool Rename(string id, string name)
        {
            int at = IndexOf(id);
            if (at < 0) return false;
            string resolved = NormalizeName(name, out _);
            if (resolved.Length == 0 || resolved == m_Tiers[at].Name) return false;
            m_Tiers[at] = m_Tiers[at].WithName(resolved);
            return true;
        }

        /// <summary>改尺寸（就地编辑用）。非法尺寸 ⇒ <c>false</c> 且保留原值。</summary>
        public bool Resize(string id, ScaleSize size)
        {
            int at = IndexOf(id);
            if (at < 0 || !IsValidSize(size)) return false;
            m_Tiers[at] = m_Tiers[at].WithSize(size);
            return true;
        }

        /// <summary>尺寸必须 **&gt; 0 且有限**。⚠️ 这里**不**沿用内核那套"夹到 <c>1e-5</c>"——那是引擎镜像口径，不是用户输入校验。</summary>
        public static bool IsValidSize(ScaleSize size)
        {
            return size.Width > 0f && size.Height > 0f
                   && !float.IsInfinity(size.Width) && !float.IsInfinity(size.Height);
        }

        /// <summary>
        /// 名字归一化：先 <c>Trim()</c>；空 ⇒ 返回空串（调用方用自动名）；
        /// 超 <see cref="MaxNameLength"/> 字 ⇒ 截断并**如实**给出 <paramref name="notice"/>。
        /// <para>⚠️ 转义（<c>\t</c>/<c>\n</c>/<c>\\</c>）**不在这里做**——那是编码层的事。本方法只管长度。</para>
        /// </summary>
        public static string NormalizeName(string raw, out string notice)
        {
            notice = null;
            string name = (raw ?? string.Empty).Trim();
            if (name.Length <= MaxNameLength) return name;
            string cut = name.Substring(0, MaxNameLength);
            notice = "名字超过 " + MaxNameLength + " 字，已截断为「" + cut + "」";
            return cut;
        }

        /// <summary>自动名跟着尺寸走（<c>"自定义 1920x1080"</c>）。</summary>
        public static string AutoName(ScaleSize size) => "自定义 " + size.ToString();

        /// <summary>逐字文案：尺寸非法。</summary>
        public static string InvalidSizeMessage(ScaleSize size)
            => "尺寸必须是大于 0 的数（收到 宽=" + size.Width + " 高=" + size.Height + "）";

        private int CountWhere(bool included)
        {
            int count = 0;
            foreach (ScreenTier tier in m_Tiers) if (tier.Included == included) count++;
            return count;
        }

        private int CountCustom()
        {
            int count = 0;
            foreach (ScreenTier tier in m_Tiers) if (tier.Kind == ScreenTierKind.Custom) count++;
            return count;
        }

        /// <summary>
        /// 新自定义标识：<c>"c:" + 8 位随机十六进制</c>（"随机但稳定"的标识）。
        /// <para>撞了就重摇（32 位空间 + 上限 32 行，撞的概率可以忽略；真摇不出来才退化成全长 GUID）。</para>
        /// </summary>
        private string NewCustomId()
        {
            for (int attempt = 0; attempt < 64; attempt++)
            {
                string id = ScreenTier.CustomIdPrefix + Guid.NewGuid().ToString("N").Substring(0, 8);
                if (IndexOf(id) < 0) return id;
            }
            return ScreenTier.CustomIdPrefix + Guid.NewGuid().ToString("N");
        }
    }
}
