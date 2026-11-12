using System;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Evo.Tween;

namespace Evo.UI
{
    /// <summary>
    /// Base class for all interactive UI elements with state management.
    /// Based on Unity's Selectable class with extended features.
    /// </summary>
    [NoAutoStaticsCleanup]
    [AddComponentMenu("Evo/UI/UI Elements/Interactive"), HelpURL(Constants.HelpUrl + "ui-elements/interactive")]
    public class Interactive : Selectable, IStylerHandler, IStylerInteractable, IPointerDownHandler,
        IPointerClickHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler, ISelectHandler,
        IDeselectHandler, ISubmitHandler, IPointerMoveHandler
    {
        // Styling
        [Tooltip("The provider that will manage the styling options of this object.")]
        public StyleProvider styleProvider = StyleProvider.Styler;
        [Tooltip("Locks the preset to the current default Styler Preset.")]
        public bool useDefaultStylerPreset = false;
        public StylerPreset stylerPreset;

        // Visual Effects
        public ScaleEffect scaleEffect = new();
        public Ripple.Settings ripple = new() { enabled = false };
        public Spotlight.Settings spotlight = new() { enabled = false };
        public BorderGlow.Settings borderGlow = new() { enabled = false };

        // Sound Effects
        public bool soundEffects = true;
        public AudioMapping highlightedSFX = new() { stylerID = "Hover SFX" };
        public AudioMapping pressedSFX = new() { stylerID = "Click SFX" };
        public AudioMapping selectedSFX = new() { stylerID = "" };

        // Cursor
        [Tooltip("States that override the cursor while the pointer is over this element. None disables cursor handling.")]
        public CursorStates cursorStates = CursorStates.None;
        public CursorSettings disabledCursor;
        public CursorSettings normalCursor;
        public CursorSettings highlightedCursor;
        public CursorSettings pressedCursor;
        public CursorSettings selectedCursor;

        // Settings
        [Tooltip("Ensures a raycast receiver is attached to this game object. " +
                 "Keep it disabled if there are child objects that can raycast.")]
        public bool resolveRaycast = true;
        [Tooltip("Sets the transition duration for linked Styler visuals.")]
        [SerializeField, Min(0f)] protected float transitionDuration = 0.1f;
        [SerializeField] protected InteractionState interactionState = InteractionState.Normal;

        // Unity Events
        public UnityEvent onClick = new();
        public UnityEvent onPointerDown = new();
        public UnityEvent onPointerUp = new();
        public UnityEvent onDoubleClick = new();
        public UnityEvent onPointerEnter = new();
        public UnityEvent onPointerExit = new();
        public UnityEvent onSelect = new();
        public UnityEvent onDeselect = new();
        public UnityEvent onSubmit = new();

        // Events
        public event Action<InteractionState> OnStateChanged;

        // Cache
        PointerEventData hoverEvent;
        protected bool isPointerOn;
        protected bool isPressedDown;
        protected bool waitingForDoubleClickInput;

        // Cursor cache
        static Interactive cursorOwner;
        static Texture2D activeCursorTexture;
        static Vector2 activeCursorHotspot;

        protected override void Start()
        {
            if (Application.isPlaying && resolveRaycast)
                RaycastArea.ResolveRaycast(gameObject);
        }

        protected override void OnEnable()
        {
            UpdateStyler();
            Styler.Register(this);

            isPointerOn = false;
            isPressedDown = false;
            waitingForDoubleClickInput = false;

            base.OnEnable();

            InteractionState initialState = interactionState switch
            {
                InteractionState.Selected => InteractionState.Selected,
                _ when !IsInteractable() => InteractionState.Disabled,
                InteractionState.Disabled => InteractionState.Normal,
                _ => interactionState
            };
            SetState(initialState, true);
        }

        protected override void OnDisable()
        {
            Styler.Unregister(this);
            isPointerOn = false;

            if (ReferenceEquals(cursorOwner, this))
                RefreshCursor();

            ripple.Clear();
            spotlight.Clear();
            borderGlow.Clear();
            scaleEffect.Clear();
            hoverEvent = null;

            base.OnDisable();

            if (IsInteractable() && interactionState != InteractionState.Selected)
                interactionState = InteractionState.Normal;

#if UNITY_EDITOR
            hasEditorPreviewState = false;
            if (updateEditorStateDelayCall != null)
                UnityEditor.EditorApplication.delayCall -= updateEditorStateDelayCall;
#endif
        }

        protected override void OnCanvasGroupChanged()
        {
            base.OnCanvasGroupChanged();

#if UNITY_EDITOR
            if (!Application.IsPlaying(gameObject))
            {
                UpdateEditorState();
                return;
            }
#endif

            if (Time.frameCount == 0 || interactionState == InteractionState.Selected)
                return;

            bool isInteractable = IsInteractable();
            if (!isInteractable && interactionState != InteractionState.Disabled)
                SetState(InteractionState.Disabled);
            else if (isInteractable && interactionState == InteractionState.Disabled &&
                (isPointerOn || Utilities.GetSelectedObject() == gameObject))
                SetState(InteractionState.Highlighted);
            else if (isInteractable && interactionState == InteractionState.Disabled)
                SetState(InteractionState.Normal);
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);

            // Do not overwrite persistent selection
            if (!Application.IsPlaying(gameObject) || !IsActive() || interactionState == InteractionState.Selected)
                return;

            if (state == SelectionState.Disabled)
                SetState(InteractionState.Disabled, instant);
            else if (interactionState == InteractionState.Disabled)
                SetState(isPointerOn ? InteractionState.Highlighted : InteractionState.Normal, instant);
        }

