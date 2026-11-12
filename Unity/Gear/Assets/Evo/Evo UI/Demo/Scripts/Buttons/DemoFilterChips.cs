using System;
using UnityEngine;
using Evo.Tween;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Button (Pill) tab: filter chips over a media grid, Regular or Outline by the Type option.
    /// The picked chip stays in its Selected state; matching tiles glide into the first cells and the rest fade out in place.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoFilterChips : DemoButtonPage
    {
        [Serializable]
        class Tile
        {
            public RectTransform rect;
            public CanvasGroup group;
            [Tooltip("The chip that shows this tile. The first chip (All) shows every tile.")]
            public int chip;
        }

        [SerializeField] private Tile[] tiles;

        [Header("Grid")]
        [Tooltip("Anchored position of the first cell; tiles are top-left anchored.")]
        [SerializeField] private Vector2 origin = new(32f, -115f);
        [SerializeField] private Vector2 cellStep = new(176f, -136f);
        [SerializeField, Min(1)] private int columns = 4;
        [SerializeField, Min(0f)] private float duration = 0.35f;

        const float HiddenScale = 0.9f;
        const int LayoutKey = 1;

        Vector2[] fromPosition, toPosition;
        float[] fromAlpha, toAlpha;
        TweenHandle tween;
        int filter = -1;

        protected override void Awake()
        {
            base.Awake();

            fromPosition = new Vector2[tiles.Length];
            toPosition = new Vector2[tiles.Length];
            fromAlpha = new float[tiles.Length];
            toAlpha = new float[tiles.Length];
            Pick(0, false);
        }

        public void Pick(int index) => Pick(index, true);

        void Pick(int index, bool animate)
        {
            for (int i = 0; i < SlotCount; i++)
                Select(i, i == index);

            if (index == filter)
                return;

            filter = index;
            int cell = 0;

            for (int i = 0; i < tiles.Length; i++)
            {
                bool show = index == 0 || tiles[i].chip == index;
                fromPosition[i] = tiles[i].rect.anchoredPosition;
                fromAlpha[i] = tiles[i].group.alpha;
                toAlpha[i] = show ? 1f : 0f;
                toPosition[i] = show ? Cell(cell++) : fromPosition[i];
            }

            tween.Kill();
            if (!animate || duration <= 0f) { Apply(1f); }
            else
            {
                tween = EvoTween.Create(0f, 1f, duration)
                    .WithEase(Ease.OutCubic)
                    .WithTimeMode(TimeMode.Unscaled)
                    .Bind(this, static (demo, t) => demo.Apply(t), LayoutKey);
            }
        }

        Vector2 Cell(int index) => origin + new Vector2(index % columns * cellStep.x, index / columns * cellStep.y);

        void Apply(float t)
        {
            for (int i = 0; i < tiles.Length; i++)
            {
                float alpha = Mathf.Lerp(fromAlpha[i], toAlpha[i], t);
                tiles[i].rect.anchoredPosition = Vector2.LerpUnclamped(fromPosition[i], toPosition[i], t);
                tiles[i].rect.localScale = Vector3.one * Mathf.Lerp(HiddenScale, 1f, alpha);
                tiles[i].group.alpha = alpha;
            }
        }
    }
}
