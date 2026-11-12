using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;
using Evo.EditorTools;

namespace Evo.Tween.Editor
{
    /// <summary>
    /// Reusable preview controls for standalone and embedded tween sequences.
    /// </summary>
    [NoAutoStaticsCleanup]
    public sealed class TweenSequencePreviewGUI : IDisposable
    {
        static TweenSequencePreviewGUI activeAnimatorPreview;
        static readonly List<TweenSequencePreviewGUI> openPreviews = new();
        readonly Action repaint;
        readonly Component owner;
        readonly Func<TweenSequence> getSequence;
        readonly TweenAnimator animator;
        readonly bool showTimelineButton;
        readonly int animatorStateIndex = -1;
        string sequencePropertyPath;
        bool sequencePropertyResolved;

        GUIStyle timelineLabelStyle;
        Texture2D playIcon;
        Texture2D pauseIcon;
        Texture2D stopIcon;
        Texture2D timelineIcon;

        TweenSequenceSampler sampler;
        bool previewPlaying;
        bool liveEditActive;
        bool recaptureAfterEdit;
        float editNormalizedTime;
        double lastPreviewUpdate;
        bool disposed;

        bool UsesAnimator => animator != null && Application.isPlaying && animator.ActiveStateIndex == animatorStateIndex;
        public bool HasCapturedValues => UsesAnimator ? animator.HasCapturedValues : sampler?.HasCapturedValues == true;
        public bool IsPlaying => UsesAnimator ? animator.IsPlaying : previewPlaying;
        public float CurrentTime => UsesAnimator ? animator.CurrentTime : sampler?.CurrentTime ?? 0f;
        public float Duration => UsesAnimator ? animator.Duration : sampler?.Duration ?? getSequence()?.Duration ?? 0f;
        public Action BeforePreview { get; set; }

        /// <summary>
        /// Wraps edit-mode preview playback back to the start instead of stopping at the end.
        /// </summary>
        public bool Loop { get; set; }

