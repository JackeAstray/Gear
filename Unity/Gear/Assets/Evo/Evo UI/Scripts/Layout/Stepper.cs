using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Evo.Tween;

namespace Evo.UI
{
    [DisallowMultipleComponent, ExecuteAlways, RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Evo/UI/Layout/Stepper"), HelpURL(Constants.HelpUrl)]
    public class Stepper : MonoBehaviour
    {
        // Content
        [SerializeField] private int currentIndex;
        public List<Item> items = new();

        // Layout
        public Orientation orientation = Orientation.Horizontal;
        public RectOffset padding = new();
        public RectOffset indicatorPadding = new();
        public RectOffset contentPadding = new();
        [Min(0f)] public float spacing = 40f;
        [Tooltip("X runs along the rail and Y across it, in both orientations. Indicators shrink to fit, so a large X splits the rail evenly.")]
        public Vector2 indicatorSize = new(56f, 56f);
        [Min(0f), Tooltip("Minimum gap between indicators and between their titles. Indicators shrink if the container is too small.")]
        public float indicatorSpacing = 24f;
        [Tooltip("Where titles sit along the rail: centered on their indicator, or from its start (left, or top in vertical steppers).")]
        public TitleAlignment titleAlignment = TitleAlignment.Center;
        [Min(0f), Tooltip("Gap between an indicator and its title.")]
        public float titleSpacing = 14f;
        [Min(0f), Tooltip("Longer titles are truncated. Horizontal titles also stay within the space between their neighbors.")]
        public float maxTitleWidth = 220f;
        [Min(0f)] public float navigationHeight = 60f;

        // Settings
        public bool allowIndicatorNavigation = true;
        public bool allowStepSkipping = true;
        [Tooltip("Allow returning to earlier steps through buttons, indicators, or the navigation API.")]
        public bool allowBackwardNavigation = true;
        public bool useUnscaledTime = false;
        [Tooltip("Fallback button text when the current step's Next Text is empty.")]
        public string nextText = "Continue";
        [Tooltip("Fallback button text on the final step when its Next Text is empty.")]
        public string completeText = "Complete";

        // Animation
        [Min(0f), Tooltip("Total transition duration. The current page exits first, then the next page enters.")]
        public float animationDuration = 0.3f;
        [Min(0f)] public float slideDistance = 40f;
        public Ease animationEase = Ease.OutCubic;
        public AnimationCurve animationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Min(0f), Tooltip("Indicators animate separately from the content, continuing from their current state.")]
        public float indicatorDuration = 0.3f;
        public Ease indicatorEase = Ease.OutCubic;
        public AnimationCurve indicatorCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        // References
        [Tooltip("Direct child of this Stepper. Created automatically when unassigned.")]
        public RectTransform indicatorParent;
        [Tooltip("Direct child of this Stepper. Add arbitrary UI below each step's Content container.")]
        public RectTransform contentParent;
        public StepperIndicator indicatorPreset;
        [Tooltip("Optional direct child of this Stepper, laid out as a footer.")]
        public RectTransform navigationParent;
        public Interactive backButton;
        public Interactive nextButton;
        [Tooltip("Optional child of Content Parent, shown after completing the last step.")]
        public CanvasGroup completionContent;

        // Events
        public UnityEvent<int> onStepChanged = new();
        public UnityEvent onCompleted = new();

        // Serialized Ownership
        [SerializeField, HideInInspector] List<StepperIndicator> indicatorObjects = new();
        [SerializeField, HideInInspector] List<CanvasGroup> generatedContent = new();
        [SerializeField, HideInInspector] StepperIndicator generatedIndicatorPreset;

        // Cache
        readonly HashSet<CanvasGroup> usedContent = new();
        readonly List<CanvasGroup> activeContent = new();
        readonly List<RectTransform> titleRects = new();
        readonly List<Vector2> titleSizes = new();
        readonly List<RectTransform> drivenRects = new();
        DrivenRectTransformTracker layoutTracker;
        RectTransform rectTransform;
        Interactive boundBackButton;
        Interactive boundNextButton;
        UnityAction previousAction;
        UnityAction nextAction;
        TweenHandle transitionTween;
        TweenHandle indicatorTween;
        Action<float> animateTransition;
        Action<float> animateIndicators;
        Action completeTransition;
        CanvasGroup outgoingContent;
        CanvasGroup incomingContent;
        Vector2 transitionOffset;

        // State
        bool isInitialized;
        bool isRefreshing;
        bool isCompleted;
        bool drivenChanged;
        int drivenCount;
        float transitionProgress = 1f;

        // Constants
        const DrivenTransformProperties LayoutProperties = DrivenTransformProperties.Anchors
            | DrivenTransformProperties.Pivot | DrivenTransformProperties.AnchoredPosition
            | DrivenTransformProperties.SizeDelta;

        // Properties
        public int CurrentIndex => currentIndex;
        public Item CurrentItem => currentIndex >= 0 && currentIndex < items.Count ? items[currentIndex] : null;
        public bool IsCompleted => isCompleted;
        public bool IsTransitioning => transitionTween.IsActive;
        public bool CanContinue => !isCompleted && CurrentItem != null && CurrentItem.canContinue;
        RectTransform RectTransform => rectTransform != null ? rectTransform : rectTransform = (RectTransform)transform;
        TimeMode TweenTimeMode => useUnscaledTime ? TimeMode.Unscaled : TimeMode.Scaled;

        void OnEnable()
        {
            if (Application.IsPlaying(gameObject))
                Refresh();
#if UNITY_EDITOR
            else
            {
                OnValidate();
                RequestLayoutBeforeRender();
            }
#endif
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            CancelEditorRefresh();
#endif
            StopAnimation();
            bool initialized = isInitialized;
            isInitialized = false;
            if (initialized)
                ApplySelection();

            ReleaseLayout();
            BindButtons(false);
         
            for (int i = 0; i < indicatorObjects.Count; i++)
            {
                if (indicatorObjects[i] != null)
                    indicatorObjects[i].Unbind();
            }
        }

        #region Setup
        public void Initialize() => Refresh();

        /// <summary>
        /// Reuses the preview hierarchy. Call after editing items or references from script.
        /// Removed content is hidden, never deleted. Reassign it to a step to reuse it.
        /// During an undo or redo, the refresh runs once Unity has finished applying it.
        /// </summary>
        public void Refresh() => Refresh(false);

        void Refresh(bool afterUndo)
        {
            if (isRefreshing || !isActiveAndEnabled)
                return;
#if UNITY_EDITOR
            // Unity can't record created objects while it applies an undo or redo
            if (UnityEditor.Undo.isProcessing)
            {
                RequestEditorRefresh(true);
                return;
            }

            CancelEditorRefresh();

            // Edit mode waits for a loaded scene. Play mode enables objects mid-load, so it refreshes then, like a build.
            if (UnityEditor.EditorUtility.IsPersistent(this) || (Application.isPlaying
                ? !Application.IsPlaying(gameObject) : !gameObject.scene.isLoaded))
                return;

            // Undo restores only what it recorded. Rebuilding missing objects would add an undo step
            // and clear the redo history, so an incomplete hierarchy waits for the next edit.
            if (afterUndo && !Application.isPlaying && !HasCompleteHierarchy())
                return;
#endif
            isRefreshing = true;
            try
            {
                StopAnimation();
                items ??= new List<Item>();
                bool runtime = Application.IsPlaying(gameObject);
                bool changed = false;
            
                if (indicatorParent == null)
                {
                    indicatorParent = CreateRect("Indicators", RectTransform);
                    changed = true;
                }

                if (contentParent == null)
                {
                    contentParent = CreateRect("Content", RectTransform);
                    changed = true;
                }

                // Never drive this component's root, another stepper, or an ancestor
                if (!ValidContainer(indicatorParent) || !ValidContainer(contentParent) || indicatorParent == contentParent)
                {
                    isInitialized = false;
                    ReleaseLayout();
                    return;
                }

                usedContent.Clear();

                for (int i = 0; i < items.Count; i++)
                {
                    items[i] ??= new Item();
                    Item item = items[i];
                    if (!ValidContent(item.content) || !usedContent.Add(item.content))
                    {
                        RectTransform content = CreateRect(item.title, contentParent);
                        item.content = content.gameObject.AddComponent<CanvasGroup>();
                        generatedContent.Add(item.content);
                        usedContent.Add(item.content);
                        changed = true;
                    }
                    if (item.content.transform.parent != contentParent)
                    {
                        item.content.transform.SetParent(contentParent, false);
                        changed = true;
                    }
                }

                for (int i = 0; i < activeContent.Count; i++)
                    HideIfUnused(activeContent[i]);

                activeContent.Clear();
              
                for (int i = 0; i < items.Count; i++)
                    activeContent.Add(items[i].content);

                // Keep removed pages, including all user-added children, available for recovery
                for (int i = generatedContent.Count - 1; i >= 0; i--)
                {
                    CanvasGroup content = generatedContent[i];
                    if (content == null)
                    {
                        generatedContent.RemoveAt(i);
                        changed = true;
                    }
                    else { HideIfUnused(content); }
                }

                changed |= RefreshIndicators();
                currentIndex = items.Count == 0 ? -1 : Mathf.Clamp(currentIndex, 0, items.Count - 1);

                if (items.Count == 0 || !runtime)
                    isCompleted = false;

                for (int i = 0; i < indicatorObjects.Count; i++)
                {
                    indicatorObjects[i].Bind(this, i, items[i].title, runtime);
                    indicatorObjects[i].SetOrientation(orientation == Orientation.Horizontal);
                }

                BindButtons(runtime);
                UpdateLayout();
                ApplySelection();
                isInitialized = runtime;
#if UNITY_EDITOR
                // Selection visuals aren't tracked by changed, so linked indicators record them on every edit-mode refresh
                if (!runtime)
                {
                    for (int i = 0; i < indicatorObjects.Count; i++)
                        indicatorObjects[i].RecordPrefabChanges();
                }

                if (!runtime && changed)
                {
                    UnityEditor.EditorUtility.SetDirty(this);
                    UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
                }
#endif
            }
            finally { isRefreshing = false; }
        }

        bool ValidContainer(RectTransform container) => container != null && container.parent == transform
            && !container.TryGetComponent<Stepper>(out _);

        // Content sits inside this Stepper (not in a nested one) and never contains the Stepper or its containers
        bool ValidContent(CanvasGroup content) => content != null && content != completionContent
            && content.transform is UnityEngine.RectTransform rect && rect != transform && rect.IsChildOf(transform)
            && !indicatorParent.IsChildOf(rect) && !contentParent.IsChildOf(rect) && content.GetComponentInParent<Stepper>() == this;

        void HideIfUnused(CanvasGroup content)
        {
            if (!usedContent.Contains(content))
                SetContent(content, false, 0f, Vector2.zero, false);
        }

        // Presets that aren't UI objects generate nothing
        int IndicatorCount => indicatorPreset != null && indicatorPreset.transform is UnityEngine.RectTransform ? items.Count : 0;

        bool RefreshIndicators()
        {
            int count = IndicatorCount;
            bool replace = generatedIndicatorPreset != indicatorPreset;
            bool changed = replace || indicatorObjects.Count != count;
            string linkPath = count > 0 ? GeneratedObjects.GetLinkPath(this, indicatorPreset) : null;

            for (int i = indicatorObjects.Count - 1; i >= 0; i--)
            {
                if (replace || i >= count)
                {
                    DestroyDecoration(indicatorObjects[i]);
                    indicatorObjects.RemoveAt(i);
                }
                else if (GeneratedObjects.NeedsRelink(indicatorObjects[i], indicatorPreset, linkPath))
                {
                    DestroyDecoration(indicatorObjects[i]);
                    indicatorObjects[i] = null;
                    changed = true;
                }
            }

            while (indicatorObjects.Count < count)
                indicatorObjects.Add(null);

            if (linkPath != null)
                changed |= GeneratedObjects.ClaimOrphans(indicatorObjects, count, indicatorParent, indicatorPreset, linkPath, DestroyDecoration);

            for (int i = 0; i < count; i++)
            {
                StepperIndicator indicator = indicatorObjects[i];
                if (indicator == null)
                {
                    indicator = indicatorObjects[i] = GeneratedObjects.Create(indicatorPreset, indicatorParent, linkPath);
                    RegisterCreated(indicator.gameObject);
                    changed = true;
                }
                else if (indicator.transform.parent != indicatorParent)
                {
                    indicator.transform.SetParent(indicatorParent, false);
                    changed = true;
                }

                if (!indicator.gameObject.activeSelf)
                    indicator.gameObject.SetActive(true);
            }

            generatedIndicatorPreset = indicatorPreset;
            return changed;
        }

        static RectTransform CreateRect(string title, Transform parent)
        {
            var rect = new GameObject(title, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.gameObject.layer = parent.gameObject.layer;
            RegisterCreated(rect.gameObject);
            return rect;
        }

        static void RegisterCreated(GameObject instance)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.Undo.RegisterCreatedObjectUndo(instance, "Create Stepper Content");
#endif
        }

        static void DestroyDecoration(Component instance)
        {
            if (instance == null)
                return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.Undo.DestroyObjectImmediate(instance.gameObject);
                return;
            }
#endif
            instance.gameObject.SetActive(false);
            Destroy(instance.gameObject);
        }
        #endregion

