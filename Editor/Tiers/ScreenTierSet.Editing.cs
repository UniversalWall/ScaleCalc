using System.Globalization;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// <see cref="ScreenTierSet"/> 的**就地编辑章节**（本文件只放"用户敲进来的两个字段"的**纯判据**）。
    /// <para>两个纯函数都**只用参数、不碰实例状态** ⇒ 窗口与断言共用同一份口径（不开窗即可断言）。</para>
    /// <para>背景：改名原先只做到数据层（<c>Rename</c>），**界面没有入口**；补入口时把"空名字怎么办"
    /// 这类口径从窗口里挪到这里来 —— 窗口只负责"转达 + 刷新 + 落盘 + 报文案"。</para>
    /// </summary>
    public sealed partial class ScreenTierSet
    {
        /// <summary>
        /// **改名提交的纯判据**（窗口与断言共用）。
        /// <para>返回 <c>true</c> ⇒ 名字改成 <paramref name="resolved"/>；<c>false</c> ⇒ **不落地**（调用方把显示回滚），
        /// 且 <paramref name="notice"/> 非空时**如实报告**；<c>false</c> + <c>null</c> = 名字没变、无事可做。</para>
        /// <para>🔴 **空名的口径按种类分**（内置与自定义都能改名）：
        /// 自定义行 ⇒ **回落到自动名**（与「新增一行」同源 ⇒ 把输入框清空就能回到自动名）；
        /// 内置档 ⇒ **拒绝**（内置档名对的是清单口径，没有"自动名"可回落；来源标注按 <see cref="ScreenTier.Id"/> 里嵌的**原名**查，本就不受影响）。</para>
        /// <para>超 <see cref="MaxNameLength"/> 字照 <see cref="NormalizeName"/> 截断，并带上它的提示。</para>
        /// </summary>
        public static bool TryResolveRename(ScreenTierKind kind, ScaleSize size, string raw, string current,
            out string resolved, out string notice)
        {
            current = current ?? string.Empty;
            resolved = NormalizeName(raw, out notice);          // 非空 notice = 截断提示
            if (resolved.Length > 0) return resolved != current;

            if (kind == ScreenTierKind.Custom)
            {
                string auto = AutoName(size);
                if (auto == current) return false;              // 本来就是自动名 ⇒ 无事可做（也别报"已恢复"）
                resolved = auto;
                notice = ScreenTierText.NameFellBackToAuto(auto);
                return true;
            }

            resolved = current;
            notice = ScreenTierText.BuiltInNameCannotBeEmpty();
            return false;
        }

        /// <summary>
        /// 解析「宽x高」文本（也认大写 `X` 与半角逗号）。**只解析形状**，数值合法性交给 <see cref="IsValidSize"/>。
        /// <para>**住在数据层而不是窗口里**：它是纯函数 ⇒ 不开窗即可断言；
        /// 顺带把窗口那一章腾出十几行（那个文件贴着 200 行红线）。</para>
        /// </summary>
        public static bool TryParseSize(string text, out ScaleSize size)
        {
            size = default;
            if (string.IsNullOrEmpty(text)) return false;

            string[] parts = text.Split('x', 'X', ',');
            if (parts.Length != 2) return false;
            if (!float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float w)) return false;
            if (!float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float h)) return false;
            size = new ScaleSize(w, h);
            return true;
        }
    }
}