        TweenSequencePreviewGUI(Component owner, Func<TweenSequence> getSequence, Action repaint,string seqPropPath, bool showTimelineButton)
        {
            this.owner = owner != null ? owner : throw new ArgumentNullException(nameof(owner));
            this.getSequence = getSequence ?? throw new ArgumentNullException(nameof(getSequence));
            this.repaint = repaint ?? throw new ArgumentNullException(nameof(repaint));
            this.sequencePropertyPath = seqPropPath;
            this.showTimelineButton = showTimelineButton;
            sequencePropertyResolved = !showTimelineButton || !string.IsNullOrEmpty(sequencePropertyPath);

            openPreviews.Add(this);
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        /// <summary>
        /// Stops and restores every open preview of the owner's sequences, including the Tween Timeline's.
        /// </summary>
        public static void StopPreviews(Component owner)
        {
            for (int i = openPreviews.Count - 1; i >= 0; i--)
            {
                if (openPreviews[i].owner == owner)
                    openPreviews[i].StopPreview();
            }
        }

        /// <summary>
        /// Creates preview controls for a component-owned sequence. The timeline button is shown by default
        /// when the sequence maps to a directly serialized field.
        /// </summary>
        public TweenSequencePreviewGUI(UnityEditor.Editor editor, Component owner,
            Func<TweenSequence> getSequence, string sequencePropertyPath = null, bool showTimelineButton = true)
            : this(owner, getSequence, GetRepaintAction(editor), sequencePropertyPath, showTimelineButton) { }

        /// <summary>
        /// Creates preview controls hosted by an editor window.
        /// </summary>
        public TweenSequencePreviewGUI(EditorWindow window, Component owner,
            Func<TweenSequence> getSequence, string sequencePropertyPath = null, bool showTimelineButton = true)
            : this(owner, getSequence, GetRepaintAction(window), sequencePropertyPath, showTimelineButton) { }

        /// <summary>
        /// Creates preview controls backed by a standalone TweenAnimator.
        /// </summary>
        public TweenSequencePreviewGUI(UnityEditor.Editor editor, TweenAnimator animator, int stateIndex, bool showTimelineButton = true)
            : this(editor, animator, () => animator != null ? animator.GetState(stateIndex)?.sequence : null,
                  TweenAnimatorEditor.GetSequencePath(stateIndex), showTimelineButton)
        {
            this.animator = animator;
            animatorStateIndex = stateIndex;
            animator.EditorStateChanged += OnAnimatorStateChanged;
        }

        /// <summary>
        /// Creates preview controls backed by a standalone TweenAnimator and hosted by an editor window.
        /// </summary>
        public TweenSequencePreviewGUI(EditorWindow window, TweenAnimator animator, int stateIndex, bool showTimelineButton = true)
            : this(window, animator, () => animator != null ? animator.GetState(stateIndex)?.sequence : null,
                  TweenAnimatorEditor.GetSequencePath(stateIndex), showTimelineButton)
        {
            this.animator = animator;
            animatorStateIndex = stateIndex;
            animator.EditorStateChanged += OnAnimatorStateChanged;
        }

        static Action GetRepaintAction(UnityEditor.Editor editor)
        {
            if (editor == null)
                throw new ArgumentNullException(nameof(editor));

            return editor.Repaint;
        }

        static Action GetRepaintAction(EditorWindow window)
        {
            if (window == null)
                throw new ArgumentNullException(nameof(window));

            return window.Repaint;
        }

        /// <summary>
        /// Draws play, pause, stop, and timeline-scrubbing controls.
        /// </summary>
        public void DrawControls() => DrawControls(false);

        /// <summary>
        /// Draws preview controls with optional foldout spacing after the control.
        /// </summary>
        public void DrawControls(bool addFoldoutSpace)
        {
            EnsureVisualResources();

            EvoEditorGUI.BeginColumn();
            EvoEditorGUI.BeginContainer("Preview", padding: 6);
            GUILayout.BeginHorizontal();

            Texture2D playbackIcon = IsPlaying ? pauseIcon : playIcon;
            string playbackTooltip = IsPlaying ? "Pause preview" : "Play preview";
            if (EvoEditorGUI.Button("", playbackIcon, ButtonOptions.WithTooltip(playbackTooltip).WithWidth(26f).WithIconSize(15).WithInvertedColor()))
                TogglePreview();

            EvoEditorGUI.AddFoldoutSpace();
            if (EvoEditorGUI.Button("", stopIcon, ButtonOptions.WithTooltip("Stop and restore").WithWidth(26f).WithIconSize(13).WithInvertedColor()))
                StopPreview();

            EvoEditorGUI.AddFoldoutSpace();
            if (DrawTimelineBar(CurrentTime, Duration, out float sampledTime))
                ScrubPreview(sampledTime);

            string timelinePropertyPath = GetTimelinePropertyPath();
            if (!string.IsNullOrEmpty(timelinePropertyPath))
            {
                EvoEditorGUI.AddFoldoutSpace();
                if (EvoEditorGUI.Button("", timelineIcon, ButtonOptions.WithTooltip("Open in Tween Timeline").WithWidth(26f).WithIconSize(12).WithInvertedColor()))
                    TimelineWindow.Open(owner, timelinePropertyPath);
            }

            GUILayout.EndHorizontal();
            EvoEditorGUI.EndContainer();
            EvoEditorGUI.EndColumn();

            if (addFoldoutSpace) { EvoEditorGUI.AddFoldoutSpace(); }
            else { EvoEditorGUI.AddPropertySpace(); }
        }

        void EnsureVisualResources()
        {
            playIcon = playIcon != null ? playIcon : EvoEditorGUI.GetIcon(IconLibrary.Play);
            pauseIcon = pauseIcon != null ? pauseIcon : EvoEditorGUI.GetIcon(IconLibrary.Pause);
            stopIcon = stopIcon != null ? stopIcon : EvoEditorGUI.GetIcon(IconLibrary.Stop);

            if (showTimelineButton)
                timelineIcon = timelineIcon != null ? timelineIcon : EvoEditorGUI.GetIcon("Tween_Timeline");
            if (timelineLabelStyle != null)
                return;

            timelineLabelStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
        }

        /// <summary>
        /// Restores captured values before serialized sequence data is edited.
        /// </summary>
        public void BeginLiveEdit()
        {
            if (liveEditActive)
                return;

            liveEditActive = true;
            recaptureAfterEdit = HasCapturedValues;
            if (!recaptureAfterEdit)
                return;

            float duration = Duration;
            editNormalizedTime = duration > 0f ? CurrentTime / duration : 0f;
            RestorePreview();
        }

        /// <summary>
        /// Recaptures edited sequence data at its previous normalized preview position.
        /// </summary>
        public void EndLiveEdit()
        {
            if (!liveEditActive)
                return;

            if (animator != null && Application.isPlaying && (!recaptureAfterEdit || !UsesAnimator))
                animator.RefreshState(animatorStateIndex);

            if (recaptureAfterEdit)
            {
                if (UsesAnimator) { animator.Refresh(editNormalizedTime); }
                else if (EnsureSampler())
                {
                    sampler.Capture();
                    sampler.Sample(sampler.Duration * Mathf.Clamp01(editNormalizedTime));
                }

                SceneView.RepaintAll();
            }

            liveEditActive = false;
            recaptureAfterEdit = false;
            RepaintEditor();
        }

        /// <summary>
        /// Stops playback and restores every value captured by this preview.
        /// </summary>
        public void StopPreview()
        {
            if (activeAnimatorPreview == this)
                activeAnimatorPreview = null;

            previewPlaying = false;
            liveEditActive = false;
            recaptureAfterEdit = false;

            if (UsesAnimator) { animator.Stop(true); }
            else { RestorePreview(); }

            SceneView.RepaintAll();
            RepaintEditor();
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            openPreviews.Remove(this);

            if (activeAnimatorPreview == this)
                activeAnimatorPreview = null;

            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

            if (animator != null)
                animator.EditorStateChanged -= OnAnimatorStateChanged;
            if (!UsesAnimator)
                RestorePreview();

            sampler?.Dispose();
            previewPlaying = false;
            liveEditActive = false;
            recaptureAfterEdit = false;
            sampler = null;
        }

        bool EnsureSampler()
        {
            if (owner == null)
                return false;

            TweenSequence sequence = getSequence();
            GameObject root = TweenPreviewRoot.Resolve(owner);
            if (sequence == null || root == null)
                return false;

            if (sampler == null)
                sampler = new TweenSequenceSampler(root, sequence);
            else
                sampler.SetSource(root, sequence);

            return true;
        }

        bool CapturePreview()
        {
            if (animator != null && !Application.isPlaying && activeAnimatorPreview != this)
            {
                activeAnimatorPreview?.StopPreview();
                activeAnimatorPreview = this;
            }

            if (UsesAnimator)
            {
                animator.Capture();
                return animator.HasCapturedValues;
            }

            if (!EnsureSampler())
                return false;

            sampler.Capture();
            return sampler.HasCapturedValues;
        }

        void SamplePreview(float time)
        {
            if (UsesAnimator) { animator.Sample(time); }
            else { sampler?.Sample(time); }
        }

        void RestorePreview()
        {
            if (UsesAnimator) { animator.Restore(); }
            else { sampler?.Restore(); }
        }

        void RepaintEditor() => repaint?.Invoke();

        public void TogglePreview()
        {
            if (animator != null && Application.isPlaying)
            {
                if (UsesAnimator)
                    animator.TogglePause();
                else
                    animator.Play(animatorStateIndex);

                RepaintEditor();
                return;
            }

            if (!HasCapturedValues)
            {
                BeforePreview?.Invoke();
                if (!CapturePreview())
                    return;
            }

            if (!previewPlaying && CurrentTime >= Duration)
                SamplePreview(0f);

            previewPlaying = !previewPlaying;
            lastPreviewUpdate = EditorApplication.timeSinceStartup;
            RepaintEditor();
        }

        public void ScrubPreview(float time)
        {
            if (animator != null && Application.isPlaying)
            {
                if (!UsesAnimator || animator.State == PlaybackState.Stopped || animator.State == PlaybackState.Completed)
                    animator.Play(animatorStateIndex);

                animator.Pause();
                animator.Sample(time);
                RepaintEditor();
                return;
            }

            if (!HasCapturedValues)
            {
                BeforePreview?.Invoke();
                if (!CapturePreview())
                    return;
            }

            previewPlaying = false;
            SamplePreview(time);
            SceneView.RepaintAll();
            RepaintEditor();
        }

        bool DrawTimelineBar(float time, float duration, out float sampledTime)
        {
            EvoEditorGUI.BeginRow(LayoutOptions.WithInvertedColor());

            Rect rect = GUILayoutUtility.GetRect(80f, EditorGUIUtility.singleLineHeight + EvoEditorGUI.Metrics.ControlHeightOffset, GUILayout.ExpandWidth(true));
            int controlId = GUIUtility.GetControlID(FocusType.Passive, rect);
            Event currentEvent = Event.current;
            bool changed = false;

            if (duration > 0f)
            {
                if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && rect.Contains(currentEvent.mousePosition))
                {
                    GUIUtility.hotControl = controlId;
                    changed = true;
                    currentEvent.Use();
                }
                else if (currentEvent.type == EventType.MouseDrag && GUIUtility.hotControl == controlId)
                {
                    changed = true;
                    currentEvent.Use();
                }
                else if (currentEvent.type == EventType.MouseUp && GUIUtility.hotControl == controlId)
                {
                    GUIUtility.hotControl = 0;
                    changed = true;
                    currentEvent.Use();
            }
        }

            float normalizedTime = duration > 0f ? Mathf.Clamp01(time / duration) : 0f;        
            if (changed)
                normalizedTime = Mathf.Clamp01((currentEvent.mousePosition.x - rect.x) / rect.width);

            Color fill = new(0.24f, 0.62f, 1f, 0.72f);
            Rect fillRect = new(rect.x + 3f, rect.y + 3f, Mathf.Max(0f, (rect.width - 6f) * normalizedTime), rect.height - 6f);
            EditorGUI.DrawRect(fillRect, fill);

            float markerX = Mathf.Lerp(rect.x + 3f, rect.xMax - 3f, normalizedTime);
            EditorGUI.DrawRect(new Rect(markerX - 1f, rect.y + 3f, 2f, rect.height - 6f), EditorStyles.label.normal.textColor);

            float visibleTime = normalizedTime * duration;
            GUI.Label(rect, $"{visibleTime:0.00}s  /  {duration:0.00}s", timelineLabelStyle);
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.SlideArrow);
          
