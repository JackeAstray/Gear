using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Gear.Utilities.UI.Navigation;

namespace Gear.Utilities.UI.Widgets
{
    /// <summary>
    /// 轮播：包装 GearPageView，提供自动轮播、循环、指示器缩放、邻页透明度/缩放。
    /// 邻页可见性由 PageView.PagePadding + PageSizeOverride 控制，本组件只做视觉状态。
    /// </summary>
    [AddComponentMenu("UI/Gear/Carousel")]
    [DisallowMultipleComponent]
    public sealed class GearCarousel : MonoBehaviour
    {
        [SerializeField] private GearPageView pageView;
        [SerializeField] private bool autoSlide;
        [SerializeField, Min(0.1f)] private float autoInterval = 3f;
        [SerializeField] private bool loop = true;
        [SerializeField] private bool indicatorShrink = true;
        [SerializeField, Min(1f)] private float indicatorSize = 8f;
        [SerializeField, Min(1f)] private float selectedIndicatorSize = 18f;
        [SerializeField] private bool showNeighbors = true;
        [SerializeField, Range(0f, 1f)] private float neighborOpacity = 0.38f;
        [SerializeField, Range(0.5f, 1f)] private float neighborScale = 0.9f;
        [SerializeField, Min(0)] private int maxVisibleNeighbors = 1;

        private readonly Dictionary<RectTransform, CanvasGroup> groups = new Dictionary<RectTransform, CanvasGroup>();
        private float timer;

        public GearPageView PageView => pageView;
        public int CurrentIndex => pageView != null ? pageView.CurrentIndex : 0;
        public bool AutoSlide { get => autoSlide; set => autoSlide = value; }
        public float AutoInterval { get => autoInterval; set => autoInterval = Mathf.Max(0.1f, value); }
        public bool Loop { get => loop; set => loop = value; }

        public void Configure(GearPageView view, IList<Button> indicators = null)
        {
            if (pageView != null) pageView.PageChanged -= HandlePageChanged;
            pageView = view;
            if (pageView == null) return;
            pageView.PageChanged += HandlePageChanged;
            if (indicators != null) pageView.SetIndicators(indicators);
            EnsureGroups();
            ApplyVisuals();
        }

        private void OnEnable()
        {
            if (pageView != null)
            {
                pageView.PageChanged -= HandlePageChanged;
                pageView.PageChanged += HandlePageChanged;
            }

            EnsureGroups();
            ApplyVisuals();
        }

        private void OnDisable()
        {
            if (pageView != null) pageView.PageChanged -= HandlePageChanged;
        }

        private void Update()
        {
            Tick(Time.unscaledDeltaTime);
        }

        /// <summary>手动推进自动轮播，测试/外部时钟可用。</summary>
        public void Tick(float delta)
        {
            if (!autoSlide || pageView == null || pageView.Pages.Count <= 1 || pageView.IsDragging) return;
            timer += Mathf.Max(0f, delta);
            if (timer < autoInterval) return;
            timer = 0f;
            Next();
        }

        public void Next()
        {
            if (pageView == null || pageView.Pages.Count == 0) return;
            int next = pageView.CurrentIndex + 1;
            if (next >= pageView.Pages.Count) next = loop ? 0 : pageView.Pages.Count - 1;
            pageView.GoTo(next);
            ApplyVisuals();
        }

        public void Prev()
        {
            if (pageView == null || pageView.Pages.Count == 0) return;
            int prev = pageView.CurrentIndex - 1;
            if (prev < 0) prev = loop ? pageView.Pages.Count - 1 : 0;
            pageView.GoTo(prev);
            ApplyVisuals();
        }

        private void HandlePageChanged(int index)
        {
            timer = 0f;
            ApplyVisuals();
        }

        private void EnsureGroups()
        {
            groups.Clear();
            if (pageView == null) return;
            IReadOnlyList<RectTransform> pages = pageView.Pages;
            for (int i = 0; i < pages.Count; i++)
            {
                RectTransform page = pages[i];
                if (page == null) continue;
                CanvasGroup group = page.GetComponent<CanvasGroup>();
                if (group == null) group = page.gameObject.AddComponent<CanvasGroup>();
                groups[page] = group;
            }
        }

        private void ApplyVisuals()
        {
            if (pageView == null) return;
            IReadOnlyList<RectTransform> pages = pageView.Pages;
            int current = Mathf.Clamp(pageView.CurrentIndex, 0, Mathf.Max(0, pages.Count - 1));
            for (int i = 0; i < pages.Count; i++)
            {
                RectTransform page = pages[i];
                if (page == null) continue;
                if (!groups.TryGetValue(page, out CanvasGroup group))
                {
                    group = page.GetComponent<CanvasGroup>();
                    if (group == null) group = page.gameObject.AddComponent<CanvasGroup>();
                    groups[page] = group;
                }

                int distance = Mathf.Abs(i - current);
                bool currentPage = distance == 0;
                bool neighbor = showNeighbors && distance <= maxVisibleNeighbors;
                group.alpha = currentPage || !neighbor ? 1f : neighborOpacity;
                page.localScale = Vector3.one * (currentPage || !neighbor ? 1f : neighborScale);
            }

            if (!indicatorShrink) return;
            IReadOnlyList<Button> indicators = pageView.Indicators;
            for (int i = 0; i < indicators.Count; i++)
            {
                Button button = indicators[i];
                if (button == null) continue;
                var rect = (RectTransform)button.transform;
                float size = i == current ? selectedIndicatorSize : indicatorSize;
                rect.sizeDelta = new Vector2(size, size);
            }
        }
    }
}
