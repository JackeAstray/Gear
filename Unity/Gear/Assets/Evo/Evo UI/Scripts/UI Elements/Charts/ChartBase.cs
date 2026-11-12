using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Evo.Tween;

namespace Evo.UI
{
    /// <summary>
    /// Provides event-driven refreshes and incremental management of chart-owned UI.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(RectTransform)), NoAutoStaticsCleanup]
    public abstract class ChartBase : MonoBehaviour, IStylerHandler
    {
        // Styling
        [Tooltip("Choose custom colors/fonts, a Styler Preset, or no mapped styling.")]
        public StyleProvider styleProvider = StyleProvider.Styler;
        [Tooltip("Locks the preset to the current default Styler Preset.")]
        public bool useDefaultStylerPreset = false;
        [Tooltip("Preset used to resolve color and font IDs when Styling Source is Styler Preset.")]
        public StylerPreset stylerPreset;
        [Tooltip("Color mapping for numeric data labels, independent of category and scale text.")]
        public ColorMapping valueColor = new() { stylerID = "Primary" };

        // Settings
        [Tooltip("Numeric format used by value and scale labels.")]
        public NumberDisplayFormat displayFormat = NumberDisplayFormat.Fixed1;
#if UNITY_EDITOR
        [Tooltip("Automatically refresh the editor preview. Runtime updates are independent of this setting.")]
        public bool autoRefresh = true;
#endif

        // Interaction
        [Tooltip("Respond to the pointer at runtime with hover effects, the tooltip and point events.")]
        public bool interactable;
        [Tooltip("Element reacts over the drawn bar or point only. Category also reacts over the rest of its slot.")]
        public PointerArea pointerArea = PointerArea.Category;
        [Min(0f), Tooltip("How far the hovered bar or point grows, or a hovered slice moves out, in local UI units.")]
        public float hoverExpand = 4f;
        [Range(0f, 1f), Tooltip("Opacity of the other bars, points or slices while one is hovered. 1 keeps them unchanged.")]
        public float dimmedOpacity = 0.6f;
        [Min(0f), Tooltip("Hover transition length in seconds, on unscaled time.")]
        public float hoverDuration = 0.2f;
        [Tooltip("Shows the hovered point's label and value. Add it to an object other than the chart or its parents.")]
        public Tooltip tooltip;

        // Events
        public UnityEvent<int> onPointClicked = new();
        public UnityEvent<int> onPointEntered = new();
        public UnityEvent<int> onPointExited = new();

        // Text provider of generated labels, saved so builds match the editor
        [SerializeField, HideInInspector] string textProvider;

        // Constants
        protected const string GenSuffix = "[Generated]";

        // Plot Labels
        protected readonly List<PlotLabel> bottomLabels = new();
        protected readonly List<PlotLabel> leftLabels = new();
        protected readonly List<PlotLabel> valueLabels = new();
        readonly Dictionary<Graphic, PlotLabel> plotLabels = new();
        readonly List<Graphic> unusedLabels = new();

        // Runtime State
        readonly Dictionary<(Transform, string), RectTransform> elements = new();
        readonly HashSet<Transform> used = new();
        readonly List<RectTransform> roots = new();
        readonly Dictionary<Transform, int> siblingOrder = new();
        readonly DrawCache<Transform, string> names = new();
        readonly DrawCache<(int, string, string), string> numbers = new();
        float stylerTransitionDuration;
        bool pendingRedraw;
        bool pendingClear;
        bool forceRedraw;
        bool runtimeDrawScheduled;
        bool runtimeDrawQueued;
        bool editorDrawScheduled;
        bool drawing;
        bool recordUndo;
        bool hasState;
        ulong lastState;
#if UNITY_EDITOR
        ulong lastSourceState;
        int undoGroup = -1;
        readonly List<Transform> created = new();
#endif

        // Interaction State
        readonly List<PointVisual> pointVisuals = new();
        readonly List<HoverState> hoverStates = new();
        ChartHitArea hitArea;
        TweenHandle hoverAnimation;
        int pointerIndex = -1;
        int highlightIndex = -1;
        bool pointerEnabled;
        bool tooltipShown;

        // Shared State
        static readonly List<ChartBase> scheduledDraws = new();
        static readonly Dictionary<(string, string), List<string>> indexedNames = new();
        static readonly Dictionary<string, string> containerNames = new();
        static bool drawCallbackAdded;

        // Properties
        protected RectTransform RectTransform => (RectTransform)transform;
        protected static readonly Vector2 Center = new(0.5f, 0.5f);

        protected virtual void OnEnable()
        {
            Styler.SyncDefaultPreset(ref stylerPreset, styleProvider, useDefaultStylerPreset);
            Styler.Register(this);

#if UNITY_EDITOR
            UnityEditor.Undo.undoRedoPerformed -= OnUndoRedo;
            UnityEditor.Undo.undoRedoPerformed += OnUndoRedo;
            CaptureUndoGroup();
#endif

#if EVO_LOCALIZATION
            Localization.LocalizationManager.Subscribe(this, enableLocalization, ref localizedObject, UpdateLocalization);
#endif

            // Re-adopt saved children, including when entering play mode or after a domain reload
            forceRedraw = true;
            DrawChartSafe();
        }

        protected virtual void OnDisable()
        {
            Styler.Unregister(this);
            CancelScheduledDraw();

            // uGUI sends no pointer exit to disabled objects, so a hover ends here without events
            ResetHover(notify: false);

#if UNITY_EDITOR
            UnityEditor.Undo.undoRedoPerformed -= OnUndoRedo;
#endif

#if EVO_LOCALIZATION
            Localization.LocalizationManager.Unsubscribe(UpdateLocalization);
#endif

            pendingRedraw = false;
        }

        protected virtual void OnDestroy()
        {
            OnDisable();
            GatherRoots();

            if (Application.IsPlaying(gameObject))
            {
                foreach (var root in roots)
                {
                    if (root != null)
                        Destroy(root.gameObject);
                }

                return;
            }
#if UNITY_EDITOR
            // Hierarchy mutation inside OnDestroy breaks deletion/Undo. Clean up surviving
            // output (component removal or external legends) on the next editor tick instead.
            if (UnityEditor.EditorUtility.IsPersistent(this))
                return;

            var output = roots.ToArray();
            var owner = this;
            int group = UnityEditor.Undo.isProcessing ? -1 : UnityEditor.Undo.GetCurrentGroup();
            UnityEditor.EditorApplication.delayCall += Cleanup;

            void Cleanup()
            {
                if (UnityEditor.Undo.isProcessing)
                {
                    UnityEditor.EditorApplication.delayCall += Cleanup;
                    return;
                }

                if (owner != null || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                    return;

                // Removing the component in the Inspector also removes its output in the same undo step
                bool record = IsEditUndoGroup(group);

                foreach (var item in output)
                {
                    if (item != null && item.gameObject.scene.IsValid())
                        RemoveObject(item.gameObject, record);
                }
            }
#endif
        }

        protected virtual void OnRectTransformDimensionsChange() => DrawChartSafe();

        protected virtual void OnTransformParentChanged() => DrawChartSafe();

        protected virtual void OnTransformChildrenChanged()
        {
            if (drawing)
                return;

#if UNITY_EDITOR
            CaptureUndoGroup();
#endif
            forceRedraw = true;
            DrawChartSafe();
        }

        protected virtual void OnDidApplyAnimationProperties() => DrawChartSafe();

        protected abstract void RefreshChart();

        protected abstract void CollectState(ref ChartState state);

        protected virtual bool IsGeneratedRoot(Transform child)
        {
            string name = NameOf(child);
            return name == "Grid " + GenSuffix || name == "Data " + GenSuffix || name == "Labels " + GenSuffix || name == "Slices " + GenSuffix
                || name == "Hit Area " + GenSuffix;
        }

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
            stylerTransitionDuration = ColorTransition.Duration;
            forceRedraw = true;
            DrawChartSafe();
        }
        #endregion

        #region Data Helpers
        protected void UpdatePoints<T>(ref List<T> data, List<T> replacement)
        {
            data = replacement;
            MarkDirty();
        }

        protected void AddPoint<T>(ref List<T> data, T point) where T : DataPoint
        {
            if (point == null)
                return;

            data ??= new();
            data.Add(point);
            MarkDirty();
        }

        protected void RemovePoint<T>(List<T> data, int index)
        {
            if (index < 0 || index >= (data?.Count ?? 0))
                return;

            data.RemoveAt(index);
            MarkDirty();
        }