        public virtual void OnPointerClick(PointerEventData eventData)
        {
            if (!IsInteractable() || eventData.button != PointerEventData.InputButton.Left)
                return;

            onClick?.Invoke();
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            if (!IsActive() || !IsInteractable() || eventData.button != PointerEventData.InputButton.Left)
                return;

            isPressedDown = true;
            ripple.Create(targetGraphic, eventData);

            if (interactionState != InteractionState.Selected)
                SetState(InteractionState.Pressed);

            if (navigation.mode != Navigation.Mode.None)
                Utilities.SetSelectedObject(gameObject);

            if (soundEffects)
                AudioManager.PlayClip(Styler.GetAudio(styleProvider, pressedSFX, stylerPreset));

            onPointerDown?.Invoke();
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            isPressedDown = false;

            if (!IsInteractable())
                return;

            if (interactionState != InteractionState.Selected)
                SetState(isPointerOn ? InteractionState.Highlighted : InteractionState.Normal);

            onPointerUp?.Invoke();
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            isPointerOn = true;
            hoverEvent = eventData;

            if (!IsInteractable())
            {
                RefreshCursor();
                return;
            }

            if (interactionState != InteractionState.Selected && !isPressedDown)
                SetState(InteractionState.Highlighted);
            else
                RefreshCursor();

            spotlight.Show(targetGraphic, eventData);
            borderGlow.Show(targetGraphic, eventData);

            if (soundEffects)
                AudioManager.PlayClip(Styler.GetAudio(styleProvider, highlightedSFX, stylerPreset));
           
            onPointerEnter?.Invoke();
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            isPointerOn = false;

            if (ReferenceEquals(cursorOwner, this))
                RefreshCursor();

            spotlight.OnPointerExit();
            borderGlow.OnPointerExit();
            hoverEvent = null;

            if (!IsInteractable())
                return;

            if (interactionState != InteractionState.Selected && !isPressedDown)
                SetState(InteractionState.Normal);

            onPointerExit?.Invoke();
        }

        public virtual void OnPointerMove(PointerEventData eventData)
        {
            hoverEvent = eventData;
            spotlight.Move(eventData);
            borderGlow.Move(eventData);
        }

