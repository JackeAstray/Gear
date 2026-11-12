using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace Evo.UI
{
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Evo/UI/Layout/Stepper Indicator"), HelpURL(Constants.HelpUrl)]
    public class StepperIndicator : MonoBehaviour
    {
        [Header("References")]
        public Interactive button;
        [TextComponent] public Graphic numberText;
        [Tooltip("Optional. The Stepper places it below horizontal indicators and beside vertical ones.")]
        [TextComponent] public Graphic titleText;
        [Tooltip("Optional group on the title object. Fades the title of upcoming steps.")]
        public CanvasGroup titleGroup;
        public CanvasGroup upcomingGroup;
        public CanvasGroup currentGroup;
        public CanvasGroup completedGroup;
        [Tooltip("Optional shape clipped to show how far the step is reached. It fills as the step becomes current and empties when it's upcoming again.")]
        public ProceduralShape fill;

        [Header("Animation")]
        [Range(0.1f, 1f)] public float inactiveScale = 0.5f;
        [Range(0f, 1f)] public float upcomingTitleOpacity = 0.25f;

        // Cache
        Stepper owner;
        Interactive boundButton;
        UnityAction clickAction;
        Graphic boundNumberText;
        readonly char[] numberBuffer = new char[11];
        Vector3 startWeights;
        Vector3 targetWeights;
        Vector3 weights;
        int index;
        bool isSettled;

        internal RectTransform TitleRect => titleText != null && titleText.transform.parent == transform ? titleText.rectTransform : null;

        void OnDestroy() => Unbind();

        internal void Bind(Stepper stepper, int itemIndex, string title, bool runtime)
        {
            owner = stepper;

            if (numberText != null && (index != itemIndex || boundNumberText != numberText) && (itemIndex + 1).TryFormat(numberBuffer, out int length))
                TextProvider.SetText(numberText, numberBuffer, 0, length);

            boundNumberText = numberText;
            index = itemIndex;

            if (titleText != null)
            {
                TextProvider.UpdateText(titleText, title);
                bool hasTitle = !string.IsNullOrEmpty(title);
                if (titleText.gameObject.activeSelf != hasTitle)
                    titleText.gameObject.SetActive(hasTitle);
            }

            // State layers are visuals only, so the indicator's button gets the input
            DisableInput(upcomingGroup);
            DisableInput(currentGroup);
            DisableInput(completedGroup);

            Interactive target = runtime ? button : null;
            if (boundButton == target)
                return;

            Unbind();
            owner = stepper;

            if (target != null)
            {
                clickAction ??= OnClick;
                boundButton = target;
                boundButton.onClick.AddListener(clickAction);
            }
        }

        internal void Unbind()
        {
            if (boundButton != null)
                boundButton.onClick.RemoveListener(clickAction);

            boundButton = null;
            owner = null;
        }

        void OnClick()
        {
            if (owner != null && owner.allowIndicatorNavigation)
                owner.GoToStep(index);
        }

#if UNITY_EDITOR
        internal void RecordPrefabChanges()
        {
            if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(this))
                GeneratedObjects.Record(gameObject, button, numberText, titleText, titleGroup, upcomingGroup, currentGroup, completedGroup, fill,
                    titleText != null ? titleText.gameObject : null, upcomingGroup != null ? upcomingGroup.transform : null,
                    currentGroup != null ? currentGroup.transform : null, completedGroup != null ? completedGroup.transform : null);
        }
#endif

        // Bar fills run along the rail: left to right, or top to bottom. Other clip methods (radial) stay as designed.
        internal void SetOrientation(bool horizontal)
        {
            if (fill == null || (fill.ClippingMethod != ProceduralShape.ClipMethod.Horizontal && fill.ClippingMethod != ProceduralShape.ClipMethod.Vertical))
                return;

            ProceduralShape.ClipMethod method = horizontal ? ProceduralShape.ClipMethod.Horizontal : ProceduralShape.ClipMethod.Vertical;
            if (fill.ClippingMethod != method)
                fill.ClippingMethod = method;

            // Vertical clipping starts at the bottom by default, while vertical steppers progress downward
            int origin = horizontal ? 0 : 1;
            if (fill.ClipOrigin != origin)
                fill.ClipOrigin = origin;
        }

        internal void SetTarget(int state)
        {
            startWeights = weights;
            targetWeights = state == 0 ? Vector3.right : state == 1 ? Vector3.up : Vector3.forward;
            isSettled = false;
        }

        internal void ApplyAnimation(float progress)
        {
            // An indicator whose state doesn't change keeps its look for the rest of the animation
            if (isSettled)
                return;

            isSettled = startWeights.Equals(targetWeights);

            // Unclamped, so eases like Out Back overshoot the scale. Opacity uses the clamped weights.
            weights = Vector3.LerpUnclamped(startWeights, targetWeights, progress);
            float upcoming = Mathf.Clamp01(weights.x);
            float current = Mathf.Clamp01(weights.y);
            float completed = Mathf.Clamp01(weights.z);

            // Layers stack upcoming, current, completed from the bottom. Each one covers the blend below it,
            // so the visible result is a true crossfade instead of two half-transparent layers.
            float below = upcoming;
            ApplyGroup(upcomingGroup, weights.x, below > 0f ? 1f : 0f);
            below += current;
            ApplyGroup(currentGroup, weights.y, below > 0f ? current / below : 0f);
            below += completed;
            ApplyGroup(completedGroup, weights.z, below > 0f ? completed / below : 0f);

            if (titleGroup != null)
            {
                float alpha = Mathf.Lerp(upcomingTitleOpacity, 1f, current + completed);
                if (titleGroup.alpha != alpha)
                    titleGroup.alpha = alpha;
            }

            if (fill != null)
            {
                float amount = Mathf.Clamp01(current + completed);
                if (fill.ClipAmount != amount)
                    fill.ClipAmount = amount;
            }
        }

        void ApplyGroup(CanvasGroup group, float weight, float alpha)
        {
            if (group == null)
                return;

            Vector3 scale = Vector3.one * Mathf.Max(0f, Mathf.LerpUnclamped(inactiveScale, 1f, weight));

            if (group.alpha != alpha)
                group.alpha = alpha;
            if (group.transform.localScale != scale)
                group.transform.localScale = scale;
        }

        static void DisableInput(CanvasGroup group)
        {
            if (group == null)
                return;
            if (group.interactable)
                group.interactable = false;
            if (group.blocksRaycasts)
                group.blocksRaycasts = false;
        }
    }
}
