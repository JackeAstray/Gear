using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Warning Stripe tab: an alert card framed by two stripe bands that share one stripe setup. The bands are dark in both themes,
    /// so the colors are custom, and the warning badge follows the stripe color. The options start from the first band.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoWarningStripe : MonoBehaviour
    {
        [SerializeField] private WarningStripe[] stripes;
        [SerializeField] private Graphic[] labels;
        [SerializeField] private Color[] colors = { Color.white, new Color32(242, 193, 78, 255), new Color32(218, 112, 120, 255), new Color32(100, 180, 255, 255) };

        [Header("Options")]
        [SerializeField] private RadioButtonGroup color;
        [SerializeField] private Slider width;
        [SerializeField] private Slider spacing;
        [SerializeField] private Slider angle;
        [SerializeField] private Slider speed;

        void Awake()
        {
            WarningStripe first = stripes[0];
            DemoUtils.Show(color, System.Array.IndexOf(colors, first.color));
            DemoUtils.Show(width, first.LineWidth * 100f);
            DemoUtils.Show(spacing, first.LineSpacing * 100f);
            DemoUtils.Show(angle, first.LineAngle);
            DemoUtils.Show(speed, first.ScrollSpeed);
        }

        public void SetColor(int index)
        {
            foreach (WarningStripe stripe in stripes)
                stripe.color = colors[index];

            // Each label keeps its own alpha, so a tinted badge stays a tint
            foreach (Graphic label in labels)
            {
                Color color = colors[index];
                color.a = label.color.a;
                label.color = color;
            }
        }

        // Width and spacing bars are in canvas units; the stripe stores them divided by 100
        public void SetWidth(float value)
        {
            foreach (WarningStripe stripe in stripes)
                stripe.LineWidth = value / 100f;
        }

        public void SetSpacing(float value)
        {
            foreach (WarningStripe stripe in stripes)
                stripe.LineSpacing = value / 100f;
        }

        public void SetAngle(float value)
        {
            foreach (WarningStripe stripe in stripes)
                stripe.LineAngle = value;
        }

        public void SetSpeed(float value)
        {
            foreach (WarningStripe stripe in stripes)
                stripe.ScrollSpeed = value;
        }
    }
}