        public override void OnSelect(BaseEventData eventData)
        {
            if (!IsInteractable())
                return;

            onSelect?.Invoke();

            if (isPressedDown)
                return;

            if (interactionState != InteractionState.Selected)
                SetState(InteractionState.Highlighted);

            if (soundEffects)
                AudioManager.PlayClip(Styler.GetAudio(styleProvider, highlightedSFX, stylerPreset));
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            if (!IsInteractable())
                return;

            if (interactionState != InteractionState.Selected)
                SetState(InteractionState.Normal);

            onDeselect?.Invoke();
        }

        public virtual void OnSubmit(BaseEventData eventData)
        {
            if (!IsActive() || !IsInteractable())
                return;

            ripple.Create(targetGraphic);

            if (interactionState != InteractionState.Selected && Utilities.GetSelectedObject() != gameObject)
                SetState(InteractionState.Normal);

            if (soundEffects)
                AudioManager.PlayClip(Styler.GetAudio(styleProvider, pressedSFX, stylerPreset));

            onClick?.Invoke();
            onSubmit?.Invoke();
        }

        #region Public API
        /// <summary>
        /// Sets the interaction state and notifies linked Styler visuals.
        /// </summary>
        public virtual void SetState(InteractionState state, bool instant = false)
        {
            bool wasDisabled = interactionState == InteractionState.Disabled;
            bool stateChanged = interactionState != state;
            interactionState = state;

            RefreshCursor();

            if (!stateChanged && !instant)
                return;

            if (stateChanged)
                OnStateChanged?.Invoke(interactionState);

            if (Application.isPlaying)
            {
                if (state == InteractionState.Disabled)
                {
                    ripple.Clear();
                    spotlight.Clear();
                    borderGlow.Clear();
                }
                else if ((wasDisabled || instant) && IsActive() && IsInteractable())
                {
                    spotlight.UpdateVisibility(targetGraphic, isPointerOn, hoverEvent);
                    borderGlow.UpdateVisibility(targetGraphic, isPointerOn, hoverEvent);
                }
            }

            UpdateScaleEffect(instant);

            // State feedback is independent of Styler visual transitions
            if (Application.isPlaying && !instant && gameObject.activeInHierarchy)
            {
                if (state == InteractionState.Selected)
                    AudioManager.PlayClip(Styler.GetAudio(styleProvider, selectedSFX, stylerPreset));
            }

#if UNITY_EDITOR
            if (!Application.IsPlaying(gameObject))
                RefreshEditorInteractionVisuals();
#endif
        }

        /// <summary>
        /// Helper method to change interactable state and update visual state accordingly.
        /// </summary>
        public virtual void SetInteractable(bool value)
        {
            if (interactable == value)
                return;

            isPressedDown = false;
            interactable = value;

            bool isHighlighted = value && (isPointerOn || Utilities.GetSelectedObject() == gameObject);
            SetState(!value ? InteractionState.Disabled : isHighlighted ? InteractionState.Highlighted : InteractionState.Normal);
        }

