using UnityEditor;
using UnityEngine.UIElements;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// <see cref="ScaleCalcWindow"/> 的**就地编辑章节**（本文件只放"用户在管理表里敲进来的两个字段"的提交口）。
    /// <para>两个字段的**口径**都不在这里：尺寸的解析与判据在 <see cref="ScreenTierSet.TryParseSize"/> / <see cref="ScreenTierSet.Resize"/>，
    /// 改名的判据在 <see cref="ScreenTierSet.TryResolveRename"/> —— 那样它们在 EditMode 里**不开窗即可断言**。
    /// 本文件只做"转达 + 刷新 + 落盘 + 报文案"。</para>
    /// <para>其余章节：工作集与三件操作在 `ScaleCalcWindow.Rows.cs` · 对账在 `…Measure.cs` · 导出在 `…Export.cs`。</para>
    /// </summary>
    public sealed partial class ScaleCalcWindow
    {
        /// <summary>
        /// 尺寸就地编辑提交：解析 → 尺寸非法就回滚并说明；合法就改数据，**自动名跟着尺寸走**。
        /// <para>⚠️ 名字**只在自己是自动名时**才跟着改——使用者手动取的名字不许被尺寸编辑悄悄抹掉。</para>
        /// </summary>
        bool ITierTableHost.TryCommitTierSize(string id, string text)
        {
            if (!ScreenTierSet.TryParseSize(text, out ScaleSize size))
            {
                Report(ScreenTierText.UnparsableSize(text));
                return false;
            }
            if (!TryGetTier(id, out ScreenTier before)) return false;

            // ⚠️ 「是不是自动名」必须用**改尺寸之前**那一份来判（改完再比就永远判不出来）
            bool wasAutoName = before.Kind == ScreenTierKind.Custom
                               && before.Name == ScreenTierSet.AutoName(before.Size);

            if (!m_TierSet.Resize(id, size)) { Report(ScreenTierSet.InvalidSizeMessage(size)); return false; }
            if (wasAutoName) m_TierSet.Rename(id, ScreenTierSet.AutoName(size));

            Rebuild();
            SaveTiers();
            ScheduleTierTableRebind(null);       // 名字列要跟着变，所以整表重绑（不试着手改单元格）
            return true;
        }

        /// <summary>
        /// 改名就地编辑提交：**口径在 <see cref="ScreenTierSet.TryResolveRename"/>**（纯函数、可断言），
        /// 这里只负责落地与刷新。<c>false</c> ⇒ 视图回滚（内置档被清空 / 名字没变都算"不落地"）。
        /// <para>落地后要整表重绑：选型表的「屏幕档位」列吃的是同一个名字，只改一个单元格会两张表不同步。</para>
        /// </summary>
        bool ITierTableHost.TryCommitTierName(string id, string text)
        {
            if (!TryGetTier(id, out ScreenTier before)) return false;

            bool apply = ScreenTierSet.TryResolveRename(before.Kind, before.Size, text, before.Name,
                out string resolved, out string notice);
            if (!apply)
            {
                if (notice != null) Report(notice);          // 内置档被清空 / 名字没变（后者 notice 为 null ⇒ 什么都不说）
                return false;
            }

            if (!m_TierSet.Rename(id, resolved)) return false;
            Rebuild();
            SaveTiers();
            ScheduleTierTableRebind(null);
            if (notice != null) Report(notice);              // 截断提示 / "已恢复自动名"
            return true;
        }

        /// <summary>尺寸的显示文本（绑定与回滚共用 <see cref="ScaleSize.ToString"/>，两处不会不一致）。</summary>
        string ITierTableHost.TierSizeText(string id)
            => TryGetTier(id, out ScreenTier tier) ? tier.Size.ToString() : string.Empty;

        /// <summary>档位名的显示文本（绑定与回滚共用同一个来源，两处不会不一致）。</summary>
        string ITierTableHost.TierNameText(string id)
            => TryGetTier(id, out ScreenTier tier) ? tier.Name : string.Empty;
    }
}
