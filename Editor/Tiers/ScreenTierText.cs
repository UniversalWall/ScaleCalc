using System;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// 档位操作**给人看的字**（集中一处，免得散在回调里各写一套）。
    /// <para>🔴 **措辞即契约**：这些句子有逐字断言守着，改字必须同步改断言。</para>
    /// </summary>
    public static class ScreenTierText
    {
        /// <summary>内置档点了删除（给可操作的下一步，而不是"不可删"就完了）。</summary>
        public static string BuiltInNotDeletable(string name)
            => "「" + name + "」是内置档位，不可删除；要不算它就取消勾选";

        /// <summary>自定义行已删。</summary>
        public static string Deleted(string name) => "已删除「" + name + "」（可用「新增一行」重建）";

        /// <summary>取消勾选。</summary>
        public static string Excluded(string name) => "已排除「" + name + "」：本行不参与计算（仍可在上表勾回）";

        /// <summary>恢复勾选。</summary>
        public static string Included(string name) => "已纳入「" + name + "」";

        /// <summary>全不选（如实写 0 档，**不自动勾回**）。</summary>
        public static string NoneIncluded() => "当前 0 档参与计算（上表可勾回）";

        /// <summary>新增成功。</summary>
        public static string Added(string name)
            => "已新增「" + name + "」——尺寸可直接在表里改（回车或点开别处提交）";

        /// <summary>
        /// 尺寸文本**根本解析不出两个数**。
        /// <para><see cref="ScreenTierSet.InvalidSizeMessage"/> 要求已经解析出两个数，
        /// 而"连数都不是"这种情况它表达不了——所以单独一条，并把**原文**照抄给使用者。</para>
        /// </summary>
        public static string UnparsableSize(string text)
            => "尺寸要写成「宽x高」两个大于 0 的数（收到「" + text + "」）";

        /// <summary>改名：自定义行的名字被清空 ⇒ **已回落到自动名**。</summary>
        public static string NameFellBackToAuto(string auto) => "名字清空 ⇒ 已恢复自动名「" + auto + "」";

        /// <summary>
        /// 改名：**内置档**的名字被清空 ⇒ 拒绝。
        /// <para>内置档名对的是清单口径，没有"自动名"可回落；这里必须说清"没改成"，而不是静默不动。</para>
        /// </summary>
        public static string BuiltInNameCannotBeEmpty() => "内置档位的名字不能为空（名字未改动）";
    }
}