        #region Types
        public enum Orientation { Horizontal, Vertical }
        public enum TitleAlignment { Center, Start }

        [Serializable]
        public class Item
        {
            [Tooltip("Shown on the step's indicator when its preset has a title text.")]
            public string title = "Step";
            [Tooltip("Overrides the next button text for this step. Leave empty to use the Stepper's fallback text.")]
            public string nextText = string.Empty;
            [Tooltip("Content must belong to this Stepper. Its RectTransform is driven by the layout. " +
                "An empty container is created when unassigned; removed content is preserved.")]
            public CanvasGroup content;
            public bool canContinue = true;
            public UnityEvent onEnter = new();
        }
        #endregion

        #region Layout
        void OnRectTransformDimensionsChange()
        {
            if (!isRefreshing && contentParent != null && indicatorParent != null)
                UpdateLayout();
        }

        public void UpdateLayout()
        {
            if (!isActiveAndEnabled || !ValidContainer(indicatorParent) || !ValidContainer(contentParent))
            {
                ReleaseLayout();
                return;
            }

            drivenCount = 0;
            padding ??= new RectOffset();
            contentPadding ??= new RectOffset();
            indicatorPadding ??= new RectOffset();
            Vector2 size = RectTransform.rect.size;
            float width = Mathf.Max(0f, size.x - padding.horizontal);
            float height = Mathf.Max(0f, size.y - padding.vertical);
            bool horizontal = orientation == Orientation.Horizontal;
            float gap = Mathf.Max(0f, spacing);
            float markerBreadth = Mathf.Max(0f, indicatorSize.y);
            float titleGap = Mathf.Max(0f, titleSpacing);
            float titleExtent = MeasureTitles(horizontal);
            float titleBlock = titleExtent > 0f ? titleGap + titleExtent : 0f;
            float rail = markerBreadth + titleBlock + (horizontal ? indicatorPadding.vertical : indicatorPadding.horizontal);
            rail = Mathf.Clamp(rail, 0f, horizontal ? height : width);
            float footer = ValidContainer(navigationParent) ? Mathf.Min(Mathf.Max(0f, navigationHeight), height) : 0f;
            float contentX = padding.left + (horizontal ? 0f : rail + gap);
            float contentY = padding.top + (horizontal ? rail + gap : 0f);
            float contentWidth = Mathf.Max(0f, width - (horizontal ? 0f : rail + gap));
            float contentHeight = Mathf.Max(0f, height - (horizontal ? rail + gap : 0f) - (footer > 0f ? footer + gap : 0f));

            SetRect(indicatorParent, new Vector2(padding.left, padding.top), new Vector2(horizontal ? width : rail, horizontal ? rail : height));
            SetRect(contentParent, new Vector2(contentX, contentY), new Vector2(contentWidth, contentHeight));
         
            if (footer > 0f)
                SetRect(navigationParent, new Vector2(contentX, padding.top + height - footer), new Vector2(contentWidth, footer));

            int count = items.Count;
            Vector2 railSize = indicatorParent.rect.size;
            float available = Mathf.Max(0f, horizontal ? railSize.x - indicatorPadding.horizontal : railSize.y - indicatorPadding.vertical);
            float cross = Mathf.Max(0f, horizontal ? railSize.y - indicatorPadding.vertical : railSize.x - indicatorPadding.horizontal);
            float gapSize = count > 1 ? Mathf.Min(Mathf.Max(0f, indicatorSpacing), available / (count - 1)) : 0f;
            float length = count > 0 ? Mathf.Min(Mathf.Max(0f, indicatorSize.x), Mathf.Max(0f, available - gapSize * (count - 1)) / count) : 0f;
            float breadth = Mathf.Min(cross, markerBreadth);
            float stride = count > 1 ? (available - length) / (count - 1) : 0f;
            float start = count == 1 ? (available - length) * 0.5f : 0f;
            Vector2 origin = new(indicatorPadding.left, indicatorPadding.top);

            // Indicators and titles form one block, centered across the rail
            float titleOffset = breadth + titleGap;
            float titleRoom = titleExtent > 0f ? Mathf.Clamp(cross - titleOffset, 0f, titleExtent) : 0f;
            float markerOffset = Mathf.Max(0f, cross - (titleRoom > 0f ? titleOffset + titleRoom : breadth)) * 0.5f;

            for (int i = 0; i < indicatorObjects.Count; i++)
            {
                float along = start + stride * i;
                Vector2 position = horizontal ? new Vector2(along, markerOffset) : new Vector2(markerOffset, along);
                SetRect((RectTransform)indicatorObjects[i].transform, origin + position, horizontal ? new Vector2(length, breadth) : new Vector2(breadth, length));
                LayoutTitle(i, along, available, length, stride, titleOffset, titleRoom, horizontal);
            }

            for (int i = 0; i < count; i++)
                LayoutContent(items[i]?.content);

            LayoutContent(completionContent);
            TrackDrivenRects();

            if (IsTransitioning)
                AnimateTransition(transitionProgress);
        }

