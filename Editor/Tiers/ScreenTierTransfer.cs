using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// 档位清单的**导入 / 导出编排**：碰 IO，但**不改全局状态** —— 它只负责
    /// "文件 ⇄ 候选列表"，替换工作集由窗口侧做（那样才守得住"解析全过了才动数据"）。
    /// <para>🔴 **不建目录、不询问覆盖、不碰 <c>EditorPrefs</c>、不碰场景**：写盘的两条硬前置由
    /// <see cref="EvidenceExporter.Export"/> 承担。</para>
    /// </summary>
    public static class ScreenTierTransfer
    {
        /// <summary>默认文件名（落在工程根，名字自带作品名，免得同事的仓库里出现一个认不出的 <c>.txt</c>）。</summary>
        public const string DefaultFileName = "ScaleCalc-屏幕档位清单.txt";

        /// <summary>没选文件的逐字文案（脚本直调时也会看到它）。**不是异常**——"用户没选"是正常路径。</summary>
        public const string NoPathMessage = "未选择文件";

        /// <summary>
        /// 默认落点：<paramref name="projectRoot"/> 非空 ⇒ 工程根下；**为空 ⇒ 只给文件名**
        /// （退化口径，与 <see cref="EvidencePackCommand.DefaultPath"/> 一致——消费者工程里没有工程根也不能崩）。
        /// </summary>
        public static string DefaultPath(string projectRoot)
            => string.IsNullOrEmpty(projectRoot) ? DefaultFileName : Path.Combine(projectRoot, DefaultFileName);

        /// <summary>
        /// 导出到 <paramref name="path"/>，返回实际写入路径（窗口拿它拼成功文案）。
        /// <para>🔴 <paramref name="allowOverwrite"/> 由**调用方**决定：窗口那条路传 <c>true</c>（保存对话框的
        /// "替换吗？"就是用户的显式许可）；脚本直调传 <c>false</c> 就会撞上拒绝路径。</para>
        /// <para>⚠️ **不带 BOM**：这份文件要进版本库、要被人手编辑，多出 3 个字节会被 git 当内容差异、
        /// 被别的编辑器当怪字符。写盘助手默认带 BOM，所以这里**显式**关掉。</para>
        /// </summary>
        /// <exception cref="InvalidOperationException">载荷超过长度上限（纵深闸门，见下）。</exception>
        /// <exception cref="DirectoryNotFoundException">父目录不存在。</exception>
        /// <exception cref="IOException">目标文件已存在且未获覆盖许可。</exception>
        /// <exception cref="ArgumentException"><paramref name="path"/> 为空。</exception>
        public static string ExportTo(ScreenTierSet set, string path, bool allowOverwrite)
        {
            string text = ScreenTierListFile.Describe(set);

            // 纵深闸门：保存路径（`TrySave`）已经挡过一次，这里是"闸门不只在一条路上"的第二道。
            // ⚠️ 按当前上限这是**不可能发生**的分支（32 行 × 名字 ≤32 字远够不到 8192），但它必须存在且可断言：
            //    一旦有人放宽上限，这里就是最后一道，而不是让一份超长清单悄悄落盘。
            string payload = ScreenTierStorage.Encode(set);
            if (ScreenTierStorage.IsPayloadTooLarge(payload))
                throw new InvalidOperationException("工作集太大，无法导出清单（数据部分 " + payload.Length
                    + " 字符 > " + ScreenTierStorage.MaxPayloadLength + "）——请先删掉一些自定义档位。");

            EvidenceExporter.Export(text, path, allowOverwrite, withBom: false);
            return path;
        }

        /// <summary>
        /// 读文件 → 解析 → 得到**候选列表**（**尚未生效**：调用方负责替换）。
        /// <para>返回 <c>false</c> 时 <paramref name="message"/> 给可读原因（解析类**含行号**），
        /// 且**当前工作集一个字段都不动**——所以"坏输入零副作用"是调用结构保证的，不靠调用方自觉。</para>
        /// <para>宽容口径：<c>File.ReadAllText</c> **会吃掉 BOM**（别人用带 BOM 的编辑器存过也能读）；
        /// <c>TryDecode</c> 统一换行 ⇒ CRLF 也能读。</para>
        /// <para>文件不存在 / 是目录 / 无权限 ⇒ **不在这里兜底**：由 <see cref="IOException"/> /
        /// <see cref="UnauthorizedAccessException"/> 冒到窗口侧统一报（带路径的那种文案）。</para>
        /// </summary>
        public static bool TryImport(string path, out List<ScreenTier> tiers, out string message)
        {
            tiers = null;

            if (string.IsNullOrEmpty(path)) { message = NoPathMessage; return false; }

            string text = File.ReadAllText(path, new UTF8Encoding(false));
            if (!ScreenTierListFile.TryParseFile(text, out tiers, out string reason))
            {
                message = reason;
                return false;
            }

            message = null;
            return true;
        }
    }
}
