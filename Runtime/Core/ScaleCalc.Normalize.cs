namespace Wayward.ScaleCalc
{
    public static partial class ScaleCalc
    {
        /// <summary>
        /// 归一化的结果：夹取后的输入 + "哪几处夹取**真的**改过值"（透明化，出进诊断）。
        /// </summary>
        internal readonly struct Normalized
        {
            public readonly ScaleCalcInput Input;
            public readonly bool ClampedReferenceResolution;
            public readonly bool ClampedScaleFactor;
            public readonly bool ClampedDefaultSpriteDPI;

            public Normalized(ScaleCalcInput input, bool clampedReferenceResolution, bool clampedScaleFactor,
                              bool clampedDefaultSpriteDpi)
            {
                Input = input;
                ClampedReferenceResolution = clampedReferenceResolution;
                ClampedScaleFactor = clampedScaleFactor;
                ClampedDefaultSpriteDPI = clampedDefaultSpriteDpi;
            }
        }

        /// <summary>
        /// 夹取与归一化。**内核一律夹取**——引擎的属性 setter 会夹、Inspector 直写字段不会，
        /// 这是登记过的**合法差异**：内核在任何输入下都不产出 <c>∞</c>/<c>NaN</c>。
        /// <para>⚠️ 前提：调用方给的是**有限数**（输入本身为 <c>NaN</c> 不在定义域内；引擎同样不做清洗）。</para>
        /// </summary>
        internal static Normalized Normalize(in ScaleCalcInput raw)
        {
            float rw = ClampResolutionComponent(raw.ReferenceResolution.Width, out bool clampedW);
            float rh = ClampResolutionComponent(raw.ReferenceResolution.Height, out bool clampedH);
            float sf = ClampScaleFactor(raw.ConstantScaleFactor, out bool clampedSf);
            float dp = ClampDefaultSpriteDpi(raw.DefaultSpriteDPI, out bool clampedDp);

            ScaleCalcInput i = raw;                                          // 值拷贝：**不改调用方的那个结构**
            i.ReferenceResolution = new ScaleSize(rw, rh);
            i.ConstantScaleFactor = sf;
            i.DefaultSpriteDPI = dp;
            i.MatchWidthOrHeight = Clamp01(raw.MatchWidthOrHeight);          // 等价于引擎在 Mathf.Lerp 内部的夹取

            return new Normalized(i, clampedW || clampedH, clampedSf, clampedDp);
        }

        /// <summary>
        /// 参考分辨率分量：两层夹取。
        /// <list type="number">
        /// <item>镜像 <c>CanvasScaler.referenceResolution</c> 的 setter：<c>|v| &lt; 1e-5 ⇒ 1e-5 * Sign(v)</c>（<c>Sign(0) = +1</c>）。</item>
        /// <item>内核兜底：非正分量喂进 <c>Log</c> 会产 <c>NaN</c>/<c>∞</c>，所以不允许非正。</item>
        /// </list>
        /// </summary>
        private static float ClampResolutionComponent(float v, out bool clamped)
        {
            const float kMin = 0.00001f;
            float original = v;
            if (v > -kMin && v < kMin) v = kMin * Sign(v);
            if (v <= 0f) v = kMin;
            clamped = v != original;
            return v;
        }

        /// <summary>常量像素模式的缩放系数：镜像属性 setter + <c>OnValidate</c>（<c>≥ 0.01</c>）。</summary>
        private static float ClampScaleFactor(float v, out bool clamped)
        {
            clamped = v < 0.01f;
            return clamped ? 0.01f : v;
        }

        /// <summary>精灵基准 DPI：镜像属性 setter + <c>OnValidate</c>（<c>≥ 1</c>）。</summary>
        private static float ClampDefaultSpriteDpi(float v, out bool clamped)
        {
            clamped = v < 1f;
            return clamped ? 1f : v;
        }
    }
}
