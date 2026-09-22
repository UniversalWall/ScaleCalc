using System.Globalization;
using UnityEditor;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// <c>EditorPrefs</c> 实现。🔴 键 = 包名前缀 **+ 工程路径短哈希**：
    /// <c>EditorPrefs</c> 是**每工程**的存储，而本作品会装进**别人的工程**——只用包名前缀的话，
    /// 同一台机器上**不同工程**会互相覆盖工作集。
    /// <para><b>已知代价</b>：工程被移动 / 复制 ⇒ 旧键成为**孤儿**（不自动清理——只报告、不清理）。</para>
    /// <para>载荷的编解码与容错**不在这里**（见 <see cref="ScreenTierStorage"/>）：本类只负责"写哪儿、读到没读到"。</para>
    /// </summary>
    public sealed class EditorPrefsScreenTierStorage : IScreenTierStorage
    {
        /// <summary><c>EditorPrefs</c> 键前缀（后面拼工程路径短哈希，见 <see cref="KeyForProjectPath"/>）。</summary>
        public const string KeyPrefix = "Wayward.ScaleCalc.ScreenTiers.";

        private readonly string m_Key;

        /// <summary>默认按**当前工程**派生键。</summary>
        public EditorPrefsScreenTierStorage()
            : this(KeyForProjectPath(ProjectPath()))
        {
        }

        /// <summary>显式指定键（测试用：给个一次性键就不会碰到使用者的真实工作集）。</summary>
        public EditorPrefsScreenTierStorage(string key)
        {
            m_Key = key;
        }

        /// <summary>实际用的键（测试与排障读取）。</summary>
        public string Key => m_Key;

        /// <summary>
        /// 工程路径 ⇒ 存储键。用 **FNV-1a 32 位**，**不是** <c>string.GetHashCode</c>：
        /// 后者在 .NET Core 上**每次进程启动都变**（随机化哈希）⇒ 存下去就再也读不回来（那是个静默的数据丢失）。
        /// <para>路径先统一分隔符并小写（Windows 路径大小写不敏感）⇒ 同一工程的不同写法得到同一个键。</para>
        /// <para>⚠️ 本方法必须是**确定性**的：改它等于让所有既有使用者的工作集变成孤儿键。</para>
        /// </summary>
        public static string KeyForProjectPath(string projectPath)
        {
            string normalized = (projectPath ?? string.Empty).Replace('\\', '/').ToLowerInvariant();
            uint hash = 2166136261u;
            foreach (char c in normalized)
            {
                hash ^= c;
                hash *= 16777619u;   // uint 乘法按定义回绕，不需要 unchecked 块
            }
            return KeyPrefix + hash.ToString("x8", CultureInfo.InvariantCulture);
        }

        public bool TryLoad(out string payload)
        {
            if (!EditorPrefs.HasKey(m_Key)) { payload = null; return false; }
            payload = EditorPrefs.GetString(m_Key);
            return true;
        }

        public bool TrySave(string payload)
        {
            // `EditorPrefs.SetString` 不返回失败信号 ⇒ 这里只能如实报"已调用"。
            // 真正的保护是调用前的长度闸门——不编一个假的 Try 语义出来。
            EditorPrefs.SetString(m_Key, payload);
            return true;
        }

        public void Delete() => EditorPrefs.DeleteKey(m_Key);

        /// <summary>工程根目录（<c>Assets/</c> 的上一级）。</summary>
        private static string ProjectPath() => System.IO.Path.GetDirectoryName(UnityEngine.Application.dataPath);
    }
}