        /// <summary>
        /// Refreshes the hovered cursor after changing cursor settings from code.
        /// Pointer events and interaction state changes call this automatically.
        /// </summary>
        public void RefreshCursor()
        {
            bool ownsCursor = ReferenceEquals(cursorOwner, this);
            if ((cursorStates == CursorStates.None || !isPointerOn) && !ownsCursor)
                return;

            bool isPlaying = Application.IsPlaying(gameObject);
            if (!isPlaying && !ownsCursor)
                return;

            // Resolve the closest hovered element so nested Interactives share one cursor
            Interactive nextOwner = null;
            GameObject hoveredObj = isPlaying ? hoverEvent?.pointerCurrentRaycast.gameObject : null;
            for (Transform current = hoveredObj != null ? hoveredObj.transform : null; current != null; current = current.parent)
            {
                if (current.TryGetComponent(out Interactive candidate) && candidate.isPointerOn &&
                    candidate.cursorStates != CursorStates.None && candidate.IsActive())
                {
                    nextOwner = candidate;
                    break;
                }
            }

            // Only the owner can release the global cursor
            if (nextOwner == null && !ownsCursor)
                return;

            CursorSettings settings = nextOwner != null ? nextOwner.GetCursorSettings() : default;
            Texture2D texture = settings.texture != null ? settings.texture : null;
            Vector2 hotspot = texture != null
                ? new Vector2(Mathf.Clamp(settings.hotspot.x, 0f, texture.width - 1f),
                    Mathf.Clamp(settings.hotspot.y, 0f, texture.height - 1f))
                : Vector2.zero;

            cursorOwner = nextOwner;

            if (ReferenceEquals(activeCursorTexture, texture) && activeCursorHotspot == hotspot)
                return;

            activeCursorTexture = texture;
            activeCursorHotspot = hotspot;
            Cursor.SetCursor(texture, hotspot, CursorMode.Auto);
        }
        #endregion

        #region Helpers
        CursorSettings GetCursorSettings()
        {
            if ((cursorStates & (CursorStates)(1 << (int)interactionState)) == 0)
                return default;

            return interactionState switch
            {
                InteractionState.Disabled => disabledCursor,
                InteractionState.Normal => normalCursor,
                InteractionState.Highlighted => highlightedCursor,
                InteractionState.Pressed => pressedCursor,
                InteractionState.Selected => selectedCursor,
                _ => default
            };
        }

        void UpdateScaleEffect(bool instant)
        {
            // Scale changes would be saved into scenes and prefabs outside play mode
            if (Application.IsPlaying(gameObject) && IsActive())
                scaleEffect.Apply(transform, interactionState, instant);
        }
        #endregion

        #region Scale Effect
        /// <summary>
        /// Per-state multipliers of the target's rest scale, animated with Evo Tween in play mode.
        /// </summary>
        [Serializable]
        public sealed class ScaleEffect
        {
            public bool enabled = false;
            [Tooltip("Transform to scale. If empty, uses this object.")]
            public Transform target;
            [Min(0f)] public float duration = 0.15f;
            public Ease ease = Ease.OutCubic;
            public AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

            // Multipliers
            [Range(0f, 2f)] public float disabled = 1f;
            [Range(0f, 2f)] public float normal = 1f;
            [Range(0f, 2f)] public float highlighted = 1.05f;
            [Range(0f, 2f)] public float pressed = 0.98f;
            [Range(0f, 2f)] public float selected = 1f;

            // State
            [NonSerialized] Transform activeTarget;
            [NonSerialized] Vector3 restScale;
            [NonSerialized] TweenHandle tween;

            public float GetMultiplier(InteractionState state) => state switch
            {
                InteractionState.Disabled => disabled,
                InteractionState.Highlighted => highlighted,
                InteractionState.Pressed => pressed,
                InteractionState.Selected => selected,
                _ => normal
            };

            /// <summary>
            /// Scales the target for a state. The rest scale is read on first use and kept until <see cref="Clear"/>.
            /// </summary>
            internal void Apply(Transform fallback, InteractionState state, bool instant)
            {
                Transform next = target != null ? target : fallback;
                if (!enabled || activeTarget != next)
                    Clear();

                if (!enabled || next == null)
                    return;

                if (activeTarget == null)
                {
                    activeTarget = next;
                    restScale = next.localScale;
                }

                // Retargets from the displayed scale, so interrupted states never jump
                tween.Kill();
                tween = default;

                // Depth stays at rest: flat UI only needs X and Y, and z scale resizes shape shadows on explicit materials
                float multiplier = GetMultiplier(state);
                Vector3 current = next.localScale;
                Vector3 scale = new(restScale.x * multiplier, restScale.y * multiplier, restScale.z);
                if (current == scale)
                    return;

                if (instant || duration <= 0f) { next.localScale = scale; }
                else
                {
                    tween = EvoTween.Create(current, scale, duration)
                        .WithEase(ease, curve)
                        .WithTimeMode(TimeMode.Unscaled)
                        .BindToScale(next);
                }
            }

