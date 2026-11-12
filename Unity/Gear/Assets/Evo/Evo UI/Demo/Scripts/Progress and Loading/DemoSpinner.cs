using Evo.Tween;
using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Spinner tabs: style, motion and shape options for every spinner on the page. Spinners read their settings every frame,
    /// so they apply while they spin.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoSpinner : MonoBehaviour
    {
        [SerializeField] private Spinner[] spinners;

        static readonly Ease[] Eases = { Ease.InOutSine, Ease.InOutCubic, Ease.InOutQuint, Ease.InOutExpo };

        // Smooth or Simple
        public void SetStyle(int index)
        {
            foreach (Spinner spinner in spinners)
                spinner.SetSpinnerStyle(index == 0 ? Spinner.SpinnerStyle.Radial : Spinner.SpinnerStyle.RadialSimple);
        }

        public void SetSpeed(float value)
        {
            foreach (Spinner spinner in spinners)
                spinner.rotationSpeed = value;
        }

        // Sine, Cubic, Quint or Expo
        public void SetEase(int index)
        {
            foreach (Spinner spinner in spinners)
                spinner.ease = Eases[index];
        }

        // The ring's outline width, or the line's height
        public void SetThickness(float value)
        {
            foreach (Spinner spinner in spinners)
            {
                ProceduralShape shape = spinner.spinnerShape;
                if (shape.ClippingMethod == ProceduralShape.ClipMethod.Radial360)
                {
                    shape.OutlineWidth = value;
                    continue;
                }

                RectTransform line = shape.rectTransform;
                line.sizeDelta = new Vector2(line.sizeDelta.x, value);
            }
        }
    }
}
