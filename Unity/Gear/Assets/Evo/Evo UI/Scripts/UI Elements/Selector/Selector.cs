using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Evo.Tween;

namespace Evo.UI
{
    /// <summary>
    /// Displays one item at a time with animated navigation and selection indicators.
    /// </summary>
    [DisallowMultipleComponent, ExecuteAlways]
    [AddComponentMenu("Evo/UI/UI Elements/Selector"), HelpURL(Constants.HelpUrl + "ui-elements/selector")]
    public class Selector : MonoBehaviour
    {
        // Item List
        [SerializeField] private int selectedIndex = 0;
        public List<Item> items = new();

        // Settings
        [Tooltip("Wraps from the last item to the first, and from the first item to the last.")]
        public bool loop = false;

        // Animation
        [Tooltip("Slides content horizontally when enabled, or vertically when disabled.")]
        public bool isHorizontal = true;
        [Tooltip("Reverses the slide direction for both next and previous selections.")]
        public bool invertAnimationDirection = false;
        [Tooltip("Distance the content moves from its resting position, in UI units.")]
        public float slideOffset = 25f;
        [Range(0f, 1f)] public float animationDuration = 0.25f;
        [Tooltip("Eases the content slide and the indicator transitions. Custom uses the animation curve.")]
        public Ease animationEase = Ease.Custom;
        public AnimationCurve animationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        // Indicator
        [Range(0f, 1f)] public float selectedIndicatorAlpha = 1f;
        [Range(0f, 1f)] public float unselectedIndicatorAlpha = 0.5f;
        [Tooltip("Selected indicator size as a multiple of the indicator prefab's width and height. Axes sized by the indicator parent's layout group are left to the layout.")]
        public Vector2 selectedIndicatorSize = Vector2.one;
        [Range(0f, 1f), UnityEngine.Serialization.FormerlySerializedAs("indicatorFadeDuration")]
        public float indicatorDuration = 0.15f;

#if EVO_LOCALIZATION
        // Localization
        [Tooltip("Resolves item labels from their table keys on enable and when the language changes.")]
        public bool enableLocalization = true;
        [Tooltip("Localization source used to resolve item table keys. Resolved automatically when localization is enabled.")]
        public Localization.LocalizedObject localizedObject;
#endif

        // References
        [Tooltip("Content moved during selection animations.")]
        public RectTransform contentParent;
        [TextComponent] public Graphic textObject;
        [Tooltip("Displays the selected item's icon. Its GameObject is hidden when the item has no icon.")]
        public Image iconObject;
        [Tooltip("Container for generated indicators. Existing direct children with a CanvasGroup are adopted on the first refresh.")]
        public RectTransform indicatorParent;
        [Tooltip("Template for each indicator. A CanvasGroup is added to generated instances if missing.")]
        public GameObject indicatorPrefab;
        public Button prevButton;
        public Button nextButton;

        // Events
        public UnityEvent<int> onSelectionChanged = new();

        // Serialized Ownership
        [SerializeField, HideInInspector] private List<GameObject> indicatorObjects = new();
        [SerializeField, HideInInspector] private GameObject generatedIndicatorPrefab;

        // Runtime State
        bool isAnimatingOut;
        CanvasGroup contentCanvasGroup;
        Vector2 originalContentPosition;
        Vector2 animationOffset;
        TweenHandle currentAnimation;
        TweenHandle indicatorAnimation;
        Action<float> animateContent;
        Action<float> animateIndicators;
        Action completeAnimation;
        readonly List<IndicatorTransition> indicatorTransitions = new();

        void OnEnable()
        {
            if (!Application.IsPlaying(gameObject))
            {
#if UNITY_EDITOR
                // Unity skips OnValidate for objects created in the editor, such as menu copies
                OnValidate();
#endif
                return;
            }

            Refresh();

#if EVO_LOCALIZATION
            Localization.LocalizationManager.Subscribe(this, enableLocalization, ref localizedObject, UpdateLocalization);
#endif

            if (prevButton != null)
                prevButton.onClick.AddListener(SelectPrevious);
            if (nextButton != null)
                nextButton.onClick.AddListener(SelectNext);
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= RefreshDelayed;
            refreshPending = false;
#endif

            if (prevButton != null)
                prevButton.onClick.RemoveListener(SelectPrevious);
            if (nextButton != null)
                nextButton.onClick.RemoveListener(SelectNext);

            StopAnimation();

            if (Application.IsPlaying(gameObject))
            {
                UpdateDisplay();
                UpdateIndicators(false);
            }

#if EVO_LOCALIZATION
            Localization.LocalizationManager.Unsubscribe(UpdateLocalization);
#endif
        }

