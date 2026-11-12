using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using Evo.Tween;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Evo/UI/UI Elements/Radio Button Group"), HelpURL(Constants.HelpUrl + "ui-elements/radio-button")]
    public class RadioButtonGroup : MonoBehaviour, ICanvasElement
    {
        // Settings
        [Tooltip("Direct child index. -1 means no selection.")]
        public int selectedIndex = -1;
        [Tooltip("Allow deselection by selecting the same button again.")]
        public bool allowDeselection = false;
        public bool useUnscaledTime = true;

        // Indicator
        public RectTransform indicatorObject;
        public IndicatorDirection indicatorDirection = IndicatorDirection.Horizontal;
        public bool indicatorAutoSize = true;
        [Min(0f), Tooltip("Amount of stretch effect during animation.")]
        public float indicatorStretch = 0f;
        public AnimationCurve indicatorCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Range(0.01f, 1f)] public float indicatorDuration = 0.3f;
        [Range(0f, 1f), Tooltip("Delay the visual state change (e.g. text color) to sync with indicator arrival.")]
        public float stateChangeDelay = 0f;

        // References
        [Tooltip("If not set, attached transform will be used as button parent. Call Initialize after changing it at runtime.")]
        public Transform targetParent;

        // Enums
        public enum IndicatorDirection { Horizontal, Vertical }

        // Events
        public UnityEvent<int> onSelectionChanged = new();

        // Cache
        Button selectedButton;
        Button visualSelectedButton;
        RectTransform cachedIndicatorObject;
        Vector2 indicatorSize;
        RectTransform indicatorTarget;
        Vector2 indicatorStartSize;
        Vector2 indicatorTargetSize;
        Vector2 indicatorStartPosition;
        Vector2 indicatorTargetPosition;
        TweenHandle indicatorAnimation;
        TweenHandle stateChangeTimer;
        System.Action indicatorCompleted;
        System.Action stateChangeCompleted;
        bool indicatorLayoutPending;
        readonly List<Button> availableButtons = new();
        readonly List<UnityAction> buttonClickListeners = new();

        // Properties
        bool CanAnimate => Application.isPlaying && isActiveAndEnabled;
        int IndicatorAxis => indicatorDirection == IndicatorDirection.Horizontal ? 0 : 1;
        TimeMode TweenTimeMode => useUnscaledTime ? TimeMode.Unscaled : TimeMode.Scaled;

        void Awake() => Initialize();

        void OnEnable() => UpdateIndicator(selectedButton, false);

        void OnDisable()
        {
            StopIndicatorAnimation();
            StopStateChange();
            ApplyStateChange();
        }

        void OnDestroy() => RemoveButtonListeners();

        public void Initialize()
        {
            StopIndicatorAnimation();
            StopStateChange();
            RemoveButtonListeners();

            selectedButton = null;
            ApplyStateChange();
            availableButtons.Clear();

            // Get button parent
            if (targetParent == null)
                targetParent = transform;

            // Keep direct child indices, including children without a Button
            for (int i = 0; i < targetParent.childCount; i++)
            {
                targetParent.GetChild(i).TryGetComponent(out Button btn);
                availableButtons.Add(btn);

                UnityAction listener = null;
                if (btn != null)
                {
                    int btnIndex = i;
                    listener = () =>
                    {
                        if (isActiveAndEnabled)
                            SetButton(btnIndex);
                    };
                    btn.onClick.AddListener(listener);
                    btn.SetState(InteractionState.Normal);
                }
                buttonClickListeners.Add(listener);
            }

            // Set default selection if specified
            Button defaultButton = GetButton(selectedIndex);
            Select(defaultButton, defaultButton != null ? selectedIndex : -1, false, false);
        }

        void RemoveButtonListeners()
        {
            for (int i = 0; i < buttonClickListeners.Count; i++)
            {
                if (availableButtons[i] != null && buttonClickListeners[i] != null)
                    availableButtons[i].onClick.RemoveListener(buttonClickListeners[i]);
            }

            buttonClickListeners.Clear();
        }

        void StopIndicatorAnimation()
        {
            indicatorAnimation.Kill();
            indicatorAnimation = default;
            indicatorLayoutPending = false;
        }

        void UpdateIndicator(Button targetButton, bool animate)
        {
            StopIndicatorAnimation();

            if (indicatorObject == null)
                return;

            if (cachedIndicatorObject != indicatorObject)
            {
                cachedIndicatorObject = indicatorObject;
                indicatorSize = indicatorObject.rect.size;
            }

            RectTransform targetRect = targetButton != null ? targetButton.transform as RectTransform : null;

            // Snaps wait for the canvas layout pass, so new and re-enabled groups measure laid-out buttons before they render
            if (!animate && targetRect != null && enabled && gameObject.activeInHierarchy)
            {
                indicatorTarget = targetRect;
                indicatorLayoutPending = true;
                indicatorObject.gameObject.SetActive(true);
                CanvasUpdateRegistry.TryRegisterCanvasElementForLayoutRebuild(this);
            }
            else
            {
                AnimateIndicator(targetRect, animate);
            }
        }

        void ICanvasElement.LayoutComplete()
        {
            // Also called after destruction
            if (this == null || !indicatorLayoutPending)
                return;

            indicatorLayoutPending = false;

            if (isActiveAndEnabled && indicatorObject != null && indicatorTarget != null)
                AnimateIndicator(indicatorTarget, false);
        }

        void ICanvasElement.Rebuild(CanvasUpdate executing) { }

        void ICanvasElement.GraphicUpdateComplete() { }

        bool ICanvasElement.IsDestroyed() => this == null;

        void AnimateIndicator(RectTransform targetRect, bool animate)
        {
            indicatorTarget = targetRect;
            int axis = IndicatorAxis;
            Vector3 worldPos = indicatorObject.position;
            Vector2 startSize = indicatorObject.rect.size;
            Vector2 targetSize = indicatorSize;
            Vector2 startAnchoredPos;
            Vector2 targetAnchoredPos;

            if (targetRect != null)
            {
                bool wasHidden = !indicatorObject.gameObject.activeSelf;
                indicatorObject.gameObject.SetActive(true);

                // Anchored positions can only be copied between siblings
                if (indicatorObject.parent == targetRect.parent)
                {
                    indicatorObject.anchorMin = targetRect.anchorMin;
                    indicatorObject.anchorMax = targetRect.anchorMax;
                }

                indicatorObject.pivot = targetRect.pivot;
                indicatorObject.position = worldPos;
                startAnchoredPos = indicatorObject.anchoredPosition;
                indicatorObject.position = targetRect.position;
                targetAnchoredPos = indicatorObject.anchoredPosition;
                indicatorObject.anchoredPosition = startAnchoredPos;

                if (indicatorAutoSize)
                {
                    Vector3 width = targetRect.TransformVector(Vector3.right * targetRect.rect.width);
                    Vector3 height = targetRect.TransformVector(Vector3.up * targetRect.rect.height);
                    targetSize = new Vector2(indicatorObject.InverseTransformVector(width).magnitude,
                        indicatorObject.InverseTransformVector(height).magnitude);
                }

                if (wasHidden)
                    startSize[axis] = 0f;
            }
            else
            {
                startAnchoredPos = indicatorObject.anchoredPosition;
                targetAnchoredPos = startAnchoredPos;
                targetSize = startSize;
                targetSize[axis] = 0f;
            }

            indicatorStartSize = startSize;
            indicatorTargetSize = targetSize;
            indicatorStartPosition = startAnchoredPos;
            indicatorTargetPosition = targetAnchoredPos;

            if (animate && CanAnimate && indicatorDuration > 0f && indicatorObject.gameObject.activeInHierarchy)
            {
                bool hasCustomCurve = indicatorCurve != null && indicatorCurve.length > 0;
                indicatorAnimation = EvoTween.Create(0f, 1f, indicatorDuration)
                    .WithEase(hasCustomCurve ? Ease.Custom : Ease.Linear, hasCustomCurve ? indicatorCurve : null)
                    .WithTimeMode(TweenTimeMode)
                    .OnComplete(indicatorCompleted ??= CompleteIndicatorAnimation)
                    .Bind(this, static (group, value) => group.UpdateIndicatorAnimation(value), 0);
            }
            else
            {
                CompleteIndicatorAnimation();
            }
        }

        void UpdateIndicatorAnimation(float progress)
        {
            if (indicatorObject == null)
            {
                StopIndicatorAnimation();
                return;
            }

            float stretchValue = indicatorTarget != null ? Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI) * indicatorStretch : 0f;
            Vector2 currentSize = Vector2.Lerp(indicatorStartSize, indicatorTargetSize, progress);
            currentSize[IndicatorAxis] += stretchValue;

            indicatorObject.anchoredPosition = Vector2.Lerp(indicatorStartPosition, indicatorTargetPosition, progress);
            SetIndicatorSize(currentSize);
        }

        void CompleteIndicatorAnimation()
        {
            indicatorAnimation = default;

            if (indicatorObject == null)
                return;

            indicatorObject.anchoredPosition = indicatorTargetPosition;

            // Restore the authored size while hidden so fixed-size indicators can be selected again
            if (indicatorTarget == null)
                indicatorObject.gameObject.SetActive(false);

            SetIndicatorSize(indicatorTarget != null ? indicatorTargetSize : indicatorSize);
        }

        // SetSizeWithCurrentAnchors for both axes in a single write
        void SetIndicatorSize(Vector2 size)
        {
            Vector2 parentSize = indicatorObject.parent is RectTransform parent ? parent.rect.size : Vector2.zero;
            indicatorObject.sizeDelta = size - Vector2.Scale(parentSize, indicatorObject.anchorMax - indicatorObject.anchorMin);
        }

        public void SetButton(int index) => SetButton(index, true, true);

        public void SetButton(int index, bool animate, bool invokeEvents = true)
        {
            if (index < 0 || index >= availableButtons.Count)
            {
                Debug.LogWarning($"[Radio Button Group] Invalid button index {index}. Valid range: 0-{availableButtons.Count - 1}", this);
                return;
            }

            Button targetButton = availableButtons[index];
            if (targetButton == null)
            {
                Debug.LogWarning($"[Radio Button Group] No button found at index {index}", this);
                return;
            }

            if (selectedButton == targetButton)
            {
                if (allowDeselection)
                    DeselectAll(animate);

                return;
            }

            Select(targetButton, index, animate, invokeEvents);
        }

        void Select(Button button, int index, bool animate, bool invokeEvents)
        {
            StopStateChange();

            // Selection is immediate; only the visual state change is delayed
            selectedButton = button;
            selectedIndex = index;
            UpdateIndicator(button, animate);

            if (button != null && animate && stateChangeDelay > 0f && CanAnimate &&
                indicatorObject != null && indicatorObject.gameObject.activeInHierarchy)
            {
                stateChangeTimer = EvoTween.Create(0f, 1f, stateChangeDelay)
                    .WithTimeMode(TweenTimeMode)
                    .OnComplete(stateChangeCompleted ??= CompleteStateChange)
                    .Bind(this, static (_, _) => { }, 1);
            }
            else
            {
                ApplyStateChange();
            }

            // Notify last so callbacks can safely select again, deselect, or disable the group
            if (invokeEvents && selectedButton == button && selectedIndex == index)
                onSelectionChanged?.Invoke(index);
        }

        void StopStateChange()
        {
            stateChangeTimer.Kill();
            stateChangeTimer = default;
        }

        void CompleteStateChange()
        {
            stateChangeTimer = default;
            ApplyStateChange();
        }

        void ApplyStateChange()
        {
            Button previousButton = visualSelectedButton;
            visualSelectedButton = selectedButton;

            if (previousButton != null && previousButton != visualSelectedButton)
                previousButton.SetState(InteractionState.Normal);

            if (visualSelectedButton != null)
                visualSelectedButton.SetState(InteractionState.Selected);
        }

        public void DeselectAll() => DeselectAll(true);

        void DeselectAll(bool animate) => Select(null, -1, animate, selectedIndex != -1);

        /// <summary>
        /// Sets interactable state for all buttons.
        /// </summary>
        public void SetInteractable(bool interactable)
        {
            for (int i = 0; i < availableButtons.Count; i++)
            {
                if (availableButtons[i] != null)
                    availableButtons[i].interactable = interactable;
            }
        }

        /// <summary>
        /// Returns the currently selected button.
        /// </summary>
        public Button GetButton() => GetButton(selectedIndex);

        /// <summary>
        /// Returns the specified button at index.
        /// </summary>
        public Button GetButton(int index) => index >= 0 && index < availableButtons.Count ? availableButtons[index] : null;

        /// <summary>
        /// Returns true if specified button index is selected.
        /// </summary>
        public bool IsButtonSelected(int index) => selectedIndex == index && GetButton(index) != null;

#if UNITY_EDITOR
        void OnValidate()
        {
            if (Application.IsPlaying(gameObject) || UnityEditor.EditorUtility.IsPersistent(this))
                return;

            UnityEditor.EditorApplication.delayCall -= UpdateEditorPreview;
            UnityEditor.EditorApplication.delayCall += UpdateEditorPreview;
        }

        void UpdateEditorPreview()
        {
            if (this == null || Application.IsPlaying(gameObject))
                return;

            Transform buttonParent = targetParent != null ? targetParent : transform;
            selectedIndex = Mathf.Clamp(selectedIndex, -1, buttonParent.childCount - 1);
            Button previewButton = null;

            for (int i = 0; i < buttonParent.childCount; i++)
            {
                if (!buttonParent.GetChild(i).TryGetComponent(out Button btn))
                    continue;

                btn.SetState(i == selectedIndex ? InteractionState.Selected : InteractionState.Normal);

                if (i == selectedIndex)
                    previewButton = btn;
            }

            if (previewButton == null)
                selectedIndex = -1;

            UpdateIndicator(previewButton, false);
        }
#endif
    }
}