        protected void ClearPoints<T>(List<T> data)
        {
            if (data == null || data.Count == 0)
                return;

            data.Clear();
            MarkDirty();
        }

        protected void SetPoint(DataPoint point, string label, float value, Color? color = null)
        {
            if (point == null)
                return;

            bool colorChanged = color.HasValue && point is ColoredDataPoint data && data.color != color.Value;
            if (point.label == label && point.value.Equals(value) && !colorChanged)
                return;

            point.label = label;
            point.value = value;
            if (color.HasValue && point is ColoredDataPoint colored)
                colored.color = color.Value;

            MarkDirty();
        }

        protected void SetPointAt<T>(List<T> data, int index, string label, float value, System.Func<T> create, Color? color = null) where T : DataPoint
        {
            if (index < 0 || index >= (data?.Count ?? 0))
                return;

            data[index] ??= create();
            SetPoint(data[index], label, value, color);
        }
        #endregion

        #region Setting Helpers
        // Property setters refresh only when the value changes. Non-finite numbers are ignored.
        protected void SetValue(ref float field, float value)
        {
            if (field == value || float.IsNaN(value) || float.IsInfinity(value))
                return;

            field = value;
            MarkDirty();
        }

        protected void SetValue<T>(ref T field, T value)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return;

            field = value;
            MarkDirty();
        }
        #endregion

        #region Formatting and Validation
        protected string FormatValue(float value) => FormatNumber(Finite(value), Utilities.GetNumberFormatString(displayFormat, "F1"));

        // Number text is reused by the next draw while its value and format stay the same, so redraws don't allocate
        internal string FormatNumber(float value, string format, string suffix = null)
        {
            var key = (System.BitConverter.SingleToInt32Bits(value), format, suffix);
            if (!numbers.TryGetValue(key, out string text))
                numbers.Add(key, text = value.ToString(format) + suffix);

            return text;
        }

        // Generated names ("Slice 3", "Grid Level 2 Side ") are built once per pattern and index and shared by every chart
        internal static string IndexedName(string prefix, int index, string suffix = null)
        {
            if (!indexedNames.TryGetValue((prefix, suffix), out var list))
                indexedNames.Add((prefix, suffix), list = new List<string>());

            while (list.Count <= index)
                list.Add(null);

            return list[index] ??= prefix + index + suffix;
        }

        // Reading a name allocates. At runtime, a name read during the last draw is reused while
        // the object stays in the output, the same rule GeneratedNames applies to renamed slots.
        internal string NameOf(Transform item)
        {
            if (!Application.IsPlaying(item))
                return item.name;

            if (!names.TryGetValue(item, out string name))
                names.Add(item, name = item.name);

            return name;
        }

        // Play mode output is never saved, so only edit mode waits for compilation (the check allocates)
        bool CanDraw => gameObject.scene.IsValid()
#if UNITY_EDITOR
            && !UnityEditor.EditorUtility.IsPersistent(this)
            && (Application.IsPlaying(this) || !UnityEditor.EditorApplication.isCompiling)
            && !UnityEditor.Undo.isProcessing
#endif
            ;
        #endregion

        #region Refresh Scheduling
        protected void DrawChartSafe()
        {
            pendingRedraw = true;

            if (drawing || !isActiveAndEnabled || !CanDraw)
                return;

#if UNITY_EDITOR
            if (!Application.IsPlaying(gameObject))
            {
                RequestEditorRefresh();
                return;
            }
#endif

            // Multiple edits share one canvas pass
            ScheduleRuntimeDraw();
        }

        void ScheduleRuntimeDraw()
        {
            if (runtimeDrawScheduled)
                return;

            runtimeDrawScheduled = true;

            // A cancelled chart stays listed until the next pass, so each chart is listed once
            if (!runtimeDrawQueued)
            {
                scheduledDraws.Add(this);
                runtimeDrawQueued = true;
            }

            // One callback serves every chart and stays subscribed, as subscribing allocates
            if (!drawCallbackAdded)
            {
                Canvas.preWillRenderCanvases += DrawScheduled;
                drawCallbackAdded = true;
            }
        }

        void CancelScheduledDraw()
        {
            // The canvas pass skips cancelled charts
            runtimeDrawScheduled = false;

#if UNITY_EDITOR
            if (editorDrawScheduled)
            {
                UnityEditor.EditorApplication.delayCall -= DrawDelayed;
                editorDrawScheduled = false;
            }
#endif
        }

        static void DrawScheduled()
        {
            // Requests raised by these draws wait for the next canvas pass
            int count = scheduledDraws.Count;

            for (int i = 0; i < count; i++)
            {
                var chart = scheduledDraws[i];
                if (chart == null)
                    continue;

                chart.runtimeDrawQueued = false;

                if (chart.runtimeDrawScheduled)
                    chart.DrawPending();
            }

            scheduledDraws.RemoveRange(0, count);
        }

        // Unity 6.6 clears the Canvas render events when entering play mode without a domain reload
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RestoreDrawCallback()
        {
            if (!drawCallbackAdded)
                return;

            Canvas.preWillRenderCanvases -= DrawScheduled;
            Canvas.preWillRenderCanvases += DrawScheduled;
        }

        void DrawPending()
        {
            runtimeDrawScheduled = false;

            if (!isActiveAndEnabled || !CanDraw || (!pendingRedraw && !pendingClear))
                return;

            if (CanvasUpdateRegistry.IsRebuildingLayout() || CanvasUpdateRegistry.IsRebuildingGraphics())
            {
                ScheduleRuntimeDraw();
                return;
            }

            RefreshIfChanged();
        }

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            Styler.SyncDefaultPreset(ref stylerPreset, styleProvider, useDefaultStylerPreset);
            CaptureUndoGroup();
            RequestEditorRefresh();
        }

        protected virtual void Reset()
        {
            useDefaultStylerPreset = true;
            interactable = true;
            Styler.SyncDefaultPreset(ref stylerPreset, styleProvider, useDefaultStylerPreset);
        }

        /// <summary>
        /// Schedule a preview refresh after serialized editor edits.
        /// </summary>
        public void RequestEditorRefresh(bool force = false)
        {
            pendingRedraw = true;
            forceRedraw |= force;
            editorDrawScheduled = true;
            UnityEditor.EditorApplication.delayCall -= DrawDelayed;
            UnityEditor.EditorApplication.delayCall += DrawDelayed;
        }

        void DrawDelayed()
        {
            editorDrawScheduled = false;

            if (this == null || !isActiveAndEnabled || !CanDraw)
                return;

            if (Application.IsPlaying(gameObject)) { ScheduleRuntimeDraw(); }
            else if (autoRefresh || forceRedraw || pendingClear) { RefreshIfChanged(); }
        }

        void OnUndoRedo()
        {
            if (this == null)
                return;

            // Reconcile derived UI after Unity has restored the settings, even in manual
            // preview mode, without adding a new undo step (it would clear the redo history).
            undoGroup = -1;

            if (pendingRedraw || !hasState || GetState() != lastState)
                RequestEditorRefresh(force: true);
        }

        // Called by the messages an edit of this chart raises (Add Component, Inspector changes, deleting output),
        // which Unity sends within that edit's undo group
        void CaptureUndoGroup() => undoGroup = UnityEditor.Undo.isProcessing ? -1 : UnityEditor.Undo.GetCurrentGroup();

        // An empty group holds no edit, such as when a scene loads
        static bool IsEditUndoGroup(int group) => group >= 0 && group == UnityEditor.Undo.GetCurrentGroup()
            && !string.IsNullOrEmpty(UnityEditor.Undo.GetCurrentGroupName());

        // Unity restores a transform's children from the undo step that recorded them, such as Add Component, and
        // reports the ones it doesn't know as dangling. So output a draw creates or removes for an edit joins its step.
        bool RecordsUndo() => !Application.IsPlaying(gameObject) && IsEditUndoGroup(undoGroup);

        void SavePrefabChanges(Transform item)
        {
            if (item == null || !UnityEditor.PrefabUtility.IsPartOfPrefabInstance(item))
                return;

            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(item.gameObject);

            foreach (var component in item.GetComponents<Component>())
            {
                if (component != null)
                    UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }

            foreach (Transform child in item)
                SavePrefabChanges(child);
        }
