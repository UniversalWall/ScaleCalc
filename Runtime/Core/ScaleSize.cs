using System;
using System.Globalization;

namespace Wayward.ScaleCalc
{
    /// <summary>
    /// 宽高对。**不用 <c>UnityEngine.Vector2</c>**（内核零 Unity 依赖），也不用 <c>System.Numerics</c>（避免多引一个程序集）。
    /// 字段是 <c>float</c>：与引擎同精度，中间结果不做 <c>double</c> 提升——提升会与引擎算出不同的舍入。
    /// </summary>
    public readonly struct ScaleSize : IEquatable<ScaleSize>
    {
        public readonly float Width;
        public readonly float Height;

        public ScaleSize(float width, float height)
        {
            Width = width;
            Height = height;
        }

        public bool Equals(ScaleSize other) => Width.Equals(other.Width) && Height.Equals(other.Height);

        public override bool Equals(object obj) => obj is ScaleSize other && Equals(other);

        public override int GetHashCode() => unchecked(Width.GetHashCode() * 397 ^ Height.GetHashCode());

        public static bool operator ==(ScaleSize a, ScaleSize b) => a.Equals(b);

        public static bool operator !=(ScaleSize a, ScaleSize b) => !a.Equals(b);

        /// <summary>不变文化格式（断言失败信息与证据输出都可读、可比对）。</summary>
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0}x{1}", Width, Height);
    }
}