            /// <summary>
            /// Stops the animation and restores the rest scale.
            /// </summary>
            public void Clear()
            {
                tween.Kill();
                tween = default;

                if (activeTarget != null && activeTarget.localScale != restScale)
                    activeTarget.localScale = restScale;

                activeTarget = null;
            }
        }
        #endregion

        #region IStylerHandler Implementation
        public StylerPreset Preset
        {
            get => stylerPreset;
            set
            {
                if (stylerPreset == value)
                    return;

                stylerPreset = value;
                UpdateStyler();
            }
        }

        public void UpdateStyler()
        {
            Styler.SyncDefaultPreset(ref stylerPreset, styleProvider, useDefaultStylerPreset);
            ripple.UpdateStyler(styleProvider, stylerPreset);
            spotlight.UpdateStyler(styleProvider, stylerPreset);
            borderGlow.UpdateStyler(styleProvider, stylerPreset);
        }
        #endregion

        #region IStylerInteractable Implementation
        public InteractionState InteractionState
        {
            get => interactionState;
            set => SetState(value);
        }

        public float TransitionDuration
        {
            get => transitionDuration;
            set => transitionDuration = value;
        }
        #endregion

#if UNITY_EDITOR
        bool hasEditorPreviewState;
        InteractionState lastEditorPreviewState;
        List<IStylerHandler> editorStylerHandlersCache;
        UnityEditor.EditorApplication.CallbackFunction updateEditorStateDelayCall;

        protected override void OnValidate()
        {
            UpdateStyler();
            ripple.Validate(this);
            spotlight.Validate(this);
            borderGlow.Validate(this);
            base.OnValidate();
            RefreshCursor();
            UpdateEditorState();
            UpdateScaleEffect(true);
        }

        protected override void Reset()
        {
            base.Reset();
            useDefaultStylerPreset = true;
            Styler.SyncDefaultPreset(ref stylerPreset, styleProvider, useDefaultStylerPreset);
        }

        protected void UpdateEditorState()
        {
            if (Application.IsPlaying(gameObject))
                return;

            updateEditorStateDelayCall ??= ApplyEditorState;
            UnityEditor.EditorApplication.delayCall -= updateEditorStateDelayCall;
            UnityEditor.EditorApplication.delayCall += updateEditorStateDelayCall;
        }

        void ApplyEditorState()
        {
            UnityEditor.EditorApplication.delayCall -= updateEditorStateDelayCall;

            if (this == null || Application.IsPlaying(gameObject))
                return;

            InteractionState state = interactionState;
            if (state != InteractionState.Selected)
            {
                if (!IsInteractable())
                    state = InteractionState.Disabled;
                else if (state == InteractionState.Disabled)
                    state = InteractionState.Normal;
            }

            SetState(state, true);
        }

        void RefreshEditorInteractionVisuals()
        {
            if (!gameObject.activeInHierarchy || (hasEditorPreviewState && lastEditorPreviewState == interactionState))
                return;

            lastEditorPreviewState = interactionState;
            hasEditorPreviewState = true;

            // Refresh only the linked visuals when their editor state changes
            editorStylerHandlersCache ??= new List<IStylerHandler>(8);
            GetComponentsInChildren(true, editorStylerHandlersCache);
            try
            {
                for (int i = 0; i < editorStylerHandlersCache.Count; i++)
                {
                    switch (editorStylerHandlersCache[i])
                    {
                        case StylerObject styler when styler != null && styler.enableInteraction && styler.interactableObject == this:
                            styler.UpdateStyler();
                            break;
                        case ProceduralShape shape when shape != null && shape.interactableObject == this:
                            shape.UpdateStyler();
                            break;
                    }
                }
            }
            finally
            {
                editorStylerHandlersCache.Clear();
            }
        }
#endif
    }
}