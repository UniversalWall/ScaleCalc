using System;
using System.Globalization;
using System.IO;
using System.Text;
using Wayward.ScaleCalc.Unity;
using UnityEditor;
using UnityEngine;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// **换算证据包生成器**："一条命令可重新生成"——≥5 组「参考分辨率 × 屏幕分辨率」，
    /// 四列（内核值 / 真值 / 差值 / **输入源**）+ 参考分辨率与 match 标注。
    /// <para>入口：选型台窗口按钮「导出换算证据包（≥5 组）」（保存对话框），或脚本调用 <see cref="ExportTo(string)"/>——
    /// **落点必须由调用方显式给出**：本类**不**提供"自行选址"的无参导出入口，
    /// 也不建目录链、不静默覆盖（两条硬前置见 <see cref="EvidenceExporter.Export"/>）。</para>
    /// <para>⚠️ <b>真值只在一个地方有效</b>：现场只对账一次（<see cref="LiveReference"/> × <see cref="LiveMatch"/>），
    /// 其余参考分辨率/match 的块整表离线——拿别的条件的真值来减会得到**假差值**。</para>
    /// </summary>
    public static partial class EvidencePackCommand
    {
        /// <summary>现场对账用的参考分辨率（= <see cref="References"/> 的第 1 组）。**只有这一组**带现场真值。</summary>
        public static readonly ScaleSize LiveReference = new ScaleSize(1920f, 1080f);

        /// <summary>现场对账用的 <c>match</c>（= <see cref="MatchLadder"/> 的中间那档）。</summary>
        public const float LiveMatch = 0.5f;

        /// <summary>参考分辨率档（≥5 组，覆盖竖屏/横屏/方屏）。</summary>
        private static readonly ScaleSize[] References =
        {
            new ScaleSize(1920f, 1080f),
            new ScaleSize(1280f, 720f),
            new ScaleSize(1080f, 1920f),
            new ScaleSize(750f, 1334f),
            new ScaleSize(2560f, 1440f),
            new ScaleSize(800f, 600f),
        };

        /// <summary>match 档（三个值即可看出"对数平均"与两端退化）。</summary>
        private static readonly float[] MatchLadder = { 0f, LiveMatch, 1f };

        /// <summary>
        /// 证据目录约定（**相对工程根**）——**只作为窗口保存对话框的初值**，
        /// 不是任何"包内自选落点"的行为依据（导出永远由调用方给路径）。
        /// </summary>
        public const string EvidenceFolder = "Document/Works/ScaleCalc/Evidence";

        public const string EvidenceFileName = "ScaleCalc-005-换算证据包.csv";

        /// <summary>
        /// 窗口保存对话框的**初值**（不是导出落点）：工程根下存在 <see cref="EvidenceFolder"/> 时给出该目录里的默认文件名；
        /// **没有这棵树时退化为"只给一个文件名"**——那只是个文件名，不拼目录、也不建目录
        /// （建目录链会往别人的工程里凭空造出目录树，这正是本作品明确不做的事）。
        /// <para>⇒ 消费者工程（没有本平台那套文档目录）里，对话框初值 = 工程根 + 默认文件名。</para>
        /// </summary>
        public static string DefaultPath
        {
            get
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                string folder = Path.Combine(projectRoot, EvidenceFolder);
                return Directory.Exists(folder)
                    ? Path.GetFullPath(Path.Combine(folder, EvidenceFileName))
                    : EvidenceFileName;      // 退化：只有文件名 ⇒ 对话框落在工程根，不会在别人的工程里造出目录树
            }
        }

        /// <summary>
        /// 现场条件下的一份模板（= <see cref="LiveReference"/> × <see cref="LiveMatch"/>）——**只描述"测什么条件"，
        /// 本身不碰任何东西**；真正会动场景的是 <see cref="MeasureLive"/>（证据包默认就是按这个条件对账的）。
        /// </summary>
        public static ScaleCalcInput LiveTemplate()
        {
            ScaleCalcInput template = ScaleCalcInput.Default;
            template.Mode = ScaleMode.ScaleWithScreenSize;
            template.ReferenceResolution = LiveReference;
            template.ScreenMatch = ScreenMatchMode.MatchWidthOrHeight;
            template.MatchWidthOrHeight = LiveMatch;
            template.ScreenDpi = Screen.dpi;
            return template;
        }

        /// <summary>
        /// 会建/拆临时场景：现场对账一次，取引擎真值。
        /// <para><b>调用方须知</b>：本方法会建一个 additive 临时场景、挂 Canvas/CanvasScaler、显式反射 `Handle()`
        /// 读数、再把**自己建的**那个场景关掉。宿主必须是**已保存**且**就是当前活动场景**，否则直接拒绝、
        /// 零副作用（三条硬前置见 <see cref="LiveReconcile.TryMeasure(ScaleCalcInput, UnityEngine.SceneManagement.Scene, out LiveTruth, out string)"/>）。</para>
        /// <para><b>除了这里，本作品没有别的地方会为了取真值而动场景</b>——纯函数 <see cref="BuildCsv(out int)"/> 一个场景操作都不做。
        /// 这也是"名字必须说出副作用"的落地：看到 <c>MeasureLive</c> 就该知道它会动场景。</para>
        /// <para><paramref name="template"/> 留空 ⇒ 用 <see cref="LiveTemplate"/>（证据包的现场条件；
        /// 选型台会传它**当前**的参考分辨率/match，好让真值与表上的条件自洽）。</para>
        /// </summary>
        public static bool MeasureLive(out LiveTruth truth, out string error)
            => MeasureLive(default, out truth, out error);

        /// <summary>
        /// 会建/拆临时场景：按 <paramref name="template"/> 给的条件现场对账一次（重载说明见上）。
        /// </summary>
        public static bool MeasureLive(ScaleCalcInput template, out LiveTruth truth, out string error)
        {
            // `template == default` 只可能是"没给条件"（内核默认值不是零值：它有字段初值）
            if (template.ReferenceResolution.Width <= 0f) template = LiveTemplate();
            return LiveReconcile.TryMeasure(template, out truth, out error);
        }

        /// <summary>
        /// 导出到指定路径：**相对路径按项目根解析**；返回写出的**绝对路径**。
        /// <para>🔴 **落点必须由调用方给出**——没有"猜一个默认位置"的重载。</para>
        /// <para>⚠️ 目标父目录必须已存在、已存在文件未获许可不覆盖：这两条由 <see cref="EvidenceExporter.Export"/> 判定，
        /// 失败**如实抛出**（窗口负责把异常写进状态栏）——证据导出不许"静默成功"。</para>
        /// <para>覆盖许可默认**不开**：调用方要么先拿到用户的显式确认再传 <c>true</c>，要么就老实收到异常。</para>
        /// <para>🔴 **本方法不取真值、不碰场景**：要带现场真值就自己先 <see cref="MeasureLive"/>
        /// 再把结果当参数传进来；不传就是一份**全离线列**的证据包。</para>
        /// </summary>
        public static string ExportTo(string path, LiveTruth truth = default, bool truthOk = false,
                                      bool allowOverwrite = false)
            => ExportTo(path, out _, truth, truthOk, allowOverwrite);

        /// <summary>
        /// 导出到指定路径（窗口的保存对话框走这里）：**相对路径按项目根解析**；返回写出的**绝对路径**，
        /// <paramref name="dataRows"/> 给出**实际**写出的数据行数。
        /// <para>🔴 **落点必须由调用方给出**：本类没有"按仓库布局自行选址"的入口。</para>
        /// <para>⚠️ 两条硬前置由 <see cref="EvidenceExporter.Export"/> 判定并**如实抛出**；
        /// <paramref name="allowOverwrite"/> 默认**不开**——窗口传 <c>true</c>，依据是保存对话框的"替换吗？"
        /// 已经拿到了用户的显式许可。</para>
        /// <para>🔴 **本方法不取真值、不碰场景**：真值走 <paramref name="truth"/> 参数进来。</para>
        /// </summary>
        public static string ExportTo(string path, out int dataRows, LiveTruth truth = default, bool truthOk = false,
                                      bool allowOverwrite = false)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("导出路径为空", nameof(path));

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string resolved = Path.IsPathRooted(path) ? path : Path.Combine(projectRoot, path);
            EvidenceExporter.Export(BuildCsv(out dataRows, truth, truthOk), resolved, allowOverwrite);
            return Path.GetFullPath(resolved);
        }

        /// <summary>参考分辨率档数（≥5 组）。</summary>
        public static int ReferenceCount => References.Length;
    }
}
