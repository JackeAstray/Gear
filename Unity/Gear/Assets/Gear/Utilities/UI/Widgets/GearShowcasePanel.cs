using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Gear.Utilities.UI.ImageExtensions;
using Gear.Utilities.UI.Navigation;

namespace Gear.Utilities.UI.Widgets
{
    /// <summary>
    /// Showcase 面板：一页一个条目，PageView 负责滑动，标题/说明/背景负责内容，
    /// 可选自动轮播与淡入；按钮列表可直接跳页。
    /// </summary>
    [AddComponentMenu("UI/Gear/Showcase Panel")]
    [DisallowMultipleComponent]
    public sealed class GearShowcasePanel : MonoBehaviour
    {
        [Serializable]
        public struct Item
        {
            public string title;
            [TextArea(2, 4)] public string description;
            public Color color;
            public Sprite sprite;
        }

        [SerializeField] private GearPageView pageView;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text descriptionLabel;
        [SerializeField] private ImageEx background;
        [SerializeField] private CanvasGroup contentGroup;
        [SerializeField] private List<Item> items = new List<Item>();
        [SerializeField] private List<Button> pageButtons = new List<Button>();
        [SerializeField] private bool autoPlay;
        [SerializeField, Min(0.1f)] private float autoInterval = 3f;
        [SerializeField] private bool loop = true;
        [SerializeField, Min(0f)] private float fadeDuration = 0.18f;
        [SerializeField] private UnityEvent<int> onPageChanged = new UnityEvent<int>();

        private float autoTimer;
        private float fadeTimer;
        private bool fading;

        public IReadOnlyList<Item> Items => items;
        public int CurrentIndex => pageView != null ? pageView.CurrentIndex : 0;
        public UnityEvent<int> OnPageChanged => onPageChanged;
        public bool AutoPlay { get => autoPlay; set => autoPlay = value; }
        public float AutoInterval { get => autoInterval; set => autoInterval = Mathf.Max(0.1f, value); }

        private void OnEnable()
        {
            if (pageView != null)
            {
                pageView.PageChanged -= HandlePageChanged;
                pageView.PageChanged += HandlePageChanged;
            }

            BindButtons();
            ApplyPage(CurrentIndex);
        }

        private void OnDisable()
        {
            if (pageView != null) pageView.PageChanged -= HandlePageChanged;
        }

        public void Configure(GearPageView view, TMP_Text title, TMP_Text description,
            ImageEx panelBackground, CanvasGroup group, IList<Item> source, IList<Button> buttons = null)
        {
            pageView = view;
            titleLabel = title;
            descriptionLabel = description;
            background = panelBackground;
            contentGroup = group;
            items.Clear();
            if (source != null)
            {
                for (int i = 0; i < source.Count; i++) items.Add(source[i]);
            }

            pageButtons.Clear();
            if (buttons != null)
            {
                for (int i = 0; i < buttons.Count; i++)
                {
                    if (buttons[i] != null) pageButtons.Add(buttons[i]);
                }
            }

            if (isActiveAndEnabled) OnEnable();
        }

        public void SetItems(IList<Item> source)
        {
            items.Clear();
            if (source != null)
            {
                for (int i = 0; i < source.Count; i++) items.Add(source[i]);
            }

            ApplyPage(CurrentIndex);
        }

        public void GoTo(int index, bool instant = false)
        {
            if (pageView == null || items.Count == 0) return;
            if (loop) index = (index % items.Count + items.Count) % items.Count;
            else index = Mathf.Clamp(index, 0, items.Count - 1);
            pageView.GoTo(index, instant);
            ApplyPage(index);
        }

        public void Next()
        {
            GoTo(CurrentIndex + 1);
        }

        /// <summary>手动推进自动轮播；测试或外部时钟可用。</summary>
        public void Tick(float delta)
        {
            if (!autoPlay || items.Count <= 1 || pageView == null || pageView.IsDragging) return;
            autoTimer += Mathf.Max(0f, delta);
            if (autoTimer >= autoInterval)
            {
                autoTimer = 0f;
                Next();
            }
        }

        private void Update()
        {
            Tick(Time.unscaledDeltaTime);
            if (!fading || contentGroup == null) return;
            fadeTimer += Time.unscaledDeltaTime;
            float t = fadeDuration <= 0f ? 1f : Mathf.Clamp01(fadeTimer / fadeDuration);
            contentGroup.alpha = t;
            if (t >= 1f) fading = false;
        }

        private void HandlePageChanged(int index)
        {
            ApplyPage(index);
            onPageChanged?.Invoke(index);
        }

        private void ApplyPage(int index)
        {
            if (items.Count == 0) return;
            index = Mathf.Clamp(index, 0, items.Count - 1);
            Item item = items[index];
            if (titleLabel != null) titleLabel.text = item.title;
            if (descriptionLabel != null) descriptionLabel.text = item.description;
            if (background != null)
            {
                background.color = item.color;
                if (item.sprite != null) background.sprite = item.sprite;
            }

            if (contentGroup != null && isActiveAndEnabled && Application.isPlaying && fadeDuration > 0f)
            {
                contentGroup.alpha = 0f;
                fadeTimer = 0f;
                fading = true;
            }
        }

        private void BindButtons()
        {
            for (int i = 0; i < pageButtons.Count; i++)
            {
                Button button = pageButtons[i];
                if (button == null) continue;
                int index = i;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => GoTo(index));
            }
        }
    }
}
