using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using Evo.Tween;

namespace Evo.UI
{
    [HelpURL(Constants.HelpUrl + "ui-elements/carousel")]
    public class CarouselIndicator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Interactive button;
        [SerializeField] private ProceduralShape fillShape;
        [SerializeField] private Image fillImage;

        [Header("Idle References")]
        [SerializeField] private RectTransform idleTransform;
        [SerializeField] private CanvasGroup idleCanvasGroup;

        [Header("Timer References")]
        [SerializeField] private RectTransform timerTransform;
        [SerializeField] private CanvasGroup timerCanvasGroup;

        [Header("Settings")]
        [Min(0f), SerializeField] private float animationDuration = 0.3f;

        // Cache
        Carousel owner;
        Interactive boundButton;
        UnityAction clickAction;
        Action<float> animateSelection;
        Action completeSelection;
        TweenHandle selectionTween;

        // State
        int index;
        bool isSelected;
        float selection;
        float shrink;

        void OnDisable() => StopAnimation();
     
        void OnDestroy() => Unbind();

        internal void Bind(Carousel carousel, int itemIndex)
        {
            owner = carousel;
            index = itemIndex;

            if (boundButton == button)
                return;

            Unbind();
            owner = carousel;

            if (button != null)
            {
                clickAction ??= OnClick;
                boundButton = button;
                boundButton.onClick.AddListener(clickAction);
            }
        }

        internal void Unbind()
        {
            if (boundButton != null)
                boundButton.onClick.RemoveListener(clickAction);

            boundButton = null;
            owner = null;
            StopAnimation();
        }

        void OnClick()
        {
            if (owner != null)
                owner.GoToItem(index);
        }

        internal bool SetSelected(bool selected, float idleScale, TimeMode timeMode, bool animate)
        {
            StopAnimation();
            isSelected = selected;
            shrink = Mathf.Clamp01(idleScale);

            if (animate && isActiveAndEnabled)
            {
                SetTimerActive(true);
                SetIdleActive(true);
                animateSelection ??= AnimateSelection;
                completeSelection ??= CompleteSelection;
                selectionTween = EvoTween.Create(selection, selected ? 1f : 0f, animationDuration)
                    .WithEase(Ease.InOutCubic)
                    .WithTimeMode(timeMode)
                    .OnComplete(completeSelection)
                    .Bind(animateSelection);
                return true;
            }

            selection = selected ? 1f : 0f;
            float scale = selected ? 1f : shrink;
            Vector3 size = Vector3.one * scale;
            bool changed = (timerTransform != null && (timerTransform.localScale != size || timerTransform.gameObject.activeSelf != selected))
                || (idleTransform != null && (idleTransform.localScale != size || idleTransform.gameObject.activeSelf == selected))
                || (timerCanvasGroup != null && timerCanvasGroup.alpha != selection)
                || (idleCanvasGroup != null && idleCanvasGroup.alpha != 1f - selection);

            CompleteSelection();
            return changed;
        }

        void AnimateSelection(float value)
        {
            selection = value;
            float scale = Mathf.Lerp(shrink, 1f, value);
            SetTimerScale(scale);
            SetIdleScale(scale);
            SetTimerAlpha(value);
            SetIdleAlpha(1f - value);
        }

        void CompleteSelection()
        {
            AnimateSelection(isSelected ? 1f : 0f);
            SetTimerActive(isSelected);
            SetIdleActive(!isSelected);
        }

        void StopAnimation()
        {
            selectionTween.Kill();
            selectionTween = default;
        }

#if UNITY_EDITOR
        internal void RecordPrefabChanges()
        {
            if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(this))
                GeneratedObjects.Record(gameObject, fillShape, fillImage, idleCanvasGroup, timerCanvasGroup,
                    idleTransform, idleTransform != null ? idleTransform.gameObject : null,
                    timerTransform, timerTransform != null ? timerTransform.gameObject : null);
        }
#endif

        internal bool ResetProgress()
        {
            bool changed = fillShape != null ? fillShape.ClipAmount != 0f : fillImage != null && fillImage.fillAmount != 0f;
            SetProgress(0f);
            return changed;
        }

        internal void SetProgress(float progress)
        {
            progress = Mathf.Clamp01(progress);
            if (fillShape != null)
                fillShape.ClipAmount = progress;
            else if (fillImage != null && fillImage.fillAmount != progress)
                fillImage.fillAmount = progress;
        }

        void SetTimerAlpha(float alpha)
        {
            if (timerCanvasGroup != null && timerCanvasGroup.alpha != alpha)
                timerCanvasGroup.alpha = alpha;
        }

        void SetIdleAlpha(float alpha)
        {
            if (idleCanvasGroup != null && idleCanvasGroup.alpha != alpha)
                idleCanvasGroup.alpha = alpha;
        }

        void SetTimerScale(float scale)
        {
            Vector3 size = Vector3.one * scale;
            if (timerTransform != null && timerTransform.localScale != size)
                timerTransform.localScale = size;
        }

        void SetIdleScale(float scale)
        {
            Vector3 size = Vector3.one * scale;
            if (idleTransform != null && idleTransform.localScale != size)
                idleTransform.localScale = size;
        }

        void SetTimerActive(bool active)
        {
            if (timerTransform != null && timerTransform.gameObject.activeSelf != active)
                timerTransform.gameObject.SetActive(active);
        }

        void SetIdleActive(bool active)
        {
            if (idleTransform != null && idleTransform.gameObject.activeSelf != active)
                idleTransform.gameObject.SetActive(active);
        }
    }
}