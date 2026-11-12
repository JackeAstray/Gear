using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Evo.Tween;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Dropdown (Multi Select) tab: a game library filtered by genre. Matching games glide together,
    /// the rest fade out, and the count follows. With no genre picked, every game shows.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoGameLibrary : MonoBehaviour
    {
        [Serializable]
        class Game
        {
            public RectTransform rect;
            public CanvasGroup group;
            [Tooltip("The genre's place in the list as authored.")]
            public int genre;
        }

        [SerializeField] private DropdownMultiSelect dropdown;
        [SerializeField] private Game[] games;
        [SerializeField] private TMP_Text count;
        [SerializeField] private Vector2 cell = new(134, 126);
        [SerializeField] private int columns = 4;
        [SerializeField] private float glideDuration = 0.45f;
        [SerializeField] private float fadeDuration = 0.25f;
        [SerializeField] private float hiddenScale = 0.9f;

        readonly Dictionary<Dropdown.Item, int> genres = new();
        bool[] picked;
        TweenHandle[] glides;
        TweenHandle[] fades;
        TweenHandle[] scales;
        bool settle;

        void Awake()
        {
            for (int i = 0; i < dropdown.items.Count; i++)
                genres[dropdown.items[i]] = i;

            picked = new bool[dropdown.items.Count];
            glides = new TweenHandle[games.Length];
            fades = new TweenHandle[games.Length];
            scales = new TweenHandle[games.Length];
            dropdown.onSelectionChanged.AddListener(OnSelectionChanged);
        }

        // Multi Select applies its default picks in its own Start, so the library settles on the first frame
        void OnEnable() => settle = true;

        void LateUpdate()
        {
            if (!settle)
                return;

            settle = false;
            Filter(false);
        }

        void OnDisable()
        {
            for (int i = 0; i < games.Length; i++)
            {
                glides[i].Kill();
                fades[i].Kill();
                scales[i].Kill();
            }
        }

        void OnSelectionChanged(List<int> selection) => Filter(true);

        void Filter(bool animate)
        {
            // Items are matched by reference, so Sort A–Z doesn't change which genre a pick means
            Array.Clear(picked, 0, picked.Length);
            bool any = false;
            for (int i = 0; i < dropdown.items.Count; i++)
            {
                Dropdown.Item item = dropdown.items[i];
                if (item != null && dropdown.IsItemSelected(i) && genres.TryGetValue(item, out int genre))
                    picked[genre] = any = true;
            }

            int slot = 0;
            for (int i = 0; i < games.Length; i++)
            {
                Game game = games[i];
                bool shown = !any || picked[game.genre];
                if (shown)
                {
                    var target = new Vector2(slot % columns * cell.x, -(slot / columns) * cell.y);
                    slot++;

                    // A game coming back appears in its new place instead of gliding from where it faded out
                    bool glide = animate && game.group.alpha > 0.01f;
                    glides[i].Kill();
                    if (glide)
                    {
                        glides[i] = EvoTween.Create(game.rect.anchoredPosition, target, glideDuration)
                            .WithEase(Ease.OutCubic)
                            .WithTimeMode(TimeMode.Unscaled)
                            .BindToAnchoredPosition(game.rect);
                    }
                    else
                    {
                        game.rect.anchoredPosition = target;
                    }
                }

                Fade(i, shown ? 1f : 0f, animate);
            }

            count.SetText(slot == 1 ? "{0} game" : "{0} games", slot);
        }

        void Fade(int i, float to, bool animate)
        {
            Game game = games[i];
            game.group.blocksRaycasts = to > 0f;
            fades[i].Kill();
            scales[i].Kill();
            if (!animate)
            {
                game.group.alpha = to;
                game.rect.localScale = Vector3.one * (to > 0f ? 1f : hiddenScale);
                return;
            }

            fades[i] = DemoUtils.Fade(game.group, to, fadeDuration);
            scales[i] = EvoTween.Create(game.rect.localScale, Vector3.one * (to > 0f ? 1f : hiddenScale), fadeDuration)
                .WithEase(Ease.OutCubic)
                .WithTimeMode(TimeMode.Unscaled)
                .BindToScale(game.rect);
        }
    }
}