        // Returns the space titles need across the rail: their height below horizontal indicators, or width beside vertical ones
        float MeasureTitles(bool horizontal)
        {
            titleRects.Clear();
            titleSizes.Clear();
            float extent = 0f;
            float maxWidth = Mathf.Max(0f, maxTitleWidth);

            for (int i = 0; i < indicatorObjects.Count; i++)
            {
                RectTransform title = indicatorObjects[i] != null ? indicatorObjects[i].TitleRect : null;
                if (title != null && !title.gameObject.activeSelf)
                    title = null;

                Vector2 size = title != null
                    ? new Vector2(Mathf.Min(UnityEngine.UI.LayoutUtility.GetPreferredWidth(title), maxWidth), UnityEngine.UI.LayoutUtility.GetPreferredHeight(title))
                    : Vector2.zero;

                titleRects.Add(title);
                titleSizes.Add(size);
                extent = Mathf.Max(extent, horizontal ? size.y : size.x);
            }
            return extent;
        }

        // Titles center on (or start at) their indicator but never cross the midpoint to a neighbor, so edge titles align with the rail ends
        void LayoutTitle(int index, float along, float available, float length, float stride, float offset, float room, bool horizontal)
        {
            RectTransform title = titleRects[index];
            if (title == null)
                return;

            float center = along + length * 0.5f;
            float halfGap = Mathf.Max(0f, indicatorSpacing) * 0.5f;
            float slotStart = index == 0 ? 0f : center - stride * 0.5f + halfGap;
            float slotEnd = index == indicatorObjects.Count - 1 ? available : center + stride * 0.5f - halfGap;
            Vector2 preferred = titleSizes[index];
            float size = Mathf.Clamp(horizontal ? preferred.x : preferred.y, 0f, Mathf.Max(0f, slotEnd - slotStart));
            float start = titleAlignment == TitleAlignment.Start ? along : center - size * 0.5f;
            float position = Mathf.Clamp(start, slotStart, Mathf.Max(slotStart, slotEnd - size)) - along;

            if (horizontal) { SetRect(title, new Vector2(position, offset), new Vector2(size, room)); }
            else { SetRect(title, new Vector2(offset, position), new Vector2(Mathf.Min(preferred.x, room), size)); }
        }

