using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gear.Utilities.UI.Navigation
{
    /// <summary>
    /// 页面视图：一列页面通过 content 的位移切换，支持 Prev/Next 按钮、可调过渡与焦点迁移。
    /// <para>
    /// 页面可以是任意 RectTransform；默认自动横向/纵向排版，也可以把 <c>autoLayout</c> 关掉自行摆放。
    /// </para>
    /// </summary>
    [AddComponentMenu("UI/Gear/Page View")]
    [DisallowMultipleComponent]
    public sealed class GearPageView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform content;
        [SerializeField] private List<RectTransform> pages = new List<RectTransform>();
        [SerializeField] private bool horizontal = true;
        [SerializeField] private bool autoLayout = true;
        [SerializeField, Min(0f)] private float pagePadding;

        [Tooltip("页面尺寸；为 0 时使用 viewport 的 rect 尺寸")]
        [SerializeField] private Vector2 pageSizeOverride;
        [SerializeField][Min(0f)] private float transitionDuration = 0.25f;
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField] private int startIndex;
        [SerializeField] private bool moveFocusToPage = true;

        [SerializeField] private Button nextButton;
        [SerializeField] private Button backButton;
        [SerializeField] private List<Button> indicators = new List<Button>();
        [SerializeField] private bool enableDrag = true;
        [SerializeField][Min(0f)] private float dragSensitivity = 1f;
        [SerializeField] private Color activeIndicatorColor = Color.white;
        [SerializeField] private Color inactiveIndicatorColor = new Color(1f, 1f, 1f, 0.35f);
        [SerializeField] private UnityEvent<int> onPageChanged = new UnityEvent<int>();

        private Vector2 animStart;
        private float animElapsed;
        private bool animating;

        private readonly List<(Button button, UnityAction action)> bindings =
            new List<(Button, UnityAction)>();

        public int CurrentIndex { get; private set; } = -1;
        public int TargetIndex { get; private set; } = -1;
        public bool IsAnimating => animating;
        public IReadOnlyList<RectTransform> Pages => pages;
        public IReadOnlyList<Button> Indicators => indicators;
        public bool EnableDrag { get => enableDrag; set => enableDrag = value; }
        public bool IsDragging { get; private set; }

        public Button NextButton { get => nextButton; set => nextButton = value; }
        public Button BackButton { get => backButton; set => backButton = value; }
        public bool Horizontal { get => horizontal; set => horizontal = value; }
        public bool AutoLayout { get => autoLayout; set => autoLayout = value; }
        public float PagePadding
        {
            get => pagePadding;
            set
            {
                pagePadding = Mathf.Max(0f, value);
                if (autoLayout) LayoutPages();
            }
        }
        public Vector2 PageSizeOverride { get => pageSizeOverride; set => pageSizeOverride = value; }
        public float TransitionDuration { get => transitionDuration; set => transitionDuration = Mathf.Max(0f, value); }

        public event Action<int> PageChanged;

        private void OnEnable()
        {
            if (viewport == null) viewport = (RectTransform)transform;
            Bind();
            if (pages.Count > 0 && CurrentIndex < 0)
                GoTo(startIndex, true);
        }

        private void OnDisable()
        {
            Unbind();
        }

        /// <summary>运行时配置页面；会重新排版并跳到起始页。</summary>
        public void Configure(RectTransform viewportRect, RectTransform contentRect, IList<RectTransform> items)
        {
            Unbind();
            viewport = viewportRect != null ? viewportRect : viewport;
            content = contentRect;
            pages.Clear();
            if (items != null)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i] != null)
                        pages.Add(items[i]);
                }
            }

            CurrentIndex = -1;
            if (autoLayout) LayoutPages();

            if (isActiveAndEnabled)
                Bind();

            if (pages.Count > 0)
                GoTo(startIndex, true);
        }

        /// <summary>设置页码指示器；按钮点击会跳到对应页。</summary>
        public void SetIndicators(IList<Button> items)
        {
            indicators.Clear();
            if (items != null)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i] != null)
                        indicators.Add(items[i]);
                }
            }

            if (isActiveAndEnabled)
            {
                Bind();
                UpdateIndicators();
            }
        }

        /// <summary>按当前 viewport/override 尺寸把页面排成横向或纵向条带。</summary>
        public void LayoutPages()
        {
            if (content == null) return;

            Vector2 size = GetPageSize();
            content.anchoredPosition = Vector2.zero;
            for (int i = 0; i < pages.Count; i++)
            {
                RectTransform page = pages[i];
                if (page == null) continue;

                page.anchorMin = new Vector2(0f, 1f);
                page.anchorMax = new Vector2(0f, 1f);
                page.pivot = new Vector2(0f, 1f);
                page.sizeDelta = size;
                page.anchoredPosition = horizontal
                    ? new Vector2(i * size.x + pagePadding, 0f)
                    : new Vector2(0f, -i * size.y - pagePadding);
            }
        }

        /// <summary>跳到指定页；越界夹到首尾。</summary>
        public void GoTo(int index, bool instant = false)
        {
            if (pages.Count == 0) return;

            index = Mathf.Clamp(index, 0, pages.Count - 1);
            bool changed = CurrentIndex != index;
            CurrentIndex = index;
            TargetIndex = index;

            if (instant || transitionDuration <= 0f || !Application.isPlaying)
            {
                if (content != null)
                    content.anchoredPosition = GetTargetPosition(index);
                animating = false;
            }
            else
            {
                animStart = content != null ? content.anchoredPosition : Vector2.zero;
                animElapsed = 0f;
                animating = true;
            }

            if (moveFocusToPage)
                MoveFocus(pages[index]);

            UpdateIndicators();

            if (changed)
            {
                onPageChanged?.Invoke(index);
                PageChanged?.Invoke(index);
            }
        }

        public void Next() => GoTo(CurrentIndex + 1);
        public void Prev() => GoTo(CurrentIndex - 1);

        private void Update()
        {
            if (!animating || content == null) return;

            animElapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(animElapsed / Mathf.Max(transitionDuration, 1e-4f));
            float eased = Mathf.SmoothStep(0f, 1f, t);
            content.anchoredPosition = Vector2.Lerp(animStart, GetTargetPosition(TargetIndex), eased);

            if (t >= 1f)
                animating = false;
        }

        private Vector2 GetTargetPosition(int index)
        {
            Vector2 size = GetPageSize();
            Vector2 current = content != null ? content.anchoredPosition : Vector2.zero;
            return horizontal
                ? new Vector2(-index * size.x, current.y)
                : new Vector2(current.x, index * size.y);
        }

        private Vector2 GetPageSize()
        {
            if (pageSizeOverride.x > 0f && pageSizeOverride.y > 0f)
                return pageSizeOverride;

            if (viewport != null)
            {
                Vector2 size = viewport.rect.size;
                if (size.x > 0f && size.y > 0f) return size;
            }

            return Vector2.one;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!enableDrag || content == null || pages.Count == 0) return;
            IsDragging = true;
            animating = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!IsDragging || content == null || eventData == null) return;

            Vector2 position = content.anchoredPosition;
            if (horizontal)
                position.x += eventData.delta.x * dragSensitivity;
            else
                position.y += eventData.delta.y * dragSensitivity;

            content.anchoredPosition = ClampPosition(position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!IsDragging || content == null || pages.Count == 0) return;
            IsDragging = false;

            Vector2 size = GetPageSize();
            float offset = horizontal
                ? -content.anchoredPosition.x / Mathf.Max(size.x, 1e-4f)
                : content.anchoredPosition.y / Mathf.Max(size.y, 1e-4f);
            GoTo(Mathf.RoundToInt(offset));
        }

        private Vector2 ClampPosition(Vector2 position)
        {
            Vector2 size = GetPageSize();
            if (horizontal)
                position.x = Mathf.Clamp(position.x, -(pages.Count - 1) * size.x, 0f);
            else
                position.y = Mathf.Clamp(position.y, 0f, (pages.Count - 1) * size.y);
            return position;
        }

        private void UpdateIndicators()
        {
            for (int i = 0; i < indicators.Count; i++)
            {
                Button indicator = indicators[i];
                if (indicator == null || indicator.targetGraphic == null) continue;
                indicator.targetGraphic.color = i == CurrentIndex ? activeIndicatorColor : inactiveIndicatorColor;
            }
        }

        private void Bind()
        {
            Unbind();

            if (nextButton != null)
            {
                UnityAction next = Next;
                nextButton.onClick.AddListener(next);
                bindings.Add((nextButton, next));
            }

            if (backButton != null)
            {
                UnityAction prev = Prev;
                backButton.onClick.AddListener(prev);
                bindings.Add((backButton, prev));
            }

            for (int i = 0; i < indicators.Count; i++)
            {
                if (indicators[i] == null) continue;
                int index = i;
                UnityAction action = () => GoTo(index);
                indicators[i].onClick.AddListener(action);
                bindings.Add((indicators[i], action));
            }
        }

        private void Unbind()
        {
            for (int i = 0; i < bindings.Count; i++)
            {
                if (bindings[i].button != null)
                    bindings[i].button.onClick.RemoveListener(bindings[i].action);
            }

            bindings.Clear();
        }

        private void MoveFocus(RectTransform page)
        {
            if (EventSystem.current == null || page == null) return;

            Selectable selectable = page.GetComponentInChildren<Selectable>(true);
            if (selectable != null)
                EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        }
    }
}
