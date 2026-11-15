using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gear.Utilities.UI.Widgets
{
    /// <summary>
    /// 长按进度按钮：按住累计 Progress，达到 HoldDuration 后触发完成；松手在 ResetDuration 内回退。
    /// 三态 CanvasGroup 可分别放普通/按住/完成视觉，不改 Button 的点击语义。
    /// </summary>
    [AddComponentMenu("UI/Gear/Progress Button")]
    public sealed class GearProgressButton : Button
    {
        [Header("Progress")]
        [SerializeField, Min(0.01f)] private float holdDuration = 1.2f;
        [SerializeField, Min(0f)] private float resetDuration = 0.25f;
        [SerializeField] private AnimationCurve progressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [SerializeField] private bool stayOnComplete;
        [SerializeField, Min(0f)] private float completeStateDuration = 0.6f;

        [Header("Visuals")]
        [SerializeField] private CanvasGroup normalGroup;
        [SerializeField] private CanvasGroup holdingGroup;
        [SerializeField] private CanvasGroup completeGroup;
        [SerializeField] private Image progressFill;

        [Header("Events")]
        [SerializeField] private UnityEvent onProgressStart = new UnityEvent();
        [SerializeField] private UnityEvent<float> onProgressUpdate = new UnityEvent<float>();
        [SerializeField] private UnityEvent onProgressComplete = new UnityEvent();
        [SerializeField] private UnityEvent onProgressCancel = new UnityEvent();

        private bool holding;
        private bool complete;
        private bool resetting;
        private float rawProgress;
        private float resetStart;
        private float resetElapsed;
        private float completeElapsed;

        public float HoldDuration
        {
            get => holdDuration;
            set => holdDuration = Mathf.Max(0.01f, value);
        }

        public float ResetDuration
        {
            get => resetDuration;
            set => resetDuration = Mathf.Max(0f, value);
        }

        public float Progress => progressCurve != null ? progressCurve.Evaluate(rawProgress) : rawProgress;
        public bool IsHolding => holding;
        public bool IsComplete => complete;

        public UnityEvent OnProgressStart => onProgressStart;
        public UnityEvent<float> OnProgressUpdate => onProgressUpdate;
        public UnityEvent OnProgressComplete => onProgressComplete;
        public UnityEvent OnProgressCancel => onProgressCancel;

        protected override void OnEnable()
        {
            base.OnEnable();
            ResetProgress(false);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ResetProgress(false);
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left) return;
            base.OnPointerDown(eventData);
            StartHold();
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left) return;
            if (complete)
            {
                base.OnPointerUp(eventData);
                return;
            }

            CancelHold();
            DoStateTransition(SelectionState.Normal, false);
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            if (holding) CancelHold();
        }

        public override void OnSubmit(BaseEventData eventData)
        {
            StartHold();
        }

        private void Update()
        {
            if (holding) Step(Time.unscaledDeltaTime);
            else if (resetting)
            {
                resetElapsed += Time.unscaledDeltaTime;
                float t = resetDuration <= 0f ? 1f : Mathf.Clamp01(resetElapsed / resetDuration);
                rawProgress = Mathf.Lerp(resetStart, 0f, t);
                if (progressFill != null) progressFill.fillAmount = Progress;
                if (t >= 1f) resetting = false;
            }
            else if (complete && !stayOnComplete)
            {
                completeElapsed += Time.unscaledDeltaTime;
                if (completeElapsed >= completeStateDuration) ResetProgress(false);
            }
        }

        /// <summary>手动推进进度，供测试或外部驱动使用。</summary>
        public void Step(float delta)
        {
            if (!holding) return;
            rawProgress = Mathf.Clamp01(rawProgress + Mathf.Max(0f, delta) / holdDuration);
            onProgressUpdate?.Invoke(Progress);
            if (progressFill != null) progressFill.fillAmount = Progress;

            if (rawProgress >= 1f) Complete();
        }

        private void StartHold()
        {
            if (!IsInteractable() || holding || complete) return;
            holding = true;
            resetting = false;
            rawProgress = 0f;
            SetVisuals(true, false);
            onProgressStart?.Invoke();
            onProgressUpdate?.Invoke(0f);
            if (progressFill != null) progressFill.fillAmount = 0f;
        }

        private void CancelHold()
        {
            if (!holding) return;
            holding = false;
            SetVisuals(false, false);
            onProgressCancel?.Invoke();
            resetStart = rawProgress;
            resetElapsed = 0f;
            resetting = resetDuration > 0f;
            if (!resetting) rawProgress = 0f;
            if (progressFill != null) progressFill.fillAmount = Progress;
        }

        private void Complete()
        {
            holding = false;
            complete = true;
            completeElapsed = 0f;
            rawProgress = 1f;
            SetVisuals(false, true);
            onProgressUpdate?.Invoke(1f);
            if (progressFill != null) progressFill.fillAmount = 1f;
            onProgressComplete?.Invoke();
        }

        /// <summary>外部重置到未持有/未完成状态。</summary>
        public void ResetProgress(bool notifyCancel = false)
        {
            if (notifyCancel && holding) onProgressCancel?.Invoke();
            holding = false;
            complete = false;
            resetting = false;
            rawProgress = 0f;
            resetElapsed = 0f;
            completeElapsed = 0f;
            SetVisuals(false, false);
            if (progressFill != null) progressFill.fillAmount = 0f;
        }

        private void SetVisuals(bool isHolding, bool isComplete)
        {
            SetGroup(normalGroup, !isHolding && !isComplete);
            SetGroup(holdingGroup, isHolding);
            SetGroup(completeGroup, isComplete);
        }

        private static void SetGroup(CanvasGroup group, bool visible)
        {
            if (group == null) return;
            group.alpha = visible ? 1f : 0f;
            group.blocksRaycasts = visible;
            group.interactable = visible;
        }
    }
}
