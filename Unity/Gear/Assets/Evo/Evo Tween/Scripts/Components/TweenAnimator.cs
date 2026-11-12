using System;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using Evo.Tween.Diagnostics;

namespace Evo.Tween
{
    /// <summary>
    /// Plays named animation states made from portable <see cref="TweenClip"/> instances.
    /// </summary>
    [DisallowMultipleComponent, NoAutoStaticsCleanup]
    [AddComponentMenu("Evo/Tween/Tween Animator"), HelpURL("https://evo.michsky.com/docs/evo-tween/tween-animator")]
    public class TweenAnimator : MonoBehaviour, ITweenInstance
    {
        #region Definitions
        public enum TriggerMode { Manual, PointerEvent, Interaction }

        public enum PointerButton { Any, Left, Right, Middle }

        [Flags]
        public enum PointerTrigger
        {
            None = 0, Enter = 1, Exit = 2, Down = 4, Up = 8, Click = 16,
            Select = 32, Deselect = 64, Submit = 128, Cancel = 256
        }

        [Flags]
        public enum InteractionTrigger
        {
            None = 0, Normal = 1, Highlighted = 2, Pressed = 4, Selected = 8, Disabled = 16
        }

        [Serializable]
        public class AnimationState
        {
            [Tooltip("Unique, case-sensitive ID used by Play and Trigger.")]
            public string id = "Default";
            public TriggerMode trigger;
            public PointerTrigger pointerEvents = PointerTrigger.Enter;
            public PointerButton pointerButton = PointerButton.Left;
            public InteractionTrigger interactionStates = InteractionTrigger.Normal;
            [Tooltip("Restart when a trigger selects the current state again.")]
            public bool restartOnTrigger = true;
            [Tooltip("Playback behavior after a timeline iteration finishes.")]
            public LoopMode loop;
            [Min(0), Tooltip("Total sequence iterations. Zero means infinite when looping.")]
            public int iterations = 1;
            [Min(0f)] public float playbackSpeed = 1f;
            public UpdatePhase updatePhase = UpdatePhase.Update;
            [Tooltip("Restore this state's captured values when stopped or disabled. State changes preserve the current values.")]
            public bool restoreOnStop = true;
            public TweenSequence sequence = new();
        }
        #endregion

        #region Configuration
        [Tooltip("Play the default state whenever this component is enabled.")]
        public bool playOnEnable = true;
        [SerializeField] private int defaultState;
        [SerializeField] private List<AnimationState> states = new() { new AnimationState() };
        [Tooltip("Component implementing IStylerInteractable. Used by Interaction triggers.")]
        public Component interactionSource;

        public static Func<TweenAnimator, IInteractionBinding> InteractionBindingFactory { private get; set; }
        #endregion

        #region Runtime State
        TweenSequenceSampler sampler;
        TweenSequenceSampler[] stateSamplers;
        AnimationState activeState;
        HoverEvents hoverEvents;
        PressEvents pressEvents;
        SelectionEvents selectionEvents;
        IInteractionBinding interactionBinding;
        PlaybackState state;
        TweenClock clock;
        float currentTime;
        bool registered;
        int playbackVersion;
        int activeStateIndex = -1;
        int schedulerIndex = -1;
        int scheduledPhase = -1;
        #endregion

        #region Properties
        public bool IsPlaying => state == PlaybackState.Playing;
        public bool IsPaused => state == PlaybackState.Paused;
        public bool HasCapturedValues => sampler?.HasCapturedValues == true;
        public float CurrentTime => currentTime;
        public float NormalizedTime => Duration > 0f ? Mathf.Clamp01(currentTime / Duration) : 0f;
        public float Duration => HasCapturedValues ? sampler.Duration : Sequence?.Duration ?? 0f;
        public int DefaultStateIndex => defaultState;
        public int ActiveStateIndex => activeStateIndex;
        public PlaybackState State => state;
        public IReadOnlyList<AnimationState> States => states;
        public AnimationState ActiveState => activeState;
        public TweenSequence Sequence => CurrentState?.sequence;

        bool RestoreOnStop => CurrentState?.restoreOnStop == true;
        AnimationState CurrentState => activeState ?? GetState(defaultState);
        #endregion

