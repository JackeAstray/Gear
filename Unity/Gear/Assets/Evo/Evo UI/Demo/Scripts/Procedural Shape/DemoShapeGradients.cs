using UnityEngine;
using TMPro;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Gradients tab: type, space, palette, angle, zoom and reverse on every shape, with a glow
    /// that repeats each shape's fill gradient, and the same gradient on the outline shapes.
    /// The options start from the first shape's gradient.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoShapeGradients : MonoBehaviour
    {
        [SerializeField] private ProceduralShape[] shapes;
        [Tooltip("Shapes whose outline carries the gradient instead of their fill.")]
        [SerializeField] private ProceduralShape[] outlines;
        [SerializeField] private Gradient[] palettes;
        [Tooltip("Alpha of the glow that repeats the fill gradient under each shape.")]
        [SerializeField, Range(0f, 1f)] private float glowAlpha = 0.55f;
        [Tooltip("Screen space spans the whole screen; this scales it down to the row of shapes.")]
        [SerializeField] private float screenZoom = 1f;
        [SerializeField] private TMP_Text info;
        [SerializeField, TextArea] private string localInfo;
        [SerializeField, TextArea] private string screenInfo;

        [Header("Options")]
        [SerializeField] private RadioButtonGroup type;
        [SerializeField] private RadioButtonGroup space;
        [SerializeField] private RadioButtonGroup colors;
        [SerializeField] private Slider angle;
        [SerializeField] private Slider zoom;
        [SerializeField] private Switch reverse;

        Gradient[] glowPalettes;
        float zoomValue;

        void Awake()
        {
            ProceduralShape first = shapes[0];
            bool screen = first.FillGradientSpace == ProceduralShape.GradientSpace.ScreenSpace;
            zoomValue = first.FillGradientZoom / (screen ? screenZoom : 1f);

            DemoUtils.Show(type, (int)first.FillGradientType);
            DemoUtils.Show(space, (int)first.FillGradientSpace);
            DemoUtils.Show(colors, PaletteOf(first.FillGradient));
            DemoUtils.Show(angle, first.FillGradientAngle);
            DemoUtils.Show(zoom, zoomValue);
            DemoUtils.Show(reverse, first.FillGradientReverse);

            ShowInfo();
        }

        int PaletteOf(Gradient gradient)
        {
            for (int i = 0; i < palettes.Length; i++)
            {
                GradientColorKey[] a = palettes[i].colorKeys, b = gradient.colorKeys;
                if (a.Length != b.Length)
                    continue;

                bool same = true;
                for (int k = 0; k < a.Length && same; k++)
                    same = Mathf.Abs(a[k].time - b[k].time) < 0.01f && ((Vector4)(a[k].color - b[k].color)).sqrMagnitude < 0.0001f;
                if (same) 
                    return i;
            }
            return -1;
        }

        void ShowInfo()
        {
            if (info != null)
                info.text = shapes[0].FillGradientSpace == ProceduralShape.GradientSpace.Local ? localInfo : screenInfo;
        }

        public void SetGradientType(int index)
        {
            foreach (ProceduralShape shape in shapes)
            {
                shape.FillGradientType = (ProceduralShape.GradientType)index;
                shape.OuterShadowGradientType = (ProceduralShape.GradientType)index;
            }

            foreach (ProceduralShape shape in outlines)
                shape.OutlineGradientType = (ProceduralShape.GradientType)index;
        }

        public void SetGradientSpace(int index)
        {
            foreach (ProceduralShape shape in shapes)
            {
                shape.FillGradientSpace = (ProceduralShape.GradientSpace)index;
                shape.OuterShadowGradientSpace = (ProceduralShape.GradientSpace)index;
            }

            foreach (ProceduralShape shape in outlines)
                shape.OutlineGradientSpace = (ProceduralShape.GradientSpace)index;

            ApplyZoom();
            ShowInfo();
        }

        public void SetPalette(int index)
        {
            if (index < 0 || index >= palettes.Length)
                return;

            // The glow is the same gradient at lower alpha, made once per palette
            glowPalettes ??= new Gradient[palettes.Length];
            glowPalettes[index] ??= DemoUtils.WithAlpha(palettes[index], glowAlpha);
            foreach (ProceduralShape shape in shapes)
            {
                shape.FillGradient = palettes[index];
                shape.OuterShadowGradient = glowPalettes[index];
            }

            foreach (ProceduralShape shape in outlines)
                shape.OutlineGradient = palettes[index];
        }

        public void SetGradientAngle(float value)
        {
            foreach (ProceduralShape shape in shapes)
            {
                shape.FillGradientAngle = Mathf.Repeat(value, 360f);
                shape.OuterShadowGradientAngle = Mathf.Repeat(value, 360f);
            }

            foreach (ProceduralShape shape in outlines)
                shape.OutlineGradientAngle = Mathf.Repeat(value, 360f);
        }

        public void SetGradientZoom(float value)
        {
            zoomValue = value;
            ApplyZoom();
        }

        void ApplyZoom()
        {
            foreach (ProceduralShape shape in shapes)
            {
                bool screen = shape.FillGradientSpace == ProceduralShape.GradientSpace.ScreenSpace;
                shape.FillGradientZoom = zoomValue * (screen ? screenZoom : 1f);
                shape.OuterShadowGradientZoom = Mathf.Clamp(shape.FillGradientZoom, 0.1f, 4f);
            }

            foreach (ProceduralShape shape in outlines)
            {
                bool screen = shape.OutlineGradientSpace == ProceduralShape.GradientSpace.ScreenSpace;
                shape.OutlineGradientZoom = zoomValue * (screen ? screenZoom : 1f);
            }
        }

        public void SetGradientReverse(bool value)
        {
            foreach (ProceduralShape shape in shapes)
            {
                shape.FillGradientReverse = value;
                shape.OuterShadowGradientReverse = value;
            }

            foreach (ProceduralShape shape in outlines)
                shape.OutlineGradientReverse = value;
        }
    }
}