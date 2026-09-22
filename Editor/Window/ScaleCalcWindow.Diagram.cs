using UnityEngine;
using UnityEngine.UIElements;

namespace Wayward.ScaleCalc.Editor
{
    /// <summary>
    /// <see cref="ScaleCalcWindow"/> 的**安全区示意图章节**。
    /// <para>**外框 = 参考画布**、**内框 = 安全设计区**（居中）、**四条边带 = 危险带**（参考相对安全区被裁掉的那一圈）；
    /// 边带**红 = 至少一档会裁、黄 = 全都不裁**。几何全在 <see cref="ScaleFit.DiagramOf"/>（纯函数）——本文件只摆元素。</para>
    /// <para>🔴 **它放在结论区右侧**（不是在下面另起一段）：高度被左边那几百字的文本块**吸收**，
    /// 所以**不抬窗口底线**（加段落必须算预算——这一处是靠"横向排布"把账算平的）。
    /// 🔴 只画几何，**不承诺"某个具体 UI 不会被裁"**：图旁边就是危险带那一行的数字。</para>
    /// </summary>
    public sealed partial class ScaleCalcWindow
    {
        /// <summary>示意图画布尺寸（16:9 ⇒ 与参考分辨率同形；固定尺寸 ⇒ 布局可预算）。</summary>
        private static readonly Vector2 DiagramSize = new Vector2(200f, 113f);

        private VisualElement m_Diagram;
        private VisualElement m_DiagramOuter;
        private VisualElement m_DiagramInner;
        private VisualElement[] m_DiagramBands;

        /// <summary>控件就位（在第一次 <c>Rebuild()</c> **之前**调）。</summary>
        private void InitDiagramSection()
        {
            m_Diagram = rootVisualElement.Q<VisualElement>("safe-area-diagram");
            if (m_Diagram == null) return;

            m_Diagram.style.width = DiagramSize.x;
            m_Diagram.style.height = DiagramSize.y;

            m_DiagramOuter = new VisualElement { name = "diagram-outer" };
            m_DiagramOuter.style.position = Position.Absolute;
            m_DiagramOuter.style.left = 0f;
            m_DiagramOuter.style.top = 0f;
            m_DiagramOuter.style.width = Length.Percent(100f);
            m_DiagramOuter.style.height = Length.Percent(100f);
            m_DiagramOuter.AddToClassList("diagram-outer");
            m_Diagram.Add(m_DiagramOuter);

            m_DiagramInner = new VisualElement { name = "diagram-inner" };
            m_DiagramInner.style.position = Position.Absolute;
            m_DiagramInner.AddToClassList("diagram-inner");
            m_Diagram.Add(m_DiagramInner);

            // 四条边带（左 / 右 / 上 / 下）——危险带，颜色按"有没有会裁的档位"
            m_DiagramBands = new VisualElement[4];
            for (int i = 0; i < m_DiagramBands.Length; i++)
            {
                m_DiagramBands[i] = new VisualElement { name = "diagram-band-" + i };
                m_DiagramBands[i].style.position = Position.Absolute;
                m_Diagram.Add(m_DiagramBands[i]);
            }
        }

        /// <summary>按当前统计重画（在 <c>Rebuild()</c> 末尾、拿到新 <c>m_Stats</c> 之后调）。</summary>
        private void UpdateDiagram(ScaleSize reference)
        {
            if (m_Diagram == null || m_DiagramOuter == null) return;

            ScaleFit.DiagramLayout layout = ScaleFit.DiagramOf(m_Stats, reference);
            if (m_DiagramBands == null) return;
            foreach (VisualElement band in m_DiagramBands)
            {
                band.EnableInClassList("diagram-band-crop", layout.Cropped);
                band.EnableInClassList("diagram-band-spare", !layout.Cropped);
                band.style.display = layout.HasSafeArea ? DisplayStyle.Flex : DisplayStyle.None;
            }
            m_DiagramInner.style.display = layout.HasSafeArea ? DisplayStyle.Flex : DisplayStyle.None;
            if (!layout.HasSafeArea) return;

            float width = DiagramSize.x;
            float height = DiagramSize.y;
            float innerWidth = width * layout.InnerWidthRatio;
            float innerHeight = height * layout.InnerHeightRatio;
            float left = (width - innerWidth) * 0.5f;
            float top = (height - innerHeight) * 0.5f;

            m_DiagramInner.style.left = left;
            m_DiagramInner.style.top = top;
            m_DiagramInner.style.width = innerWidth;
            m_DiagramInner.style.height = innerHeight;

            // 边带 = 参考相对安全区被裁掉的那一圈（单侧厚度 = (外 − 内) / 2；逐轴各自算）
            float bandX = left;
            float bandY = top;
            Place(m_DiagramBands[0], 0f, 0f, bandX, height);                       // 左
            Place(m_DiagramBands[1], width - bandX, 0f, bandX, height);            // 右
            Place(m_DiagramBands[2], 0f, 0f, width, bandY);                        // 上
            Place(m_DiagramBands[3], 0f, height - bandY, width, bandY);            // 下
        }

        private static void Place(VisualElement element, float x, float y, float width, float height)
        {
            element.style.left = x;
            element.style.top = y;
            element.style.width = width < 0f ? 0f : width;
            element.style.height = height < 0f ? 0f : height;
        }
    }
}