        // Collects the rects this layout drives, in order. The tracker re-registers them only when that set changes.
        void Drive(RectTransform rect)
        {
            if (drivenCount == drivenRects.Count)
            {
                drivenRects.Add(rect);
                drivenChanged = true;
            }
            else if (!ReferenceEquals(drivenRects[drivenCount], rect))
            {
                drivenRects[drivenCount] = rect;
                drivenChanged = true;
            }
            drivenCount++;
        }

        void TrackDrivenRects()
        {
            if (drivenCount < drivenRects.Count)
            {
                drivenRects.RemoveRange(drivenCount, drivenRects.Count - drivenCount);
                drivenChanged = true;
            }

            if (!drivenChanged)
                return;

            drivenChanged = false;
            SetTracking(true);
        }

        void ReleaseLayout()
        {
            drivenRects.Clear();
            drivenChanged = false;
            SetTracking(false);
        }

        void SetTracking(bool drive)
        {
#if UNITY_EDITOR
            // Unity records the rects it starts or stops driving in the open undo group. Right after Prefab Mode opens,
            // that group still holds the selection, so the first layout after a load would dirty the stage. Layout reruns after undo.
            DrivenRectTransformTracker.StopRecordingUndo();
            try
            {
#endif
                layoutTracker.Clear();
                for (int i = 0; drive && i < drivenRects.Count; i++)
                    layoutTracker.Add(this, drivenRects[i], LayoutProperties);
#if UNITY_EDITOR
            }
            finally { DrivenRectTransformTracker.StartRecordingUndo(); }
#endif
        }