#endif

        void RefreshIfChanged()
        {
            if (pendingClear)
                ClearChart();
            else if (forceRedraw || !hasState || GetState() != lastState)
                DrawChart();
            else
                pendingRedraw = false;
        }
        #endregion

        #region Chart Rendering
        ulong GetState() => GetState(out _);

        ulong GetState(out ulong source)
        {
            ChartState state = new();
            state.Add(gameObject.layer);
            state.Add((int)displayFormat);
            state.Add(interactable);
            state.Add(Styler.GetColor(styleProvider, valueColor, stylerPreset));
            CollectState(ref state);
            source = state.Value;

            // Size, scale and pixel width follow the canvas, such as the Game view sizing itself after the Editor starts
            state.Add(RectTransform.rect.size);
            state.Add(transform.lossyScale);
            state.Add(ChartMesh.PixelWidth(RectTransform, GetComponentInParent<Canvas>()));
            return state.Value;
        }

        /// <summary>
        /// Reconcile existing elements. Safe to call repeatedly, including with unchanged data.
        /// </summary>
        public void DrawChart()
        {
            pendingClear = false;

            if (drawing || !isActiveAndEnabled || !CanDraw || CanvasUpdateRegistry.IsRebuildingLayout() || CanvasUpdateRegistry.IsRebuildingGraphics())
            {
                forceRedraw = true;
                DrawChartSafe();
                return;
            }

            using var transition = ColorTransition.Begin(this, stylerTransitionDuration);
            stylerTransitionDuration = 0f;
            CancelScheduledDraw();
            ulong state = GetState(out ulong sourceState);

#if UNITY_EDITOR
            // Canvas size changes alone redraw without dirtying the scene
            bool sourceChanged = hasState && sourceState != lastSourceState;
#endif
            pendingRedraw = false;
            forceRedraw = false;
            drawing = true;
#if UNITY_EDITOR
            recordUndo = RecordsUndo();
#endif

            try
            {
                elements.Clear();
                used.Clear();
                siblingOrder.Clear();
                GatherRoots();

                foreach (var root in roots)
                    Index(root);

                // Hover effects use the output of the last draw, so they are restored before it changes
                pointerEnabled = interactable && Application.IsPlaying(gameObject);
                if (!pointerEnabled)
                    ResetHover(notify: true);

                pointVisuals.Clear();
                RefreshChart();

                if (pointerEnabled)
                    DrawHitArea();

                foreach (var root in roots)
                {
                    if (root == null)
                        continue;

                    if (!used.Contains(root)) { RemoveObject(root.gameObject, recordUndo); }
                    else { Sweep(root); }
                }

                if (pointerEnabled)
                    SyncHover();

                unusedLabels.Clear();

                foreach (var pair in plotLabels)
                {
                    if (pair.Key == null || !used.Contains(pair.Key.transform))
                        unusedLabels.Add(pair.Key);
                }

                foreach (var label in unusedLabels)
                    plotLabels.Remove(label);

                // The next draw reuses only names and number text this draw used
                names.Next();
                numbers.Next();

                // Requests raised while drawing must compare against the input we rendered
                lastState = state;
                hasState = true;

#if UNITY_EDITOR
                lastSourceState = sourceState;

                if (!Application.IsPlaying(gameObject))
                {
                    // Undo records an object's children along with it, so new branches are recorded once complete.
                    // Children added to an object after it was recorded would be dangling when the step is undone.
                    foreach (var item in created)
                    {
                        if (item != null && !created.Contains(item.parent))
                            UnityEditor.Undo.RegisterCreatedObjectUndo(item.gameObject, "Create Chart Output");
                    }

                    UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);

                    foreach (var root in roots)
                    {
                        if (root != null)
                            SavePrefabChanges(root);
                    }

                    if (sourceChanged)
                        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);

                    UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                    UnityEditor.SceneView.RepaintAll();
                }
#endif
            }
            finally
            {
                drawing = false;
                recordUndo = false;
#if UNITY_EDITOR
                created.Clear();
#endif

                if (pendingRedraw || pendingClear)
                    DrawChartSafe();
            }
        }

        void GatherRoots()
        {
            roots.Clear();

            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (IsGeneratedRoot(child) && child is RectTransform rt)
                    roots.Add(rt);
            }

            CollectExtraRoots(roots);
        }

        void Index(RectTransform item)
        {
            var key = (item.parent, NameOf(item));
            if (!elements.TryGetValue(key, out var existing) || (!existing.gameObject.activeSelf && item.gameObject.activeSelf))
                elements[key] = item;

            // Text engines may own submeshes, those are not chart elements
            if (item.TryGetComponent<Graphic>(out _))
                return;

            for (int i = 0; i < item.childCount; i++)
            {
                if (item.GetChild(i) is RectTransform rt)
                    Index(rt);
            }
        }

        void Sweep(Transform parent)
        {
            if (parent.TryGetComponent<Graphic>(out _))
                return;

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (!used.Contains(child)) { RemoveObject(child.gameObject, recordUndo); }
                else { Sweep(child); }
            }
        }

        protected virtual void CollectExtraRoots(List<RectTransform> result) { }

        /// <summary>
        /// Explicitly remove chart-owned output. Source data and user children are retained.
        /// </summary>
        public virtual void ClearChart()
        {
            if (drawing || !CanDraw || CanvasUpdateRegistry.IsRebuildingLayout() || CanvasUpdateRegistry.IsRebuildingGraphics())
            {
                pendingClear = true;
                DrawChartSafe();
                return;
            }

            CancelScheduledDraw();
            drawing = true;
#if UNITY_EDITOR
            recordUndo = RecordsUndo();
#endif

            try
            {
                ResetHover(notify: true);
                pointVisuals.Clear();
                GatherRoots();

                foreach (var root in roots)
                {
                    if (root != null)
                        RemoveObject(root.gameObject, recordUndo);
                }

                roots.Clear();
                elements.Clear();
                used.Clear();
                siblingOrder.Clear();
                plotLabels.Clear();
                unusedLabels.Clear();
                names.Clear();

                BeginLabels();

                pendingRedraw = pendingClear = forceRedraw = false;
                hasState = false;
            }
            finally
            {
                drawing = false;
                recordUndo = false;
            }
        }
        #endregion

        #region Generated Elements
        protected static void RemoveObject(GameObject item, bool recordUndo = false)
        {
            // Detach before deferred destruction so another draw cannot re-adopt it.
            if (Application.IsPlaying(item))
            {
                item.SetActive(false);
                item.transform.SetParent(null, false);
                Destroy(item);
            }
#if UNITY_EDITOR
            // Prefab instances cannot delete inherited children. Keep unused output
            // inactive so it can be reused when the setting/data is restored.
            else if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(item) && !UnityEditor.PrefabUtility.IsAddedGameObjectOverride(item))
            {
                if (item.activeSelf)
                    item.SetActive(false);

                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(item);
            }
            else if (recordUndo) { UnityEditor.Undo.DestroyObjectImmediate(item); }