        #region Types
        /// <summary>
        /// Defines an item's display content and selection event.
        /// </summary>
        [Serializable]
        public class Item
        {
            public string label;
            public Sprite icon;
            public UnityEvent onItemSelection = new();

#if EVO_LOCALIZATION
            [Tooltip("Localization key for this item's label. Leave empty to use the label as entered.")]
            public string tableKey;
#endif

            /// <summary>
            /// Creates an item with a label and an optional icon.
            /// </summary>
            public Item(string label, Sprite icon = null)
            {
                this.label = label;
                this.icon = icon;
            }
        }

        struct IndicatorTransition
        {
            public CanvasGroup group;
            public RectTransform rect;
            public float fromAlpha;
            public float toAlpha;
            public Vector2 fromSize;
            public Vector2 toSize;

            // Writes exact targets at 1, so a finished transition matches a later comparison
            public readonly void Apply(float t)
            {
                if (group == null)
                    return;

                group.alpha = t == 1f ? toAlpha : Mathf.Clamp01(Mathf.LerpUnclamped(fromAlpha, toAlpha, t));
                if (rect != null)
                    rect.sizeDelta = t == 1f ? toSize : Vector2.Max(Vector2.zero, Vector2.LerpUnclamped(fromSize, toSize, t));
            }
        }
        #endregion

        #region Content
        bool UpdateDisplay()
        {
            Item item = items != null && selectedIndex >= 0 && selectedIndex < items.Count ? items[selectedIndex] : null;
            string label = item?.label ?? string.Empty;

#if EVO_LOCALIZATION
            if (Application.IsPlaying(gameObject) && enableLocalization && localizedObject != null && item != null)
                label = localizedObject.GetString(item.tableKey) ?? label;
#endif

            bool changed = false;
            if (TextProvider.UpdateText(textObject, label))
            {
                changed = true;
#if UNITY_EDITOR
                SetEditorDirty(textObject);
#endif
            }

            if (iconObject != null)
            {
                Sprite icon = item?.icon;
                if (iconObject.sprite != icon)
                {
                    iconObject.sprite = icon;
                    changed = true;
#if UNITY_EDITOR
                    SetEditorDirty(iconObject);
#endif
                }

                bool hasIcon = icon != null;
                if (iconObject.gameObject.activeSelf != hasIcon)
                {
                    iconObject.gameObject.SetActive(hasIcon);
                    changed = true;
#if UNITY_EDITOR
                    SetEditorDirty(iconObject.gameObject);
#endif
                }
            }

            return changed;
        }

        bool UpdateButtons()
        {
            bool canSelect = items != null && items.Count > 1;
            bool changed = SetButtonInteractable(prevButton, canSelect && (loop || selectedIndex > 0));
            changed |= SetButtonInteractable(nextButton, canSelect && (loop || selectedIndex < items.Count - 1));
            return changed;
        }

        static bool SetButtonInteractable(Button button, bool interactable)
        {
            if (button == null || button.interactable == interactable)
                return false;

            if (Application.IsPlaying(button.gameObject))
                button.SetInteractable(interactable);
            else
                button.interactable = interactable;

#if UNITY_EDITOR
            SetEditorDirty(button);
#endif
            return true;
        }
        #endregion

