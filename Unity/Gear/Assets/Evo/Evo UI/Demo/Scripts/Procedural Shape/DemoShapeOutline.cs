using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Outline tab: width, solid or gradient color, fill center and dashes. The dash options lock while the outline isn't dashed.
    /// The options start from the shape's own values.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoShapeOutline : MonoBehaviour
    {
        [SerializeField] private ProceduralShape shape;

        [Header("Options")]
        [SerializeField] private Slider width;
        [SerializeField] private RadioButtonGroup color;
        [SerializeField] private Switch fillCenter;
        [SerializeField] private Switch dashed;
        [SerializeField] private Slider dashLength;
        [SerializeField] private Slider dashGap;
        [SerializeField] private Switch roundedEnds;
        [Tooltip("Locked while the outline isn't dashed.")]
        [SerializeField] private CanvasGroup dashOptions;

        void Awake()
        {
            DemoUtils.Show(width, shape.OutlineWidth);
            DemoUtils.Show(color, shape.outlineColorMode == ProceduralShape.ColorMode.Gradient ? 1 : 0);
            DemoUtils.Show(fillCenter, shape.FillCenter);
            DemoUtils.Show(dashed, shape.OutlineDashed);
            DemoUtils.Show(dashLength, shape.OutlineDashLength);
            DemoUtils.Show(dashGap, shape.OutlineDashGap);
            DemoUtils.Show(roundedEnds, shape.OutlineDashRounded);
            DemoUtils.SetLocked(dashOptions, !shape.OutlineDashed, false);
        }

        public void SetOutlineColor(int index)
        {
            // 0: Styler color, 1: gradient. The mode is a plain field, so the shape needs a refresh to show it
            shape.outlineColorMode = index == 0 ? ProceduralShape.ColorMode.Styler : ProceduralShape.ColorMode.Gradient;
            shape.UpdateRect();
        }

        public void SetDashed(bool value)
        {
            shape.OutlineDashed = value;
            DemoUtils.SetLocked(dashOptions, !value);
        }
    }
}