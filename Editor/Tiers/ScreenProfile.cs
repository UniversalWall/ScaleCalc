using System.Collections.Generic;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// 一台"屏幕档位"（选型台的**行**；也是证据包的行标识）。
    /// <para><see cref="Source"/> 说明这一档是怎么来的（内置草案 / 自定义），导出时如实带出去。</para>
    /// </summary>
    public readonly struct ScreenProfile
    {
        public readonly string Name;
        public readonly ScaleSize Size;
        public readonly string Source;

        public ScreenProfile(string name, float width, float height, string source)
        {
            Name = name;
            Size = new ScaleSize(width, height);
            Source = source;
        }

        public float AspectRatio => Size.Height == 0f ? 0f : Size.Width / Size.Height;

        public override string ToString() => Name + " " + Size;
    }
}
