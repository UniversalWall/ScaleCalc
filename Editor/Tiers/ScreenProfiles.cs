using System.Collections.Generic;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>内置屏幕档位（≥6 档、宽高比互不相同——保守性质断言也吃这份清单）。</summary>
    public static class ScreenProfiles
    {
        /// <summary>内置档的**来源列原文**：这些是"档位草案"（手填），不是"实测本机屏幕"。</summary>
        /// <remarks>结论条那句"（含 N 档内置草案）"按它计数 ⇒ **两处共用同一个常量**，免得各写一遍字面量。</remarks>
        public const string DraftSource = "档位草案";

        /// <summary>⚠️ 这些是**档位草案**（手填），不是"实测本机屏幕"；来源列由导出如实标注。</summary>
        public static readonly ScreenProfile[] All =
        {
            new ScreenProfile("手机竖屏 9:16", 1080f, 1920f, DraftSource),
            new ScreenProfile("手机竖屏超长 9:19.5", 1440f, 3120f, DraftSource),
            new ScreenProfile("手机横屏 16:9", 1920f, 1080f, DraftSource),
            new ScreenProfile("平板 4:3", 2048f, 1536f, DraftSource),
            new ScreenProfile("方屏 1:1", 1080f, 1080f, DraftSource),
            new ScreenProfile("超宽 21:9", 2560f, 1080f, DraftSource),
            new ScreenProfile("小屏 4:3", 640f, 480f, DraftSource),
        };

        /// <summary>宽高比互不相同的档位数（保守性质断言要求 ≥3；这里作为自检暴露出来）。</summary>
        public static int DistinctAspectRatioCount()
        {
            var seen = new List<int>();
            foreach (ScreenProfile profile in All)
            {
                int key = (int)(profile.AspectRatio * 1000f);
                if (!seen.Contains(key)) seen.Add(key);
            }
            return seen.Count;
        }
    }
}
