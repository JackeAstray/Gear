using UnityEngine;
using Evo.Tween;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Soft glow behind a color picker. Each new color runs an Evo Tween from the color on screen,
    /// so a picker dragged across colors keeps easing toward the latest one.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoColorHalo : MonoBehaviour
    {
        [SerializeField] private ProceduralShape shape;
        [SerializeField, Min(0f)] private float duration = 0.4f;

        const int ColorKey = 1;

        // Below zero until Awake reads it
        float glowAlpha = -1f;
        bool shown;

        // The outer shadow's alpha on the shape is the glow strength; colors only change its hue
        void Awake() => glowAlpha = shape.OuterShadowColor.a;

        public void Show(Color color) => SetTarget(new Color(color.r, color.g, color.b, glowAlpha * color.a));

        // Fades out in the current hue
        public void Hide() => SetTarget(shape.OuterShadowColor * new Color(1f, 1f, 1f, 0f));

        void SetTarget(Color color)
        {
            // The editor's sliders can report a color before Awake; the picker or palette sends it again when it starts
            if (glowAlpha < 0f)
                return;

            // The first color arrives when the tab opens: no fade-in from the authored color.
            // A glow that is fully out takes the new hue at once and only fades in.
            Color current = shape.OuterShadowColor;
            if (!shown || current.a <= 0f)
            {
                current = shown ? new Color(color.r, color.g, color.b, 0f) : color;
                shown = true;
                shape.OuterShadowColor = current;
            }

            // A new tween on the same key replaces the running one
            EvoTween.Create(current, color, current == color ? 0f : duration)
                .WithEase(Ease.OutCubic)
                .WithTimeMode(TimeMode.Unscaled)
                .Bind(shape, static (target, value) => target.OuterShadowColor = value, ColorKey);
        }
    }
}
