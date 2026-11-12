using System;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Evo.UI
{
    /// <summary>
    /// iOS-style wheel that turns its items on a drum, with momentum, snapping and optional looping.
    /// Direct children containing a text component are reused as rows, so only the visible rows are drawn.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(RectTransform)), NoAutoStaticsCleanup]
    [AddComponentMenu("Evo/UI/UI Elements/Picker Wheel"), HelpURL(Constants.HelpUrl + "ui-elements/time-picker")]
    public class PickerWheel : Selectable, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler,
        IEndDragHandler, IScrollHandler, IPointerClickHandler
    {
        // Items
        [SerializeField, Tooltip("Labels of the wheel's items. Replaced while a Time Picker or SetItems supplies them.")]
        private List<string> items = new();
        [SerializeField, Tooltip("Index of the selected item.")]
        private int selectedIndex = 0;
        [SerializeField, Tooltip("Continues from the last item to the first, like an endless drum.")]
        private bool loop = true;

        // Appearance
        [SerializeField, Tooltip("Row that every other row copies, so restyling it restyles the whole wheel. " +
            "Copies are hidden and never saved. Defaults to the first child with a text component.")]
        private RectTransform itemTemplate;
        [SerializeField, Min(1f), Tooltip("Row height at the center. The wheel's height sets the curvature of the drum.")]
        private float itemHeight = 40f;
        [SerializeField, Range(0f, 1f), Tooltip("Perceived opacity of rows away from the center, before they fade around the drum.")]
        private float unselectedAlpha = 0.55f;

        // Motion
        [SerializeField, Range(0.001f, 0.5f), Tooltip("Share of the velocity kept after one second of free spinning. Higher values spin farther.")]
        private float decelerationRate = 0.05f;

        // Events
        [Tooltip("Invoked with the item index when the wheel settles on another item, or when a notifying selection call changes it.")]
        public UnityEvent<int> onValueChanged = new();

        // Time Picker
        internal event Action<PickerWheel, int, int> RawIndexCommitted;
        internal event Action<PickerWheel> CenterTicked;

        // Constants
        const float SnapFrequency = 12f; // Critically damped stiffness of snaps, steps and taps
        const float FlingVelocity = 3f; // Release speed in rows per second that keeps the wheel spinning
        const float MaxVelocity = 80f;
        const float MinFlingDuration = 0.3f;
        const float MaxFlingDuration = 2f;
        const float VelocityWindow = 0.1f;
        const float PerspectiveShrink = 0.15f; // Width lost by rows at the rim of the drum
        const float RubberBandFactor = 0.55f;
        const float SettleDistance = 0.25f;
        const float SettleSpeed = 8f;
        const int SampleCapacity = 16;
        const DrivenTransformProperties DrivenRowProperties = DrivenTransformProperties.Anchors | DrivenTransformProperties.Pivot
            | DrivenTransformProperties.AnchoredPositionY | DrivenTransformProperties.SizeDeltaY | DrivenTransformProperties.Scale;
        const HideFlags CopyFlags = HideFlags.HideAndDontSave;

        // Rows
        readonly List<Row> rows = new();
        readonly List<Row> rowPool = new(); // Rows are a prefix of the pool, so collecting them again allocates nothing
        static readonly List<Graphic> graphicBuffer = new();
        static readonly List<Component> componentBuffer = new();
        DrivenRectTransformTracker tracker;
        DrivenRectTransformTracker templateTracker;
        RectTransform drivenTemplate;
        RectTransform cachedRect;
        bool rowsDirty = true;
        bool layoutDirty = true;
        bool copiesStale;
        bool editingRows;
        float preparedHeight = -1f;
        float preparedWidth = float.NaN;
        float radius;
        float angleStep;
        float halfRows;
        int rowPhase; // Lets rows keep their items when a resting loop returns to its first turn

        // Data
        IReadOnlyList<string> source;
        int dataVersion;

        // Motion
        enum Motion { Idle, Dragging, Held, Moving }
        Motion motion;
        double position;
        double velocity;
        int targetRaw;
        int committedRaw;
        int centerRaw;
        bool allowOvershoot;
        bool flinging;
        double flingStart;
        double flingDistance;
        float flingDuration;
        float flingTime;
        bool userMotion;
        bool stateReady;
        bool pressStoppedMotion;
        bool pendingInspectorIndex;

        // Input
        bool isDragging;
        float dragStartY;
        double dragStartPosition;
        float scrollRemainder;
        GameObject forwardTarget;
        readonly double[] sampleTimes = new double[SampleCapacity];
        readonly double[] samplePositions = new double[SampleCapacity];
        int sampleHead;
        int sampleCount;

        sealed class Row
        {
            public RectTransform rect;
            public Graphic text;
            public TextBackend backend;
            public CanvasGroup group;
            public bool isCopy;
            public int raw;
            public int version;
            public bool visible;
            public float y;
            public Vector3 scale; // Last scale and fade of the row, which only the wheel sets
            public float alpha;
        }

        RectTransform Rect => cachedRect != null ? cachedRect : cachedRect = (RectTransform)transform;
        IReadOnlyList<string> Data => source ?? items;
        int Count => Data != null ? Data.Count : 0;
        float DecayTime => -1f / Mathf.Log(Mathf.Clamp(decelerationRate, 0.001f, 0.5f));

        // Row the wheel rests on, or is heading to
        int DestinationRaw => motion == Motion.Moving ? targetRaw : RoundToInt(position);

        protected override void OnEnable()
        {
            base.OnEnable();
            rowsDirty = true;
            layoutDirty = true;

#if UNITY_EDITOR
            if (!Application.IsPlaying(gameObject))
            {
                ListenForTemplateChanges(true);
                RequestEditorRefresh();
            }
#endif
        }

        protected override void Start()
        {
            base.Start();

            if (Application.IsPlaying(gameObject))
                RaycastArea.ResolveRaycast(gameObject);
        }

        protected override void OnDisable()
        {
            if (Application.IsPlaying(gameObject))
                SettleImmediately();

            tracker.Clear();
            ReleaseTemplate();
            isDragging = false;
            forwardTarget = null;

#if UNITY_EDITOR
            CancelEditorRefresh();
            ListenForTemplateChanges(false);

            // Copies are unsaved and only shown while the wheel draws them, as when its component is removed.
            // An undo or redo that disables the wheel may destroy it next and warn about copies it never recorded,
            // so they are removed then. Enabling the wheel copies the template again.
            // Unity 6.7 crashes when they're destroyed while an undo disables them, so it only hides them.
            if (!Application.IsPlaying(gameObject))
            {
#if !UNITY_6000_7_OR_NEWER
                if (UnityEditor.Undo.isProcessing)
                {
                    CollectCopies(false);
                    rowsDirty = true;
                }
                else
#endif
                {
                    foreach (Row row in rows)
                    {
                        if (row.rect != null && row.text != null && IsCopy(row.rect))
                            SetAlpha(row, 0f, false);
                    }
                }
            }
#endif
            base.OnDisable();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            layoutDirty = true;

            // Layout groups size the wheel after LateUpdate, so draw right away instead of a frame late
            if (Application.IsPlaying(gameObject))
            {
                if (isActiveAndEnabled)
                    LayoutRows(true);
            }
#if UNITY_EDITOR
            else
            {
                // Edit Mode draws during canvas layouts too, as captures such as Catalog previews can come before the delayed refresh.
                // Not when that would copy the template again, as removing copies during a canvas rebuild is not supported.
                if (isActiveAndEnabled && CanvasUpdateRegistry.IsRebuildingLayout() && CanPreviewNow() && (!rowsDirty || !HasCopies()))
                    LayoutRows(false);

                RequestEditorRefresh();
            }
#endif
        }

        void OnTransformChildrenChanged()
        {
            if (editingRows)
                return;

            rowsDirty = true;
            layoutDirty = true;

#if UNITY_EDITOR
            if (!Application.IsPlaying(gameObject))
                RequestEditorRefresh();
#endif
        }

        void LateUpdate()
        {
            // A resting wheel returns before any engine call
            if (!pendingInspectorIndex && motion != Motion.Moving && !rowsDirty && !layoutDirty)
                return;

#if UNITY_EDITOR
            // Players are always in Play Mode
            if (!Application.IsPlaying(gameObject))
                return;
#endif

            if (pendingInspectorIndex)
            {
                pendingInspectorIndex = false;
                if (Count > 0 && selectedIndex != WrapIndex(committedRaw))
                {
                    int index = selectedIndex;
                    selectedIndex = WrapIndex(committedRaw);
                    SetIndex(index);
                }
            }

            // A settling wheel draws once, at the row it lands on
            if (motion == Motion.Moving && StepMotion(Time.unscaledDeltaTime))
                CompleteMotion();
            else if (rowsDirty || layoutDirty)
                LayoutRows(true);
        }

        #region Rows
        void RefreshRows()
        {
            rowsDirty = false;
            preparedHeight = -1f;
            preparedWidth = float.NaN;
            rowPhase = 0;
            rows.Clear();
            tracker.Clear();

            RectTransform template = FindTemplate();
            bool hasTemplate = template != null && AddRow(template, FindText(template));
            if (!hasTemplate)
                ReleaseTemplate();

            // Existing copies are reused until an edit marks them stale. In Edit Mode that keeps the copies a redo brings back,
            // which Unity's undo already knows, instead of making new ones it would report as dangling.
            bool reuse = hasTemplate && !copiesStale;
            copiesStale = false;
            CollectCopies(reuse);
        }

        // Adds active copies as rows when reusing them, and destroys the others
        void CollectCopies(bool reuse)
        {
            editingRows = true;

            try
            {
                for (int i = transform.childCount - 1; i >= 0; i--)
                {
                    if (transform.GetChild(i) is RectTransform child && IsCopy(child)
                        && !(reuse && child.gameObject.activeSelf && AddRow(child, FindText(child))))
                        DestroyCopy(child.gameObject);
                }
            }
            finally { editingRows = false; }
        }

        RectTransform FindTemplate()
        {
            if (itemTemplate != null && itemTemplate.parent == transform && !IsCopy(itemTemplate))
                return itemTemplate;

            Transform parent = transform;
            for (int i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i) is RectTransform rect && rect.gameObject.activeSelf && !IsCopy(rect) && FindText(rect) != null)
                    return rect;
            }

            return null;
        }

        static bool IsCopy(Transform target) => (target.gameObject.hideFlags & CopyFlags) == CopyFlags;

        void DestroyCopy(GameObject copy)
        {
            if (Application.IsPlaying(gameObject))
            {
                copy.SetActive(false);
                Destroy(copy);
            }
            else { DestroyImmediate(copy); }
        }

        bool AddRow(RectTransform rect, Graphic text)
        {
            if (text == null)
                return false;

            Row row;
            if (rows.Count < rowPool.Count) { row = rowPool[rows.Count]; }
            else { rowPool.Add(row = new Row()); }

            row.rect = rect;
            row.text = text;
            row.backend = TextProvider.Find(text);
            row.group = null;
            row.isCopy = IsCopy(rect);
            row.raw = int.MinValue;
            row.version = -1;
            row.visible = false;
            row.y = 0f;

            if (Application.IsPlaying(gameObject))
            {
                // Groups fade every renderer of a row, including fallback font meshes
                if (!rect.TryGetComponent(out row.group))
                    row.group = rect.gameObject.AddComponent<CanvasGroup>();

                // Clears an Edit Mode preview kept by entering Play Mode without a scene reload
                text.canvasRenderer.SetAlpha(1f);
            }

            row.scale = rect.localScale;
            row.alpha = row.group != null ? row.group.alpha : text.canvasRenderer.GetAlpha();

            // The template stays driven while it's the template, as driving it anew in an open undo group records its driven values as changes
            if (row.isCopy) { tracker.Add(this, rect, DrivenRowProperties); }
            else if (drivenTemplate != rect)
            {
                templateTracker.Clear();
                templateTracker.Add(this, rect, DrivenRowProperties);
                drivenTemplate = rect;
            }

            rows.Add(row);
            return true;
        }

        void ReleaseTemplate()
        {
            templateTracker.Clear();
            drivenTemplate = null;
        }

        static Graphic FindText(Transform target)
        {
            target.GetComponentsInChildren(true, graphicBuffer);
            Graphic text = null;

            foreach (Graphic graphic in graphicBuffer)
            {
                if (TextProvider.IsText(graphic))
                {
                    text = graphic;
                    break;
                }
            }

            graphicBuffer.Clear();
            return text;
        }

        int RequiredRows => Mathf.FloorToInt(halfRows * 2f) + 2;

        void EnsureRowCount()
        {
            if (rows.Count == 0 || rows.Count >= RequiredRows)
                return;

            GameObject template = rows[0].rect.gameObject;
            editingRows = true;

            try
            {
                while (rows.Count < RequiredRows)
                {
                    GameObject copy = Instantiate(template, Rect, false);
                    copy.name = template.name;

                    // Hidden and never saved, so scenes and prefabs only hold the template.
                    // Unity takes these flags on a Transform only once its GameObject has them.
                    copy.GetComponentsInChildren(true, componentBuffer);
                    foreach (Component component in componentBuffer)
                        component.gameObject.hideFlags = CopyFlags;
                    foreach (Component component in componentBuffer)
                        component.hideFlags = CopyFlags;

                    componentBuffer.Clear();
                    if (!AddRow((RectTransform)copy.transform, FindText(copy.transform)))
                    {
                        DestroyCopy(copy);
                        break;
                    }
                }
            }
            finally { editingRows = false; }

            preparedHeight = -1f;
            preparedWidth = float.NaN;
        }

        void PrepareRows()
        {
            preparedHeight = itemHeight;
            var anchorMin = new Vector2(0f, 0.5f);
            var anchorMax = new Vector2(1f, 0.5f);
            var pivot = new Vector2(0.5f, 0.5f);

            foreach (Row row in rows)
            {
                RectTransform rect = row.rect;
                if (rect == null)
                    continue;

                if (rect.anchorMin != anchorMin)
                    rect.anchorMin = anchorMin;
                if (rect.anchorMax != anchorMax)
                    rect.anchorMax = anchorMax;
                if (rect.pivot != pivot)
                    rect.pivot = pivot;

                Vector2 size = rect.sizeDelta;
                if (size.y != itemHeight)
                {
                    size.y = itemHeight;
                    rect.sizeDelta = size;
                }
            }
        }

        void UpdateGeometry(float height)
        {
            // The drum spans the wheel's height, so taller wheels curve more gently
            radius = Mathf.Max(height, itemHeight) * 0.5f;
            angleStep = itemHeight / radius;
            halfRows = Mathf.PI * 0.5f / angleStep;
        }

        void LayoutRows(bool playing)
        {
            layoutDirty = false;

            if (rowsDirty)
                RefreshRows();

            // Wait for a size, as saved prefabs zero the driven sizes of layout group children
            int count = Count;
            float height = Rect.rect.height;
            if (rows.Count == 0 || count == 0 || height <= 0f)
                return;

            EnsureState();
            UpdateGeometry(height);
            EnsureRowCount();

            if (preparedHeight != itemHeight)
                PrepareRows();

            // Each raw index keeps its row while visible, so only rows entering the view change text
            int rowCount = rows.Count;
            int first = (int)Math.Ceiling(position - halfRows);
            int last = (int)Math.Floor(position + halfRows);
            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;

            // Copies follow the template's horizontal placement, which the wheel leaves to the user
            RectTransform templateRect = rows[0].rect;
            float x = templateRect != null ? templateRect.anchoredPosition.x : 0f;
            float width = templateRect != null ? templateRect.sizeDelta.x : 0f;
            bool matchWidth = width != preparedWidth;
            preparedWidth = width;

            for (int i = 0; i < rowCount; i++)
            {
                Row row = rows[i];
                if (row.rect == null || row.text == null)
                {
                    rowsDirty = true;
                    continue;
                }

                // Copies, hidden ones too, take a new template width once
                if (matchWidth && i > 0 && row.rect.sizeDelta.x != width)
                    row.rect.sizeDelta = new Vector2(width, itemHeight);

                int raw = first + Mod(i - rowPhase - first, rowCount);
                if (raw > last || (!loop && (raw < 0 || raw >= count)))
                {
                    row.visible = false;
                    SetAlpha(row, 0f, playing);
                    continue;
                }

                float offset = (float)(raw - position);
                float angle = offset * angleStep;
                float cos = Mathf.Max(0f, Mathf.Cos(angle));
                float depth = 1f - PerspectiveShrink * (1f - cos);
                float y = -radius * Mathf.Sin(angle);

                SetText(row, raw, count, playing);

                var anchored = new Vector2(x, y);
                if (row.rect.anchoredPosition != anchored)
                    row.rect.anchoredPosition = anchored;

                var scale = new Vector3(depth, cos * depth, 1f);
                if (row.scale != scale)
                {
                    row.scale = scale;
                    row.rect.localScale = scale;
                }

                // Rows dim off center and fade around the drum. Linear projects blend alpha in linear
                // space, so the fade is converted to look the same as in Gamma projects.
                float alpha = cos * Mathf.Lerp(1f, unselectedAlpha, Mathf.Abs(offset));
                SetAlpha(row, linear ? Mathf.GammaToLinearSpace(alpha) : alpha, playing);
                row.visible = true;
                row.y = y;
            }

            UpdateCenter(count);
        }

        void SetText(Row row, int raw, int count, bool playing)
        {
            if (row.raw == raw && row.version == dataVersion)
                return;

            row.raw = raw;
            row.version = dataVersion;
            string label = Data[loop ? Mod(raw, count) : raw] ?? string.Empty;

            if (row.backend == null || row.backend.GetText(row.text) == label)
                return;

            row.backend.SetText(row.text, label);
#if UNITY_EDITOR
            // Copies are never saved, and marking them would flag the scene as modified
            if (!playing && !row.isCopy)
                SaveEditorChanges(row.text);
#endif
        }

        static void SetAlpha(Row row, float alpha, bool playing)
        {
            if (row.alpha == alpha)
                return;

            row.alpha = alpha;
            if (playing && row.group != null) { row.group.alpha = alpha; }
            else { row.text.canvasRenderer.SetAlpha(alpha); } // Edit Mode fades the renderer, which is never saved
        }

        void UpdateCenter(int count)
        {
            int center = RoundToInt(position);
            if (center == centerRaw)
                return;

            centerRaw = center;
            if (userMotion && (loop || (center >= 0 && center < count)))
                CenterTicked?.Invoke(this);
        }
        #endregion

        #region Motion
        void EnsureState()
        {
            if (stateReady)
                return;

            stateReady = true;
            int count = Count;
            int index = count > 0 ? WrapIndex(selectedIndex) : Mathf.Max(0, selectedIndex);
            motion = Motion.Idle;
            velocity = 0d;
            position = committedRaw = targetRaw = centerRaw = index;
        }

        void StartMotion(int raw, bool overshoot = false)
        {
            targetRaw = raw;
            allowOvershoot = overshoot;
            flinging = false;
            motion = Motion.Moving;
            layoutDirty = true;
        }

        void StartFling(int raw, double speed)
        {
            // A cubic ease-out starts at the release speed, so the wheel glides on and settles in a set time
            StartMotion(raw);
            flinging = true;
            flingStart = position;
            flingDistance = raw - position;
            flingDuration = (float)(3d * flingDistance / speed);
            flingTime = 0f;
        }

        bool StepMotion(float deltaTime)
        {
            if (deltaTime <= 0f)
                return false;

            layoutDirty = true;

            if (flinging)
            {
                flingTime += deltaTime;
                double remaining = 1d - Math.Min(1d, flingTime / flingDuration);
                position = flingStart + flingDistance * (1d - remaining * remaining * remaining);
                velocity = flingDistance * 3d * remaining * remaining / flingDuration;
                return remaining <= 0d;
            }

            // Exact critically damped step, so motion is identical at any frame rate
            double offset = position - targetRaw;
            double decay = Math.Exp(-SnapFrequency * deltaTime);
            double impulse = (velocity + SnapFrequency * offset) * deltaTime;
            double nextOffset = (offset + impulse) * decay;
            double nextVelocity = (velocity - SnapFrequency * impulse) * decay;

            // Snaps land without passing their row, while edge bounces may
            if (!allowOvershoot && offset != 0d && Math.Sign(nextOffset) != Math.Sign(offset))
            {
                nextOffset = 0d;
                nextVelocity = 0d;
            }

            position = targetRaw + nextOffset;
            velocity = nextVelocity;
            return Math.Abs(nextOffset) * itemHeight < SettleDistance && Math.Abs(nextVelocity) * itemHeight < SettleSpeed;
        }

        void CompleteMotion()
        {
            motion = Motion.Idle;
            velocity = 0d;
            position = targetRaw;
            LayoutRows(true);
            Commit();
            Normalize();
            userMotion = false;
        }

        void Commit()
        {
            if (targetRaw == committedRaw)
                return;

            int previous = committedRaw;
            committedRaw = targetRaw;
            int index = WrapIndex(committedRaw);
            bool changed = index != selectedIndex;
            selectedIndex = index;

            RawIndexCommitted?.Invoke(this, previous, committedRaw);
            if (changed)
                onValueChanged?.Invoke(index);
        }

        void SettleImmediately()
        {
            if (!stateReady || motion == Motion.Idle)
                return;

            isDragging = false;
            JumpTo(motion == Motion.Moving ? targetRaw : ClampRaw(RoundToInt(position)));
            Commit();
            Normalize();
            userMotion = false;
        }

        // Brings a resting loop back to its first turn, so rows map to items the same way every time
        // and saved prefabs match their instances. Runs after commits, which compare indices of one turn.
        void Normalize()
        {
            int count = Count;
            if (!loop || count == 0 || motion != Motion.Idle || targetRaw != committedRaw)
                return;

            int shift = committedRaw - Mod(committedRaw, count);
            if (shift == 0)
                return;

            committedRaw -= shift;
            targetRaw -= shift;
            centerRaw -= shift;
            position -= shift;

            // Whole turns show the same items, so every row keeps its text and place without a redraw
            if (rows.Count > 0)
                rowPhase = Mod(rowPhase + Mod(shift, rows.Count), rows.Count);

            foreach (Row row in rows)
            {
                if (row.raw != int.MinValue)
                    row.raw -= shift;
            }
        }

        void JumpTo(int raw)
        {
            motion = Motion.Idle;
            velocity = 0d;
            position = targetRaw = centerRaw = raw;
            RefreshLayout();
        }

        void Release(double releaseVelocity)
        {
            int count = Count;
            if (count == 0)
            {
                motion = Motion.Idle;
                return;
            }

            double speed = Math.Max(-MaxVelocity, Math.Min(MaxVelocity, releaseVelocity));
            velocity = speed;
            userMotion = true;

            // Return from the rubber band of a finite wheel
            if (!loop && (position < 0d || position > count - 1))
            {
                StartMotion(ClampRaw(RoundToInt(position)));
                return;
            }

            if (Math.Abs(speed) < FlingVelocity)
            {
                StartMotion(ClampRaw(RoundToInt(position + speed / SnapFrequency)));
                return;
            }

            // Land where the spin would coast to, at least half a row on so the wheel never brakes hard or backs up
            int direction = Math.Sign(speed);
            double distance = Math.Min(Math.Abs(speed) * DecayTime, Math.Abs(speed) * MaxFlingDuration / 3d);
            int raw = RoundToInt(position + distance * direction);
            if ((raw - position) * direction < 0.5d)
                raw += direction;

            // A spin cut short by the end of a finite wheel bounces off it instead
            int clamped = ClampRaw(raw);
            if (clamped == raw || 3d * (clamped - position) / speed >= MinFlingDuration)
                StartFling(clamped, speed);
            else
                StartMotion(clamped, true);
        }

        void MoveBy(int steps)
        {
            if (steps == 0 || isDragging || Count == 0)
                return;

            EnsureState();
            int from = DestinationRaw;
            int raw = ClampRaw(from + steps);

            if (raw == from && motion != Motion.Moving)
                return;

            userMotion = true;
            StartMotion(raw);
        }

        void RefreshLayout()
        {
            layoutDirty = true;
#if UNITY_EDITOR
            if (!Application.IsPlaying(gameObject) && CanPreview())
                LayoutRows(false);
#endif
        }
        #endregion

        #region Input
        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            // Touching the wheel still stops an enclosing scroll view
            if (transform.parent == null)
                return;

            GameObject parent = ExecuteEvents.GetEventHandler<IInitializePotentialDragHandler>(transform.parent.gameObject);
            if (parent != null)
                ExecuteEvents.Execute(parent, eventData, ExecuteEvents.initializePotentialDrag);
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);

            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            // Like iOS, a touch stops a spinning wheel without selecting the touched row
            pressStoppedMotion = motion == Motion.Moving && IsActive() && IsInteractable();
            if (pressStoppedMotion)
            {
                motion = Motion.Held;
                velocity = 0d;
            }
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);

            if (eventData.button == PointerEventData.InputButton.Left && motion == Motion.Held && !isDragging)
                Release(0d);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || eventData.dragging || isDragging || pressStoppedMotion
                || !IsActive() || !IsInteractable() || !TryGetLocalY(eventData, out float y))
                return;

            y -= Rect.rect.center.y;
            Row nearest = null;
            float nearestDistance = float.MaxValue;

            foreach (Row row in rows)
            {
                float distance = Mathf.Abs(row.y - y);
                if (row.visible && distance < nearestDistance)
                {
                    nearest = row;
                    nearestDistance = distance;
                }
            }

            if (nearest == null || nearest.raw == DestinationRaw)
                return;

            userMotion = true;
            StartMotion(nearest.raw);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            // Sideways drags belong to an enclosing scroll view or page
            Vector2 travel = eventData.position - eventData.pressPosition;
            if (Mathf.Abs(travel.x) > Mathf.Abs(travel.y) || !IsActive() || !IsInteractable() || Count == 0)
            {
                forwardTarget = transform.parent != null ? ExecuteEvents.GetEventHandler<IBeginDragHandler>(transform.parent.gameObject) : null;
                if (forwardTarget != null)
                    ExecuteEvents.Execute(forwardTarget, eventData, ExecuteEvents.beginDragHandler);

                return;
            }

            if (!TryGetLocalY(eventData, out float y))
                return;

            EnsureState();
            isDragging = true;
            motion = Motion.Dragging;
            velocity = 0d;
            userMotion = true;
            scrollRemainder = 0f;
            dragStartY = y;
            dragStartPosition = loop ? position : RubberBand(position, true);
            sampleCount = 0;
            AddSample(position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (forwardTarget != null)
            {
                ExecuteEvents.Execute(forwardTarget, eventData, ExecuteEvents.dragHandler);
                return;
            }

            if (!isDragging || eventData.button != PointerEventData.InputButton.Left || !TryGetLocalY(eventData, out float y))
                return;

            double raw = dragStartPosition + (y - dragStartY) / itemHeight;
            position = loop ? raw : RubberBand(raw, false);
            AddSample(position);
            layoutDirty = true;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (forwardTarget != null)
            {
                ExecuteEvents.Execute(forwardTarget, eventData, ExecuteEvents.endDragHandler);
                forwardTarget = null;
                return;
            }

            if (!isDragging || eventData.button != PointerEventData.InputButton.Left)
                return;

            isDragging = false;
            Release(EstimateVelocity());
        }

        public void OnScroll(PointerEventData eventData)
        {
            Vector2 delta = eventData.scrollDelta;
            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y) || !IsActive() || !IsInteractable() || Count == 0)
            {
                if (transform.parent != null)
                    ExecuteEvents.ExecuteHierarchy(transform.parent.gameObject, eventData, ExecuteEvents.scrollHandler);

                return;
            }

            if (isDragging)
                return;

            // One wheel notch moves one row, while trackpads add up their fractional ticks
            EventSystem system = EventSystem.current;
            if (system != null && system.currentInputModule != null)
                delta = system.currentInputModule.ConvertPointerEventScrollDeltaToTicks(delta);

            float ticks = -delta.y;
            if (ticks == 0f || float.IsNaN(ticks) || float.IsInfinity(ticks))
                return;

            if (ticks * scrollRemainder < 0f)
                scrollRemainder = 0f;

            scrollRemainder += ticks;
            int steps = (int)scrollRemainder;
            scrollRemainder -= steps;
            MoveBy(steps);
        }

        public override void OnMove(AxisEventData eventData)
        {
            if ((eventData.moveDir == MoveDirection.Up || eventData.moveDir == MoveDirection.Down)
                && IsActive() && IsInteractable() && Count > 0)
            {
                MoveBy(eventData.moveDir == MoveDirection.Down ? 1 : -1);
                eventData.Use();
                return;
            }

            base.OnMove(eventData);
        }

        bool TryGetLocalY(PointerEventData eventData, out float y)
        {
            bool hit = RectTransformUtility.ScreenPointToLocalPointInRectangle(Rect, eventData.position,
                eventData.pressEventCamera, out Vector2 local);
            y = local.y;
            return hit;
        }

        void AddSample(double value)
        {
            sampleHead = (sampleHead + 1) % SampleCapacity;
            sampleTimes[sampleHead] = Time.unscaledTimeAsDouble;
            samplePositions[sampleHead] = value;
            sampleCount = Math.Min(sampleCount + 1, SampleCapacity);
        }

        double EstimateVelocity()
        {
            if (sampleCount < 2)
                return 0d;

            // A pointer that rested before release lets the wheel settle in place
            double latest = sampleTimes[sampleHead];
            if (Time.unscaledTimeAsDouble - latest > VelocityWindow * 0.5f)
                return 0d;

            int oldest = sampleHead;
            for (int i = 1; i < sampleCount; i++)
            {
                int index = (sampleHead - i + SampleCapacity) % SampleCapacity;
                if (latest - sampleTimes[index] > VelocityWindow)
                    break;

                oldest = index;
            }

            double elapsed = latest - sampleTimes[oldest];
            return elapsed > 1e-4 ? (samplePositions[sampleHead] - samplePositions[oldest]) / elapsed : 0d;
        }

        // Applies the band past the ends of a finite wheel, or removes it with inverse
        double RubberBand(double value, bool inverse)
        {
            int last = Count - 1;
            if (value < 0d)
                return -RubberBandOffset(-value, inverse);
            if (value > last)
                return last + RubberBandOffset(value - last, inverse);

            return value;
        }

        double RubberBandOffset(double offset, bool inverse)
        {
            // iOS-style resistance that never travels farther than half of the wheel
            double range = Math.Max(1d, halfRows);
            if (!inverse)
                return (1d - 1d / (offset * RubberBandFactor / range + 1d)) * range;

            double ratio = Math.Min(offset / range, 0.99d);
            return (1d / (1d - ratio) - 1d) * range / RubberBandFactor;
        }
        #endregion

        #region Helpers
        static int Mod(int value, int count)
        {
            int result = value % count;
            return result < 0 ? result + count : result;
        }

        static int RoundToInt(double value) => (int)Math.Floor(value + 0.5d);

        int WrapIndex(int raw)
        {
            int count = Count;
            if (count == 0)
                return 0;

            return loop ? Mod(raw, count) : Mathf.Clamp(raw, 0, count - 1);
        }

        int ClampRaw(int raw) => loop ? raw : Mathf.Clamp(raw, 0, Mathf.Max(0, Count - 1));

        void SetSerializedIndex(int index)
        {
            if (selectedIndex == index)
                return;

            selectedIndex = index;
            SaveEditorChange();
        }

        void Rebase(int index)
        {
            isDragging = false;
            userMotion = false;
            committedRaw = index;
            SetSerializedIndex(index);
            JumpTo(index);
        }

        // Edit Mode keeps changes made through code, as the inspector would
        void SaveEditorChange()
        {
#if UNITY_EDITOR
            if (!Application.IsPlaying(gameObject))
                SaveEditorChanges(this);
#endif
        }
        #endregion

        #region Public API
        /// <summary>
        /// Items currently shown: those passed to SetItems, or the serialized list.
        /// </summary>
        public IReadOnlyList<string> Items => Data;

        public int ItemCount => Count;

        /// <summary>
        /// Index of the selected item. Setting it animates the wheel and invokes events on change.
        /// </summary>
        public int SelectedIndex
        {
            get => selectedIndex;
            set => SetIndex(value);
        }

        /// <summary>
        /// Whether the wheel is being dragged, held, or is still moving toward a row.
        /// </summary>
        public bool IsMoving => motion != Motion.Idle;

        public bool Loop
        {
            get => loop;
            set
            {
                if (loop == value)
                    return;

                loop = value;
                SaveEditorChange();
                if (stateReady)
                    Rebase(WrapIndex(selectedIndex));
            }
        }

        public float ItemHeight
        {
            get => itemHeight;
            set
            {
                value = Mathf.Max(1f, value);
                if (itemHeight == value)
                    return;

                itemHeight = value;
                SaveEditorChange();
                RefreshLayout();
            }
        }

        public float UnselectedAlpha
        {
            get => unselectedAlpha;
            set
            {
                unselectedAlpha = Mathf.Clamp01(value);
                RefreshLayout();
            }
        }

        public float DecelerationRate
        {
            get => decelerationRate;
            set => decelerationRate = Mathf.Clamp(value, 0.001f, 0.5f);
        }

        /// <summary>
        /// Shows the given items instead of the serialized list. Pass null to show the serialized list again.
        /// The list is read while drawing, so call this again or Refresh after changing it.
        /// </summary>
        public void SetItems(IReadOnlyList<string> newItems)
        {
            int previousCount = Count;
            source = newItems;
            dataVersion++;

            if (stateReady && Count != previousCount)
                Rebase(Count > 0 ? Mathf.Clamp(selectedIndex, 0, Count - 1) : 0);

            RefreshLayout();
        }

        /// <summary>
        /// Selects an item. Looping wheels turn the shortest way. Events run only when the index changes.
        /// </summary>
        public void SetIndex(int index, bool animated = true, bool invokeEvents = true)
        {
            EnsureState();
            int count = Count;
            if (count == 0)
            {
                SetSerializedIndex(Mathf.Max(0, index));
                return;
            }

            index = loop ? Mod(index, count) : Mathf.Clamp(index, 0, count - 1);
            bool animate = animated && Application.IsPlaying(gameObject) && isActiveAndEnabled;
            int raw = index;

            // Only an animated loop needs to turn the short way; a jump lands on the first turn
            if (loop && animate)
            {
                int current = DestinationRaw;
                int offset = Mod(index - Mod(current, count), count);
                raw = current + (offset > count / 2 ? offset - count : offset);
            }

            bool changed = index != selectedIndex;
            isDragging = false;
            userMotion = false;
            committedRaw = raw;
            SetSerializedIndex(index);

            // A wheel already resting there has nothing to move or redraw, except in Edit Mode, which draws right away
            bool resting = motion == Motion.Idle && position == raw && targetRaw == raw && centerRaw == raw;
#if UNITY_EDITOR
            resting &= animate || Application.IsPlaying(gameObject);
#endif
            if (!resting)
            {
                if (animate)
                    StartMotion(raw);
                else
                    JumpTo(raw);
            }

            if (changed && invokeEvents)
                onValueChanged?.Invoke(index);
        }

        /// <summary>
        /// Turns to the next item, wrapping when looping.
        /// </summary>
        public void SelectNext() => StepBy(1);

        /// <summary>
        /// Turns to the previous item, wrapping when looping.
        /// </summary>
        public void SelectPrevious() => StepBy(-1);

        void StepBy(int steps)
        {
            if (Application.IsPlaying(gameObject) && isActiveAndEnabled) { MoveBy(steps); }
            else { SetIndex(selectedIndex + steps, false); }
        }

        /// <summary>
        /// Copies the item template again and redraws the rows. Call after changing the template at runtime.
        /// </summary>
        public void Refresh()
        {
            rowsDirty = true;
            copiesStale = true;
            dataVersion++;
            RefreshLayout();
        }

        /// <summary>
        /// Row that every other row copies. Assign a direct child; null uses the first child with a text component.
        /// </summary>
        public RectTransform ItemTemplate
        {
            get => FindTemplate();
            set
            {
                if (itemTemplate == value)
                    return;

                itemTemplate = value;
                SaveEditorChange();
                Refresh();
            }
        }
        #endregion

