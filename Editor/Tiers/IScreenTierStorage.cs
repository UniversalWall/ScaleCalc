using System.Globalization;
using UnityEditor;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>工作集的存储抽象（可注入假实现 ⇒ 测试不必依赖真 <c>EditorPrefs</c>、也不会污染使用者的配置）。</summary>
    public interface IScreenTierStorage
    {
        /// <summary>读载荷。<c>false</c> = **没存过**（不是错误）。</summary>
        bool TryLoad(out string payload);

        /// <summary>写载荷。<c>false</c> = 写失败（容量 / 权限）。</summary>
        bool TrySave(string payload);

        /// <summary>删除载荷（测试清理 / 使用者重置用）。</summary>
        void Delete();
    }
}
