using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;

namespace Evo.Tween.Editor
{
    /// <summary>
    /// Unity Timeline inspired editing window for any component-owned <see cref="TweenSequence"/>.
    /// The window edits the same inline or preset-backed serialized data shown in the Inspector.
    /// </summary>
    [NoAutoStaticsCleanup]
    public sealed partial class TimelineWindow : EditorWindow
    {
        const float MinimumInspectorWidth = 286f;
        const float MaximumInspectorWidth = 460f;
        const float MinimumClipDuration = 0.01f;
        const float DefaultPixelsPerSecond = 140f;
        const float MinimumPixelsPerSecond = DefaultPixelsPerSecond * 0.25f;
        const float MaximumPixelsPerSecond = DefaultPixelsPerSecond * 8f;
        const float DefaultClipDuration = 0.3f;
        const float DefaultStaggerStep = 0.05f;
        const double DockedResizeSettleDelay = 0.1d;
        const int RulerControlHint = 0x54_57_52;
        const int InspectorSplitterControlHint = 0x54_57_49;
        const int TimelinePanControlHint = 0x54_57_50;
        const int ClipControlHint = 0x54_57_43;
        const int OverviewControlHint = 0x54_57_4F;
        const string WindowTitle = "Tween Timeline";
        const float MinimumMajorTickSpacing = 70f;
        const float MinimumMinorTickSpacing = 8f;
        static readonly float[] RulerSteps = { 0.01f, 0.02f, 0.05f, 0.1f, 0.2f, 0.25f, 0.5f, 1f, 2f, 5f, 10f, 20f, 30f, 60f };
        static readonly int[] FrameRatePresets = { 24, 25, 30, 50, 60 };
        static readonly string[] FrameRateDisplayNames = Array.ConvertAll(FrameRatePresets, value => value.ToString());
        static readonly string FreeDragKey = Application.platform == RuntimePlatform.OSXEditor ? "Cmd" : "Ctrl";
        static readonly string SnapOnTooltip = $"Snapping to frames and clip edges. Hold {FreeDragKey} while dragging a clip to place it freely.";

        enum ClipDragMode
        {
            None,
            Move,
            TrimStart,
            TrimEnd
        }

        enum OverviewDragMode
        {
            None,
            Pan,
            ZoomStart,
            ZoomEnd
        }

        readonly struct ClipTiming
        {
            public readonly float GroupStart;
            public readonly float Start;
            public readonly float End;
            public readonly float Duration;
            public readonly float Delay;
            public readonly int Iterations;
            public readonly int Group;
            public readonly LoopMode Loop;
            public readonly ClipFlow Flow;

            public float TotalDuration => Mathf.Max(0f, End - Start);

            public ClipTiming(float groupStart, float start, float end, float duration, float delay, int iterations,
                int group, LoopMode loop, ClipFlow flow)
            {
                GroupStart = groupStart;
                Start = start;
                End = end;
                Duration = duration;
                Delay = delay;
                Iterations = iterations;
                Group = group;
                Loop = loop;
                Flow = flow;
            }
        }

        // Clips that start together. A group after the first starts when the clip ending last before it ends.
        readonly struct FlowGroup
        {
            public readonly float Start;
            public readonly int First;
            public readonly int Last;
            public readonly int After;

            public FlowGroup(float start, int first, int last, int after)
            {
                Start = start;
                First = first;
                Last = last;
                After = after;
            }
        }

        // Timing of a clip when a drag moving it began: the drag offsets its start, and a cancelled drag restores its delay
        readonly struct DraggedClip
        {
            public readonly int Index;
            public readonly float Delay;
            public readonly float Start;

            public DraggedClip(int index, float delay, float start)
            {
                Index = index;
                Delay = delay;
                Start = start;
            }
        }

        // Labels and colors are derived from the module type, so they are rebuilt with the timings instead of per repaint
        readonly struct ClipVisual
        {
            public readonly string Title;
            public readonly string ShortTitle;
            public readonly string Meta;
            public readonly Color Color;
            public readonly Ease Ease;
            public readonly AnimationCurve CustomEase;

            // What the clip changes, shown under its name: text such as "XY 0.92 → 1", or two color swatches
            public readonly string ValueText;
            public readonly bool HasColors;
            public readonly bool FromCurrent;
            public readonly Color FromColor;
            public readonly Color ToColor;

            public bool HasValueLine => ValueText != null || HasColors;

            public ClipVisual(string title, float duration, int iterations, Ease ease, AnimationCurve customEase,
                SerializedProperty from, SerializedProperty to)
            {
                Title = title;
                int separator = title.LastIndexOf(" / ", StringComparison.Ordinal);
                ShortTitle = separator >= 0 ? title[(separator + 3)..] : title;
                Meta = iterations > 1 ? $"{duration:0.###}s × {iterations}" : $"{duration:0.###}s";
                Color = GetClipColor(title);
                Ease = ease;
                CustomEase = customEase;

                FromCurrent = to != null && from == null;
                HasColors = to?.propertyType == SerializedPropertyType.Color && (from == null || from.propertyType == SerializedPropertyType.Color);
                FromColor = HasColors && from != null ? from.colorValue : default;
                ToColor = HasColors ? to.colorValue : default;
                ValueText = to == null ? null : HasColors ? FormatAlphaChange(from, to) : FormatValueChange(from, to);
            }
        }