            EvoEditorGUI.EndRow(LayoutOptions.WithoutSpace());

            sampledTime = visibleTime;
            return changed;
        }

        string GetTimelinePropertyPath()
        {
            if (!showTimelineButton)
                return null;
            if (sequencePropertyResolved)
                return sequencePropertyPath;

            sequencePropertyResolved = true;
            TweenSequence activeSequence = getSequence();
            if (activeSequence == null)
                return null;

            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            Type type = owner.GetType();
            while (type != null && type != typeof(MonoBehaviour))
            {
                FieldInfo[] fields = type.GetFields(Flags);
                for (int i = 0; i < fields.Length; i++)
                {
                    FieldInfo field = fields[i];
                    bool serialized = !field.IsStatic && !field.IsNotSerialized &&
                        (field.IsPublic || field.IsDefined(typeof(SerializeField), inherit: true));
                    if (!serialized || field.FieldType != typeof(TweenSequence))
                        continue;

                    if (ReferenceEquals(field.GetValue(owner), activeSequence))
                    {
                        sequencePropertyPath = field.Name;
                        return sequencePropertyPath;
                    }
                }

                type = type.BaseType;
            }

            return null;
        }

        void OnEditorUpdate()
        {
            if (owner == null)
                return;

            if (animator != null && Application.isPlaying)
            {
                if (animator.IsPlaying)
                    RepaintEditor();

                return;
            }

            if (!previewPlaying || !HasCapturedValues)
                return;

            double currentEditorTime = EditorApplication.timeSinceStartup;
            float deltaTime = Mathf.Min(Mathf.Max(0f, (float)(currentEditorTime - lastPreviewUpdate)), Time.maximumDeltaTime);
            lastPreviewUpdate = currentEditorTime;
            float playbackSpeed = animator != null ? animator.GetState(animatorStateIndex)?.playbackSpeed ?? 1f : 1f;
            float duration = Duration;
            float sampleTime = CurrentTime + deltaTime * playbackSpeed;
            bool wraps = Loop && duration > 0f;
            sampleTime = wraps ? Mathf.Repeat(sampleTime, duration) : Mathf.Min(sampleTime, duration);
            SamplePreview(sampleTime);

            if (!wraps && sampleTime >= duration)
                previewPlaying = false;

            SceneView.RepaintAll();
            RepaintEditor();
        }

        void OnAnimatorStateChanged()
        {
            if (activeAnimatorPreview == this)
                activeAnimatorPreview = null;

            previewPlaying = false;
            sampler?.Restore();
            RepaintEditor();
        }

        void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode)
                StopPreview();
        }
    }
}