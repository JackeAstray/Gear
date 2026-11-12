using System;
using UnityEngine;
using Evo.Tween;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Radio Button (Alt) tab: the group picks how the items are laid out (list, grid or carousel)
    /// and the items glide into the new layout. A cleared pick keeps the current layout.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoViewSwitcher : MonoBehaviour
    {
        [Serializable]
        class Item
        {
            [Tooltip("Top-left anchored, like its thumb, title and subtitle.")]
            public RectTransform rect;
            public RectTransform thumb;
            public RectTransform title;
            public RectTransform subtitle;
            public RectTransform icon;
            public ProceduralShape thumbShape;
            [Tooltip("Optional: fades the item's neighbors in the carousel.")]
            public CanvasGroup group;
        }

        [SerializeField] private RadioButtonGroup group;
        [SerializeField] private Item[] items;
        [SerializeField, Min(0f)] private float duration = 0.4f;

        struct Pose
        {
            public Rect item, thumb, title, subtitle;
            public float radius, iconScale, alpha;
        }

        // Grid: two columns; carousel: one item in the middle and its neighbors peeking at both edges
        const float ListGap = 20f, GridGap = 16f, CarouselWidth = 360f, CarouselGap = 24f, NeighborAlpha = 0.45f;
        const float TitleHeight = 14f, SubtitleHeight = 11f, TextGap = 8f, GridTextTop = 12f, CarouselTextTop = 18f;
        const int LayoutKey = 1;

        Pose[] from, to, current;
        TweenHandle tween;
        int view = -1;

        void Awake()
        {
            from = new Pose[items.Length];
            to = new Pose[items.Length];
            current = new Pose[items.Length];

            group.onSelectionChanged.AddListener(SetView);
            SetView(Mathf.Max(0, group.selectedIndex), false);
        }

        public void SetView(int index) => SetView(index, true);

        void SetView(int index, bool animate)
        {
            if (index < 0 || index == view)
                return;

            view = index;

            for (int i = 0; i < items.Length; i++)
            {
                from[i] = current[i];
                to[i] = PoseFor(index, i);
            }

            tween.Kill();
            if (!animate || duration <= 0f || !Application.isPlaying) { Apply(1f); }
            else
            {
                tween = EvoTween.Create(0f, 1f, duration)
                    .WithEase(Ease.OutCubic)
                    .WithTimeMode(TimeMode.Unscaled)
                    .Bind(this, static (demo, t) => demo.Apply(t), LayoutKey);
            }
        }

        Pose PoseFor(int layout, int index)
        {
            Item item = items[index];
            Rect area = ((RectTransform)item.rect.parent).rect;
            float titleWidth = item.title.sizeDelta.x;
            float subtitleWidth = item.subtitle.sizeDelta.x;
            var pose = new Pose { alpha = 1f };

            if (layout == 0)
            {
                float height = (area.height - ListGap * (items.Length - 1)) / items.Length;
                float textX = height + ListGap;
                float textY = (height - TitleHeight - TextGap - SubtitleHeight) * 0.5f;
                pose.item = new Rect(0f, index * (height + ListGap), area.width, height);
                pose.thumb = new Rect(0f, 0f, height, height);
                pose.title = new Rect(textX, textY, titleWidth, TitleHeight);
                pose.subtitle = new Rect(textX, textY + TitleHeight + TextGap, subtitleWidth, SubtitleHeight);
                pose.radius = 14f;
                pose.iconScale = 1f;
            }
            else if (layout == 1)
            {
                int rows = (items.Length + 1) / 2;
                float width = (area.width - GridGap) * 0.5f;
                float height = (area.height - GridGap * (rows - 1)) / rows;
                float thumbHeight = height - GridTextTop - TitleHeight - TextGap - SubtitleHeight;
                pose.item = new Rect(index % 2 * (width + GridGap), index / 2 * (height + GridGap), width, height);
                pose.thumb = new Rect(0f, 0f, width, thumbHeight);
                pose.title = new Rect(0f, thumbHeight + GridTextTop, titleWidth, TitleHeight);
                pose.subtitle = new Rect(0f, thumbHeight + GridTextTop + TitleHeight + TextGap, subtitleWidth, SubtitleHeight);
                pose.radius = 16f;
                pose.iconScale = 1.3f;
            }
            else
            {
                // Item 0 in the middle, 1 to its right, the last one to its left, the rest out of view
                int slot = index <= items.Length / 2 ? index : index - items.Length;
                float thumbHeight = area.height - CarouselTextTop - TitleHeight - TextGap - SubtitleHeight;
                pose.item = new Rect((area.width - CarouselWidth) * 0.5f + slot * (CarouselWidth + CarouselGap), 0f, CarouselWidth, area.height);
                pose.thumb = new Rect(0f, 0f, CarouselWidth, thumbHeight);
                pose.title = new Rect(0f, thumbHeight + CarouselTextTop, titleWidth, TitleHeight);
                pose.subtitle = new Rect(0f, thumbHeight + CarouselTextTop + TitleHeight + TextGap, subtitleWidth, SubtitleHeight);
                pose.radius = 20f;
                pose.iconScale = 2f;
                pose.alpha = slot == 0 ? 1f : Mathf.Abs(slot) == 1 ? NeighborAlpha : 0f;
            }

            return pose;
        }

        void Apply(float t)
        {
            for (int i = 0; i < items.Length; i++)
            {
                Pose a = from[i], b = to[i];
                Pose pose = new()
                {
                    item = Lerp(a.item, b.item, t),
                    thumb = Lerp(a.thumb, b.thumb, t),
                    title = Lerp(a.title, b.title, t),
                    subtitle = Lerp(a.subtitle, b.subtitle, t),
                    radius = Mathf.LerpUnclamped(a.radius, b.radius, t),
                    iconScale = Mathf.LerpUnclamped(a.iconScale, b.iconScale, t),
                    alpha = Mathf.LerpUnclamped(a.alpha, b.alpha, t)
                };

                current[i] = pose;
                Item item = items[i];
                Place(item.rect, pose.item);
                Place(item.thumb, pose.thumb);
                Place(item.title, pose.title);
                Place(item.subtitle, pose.subtitle);
                item.thumbShape.UniformCornerRadius = pose.radius;
                item.icon.localScale = new Vector3(pose.iconScale, pose.iconScale, 1f);
                if (item.group != null)
                    item.group.alpha = pose.alpha;
            }
        }

        static Rect Lerp(Rect a, Rect b, float t) => new(
            Mathf.LerpUnclamped(a.x, b.x, t), Mathf.LerpUnclamped(a.y, b.y, t),
            Mathf.LerpUnclamped(a.width, b.width, t), Mathf.LerpUnclamped(a.height, b.height, t));

        // Rects are top-left based, with y growing downward
        static void Place(RectTransform target, Rect rect)
        {
            target.anchoredPosition = new Vector2(rect.x, -rect.y);
            target.sizeDelta = rect.size;
        }
    }
}