        #region Indicators
        bool RefreshIndicators()
        {
            bool changed = false;

            if (generatedIndicatorPrefab == null && indicatorObjects.Count == 0 && indicatorParent != null && indicatorPrefab != null)
            {
                for (int i = 0; i < indicatorParent.childCount; i++)
                {
                    GameObject child = indicatorParent.GetChild(i).gameObject;
                    if (child != indicatorPrefab && child.TryGetComponent<CanvasGroup>(out _))
                        indicatorObjects.Add(child);
                }
                
                changed = indicatorObjects.Count > 0;
            }

            int count = indicatorParent != null && indicatorPrefab != null ? items.Count : 0;
            bool prefabChanged = generatedIndicatorPrefab != null && generatedIndicatorPrefab != indicatorPrefab;
            changed |= generatedIndicatorPrefab != indicatorPrefab || indicatorObjects.Count != count;
            generatedIndicatorPrefab = indicatorPrefab;
            string linkPath = count > 0 ? GeneratedObjects.GetLinkPath(this, indicatorPrefab) : null;

            for (int i = indicatorObjects.Count - 1; i >= 0; i--)
            {
                if (!prefabChanged && i < count && !GeneratedObjects.NeedsRelink(indicatorObjects[i], indicatorPrefab, linkPath))
                    continue;

                DestroyIndicator(indicatorObjects[i]);
                indicatorObjects[i] = null;
            }

            if (indicatorObjects.Count > count)
                indicatorObjects.RemoveRange(count, indicatorObjects.Count - count);
            while (indicatorObjects.Count < count)
                indicatorObjects.Add(null);

            if (linkPath != null)
                changed |= GeneratedObjects.ClaimOrphans(indicatorObjects, count, indicatorParent, indicatorPrefab, linkPath, DestroyIndicator);

            for (int i = 0; i < count; i++)
            {
                GameObject instance = indicatorObjects[i];
                if (instance == null)
                {
                    indicatorObjects[i] = instance = GeneratedObjects.Create(indicatorPrefab, indicatorParent, linkPath);
                    changed = true;
                }
                else if (instance.transform.parent != indicatorParent)
                {
                    instance.transform.SetParent(indicatorParent, false);
                    changed = true;
#if UNITY_EDITOR
                    SetEditorDirty(instance.transform);
#endif
                }

                if (instance.transform.GetSiblingIndex() != i)
                {
                    instance.transform.SetSiblingIndex(i);
                    changed = true;
#if UNITY_EDITOR
                    SetEditorDirty(instance.transform);
#endif
                }

                if (!instance.activeSelf)
                {
                    instance.SetActive(true);
                    changed = true;
#if UNITY_EDITOR
                    SetEditorDirty(instance);
#endif
                }

                if (!instance.TryGetComponent<CanvasGroup>(out _))
                {
                    instance.AddComponent<CanvasGroup>();
                    changed = true;
                }
            }

            return changed;
        }

        static void DestroyIndicator(GameObject instance)
        {
            if (instance == null)
                return;

            if (!Application.isPlaying) { DestroyImmediate(instance); }
            else
            {
                instance.SetActive(false);
                Destroy(instance);
            }
        }

        bool UpdateIndicators(bool animate = true)
        {
            bool changed = false;
            animate &= Application.IsPlaying(gameObject) && isActiveAndEnabled && indicatorDuration > 0f;
            indicatorAnimation.Kill();
            indicatorAnimation = default;
            indicatorTransitions.Clear();

            // Sizes come from the prefab, so resizing the prefab keeps the multiplier valid
            Vector2 baseSize = default;
            bool resizeWidth = false, resizeHeight = false;
            if (indicatorPrefab != null && indicatorPrefab.transform is RectTransform prefabRect)
            {
                baseSize = prefabRect.sizeDelta;
                resizeWidth = resizeHeight = true;

                // Axes a layout group sizes stay with the layout instead of fighting it
                if (indicatorParent != null && indicatorParent.TryGetComponent<LayoutGroup>(out var layout) && layout.enabled)
                {
                    var linearLayout = layout as HorizontalOrVerticalLayoutGroup;
                    resizeWidth = linearLayout != null && !linearLayout.childControlWidth;
                    resizeHeight = linearLayout != null && !linearLayout.childControlHeight;
                }
            }

            for (int i = 0; i < indicatorObjects.Count; i++)
            {
                GameObject indicator = indicatorObjects[i];
                if (indicator == null || !indicator.TryGetComponent<CanvasGroup>(out var group))
                    continue;

                bool selected = i == selectedIndex;
                var transition = new IndicatorTransition
                {
                    group = group,
                    fromAlpha = group.alpha,
                    toAlpha = selected ? selectedIndicatorAlpha : unselectedIndicatorAlpha
                };

                if ((resizeWidth || resizeHeight) && indicator.transform is RectTransform rect)
                {
                    Vector2 size = rect.sizeDelta;
                    Vector2 targetSize = selected ? Vector2.Scale(baseSize, selectedIndicatorSize) : baseSize;
                    targetSize = new Vector2(resizeWidth ? targetSize.x : size.x, resizeHeight ? targetSize.y : size.y);

                    if (size != targetSize)
                    {
                        transition.rect = rect;
                        transition.fromSize = size;
                        transition.toSize = targetSize;
                    }
                }

                bool alphaChanged = transition.fromAlpha != transition.toAlpha;
                if (!alphaChanged && transition.rect == null)
                    continue;

                if (animate) { indicatorTransitions.Add(transition); }
                else
                {
                    transition.Apply(1f);
#if UNITY_EDITOR
                    if (alphaChanged)
                        SetEditorDirty(group);
                    SetEditorDirty(transition.rect);
#endif
                }
                changed = true;
            }

            if (indicatorTransitions.Count > 0)
            {
                animateIndicators ??= AnimateIndicators;
                indicatorAnimation = EvoTween.Create(0f, 1f, indicatorDuration)
                    .WithTimeMode(TimeMode.Unscaled).Bind(animateIndicators);
            }

            return changed;
        }