        // Persistent editor-window state
        [SerializeField] Component owner;
        [SerializeField] string sequencePropertyPath;
        [SerializeField] bool lockSelection;
        [SerializeField] bool snapEnabled = true;
        [SerializeField] bool loopPreview;
        [SerializeField] int snapFrameRate = 60;
        [SerializeField] float pixelsPerSecond = DefaultPixelsPerSecond;
        [SerializeField] float horizontalScroll;
        [SerializeField] float verticalScroll;
        [SerializeField] float inspectorWidth = 340f;
        [SerializeField] float staggerStep = DefaultStaggerStep;
        [SerializeField] List<int> selectedTracks = new();

        [NonSerialized] bool frameTimelineOnNextLayout;
        [NonSerialized] bool playModeTransitionInProgress;

        // Serialized sources. Definitions may live inline on the owner or in a preset asset
        SerializedObject sequenceOwner;
        SerializedObject definitionOwner;
        SerializedProperty sequence;
        SerializedProperty sequencePreset;
        SerializedProperty inlineClips;
        SerializedProperty targetBindings;
        SerializedProperty clips;
        SerializedProperty playbackSpeed;
        TweenSequencePreset activePreset;
        FieldInfo sequenceField;

        // Inspector/preview collaborators and derived timeline data
        TweenClipGUI detailsGUI;
        TweenSequencePreviewGUI previewGUI;
        readonly List<string> sequencePropertyPaths = new();
        string[] sequenceDisplayNames = Array.Empty<string>();
        readonly List<ClipTiming> clipTimings = new();
        readonly List<ClipVisual> clipVisuals = new();
        readonly List<FlowGroup> flowGroups = new();
        readonly List<int> sortedSelection = new();
        readonly List<SerializedProperty> selectedClipProperties = new();
        Action<bool> drawChoreographyAction;
        Vector2 inspectorScroll;
        int trackToReveal = -1;
        int cachedTimeLabelCurrent = -1;
        int cachedTimeLabelDuration = -1;
        string cachedTimeLabel;
        float cachedTimeButtonWidth;
        readonly GUIContent timeMeasureContent = new();
        readonly GUIContent clipMeasureContent = new();
        readonly GUIContent tooltipContent = new();
        readonly Vector3[] sparklinePoints = new Vector3[Layout.SparklineMaxSamples + 1];

        // Cached visual resources are rebuilt after domain reload by EnsureStyles
        [NonSerialized] GUIStyle headerTitleStyle;
        [NonSerialized] GUIStyle headerTimeStyle;
        [NonSerialized] GUIStyle rulerLabelStyle;
        [NonSerialized] GUIStyle trackSectionLabelStyle;
        [NonSerialized] GUIStyle trackSectionCountStyle;
        [NonSerialized] GUIStyle trackTitleStyle;
        [NonSerialized] GUIStyle trackSubtitleStyle;
        [NonSerialized] GUIStyle clipTitleStyle;
        [NonSerialized] GUIStyle clipTitleMeasureStyle;
        [NonSerialized] GUIStyle clipMetaStyle;
        [NonSerialized] GUIStyle clipMetaMeasureStyle;
        [NonSerialized] GUIStyle clipValueStyle;
        [NonSerialized] GUIStyle centeredTitleStyle;
        [NonSerialized] GUIStyle centeredBodyStyle;
        [NonSerialized] GUIStyle dragTimeStyle;
        [NonSerialized] Texture2D lockIcon;
        [NonSerialized] Texture2D lockedIcon;
        [NonSerialized] Texture2D snapIcon;
        [NonSerialized] Texture2D loopIcon;
        [NonSerialized] Texture2D fitAllIcon;
        [NonSerialized] Texture2D parallelFlowIcon;
        [NonSerialized] Texture2D sequentialFlowIcon;

        // Transient pointer-interaction state
        ClipDragMode clipDragMode;
        int draggedTrack = -1;
        float dragMouseStartTime;
        float dragMouseStartX;
        bool dragMoved;
        float dragOriginalDuration;
        float dragOriginalStart;
        float dragOriginalEnd;
        float dragGroupStart;
        int dragIterations;
        bool dragSnappedToEdge;
        float dragSnapEdgeTime;
        readonly List<float> dragSnapEdges = new();
        readonly List<DraggedClip> draggedClips = new();
        readonly List<int> placedClips = new();
        readonly List<float> placedStarts = new();
        int pendingClickTrack = -1;
        bool pendingClickDeselects;