        void SetRect(RectTransform rect, Vector2 topLeft, Vector2 size)
            => SetRect(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(topLeft.x + size.x * 0.5f, -topLeft.y - size.y * 0.5f), size);

        // Stretches content placed in the container, with the padding as offsets
        void LayoutContent(CanvasGroup content)
        {
            if (content != null && content.transform is UnityEngine.RectTransform rect && rect.parent == contentParent)
                SetRect(rect, Vector2.zero, Vector2.one, ContentOrigin, new Vector2(-contentPadding.horizontal, -contentPadding.vertical));
        }

        void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            Drive(rect);
            if (rect.anchorMin != anchorMin)
                rect.anchorMin = anchorMin;
            if (rect.anchorMax != anchorMax)
                rect.anchorMax = anchorMax;
            if (rect.pivot != Vector2.one * 0.5f)
                rect.pivot = Vector2.one * 0.5f;
            if (rect.sizeDelta != size)
                rect.sizeDelta = size;
            if (rect.anchoredPosition != position)
                rect.anchoredPosition = position;
        }

        // Where stretched content sits at rest: offset by the difference between opposite paddings
        Vector2 ContentOrigin => new((contentPadding.left - contentPadding.right) * 0.5f, (contentPadding.bottom - contentPadding.top) * 0.5f);
        #endregion

        #region Navigation
        void BindButtons(bool bind)
        {
            if (bind)
            {
                previousAction ??= PreviousStep;
                nextAction ??= NextStep;
            }

            BindButton(ref boundBackButton, bind ? backButton : null, previousAction);
            BindButton(ref boundNextButton, bind ? nextButton : null, nextAction);
        }

        static void BindButton(ref Interactive boundButton, Interactive button, UnityAction action)
        {
            if (boundButton == button)
                return;
           
            if (boundButton != null)
                boundButton.onClick.RemoveListener(action);

            boundButton = button;
          
            if (boundButton != null)
                boundButton.onClick.AddListener(action);
        }

        // Transitions don't disable anything: input during one settles it first, so buttons never flash their disabled look
        void UpdateButtons()
        {
            bool available = !isCompleted && currentIndex >= 0;
            if (backButton != null)
                backButton.interactable = available && allowBackwardNavigation && currentIndex > 0;
           
            if (nextButton != null)
            {
                nextButton.interactable = available && CanContinue;
                if (nextButton is Button button)
                {
                    string text = !string.IsNullOrEmpty(CurrentItem?.nextText) ? CurrentItem.nextText
                        : currentIndex == items.Count - 1 ? completeText : nextText;
                    if (button.Text != text)
                        button.Text = text;
                }
            }

            int lastReachableIndex = available && allowIndicatorNavigation ? GetLastReachableIndex() : currentIndex;
            for (int i = 0; i < indicatorObjects.Count; i++)
            {
                Interactive button = indicatorObjects[i].button;
                if (button != null)
                    button.interactable = available && allowIndicatorNavigation && CanSelect(i, lastReachableIndex);
            }
           
            if (ValidContainer(navigationParent))
                navigationParent.gameObject.SetActive(!isCompleted && items.Count > 0);
        }

        bool CanSelect(int index, int lastReachableIndex)
        {
            if (index < 0 || index >= items.Count || index == currentIndex || isCompleted)
                return false;
            if (index < currentIndex)
                return allowBackwardNavigation;
           
            return index <= lastReachableIndex;
        }

        int GetLastReachableIndex()
        {
            int index = currentIndex;
            while (index >= 0 && index < items.Count - 1 && items[index] != null && items[index].canContinue)
            {
                index++;
                if (!allowStepSkipping)
                    break;
            }
            return index;
        }

        public void NextStep()
        {
            if (currentIndex == items.Count - 1) { Complete(); }
            else { GoToStep(currentIndex + 1); }
        }

        public void PreviousStep() => GoToStep(currentIndex - 1);

        public void GoToStep(int index) => TryGoToStep(index);

        /// <summary>
        /// Zero-based selection. Returns false when invalid or gated. A running transition finishes instantly first.
        /// </summary>
        public bool TryGoToStep(int index)
        {
            if (!isInitialized || !CanSelect(index, GetLastReachableIndex()))
                return false;

            FinishTransition();
            CanvasGroup previous = CurrentItem?.content;
            int direction = index > currentIndex ? 1 : -1;
            currentIndex = index;
            BeginTransition(previous, CurrentItem.content, direction);
            NotifyStepEntered();
            return true;
        }

        // A step changed listener may navigate again, so the entered step only gets its event if it's still current
        void NotifyStepEntered()
        {
            Item selected = CurrentItem;
            onStepChanged?.Invoke(currentIndex);

            if (isActiveAndEnabled && CurrentItem == selected && !isCompleted)
                selected.onEnter?.Invoke();
        }

        public void Complete()
        {
            if (!isInitialized || !CanContinue || currentIndex != items.Count - 1)
                return;

            FinishTransition();
            isCompleted = true;
            BeginTransition(CurrentItem.content, completionContent, 1);
            onCompleted?.Invoke();
        }

        public void Restart()
        {
            StopAnimation();
            isCompleted = false;
            currentIndex = 0;
            Refresh();

            if (isInitialized && CurrentItem != null)
                NotifyStepEntered();
        }

        public void SetCanContinue(bool value) => SetCanContinue(currentIndex, value);

        /// <summary>
        /// Gates leaving the given step. Steps after a gated one can't be reached through indicators either.
        /// </summary>
        public void SetCanContinue(int index, bool value)
        {
            Item item = index >= 0 && index < items.Count ? items[index] : null;
            if (item == null || item.canContinue == value)
                return;

            item.canContinue = value;
            UpdateButtons();
        }

        public CanvasGroup GetStepContent(int index) => index >= 0 && index < items.Count ? items[index]?.content : null;

        public StepperIndicator GetIndicator(int index) => index >= 0 && index < indicatorObjects.Count ? indicatorObjects[index] : null;
        #endregion

        #region Animation
        void PrepareIndicators()
        {
            for (int i = 0; i < indicatorObjects.Count; i++)
                indicatorObjects[i].SetTarget(isCompleted || i < currentIndex ? 2 : i == currentIndex ? 1 : 0);
        }

        void ApplySelection()
        {
            ApplyContent();
            PrepareIndicators();
            ApplyIndicators(1f);
        }

        void ApplyContent()
        {
            CanvasGroup selected = isCompleted ? completionContent : CurrentItem?.content;
            for (int i = 0; i < activeContent.Count; i++)
                SetContent(activeContent[i], activeContent[i] == selected, 1f, Vector2.zero, true);
           
            SetContent(completionContent, isCompleted, 1f, Vector2.zero, true);
            outgoingContent = incomingContent = null;
            UpdateButtons();
        }

        void BeginTransition(CanvasGroup previous, CanvasGroup next, int direction)
        {
            outgoingContent = previous;
            incomingContent = next;
            transitionOffset = (orientation == Orientation.Horizontal ? Vector2.right : Vector2.down) * (Mathf.Max(0f, slideDistance) * direction);
            AnimateIndicators();
           
            if (animationDuration <= 0f)
            {
                ApplyContent();
                return;
            }

            animateTransition ??= AnimateTransition;
            completeTransition ??= CompleteTransition;
            transitionTween = EvoTween.Create(0f, 1f, animationDuration)
                .WithEase(Ease.Linear)
                .WithTimeMode(TweenTimeMode)
                .OnComplete(completeTransition)
                .Bind(animateTransition);
            AnimateTransition(0f);
            UpdateButtons();
        }

        // Indicators start from their current blend, so a step change mid-animation doesn't snap them
        void AnimateIndicators()
        {
            indicatorTween.Kill();
            PrepareIndicators();

            if (indicatorDuration <= 0f)
            {
                ApplyIndicators(1f);
                return;
            }

            animateIndicators ??= ApplyIndicators;
            indicatorTween = EvoTween.Create(0f, 1f, indicatorDuration)
                .WithEase(indicatorEase, indicatorCurve)
                .WithTimeMode(TweenTimeMode)
                .Bind(animateIndicators);
        }

        void AnimateTransition(float value)
        {
            transitionProgress = Mathf.Clamp01(value);
            // Ease each half separately so the incoming page starts only after the outgoing page is hidden.
            // Offsets stay unclamped, so eases like Out Back overshoot the slide.
            bool entering = transitionProgress >= 0.5f;
            float pageProgress = TweenEase.Evaluate(animationEase, transitionProgress * 2f - (entering ? 1f : 0f), animationCurve);
            float exit = entering ? 1f : pageProgress;
            float enter = entering ? pageProgress : 0f;
            SetContent(outgoingContent, !entering, Mathf.Clamp01(1f - exit), -transitionOffset * exit, false);
            SetContent(incomingContent, entering, Mathf.Clamp01(enter), transitionOffset * (1f - enter), false);
        }

        void ApplyIndicators(float progress)
        {
            for (int i = 0; i < indicatorObjects.Count; i++)
                indicatorObjects[i].ApplyAnimation(progress);
        }

        void SetContent(CanvasGroup content, bool active, float alpha, Vector2 offset, bool interactable)
        {
            if (content == null || content.transform is not RectTransform rect || rect.parent != contentParent)
                return;
            if (content.gameObject.activeSelf != active)
                content.gameObject.SetActive(active);
            if (content.alpha != alpha)
                content.alpha = alpha;

            bool canInteract = active && interactable;
            if (content.interactable != canInteract)
                content.interactable = canInteract;
            if (content.blocksRaycasts != canInteract)
                content.blocksRaycasts = canInteract;

            Vector2 position = ContentOrigin + offset;
            if (rect.anchoredPosition != position)
                rect.anchoredPosition = position;
        }

        void CompleteTransition()
        {
            StopTransition();
            ApplyContent();
        }

        // Settles the content only. Indicators keep animating, and the next step change continues from their state.
        void FinishTransition()
        {
            if (IsTransitioning)
                CompleteTransition();
        }

        void StopTransition()
        {
            transitionTween.Kill();
            transitionTween = default;
            transitionProgress = 1f;
        }

        void StopAnimation()
        {
            StopTransition();
            indicatorTween.Kill();
            indicatorTween = default;
        }
        #endregion