        #region Events
        public event Action<TweenAnimator> Started;
        public event Action<TweenAnimator> Paused;
        public event Action<TweenAnimator> Resumed;
        public event Action<TweenAnimator> Stopped;
        public event Action<TweenAnimator> Completed;
        public event Action<TweenAnimator, float> Updated;

        void InvokeEvent(Action<TweenAnimator> callback)
        {
            if (callback == null)
                return;

            try { callback(this); }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }

        void InvokeUpdated(float normalizedTime)
        {
            if (Updated == null)
                return;

            try { Updated(this, normalizedTime); }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }
        #endregion

        #region Unity Lifecycle
        void Awake() => PrepareStates();

        void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            int version = playbackVersion;
            RebindTriggers();

            if (playOnEnable && version == playbackVersion)
                Play();
        }

        void OnDisable()
        {
            SetPointerHelpers(PointerTrigger.None);
            interactionBinding?.Unbind();

            if (Application.isPlaying)
                Stop();
        }

        void OnDestroy()
        {
            AdvancePlaybackVersion();
            state = PlaybackState.Stopped;
            registered = false;
            RestoreCapturedValues();
            interactionBinding?.Unbind();
            interactionBinding = null;
           
            DestroyPointerHelper(hoverEvents);
            DestroyPointerHelper(pressEvents);
            DestroyPointerHelper(selectionEvents);
            DisposeSamplers();

            Started = null;
            Paused = null;
            Resumed = null;
            Stopped = null;
            Completed = null;
            Updated = null;
#if UNITY_EDITOR
            EditorStateChanged = null;
#endif
        }
        #endregion

        #region State Management
        /// <summary>
        /// Gets a state by index, or null if the index is invalid.
        /// </summary>
        public AnimationState GetState(int index) => states != null && (uint)index < (uint)states.Count ? states[index] : null;

