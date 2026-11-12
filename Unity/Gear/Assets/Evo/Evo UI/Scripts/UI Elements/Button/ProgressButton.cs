using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using Evo.Tween;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Evo/UI/UI Elements/Progress Button"), HelpURL(Constants.HelpUrl + "ui-elements/progress-button")]
    public class ProgressButton : Interactive
    {
        // Progress Settings
        [Range(0.1f, 30f), Tooltip("Time required to hold the button.")]
        public float holdDuration = 2f;
        [Range(0f, 3f), Tooltip("Reset animation duration.")]
        public float resetDuration = 0.5f;
        public AnimationCurve progressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        public AnimationCurve resetCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        // Completion Settings
        public bool stayOnComplete = false;
        [Range(0f, 10f), Tooltip("How long to show the complete state before resetting.")]
        public float completeStateDuration = 1f;

        // State Animation
        [Range(0f, 1f), Tooltip("Duration of each outgoing and incoming phase. Zero switches immediately.")]
        public float progressTransitionDuration = 0.15f;
        [SerializeField, Tooltip("Transform animation for switching states. None keeps only the fade.")]
        private AnimationType animationType = AnimationType.Slide;
        [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField, Range(0f, 1f)] private float scaleFrom = 0.94f;
        [SerializeField] private Vector2 slideOffset = new(0f, 6f);

        // References
        public ProceduralShape progressFill;
        [SerializeField] private CanvasGroup normalStateCG;
        [SerializeField] private CanvasGroup inProgressStateCG;
        [SerializeField] private CanvasGroup completeStateCG;

        // Events
        public UnityEvent onProgressStart = new();
        public UnityEvent<float> onProgressUpdate = new();
        public UnityEvent onComplete = new();
        public UnityEvent onCancel = new();

        // State
        TweenHandle progressAnimation;
        TweenHandle stateAnimation;
        bool isSubmitHolding;
        bool hasOutgoingState;

        // Cache
        readonly StateInfo[] stateInfo = new StateInfo[3];
        struct StateInfo
        {
            public CanvasGroup canvasGroup;
            public RectTransform rectTransform;
            public Vector2 originalPosition;
            public Vector3 originalScale;
            public float startAlpha;
            public Vector2 startPosition;
            public Vector3 startScale;
        }

        // Enums
        public enum ProgressState { Normal, InProgress, Complete }
        public enum AnimationType { None = 0, Scale = 1, Slide = 2 }
        public enum FillOrigin { Bottom = 0, Right = 1, Top = 2, Left = 3 }

        // Properties
        public ProgressState CurrentProgressState { get; private set; } = ProgressState.Normal;
        public float CurrentProgress { get; private set; } = 0f;
        public bool IsHolding { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            InitializeProgressFill();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            CacheStateInfo();
            ResetProgress(true);
        }

        protected override void OnDisable()
        {
            ResetProgress(true);
            base.OnDisable();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= OnValidateDelayCall;
#endif
        }

        void Update()
        {
            if (!IsHolding)
                return;

            if (!IsInteractable() || (isSubmitHolding
                && (Utilities.GetSelectedObject() != gameObject || !Utilities.WasSubmitPressed())))
                CancelProgress();
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);

            if (!IsInteractable() || eventData.button != PointerEventData.InputButton.Left)
                return;

            StartProgress();
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);

            if (eventData.button == PointerEventData.InputButton.Left)
                CancelProgress();
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            CancelProgress();
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);
            CancelProgress();
        }

        public override void OnSubmit(BaseEventData eventData)
        {
            if (!IsActive() || !IsInteractable() || IsHolding)
                return;

            base.OnSubmit(eventData);
            StartProgress();
            isSubmitHolding = IsHolding;
        }

        void InitializeProgressFill()
        {
            if (progressFill == null)
                return;

            progressFill.ClipAmount = CurrentProgress;
        }

        void CacheStateInfo()
        {
            CacheSingleState(0, normalStateCG);
            CacheSingleState(1, inProgressStateCG);
            CacheSingleState(2, completeStateCG);
        }

        void CacheSingleState(int index, CanvasGroup cg)
        {
            ref StateInfo info = ref stateInfo[index];
            if (info.canvasGroup == cg && Application.isPlaying)
                return;

            info = default;

            if (cg == null)
                return;

            info.canvasGroup = cg;
            info.rectTransform = cg.transform as RectTransform;
            info.originalScale = cg.transform.localScale;

            if (info.rectTransform != null)
                info.originalPosition = info.rectTransform.anchoredPosition;
        }

        void UpdateProgress(float progress)
        {
            SetProgress(progress);

            // Binding applies the initial value before the handle is assigned
            if (IsHolding && progressAnimation.IsActive)
                onProgressUpdate?.Invoke(CurrentProgress);
        }

        void SetProgressState(ProgressState state, bool instant = false)
        {
            if (CurrentProgressState == state && !instant)
                return;

            stateAnimation.Kill();
            stateAnimation = default;
            CurrentProgressState = state;

            if (instant || !Application.isPlaying || !isActiveAndEnabled || progressTransitionDuration <= 0f)
            {
                SetStateImmediate();
                return;
            }

            hasOutgoingState = false;
            for (int i = 0; i < stateInfo.Length; i++)
            {
                ref StateInfo info = ref stateInfo[i];
                CanvasGroup cg = info.canvasGroup;
                if (cg == null)
                    continue;

                bool isIncoming = i == (int)state;
                cg.blocksRaycasts = isIncoming;

                if (isIncoming && cg.alpha <= 0f)
                {
                    cg.transform.localScale = animationType == AnimationType.Scale
                        ? info.originalScale * scaleFrom : info.originalScale;

                    if (info.rectTransform != null)
                    {
                        info.rectTransform.anchoredPosition = info.originalPosition -
                            (animationType == AnimationType.Slide ? slideOffset : Vector2.zero);
                    }
                }

                info.startAlpha = cg.alpha;
                info.startScale = cg.transform.localScale;

                if (info.rectTransform != null)
                    info.startPosition = info.rectTransform.anchoredPosition;
                if (!isIncoming && info.startAlpha > 0f)
                    hasOutgoingState = true;
            }

            float phases = (hasOutgoingState ? 1f : 0f) + (stateInfo[(int)state].canvasGroup != null ? 1f : 0f);
            if (phases == 0f)
            {
                SetStateImmediate();
                return;
            }

            stateAnimation = EvoTween.Create(0f, phases, progressTransitionDuration * phases)
                .WithTimeMode(TimeMode.Unscaled)
                .OnComplete(SetStateImmediate)
                .Bind(this, static (button, value) => button.UpdateStateAnimation(value), 1);
        }

        void UpdateStateAnimation(float progress)
        {
            for (int i = 0; i < stateInfo.Length; i++)
            {
                ref StateInfo info = ref stateInfo[i];
                CanvasGroup cg = info.canvasGroup;
                bool isIncoming = i == (int)CurrentProgressState;
                if (cg == null || (!isIncoming && info.startAlpha <= 0f))
                    continue;

                float t = Mathf.Clamp01(progress - (isIncoming && hasOutgoingState ? 1f : 0f));
                float curvedProgress = animationCurve != null && animationCurve.length > 0 ? animationCurve.Evaluate(t) : t;
                cg.alpha = Mathf.Lerp(info.startAlpha, isIncoming ? 1f : 0f, t);

                if (animationType == AnimationType.Scale)
                {
                    Vector3 targetScale = info.originalScale * (isIncoming ? 1f : 2f - scaleFrom);
                    cg.transform.localScale = Vector3.LerpUnclamped(info.startScale, targetScale, curvedProgress);
                }
                else if (animationType == AnimationType.Slide && info.rectTransform != null)
                {
                    Vector2 targetPosition = info.originalPosition + (isIncoming ? Vector2.zero : slideOffset);
                    info.rectTransform.anchoredPosition = Vector2.LerpUnclamped(info.startPosition, targetPosition, curvedProgress);
                }
            }
        }

        void SetStateImmediate()
        {
            for (int i = 0; i < stateInfo.Length; i++)
            {
                ref StateInfo info = ref stateInfo[i];
                if (info.canvasGroup == null)
                    continue;

                bool isActive = i == (int)CurrentProgressState;
                info.canvasGroup.alpha = isActive ? 1f : 0f;
                info.canvasGroup.blocksRaycasts = isActive;
                info.canvasGroup.transform.localScale = info.originalScale;

                if (info.rectTransform != null)
                    info.rectTransform.anchoredPosition = info.originalPosition;
            }
        }

        void StopProgressAnimation()
        {
            progressAnimation.Kill();
            progressAnimation = default;
        }

        void FinishReset()
        {
            SetProgress(0f);
            SetProgressState(ProgressState.Normal);
        }

        void ResetProgressAnimated() => ResetProgress(false);

        public void StartProgress()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || !IsInteractable() || IsHolding ||
                (CurrentProgressState == ProgressState.Complete && stayOnComplete))
                return;

            StopProgressAnimation();
            IsHolding = true;
            isSubmitHolding = false;
            SetProgressState(ProgressState.InProgress);

            bool hasCustomCurve = progressCurve != null && progressCurve.length > 0;
            progressAnimation = EvoTween.Create(CurrentProgress, 1f, Mathf.Max(0f, holdDuration))
                .WithEase(hasCustomCurve ? Ease.Custom : Ease.Linear, hasCustomCurve ? progressCurve : null)
                .WithTimeMode(TimeMode.Unscaled)
                .OnComplete(CompleteProgress)
                .Bind(this, static (button, value) => button.UpdateProgress(value), 0);
           
            onProgressStart?.Invoke();
        }

        public void CancelProgress()
        {
            if (!IsHolding)
                return;

            ResetProgress(false);
            onCancel?.Invoke();
        }

        public void CompleteProgress()
        {
            IsHolding = false;
            isSubmitHolding = false;

            StopProgressAnimation();
            SetProgress(1f);
            SetProgressState(ProgressState.Complete);

            if (!stayOnComplete && Application.isPlaying && isActiveAndEnabled)
            {
                progressAnimation = EvoTween.Create(0f, 1f, Mathf.Max(0f, completeStateDuration))
                    .WithTimeMode(TimeMode.Unscaled)
                    .OnComplete(ResetProgressAnimated)
                    .Bind(this, static (button, value) => { }, 0);
            }

            onComplete?.Invoke();
        }

        public void ResetProgress(bool instant)
        {
            IsHolding = false;
            isSubmitHolding = false;
            StopProgressAnimation();

            if (instant || !Application.isPlaying || !isActiveAndEnabled || resetDuration <= 0f)
            {
                SetProgress(0f);
                SetProgressState(ProgressState.Normal, true);
                return;
            }

            if (CurrentProgress <= 0f)
            {
                FinishReset();
                return;
            }

            bool hasCustomCurve = resetCurve != null && resetCurve.length > 0;
            progressAnimation = EvoTween.Create(CurrentProgress, 0f, resetDuration)
                .WithEase(hasCustomCurve ? Ease.Custom : Ease.Linear, hasCustomCurve ? resetCurve : null)
                .WithTimeMode(TimeMode.Unscaled)
                .OnComplete(FinishReset)
                .Bind(this, static (button, value) => button.SetProgress(value), 0);
        }

        public void SetProgress(float progress)
        {
            CurrentProgress = Mathf.Clamp01(progress);

            if (progressFill != null)
                progressFill.ClipAmount = CurrentProgress;
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            UnityEditor.EditorApplication.delayCall -= OnValidateDelayCall;
            UnityEditor.EditorApplication.delayCall += OnValidateDelayCall;
        }

        void OnValidateDelayCall()
        {
            if (this == null)
                return;

            InitializeProgressFill();

            if (!Application.isPlaying)
            {
                CacheStateInfo();
                SetStateImmediate();
            }
        }
#endif
    }
}