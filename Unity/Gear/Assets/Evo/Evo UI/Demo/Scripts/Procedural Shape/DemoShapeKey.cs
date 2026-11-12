using System;
using UnityEngine;
using Evo.Tween;

namespace Evo.UI.Demo
{
    /// <summary>
    /// A 3D key on the Shadows tab. The outer shadow, offset straight down with no softness, is the key's side,
    /// and the inner shadow presses it in. Hover lifts the face and grows the side; a press, or the switch being on,
    /// pushes the face down onto a thin side, so the bottom of the side never moves.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoShapeKey : MonoBehaviour
    {
        [SerializeField] private Switch key;
        [Tooltip("Moves with the key and holds its shape, so the switch's raycast area stays put.")]
        [SerializeField] private RectTransform face;
        [SerializeField] private ProceduralShape shape;

        [Header("Look")]
        [Tooltip("Side height while the key is up.")]
        [SerializeField, Min(0f)] private float height = 16f;
        [SerializeField, Min(0f)] private float softness;
        [Tooltip("Inner shadow offset while the key is pressed.")]
        [SerializeField, Min(0f)] private float depth = 8f;
        [SerializeField, Min(0f)] private float pressedSoftness = 10f;

        const float HoverLift = 3f;
        const float PressedSide = 0.4f;
        const float MinPressedSide = 2f;
        const float HoverDuration = 0.16f;
        const float PressDuration = 0.1f;
        const float ReleaseDuration = 0.28f;
        const int HoverKey = 1;
        const int PressKey = 2;

        Action<InteractionState> stateChanged;
        float hover, hoverGoal, press, pressGoal;
        bool hovered, pointerDown, dirty;
        TweenHandle hoverTween, pressTween;

        public float Height { get => height; set { height = value; Apply(); } }
        public float Softness { get => softness; set { softness = value; Apply(); } }
        public float Depth { get => depth; set { depth = value; Apply(); } }
        public float PressedSoftness { get => pressedSoftness; set { pressedSoftness = value; Apply(); } }

        void Awake() => stateChanged = OnStateChanged;

        void OnEnable()
        {
            key.OnStateChanged += stateChanged;
            hovered = pointerDown = dirty = false;
            hover = hoverGoal = 0f;
            press = pressGoal = key.IsOn ? 1f : 0f;
            Apply();
        }

        void OnDisable()
        {
            key.OnStateChanged -= stateChanged;
            hoverTween.Kill();
            pressTween.Kill();
        }

        // Called by the switch's On Value Changed
        public void SetOn(bool value) => dirty = true;

        void OnStateChanged(InteractionState state)
        {
            hovered = state == InteractionState.Highlighted || state == InteractionState.Pressed;
            pointerDown = state == InteractionState.Pressed;
            dirty = true;
        }

        // A click raises Pointer Up before the switch toggles, so the press settles once both have run
        void LateUpdate()
        {
            if (!dirty)
                return;

            dirty = false;
            float hoverTarget = hovered ? 1f : 0f;
            if (hoverTarget != hoverGoal)
            {
                hoverGoal = hoverTarget;
                hoverTween = EvoTween.Create(hover, hoverTarget, HoverDuration)
                    .WithEase(Ease.OutCubic)
                    .WithTimeMode(TimeMode.Unscaled)
                    .Bind(this, static (k, v) => k.SetHover(v), HoverKey);
            }

            float pressTarget = pointerDown || key.IsOn ? 1f : 0f;
            if (pressTarget != pressGoal)
            {
                pressGoal = pressTarget;
                bool release = pressTarget < press;
                pressTween = EvoTween.Create(press, pressTarget, release ? ReleaseDuration : PressDuration)
                    .WithEase(release ? Ease.OutBack : Ease.OutCubic)
                    .WithTimeMode(TimeMode.Unscaled)
                    .Bind(this, static (k, v) => k.SetPress(v), PressKey);
            }
        }

        void SetHover(float value)
        {
            hover = value;
            Apply();
        }

        void SetPress(float value)
        {
            press = value;
            Apply();
        }

        void Apply()
        {
            // The release overshoots below 0, which lifts the key a little past rest before it settles
            float pressed = Mathf.Min(height, Mathf.Max(MinPressedSide, height * PressedSide));
            float side = Mathf.LerpUnclamped(height + HoverLift * hover * (1f - press), pressed, press);
            float pressedIn = Mathf.Max(0f, press);

            face.anchoredPosition = new Vector2(face.anchoredPosition.x, side - height);
            shape.OuterShadowOffset = new Vector2(0f, -side);
            shape.OuterShadowSoftness = softness;
            shape.InnerShadowOffset = new Vector2(0f, -depth * pressedIn);
            shape.InnerShadowSoftness = pressedSoftness * pressedIn;
        }
    }
}