#if UNITY_EDITOR
        bool refreshPending;
        bool refreshAfterUndo;
        bool renderLayoutPending;
        Canvas.WillRenderCanvases layoutBeforeRender;

        void Reset() => padding = new RectOffset(44, 44, 44, 40);

        // Unity also validates the components it restores during an undo or redo
        void OnValidate()
        {
            GeneratedObjects.Watch(this, OnValidate);
            RequestEditorRefresh(UnityEditor.Undo.isProcessing);
        }

        void RequestEditorRefresh(bool afterUndo)
        {
            refreshAfterUndo = afterUndo;
            if (refreshPending)
                return;
           
            refreshPending = true;
            UnityEditor.EditorApplication.delayCall += RefreshDelayed;
        }

        // Canvases can render before the delayed refresh runs, such as Catalog previews made with Object.Instantiate.
        // So the stepper also lays out right before the next canvas update, once its children are enabled and measurable.
        void RequestLayoutBeforeRender()
        {
            if (renderLayoutPending)
                return;

            renderLayoutPending = true;
            layoutBeforeRender ??= LayoutBeforeRender;
            Canvas.preWillRenderCanvases += layoutBeforeRender;
        }

        void CancelEditorRefresh()
        {
            if (refreshPending)
            {
                refreshPending = false;
                UnityEditor.EditorApplication.delayCall -= RefreshDelayed;
            }

            if (renderLayoutPending)
            {
                renderLayoutPending = false;
                Canvas.preWillRenderCanvases -= layoutBeforeRender;
            }
        }

        void RefreshDelayed()
        {
            refreshPending = false;
            if (CanRefreshFromCallback)
                Refresh(refreshAfterUndo);
        }

        // Only places what's already there. The delayed refresh still builds, links, and updates the rest.
        void LayoutBeforeRender()
        {
            renderLayoutPending = false;
            Canvas.preWillRenderCanvases -= layoutBeforeRender;
            if (CanRefreshFromCallback && !isRefreshing && !UnityEditor.Undo.isProcessing)
                UpdateLayout();
        }

        bool CanRefreshFromCallback => this != null && !UnityEditor.EditorApplication.isCompiling
            && !(UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode && !Application.isPlaying);

        // Whether Refresh can run without creating, destroying, or moving objects
        bool HasCompleteHierarchy()
        {
            if (items == null || indicatorParent == null || contentParent == null)
                return false;

            usedContent.Clear();
            for (int i = 0; i < items.Count; i++)
            {
                CanvasGroup content = items[i]?.content;
                if (!ValidContent(content) || !usedContent.Add(content) || content.transform.parent != contentParent)
                    return false;
            }

            return HasIndicators();
        }

        // Whether RefreshIndicators would leave every indicator in place
        bool HasIndicators()
        {
            int count = IndicatorCount;
            if (generatedIndicatorPreset != indicatorPreset || indicatorObjects.Count != count)
                return false;

            string linkPath = count > 0 ? GeneratedObjects.GetLinkPath(this, indicatorPreset) : null;
            for (int i = 0; i < count; i++)
            {
                StepperIndicator indicator = indicatorObjects[i];
                if (indicator == null || indicator.transform.parent != indicatorParent || GeneratedObjects.NeedsRelink(indicator, indicatorPreset, linkPath))
                    return false;
            }
            return !GeneratedObjects.HasOrphans(indicatorObjects, indicatorParent, indicatorPreset, linkPath);
        }
#endif
    }
}