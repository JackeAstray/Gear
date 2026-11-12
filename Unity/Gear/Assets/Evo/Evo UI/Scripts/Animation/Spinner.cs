using System;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.UI;
using Evo.Tween;

namespace Evo.UI
{
    /// <summary>
    /// Handles animated loading spinners using Procedural Shape or Image fills and rotations.
    /// </summary>
    [DisallowMultipleComponent, NoAutoStaticsCleanup]
    [AddComponentMenu("Evo/UI/Animation/Spinner"), HelpURL(Constants.HelpUrl + "animation/spinner")]
    public class Spinner : MonoBehaviour
    {
        // Settings
        [SerializeField] private SpinnerStyle spinnerStyle = SpinnerStyle.Radial;
        [Range(0.1f, 10f)] public float rotationSpeed = 1f;
        [Range(0f, 1f)] public float minFillAmount = 0.1f;
        [Range(0f, 1f)] public float maxFillAmount = 0.8f;
        public Ease ease = Ease.InOutSine;
        public AnimationCurve customEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

        // References
        [Tooltip("Takes priority over Spinner Image when assigned.")]
        public ProceduralShape spinnerShape;
        public Image spinnerImage;

        public enum SpinnerStyle
        {
            Horizontal = 1,
            Vertical = 2,
            [InspectorName("Radial (Smooth)")] Radial = 0,
            [InspectorName("Radial (Simple)")] RadialSimple = 3
        }

        // Cache
        Graphic spinnerGraphic;
        RectTransform rectTransform;
        Quaternion initialRotation;
        SpinnerStyle configuredStyle;

        // Animation
        TweenHandle spinnerAnimation;
        float previousAngle;
        static readonly Action<Spinner, float> UpdateCallback = (spinner, phase) => spinner.UpdateAnimation(phase);

        void OnEnable() => StartSpinning();

        void OnDisable() => StopSpinning();

        bool ConfigureSpinner()
        {
            if (spinnerShape == null && spinnerImage == null)
            {
                TryGetComponent(out spinnerShape);
                if (spinnerShape == null)
                    TryGetComponent(out spinnerImage);
            }

            Graphic graphic = spinnerShape != null ? spinnerShape : spinnerImage;
            if (spinnerGraphic != graphic)
            {
                if (rectTransform != null)
                    rectTransform.localRotation = initialRotation;

                spinnerGraphic = graphic;
                rectTransform = graphic != null ? graphic.rectTransform : null;
             
                if (rectTransform != null)
                    initialRotation = rectTransform.localRotation;
            }

            if (spinnerGraphic == null || rectTransform == null)
                return false;

            rectTransform.localRotation = initialRotation;
            previousAngle = 0f;
            configuredStyle = spinnerStyle;
            bool radial = spinnerStyle == SpinnerStyle.Radial || spinnerStyle == SpinnerStyle.RadialSimple;
            bool horizontal = spinnerStyle == SpinnerStyle.Horizontal;

            if (spinnerGraphic is ProceduralShape shape)
            {
                shape.ClippingMethod = radial ? ProceduralShape.ClipMethod.Radial360 :
                    horizontal ? ProceduralShape.ClipMethod.Horizontal : ProceduralShape.ClipMethod.Vertical;
            }
            else if (spinnerGraphic is Image image)
            {
                image.type = Image.Type.Filled;
                image.fillMethod = radial ? Image.FillMethod.Radial360 :
                    horizontal ? Image.FillMethod.Horizontal : Image.FillMethod.Vertical;
            }

            UpdateAnimation(0f);
            return true;
        }

        void UpdateAnimation(float phase)
        {
            if (spinnerStyle != configuredStyle || spinnerGraphic != (spinnerShape != null ? spinnerShape : spinnerImage))
            {
                StartSpinning();
                return;
            }

            if (spinnerGraphic == null || rectTransform == null)
            {
                StopSpinning();
                return;
            }

            bool radial = spinnerStyle == SpinnerStyle.Radial || spinnerStyle == SpinnerStyle.RadialSimple;
            float fill;
            int origin;

            if (radial)
            {
                float minimum = float.IsNaN(minFillAmount) ? 0.1f : Mathf.Clamp01(minFillAmount);
                float maximum = float.IsNaN(maxFillAmount) ? 0.8f : Mathf.Clamp01(maxFillAmount);
                float range = spinnerStyle == SpinnerStyle.Radial ? Mathf.Max(0f, maximum - minimum) : 0f;
                float angle = phase * 360f;
                fill = minimum;

                if (range > 0f)
                {
                    float cycle = Mathf.Repeat(phase, 2f);
                    bool expanding = cycle < 1f;
                    float easedFill = EvaluateEase(expanding ? cycle : cycle - 1f);
                    fill += range * (expanding ? easedFill : 1f - easedFill);

                    // Grow the leading edge, then advance the trailing edge to catch it.
                    // Balance the extra sweep so each cycle joins continuously at any fill range.
                    float tail = expanding ? 0f : easedFill;
                    angle += range * (tail - cycle * 0.5f) * 360f;
                }

                float delta = Mathf.DeltaAngle(previousAngle, angle);
                previousAngle = angle;
                bool clockwise = spinnerGraphic is ProceduralShape radialShape ? radialShape.ClipClockwise : ((Image)spinnerGraphic).fillClockwise;
                rectTransform.Rotate(0f, 0f, clockwise ? -delta : delta, Space.Self);
                origin = (int)Image.Origin360.Top;
            }
            else
            {
                int step = Mathf.Min((int)phase, 3);
                float progress = phase - step;
                float easedFill = EvaluateEase(progress);
                fill = (step & 1) == 0 ? easedFill : 1f - easedFill;
                origin = step == 1 || step == 2 ? 1 : 0;
            }

            spinnerAnimation.WithSpeed(rotationSpeed);

            if (spinnerGraphic is ProceduralShape shape)
            {
                shape.ClipOrigin = !radial && !shape.ClipClockwise ? 1 - origin : origin;
                shape.ClipAmount = fill;
            }
            else if (spinnerGraphic is Image image)
            {
                image.fillOrigin = origin;
                image.fillAmount = fill;
            }
        }

        float EvaluateEase(float progress)
        {
            float value = TweenEase.Evaluate(ease, progress, customEase);
            return float.IsNaN(value) ? progress : Mathf.Clamp01(value);
        }

        /// <summary>
        /// Begins the spinner animation sequence. Automatically called OnEnable.
        /// </summary>
        public void StartSpinning()
        {
            StopSpinning();

            if (!Application.isPlaying || !isActiveAndEnabled || !ConfigureSpinner())
                return;

            // One linear clock keeps rotation and eased fill in sync across loop boundaries
            spinnerAnimation = EvoTween.Create(0f, 4f, 4f)
                .WithSpeed(rotationSpeed)
                .WithLoops(0)
                .WithTimeMode(TimeMode.Unscaled)
                .Bind(this, UpdateCallback, 0x535046);
        }

        /// <summary>
        /// Stops the spinner animation sequence. Automatically called OnDisable.
        /// </summary>
        public void StopSpinning()
        {
            spinnerAnimation.Kill();
            spinnerAnimation = default;
        }

        /// <summary>
        /// Updates the visual style of the spinner dynamically at runtime.
        /// </summary>
        public void SetSpinnerStyle(SpinnerStyle style)
        {
            if (spinnerStyle == style)
                return;

            bool wasSpinning = spinnerAnimation.IsActive;
            spinnerStyle = style;

            if (wasSpinning) { StartSpinning(); }
            else { ConfigureSpinner(); }
        }
    }
}