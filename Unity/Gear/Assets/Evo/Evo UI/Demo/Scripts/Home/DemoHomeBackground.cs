using System;
using UnityEngine;
using Evo.Tween;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Home's background: a Topographic Skin in the current screen's color that is brighter around the deck (its falloff
    /// sits where the top card rests), under an ambient glow there too. The light stays put while the cards move.
    /// A swap nudges the map's seed so the ground re-forms, opening a screen zooms the map in, and other pages move the
    /// falloff onto the page in the preset's accent. Those changes run as Evo Tweens.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoHomeBackground : MonoBehaviour
    {
        [Serializable]
        public struct ThemeValue
        {
            [Range(0f, 1f)] public float light;
            [Range(0f, 1f)] public float dark;

            public ThemeValue(float light, float dark)
            {
                this.light = light;
                this.dark = dark;
            }

            public readonly float this[int theme] => theme == 0 ? light : dark;
        }

        [SerializeField] private TopographicSkin skin;
        [SerializeField] private ProceduralShape glow;
        [Tooltip("Outside Play mode, edits here show around this Home's start card.")]
        [SerializeField] private DemoHome home;

        [Header("Lines")]
        [Tooltip("Line opacity around the card.")]
        [SerializeField] private ThemeValue lineAlpha = new(0.22f, 0.17f);
        [Tooltip("Line opacity away from the card, as a share of Line Alpha.")]
        [SerializeField] private ThemeValue outsideOpacity = new(0.095f, 0.082f);

        [Header("Ambient")]
        [SerializeField] private ThemeValue glowAlpha = new(0.1f, 0.055f);
        [Tooltip("Glow size past the card's edges.")]
        [SerializeField, Min(0f)] private Vector2 glowPadding = new(560, 380);

        [Header("Motion")]
        [Tooltip("Seed change per swap; the ground slides and re-forms.")]
        [SerializeField, Range(0f, 0.2f)] private float swapShift = 0.022f;
        [SerializeField, Min(0f)] private float shiftDuration = 1.4f;
        [Tooltip("Map scale while a screen is open, relative to the skin's own.")]
        [SerializeField, Range(1f, 3f)] private float openZoom = 1.3f;
        [SerializeField, Min(0f)] private float zoomInDuration = 0.8f;
        [SerializeField, Min(0f)] private float zoomOutDuration = 0.6f;

        [Header("Pages")]
        [Tooltip("Other pages move the light here.")]
        [SerializeField] private RectTransform pageTarget;
        [Tooltip("Share of the page target the glow covers.")]
        [SerializeField, Min(0f)] private Vector2 pageSize = new(0.6f, 0.83f);
        [SerializeField, Min(0f)] private float pageDuration = 0.6f;

        const int SeedProperty = 31401;
        const int ZoomProperty = 31402;
        const int PageProperty = 31403;
        const int ColorProperty = 31404;
        const int AlphaProperty = 31405;

        // A rect measured in the skin's space, kept with what it was measured from
        struct Measurement
        {
            public Vector2 center, size;
            Matrix4x4 world, local;
            Vector4 anchors, shape, sizes;
            bool valid;

            // Whether the rect may have moved or resized since it was measured: its transform, its own layout, its parent's size
            // or the screen changed. These reads are cheap, unlike the rect itself
            public bool Changed(RectTransform target, RectTransform space)
            {
                Matrix4x4 targetWorld = target.localToWorldMatrix, spaceLocal = space.worldToLocalMatrix;
                Vector2 min = target.anchorMin, max = target.anchorMax, delta = target.sizeDelta, pivot = target.pivot;
                Vector2 parent = target.parent is RectTransform parentRect ? parentRect.sizeDelta : Vector2.zero;
                var targetAnchors = new Vector4(min.x, min.y, max.x, max.y);
                var targetShape = new Vector4(delta.x, delta.y, pivot.x, pivot.y);
                var outerSizes = new Vector4(parent.x, parent.y, Screen.width, Screen.height);

                if (valid && targetWorld.Equals(world) && spaceLocal.Equals(local) && targetAnchors.Equals(anchors)
                    && targetShape.Equals(shape) && outerSizes.Equals(sizes))
                    return false;

                valid = true;
                world = targetWorld;
                local = spaceLocal;
                anchors = targetAnchors;
                shape = targetShape;
                sizes = outerSizes;
                return true;
            }

            public void Invalidate() => valid = false;
        }

        RectTransform rect, target;
        int theme = 1;
        float baseScale, seed, zoom = 1, page;
        Color color, colorTarget;
        Vector2 alpha;
        Vector4 glowRect = new(float.NaN, 0, 0, 0);
        Measurement targetMeasure, pageMeasure;
        TweenHandle zoomTween, colorTween, alphaTween;
        bool colorsDirty = true;

        void Awake()
        {
            rect = (RectTransform)skin.transform;
            baseScale = skin.Scale;
            seed = skin.Seed;
            color = colorTarget = skin.color;
            alpha = Alphas(theme);
        }

        Vector2 Alphas(int index) => new(lineAlpha[index], glowAlpha[index]);

        /// <summary>
        /// The rect the falloff and the glow sit around: the deck, where the top card rests.
        /// </summary>
        public void SetTarget(RectTransform value)
        {
            target = value;
            targetMeasure.Invalidate();
        }

        public void SetTheme(int index, float duration)
        {
            theme = index;
            alphaTween.Kill();

            if (duration <= 0)
            {
                SetAlpha(Alphas(index));
                return;
            }

            alphaTween = EvoTween.Create(alpha, Alphas(index), duration)
                .WithTimeMode(TimeMode.Unscaled)
                .Bind(this, static (background, value) => background.SetAlpha(value), AlphaProperty);
        }

        public void SetColor(Color value, float duration)
        {
            value.a = 1;
            if (value == colorTarget)
                return;

            colorTarget = value;
            colorTween.Kill();

            if (duration <= 0)
            {
                SetTint(value);
                return;
            }

            colorTween = EvoTween.Create(color, value, duration)
                .WithTimeMode(TimeMode.Unscaled)
                .Bind(this, static (background, tint) => background.SetTint(tint), ColorProperty);
        }

        /// <summary>
        /// A swap: the map's seed drifts a little, so the ground slides and re-forms.
        /// </summary>
        public void Shift()
        {
            EvoTween.Create(seed, seed + swapShift, shiftDuration)
                .WithEase(Ease.OutCubic)
                .WithTimeMode(TimeMode.Unscaled)
                .Bind(this, static (background, value) => background.SetSeed(value), SeedProperty);
        }

        public void SetOpen(bool open)
        {
            zoomTween = EvoTween.Create(zoom, open ? openZoom : 1, open ? zoomInDuration : zoomOutDuration)
                .WithEase(Ease.OutQuint)
                .WithTimeMode(TimeMode.Unscaled)
                .Bind(this, static (background, value) => background.SetZoom(value), ZoomProperty);
        }

        public void SetPage(bool value)
        {
            EvoTween.Create(page, value ? 1f : 0f, pageDuration)
                .WithEase(Ease.InOutCubic)
                .WithTimeMode(TimeMode.Unscaled)
                .Bind(this, static (background, share) => background.page = share, PageProperty);
        }

        void SetAlpha(Vector2 value)
        {
            alpha = value;
            colorsDirty = true;
        }

        void SetTint(Color value)
        {
            color = value;
            colorsDirty = true;
        }

        void SetSeed(float value)
        {
            seed = value;
            skin.Seed = Mathf.Repeat(value, 100);
        }

        void SetZoom(float value)
        {
            zoom = value;
            skin.Scale = baseScale * value;
        }

        void Update()
        {
            if (colorsDirty)
            {
                colorsDirty = false;
                ApplyColors();
            }

            UpdateTarget();
        }

        void ApplyColors()
        {
            skin.color = new Color(color.r, color.g, color.b, alpha.x);
            skin.FalloffOutsideOpacity = outsideOpacity[theme];
            glow.OuterShadowColor = new Color(color.r, color.g, color.b, alpha.y);
        }

        // Center and size of what the light sits around, in the skin's space
        void UpdateTarget()
        {
            Vector2 center = Vector2.zero, size = Vector2.zero;
            if (target != null && page < 1)
                Measure(target, Vector2.one, ref targetMeasure, out center, out size);

            if (page > 0 && pageTarget != null)
            {
                Measure(pageTarget, pageSize, ref pageMeasure, out Vector2 pageCenter, out Vector2 pageSizeValue);
                center = Vector2.Lerp(center, pageCenter, page);
                size = Vector2.Lerp(size, pageSizeValue, page);
            }

            Light(center, size);
        }

        // The falloff centers on the lit rect, keeping the skin's own Start, End and Aspect; the glow sits around it
        void Light(Vector2 center, Vector2 size)
        {
            Rect area = rect.rect;
            if (area.width <= 0 || area.height <= 0)
                return;

            Place(glow, center, size + glowPadding * 2, ref glowRect);
        }

        // Reading a rect under the root canvas makes the canvas lay out its own rect again first, about 13 us a read for one right
        // under it even in players, so a light target is measured again only when it may have changed
        void Measure(RectTransform target, Vector2 share, ref Measurement measurement, out Vector2 center, out Vector2 size)
        {
            if (measurement.Changed(target, rect))
                Measure(target, share, out measurement.center, out measurement.size);

            center = measurement.center;
            size = measurement.size;
        }

        void Measure(RectTransform target, Vector2 share, out Vector2 center, out Vector2 size)
        {
            Rect area = target.rect;
            center = rect.InverseTransformPoint(target.TransformPoint(area.center));
            float scale = target.lossyScale.x / Mathf.Max(rect.lossyScale.x, 1e-6f);
            size = Vector2.Scale(area.size, share) * scale;
        }

        // The glow is an ellipse whose shadow softness equals its smaller radius, so it fades from the center
        static void Place(ProceduralShape shape, Vector2 center, Vector2 size, ref Vector4 last)
        {
            var value = new Vector4(center.x, center.y, size.x, size.y);
            if (value == last)
                return;

            RectTransform shapeRect = shape.rectTransform;
            if (value.x != last.x || value.y != last.y)
                shapeRect.anchoredPosition = center;

            if (value.z != last.z || value.w != last.w)
            {
                shapeRect.sizeDelta = size;
                shape.OuterShadowSoftness = Mathf.Min(size.x, size.y) * 0.5f;
            }

            last = value;
        }

#if UNITY_EDITOR
        // Inspector edits show at once: in Play mode here, in edit mode through Home's preview of the start card
        void OnValidate()
        {
            if (!Application.isPlaying)
            {
                if (home != null)
                    UnityEditor.EditorApplication.delayCall += home.PreviewStart;

                return;
            }

            // Not set up yet: entering Play mode without a scene reload validates before Awake
            if (baseScale <= 0)
                return;

            alphaTween.Kill();
            SetAlpha(Alphas(theme));
            glowRect.x = float.NaN;
            pageMeasure.Invalidate();

            if (!zoomTween.IsActive)
                SetZoom(zoom > 1 ? openZoom : 1);
        }

        // Edit mode: the look the page opens with, lit around the target
        public void PreviewStart(RectTransform target, int themeIndex, Color tint)
        {
            rect = (RectTransform)skin.transform;
            theme = themeIndex;
            color = tint;
            alpha = Alphas(themeIndex);
            glowRect.x = float.NaN;
            ApplyColors();
            Measure(target, Vector2.one, out Vector2 center, out Vector2 size);
            Light(center, size);
        }
#endif
    }
}
