using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// 工作集的**载荷与容错**。
    /// <para>🔴 本类**全是纯函数**、**不碰 <c>EditorPrefs</c>**（连 <c>using UnityEditor</c> 都没有）——否则测试只能验
    /// "往返对不对"，测不到"坏载荷怎么处理"（那才是这一环真正的风险）。落盘实现见 <see cref="EditorPrefsScreenTierStorage"/>。</para>
    /// <para>⚠️ 载荷**不依赖本工程程序集**：一行一条 · <c>\t</c> 分列 · <c>\n</c> 分行 · 首行版本号。</para>
    /// </summary>
    public static class ScreenTierStorage
    {
        /// <summary>载荷版本号（首行）。不认的版本 ⇒ **整份回退**，不做兼容猜测。</summary>
        public const string FormatVersion = "v1";

        /// <summary>
        /// 载荷长度上限（**保守猜测值**）。
        /// <para>⚠️ <c>EditorPrefs</c> 的真实上限要以实测为准。超限 ⇒ **拒绝保存**而非截断。</para>
        /// </summary>
        public const int MaxPayloadLength = 8192;

        /// <summary>逐字文案：载荷超长拒存。</summary>
        public const string TooLargeMessage = "工作集太大，未保存到编辑器配置（下次打开会回到上次成功保存的状态）";

        /// <summary>逐字文案：存储写失败。</summary>
        public const string SaveFailedMessage = "工作集未能保存到编辑器配置（下次打开会回到上次成功保存的状态）";

        /// <summary>工作集 ⇒ 载荷。空 <see cref="ScreenTier.Id"/> 不写（构造已丢弃，这里是第二道闸）。</summary>
        public static string Encode(ScreenTierSet set)
        {
            var sb = new StringBuilder();
            sb.Append(FormatVersion).Append('\n');
            if (set == null) return sb.ToString();
            foreach (ScreenTier tier in set.Tiers)
            {
                if (string.IsNullOrEmpty(tier.Id)) continue;
                sb.Append(tier.Id).Append('\t')
                  .Append(Escape(tier.Name)).Append('\t')
                  .Append(tier.Size.Width.ToString("G9", CultureInfo.InvariantCulture)).Append('\t')
                  .Append(tier.Size.Height.ToString("G9", CultureInfo.InvariantCulture)).Append('\t')
                  .Append(tier.Included ? '1' : '0').Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>
        /// 载荷 ⇒ 工作集条目。**逐条判，任一不合法 ⇒ 整份失败**（半个工作集比没有工作集更危险——
        /// 使用者以为在算 5 档，其实算的是 3 档）。
        /// <para>拒绝理由是一张**封闭清单**（7 条）；本方法**不新增**理由。</para>
        /// </summary>
        public static bool TryDecode(string payload, out List<ScreenTier> tiers, out string reason)
        {
            tiers = null;
            reason = null;

            // 换行统一后再切（Windows 上手工编辑过的载荷也能读）
            string[] lines = (payload ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            if (lines[0] != FormatVersion) { reason = "版本不认"; return false; }

            var list = new List<ScreenTier>();
            int customCount = 0;
            for (int i = 1; i < lines.Length; i++)
            {
                if (lines[i].Length == 0) continue;   // 末尾换行不算条目
                int at = i + 1;                       // 报给人看的行号（1 基，含版本行）
                string[] cells = lines[i].Split('\t');
                if (cells.Length != 5) { reason = "第 " + at + " 行字段数不对"; return false; }
                if (cells[0].Length == 0) { reason = "第 " + at + " 行缺标识"; return false; }

                if (!float.TryParse(cells[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float w) ||
                    !float.TryParse(cells[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float h))
                { reason = "第 " + at + " 行尺寸不是数"; return false; }

                if (!ScreenTierSet.IsValidSize(new ScaleSize(w, h))) { reason = "第 " + at + " 行尺寸越界"; return false; }
                if (cells[4] != "0" && cells[4] != "1") { reason = "第 " + at + " 行勾选态无法识别"; return false; }

                ScreenTierKind kind = cells[0].StartsWith(ScreenTier.BuiltInIdPrefix, StringComparison.Ordinal)
                    ? ScreenTierKind.BuiltIn
                    : ScreenTierKind.Custom;
                if (kind == ScreenTierKind.Custom) customCount++;

                list.Add(new ScreenTier(cells[0], Unescape(cells[1]), new ScaleSize(w, h), kind, cells[4] == "1"));
            }

            if (customCount > ScreenTierSet.MaxCustomTiers)
            {
                reason = "自定义行超上限 " + ScreenTierSet.MaxCustomTiers;
                return false;
            }

            tiers = list;
            return true;
        }

        /// <summary>
        /// 读工作集：**任何坏载荷都整份回退到内置 7 档，且不抛异常**（恶数据不许让窗口打不开）。
        /// <para><paramref name="fallbackReason"/> = **具体原因**（没存过时为 <c>null</c>）；
        /// 给使用者看的整句用 <see cref="DescribeFallback"/>。</para>
        /// </summary>
        public static ScreenTierSet LoadTiers(IScreenTierStorage storage, out string fallbackReason)
        {
            fallbackReason = null;
            if (storage == null || !storage.TryLoad(out string payload)) return ScreenTierSet.Default();
            if (TryDecode(payload, out List<ScreenTier> tiers, out string reason))
            {
                // 🔴 `TryDecode` **不查"标识唯一"**（它的拒绝理由是一张 7 条封闭清单），
                //    而 `ScreenTierSet` 的构造**按 `Id` 去重、只留第一条** ⇒ 重复标识会**静默少一档**。
                //    ⇒ 补一道：宁可整份回落内置 7 档并说清原因，也不让使用者以为有 9 档、实际 8 档。
                //    （文件层另有一道等价的，见 `ScreenTierListFile.TryParseFile`——两处都要，载体不同。）
                if (TryFindDuplicateId(tiers, out string duplicateId))
                {
                    fallbackReason = DuplicateIdMessage(duplicateId);
                    return ScreenTierSet.Default();
                }
                return new ScreenTierSet(tiers);
            }

            fallbackReason = reason;
            return ScreenTierSet.Default();
        }

        /// <summary>逐字文案：加载回退（回退**不许静默**）。</summary>
        public static string DescribeFallback(string reason)
            => "工作集配置无法识别（" + reason + "），已回到内置 " + ScreenProfiles.All.Length + " 档";

        /// <summary>
        /// 逐字文案：载荷里有**重复标识**。<para>必须把**后果**写出来（"静默少一档"）——
        /// 只说"重复"会让人以为只是啰嗦，不知道会少一行。</para>
        /// </summary>
        public static string DuplicateIdMessage(string id)
            => "有重复的档位标识「" + id + "」（照常载入会静默少一档）";

        /// <summary>找第一处重复的 <see cref="ScreenTier.Id"/>（序数比较）。载荷层补的那道闸用它。</summary>
        private static bool TryFindDuplicateId(List<ScreenTier> tiers, out string id)
        {
            id = null;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (ScreenTier tier in tiers)
                if (!seen.Add(tier.Id)) { id = tier.Id; return true; }
            return false;
        }

        /// <summary>
        /// 载荷长度闸门。独立成纯函数**是为了可测**：自定义行上限（32 行 × 名字 ≤32 字）
        /// 让真实工作集**目前够不到** 8192，闸门是**纵深防御**而非日常路径。
        /// </summary>
        public static bool IsPayloadTooLarge(string payload)
            => payload != null && payload.Length > MaxPayloadLength;

        /// <summary>
        /// <c>SaveTiers()</c> 的唯一实现：编码 → 长度闸门 → 写。
        /// <para>成功时 <paramref name="message"/> = <c>null</c>；失败时给逐字文案。
        /// **失败不回滚内存**——本会话照常可用，只如实告知。</para>
        /// </summary>
        public static bool TrySave(ScreenTierSet set, IScreenTierStorage storage, out string message)
        {
            message = null;
            string payload = Encode(set);
            if (IsPayloadTooLarge(payload)) { message = TooLargeMessage; return false; }
            if (storage == null || !storage.TrySave(payload)) { message = SaveFailedMessage; return false; }
            return true;
        }

        /// <summary>转义 <c>\</c> <c>\t</c> <c>\n</c>。⚠️ **反斜杠必须第一个换**，否则会把后面换出来的反斜杠再转一遍。</summary>
        private static string Escape(string raw)
            => (raw ?? string.Empty).Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\n", "\\n");

        /// <summary>
        /// 反向还原。**容忍**孤立或认不出的转义（按字面反斜杠收下）——拒绝理由是一张封闭清单，
        /// 这里不新增第 8 条理由（真损坏的载荷几乎都会先撞上"字段数不对"）。
        /// </summary>
        private static string Unescape(string raw)
        {
            if (string.IsNullOrEmpty(raw) || raw.IndexOf('\\') < 0) return raw ?? string.Empty;
            var sb = new StringBuilder(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                if (raw[i] != '\\' || i + 1 >= raw.Length) { sb.Append(raw[i]); continue; }
                char next = raw[i + 1];
                if (next == 't') { sb.Append('\t'); i++; }
                else if (next == 'n') { sb.Append('\n'); i++; }
                else if (next == '\\') { sb.Append('\\'); i++; }
                else sb.Append(raw[i]);
            }
            return sb.ToString();
        }
    }
}