        void AnimateIndicators(float progress)
        {
            float t = progress < 1f ? EvaluateEase(progress) : 1f;

            for (int i = 0; i < indicatorTransitions.Count; i++)
                indicatorTransitions[i].Apply(t);
        }
        #endregion

        #region Animation
        void StartSlideAnimation(int direction)
        {
            StopAnimation();
            originalContentPosition = contentParent.anchoredPosition;
          
            if (invertAnimationDirection)
                direction = -direction;

            animationOffset = (isHorizontal ? Vector2.left : Vector2.up) * (slideOffset * direction);
            isAnimatingOut = true;
            animateContent ??= AnimateContent;
            completeAnimation ??= CompleteAnimation;
            currentAnimation = EvoTween.Create(0f, 2f, animationDuration)
                .WithTimeMode(TimeMode.Unscaled).OnComplete(completeAnimation).Bind(animateContent);
        }

        void AnimateContent(float progress)
        {
            bool outgoing = progress < 1f;
            if (!outgoing && isAnimatingOut)
            {
                isAnimatingOut = false;
                UpdateDisplay();
            }

            // Position follows overshooting eases, alpha stays in range
            float t = EvaluateEase(outgoing ? progress : progress - 1f);
            if (contentParent != null)
                contentParent.anchoredPosition = originalContentPosition + animationOffset * (outgoing ? t : t - 1f);
            if (contentCanvasGroup != null)
                contentCanvasGroup.alpha = Mathf.Clamp01(outgoing ? 1f - t : t);
        }

        float EvaluateEase(float t) => TweenEase.Evaluate(animationEase, t, animationCurve);

        void CompleteAnimation()
        {
            if (contentParent != null)
                contentParent.anchoredPosition = originalContentPosition;
            if (contentCanvasGroup != null)
                contentCanvasGroup.alpha = 1f;

            currentAnimation = default;
            isAnimatingOut = false;
        }

        void StopAnimation()
        {
            if (!currentAnimation.IsActive)
                return;

            currentAnimation.Kill();
            CompleteAnimation();
        }
        #endregion

        #region Selection
        void SetSelection(int index, bool animated, int direction, bool invokeEvents)
        {
            if (items == null || items.Count == 0)
                return;

            index = Mathf.Clamp(index, 0, items.Count - 1);
            if (index == selectedIndex)
            {
                if (!animated)
                {
                    StopAnimation();
                    UpdateDisplay();
                    UpdateIndicators(false);
                    UpdateButtons();
                }
                return;
            }

            selectedIndex = index;
            bool animate = animated && Application.IsPlaying(gameObject) && isActiveAndEnabled;

            if (animate && animationDuration > 0f && contentParent != null && contentCanvasGroup != null)
                StartSlideAnimation(direction);
            else
            {
                StopAnimation();
                UpdateDisplay();
            }

            UpdateIndicators(animate);
            UpdateButtons();

            if (index < items.Count && invokeEvents)
            {
                onSelectionChanged?.Invoke(index);
                if (items[index] != null)
                    items[index]?.onItemSelection?.Invoke();
            }
        }
        #endregion

        #region Public API
        public int SelectedIndex
        {
            get => selectedIndex;
            set => SetSelection(value);
        }

        /// <summary>
        /// Updates the preview or runtime content, reusing valid indicators. Call after editing items or references.
        /// </summary>
        public void Refresh()
        {
#if UNITY_EDITOR
            if (refreshPending)
            {
                UnityEditor.EditorApplication.delayCall -= RefreshDelayed;
                refreshPending = false;
            }

            if (UnityEditor.EditorUtility.IsPersistent(this) || (Application.isPlaying && !Application.IsPlaying(gameObject)))
                return;
#endif
            bool runtime = Application.IsPlaying(gameObject);
            if (runtime)
                StopAnimation();

            bool changed = items == null;
            items ??= new List<Item>();
            int index = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, items.Count - 1));
            changed |= selectedIndex != index;
            selectedIndex = index;

            // The cache is empty after every load, so only adding the component counts as a change
            if (contentParent != null && contentCanvasGroup == null && !contentParent.TryGetComponent(out contentCanvasGroup))
            {
                contentCanvasGroup = contentParent.gameObject.AddComponent<CanvasGroup>();
                changed = true;
            }