        int selectionAnchor = -1;
        bool boxSelecting;
        bool boxSelectionMoved;
        Vector2 boxSelectionStart;
        Vector2 boxSelectionEnd;
        readonly List<int> boxSelectionBase = new();

        int hoveredTrack = -1;
        ClipDragMode hoveredZone;
        int hoveredFlowTrack = -1;
        int flowLabelGroup = -1;
        string flowLabel;
        float timelineViewportWidth;

        OverviewDragMode overviewDragMode;
        bool overviewHovered;
        float overviewDragSpan;
        float overviewDragStartTime;
        float overviewDragEndTime;
        float overviewDragMouseX;
        float overviewDragScroll;

        int dropTrack = -1;
        bool dropAccepted;
        int dropInsertIndex = -1;
        float dropTime;
        string dropLabel;

        int reorderSource = -1;
        int reorderTarget = -1;
        bool resizingInspector;
        bool serializedFieldsChanged;
        bool scrubbingRuler;
        bool panningTimeline;
        bool clipTimingsDirty = true;
        float timelineDuration;
        float dragPreviewTime;
        float panMouseStartX;
        float panScrollStart;
        Vector2 lastWindowSize;
        double lastDockedResizeTime = double.NegativeInfinity;
        bool suppressInspectorDetails;

        [MenuItem("Tools/Evo Tween/Tween Timeline", false, 0)]
        static void OpenFromMenu()
        {
            TimelineWindow window = GetOrCreateWindow();
            Component selectedOwner = FindSelectedOwner();

            if (selectedOwner != null)
                window.Bind(selectedOwner);
        }

        /// <summary>
        /// Opens the timeline and focuses a standalone tween animator.
        /// </summary>
        public static void Open(TweenAnimator value) => Open(value, TweenAnimatorEditor.GetSequencePath(value.EditorStateIndex));

        /// <summary>
        /// Opens a component-owned sequence. The property path must identify a directly serialized
        /// <see cref="TweenSequence"/> field on the component.
        /// </summary>
        public static void Open(Component value, string sequencePropertyPath)
        {
            TimelineWindow window = GetOrCreateWindow();
            window.Focus();
            window.Bind(value, sequencePropertyPath, reportMissingPath: true);
        }

        static TimelineWindow GetOrCreateWindow()
        {
            TimelineWindow window = GetWindow<TimelineWindow>();
            window.EnsureWindowTitle();
            window.Show();
            return window;
        }

        void EnsureWindowTitle()
        {
            if (titleContent.text == WindowTitle && titleContent.image != null)
                return;

            titleContent = new GUIContent(WindowTitle, Resources.Load<Texture2D>("GUI/Tween_TimelineWindow"));
        }

        void OnEnable()
        {
            // Rebuild cached styles in OnGUI, where the editor skin is available
            headerTitleStyle = null;
            EnsureWindowTitle();

            minSize = new Vector2(800f, 300f);
            wantsMouseMove = true;
            frameTimelineOnNextLayout = true;
            lastWindowSize = position.size;
            SetZoom(pixelsPerSecond);
            horizontalScroll = SanitizeNonNegative(horizontalScroll);
            verticalScroll = SanitizeNonNegative(verticalScroll);
            inspectorWidth = float.IsNaN(inspectorWidth) || float.IsInfinity(inspectorWidth)
                ? 340f
                : Mathf.Clamp(inspectorWidth, MinimumInspectorWidth, MaximumInspectorWidth);
            if (Array.IndexOf(FrameRatePresets, snapFrameRate) < 0)
                snapFrameRate = 60;
            staggerStep = SanitizeNonNegative(staggerStep);

            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            if (owner != null) { Bind(owner, sequencePropertyPath, force: true); }
            else
            {
                owner = FindSelectedOwner();
                Bind(owner, force: true);
            }
        }

        void OnDisable()
        {
            StopRecording();
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.delayCall -= RebindAfterPlayModeTransition;
            EditorApplication.update -= OnDockedResizeUpdate;
            ReleasePointerCapture();
            previewGUI?.Dispose();
            previewGUI = null;
        }

        void OnSelectionChange()
        {
            if (recording)
            {
                ScrubTimeline(recordingTime);
                return;
            }

            if (!lockSelection && !IsPlayModeTransitioning)
                Bind(FindSelectedOwner());
        }