        /// <summary>
        /// Resolves a case-sensitive state ID without hashing, boxing, or temporary allocations.
        /// Cache the returned index when calling from a hot loop. Duplicate IDs resolve to the first state.
        /// </summary>
        public int GetStateIndex(string id)
        {
            if (string.IsNullOrEmpty(id) || states == null)
                return -1;

            for (int i = 0; i < states.Count; i++)
            {
                if (states[i] != null && string.Equals(states[i].id, id, StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Prepares reusable clip data for every state. Call again after changing state definitions at runtime.
        /// Preparation may allocate; playback and triggers reuse this storage. Target values are not sampled.
        /// </summary>
        public void PrepareStates()
        {
            bool notify = IsPlaying || IsPaused;
            AdvancePlaybackVersion();
            state = PlaybackState.Stopped;
            currentTime = 0f;
         
            if (RestoreOnStop) { RestoreCapturedValues(); }
            else { ReleaseCapturedValues(); }
           
            DisposeSamplers();
            activeState = null;
            activeStateIndex = -1;
            int count = states?.Count ?? 0;
            stateSamplers = new TweenSequenceSampler[count];
            for (int i = 0; i < count; i++)
            {
                if (states[i]?.sequence == null)
                    continue;

                TweenSequenceSampler prepared = new(gameObject, states[i].sequence, cloneInlineClips: false, cacheClips: true);
                stateSamplers[i] = prepared;
                prepared.Prepare();
            }

            if (notify)
                InvokeEvent(Stopped);
        }

        /// <summary>
        /// Rebuilds one edited state. Active playback retains its normalized position.
        /// </summary>
        public void RefreshState(int index)
        {
            AnimationState item = GetState(index);
            if (item?.sequence == null)
                return;

            if (stateSamplers == null || stateSamplers.Length != states.Count)
            {
                PrepareStates();
                return;
            }

            if (index == activeStateIndex && HasCapturedValues)
            {
                Refresh(NormalizedTime);
                return;
            }

            TweenSequenceSampler prepared = stateSamplers[index] ??= new TweenSequenceSampler(gameObject, item.sequence, cloneInlineClips: false, cacheClips: true);
            prepared.SetSource(gameObject, item.sequence);
            prepared.Prepare(rebuild: true);
        }
        #endregion

        #region Triggers
        /// <summary>
        /// Reconnects an edited interaction source. Subscriptions exist only while this component is enabled.
        /// </summary>
        public void RebindTriggers()
        {
            interactionBinding?.Unbind();

            if (!Application.isPlaying || !isActiveAndEnabled || states == null)
                return;

            PointerTrigger receivedEvents = PointerTrigger.None;
            for (int i = 0; i < states.Count; i++)
            {
                if (states[i]?.trigger == TriggerMode.PointerEvent)
                    receivedEvents |= states[i].pointerEvents;
            }

            SetPointerHelpers(receivedEvents);

            if (interactionSource == null)
                return;

            for (int i = 0; i < states.Count; i++)
            {
                if (states[i]?.trigger != TriggerMode.Interaction)
                    continue;

                interactionBinding ??= InteractionBindingFactory?.Invoke(this);
                interactionBinding?.Bind(interactionSource);
                return;
            }
        }

        void SetPointerHelpers(PointerTrigger events)
        {
            hoverEvents = EnsurePointerHelper(hoverEvents, events, PointerTrigger.Enter | PointerTrigger.Exit);
            pressEvents = EnsurePointerHelper(pressEvents, events, PointerTrigger.Down | PointerTrigger.Up | PointerTrigger.Click);
            selectionEvents = EnsurePointerHelper(selectionEvents, events,
                PointerTrigger.Select | PointerTrigger.Deselect | PointerTrigger.Submit | PointerTrigger.Cancel);
        }

        T EnsurePointerHelper<T>(T helper, PointerTrigger events, PointerTrigger group) where T : PointerEventsBase
        {
            bool needed = (events & group) != 0;
            if (needed && helper == null)
            {
                helper = gameObject.AddComponent<T>();
                helper.hideFlags = HideFlags.HideInInspector | HideFlags.DontSave;
                helper.owner = this;
            }

            if (helper != null)
                helper.enabled = needed;

            return helper;
        }

        static void DestroyPointerHelper(PointerEventsBase helper)
        {
            if (helper == null)
                return;

            helper.enabled = false;
            helper.owner = null;
            Destroy(helper);
        }

        /// <summary>
        /// Enters a state by ID, respecting that state's restart-on-trigger setting.
        /// </summary>
        public void Trigger(string id) => Trigger(GetStateIndex(id));

        public void Trigger(int index)
        {
            AnimationState next = GetState(index);
            if (next == null || (activeState == next && (state == PlaybackState.Playing || state == PlaybackState.Paused) && !next.restartOnTrigger))
                return;

            Play(index);
        }

        /// <summary>
        /// Dispatches an interaction notification from an optional integration module.
        /// The first matching state wins, so one event never plays several states in succession.
        /// </summary>
        public void TriggerInteraction(InteractionTrigger interaction)
        {
            if (states == null)
                return;

            for (int i = 0; i < states.Count; i++)
            {
                AnimationState item = states[i];
                if (item == null || item.trigger != TriggerMode.Interaction || (item.interactionStates & interaction) == 0)
                    continue;

                Trigger(i);
                return;
            }
        }

        internal void TriggerPointer(PointerTrigger pointer, PointerEventData eventData = null)
        {
            if (states == null)
                return;

            bool checkButton = (pointer & (PointerTrigger.Down | PointerTrigger.Up | PointerTrigger.Click)) != 0;
            for (int i = 0; i < states.Count; i++)
            {
                AnimationState item = states[i];
                if (item == null || item.trigger != TriggerMode.PointerEvent || (item.pointerEvents & pointer) == 0)
                    continue;
                if (checkButton && eventData != null && item.pointerButton != PointerButton.Any && (int)item.pointerButton - 1 != (int)eventData.button)
                    continue;

                Trigger(i);
                return;
            }
        }
        #endregion

        #region Playback
        /// <summary>
        /// Restarts the timeline from the beginning.
        /// </summary>
        public void Play() => Play(defaultState);

        /// <summary>
        /// Restarts the requested state, regardless of its automatic trigger mode.
        /// </summary>
        public void Play(string id) => Play(GetStateIndex(id));

        public void Play(int index)
        {
            AnimationState next = GetState(index);
            if (!Application.isPlaying || !isActiveAndEnabled || next?.sequence == null)
                return;

            if (stateSamplers == null || stateSamplers.Length != states.Count)
                PrepareStates();

            AdvancePlaybackVersion();
            ReleaseCapturedValues(); // The next clips must capture the displayed values, including when restarting this state

            activeState = next;
            activeStateIndex = index;
            sampler = stateSamplers[index];
            CaptureAnimations();
            currentTime = 0f;
            state = PlaybackState.Playing;
            clock.Reset(0f, Duration, next.playbackSpeed, next.iterations, next.loop);

            if (!RegisterIfRequired())
            {
                RestoreCapturedValues();
                return;
            }

            InvokeEvent(Started);
        }

        /// <summary>
        /// Restarts the active state, or the default state before the first run.
        /// </summary>
        public void Restart() => Play(activeStateIndex >= 0 ? activeStateIndex : defaultState);

        /// <summary>
        /// Pauses playback without changing the sampled values.
        /// </summary>
        public void Pause()
        {
            if (state != PlaybackState.Playing)
                return;

            state = PlaybackState.Paused;
            InvokeEvent(Paused);
        }

        /// <summary>
        /// Resumes paused playback.
        /// </summary>
        public void Resume()
        {
            if (state != PlaybackState.Paused)
                return;

            state = PlaybackState.Playing;
            InvokeEvent(Resumed);
        }

        /// <summary>
        /// Pauses, resumes, or starts the clip according to its current state.
        /// </summary>
        public void TogglePause()
        {
            if (state == PlaybackState.Playing) { Pause(); }
            else if (state == PlaybackState.Paused) { Resume(); }
            else { Restart(); }
        }

        /// <summary>
        /// Stops playback using the configured restore behavior.
        /// </summary>
        public void Stop() => Stop(RestoreOnStop);

        /// <summary>
        /// Stops playback and optionally restores captured target values.
        /// </summary>
        public void Stop(bool restore)
        {
            bool notify = state == PlaybackState.Playing || state == PlaybackState.Paused;
            AdvancePlaybackVersion();
            state = PlaybackState.Stopped;
            currentTime = 0f;

            if (restore) { RestoreCapturedValues(); }
            else { ReleaseCapturedValues(); }

            if (notify)
                InvokeEvent(Stopped);
        }

        /// <summary>
        /// Samples the timeline end and completes active playback immediately.
        /// </summary>
        public void Complete()
        {
            if (state != PlaybackState.Playing && state != PlaybackState.Paused)
                return;

            // Same position as natural completion, which ends at the start after an even number of Yoyo iterations
            AdvancePlaybackVersion();
            EvaluateTo(Duration * clock.FinalNormalizedTime);
            CompletePlayback();
        }

        void CompletePlayback()
        {
            state = PlaybackState.Completed;
            ReleaseCapturedValues();
            InvokeEvent(Completed);
        }

        void AdvancePlaybackVersion() => playbackVersion = unchecked(playbackVersion + 1);
        #endregion

        #region Sampling
        /// <summary>
        /// Captures target values without starting playback. Used by manual sampling and editor preview.
        /// </summary>
        public void Capture()
        {
            if (HasCapturedValues)
                return;

            CaptureAnimations();
            currentTime = 0f;
        }

        /// <summary>
        /// Samples the timeline at an absolute time. Call Restore after manual sampling.
        /// </summary>
        public void Sample(float time)
        {
            if (!HasCapturedValues)
                Capture();

            EvaluateTo(time);

            if (state == PlaybackState.Playing || state == PlaybackState.Paused)
            {
                float duration = Duration;
                float normalized = duration > 0f ? currentTime / duration : 0f;
                AdvancePlaybackVersion();
                clock.Seek(normalized, clock.IsForward, clock.CompletedIterations);
            }
        }

        /// <summary>
        /// Restores every captured target to the value it had before playback or sampling.
        /// </summary>
        public void Restore()
        {
            RestoreCapturedValues();
            currentTime = 0f;
        }

        /// <summary>
        /// Rebuilds edited track data while preserving the current normalized timeline position.
        /// </summary>
        public void Refresh(float normalizedTime)
        {
            AdvancePlaybackVersion();
            bool preserveClock = state == PlaybackState.Playing || state == PlaybackState.Paused;
            bool forward = !preserveClock || clock.IsForward;
            int completedIterations = preserveClock ? clock.CompletedIterations : 0;
            float normalized = Mathf.Clamp01(normalizedTime);
            GetSampler().Refresh(normalized);
            currentTime = GetSampler().CurrentTime;

            AnimationState settings = CurrentState;
            if (settings == null)
                return;

            clock.Reset(0f, Duration, settings.playbackSpeed, settings.iterations, settings.loop);
            clock.Seek(normalized, forward, completedIterations);
            RegisterIfRequired();
        }

        void CaptureAnimations()
        {
            TweenSequenceSampler activeSampler = GetSampler();
            activeSampler.Restore();
            activeSampler.Capture();
        }

        void RestoreCapturedValues() => sampler?.Restore();

        void ReleaseCapturedValues() => sampler?.Release();

        void EvaluateTo(float time)
        {
            if (!HasCapturedValues)
                return;

            TweenSequenceSampler activeSampler = GetSampler();
            activeSampler.Sample(time);
            currentTime = activeSampler.CurrentTime;
        }

        void DisposeSamplers()
        {
            sampler?.Dispose();
            sampler = null;

            if (stateSamplers == null)
                return;

            for (int i = 0; i < stateSamplers.Length; i++)
                stateSamplers[i]?.Dispose();

            stateSamplers = null;
        }

        TweenSequenceSampler GetSampler()
        {
            int index = activeStateIndex >= 0 ? activeStateIndex : defaultState;
            if (sampler == null && stateSamplers != null && (uint)index < (uint)stateSamplers.Length)
                sampler = stateSamplers[index];

            sampler ??= new TweenSequenceSampler(gameObject, Sequence, cloneInlineClips: false);
            sampler.SetSource(gameObject, Sequence);
            return sampler;
        }
        #endregion

        #region ITweenInstance Implementation
        public UpdatePhase UpdatePhase => CurrentState?.updatePhase ?? UpdatePhase.Update;

        int ITweenInstance.SchedulerIndex { get => schedulerIndex; set => schedulerIndex = value; }
        int ITweenInstance.ScheduledPhase { get => scheduledPhase; set => scheduledPhase = value; }

        bool ITweenInstance.IsAlive => (state == PlaybackState.Playing || state == PlaybackState.Paused) && (sampler == null || sampler.HasLiveTargets);

        bool ITweenInstance.Tick(float deltaTime, float unscaledDeltaTime)
        {
            if (state == PlaybackState.Stopped || state == PlaybackState.Completed)
                return false;

            if (sampler != null && !sampler.HasLiveTargets)
            {
                Stop(false);
                return IsPlaying || IsPaused;
            }

            if (state == PlaybackState.Paused)
                return true;

            int version = playbackVersion;
            clock.SetSpeed(CurrentState.playbackSpeed);
            float delta = Sequence.TimeMode == TimeMode.Unscaled ? unscaledDeltaTime : deltaTime;
            bool completed = clock.Tick(delta, out float normalizedTime);

            EvaluateTo(Duration * normalizedTime);
            InvokeUpdated(normalizedTime);

            if (version != playbackVersion || state != PlaybackState.Playing)
                return state == PlaybackState.Playing || state == PlaybackState.Paused;
            if (!completed)
                return true;

            CompletePlayback();

            // A completion listener is allowed to restart this animator immediately
            return state == PlaybackState.Playing || state == PlaybackState.Paused;
        }

        void ITweenInstance.OnRemoved()
        {
            registered = false;

            if (state != PlaybackState.Playing && state != PlaybackState.Paused)
                return;

            AdvancePlaybackVersion();
            state = PlaybackState.Stopped;
            currentTime = 0f;

            if (RestoreOnStop) { RestoreCapturedValues(); }
            else { ReleaseCapturedValues(); }
        }

        bool RegisterIfRequired()
        {
            if (registered)
            {
                // The active state may advance in another update phase
                TweenScheduler.NotifyPhaseChanged(this);
                return true;
            }
            if (state != PlaybackState.Playing && state != PlaybackState.Paused)
                return false;

            registered = TweenScheduler.Add(this);
            if (!registered)
                state = PlaybackState.Stopped;

            return registered;
        }

        TweenDebugInfo ITweenInstance.GetDebugInfo()
        {
            return new TweenDebugInfo("Tween Animator", this, IsPaused, UpdatePhase, Sequence?.TimeMode ?? TimeMode.Scaled, currentTime,
                Duration, clock.DelayRemaining, clock.Speed, clock.CompletedIterations, clock.Iterations, clock.Loop, 0L);
        }
        #endregion

#if UNITY_EDITOR
        [SerializeField, HideInInspector] int editorStateIndex;
        public event Action EditorStateChanged;
        public int EditorStateIndex
        {
            get => states == null || states.Count == 0 ? -1 : Mathf.Clamp(editorStateIndex, 0, states.Count - 1);
            set
            {
                if (editorStateIndex == value)
                    return;

                editorStateIndex = value;
                EditorStateChanged?.Invoke();
            }
        }

        void OnValidate()
        {
            if (states == null)
                return;

            defaultState = Mathf.Clamp(defaultState, 0, Mathf.Max(0, states.Count - 1));

            for (int i = 0; i < states.Count; i++)
            {
                AnimationState item = states[i];
                if (item == null)
                    continue;

                item.playbackSpeed = TweenMath.SanitizeNonNegative(item.playbackSpeed);
                item.iterations = Mathf.Max(0, item.iterations);
                item.sequence?.Validate();
            }
        }
#endif
    }

    /// <summary>
    /// Added only for pointer-triggered animators, so manual animations do not intercept UI events.
    /// Each event group has its own helper: hover-only states leave presses and selection to parent controls.
    /// </summary>
    abstract class PointerEventsBase : MonoBehaviour
    {
        internal TweenAnimator owner;

        protected void Forward(TweenAnimator.PointerTrigger pointer, PointerEventData eventData = null)
        {
            if (owner != null)
                owner.TriggerPointer(pointer, eventData);
        }
    }

    [AddComponentMenu("")]
    sealed class HoverEvents : PointerEventsBase, IPointerEnterHandler, IPointerExitHandler
    {
        void IPointerEnterHandler.OnPointerEnter(PointerEventData data) => Forward(TweenAnimator.PointerTrigger.Enter);
        void IPointerExitHandler.OnPointerExit(PointerEventData data) => Forward(TweenAnimator.PointerTrigger.Exit);
    }

    [AddComponentMenu("")]
    sealed class PressEvents : PointerEventsBase, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        void IPointerDownHandler.OnPointerDown(PointerEventData data) => Forward(TweenAnimator.PointerTrigger.Down, data);
        void IPointerUpHandler.OnPointerUp(PointerEventData data) => Forward(TweenAnimator.PointerTrigger.Up, data);
        void IPointerClickHandler.OnPointerClick(PointerEventData data) => Forward(TweenAnimator.PointerTrigger.Click, data);
    }

    [AddComponentMenu("")]
    sealed class SelectionEvents : PointerEventsBase, ISelectHandler, IDeselectHandler, ISubmitHandler, ICancelHandler
    {
        void ISelectHandler.OnSelect(BaseEventData data) => Forward(TweenAnimator.PointerTrigger.Select);
        void IDeselectHandler.OnDeselect(BaseEventData data) => Forward(TweenAnimator.PointerTrigger.Deselect);
        void ISubmitHandler.OnSubmit(BaseEventData data) => Forward(TweenAnimator.PointerTrigger.Submit);
        void ICancelHandler.OnCancel(BaseEventData data) => Forward(TweenAnimator.PointerTrigger.Cancel);
    }

    /// <summary>
    /// Optional module bridge for state interaction.
    /// </summary>
    public interface IInteractionBinding
    {
        void Bind(Component source);
        void Unbind();
    }
}