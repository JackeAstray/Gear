using System;
using System.Text;
using TMPro;
using UnityEngine;
using Evo.Tween;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Input Field (Icon) tab: a friends list filtered by the search field. Rows that don't match fold away,
    /// the matching letters take the accent color, and a short row says so when nobody is left.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoFriendList : MonoBehaviour
    {
        [Serializable]
        class Row
        {
            public RectTransform rect;
            public CanvasGroup group;
            public TMP_Text name;
        }

        [SerializeField] private TMP_InputField search;
        [SerializeField] private Row[] rows;
        [Tooltip("A name's Styler Object. Its preset's Accent colors the matched letters.")]
        [SerializeField] private StylerObject nameStyler;
        [SerializeField] private float rowHeight = 72;

        [Header("No Matches")]
        [SerializeField] private CanvasGroup empty;
        [SerializeField] private TMP_Text emptyText;

        const float FoldDuration = 0.25f;
        const float EmptyDuration = 0.2f;
        const int FoldKey = 1;

        string[] names;
        float[] shown;
        float[] from;
        float[] to;
        TweenHandle foldTween;
        bool emptyShown;
        readonly StringBuilder text = new();
        string accentTag;
        Color accentColor;

        void Awake()
        {
            names = new string[rows.Length];
            shown = new float[rows.Length];
            from = new float[rows.Length];
            to = new float[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                names[i] = rows[i].name.text;
                shown[i] = from[i] = to[i] = 1;
            }

            search.onValueChanged.AddListener(Filter);
            empty.alpha = 0;
            Fold(1);
        }

        void Filter(string value)
        {
            string query = value.Trim();
            bool any = false;
            bool accentRead = false;

            for (int i = 0; i < rows.Length; i++)
            {
                int hit = query.Length == 0 ? -1 : names[i].IndexOf(query, StringComparison.OrdinalIgnoreCase);
                bool match = query.Length == 0 || hit >= 0;
                any |= match;
                from[i] = shown[i];
                to[i] = match ? 1 : 0;

                if (hit < 0) { rows[i].name.text = names[i]; }
                else
                {
                    if (!accentRead)
                    {
                        accentRead = true;
                        UpdateAccent(rows[i].name);
                    }

                    // Built in place, so typing doesn't make new strings
                    string name = names[i];
                    int end = hit + query.Length;
                    text.Clear().Append(name, 0, hit).Append(accentTag).Append(name, hit, query.Length).Append("</color>").Append(name, end, name.Length - end);
                    rows[i].name.SetText(text);
                }
            }

            foldTween.Kill();
            foldTween = EvoTween.Create(0f, 1f, FoldDuration)
                .WithEase(Ease.OutCubic)
                .WithTimeMode(TimeMode.Unscaled)
                .Bind(this, static (list, progress) => list.Fold(progress), FoldKey);

            if (!any)
            {
                text.Clear().Append("No friends named “").Append(query).Append('”');
                emptyText.SetText(text);
            }

            if (any == !emptyShown)
                return;

            emptyShown = !any;
            DemoUtils.Fade(empty, emptyShown ? 1f : 0f, EmptyDuration);
        }

        // Rows stack from the top; a folding row gives its height to the ones below and fades out faster than it folds
        void Fold(float progress)
        {
            float y = 0;
            for (int i = 0; i < rows.Length; i++)
            {
                shown[i] = Mathf.Lerp(from[i], to[i], progress);
                rows[i].rect.anchoredPosition = new Vector2(rows[i].rect.anchoredPosition.x, -y);
                rows[i].group.alpha = shown[i] * shown[i];
                rows[i].group.blocksRaycasts = to[i] > 0;
                y += shown[i] * rowHeight;
            }
        }

        // Matched letters use the preset's Accent, so the highlight follows the theme. The tag is rebuilt only when the color changes
        void UpdateAccent(TMP_Text label)
        {
            StylerPreset preset = nameStyler != null ? nameStyler.Preset : null;
            Color color = preset != null && preset.TryGetColor("Accent", out Color accent) ? accent : label.color;
            if (accentTag != null && color.Equals(accentColor))
                return;

            accentColor = color;
            accentTag = "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">";
        }
    }
}