            changed |= RefreshIndicators();
            changed |= UpdateDisplay();
            changed |= UpdateIndicators(false);
            changed |= UpdateButtons();

#if UNITY_EDITOR
            if (!runtime && changed)
            {
                SetEditorDirty(this);
                if (gameObject.scene.IsValid())
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif
        }

        /// <summary>
        /// Selects a clamped item index, optionally animating the change. Selection events run only when the index changes.
        /// </summary>
        public void SetSelection(int index, bool animated, bool invokeEvents = true)
        {
            SetSelection(index, animated, index > selectedIndex ? 1 : -1, invokeEvents);
        }

        /// <summary>
        /// Selects a clamped item index with animation.
        /// </summary>
        public void SetSelection(int index) => SetSelection(index, true, true);

        /// <summary>
        /// Selects the next item with animation, wrapping to the first item when looping is enabled.
        /// </summary>
        public void SelectNext()
        {
            if (items == null || items.Count <= 1 || (!loop && selectedIndex >= items.Count - 1))
                return;

            SetSelection(selectedIndex >= items.Count - 1 ? 0 : selectedIndex + 1, true, 1, true);
        }

        /// <summary>
        /// Selects the previous item with animation, wrapping to the last item when looping is enabled.
        /// </summary>
        public void SelectPrevious()
        {
            if (items == null || items.Count <= 1 || (!loop && selectedIndex <= 0))
                return;

            SetSelection(selectedIndex <= 0 ? items.Count - 1 : selectedIndex - 1, true, -1, true);
        }

        /// <summary>
        /// Adds a non-null item and refreshes the display and indicators without invoking selection events.
        /// </summary>
        public void AddItem(Item item)
        {
            if (item == null)
                return;

            items ??= new List<Item>();
            items.Add(item);
            Refresh();
        }

        /// <summary>
        /// Creates and adds an item with a label and an optional icon, then refreshes without invoking selection events.
        /// </summary>
        public void AddItem(string text, Sprite icon = null) => AddItem(new Item(text, icon));

        /// <summary>
        /// Adds the supplied non-null items and refreshes once without invoking selection events.
        /// </summary>
        public void AddItems(params Item[] newItems)
        {
            if (newItems == null || newItems.Length == 0)
                return;

            items ??= new List<Item>();
           
            for (int i = 0; i < newItems.Length; i++)
                if (newItems[i] != null) { items.Add(newItems[i]); }
          
            Refresh();
        }

        /// <summary>
        /// Creates items from the supplied labels and refreshes once without invoking selection events.
        /// </summary>
        public void AddItems(params string[] labels)
        {
            if (labels == null || labels.Length == 0)
                return;

            items ??= new List<Item>();
           
            for (int i = 0; i < labels.Length; i++)
                items.Add(new Item(labels[i]));
           
            Refresh();
        }

        /// <summary>
        /// Removes an item at a valid index and adjusts the selection without invoking selection events.
        /// </summary>
        public void RemoveItem(int index)
        {
            if (items == null || index < 0 || index >= items.Count)
                return;

            items.RemoveAt(index);

            if (index < selectedIndex)
                selectedIndex--;

            Refresh();
        }

        /// <summary>
        /// Removes all items and resets the display, indicators, and selection without invoking selection events.
        /// </summary>
        public void ClearItems()
        {
            items?.Clear();
            selectedIndex = 0;
            Refresh();
        }
        #endregion

#if EVO_LOCALIZATION
        void UpdateLocalization(Localization.LocalizationLanguage language = null)
        {
            if (!enableLocalization || localizedObject == null || items == null)
                return;

            StopAnimation();
            UpdateDisplay();
        }
#endif

#if UNITY_EDITOR
        bool refreshPending;

        static void SetEditorDirty(UnityEngine.Object target)
        {
            if (Application.isPlaying || target == null || UnityEditor.EditorUtility.IsPersistent(target))
                return;

            UnityEditor.EditorUtility.SetDirty(target);
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }

        void OnValidate()
        {
            slideOffset = Mathf.Max(0f, slideOffset);
            animationDuration = Mathf.Max(0f, animationDuration);
            selectedIndicatorSize = Vector2.Max(Vector2.zero, selectedIndicatorSize);
            indicatorDuration = Mathf.Max(0f, indicatorDuration);
            GeneratedObjects.Watch(this, OnValidate);

            if (refreshPending)
                return;

            refreshPending = true;
            UnityEditor.EditorApplication.delayCall += RefreshDelayed;
        }

        void RefreshDelayed()
        {
            refreshPending = false;
            
            if (this == null || UnityEditor.EditorApplication.isCompiling
                || (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode && !Application.isPlaying))
                return;

            Refresh();
        }
#endif
    }
}