#endif
            else { DestroyImmediate(item); }
        }

        protected T EnsureComponent<T>(GameObject item) where T : Component
        {
            if (item.TryGetComponent<T>(out var component))
                return component;

            return item.AddComponent<T>();
        }

        protected RectTransform Element(Transform parent, string name, System.Type graphicType = null)
        {
            var key = (parent, name);
            elements.TryGetValue(key, out var rt);

            if (rt != null && graphicType != null && rt.GetComponent(graphicType) == null)
            {
                RemoveObject(rt.gameObject, recordUndo);
                rt = null;
            }

            if (rt == null)
            {
                GameObject obj = graphicType == null ? new(name, typeof(RectTransform)) : new(name, typeof(RectTransform), graphicType);
                rt = (RectTransform)obj.transform;
                rt.SetParent(parent, false);
                elements[key] = rt;

                if (parent == transform)
                    roots.Add(rt);
#if UNITY_EDITOR
                if (recordUndo)
                    created.Add(rt);
#endif
            }

            if (rt.gameObject.layer != parent.gameObject.layer)
                rt.gameObject.layer = parent.gameObject.layer;
            if (rt.gameObject.hideFlags != HideFlags.None)
                rt.gameObject.hideFlags = HideFlags.None;
            if (!rt.gameObject.activeSelf)
                rt.gameObject.SetActive(true);

            used.Add(rt);

            // Restore draw order when a previously disabled layer is added back
            siblingOrder.TryGetValue(parent, out int order);
            if (parent == transform || used.Contains(parent))
            {
                if (rt.GetSiblingIndex() != order)
                    rt.SetSiblingIndex(order);

                siblingOrder[parent] = order + 1;
            }

            return rt;
        }

        protected GameObject Container(string name, System.Type graphicType = null)
        {
            if (!containerNames.TryGetValue(name, out string generated))
                containerNames.Add(name, generated = name + " " + GenSuffix);

            // Chart output is positioned explicitly, even when the host also has a layout group
            var rt = Element(transform, generated, graphicType);
            var layout = EnsureComponent<LayoutElement>(rt.gameObject);
            if (!layout.ignoreLayout)
                layout.ignoreLayout = true;

            Stretch(rt);
            return rt.gameObject;
        }
        #endregion

        #region Layout and Math
        protected static void Layout(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size, float angle = 0)
        {
            size = new Vector2(Finite(size.x), Finite(size.y));
            position = new Vector2(Finite(position.x), Finite(position.y));

            if (rt.anchorMin != anchorMin)
                rt.anchorMin = anchorMin;
            if (rt.anchorMax != anchorMax)
                rt.anchorMax = anchorMax;
            if (rt.pivot != pivot)
                rt.pivot = pivot;
            if (rt.anchoredPosition != position)
                rt.anchoredPosition = position;
            if (rt.anchoredPosition3D.z != 0)
                rt.anchoredPosition3D = new(position.x, position.y, 0);
            if (rt.sizeDelta != size)
                rt.sizeDelta = size;

            var rotation = Quaternion.Euler(0, 0, Finite(angle));

            // Quaternion != is approximate and skips small turns, which left eased lines stale
            if (!rt.localRotation.Equals(rotation))
                rt.localRotation = rotation;
            if (rt.localScale != Vector3.one)
                rt.localScale = Vector3.one;
        }

        protected static void Stretch(RectTransform rt) => Layout(rt, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);

        protected static float Finite(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0 : value;

        protected static float Positive(float value) => Mathf.Max(0, Finite(value));

        // Mathf.Min with three values allocates its params array
        static float Min(float a, float b, float c)
        {
            float min = a;
            if (b < min)
                min = b;
            if (c < min)
                min = c;

            return min;
        }

        protected Object GetFont(FontMapping mapping) => Styler.GetFont(styleProvider, mapping, stylerPreset, LabelBackend);

        TextBackend LabelBackend => TextProvider.Resolve(ref textProvider, this);

        // Falls back to the backend default when the font is missing or belongs to another provider
        static Object ResolveFont(TextBackend backend, Object font) => font != null && backend.IsFont(font) ? font : backend.DefaultFont;

        // Element replaces labels of another provider, so the component always exists
        static Graphic TextElement(RectTransform element, TextBackend backend) => (Graphic)element.GetComponent(backend.ComponentType);
        #endregion

        #region Generated Drawing
        protected ProceduralShape Shape(Transform parent, string name, Color color, float cornerRadius = 0,
            ProceduralShape.ShapeType type = ProceduralShape.ShapeType.Rectangle, ShapeSettings settings = null,
            float softnessLimit = float.MaxValue)
        {
            var rt = Element(parent, name, typeof(ProceduralShape));
            var shape = rt.GetComponent<ProceduralShape>();
            shape.Shape = type;

            if (settings == null)
                shape.UniformCornerRadius = Positive(cornerRadius);
            else
                settings.Apply(shape, cornerRadius, softnessLimit);

            ColorTransition.SetGraphicColor(shape, color);

            shape.RaycastDetectionMode = ProceduralShape.RaycastMode.None;

            if (shape.raycastTarget)
                shape.raycastTarget = false;

            return shape;
        }

        protected void Line(GameObject parent, Vector2 start, Vector2 end, Color color, float thickness, string name, Vector2 anchor)
        {
            thickness = Positive(thickness);
            var shape = Shape(parent.transform, name, color, thickness * 0.5f);
            Vector2 diff = end - start;
            Layout(shape.rectTransform, anchor, anchor, new Vector2(0, 0.5f), start,
                new Vector2(diff.magnitude, thickness), Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg);
        }

        protected ProceduralShape Point(GameObject parent, Vector2 position, Color color, float size, string name, Vector2 anchor)
        {
            var shape = Shape(parent.transform, name, color, 0, ProceduralShape.ShapeType.Ellipse);
            Layout(shape.rectTransform, anchor, anchor, Center, position, Vector2.one * Positive(size));
            return shape;
        }

        protected Graphic Text(Transform parent, string name, string text, Object font, float size, Color color, TextAlignmentOptions alignment)
        {
            TextBackend backend = LabelBackend;
            var label = TextElement(Element(parent, name, backend.ComponentType), backend);

            backend.SetText(label, text ?? "");
            backend.SetFont(label, ResolveFont(backend, font));
            backend.SetFontSize(label, Positive(size));
            ColorTransition.SetGraphicColor(label, color);
            backend.SetAlignment(label, alignment);
            backend.SetWordWrap(label, false);

            if (label.raycastTarget)
                label.raycastTarget = false;

            return label;
        }

        protected void Label(GameObject parent, string text, Vector2 position, TextAlignmentOptions alignment,
            string name, Vector2 size, Object font, float fontSize, Color color)
        {
            var label = Text(parent.transform, name, text, font, fontSize, color, alignment);
            var pivot = alignment == TextAlignmentOptions.MidlineRight 
                ? new Vector2(1, 0.5f)
                : alignment == TextAlignmentOptions.MidlineLeft ? new Vector2(0, 0.5f) : Center;
            Layout(label.rectTransform, Vector2.zero, Vector2.zero, pivot, position, new Vector2(Positive(size.x), Positive(size.y)));
        }

        protected void Badge(GameObject parent, string name, string text, Vector2 position, Object font,
            float fontSize, Color color, bool background, Color backgroundColor, Vector2 padding, float cornerRadius,
            ShapeSettings settings = null, string detail = null, Color detailColor = default)
        {
            var rt = Element(parent.transform, name);

            if (background)
            {
                var bg = Shape(rt, "Background", backgroundColor, cornerRadius, settings: settings);
                Stretch(bg.rectTransform);

                if (bg.transform.GetSiblingIndex() != 0)
                    bg.transform.SetAsFirstSibling();
            }

            TextBackend backend = LabelBackend;
            var label = Text(rt, "Text", text, font, fontSize, color, TextAlignmentOptions.Center);
            var preferred = backend.GetPreferredValues(label, text, Mathf.Infinity, Mathf.Infinity);
         
            if (detail == null) { Stretch(label.rectTransform); }
            else
            {
                var value = Text(rt, "Value", detail, font, fontSize, detailColor, TextAlignmentOptions.Center);
                var valueSize = backend.GetPreferredValues(value, detail, Mathf.Infinity, Mathf.Infinity);
                float width = Mathf.Max(preferred.x, valueSize.x);
                Layout(label.rectTransform, Center, Center, Center, new Vector2(0, valueSize.y * 0.5f), new Vector2(width, preferred.y));
                Layout(value.rectTransform, Center, Center, Center, new Vector2(0, -preferred.y * 0.5f), new Vector2(width, valueSize.y));
                preferred = new Vector2(width, preferred.y + valueSize.y);
            }

            Layout(rt, Center, Center, Center, position, preferred + new Vector2(Positive(padding.x), Positive(padding.y)) * 2);
        }
        #endregion

        #region Label Preparation
        protected void BeginLabels()
        {
            bottomLabels.Clear();
            leftLabels.Clear();
            valueLabels.Clear();
        }

        protected void DrawCartesianAxes(GameObject container, PlotFrame frame, float axisThickness, Color color)
        {
            Rect plot = frame.plot;
            float thickness = FitStroke(frame, axisThickness);
            Line(container, plot.position - 0.5f * thickness * Vector2.right, new Vector2(plot.xMax, plot.yMin), color, thickness, "X-Axis", Vector2.zero);
            Line(container, plot.position - 0.5f * thickness * Vector2.up, new Vector2(plot.xMin, plot.yMax), color, thickness, "Y-Axis", Vector2.zero);
        }

        protected void DrawScaleGrid(GameObject container, PlotFrame frame, int divisions, float gridLineThickness, Color color, bool horizontal, float offset = 0)
        {
            Rect plot = frame.plot;
            float length = (horizontal ? plot.width : plot.height) - offset;

            for (int i = 0; i <= divisions; i++)
            {
                Vector2 start = plot.position + (horizontal ? Vector2.right : Vector2.up) * (offset + i * length / divisions);
                Vector2 end = start + (horizontal ? Vector2.up * plot.height : Vector2.right * plot.width);
                Line(container, start, end, color, FitStroke(frame, gridLineThickness), IndexedName(horizontal ? "V-Grid " : "H-Grid ", i), Vector2.zero);
            }
        }

        protected bool HasPlotSpace() => Positive(RectTransform.rect.width) > 0 && Positive(RectTransform.rect.height) > 0;

        protected bool HasPlotSpace(PlotFrame frame)
        {
            if (frame.plot.width > 0 && frame.plot.height > 0)
                return true;

            // Padding can intentionally consume the entire rect. Hide prepared text until
            // there is room again instead of drawing it at a stale/default position.
            foreach (var label in bottomLabels)
                label.backend.SetFontSize(label.graphic, 0);
            foreach (var label in leftLabels)
                label.backend.SetFontSize(label.graphic, 0);
            foreach (var label in valueLabels)
                label.backend.SetFontSize(label.graphic, 0);

            return false;
        }

        protected void PrepareLabel(List<PlotLabel> list, GameObject parent, string name, string text, Object font, float size, Color color)
        {
            TextBackend backend = LabelBackend;
            var graphic = TextElement(Element(parent.transform, name, backend.ComponentType), backend);
            if (!plotLabels.TryGetValue(graphic, out var label))
            {
                label = new PlotLabel { graphic = graphic, backend = backend };
                plotLabels.Add(graphic, label);
            }

            text ??= "";
            font = ResolveFont(backend, font);
            size = Positive(size);
            bool measure = !label.measured || label.text != text || label.font != font || label.requestedSize != size;

            backend.SetText(graphic, text);
            backend.SetFont(graphic, font);
            ColorTransition.SetGraphicColor(graphic, color);

            graphic.raycastTarget = false;
            backend.SetWordWrap(graphic, false);
            backend.SetAutoSize(graphic, false, 0f, 0f);
            backend.SetOverflow(graphic, TextOverflowModes.Ellipsis);
            backend.SetMargin(graphic, Vector4.zero);

            if (measure)
            {
                backend.SetFontSize(graphic, size);
                var preferred = string.IsNullOrEmpty(text) || size <= 0 ? Vector2.zero : backend.GetPreferredValues(graphic, text, Mathf.Infinity, Mathf.Infinity);
                label.preferredSize = preferred == Vector2.zero ? Vector2.zero : new Vector2(Positive(preferred.x) + 2, Positive(preferred.y) + 2);
                label.text = text;
                label.font = font;
                label.requestedSize = size;
                label.measured = true;
            }

            label.fontSize = size;
            list.Add(label);
        }
        #endregion

        #region Plot Measurement
        static Vector2 Measure(PlotLabel label)
        {
            return label.requestedSize > 0 ? label.preferredSize * (label.fontSize / label.requestedSize) : Vector2.zero;
        }

        static Vector2 MeasureAll(List<PlotLabel> labels)
        {
            Vector2 size = Vector2.zero;

            foreach (var label in labels)
                size = Vector2.Max(size, Measure(label));

            return size;
        }

        static Vector2 RotatedSize(Vector2 size, float angle)
        {
            float sin = Mathf.Abs(Mathf.Sin(angle * Mathf.Deg2Rad));
            float cos = Mathf.Abs(Mathf.Cos(angle * Mathf.Deg2Rad));
            return new Vector2(size.x * cos + size.y * sin, size.x * sin + size.y * cos);
        }

        // Edge is the clearance strokes and markers need past the plot: x on the left and right, y on the bottom and top
        protected PlotFrame FitPlot(Vector2 edge, Vector3 padding, LabelRotation rotation, bool horizontalValues,
            float bottomStep = 0, float valueClearance = 0)
        {
            float width = Positive(RectTransform.rect.width);
            float height = Positive(RectTransform.rect.height);
            var content = new Rect(0, 0, width, height);

            // ProceduralShape includes a two-pixel AA fringe outside its RectTransform
            float edgeLimit = Mathf.Min(content.width, content.height) * 0.45f;
            edge = new Vector2(Mathf.Min(Positive(edge.x) + 2, edgeLimit), Mathf.Min(Positive(edge.y) + 2, edgeLimit));

            Vector2 bottomSize = MeasureAll(bottomLabels);
            Vector2 leftSize = MeasureAll(leftLabels);
            Vector2 values = MeasureAll(valueLabels);
            float bottomGap = bottomLabels.Count > 0 ? Positive(horizontalValues ? padding.y : padding.x) : 0;
            float leftGap = leftLabels.Count > 0 ? Positive(horizontalValues ? padding.x : padding.y) : 0;
            float valueGap = valueLabels.Count > 0 ? Positive(padding.z) : 0;

            // Only the text budget is constrained. Requested gaps are always respected.
            float left = leftLabels.Count > 0 ? Mathf.Min(leftSize.x, content.width * 0.35f) + leftGap : 0;
            float right = horizontalValues && valueLabels.Count > 0 ? Mathf.Min(values.x, content.width * 0.3f) + valueGap : 0;
            float top = !horizontalValues && valueLabels.Count > 0
                ? Mathf.Min(values.y, content.height * 0.25f) + valueGap + Positive(valueClearance)
                : 0;
            top = Mathf.Max(top, Mathf.Min(leftSize.y * 0.5f, content.height * 0.1f));

            float baseLeft = Mathf.Max(edge.x, left);
            float baseRight = Mathf.Max(edge.x, right);
            float bottom = edge.y;
            float angle = 0;

            if (bottomStep <= 0)
                bottomStep = 1f / Mathf.Max(1, bottomLabels.Count);

            // Endpoint labels and their rotation both affect the plot width. A bounded pass
            // resolves that dependency without waiting for TMP or the next layout frame.
            for (int pass = 0; pass < 6; pass++)
            {
                float slot = Mathf.Max(0, (content.width - left - right) * bottomStep - 4);
                float autoAngle = bottomSize.x <= slot ? 0 : 45;
                angle = rotation switch
                {
                    LabelRotation.Horizontal => 0,
                    LabelRotation.Angled => 45,
                    _ => autoAngle
                };

                Vector2 fitted = bottomSize;

                if (angle != 0)
                {
                    // Parallel labels can share horizontal space. Their perpendicular
                    // separation limits line height, not the length of the text.
                    float sin = Mathf.Sin(angle * Mathf.Deg2Rad);
                    float cos = Mathf.Cos(angle * Mathf.Deg2Rad);
                    float scale = Mathf.Min(1, Mathf.Max(0, (slot + 4) * sin - 2) / Mathf.Max(0.001f, fitted.y));
                    fitted *= scale;

                    float heightBudget = Mathf.Max(0, content.height - top - bottomGap - edge.y) * 0.55f;
                    fitted.x = Mathf.Max(0, Min(fitted.x, (heightBudget - fitted.y * cos) / sin,
                        (content.width * 0.5f - fitted.y * sin) / cos));
                }

                Vector2 rotated = RotatedSize(fitted, angle);
                bottom = bottomLabels.Count > 0 ? (angle == 0 ? Mathf.Min(rotated.y, content.height * 0.35f) : rotated.y) + bottomGap : 0;
                bottom = Mathf.Max(edge.y, Mathf.Max(bottom, Mathf.Min(leftSize.y * 0.5f, content.height * 0.1f)));

                // Reserve the overhang of first/last labels, even on charts whose data lie at the edges.
                float overhang = angle == 0 ? Mathf.Min(rotated.x, slot) * 0.5f : rotated.x;
                left = Mathf.Max(baseLeft, overhang);
                right = Mathf.Max(baseRight, angle == 0 ? overhang : 0);
            }

            top = Mathf.Max(edge.y, top);

            // Numeric values at the first/last line point also need horizontal overhang.
            if (!horizontalValues && valueLabels.Count > 0)
            {
                left = Mathf.Max(left, Mathf.Min(values.x * 0.5f, content.width * 0.15f));
                right = Mathf.Max(right, Mathf.Min(values.x * 0.5f, content.width * 0.15f));
            }

            return new PlotFrame
            {
                content = content,
                plot = new Rect(content.x + left, content.y + bottom,
                Mathf.Max(0, content.width - left - right), Mathf.Max(0, content.height - bottom - top)),
                angle = angle,
                bottomGap = bottomGap,
                leftGap = leftGap,
                valueGap = valueGap,
                edge = edge
            };
        }

        // Fits strokes and markers whose half width reaches past the plot on every side
        protected static float FitStroke(PlotFrame frame, float thickness) => Mathf.Min(Positive(thickness), Mathf.Max(0, Mathf.Min(frame.edge.x, frame.edge.y) - 2) * 2);
        #endregion

        #region Label Placement
        // The supplied box is the available axis-aligned space, including for rotated text.
        // Keep the selected font size whenever possible; use ellipsis for long names, and
        // reduce the rendered size only when the row/column is shorter than a line of text.
        protected void FitLabel(PlotLabel label, Vector2 center, Vector2 box, float angle = 0,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            box = new Vector2(Positive(box.x), Positive(box.y));

            Vector2 preferred = Measure(label);
            float sin = Mathf.Abs(Mathf.Sin(angle * Mathf.Deg2Rad));
            float cos = Mathf.Abs(Mathf.Cos(angle * Mathf.Deg2Rad));
            float lineLimit = float.MaxValue; // Line height runs along box.y at 0 degrees and box.x at 90 degrees

            if (cos > 0.001f)
                lineLimit = box.y / cos;
            if (sin > 0.001f)
                lineLimit = Mathf.Min(lineLimit, box.x / sin);

            float scale = Mathf.Min(1, lineLimit / Mathf.Max(0.001f, preferred.y));
            label.fontSize *= scale;
            preferred *= scale;
            float length = preferred.x;

            if (cos > 0.001f)
                length = Mathf.Min(length, (box.x - preferred.y * sin) / cos);
            if (sin > 0.001f)
                length = Mathf.Min(length, (box.y - preferred.y * cos) / sin);

            length = Mathf.Max(0, length);
            Vector2 size = new(length, preferred.y);
            label.backend.SetAlignment(label.graphic, alignment);

            if (angle == 0)
            {
                if (alignment == TextAlignmentOptions.Right)
                    center.x += (box.x - length) * 0.5f;
                else if (alignment == TextAlignmentOptions.Left)
                    center.x -= (box.x - length) * 0.5f;
            }

            Layout(label.graphic.rectTransform, Vector2.zero, Vector2.zero, Center, center, size, angle);

            // Text may draw an ellipsis wider than a near-zero box. Suppress impossible text.
            label.backend.SetFontSize(label.graphic, length < 1 || preferred.y < 1 ? 0 : label.fontSize);
        }

        protected void PlaceBottomLabel(PlotLabel label, PlotFrame frame, float x, float slot)
        {
            float height = Mathf.Max(0, frame.plot.yMin - frame.content.yMin - frame.bottomGap);

            if (frame.angle != 0)
            {
                Vector2 size = Measure(label);
                float sin = Mathf.Abs(Mathf.Sin(frame.angle * Mathf.Deg2Rad));
                float cos = Mathf.Abs(Mathf.Cos(frame.angle * Mathf.Deg2Rad));
                float scale = Mathf.Min(1, Mathf.Max(0, slot * sin - 2) / Mathf.Max(0.001f, size.y));
                label.fontSize *= scale;
                size *= scale;
                size.x = Mathf.Max(0, Min(size.x, (height - size.y * cos) / sin, (x - frame.content.xMin - size.y * sin) / cos));

                Vector2 bounds = RotatedSize(size, frame.angle);
                label.backend.SetAlignment(label.graphic, TextAlignmentOptions.Right);
                Layout(label.graphic.rectTransform, Vector2.zero, Vector2.zero, Center,
                    new Vector2(x - bounds.x * 0.5f, frame.plot.yMin - frame.bottomGap - bounds.y * 0.5f), size, frame.angle);

                label.backend.SetFontSize(label.graphic, size.x < 1 || size.y < 1 ? 0 : label.fontSize);
                return;
            }

            float width = Mathf.Max(0, Mathf.Min(slot - 4, 2 * Mathf.Min(x - frame.content.xMin, frame.content.xMax - x)));
            height = Mathf.Min(height, Measure(label).y);
            FitLabel(label, new Vector2(x, frame.plot.yMin - frame.bottomGap - height * 0.5f), new Vector2(width, height), frame.angle);
        }

        protected void PlaceLeftLabel(PlotLabel label, PlotFrame frame, float y, float slot)
        {
            float width = Mathf.Max(0, frame.plot.xMin - frame.content.xMin - frame.leftGap);
            float height = Mathf.Max(0, Mathf.Min(slot - 2, 2 * Mathf.Min(y - frame.content.yMin, frame.content.yMax - y)));
            FitLabel(label, new Vector2(frame.content.xMin + width * 0.5f, y), new Vector2(width, height), alignment: TextAlignmentOptions.Right);
        }

        protected void PlaceValue(PlotLabel label, PlotFrame frame, Vector2 tip, float slot, bool horizontal)
        {
            Vector2 natural = Measure(label);

            if (horizontal)
            {
                float width = Mathf.Max(0, frame.content.xMax - tip.x - frame.valueGap);
                width = Mathf.Min(width, natural.x);
                float height = Mathf.Max(0, Mathf.Min(slot - 2, 2 * Mathf.Min(tip.y - frame.content.yMin, frame.content.yMax - tip.y)));
                FitLabel(label, new Vector2(tip.x + frame.valueGap + width * 0.5f, tip.y), new Vector2(width, height), alignment: TextAlignmentOptions.Left);
            }
            else
            {
                float height = Mathf.Min(natural.y, Mathf.Max(0, frame.content.yMax - tip.y - frame.valueGap));
                float width = Mathf.Max(0, Min(slot - 4, natural.x, frame.plot.width));

                // Endpoint values must not extend into the scale-label column. Keep
                // the full number and shift it inward at either end of the plot.
                float x = Mathf.Clamp(tip.x, frame.plot.xMin + width * 0.5f, frame.plot.xMax - width * 0.5f);
                FitLabel(label, new Vector2(x, tip.y + frame.valueGap + height * 0.5f), new Vector2(width, height));
            }
        }
        #endregion

        #region Pointer Interaction
        // Number of data points the pointer can hover
        protected virtual int PointCount => 0;

        protected virtual DataPoint PointAt(int index) => null;

        // Value text shown by the tooltip. Number text is cached, so hovering doesn't allocate.
        protected virtual string PointValueText(int index) => FormatValue(PointAt(index)?.value ?? 0);

        // Index of the point under a position in chart space (origin at the bottom-left corner), or -1
        protected virtual int HitPoint(Vector2 point) => -1;

        // Point that hover effects apply to: the hovered one, otherwise the one highlighted from code
        protected int FocusIndex => pointerIndex >= 0 ? pointerIndex : highlightIndex;

        // True while this draw records hover targets, at runtime with Interactable on
        protected bool PointerEnabled => pointerEnabled;

        // The focused point keeps the pointer while it is grown, so its edge doesn't flicker
        protected float HitMargin(int index) => index == FocusIndex ? Positive(hoverExpand) : 0;

        /// <summary>
        /// Registers the element a point's hover effects apply to, after it is laid out.
        /// Move and grow are offsets in local UI units per unit of Hover Expand.
        /// </summary>
        protected void SetPointVisual(int index, Graphic graphic, Vector2 move, Vector2 grow)
        {
            if (!pointerEnabled || graphic == null || index < 0)
                return;

            while (pointVisuals.Count <= index)
                pointVisuals.Add(default);

            var rt = graphic.rectTransform;
            pointVisuals[index] = new PointVisual { graphic = graphic, position = rt.anchoredPosition, size = rt.sizeDelta, move = move, grow = grow };
        }

        // Bars: the category index comes from the bar pitch, then the bar or its slot is tested
        protected int HitBar(List<Rect> bars, Rect plot, Vector2 point, bool horizontal)
        {
            int count = bars.Count;
            if (count == 0)
                return -1;

            point = horizontal ? new Vector2(point.y, point.x) : point;
            plot = Swap(plot, horizontal);
            Rect first = Swap(bars[0], horizontal);
            float pitch = count > 1 ? Swap(bars[1], horizontal).x - first.x : first.width;
            float gap = Mathf.Max(0, pitch - first.width) * 0.5f;

            int focus = FocusIndex;
            if (focus >= 0 && focus < count && InBar(Swap(bars[focus], horizontal), plot, point, gap, HitMargin(focus)))
                return focus;

            int index = pitch > 0 ? Mathf.FloorToInt((point.x - first.x + gap) / pitch) : 0;
            return index >= 0 && index < count && InBar(Swap(bars[index], horizontal), plot, point, gap, 0) ? index : -1;
        }

        bool InBar(Rect bar, Rect plot, Vector2 point, float gap, float margin)
        {
            if (pointerArea == PointerArea.Category)
                return point.x >= bar.xMin - gap && point.x <= bar.xMax + gap && point.y >= plot.yMin && point.y <= plot.yMax + margin;

            return point.x >= bar.xMin - margin && point.x <= bar.xMax + margin && point.y >= bar.yMin && point.y <= bar.yMax + margin;
        }

        static Rect Swap(Rect rect, bool swap) => swap ? new Rect(rect.y, rect.x, rect.height, rect.width) : rect;

        internal bool IsPointHit(Vector2 screenPoint, Camera camera) => HitTest(screenPoint, camera) >= 0;

        internal void MovePointer(Vector2 screenPoint, Camera camera) => SetPointerIndex(HitTest(screenPoint, camera));

        internal void ExitPointer() => SetPointerIndex(-1);

        internal void ClickPointer(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            int index = HitTest(eventData.position, eventData.pressEventCamera);
            if (index >= 0)
                onPointClicked.Invoke(index);
        }

        int HitTest(Vector2 screenPoint, Camera camera)
        {
            if (!pointerEnabled || !RectTransformUtility.ScreenPointToLocalPointInRectangle(RectTransform, screenPoint, camera, out Vector2 point))
                return -1;

            int index = HitPoint(point - RectTransform.rect.min);
            return index < PointCount ? index : -1;
        }

        void SetPointerIndex(int index)
        {
            if (index == pointerIndex)
                return;

            int previous = pointerIndex;
            pointerIndex = index;
            UpdateTooltip();
            AnimateHover(true);

            // Listeners run after the change, so they read the new hovered index
            if (previous >= 0)
                onPointExited.Invoke(previous);
            if (index >= 0)
                onPointEntered.Invoke(index);
        }

        void UpdateTooltip()
        {
            if (tooltip == null)
            {
                tooltipShown = false;
                return;
            }

            if (pointerIndex < 0)
            {
                if (tooltipShown)
                    tooltip.Hide();

                tooltipShown = false;
                return;
            }

            var visual = pointerIndex < pointVisuals.Count ? pointVisuals[pointerIndex] : default;
            tooltip.Target = visual.graphic != null ? visual.graphic.rectTransform : RectTransform;
            tooltip.Title = PointAt(pointerIndex)?.label;
            tooltip.Description = PointValueText(pointerIndex);

            if (!tooltipShown)
                tooltip.Show();

            tooltipShown = true;
        }

        // Every point eases from its current look to the new focus in one shared tween
        void AnimateHover(bool animate)
        {
            int focus = FocusIndex;
            bool dims = dimmedOpacity < 1;
            bool changed = false;

            while (hoverStates.Count < pointVisuals.Count)
                hoverStates.Add(default);

            for (int i = 0; i < hoverStates.Count; i++)
            {
                var state = hoverStates[i];
                state.growFrom = state.grow;
                state.dimFrom = state.dim;
                state.growTo = i == focus ? 1 : 0;
                state.dimTo = dims && focus >= 0 && i != focus ? 1 : 0;
                changed |= state.growFrom != state.growTo || state.dimFrom != state.dimTo;
                hoverStates[i] = state;
            }

            hoverAnimation.Kill();

            if (!changed)
                return;

            if (animate && Positive(hoverDuration) > 0 && isActiveAndEnabled)
            {
                hoverAnimation = EvoTween.Create(0f, 1f, Positive(hoverDuration))
                    .WithEase(Ease.OutCubic)
                    .WithTimeMode(TimeMode.Unscaled)
                    .Bind(this, static (chart, progress) => chart.UpdateHover(progress), 0);
            }
            else { UpdateHover(1); }
        }

        void UpdateHover(float progress)
        {
            for (int i = 0; i < hoverStates.Count; i++)
            {
                var state = hoverStates[i];
                bool grows = state.growFrom != state.growTo;
                bool dims = state.dimFrom != state.dimTo;

                if (!grows && !dims)
                    continue;

                state.grow = Mathf.LerpUnclamped(state.growFrom, state.growTo, progress);
                state.dim = Mathf.LerpUnclamped(state.dimFrom, state.dimTo, progress);
                hoverStates[i] = state;
                ApplyHover(i, grows, dims);
            }
        }

        void ApplyHover(int index, bool layout, bool alpha)
        {
            if (index >= pointVisuals.Count)
                return;

            var visual = pointVisuals[index];
            if (visual.graphic == null)
                return;

            var state = hoverStates[index];

            if (layout)
            {
                float expand = Positive(hoverExpand) * state.grow;
                var rt = visual.graphic.rectTransform;
                rt.anchoredPosition = visual.position + visual.move * expand;
                rt.sizeDelta = visual.size + visual.grow * expand;
            }

            // Renderer alpha leaves the graphic color to Styler and its transitions
            if (alpha)
                visual.graphic.canvasRenderer.SetAlpha(Mathf.LerpUnclamped(1, Mathf.Clamp01(dimmedOpacity), state.dim));
        }

        void DrawHitArea()
        {
            var area = Container("Hit Area", typeof(ChartHitArea));
            if (hitArea == null || hitArea.gameObject != area)
                hitArea = area.GetComponent<ChartHitArea>();

            hitArea.chart = this;
        }

        // A draw lays elements out again at their base size, so hover effects are applied on top
        void SyncHover()
        {
            if (pointerIndex >= PointCount)
                SetPointerIndex(-1);
            if (highlightIndex >= PointCount)
                HighlightedIndex = -1;

            int focus = FocusIndex;
            bool dims = dimmedOpacity < 1 && focus >= 0;

            if (hoverStates.Count > pointVisuals.Count)
                hoverStates.RemoveRange(pointVisuals.Count, hoverStates.Count - pointVisuals.Count);

            // New points join the current focus without a transition
            while (hoverStates.Count < pointVisuals.Count)
            {
                float grow = hoverStates.Count == focus ? 1 : 0;
                float dim = dims && hoverStates.Count != focus ? 1 : 0;
                hoverStates.Add(new HoverState { grow = grow, growFrom = grow, growTo = grow, dim = dim, dimFrom = dim, dimTo = dim });
            }

            for (int i = 0; i < hoverStates.Count; i++)
            {
                var state = hoverStates[i];
                if (state.grow != 0 || state.dim != 0)
                    ApplyHover(i, state.grow != 0, state.dim != 0);
            }

            if (pointerIndex >= 0)
                UpdateTooltip();
        }

        void ResetHover(bool notify)
        {
            if (pointerIndex < 0 && highlightIndex < 0 && hoverStates.Count == 0 && !tooltipShown)
                return;

            hoverAnimation.Kill();
            int previous = pointerIndex;
            pointerIndex = highlightIndex = -1;
            UpdateTooltip();

            for (int i = 0; i < hoverStates.Count; i++)
            {
                var state = hoverStates[i];
                bool layout = state.grow != 0;
                bool alpha = state.dim != 0;
                hoverStates[i] = default;

                if (layout || alpha)
                    ApplyHover(i, layout, alpha);
            }

            hoverStates.Clear();

            if (notify && previous >= 0)
                onPointExited.Invoke(previous);
        }

        // Applies changed hover settings to the current focus
        void RefreshHover()
        {
            for (int i = 0; i < hoverStates.Count; i++)
                ApplyHover(i, true, true);

            AnimateHover(false);
        }
        #endregion

        #region Shared Types
        /// <summary>
        /// Serialized data shared by every chart. Each chart exposes its own data point type.
        /// </summary>
        [System.Serializable]
        public class DataPoint
        {
            [Tooltip("Category name shown by the chart or legend.")]
            public string label;
            [Tooltip("Data value. Non-finite values render as zero; pie, bar and radar charts also render negative values as zero.")]
            public float value;
#if EVO_LOCALIZATION
            [Tooltip("Localization key for label.")]
            public string tableKey;

#endif

            public DataPoint(string label = "", float value = 0)
            {
                this.label = label;
                this.value = value;
            }
        }

        /// <summary>
        /// A labeled chart value with a color for pie slices or individual bars.
        /// </summary>
        [System.Serializable]
        public class ColoredDataPoint : DataPoint
        {
            [Tooltip("Slice color, or per-bar color when Use Data Colors is enabled.")]
            public Color color;

            public ColoredDataPoint(string label = "", float value = 0, Color? color = null) : base(label, value)
            {
                this.color = color ?? Color.white;
            }
        }

        /// <summary>
        /// Controls how labels along the bottom axis rotate to fit the available space.
        /// </summary>
        public enum LabelRotation { Auto = 0, Horizontal = 1, Angled = 2 }

        /// <summary>
        /// Where the pointer reacts to a point: over the drawn element only, or over its whole category slot.
        /// </summary>
        public enum PointerArea { Element = 0, Category = 1 }

        /// <summary>
        /// The element a point's hover effects apply to, with its laid-out position and size.
        /// </summary>
        struct PointVisual
        {
            public Graphic graphic;
            public Vector2 position, size, move, grow;
        }

        /// <summary>
        /// Current, start and end amounts of a point's grow and dim effects.
        /// </summary>
        struct HoverState
        {
            public float grow, growFrom, growTo;
            public float dim, dimFrom, dimTo;
        }

        /// <summary>
        /// Stores the content and plot bounds, label gaps, rotation, and horizontal/vertical edge clearance used to lay out a chart.
        /// </summary>
        protected struct PlotFrame
        {
            public Rect content, plot;
            public float angle, bottomGap, leftGap, valueGap;
            public Vector2 edge;
        }

        /// <summary>
        /// Caches a generated text label's content, font, and measured size while tracking its fitted font size.
        /// </summary>
        protected sealed class PlotLabel
        {
            public Graphic graphic;
            public TextBackend backend;
            public Object font;
            public string text;
            public Vector2 preferredSize;
            public float requestedSize, fontSize;
            public bool measured;
        }

        /// <summary>
        /// Keeps the values one draw used for the next draw. Entries a draw doesn't use are released after it.
        /// </summary>
        sealed class DrawCache<TKey, TValue>
        {
            Dictionary<TKey, TValue> current = new();
            Dictionary<TKey, TValue> previous = new();

            public bool TryGetValue(TKey key, out TValue value)
            {
                if (current.TryGetValue(key, out value))
                    return true;

                if (!previous.TryGetValue(key, out value))
                    return false;

                current.Add(key, value);
                return true;
            }

            public void Add(TKey key, TValue value) => current.Add(key, value);

            public void Next()
            {
                (current, previous) = (previous, current);
                current.Clear();
            }

            public void Clear()
            {
                current.Clear();
                previous.Clear();
            }
        }

        /// <summary>
        /// Shared procedural styling. Each chart chooses its own uniform corner radius.
        /// </summary>
        [System.Serializable]
        public sealed class ShapeSettings
        {
            [Tooltip("Interpret corner radii as pixels or percentages.")]
            public ProceduralShape.RadiusMode radiusMode = ProceduralShape.RadiusMode.Pixels;
            [Tooltip("Use separate corner radii instead of the chart's uniform radius.")]
            public bool independentCorners;
            [Tooltip("Corner radii in top-left, top-right, bottom-right, bottom-left order.")]
            public Vector4 cornerRadii;
            [Tooltip("Use squircle curves for smoother transitions between corners and straight edges.")]
            public bool squircleCorners;
            [Range(0, 32), Tooltip("Additional edge softness in local UI units, from 0 to 32.")]
            public float softness;
            [Min(0), Tooltip("Procedural outline width in local UI units.")]
            public float outlineWidth;
            [Tooltip("Procedural outline color, including opacity.")]
            public Color outlineColor = Color.white;

            internal void Apply(ProceduralShape shape, float uniformRadius, float softnessLimit)
            {
                float limit = radiusMode == ProceduralShape.RadiusMode.Percentage ? 100 : float.MaxValue;
                Vector4 radii = independentCorners ? cornerRadii : Vector4.one * Positive(uniformRadius);

                for (int i = 0; i < 4; i++)
                    radii[i] = Mathf.Min(limit, Positive(radii[i]));

                float edge = Min(32, Positive(softness), Positive(softnessLimit));
                float outline = Positive(outlineWidth);
                shape.CornerRadiusMode = radiusMode;
                shape.IndependentCorners = independentCorners;
                shape.SquircleCorners = squircleCorners;
                shape.Softness = edge;
                shape.OutlineWidth = outline;
                shape.OutlineColor = outlineColor;
                shape.CornerRadius = radii;
            }
        }

        /// <summary>
        /// Value-only fingerprint used to coalesce repeated event-driven refresh requests.
        /// </summary>
        protected struct ChartState
        {
            public ulong Value;

            public void Add(int value)
            {
                unchecked
                {
                    Value = (Value ^ (uint)value) * 1099511628211UL + 1;
                }
            }

            public void Add(bool value) => Add(value ? 1 : 0);

            public void Add(float value) => Add(value.GetHashCode());

            public void Add(string value) => Add(value?.GetHashCode() ?? 0);

#if UNITY_6000_3_OR_NEWER
            // This is a fingerprint, not an identity key: hashing the full EntityId is intentional
            public void Add(Object value) => Add(value != null ? value.GetEntityId().GetHashCode() : 0);
#else
            public void Add(Object value) => Add(value != null ? value.GetInstanceID() : 0);
#endif

            public void Add(Vector2 value)
            {
                Add(value.x);
                Add(value.y);
            }

            public void Add(Vector3 value)
            {
                Add(value.x);
                Add(value.y);
                Add(value.z);
            }

            public void Add(Vector4 value)
            {
                Add(value.x);
                Add(value.y);
                Add(value.z);
                Add(value.w);
            }

            public void Add(ShapeSettings value)
            {
                if (value == null)
                {
                    Add(0);
                    return;
                }

                Add((int)value.radiusMode);
                Add(value.independentCorners);
                Add(value.cornerRadii);
                Add(value.squircleCorners);
                Add(value.softness);
                Add(value.outlineWidth);
                Add(value.outlineColor);
            }

            public void Add(Color value)
            {
                Add(value.r);
                Add(value.g);
                Add(value.b);
                Add(value.a);
            }
        }
        #endregion

        #region Public API
        /// <summary>
        /// Numeric format used by value and scale labels.
        /// </summary>
        public NumberDisplayFormat DisplayFormat
        {
            get => displayFormat;
            set => SetValue(ref displayFormat, value);
        }

        /// <summary>
        /// Schedules a chart refresh after directly changing public data or settings at runtime.
        /// </summary>
        public void MarkDirty()
        {
            pendingClear = false;
            DrawChartSafe();
        }

        /// <summary>
        /// Refresh immediately, including when editor Auto Refresh is disabled.
        /// </summary>
        public void Refresh() => DrawChart();

        /// <summary>
        /// Respond to the pointer at runtime with hover effects, the tooltip and point events.
        /// </summary>
        public bool Interactable
        {
            get => interactable;
            set => SetValue(ref interactable, value);
        }

        /// <summary>
        /// Where the pointer reacts to a point: over the drawn element only, or over its whole category slot.
        /// Pie charts always use the slice.
        /// </summary>
        public PointerArea PointerAreaMode
        {
            get => pointerArea;
            set => pointerArea = value;
        }

        /// <summary>
        /// How far the hovered bar or point grows, or a hovered slice moves out, in local UI units.
        /// </summary>
        public float HoverExpand
        {
            get => hoverExpand;
            set
            {
                hoverExpand = Positive(value);
                RefreshHover();
            }
        }

        /// <summary>
        /// Opacity of the other bars, points or slices while one is hovered, from 0 to 1. 1 keeps them unchanged.
        /// </summary>
        public float DimmedOpacity
        {
            get => dimmedOpacity;
            set
            {
                dimmedOpacity = Mathf.Clamp01(Finite(value));
                RefreshHover();
            }
        }

        /// <summary>
        /// Hover transition length in seconds, on unscaled time. Zero applies hover effects at once.
        /// </summary>
        public float HoverDuration
        {
            get => hoverDuration;
            set => hoverDuration = Positive(value);
        }

        /// <summary>
        /// Index of the data point under the pointer, or -1.
        /// </summary>
        public int HoveredIndex => pointerIndex;

        /// <summary>
        /// Highlights a data point from code as if it were hovered, without events or the tooltip.
        /// -1 clears it. The pointer takes over while it hovers a point. Requires Interactable.
        /// </summary>
        public int HighlightedIndex
        {
            get => highlightIndex;
            set
            {
                value = value >= 0 && value < PointCount ? value : -1;
                if (highlightIndex == value)
                    return;

                highlightIndex = value;
                AnimateHover(true);
            }
        }
        #endregion

#if EVO_LOCALIZATION
        [Tooltip("Resolve data label table keys through the attached localized object at runtime.")]
        public bool enableLocalization = true;
        [Tooltip("Localization source used to resolve data point table keys.")]
        public Localization.LocalizedObject localizedObject;

        protected abstract void LocalizeLabels();

        protected void LocalizePoints<T>(List<T> data) where T : DataPoint
        {
            if (data == null)
                return;

            foreach (var point in data)
            {
                if (point != null && !string.IsNullOrEmpty(point.tableKey))
                    point.label = localizedObject.GetString(point.tableKey) ?? point.label;
            }
        }

        void UpdateLocalization(Localization.LocalizationLanguage language = null)
        {
            if (!enableLocalization || localizedObject == null)
                return;

            LocalizeLabels();
            DrawChartSafe();
        }
#endif
    }
}