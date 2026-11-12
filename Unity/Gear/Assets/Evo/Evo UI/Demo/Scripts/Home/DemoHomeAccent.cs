using System.Collections.Generic;
using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// While Home is open, the theme's Accent colors take the current screen's highlight. Each theme preset gets one runtime
    /// copy per screen (the preset assets are never changed), applied through Styler.ApplyPreset, so the change uses the same
    /// color transition as the theme switch and only the colors that differ animate. Other pages get the plain preset back.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoHomeAccent : MonoBehaviour
    {
        [Tooltip("Turn the Accent colors into the current screen's highlight while Home is open.")]
        [SerializeField] private bool followScreen = true;
        [Tooltip("Transition when the deck swaps screens. The background's color uses it too.")]
        [SerializeField, Min(0f)] private float cardDuration = 0.4f;
        [Tooltip("Transition when another page opens and the default accent comes back, and when Home opens again.")]
        [SerializeField, Min(0f)] private float pageDuration = 0.1f;
        [Tooltip("Preset colors that take the highlight. Their alpha stays.")]
        [SerializeField] private string[] colorIDs = { "Accent", "Accent (Tint)", "Accent (Soft)" };

        readonly Dictionary<StylerPreset, StylerPreset[]> copies = new();
        StylerPreset applied, lastTheme;
        int lastScreen;
        Color lastHighlight;
        bool lastHome;

        public float CardDuration => cardDuration;
        public float PageDuration => pageDuration;

        /// <summary>
        /// Applies the theme preset, or its copy for this screen when the highlight should show.
        /// </summary>
        public void Apply(StylerPreset theme, int screen, Color highlight, bool home, float duration)
        {
            lastTheme = theme;
            lastScreen = screen;
            lastHighlight = highlight;
            lastHome = home;

            StylerPreset preset = followScreen && home && enabled ? GetCopy(theme, screen, highlight) : theme;
            if (preset == applied)
                return;

            applied = preset;
            Styler.ApplyPreset(preset, true, duration);
        }

        void Reapply()
        {
            if (lastTheme == null)
                return;

            applied = null;
            Apply(lastTheme, lastScreen, lastHighlight, lastHome, pageDuration);
        }

        StylerPreset GetCopy(StylerPreset theme, int screen, Color highlight)
        {
            // A highlight that already is the theme's accent needs no copy
            if (theme.TryGetColor(colorIDs.Length > 0 ? colorIDs[0] : "Accent", out Color accent) && SameColor(accent, highlight))
                return theme;

            if (!copies.TryGetValue(theme, out StylerPreset[] list) || list.Length <= screen)
            {
                var grown = new StylerPreset[screen + 1];
                list?.CopyTo(grown, 0);
                copies[theme] = list = grown;
            }

            StylerPreset copy = list[screen];
            if (copy == null)
            {
                copy = Instantiate(theme);
                copy.name = theme.name + " (Accent " + screen + ")";
                copy.hideFlags = HideFlags.DontSave;
                list[screen] = copy;
            }

            // Written on every use, so highlights edited in Play mode show on the next swap
            for (int i = 0; i < colorIDs.Length; i++)
            {
                if (theme.TryGetColor(colorIDs[i], out Color color))
                    copy.SetColor(colorIDs[i], new Color(highlight.r, highlight.g, highlight.b, color.a));
            }

            return copy;
        }

        static bool SameColor(Color a, Color b) => Mathf.Abs(a.r - b.r) < 0.002f && Mathf.Abs(a.g - b.g) < 0.002f && Mathf.Abs(a.b - b.b) < 0.002f;

        void OnDestroy()
        {
            foreach (StylerPreset[] list in copies.Values)
            {
                foreach (StylerPreset copy in list)
                {
                    if (copy != null)
                        Destroy(copy);
                }
            }

            copies.Clear();
        }

#if UNITY_EDITOR
        // Inspector edits in Play mode show at once
        void OnValidate()
        {
            if (Application.isPlaying && isActiveAndEnabled)
                Reapply();
        }
#endif
    }
}
