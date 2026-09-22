using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// 工作集**清单文件**的格式层：注释头 · 剥注释 · 文件级判据。**全是纯函数**
    /// （不碰 IO、不碰 <c>EditorPrefs</c>、不碰场景）⇒ "坏文件怎么处理"能在 EditMode 里逐条断言。
    /// <para>⚠️ 与 <see cref="ScreenTierStorage"/> 的分工：那一份管**数据**（<c>v1</c> 载荷的编解码与容错），
    /// 这一份管**文件**（外层那几行说明，以及"整份文件算不算合法"）。**载荷格式零改动**。</para>
    /// <para>🔴 闸门作用于**剥注释后的数据部分**，不是整个文件——否则 8192 的上限会随说明书字数漂移。</para>
    /// <para>🔴 **剥注释是全文过滤**：任意位置的 <c>#</c> 行与空行都不进数据部分。
    /// 依据是**数据行行首永远是 Id**（<c>builtin:</c> / <c>c:</c>，永不含 <c>#</c>）⇒ 不可能误剥数据；
    /// 而且这才对得起注释头里对使用者的承诺「以 # 开头的行只是说明，导入时会被跳过」。</para>
    /// </summary>
    public static class ScreenTierListFile
    {
        /// <summary>注释行前缀。数据行行首永远是 <see cref="ScreenTier.Id"/>（<c>builtin:</c> / <c>c:</c>，永不含 <c>#</c>）⇒ 剥注释不会误剥数据。</summary>
        public const char CommentPrefix = '#';

        /// <summary>注释头首行（标题）；<see cref="CommentHeader"/> 的第一行就是它。</summary>
        public const string FileTitle = "# ScaleCalc 屏幕档位清单（com.wayward.scalecalc · 工作集）";

        /// <summary>闸① 逐字文案：只剩注释或空行。</summary>
        public const string NoDataMessage = "文件里没有可识别的数据行（只有注释或空行）";

        /// <summary>
        /// 注释头正文（**逐字**，改动要同步测试里的逐字断言）。
        /// <para>🔴 **逐行字面量、绝不写成多行 verbatim 字符串**：本仓库 <c>core.autocrlf=true</c>，checkout 后源文件是 CRLF
        /// ⇒ verbatim 串里会带 <c>\r\n</c> 进产物，直接破坏「导出恒 LF」。这里逐行给、由 <see cref="CommentHeader"/>
        /// 显式拼 <c>'\n'</c>，产物行尾与 checkout 无关。</para>
        /// <para>上限那两个数**跟着常量走**（同 <see cref="ScreenTierSet.LimitMessage"/> 的口径），不手抄 32。</para>
        /// </summary>
        private static readonly string[] HeaderLines =
        {
            FileTitle,
            "# 用途：与同事共享 / 入版本库；导入时会**整体替换**当前工作集（本机 EditorPrefs 用的是同一份数据）",
            "# 格式：首行 v1；此后一行一档，制表符分列 —— 标识 · 名字 · 宽 · 高 · 是否参与计算(1/0)",
            "# 可手工编辑：以 # 开头的行只是说明，导入时会被跳过；数据行改完直接导入即可",
            "# 上限：自定义行 ≤ " + ScreenTierSet.MaxCustomTiers + " · 名字 ≤ " + ScreenTierSet.MaxNameLength + " 字（超限整份拒绝，不截断）",
        };

        /// <summary>行号提取（闸③ 用）：<see cref="ScreenTierStorage.TryDecode"/> 那 7 条里**带行号**的 5 条都以 <c>第 N 行</c> 开头。</summary>
        private static readonly Regex LineNumberPattern = new Regex(@"^第 (\d+) 行", RegexOptions.CultureInvariant);

        private static readonly int[] EmptyMap = new int[0];

        /// <summary>
        /// 注释头（**末尾一个换行**）。<para>🔴 **不含时间戳 / 机器名 / 用户名**——要求"同一工作集导出两次逐字节相同"。</para>
        /// </summary>
        public static string CommentHeader()
        {
            var sb = new StringBuilder();
            foreach (string line in HeaderLines) sb.Append(line).Append('\n');
            return sb.ToString();
        }

        /// <summary>文件全文 = 注释头 + 载荷。<para>逐字节可断言：剥掉注释后必须 **==** <see cref="ScreenTierStorage.Encode"/>。</para></summary>
        public static string Describe(ScreenTierSet set) => CommentHeader() + ScreenTierStorage.Encode(set);

        /// <summary>
        /// 剥掉注释行与空行（**全文过滤**）。
        /// <para>⚠️ 只按**行首**判：数据行行首永远是 <see cref="ScreenTier.Id"/>，名字里的 <c>#</c> 在第 2 列 ⇒ 不会被误剥。</para>
        /// <para>⚠️ 这里**不**做 <c>\r\n</c> 归一——<see cref="ScreenTierStorage.TryDecode"/> 已经会做，重复归一反而会掩盖"混行尾"这种真问题。</para>
        /// </summary>
        public static string StripCommentHeader(string text) => StripComments(text, out _);

        /// <summary>
        /// 四道闸，全过才给候选。**失败一律 <paramref name="tiers"/> = <c>null</c>**
        /// ⇒ 调用方据此保证"坏输入零副作用"（当前工作集一个字段都不动）。
        /// </summary>
        public static bool TryParseFile(string text, out List<ScreenTier> tiers, out string reason)
        {
            tiers = null;
            reason = null;

            string payload = StripComments(text, out int[] lineMap);

            // ① 只剩注释或空行
            if (string.IsNullOrEmpty(payload)) { reason = NoDataMessage; return false; }

            // ② 长度闸门：只算**数据部分**（注释头不计入）
            if (ScreenTierStorage.IsPayloadTooLarge(payload))
            {
                reason = "文件太大（数据部分 " + payload.Length + " 字符 > " + ScreenTierStorage.MaxPayloadLength + "）";
                return false;
            }

            // ③ 载荷层：`TryDecode` 的 7 条封闭理由**措辞一个字不改**，只做位置换算（见 Localize）
            if (!ScreenTierStorage.TryDecode(payload, out List<ScreenTier> decoded, out string decodedReason))
            {
                reason = Localize(payload, lineMap, decodedReason);
                return false;
            }

            // ④ 标识重复（**文件层新增**的第 8 类判据；`TryDecode` 的 7 条一个字没改）            //    载荷已滤掉空行 ⇒ 第 k 条档位落在**载荷内第 k+2 行**（1 号是版本行）
            if (TryFindDuplicate(decoded, out int firstIndex, out int duplicateIndex, out string duplicateId))
            {
                reason = "第 " + PhysicalLine(lineMap, firstIndex + 2) + " 行与第 "
                       + PhysicalLine(lineMap, duplicateIndex + 2) + " 行标识重复「" + duplicateId + "」";
                return false;
            }

            tiers = decoded;
            return true;
        }

        /// <summary>
        /// 把 <see cref="ScreenTierStorage.TryDecode"/> 的理由**换算到文件坐标**（报出行号与该行原文）。
        /// <para>🔴 措辞**一个字不改**：只把 <c>第 N 行</c> 的 N 从**载荷内行号**换成**文件物理行号**，并**追加**该行原文。
        /// 不换算的话，一份"5 行注释头 + 数据"的文件报「第 3 行」会指向注释头里的格式说明，用户按它去看**必然看错行**。</para>
        /// <para>带行号的只有 5 条（字段数 / 缺标识 / 尺寸不是数 / 尺寸越界 / 勾选态）；<c>版本不认</c> 与
        /// <c>自定义行超上限</c> 没有行号可换算 ⇒ **原样返回**。</para>
        /// </summary>
        private static string Localize(string payload, int[] lineMap, string decodedReason)
        {
            if (decodedReason == null) return null;
            Match match = LineNumberPattern.Match(decodedReason);
            if (!match.Success) return decodedReason;

            int inPayload = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            string raw = RawLine(payload, inPayload);
            string tail = raw == null ? string.Empty : "（该行：" + raw + "）";
            return "第 " + PhysicalLine(lineMap, inPayload) + " 行" + decodedReason.Substring(match.Length) + tail;
        }

        /// <summary>
        /// 剥掉 <c>#</c> 行与空行（**任意位置**），顺带交出**载荷内第 N 行 → 文件物理行号**的对照表。
        /// <para>🔴 **必须有这张对照表**：全文过滤之后，"载荷内行号"与"物理行号"不再只差一个固定偏移
        /// （中间每删一行注释就错开一行）——用它才能把报错指到用户真正该看的那个文件行。</para>
        /// </summary>
        private static string StripComments(string text, out int[] lineMap)
        {
            lineMap = EmptyMap;
            if (text == null) return string.Empty;

            string[] lines = text.Split('\n');
            var kept = new List<string>();
            var map = new List<int>();
            for (int i = 0; i < lines.Length; i++)
            {
                string probe = lines[i].TrimEnd('\r');
                if (probe.Length == 0 || probe.TrimStart().StartsWith(CommentPrefix)) continue;
                kept.Add(lines[i]);        // 🔴 原样收下（含 `\r`）：归一留给 TryDecode，别在这里掩盖"混行尾"
                map.Add(i + 1);            // 1 基物理行号
            }

            lineMap = map.ToArray();
            // 🔴 末尾换行**跟着原文**走（原文以换行结尾才补）：闸② 报的"数据部分 N 字符"必须是文件里**真实**的字符数，
            //    无条件补一个 `\n` 会让"没有末尾换行"的文件凭空多算 1 个字符（实测被断言当场抓出来）。
            return kept.Count == 0
                ? string.Empty
                : string.Join("\n", kept) + (text.EndsWith("\n", StringComparison.Ordinal) ? "\n" : string.Empty);
        }

        /// <summary>载荷内第 <paramref name="payloadLine"/> 行（1 基）对应的**文件物理行号**；越界给 <c>0</c>。</summary>
        private static int PhysicalLine(int[] lineMap, int payloadLine)
        {
            int index = payloadLine - 1;
            return index >= 0 && index < lineMap.Length ? lineMap[index] : 0;
        }

        /// <summary>载荷内第 <paramref name="lineNumber"/> 行（1 基）的原文；越界给 <c>null</c>。</summary>
        private static string RawLine(string payload, int lineNumber)
        {
            string normalized = (payload ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
            string[] lines = normalized.Split('\n');
            int index = lineNumber - 1;
            return index < 0 || index >= lines.Length ? null : lines[index];
        }

        /// <summary>
        /// 找第一处**标识重复**（按 <see cref="ScreenTier.Id"/>，序数比较）。给的是**列表下标**，由调用方换算成行号。
        /// <para>🔴 为什么值得单独一条：清单文件要进版本库、会被手工改、会被 git 合并——而重复标识**不会报错**，
        /// <see cref="ScreenTierSet"/> 的构造按 <c>Id</c> 去重、**只留第一条** ⇒ 导入被合并坏的清单会**静默少一档**
        /// （以为 9 档、实际 8 档，且没有任何提示）。⇒ 文件层必须**响亮拒绝**。</para>
        /// </summary>
        private static bool TryFindDuplicate(List<ScreenTier> tiers, out int firstIndex, out int duplicateIndex, out string id)
        {
            firstIndex = 0;
            duplicateIndex = 0;
            id = null;

            var seen = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < tiers.Count; i++)
            {
                if (seen.TryGetValue(tiers[i].Id, out int first)) { firstIndex = first; duplicateIndex = i; id = tiers[i].Id; return true; }
                seen[tiers[i].Id] = i;
            }
            return false;
        }
    }
}