        void OnUndoRedo()
        {
            if (owner == null)
                return;

            if (owner is TweenAnimator)
            {
                Bind(owner, sequencePropertyPath, force: true);
                return;
            }

            previewGUI?.BeginLiveEdit();
            BindDefinitionSource(force: true);
            ClampSelection();
            previewGUI?.EndLiveEdit();
           
            if (recording)
                ScrubTimeline(recordingTime);

            Repaint();
        }

        void OnLostFocus()
        {
            if (clipDragMode != ClipDragMode.None)
                EndClipDrag();

            ResetPointerState();
        }

        void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            StopRecording();
            bool enteredMode = state == PlayModeStateChange.EnteredEditMode || state == PlayModeStateChange.EnteredPlayMode;
            playModeTransitionInProgress = true;

            EditorApplication.delayCall -= RebindAfterPlayModeTransition;
            if (enteredMode)
                EditorApplication.delayCall += RebindAfterPlayModeTransition;

            Repaint();
        }

        void RebindAfterPlayModeTransition()
        {
            if (this == null)
                return;

            playModeTransitionInProgress = false;
            Component nextOwner = owner;
            if (!lockSelection || nextOwner == null)
            {
                Component selectedOwner = FindSelectedOwner();
                if (selectedOwner != null)
                    nextOwner = selectedOwner;
            }

            if (nextOwner != null)
                Bind(nextOwner, sequencePropertyPath, force: true);
            else
                Repaint();
        }

        bool IsPlayModeTransitioning => playModeTransitionInProgress
            || EditorApplication.isPlayingOrWillChangePlaymode != EditorApplication.isPlaying;

        void OnDockedResizeUpdate()
        {
            if (!suppressInspectorDetails || EditorApplication.timeSinceStartup - lastDockedResizeTime < DockedResizeSettleDelay)
                return;

            EditorApplication.update -= OnDockedResizeUpdate;
            suppressInspectorDetails = false;
            Repaint();
        }

        void OnGUI()
        {
            UpdateDockedResizeState();
            ReleaseKeyboardFocusOnMouseDown();
            if (Event.current.type == EventType.MouseLeaveWindow)
            {
                SetHoveredClip(-1, ClipDragMode.None);
                SetHoveredFlowTrack(-1);
            }

            EnsureStyles();
            EditorGUI.DrawRect(new Rect(Vector2.zero, position.size), WindowBackground);

            if (!IsPlayModeTransitioning && owner == null && sequenceOwner != null)
                Bind(null, force: true);
            else if (owner != null && (sequenceOwner == null || sequenceOwner.targetObject == null))
                Bind(owner, sequencePropertyPath, force: true);

            if (Event.current.type == EventType.Layout)
                UpdateSerializedSources();

            ApplyInitialFrameIfReady();
            serializedFieldsChanged = false;

            Rect headerRect = new(0f, 0f, position.width, Layout.HeaderHeight);
            DrawHeader(headerRect);

            if (owner == null || sequence == null)
            {
                DrawEmptyState(new Rect(0f, Layout.HeaderHeight, position.width, position.height - Layout.HeaderHeight));
                return;
            }

            Rect transportRect = new(0f, Layout.HeaderHeight, position.width, Layout.TransportHeight);
            DrawTransport(transportRect);

            Rect contentRect = new(0f, Layout.HeaderHeight + Layout.TransportHeight, position.width,
                Mathf.Max(0f, position.height - Layout.HeaderHeight - Layout.TransportHeight));
            DrawWorkspace(contentRect);

            if (serializedFieldsChanged)
                CommitPropertyChanges();

            HandleKeyboardCommands();
        }

        void UpdateDockedResizeState()
        {
            Vector2 currentSize = position.size;
            if (currentSize == lastWindowSize)
                return;

            lastWindowSize = currentSize;
            if (!docked)
                return;

            lastDockedResizeTime = EditorApplication.timeSinceStartup;
            if (!suppressInspectorDetails)
                EditorApplication.update += OnDockedResizeUpdate;

            suppressInspectorDetails = true;
        }

        void ReleasePointerCapture()
        {
            bool capturing = scrubbingRuler || resizingInspector || clipDragMode != ClipDragMode.None || boxSelecting
                || overviewDragMode != OverviewDragMode.None;
            if (capturing && GUIUtility.hotControl != 0)
                GUIUtility.hotControl = 0;
        }

        // Drops every press and hover in progress. A clip drag is ended or discarded by the caller.
        void ResetPointerState()
        {
            ReleasePointerCapture();
            reorderSource = -1;
            reorderTarget = -1;
            resizingInspector = false;
            scrubbingRuler = false;
            boxSelecting = false;
            overviewDragMode = OverviewDragMode.None;
            pendingClickTrack = -1;
            ClearDropState();
            SetHoveredClip(-1, ClipDragMode.None);
            SetHoveredFlowTrack(-1);
        }
    }
}