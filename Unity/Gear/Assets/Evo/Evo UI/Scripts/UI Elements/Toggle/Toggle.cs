using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Evo.Tween;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Evo/UI/UI Elements/Toggle"), HelpURL(Constants.HelpUrl + "ui-elements/toggle")]
    public class Toggle : Interactive
    {
        // Settings
        [SerializeField, Tooltip("Sets whether the toggle is on or off.")]
        private bool isOn;

        // Animation
        [Min(0f)] public float checkDuration = 0.2f;
        public Ease checkEase = Ease.OutCubic;
        public AnimationCurve checkCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        // References
        [SerializeField, Tooltip("Check image to reveal. Existing fill settings are preserved.")]
        private Image checkmark;

        // Events
        public UnityEvent<bool> onValueChanged = new();

        // Helpers
        TweenHandle currentCheckAnimation;
        Action completeCheckAnimation;

        public bool IsOn
        {
            get => isOn;
            set => SetValue(value);
        }

        protected override void Awake()
        {
            base.Awake();
            completeCheckAnimation = CompleteCheckAnimation;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            UpdateCheck(false);
        }

        protected override void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= OnValidateDelayCall;
#endif
            UpdateCheck(false);
            base.OnDisable();
        }

        public override void OnPointerClick(PointerEventData eventData)
        {
            if (!IsActive() || !IsInteractable() || eventData.button != PointerEventData.InputButton.Left)
                return;

            base.OnPointerClick(eventData);

            if (IsActive() && IsInteractable())
                ToggleValue();
        }

        public override void OnSubmit(BaseEventData eventData)
        {
            if (!IsActive() || !IsInteractable())
                return;

            base.OnSubmit(eventData);

            if (IsActive() && IsInteractable())
                ToggleValue();
        }

        void UpdateCheck(bool animate)
        {
            currentCheckAnimation.Kill();
            currentCheckAnimation = default;

            if (checkmark == null)
                return;

            if (checkmark.type != Image.Type.Filled)
            {
                checkmark.type = Image.Type.Filled;
                checkmark.fillMethod = Image.FillMethod.Horizontal;
                checkmark.fillOrigin = (int)Image.OriginHorizontal.Left;
            }

            float targetFill = isOn ? 1f : 0f;
            if (!animate || !Application.IsPlaying(gameObject) || !IsActive() || checkDuration <= 0f ||
                Mathf.Approximately(checkmark.fillAmount, targetFill))
            {
                checkmark.fillAmount = targetFill;
                return;
            }

            // Continue from the visible fill when reversing an unfinished check.
            float duration = checkDuration * Mathf.Abs(targetFill - checkmark.fillAmount);
            currentCheckAnimation = EvoTween.Create(checkmark.fillAmount, targetFill, duration)
                .WithEase(checkEase, checkCurve)
                .WithTimeMode(TimeMode.Unscaled)
                .OnComplete(completeCheckAnimation)
                .BindToFillAmount(checkmark);
        }

        void CompleteCheckAnimation()
        {
            currentCheckAnimation = default;

            // Custom curves may end above or below one.
            if (checkmark != null)
                checkmark.fillAmount = isOn ? 1f : 0f;
        }

        public void SetValue(bool value, bool invokeEvents = true)
        {
            if (isOn == value)
                return;

            isOn = value;
            UpdateCheck(true);

            if (invokeEvents)
            {
                if (isOn && soundEffects && IsActive() && Application.IsPlaying(gameObject))
                    AudioManager.PlayClip(Styler.GetAudio(styleProvider, selectedSFX, stylerPreset));

                onValueChanged?.Invoke(isOn);
            }
        }

        public void SetValueWithoutNotify(bool value) => SetValue(value, false);
      
        public void ToggleValue() => SetValue(!isOn);

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

            UpdateCheck(false);
        }
#endif
    }
}