#if UNITY_EDITOR
        bool refreshPending;

        protected override void Reset()
        {
            base.Reset();
            transition = Transition.None;
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            itemHeight = Mathf.Max(1f, itemHeight);

            if (Application.IsPlaying(gameObject))
            {
                pendingInspectorIndex = stateReady;
                layoutDirty = true;
                return;
            }

            // Edits, prefab updates included, copy the template again; an undo or redo keeps the copies it restored
            if (!UnityEditor.Undo.isProcessing)
                copiesStale = true;

            RequestEditorRefresh();
        }

        void RequestEditorRefresh()
        {
            if (refreshPending)
                return;

            refreshPending = true;
            UnityEditor.EditorApplication.delayCall += RefreshEditor;
        }

        void CancelEditorRefresh()
        {
            if (!refreshPending)
                return;

            refreshPending = false;
            UnityEditor.EditorApplication.delayCall -= RefreshEditor;
        }

        void RefreshEditor()
        {
            refreshPending = false;

            if (this == null || !isActiveAndEnabled || !CanPreview() || Application.IsPlaying(gameObject) || IsChangingPlayMode)
                return;

            if (IsEditorBusy)
            {
                RequestEditorRefresh();
                return;
            }

            // Follow inspector edits of the selected index
            stateReady = false;
            rowsDirty = true;
            dataVersion++;
            LayoutRows(false);
        }

        bool CanPreview() => !UnityEditor.EditorUtility.IsPersistent(this) && gameObject.scene.IsValid();

        bool CanPreviewNow() => CanPreview() && !IsChangingPlayMode && !IsEditorBusy;

        bool HasCopies()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                if (IsCopy(transform.GetChild(i)))
                    return true;
            }

            return false;
        }

        static bool IsChangingPlayMode => UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode && !Application.isPlaying;

        static bool IsEditorBusy => UnityEditor.EditorApplication.isCompiling || UnityEditor.EditorApplication.isUpdating;

        static readonly List<PickerWheel> previewWheels = new();
        static bool listening;

        void ListenForTemplateChanges(bool listen)
        {
            if (!listen)
            {
                previewWheels.Remove(this);
                return;
            }

            if (!previewWheels.Contains(this))
                previewWheels.Add(this);

            if (!listening)
            {
                listening = true;
                UnityEditor.ObjectChangeEvents.changesPublished += OnChangesPublished;
            }
        }

        // Edits to a template, its components or its children copy it to the other rows again
        static void OnChangesPublished(ref UnityEditor.ObjectChangeEventStream stream)
        {
            if (previewWheels.Count == 0)
                return;

            for (int i = 0; i < stream.length; i++)
            {
#if UNITY_6000_4_OR_NEWER
                EntityId id;
#else
                int id;
#endif
                switch (stream.GetEventType(i))
                {
                    case UnityEditor.ObjectChangeKind.ChangeGameObjectOrComponentProperties:
                        stream.GetChangeGameObjectOrComponentPropertiesEvent(i, out var properties);
#if UNITY_6000_4_OR_NEWER
                        id = properties.entityId;
#else
                        id = properties.instanceId;
#endif
                        break;
                    case UnityEditor.ObjectChangeKind.ChangeGameObjectStructure:
                        stream.GetChangeGameObjectStructureEvent(i, out var structure);
#if UNITY_6000_4_OR_NEWER
                        id = structure.entityId;
#else
                        id = structure.instanceId;
#endif
                        break;
                    case UnityEditor.ObjectChangeKind.ChangeGameObjectStructureHierarchy:
                        stream.GetChangeGameObjectStructureHierarchyEvent(i, out var hierarchy);
#if UNITY_6000_4_OR_NEWER
                        id = hierarchy.entityId;
#else
                        id = hierarchy.instanceId;
#endif
                        break;
                    default:
                        continue;
                }

                // Placement is driven by the wheel and followed every layout, so only style changes count
#if UNITY_6000_3_OR_NEWER
                // Unity 6.3 still reports an int id, which converts to EntityId implicitly
                UnityEngine.Object changed = UnityEditor.EditorUtility.EntityIdToObject(id);
#else
                UnityEngine.Object changed = UnityEditor.EditorUtility.InstanceIDToObject(id);
#endif
                Transform target = changed is Component component && component is not Transform
                    ? component.transform
                    : changed is GameObject go ? go.transform : null;
                if (target == null)
                    continue;

                foreach (PickerWheel wheel in previewWheels)
                {
                    if (wheel != null && wheel.rows.Count > 0 && wheel.rows[0].rect != null && target.IsChildOf(wheel.rows[0].rect))
                    {
                        wheel.copiesStale = true;
                        wheel.RequestEditorRefresh();
                    }
                }
            }
        }

        static void SaveEditorChanges(UnityEngine.Object target)
        {
            if (target == null || UnityEditor.EditorUtility.IsPersistent(target))
                return;

            UnityEditor.EditorUtility.SetDirty(target);
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
#endif
    }
}