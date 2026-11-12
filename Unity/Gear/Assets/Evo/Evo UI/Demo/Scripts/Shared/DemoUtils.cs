using TMPro;
using UnityEngine;
using Evo.Tween;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Small helpers shared by the demo pages.
    /// </summary>
    public static class DemoUtils
    {
        const float LockedAlpha = 0.25f;
        const float LockFadeDuration = 0.2f;
        const int RendererAlphaKey = 0x44524641; // DRFA

        // The pointer effects (Border Glow, Ripple, Spotlight) light in one of these Styler colors
        static readonly string[] EffectColors = { "Primary", "Accent" };

        // Show: puts a value on an option control without firing its event,
        // so a page can start its options from what its shapes already use
        public static void Show(Slider slider, float value)
        {
            if (slider != null)
                slider.SetValueWithoutNotify(value);
        }

        public static void Show(Switch toggle, bool value)
        {
            if (toggle != null)
                toggle.SetValue(value, false);
        }

        public static void Show(RadioButtonGroup group, int index)
        {
            if (group == null || index < 0 || group.selectedIndex == index)
                return;

            // Before its Awake the group has no buttons yet, and selects selectedIndex itself without events
            if (group.GetButton(index) != null) { group.SetButton(index, false, false); }
            else { group.selectedIndex = index; }
        }

        // Locks an option row or section that doesn't apply right now: it stops taking the pointer at once and fades to dim.
        // Pages pass animate false for the state they start in, so nothing fades when a tab opens.
        public static void SetLocked(CanvasGroup group, bool locked, bool animate = true)
        {
            // Already there, or fading there
            if (group == null || (animate && group.interactable != locked))
                return;

            group.interactable = !locked;
            group.blocksRaycasts = !locked;

            float alpha = locked ? LockedAlpha : 1f;
            if (!animate)
                group.alpha = alpha;

            // A zero-length fade still replaces one that is running, so it can't undo an instant change
            if (Application.isPlaying)
                Fade(group, alpha, animate ? LockFadeDuration : 0f);
        }

        public static void SetLocked(CanvasGroup[] groups, bool locked, bool animate = true)
        {
            foreach (CanvasGroup group in groups)
                SetLocked(group, locked, animate);
        }

        // The demo's usual fade: OutCubic on unscaled time, from the group's current alpha
        public static TweenHandle Fade(CanvasGroup group, float alpha, float duration, float delay = 0f)
        {
            return EvoTween.Create(group.alpha, alpha, duration)
                .WithDelay(delay)
                .WithEase(Ease.OutCubic)
                .WithTimeMode(TimeMode.Unscaled)
                .BindToAlpha(group);
        }

        // Graphic.CrossFadeAlpha without its garbage: the renderer's alpha moves linearly on unscaled time, leaving Graphic.color
        // to the Styler. A new fade replaces the running one; an instant one, or one on a hidden graphic, lands at once.
        // It steps in LateUpdate, after the Update that started it, the way uGUI's fade coroutine did.
        public static void FadeRenderer(UnityEngine.UI.Graphic graphic, float alpha, float duration, ref TweenHandle fade)
        {
            fade.Kill();
            CanvasRenderer renderer = graphic.canvasRenderer;
            float current = renderer.GetAlpha();

            if (duration <= 0 || !graphic.gameObject.activeInHierarchy) { renderer.SetAlpha(alpha); }
            else if (current != alpha)
            {
                fade = EvoTween.Create(current, alpha, duration)
                    .WithTimeMode(TimeMode.Unscaled)
                    .WithUpdatePhase(UpdatePhase.LateUpdate)
                    .Bind(renderer, static (target, value) => target.SetAlpha(value), RendererAlphaKey);
            }
        }

        // Turns a pointer effect on or off; off also clears what it was showing
        public static void SetEffect(InteractiveEffectSettings effect, bool value)
        {
            effect.enabled = value;
            if (!value)
                effect.Clear();
        }

        // Capacity dots: the first count dots light up, the rest stay empty
        public static void SetDots(DemoDotRow row, int count, string filled = "Primary (Dim)", string empty = "Primary (Soft)")
        {
            for (int i = 0; i < row.dots.Length; i++)
            {
                ProceduralShape dot = row.dots[i];
                string id = i < count ? filled : empty;
                if (dot.fillStyler.color.stylerID == id)
                    continue;

                dot.fillStyler.color.stylerID = id;
                dot.UpdateStyler();
            }
        }

        // Mathf.Min(a, b, c) without its params array; same comparisons in the same order, so NaN cases match too
        public static float Min(float a, float b, float c)
        {
            float min = a;
            if (b < min)
                min = b;
            if (c < min)
                min = c;

            return min;
        }

        static readonly char[] clockBuffer = new char[64];

        // Writes the prefix and a m:ss time to the text without allocating
        public static void SetClock(TMP_Text text, string prefix, int seconds)
        {
            int length = Mathf.Min(prefix.Length, clockBuffer.Length - 16);
            prefix.CopyTo(0, clockBuffer, 0, length);
            (seconds / 60).TryFormat(new System.Span<char>(clockBuffer, length, clockBuffer.Length - length), out int written);
            length += written;
            clockBuffer[length++] = ':';
            clockBuffer[length++] = (char)('0' + seconds % 60 / 10);
            clockBuffer[length++] = (char)('0' + seconds % 10);
            text.SetCharArray(clockBuffer, 0, length);
        }

        // A copy of the gradient with every alpha key scaled
        public static Gradient WithAlpha(Gradient source, float alpha)
        {
            GradientAlphaKey[] alphas = source.alphaKeys;
            for (int i = 0; i < alphas.Length; i++)
                alphas[i].alpha *= alpha;

            var gradient = new Gradient { mode = source.mode };
            gradient.SetKeys(source.colorKeys, alphas);
            return gradient;
        }

        // Pointer effects keep their color as a Styler mapping: the Color option picks its ID,
        // and Opacity (a percent bar) is its alpha override
        public static int GetEffectColor(InteractiveEffectSettings settings)
        {
            return Mathf.Max(0, System.Array.IndexOf(EffectColors, settings.stylerColor.stylerID));
        }

        public static void SetEffectColor(InteractiveEffect effect, InteractiveEffectSettings settings, int index)
        {
            settings.stylerColor.stylerID = EffectColors[index];
            effect.UpdateStyler();
        }

        public static void SetEffectOpacity(InteractiveEffect effect, InteractiveEffectSettings settings, float percent)
        {
            settings.stylerColor.alphaOverride = percent / 100f;
            effect.UpdateStyler();
        }
    }

    /// <summary>
    /// A row of capacity dots, in order. DemoUtils.SetDots lights the first ones.
    /// </summary>
    [System.Serializable]
    public class DemoDotRow
    {
        public ProceduralShape[] dots;
    }
}