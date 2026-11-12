using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using Evo.Tween;
using Evo.Tween.Modules.UGUI;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Evo/UI/Layout/Tab Manager"), HelpURL(Constants.HelpUrl + "layout/tab-manager")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Evo.UI", null, "Tabs")]
    public class TabManager : MonoBehaviour
    {
        // Tabs
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("defaultTabIndex")]
        private int currentTabIndex = 0;
        public List<Tab> tabs = new();

        // Settings
        public bool useUnscaledTime = true;
        [Tooltip("Deactivate hidden tab GameObjects after their out animation finishes.")]
        public bool disableInvisibleTabs = true;
        [Tooltip("When incoming tabs become interactable and block raycasts.")]
        public ActivationTiming activationTiming = ActivationTiming.OnInStart;

        // Animations
        [Tooltip("Reverse slide offsets when moving to an earlier tab.")]
        public bool animateDirectionally = true;
        public TweenSequence inAnimation = CreateAnimation(true);
        public TweenSequence outAnimation = CreateAnimation(false);

        // Title Display
        [Tooltip("Reverse the title slide offset when moving to an earlier tab.")]
        public bool animateTitleDirectionally = false;
        public AnimationCurve titleSlideCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Min(0f)] public float titleSlideDuration = 0.2f;
        [Tooltip("Delay in seconds before the new title animates in.")]
        [Min(0f)] public float titleChangeDelay = 0f;
        [Tooltip("Local position offset used to slide the title in and out.")]
        public Vector2 titleSlideOffset = new(0f, -25f);

        // Indicator
        [Tooltip("Axis along which the indicator moves and resizes.")]
        public IndicatorDirection indicatorDirection = IndicatorDirection.Horizontal;
        [Tooltip("Resize the indicator to match the selected tab button along the indicator axis.")]
        public bool indicatorAutoSize = true;
        [Tooltip("Extra stretch applied along the indicator axis during its animation.")]
        public float indicatorStretch = 50f;
        public AnimationCurve indicatorCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Min(0f)] public float indicatorDuration = 0.3f;

        // References
        [Tooltip("Optional title text displaying the selected tab button text, or the tab ID when no button text is available.")]
        [TextComponent] public Graphic titleObject;
        [Tooltip("Optional indicator that follows the selected tab button.")]
        public RectTransform indicatorObject;

        // Events
        public UnityEvent<int> onTabChanged = new();

        // Enums
        public enum ActivationTiming { OnInStart, OnInComplete }
        public enum IndicatorDirection { Horizontal, Vertical }

        // Properties
        public int CurrentTabIndex => currentTabIndex;
        public Tab CurrentTab => IsValidTab(currentTabIndex) ? tabs[currentTabIndex] : null;
        public string CurrentTabID => CurrentTab?.tabID ?? string.Empty;
        TimeMode AnimationTimeMode => useUnscaledTime ? TimeMode.Unscaled : TimeMode.Scaled;

        // Runtime State
        Tab visibleTab;
        bool isInitialized;
        bool isExiting;
        bool reverseAnimation;
        TweenHandle tabTween;
        TweenHandle titleTween;
        TweenHandle indicatorTween;
        TweenSequence reverseInAnimation;
        TweenSequence reverseOutAnimation;

        // Cache
        Action completeOut;
        Action completeIn;
        Action completeTitleOut;
        Action<float> updateTitle;
        Action<float> updateIndicator;
        CanvasGroup titleCanvasGroup;
        Vector2 titleOrigin;
        Vector2 titleStartPosition;
        Vector2 titleTargetPosition;
        Vector2 titleOffset;
        float titleStartAlpha;
        float titleTargetAlpha;
        bool titleExiting;
        Vector2 indicatorStartPosition;
        Vector2 indicatorTargetPosition;
        Vector2 indicatorStartSize;
        Vector2 indicatorTargetSize;
        bool indicatorHasTarget;
        readonly Vector3[] indicatorCorners = new Vector3[4];

        void Awake()
        {
            completeOut = CompleteOut;
            completeIn = CompleteIn;
            completeTitleOut = CompleteTitleOut;
            updateTitle = UpdateTitle;
            updateIndicator = UpdateIndicator;

            if (titleObject != null)
            {
                titleOrigin = titleObject.rectTransform.anchoredPosition;
                if (!titleObject.TryGetComponent(out titleCanvasGroup))
                    titleCanvasGroup = titleObject.gameObject.AddComponent<CanvasGroup>();
            }
        }

        void Start()
        {
            reverseInAnimation = CreateReverseAnimation(inAnimation);
            reverseOutAnimation = CreateReverseAnimation(outAnimation);

            for (int i = 0; i < tabs.Count; i++)
            {
                Tab tab = tabs[i];
                if (tab?.tabObject == null)
                    continue;

                tab.Bind(this);
                tab.SetVisibility(false, disableInvisibleTabs);
            }

            isInitialized = true;

            if (!IsValidTab(currentTabIndex))
                currentTabIndex = FindValidTab();

            ApplySelection();
            onTabChanged?.Invoke(currentTabIndex);
        }

        void OnEnable()
        {
            if (isInitialized)
                ApplySelection();
        }

        void OnDisable()
        {
            if (isInitialized)
                ApplySelection();
            else
                StopAnimations();
        }

        void OnDestroy()
        {
            StopAnimations();

            for (int i = 0; i < tabs.Count; i++)
                tabs[i]?.Unbind();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= UpdateEditorPreview;
#endif
        }

        #region Animation Setup
        static TweenSequence CreateAnimation(bool opening)
        {
            TweenSequence sequence = new() { TimeMode = TimeMode.Unscaled };
         
            TweenClip fade = new();
            fade.Settings.Module = new CanvasGroupAlphaModule { fromCurrent = !opening, from = 0f, to = opening ? 1f : 0f };
            fade.Settings.Duration = 0.15f;
            fade.Settings.Ease = Ease.InOutSine;

            TweenClip slide = new();
            slide.Settings.Module = new RectTransformSlideModule
            {
                fromCurrent = !opening,
                from = opening ? new Vector2(40f, 0f) : Vector2.zero,
                to = opening ? Vector2.zero : new Vector2(-40f, 0f)
            };
            slide.Settings.Duration = 0.15f;
            slide.Settings.Ease = opening ? Ease.OutCubic : Ease.InCubic;

            sequence.InlineClips.Add(slide);
            sequence.InlineClips.Add(fade);
            return sequence;
        }

        static TweenSequence CreateReverseAnimation(TweenSequence source)
        {
            if (source == null)
                return null;

            TweenSequence sequence = source.Clone();
            sequence.DetachPreset();
            for (int i = 0; i < sequence.Count; i++)
            {
                if (sequence.Clips[i]?.Settings.Module is RectTransformSlideModule slide)
                {
                    slide.from = -slide.from;
                    slide.to = -slide.to;
                }
            }
            return sequence;
        }
        #endregion

        #region Tab Selection
        bool IsValidTab(int index) => index >= 0 && index < tabs.Count && tabs[index]?.tabObject != null;

        int FindValidTab()
        {
            for (int i = 0; i < tabs.Count; i++)
            {
                if (IsValidTab(i))
                    return i;
            }
            return -1;
        }

        int FindTab(string tabID)
        {
            if (!string.IsNullOrEmpty(tabID))
            {
                for (int i = 0; i < tabs.Count; i++)
                {
                    if (IsValidTab(i) && tabs[i].tabID == tabID)
                        return i;
                }
            }
            return -1;
        }

        void StopAnimations()
        {
            tabTween.Kill();
            titleTween.Kill();
            indicatorTween.Kill();
            tabTween = default;
            titleTween = default;
            indicatorTween = default;
            isExiting = false;
            titleExiting = false;
        }

        void ApplySelection()
        {
            StopAnimations();
            visibleTab = CurrentTab;

            for (int i = 0; i < tabs.Count; i++)
            {
                Tab tab = tabs[i];
                if (tab?.tabObject == null)
                    continue;

                tab.RestoreOriginalPosition();
                tab.SetVisibility(i == currentTabIndex, disableInvisibleTabs);
            }

            UpdateButtonStates();
            RefreshTitleObject();
            RefreshIndicator();

            if (isActiveAndEnabled)
                SelectUIElement(visibleTab);
        }

        void UpdateButtonStates()
        {
            for (int i = 0; i < tabs.Count; i++)
            {
                if (tabs[i]?.tabButton != null)
                    tabs[i].tabButton.SetState(i == currentTabIndex ? InteractionState.Selected : InteractionState.Normal);
            }
        }
        #endregion

        #region Navigation
        void StoreSelection()
        {
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected != null && visibleTab?.tabObject != null && selected.transform.IsChildOf(visibleTab.tabObject))
            {
                visibleTab.latestSelected = selected;
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        void SelectUIElement(Tab tab)
        {
            if (tab == null || EventSystem.current == null || EventSystem.current.alreadySelecting)
                return;

            GameObject selected = tab.latestSelected;
            if (selected == null || !selected.activeInHierarchy)
                selected = tab.firstSelected;
            if (selected != null && selected.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(selected);
        }
        #endregion

        #region Tab Animations
        void BeginOut()
        {
            // A second request during the exit only changes the destination. Never reveal a partially hidden tab.
            if (isExiting && tabTween.IsActive && visibleTab?.tabObject != null)
                return;

            tabTween.Kill();
            isExiting = true;

            if (visibleTab?.tabObject == null)
            {
                CompleteOut();
                return;
            }

            visibleTab.SetInteraction(false);
            TweenSequence sequence = reverseAnimation ? reverseOutAnimation : outAnimation;
            tabTween = sequence != null
                ? sequence.Trigger(visibleTab.tabObject.gameObject).WithTimeMode(AnimationTimeMode).OnComplete(completeOut)
                : default;

            if (!tabTween.IsActive)
                CompleteOut();
        }

        void CompleteOut()
        {
            tabTween = default;
            isExiting = false;

            if (visibleTab?.tabObject != null)
            {
                visibleTab.SetVisibility(false, disableInvisibleTabs);
                visibleTab.RestoreOriginalPosition();
            }

            visibleTab = CurrentTab;
            if (visibleTab == null || !isActiveAndEnabled)
                return;

            visibleTab.RestoreOriginalPosition();
            visibleTab.SetVisibility(true);
            visibleTab.SetInteraction(activationTiming == ActivationTiming.OnInStart);

            TweenSequence sequence = reverseAnimation ? reverseInAnimation : inAnimation;
            tabTween = sequence != null
                ? sequence.Trigger(visibleTab.tabObject.gameObject).WithTimeMode(AnimationTimeMode).OnComplete(completeIn)
                : default;

            if (!tabTween.IsActive)
                CompleteIn();
        }

        void CompleteIn()
        {
            tabTween = default;
            if (visibleTab?.tabObject == null)
                return;

            visibleTab.SetVisibility(true);
            SelectUIElement(visibleTab);
        }
        #endregion

        #region Title Animation
        void AnimateTitle(bool animate, int previousIndex)
        {
            if (titleObject == null || titleCanvasGroup == null)
                return;

            titleOffset = animateTitleDirectionally && currentTabIndex < previousIndex ? -titleSlideOffset : titleSlideOffset;

            if (!animate || titleSlideDuration <= 0f)
            {
                RefreshTitleObject();
                return;
            }

            if (titleExiting)
                return;

            titleTween.Kill();
            titleExiting = true;
            titleStartPosition = titleObject.rectTransform.anchoredPosition;
            titleTargetPosition = titleOrigin - titleOffset;
            titleStartAlpha = titleCanvasGroup.alpha;
            titleTargetAlpha = 0f;
            titleTween = EvoTween.Create(0f, 1f, titleSlideDuration * 0.5f)
                .WithTimeMode(AnimationTimeMode).OnComplete(completeTitleOut).Bind(updateTitle);
        }

        void CompleteTitleOut()
        {
            titleTween = default;
            titleExiting = false;

            if (titleObject == null || titleCanvasGroup == null || !isActiveAndEnabled)
                return;

            SetTitleText();
            titleStartPosition = titleOrigin + titleOffset;
            titleTargetPosition = titleOrigin;
            titleStartAlpha = 0f;
            titleTargetAlpha = 1f;
            titleTween = EvoTween.Create(0f, 1f, titleSlideDuration * 0.5f)
                .WithDelay(titleChangeDelay).WithTimeMode(AnimationTimeMode).Bind(updateTitle);
        }

        void UpdateTitle(float progress)
        {
            if (titleObject == null || titleCanvasGroup == null)
                return;

            float positionProgress = titleExiting && titleSlideCurve != null ? titleSlideCurve.Evaluate(progress) : progress;
            titleObject.rectTransform.anchoredPosition = Vector2.LerpUnclamped(titleStartPosition, titleTargetPosition, positionProgress);
            titleCanvasGroup.alpha = Mathf.Lerp(titleStartAlpha, titleTargetAlpha, progress);
        }

        void SetTitleText()
        {
            Tab tab = CurrentTab;
            TextProvider.SetText(titleObject, tab?.tabButton != null && tab.tabButton.textObject != null ? tab.tabButton.Text : tab?.tabID ?? string.Empty);
        }
        #endregion

        #region Indicator Animation
        void AnimateIndicator(bool animate)
        {
            indicatorTween.Kill();
            indicatorTween = default;

            if (indicatorObject == null)
                return;

            RectTransform targetRect = CurrentTab?.tabButton != null ? CurrentTab.tabButton.transform as RectTransform : null;
            indicatorStartPosition = indicatorObject.anchoredPosition;
            indicatorStartSize = indicatorObject.sizeDelta;
            indicatorTargetPosition = indicatorStartPosition;
            indicatorTargetSize = indicatorStartSize;
            indicatorHasTarget = targetRect != null;
            int axis = indicatorDirection == IndicatorDirection.Horizontal ? 0 : 1;

            if (targetRect == null) { indicatorTargetSize[axis] -= indicatorObject.rect.size[axis]; }
            else
            {
                // A layout group may not have placed the buttons yet (first frame of a new instance)
                if (targetRect.parent is RectTransform buttonParent && buttonParent.TryGetComponent(out LayoutGroup _))
                    LayoutRebuilder.ForceRebuildLayoutImmediate(buttonParent);

                // Measure in the indicator's parent space, including stretched anchors and nested buttons
                targetRect.GetWorldCorners(indicatorCorners);
                Transform parent = indicatorObject.parent;
                Vector3 min = parent != null ? parent.InverseTransformPoint(indicatorCorners[0]) : indicatorCorners[0];
                Vector3 max = min;
                for (int i = 1; i < indicatorCorners.Length; i++)
                {
                    Vector3 corner = parent != null ? parent.InverseTransformPoint(indicatorCorners[i]) : indicatorCorners[i];
                    min = Vector3.Min(min, corner);
                    max = Vector3.Max(max, corner);
                }

                float size = max[axis] - min[axis];
                float pivotPosition = min[axis] + size * indicatorObject.pivot[axis];
                indicatorTargetPosition[axis] += pivotPosition - indicatorObject.localPosition[axis];

                if (indicatorAutoSize)
                    indicatorTargetSize[axis] += size - indicatorObject.rect.size[axis];
            }

            if (!animate || indicatorDuration <= 0f)
            {
                UpdateIndicator(1f);
                return;
            }

            indicatorTween = EvoTween.Create(0f, 1f, indicatorDuration).WithTimeMode(AnimationTimeMode).Bind(updateIndicator);
        }

        void UpdateIndicator(float progress)
        {
            if (indicatorObject == null)
                return;

            float value = progress >= 1f ? 1f : indicatorCurve != null ? indicatorCurve.Evaluate(progress) : progress;
            float stretch = indicatorHasTarget && progress < 1f ? Mathf.Sin(Mathf.Clamp01(value) * Mathf.PI) * indicatorStretch : 0f;
            int axis = indicatorDirection == IndicatorDirection.Horizontal ? 0 : 1;
            Vector2 position = indicatorObject.anchoredPosition;
            Vector2 size = indicatorObject.sizeDelta;
            position[axis] = Mathf.LerpUnclamped(indicatorStartPosition[axis], indicatorTargetPosition[axis], value);
            size[axis] = Mathf.LerpUnclamped(indicatorStartSize[axis], indicatorTargetSize[axis], value) + stretch;
            indicatorObject.anchoredPosition = position;
            indicatorObject.sizeDelta = size;
        }
        #endregion

        #region Public API
        public void OpenTab(int tabIndex, bool animate = true)
        {
            if (!IsValidTab(tabIndex))
                return;

            if (!isInitialized)
            {
                currentTabIndex = tabIndex;
                return;
            }

            bool changed = currentTabIndex != tabIndex;
            if (!changed && animate && (!isExiting || tabTween.IsActive))
                return;

            int previousIndex = currentTabIndex;
            StoreSelection();
            currentTabIndex = tabIndex;
            reverseAnimation = animateDirectionally && tabIndex < previousIndex;

            if (!animate || !isActiveAndEnabled) { ApplySelection(); }
            else
            {
                UpdateButtonStates();
                AnimateTitle(true, previousIndex);
                AnimateIndicator(true);
                BeginOut();
            }

            // Publish the new index before invoking user code, including reentrant OpenTab calls
            if (changed)
                onTabChanged?.Invoke(currentTabIndex);
        }

        public void OpenTab(string tabID, bool animate = true) => OpenTab(FindTab(tabID), animate);

        public void OpenTab(string tabID) => OpenTab(tabID, true);

        public void OpenTab(int tabIndex) => OpenTab(tabIndex, true);

        public void OpenNextTab()
        {
            for (int i = currentTabIndex + 1; i < tabs.Count; i++)
            {
                if (IsValidTab(i))
                {
                    OpenTab(i);
                    return;
                }
            }
        }

        public void OpenPreviousTab()
        {
            for (int i = currentTabIndex - 1; i >= 0; i--)
            {
                if (IsValidTab(i))
                {
                    OpenTab(i);
                    return;
                }
            }
        }

        public void OpenFirstTab() => OpenTab(FindValidTab());

        public void OpenLastTab()
        {
            for (int i = tabs.Count - 1; i >= 0; i--)
            {
                if (IsValidTab(i))
                {
                    OpenTab(i);
                    return;
                }
            }
        }

        public void AddTab(Tab newTab)
        {
            if (newTab?.tabObject == null)
                return;

            for (int i = 0; i < tabs.Count; i++)
            {
                if (tabs[i] == newTab || tabs[i]?.tabObject == newTab.tabObject)
                    return;
            }

            tabs.Add(newTab);

            if (!isInitialized)
                return;

            newTab.Bind(this);
            newTab.SetVisibility(false, disableInvisibleTabs);

            if (CurrentTab == null)
                OpenTab(tabs.Count - 1, false);
            else
                UpdateButtonStates();
        }

        public void RemoveTab(int index)
        {
            if (index < 0 || index >= tabs.Count)
                return;

            Tab selected = CurrentTab;
            Tab removed = tabs[index];

            if (isInitialized)
            {
                StoreSelection();
                StopAnimations();
                removed?.Unbind();
                removed?.SetVisibility(false, disableInvisibleTabs);
                removed?.RestoreOriginalPosition();
            }

            tabs.RemoveAt(index);
            currentTabIndex = selected != null && selected != removed ? tabs.IndexOf(selected) : FindValidTab();

            if (!isInitialized)
                return;

            ApplySelection();

            if (selected == removed)
                onTabChanged?.Invoke(currentTabIndex);
        }

        public void RemoveTab(string tabID) => RemoveTab(FindTab(tabID));

        /// <summary>
        /// Immediately refreshes the indicator using the current button position and size.
        /// Call after layout changes when the indicator needs to be realigned.
        /// </summary>
        public void RefreshIndicator() => AnimateIndicator(false);

        /// <summary>
        /// Refreshes the title and restores its resting position (e.g., after changing language).
        /// </summary>
        public void RefreshTitleObject()
        {
            titleTween.Kill();
            titleTween = default;
            titleExiting = false;

            if (titleObject == null)
                return;

            SetTitleText();

            if (!isInitialized)
                return;

            titleObject.rectTransform.anchoredPosition = titleOrigin;

            if (titleCanvasGroup != null)
                titleCanvasGroup.alpha = 1f;
        }
        #endregion

#if UNITY_EDITOR
        bool isEditingTabs;
        int editedTabIndex;
        RectTransform editedTabObject;

        public void BeginTabsEdit()
        {
            if (!Application.isPlaying || !isInitialized || isEditingTabs)
                return;

            isEditingTabs = true;
            editedTabIndex = currentTabIndex;
            editedTabObject = CurrentTab?.tabObject;
            StopAnimations();

            for (int i = 0; i < tabs.Count; i++)
            {
                Tab tab = tabs[i];
                if (tab == null)
                    continue;

                tab.RestoreOriginalPosition();
                tab.SetVisibility(false, disableInvisibleTabs);
                tab.Unbind();
                tab.ResetCache();
            }

            visibleTab = null;
        }

        public void EndTabsEdit()
        {
            if (!isEditingTabs)
                return;

            isEditingTabs = false;

            for (int i = 0; i < tabs.Count; i++)
            {
                if (tabs[i]?.tabObject != null)
                    tabs[i].Bind(this);
            }

            if (!IsValidTab(currentTabIndex))
                currentTabIndex = FindValidTab();

            bool selectionChanged = currentTabIndex != editedTabIndex || CurrentTab?.tabObject != editedTabObject;
            editedTabObject = null;
            ApplySelection();

            if (selectionChanged)
                onTabChanged?.Invoke(currentTabIndex);
        }

        void OnValidate()
        {
            if (Application.isPlaying)
                return;

            currentTabIndex = tabs.Count > 0 ? Mathf.Clamp(currentTabIndex, 0, tabs.Count - 1) : -1;
            UnityEditor.EditorApplication.delayCall -= UpdateEditorPreview;
            UnityEditor.EditorApplication.delayCall += UpdateEditorPreview;
        }

        void UpdateEditorPreview()
        {
            if (this == null || Application.isPlaying || UnityEditor.EditorUtility.IsPersistent(this))
                return;

            for (int i = 0; i < tabs.Count; i++)
                tabs[i]?.SetVisibility(i == currentTabIndex, disableInvisibleTabs);

            UpdateButtonStates();
            RefreshTitleObject();
            RefreshIndicator();
        }
#endif

        [Serializable]
        [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Evo.UI", sourceAssembly: "Evo.UI", sourceClassName: "Item")]
        public class Tab
        {
            [Tooltip("Identifier used to open or remove this tab by name. Use a unique ID for each tab.")]
            public string tabID;
            [Tooltip("Root RectTransform of the tab content. A CanvasGroup is added automatically if needed.")]
            public RectTransform tabObject;
            [Tooltip("Optional button that opens this tab and reflects its selected state.")]
            public Button tabButton;
            [Tooltip("UI object selected after this tab opens when no active previous selection is available.")]
            public GameObject firstSelected;

            // Cache
            [NonSerialized] public GameObject latestSelected;
            [NonSerialized] public CanvasGroup canvasGroup;
            [NonSerialized] public Vector2 originalAnchoredPosition;
            [NonSerialized] Vector3 originalScale;
            [NonSerialized] Quaternion originalRotation;
            [NonSerialized] TabManager owner;
            [NonSerialized] Button boundButton;
            [NonSerialized] UnityAction clickAction;
            [NonSerialized] bool initialized;

#if UNITY_EDITOR
            internal void ResetCache()
            {
                initialized = false;
                canvasGroup = null;
                latestSelected = null;
            }
#endif

            internal void Bind(TabManager manager)
            {
                Unbind();
                Initialize();
                owner = manager;
                boundButton = tabButton;
                clickAction ??= OnClick;

                if (boundButton != null)
                    boundButton.onClick.AddListener(clickAction);
            }

            internal void Unbind()
            {
                if (boundButton != null && clickAction != null)
                    boundButton.onClick.RemoveListener(clickAction);

                boundButton = null;
                owner = null;
            }

            void OnClick()
            {
                if (owner != null && owner.isActiveAndEnabled)
                    owner.OpenTab(owner.tabs.IndexOf(this));
            }

            public CanvasGroup GetCanvasGroup()
            {
                if (canvasGroup == null && tabObject != null && !tabObject.TryGetComponent(out canvasGroup))
                    canvasGroup = tabObject.gameObject.AddComponent<CanvasGroup>();

                return canvasGroup;
            }

            public void Initialize()
            {
                if (initialized || tabObject == null)
                    return;

                GetCanvasGroup();
                StoreOriginalPositionData();
                initialized = true;
            }

            public void StoreOriginalPositionData()
            {
                if (tabObject == null)
                    return;

                originalAnchoredPosition = tabObject.anchoredPosition;
                originalScale = tabObject.localScale;
                originalRotation = tabObject.localRotation;
            }

            public void RestoreOriginalPosition()
            {
                if (!initialized || tabObject == null)
                    return;

                tabObject.anchoredPosition = originalAnchoredPosition;
                tabObject.localScale = originalScale;
                tabObject.localRotation = originalRotation;
            }

            internal void SetInteraction(bool interactable)
            {
                CanvasGroup group = GetCanvasGroup();
                if (group == null)
                    return;

                group.interactable = interactable;
                group.blocksRaycasts = interactable;
            }

            public void SetVisibility(bool visible, bool respectDisableInvisibleTabs = true)
            {
                CanvasGroup group = GetCanvasGroup();
                if (group == null)
                    return;

                group.alpha = visible ? 1f : 0f;
                SetInteraction(visible);
                bool active = visible || !respectDisableInvisibleTabs;

                if (tabObject.gameObject.activeSelf != active)
                    tabObject.gameObject.SetActive(active);
            }
        }
    }
}