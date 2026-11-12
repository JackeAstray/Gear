using System;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using Evo.Tween;

namespace Evo.UI
{
    [ExecuteAlways, DisallowMultipleComponent, NoAutoStaticsCleanup]
    [AddComponentMenu("Evo/UI/UI Elements/Calendar"), HelpURL(Constants.HelpUrl + "ui-elements/calendar")]
    public class Calendar : MonoBehaviour
    {
        // Settings
        [SerializeField] private bool highlightToday = true;
        [SerializeField, Range(0, 100), Tooltip("Year range from current year.")] 
        private int yearRangeFromCurrent = 50;

        // Initial Date
        [SerializeField] private InitialDateMode initialDateMode = InitialDateMode.Today;
        [SerializeField] private int customYear = 1999;
        [SerializeField, Range(1, 12)] private int customMonth = 6;
        [SerializeField, Range(1, 31)] private int customDay = 21;

        // Animation
        [SerializeField, Min(0f), Tooltip("Duration of the zoom and slide transitions. Set to 0 to switch instantly.")]
        private float transitionDuration = 0.3f;
        [SerializeField] private Ease transitionEase = Ease.OutCubic;
        [SerializeField] private AnimationCurve customEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField, Range(0f, 0.5f), Tooltip("Scale difference used when zooming between the day, month, and year views.")]
        private float zoomScale = 0.2f;
        [SerializeField, Tooltip("Direction of the previous and next page slide.")]
        private SlideAxis slideAxis = SlideAxis.Horizontal;
        [SerializeField, Range(0f, 1f), Tooltip("Page slide distance relative to the grid size. 1 slides a full page " +
            "and needs a RectMask2D on the grid's parent, 0 crossfades in place.")]
        private float slideDistance = 0.25f;
        [SerializeField] private bool useUnscaledTime = true;

        // References
        [TextComponent] public Graphic dateLabel;
        [Tooltip("Zooms out from the day view to the month view, and from the month view to the year view.")]
        public Button headerButton;
        public Dropdown monthDropdown;
        public Dropdown yearDropdown;
        public Button previousMonthButton;
        public Button nextMonthButton;
        [SerializeField, Tooltip("Scaled and faded while zooming. Uses Day Container when empty.")]
        private RectTransform dayView;
        [SerializeField, Tooltip("Managed by Calendar. Direct children are reused, replaced, or removed to maintain the day grid.")]
        private Transform daysContainer;
        [SerializeField, Tooltip("Requires CalendarDay on the root and a linked Button within that object. Changes update the day grid.")]
        private GameObject dayButtonPrefab;
        [SerializeField, Tooltip("Scaled and faded while zooming. Uses Month Container when empty.")]
        private RectTransform monthView;
        [SerializeField, Tooltip("Managed by Calendar. Direct children are reused, replaced, or removed to maintain the month grid.")]
        private Transform monthContainer;
        [SerializeField, Tooltip("Scaled and faded while zooming. Uses Year Container when empty.")]
        private RectTransform yearView;
        [SerializeField, Tooltip("Managed by Calendar. Direct children are reused, replaced, or removed to maintain the year grid.")]
        private Transform yearContainer;
        [SerializeField, Tooltip("Used by the month and year grids. Uses Day Button Prefab when empty.")]
        private GameObject pickerButtonPrefab;

        // Month Data
        [SerializeField] private MonthData january = new("January", "Jan");
        [SerializeField] private MonthData february = new("February", "Feb");
        [SerializeField] private MonthData march = new("March", "Mar");
        [SerializeField] private MonthData april = new("April", "Apr");
        [SerializeField] private MonthData may = new("May", "May");
        [SerializeField] private MonthData june = new("June", "Jun");
        [SerializeField] private MonthData july = new("July", "Jul");
        [SerializeField] private MonthData august = new("August", "Aug");
        [SerializeField] private MonthData september = new("September", "Sep");
        [SerializeField] private MonthData october = new("October", "Oct");
        [SerializeField] private MonthData november = new("November", "Nov");
        [SerializeField] private MonthData december = new("December", "Dec");

#if EVO_LOCALIZATION
        // Localization
        public bool enableLocalization = true;
        public Localization.LocalizedObject localizedObject;
#endif

        // Events
        public UnityEvent<DateTime> onDateSelected = new();

        // Constants
        const int DayButtonCount = 42;
        const int PickerButtonCount = 16;
        const int MonthsPerPage = 12;
        const int YearsPerPage = 10;
        const int TransitionPropertyKey = 0x43414C44; // CALD
        const float FadeOutEnd = 0.3f;
        const float FadeInStart = 0.1f;
        const float FadeInEnd = 0.7f;

        // Cache
        static readonly string[] dayLabelCache = InitDayLabels();
        static readonly Action<Calendar, float> TransitionUpdateCallback = ApplyTransitionValue;
        readonly ButtonGrid dayGrid = new(DayButtonCount);
        readonly ButtonGrid monthGrid = new(PickerButtonCount);
        readonly ButtonGrid yearGrid = new(PickerButtonCount);
        readonly string[] monthNameCache = new string[12];
        Action transitionCompleteCallback;

        // State
        bool isInitialized, isRefreshing;
        int startYear, endYear;
        DateTime? selectedDate;
        DisplayMode displayMode = DisplayMode.Day;

        // Transition
        TweenHandle transition;
        TransitionLayer incomingLayer, outgoingLayer;

        // Rules
        DateTime minDate = new(1900, 1, 1);
        DateTime maxDate = new(2100, 12, 31);
        DateTime currentDisplayMonth = DateTime.Today;

        // Properties
        public DisplayMode CurrentDisplayMode => displayMode;
        MonthData[] Months { get; set; }
        GameObject PickerButtonPrefab => pickerButtonPrefab != null ? pickerButtonPrefab : dayButtonPrefab;

        static string[] InitDayLabels()
        {
            var labels = new string[32];
            for (int i = 1; i < labels.Length; i++)
                labels[i] = i.ToString();

            return labels;
        }

        void Start()
        {
            if (!Application.IsPlaying(gameObject))
                return;

            Initialize();
        }

        void OnEnable()
        {
#if EVO_LOCALIZATION
            if (isInitialized)
                Localization.LocalizationManager.Subscribe(this, enableLocalization, ref localizedObject, UpdateLocalization);
#endif
#if UNITY_EDITOR
            UnityEditor.EditorApplication.hierarchyChanged += RequestEditorRefresh;
            UnityEditor.EditorApplication.projectChanged += RequestEditorRefresh;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeChanged;
            UnityEditor.Undo.undoRedoPerformed += RequestEditorRefresh;
            RequestEditorRefresh();
#endif

            if (Application.IsPlaying(gameObject) && isInitialized)
                Refresh();
        }

        void OnDisable()
        {
#if EVO_LOCALIZATION
            Localization.LocalizationManager.Unsubscribe(UpdateLocalization);
#endif
            // Views can't be toggled while this hierarchy deactivates; Refresh hides them on enable
            CompleteTransition(gameObject.activeInHierarchy);
            ReleaseGrids();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= RefreshEditor;
            UnityEditor.EditorApplication.hierarchyChanged -= RequestEditorRefresh;
            UnityEditor.EditorApplication.projectChanged -= RequestEditorRefresh;
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            UnityEditor.Undo.undoRedoPerformed -= RequestEditorRefresh;
#endif
        }

        void OnDestroy()
        {
            ReleaseGrids();
            DestroyTransitionCopy(dayGrid);
            DestroyTransitionCopy(monthGrid);
            DestroyTransitionCopy(yearGrid);
            RemoveListeners();
        }

        void Initialize()
        {
            if (isInitialized || !Application.IsPlaying(gameObject))
                return;

            CacheMonths();
            SetInitialDate();
            UpdateYearRange();
            displayMode = DisplayMode.Day;
            transitionCompleteCallback ??= FinishTransition;

            if (monthDropdown != null)
                SetupMonthDropdown();
            if (yearDropdown != null)
                SetupYearDropdown();
            if (headerButton != null)
                headerButton.onClick.AddListener(ZoomOut);
            if (previousMonthButton != null)
                previousMonthButton.onClick.AddListener(GoToPrevious);
            if (nextMonthButton != null)
                nextMonthButton.onClick.AddListener(GoToNext);

            isInitialized = true;
            Refresh();

            // Copying the day grid up front keeps the first month change from stalling
            if (transitionDuration > 0f && dayGrid.HasValidTemplate && dayGrid.CachedContainer is RectTransform dayRect
                && EnsureTransitionCopy(dayGrid, dayRect))
                HideTransitionCopy(dayGrid);

#if EVO_LOCALIZATION
            Localization.LocalizationManager.Subscribe(this, enableLocalization, ref localizedObject, UpdateLocalization);
#endif

            if (selectedDate.HasValue)
                onDateSelected?.Invoke(selectedDate.Value);
        }

        void RemoveListeners()
        {
            if (headerButton != null)
                headerButton.onClick.RemoveListener(ZoomOut);
            if (previousMonthButton != null)
                previousMonthButton.onClick.RemoveListener(GoToPrevious);
            if (nextMonthButton != null)
                nextMonthButton.onClick.RemoveListener(GoToNext);
            if (monthDropdown != null)
                monthDropdown.onItemSelected.RemoveListener(OnMonthSelected);
            if (yearDropdown != null)
                yearDropdown.onItemSelected.RemoveListener(OnYearSelected);
        }

        void CacheMonths()
        {
            Months = new MonthData[]
            {
                january, february, march, april, may, june, july, august, september, october, november, december
            };
        }

        void SetInitialDate()
        {
            DateTime date = DateTime.Today;

            if (initialDateMode == InitialDateMode.Custom)
            {
                int year = Mathf.Clamp(customYear, 1, 9999);
                int month = Mathf.Clamp(customMonth, 1, 12);
                int day = Mathf.Clamp(customDay, 1, DateTime.DaysInMonth(year, month));
                date = new DateTime(year, month, day);
            }

            selectedDate = initialDateMode == InitialDateMode.None ? null : date;
            currentDisplayMonth = new DateTime(date.Year, date.Month, 1);
        }

        void UpdateYearRange()
        {
            int currentYear = DateTime.Today.Year;
            int range = Mathf.Clamp(yearRangeFromCurrent, 0, 100);
            startYear = Mathf.Clamp(currentYear - range, minDate.Year, maxDate.Year);
            endYear = Mathf.Clamp(currentYear + range, startYear, maxDate.Year);
        }

        void SetupMonthDropdown()
        {
            if (monthDropdown == null || !Application.IsPlaying(gameObject))
                return;

            monthDropdown.ClearAllItems();
            monthDropdown.onItemSelected.RemoveListener(OnMonthSelected);

            // Write into the reusable buffer instead of allocating a new List<string> + ToArray()
            for (int i = 0; i < Months.Length; i++)
                monthNameCache[i] = Months[i].GetDisplayName();

            monthDropdown.AddItems(monthNameCache);
            monthDropdown.onItemSelected.AddListener(OnMonthSelected);
        }

        void OnMonthSelected(int monthIndex)
        {
            if (monthIndex < 0 || monthIndex >= 12)
                return;

            ShowMonth(new DateTime(currentDisplayMonth.Year, monthIndex + 1, 1));
        }

        void SetupYearDropdown()
        {
            if (yearDropdown == null || !Application.IsPlaying(gameObject))
                return;

            yearDropdown.ClearAllItems();
            yearDropdown.onItemSelected.RemoveListener(OnYearSelected);

            UpdateYearRange();

            string[] years = new string[endYear - startYear + 1];
            for (int i = 0; i < years.Length; i++)
                years[i] = (startYear + i).ToString();

            yearDropdown.AddItems(years);
            yearDropdown.onItemSelected.AddListener(OnYearSelected);
        }

        void OnYearSelected(int yearIndex)
        {
            if (yearIndex < 0 || yearIndex > endYear - startYear)
                return;

            ShowMonth(new DateTime(startYear + yearIndex, currentDisplayMonth.Month, 1));
        }

        #region Types
        public enum InitialDateMode { None, Today, Custom }
        public enum DisplayMode { Day, Month, Year }
        public enum SlideAxis { Horizontal, Vertical }

        [Serializable]
        public class MonthData
        {
            [Tooltip("Display name for the month.")]
            public string monthName;
            [Tooltip("Abbreviated name used by the month view.")]
            public string shortName;
#if EVO_LOCALIZATION
            [Tooltip("Localization key for Evo Localization integration.")]
            public string localizationKey;
            [Tooltip("Localization key for the abbreviated name.")]
            public string shortLocalizationKey;
#endif
            public MonthData(string name) => monthName = name;
            public MonthData(string name, string abbreviation)
            {
                monthName = name;
                shortName = abbreviation;
            }

            public string GetDisplayName() => monthName;
            public string GetShortName()
            {
                if (!string.IsNullOrEmpty(shortName))
                    return shortName;

                return monthName != null && monthName.Length > 3 ? monthName[..3] : monthName;
            }
        }

        sealed class ButtonGrid
        {
            public readonly int Count;
            public readonly List<CalendarDay> Items;
            public readonly List<CalendarDay> TransitionItems;
            public Transform CachedContainer;
            public GameObject CachedPrefab;
            public CalendarDay CachedTemplate;
            public bool HasValidTemplate;

            // Runtime copy that shows the previous page while the live grid slides in
            public RectTransform TransitionCopy;

            public ButtonGrid(int count)
            {
                Count = count;
                Items = new List<CalendarDay>(count);
                TransitionItems = new List<CalendarDay>(count);
            }
        }

        struct TransitionLayer
        {
            public RectTransform Rect;
            public CanvasGroup Group;
            public Vector2 RestPosition;
            public Vector3 RestScale;
            public float RestAlpha;
            public bool RestBlocksRaycasts;
            public Vector2 FromOffset;
            public Vector2 ToOffset;
            public float FromScale;
            public float ToScale;
            public bool IsOutgoing;
        }
        #endregion

        #region Grids
        bool HasValidTemplate(ButtonGrid grid, Transform container, GameObject prefab)
        {
            if (container == null || prefab == null)
                return false;

            // The managed container must never contain this component, its template, or other calendar parts
            if ((container != transform && transform.IsChildOf(container)) || container == prefab.transform
                || prefab.transform.IsChildOf(container) || container.IsChildOf(prefab.transform)
                || OverlapsOtherReferences(grid, container) || IsInsideContainer(prefab.transform))
                return false;

#if UNITY_EDITOR
            if (!Application.IsPlaying(gameObject) && (UnityEditor.EditorUtility.IsPersistent(container)
                || container.gameObject.scene != gameObject.scene))
                return false;

            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(prefab) && prefab.transform.parent != null)
                return false;
#endif

            return prefab.TryGetComponent(out grid.CachedTemplate) && grid.CachedTemplate.button != null
                && grid.CachedTemplate.button.transform.IsChildOf(grid.CachedTemplate.transform)
                && prefab.GetComponentInChildren<Calendar>(true) == null;
        }

        bool OverlapsOtherReferences(ButtonGrid grid, Transform container)
        {
            // Syncing a container removes unknown children, so it must not hold a view or another grid
            if (Contains(container, dayView) || Contains(container, monthView) || Contains(container, yearView))
                return true;

            return (grid != dayGrid && Overlaps(container, daysContainer))
                || (grid != monthGrid && Overlaps(container, monthContainer))
                || (grid != yearGrid && Overlaps(container, yearContainer));
        }

        bool IsInsideContainer(Transform item)
        {
            return (daysContainer != null && item.IsChildOf(daysContainer))
                || (monthContainer != null && item.IsChildOf(monthContainer))
                || (yearContainer != null && item.IsChildOf(yearContainer));
        }

        static bool Contains(Transform container, Transform item) => item != null && item != container && item.IsChildOf(container);

        static bool Overlaps(Transform a, Transform b) => a != null && b != null && (a == b || a.IsChildOf(b) || b.IsChildOf(a));

        bool GridChanged(ButtonGrid grid, Transform container, GameObject prefab)
        {
            if (grid.CachedContainer != container || grid.CachedPrefab != prefab
                || (grid.HasValidTemplate && (container == null || prefab == null)))
                return true;

            if (!grid.HasValidTemplate)
                return HasValidTemplate(grid, container, prefab);

            if (grid.CachedTemplate == null || grid.CachedTemplate.button == null
                || !grid.CachedTemplate.button.transform.IsChildOf(grid.CachedTemplate.transform)
                || container.childCount != grid.Count || grid.Items.Count != grid.Count
                || OverlapsOtherReferences(grid, container))
                return true;

            for (int i = 0; i < grid.Items.Count; i++)
            {
                CalendarDay item = grid.Items[i];
                if (item == null || item.button == null || item.transform != container.GetChild(i)
                    || !item.button.transform.IsChildOf(item.transform) || item.SourcePrefab != prefab
                    || !item.gameObject.activeSelf || !item.IsBoundTo(this))
                    return true;
            }

            return false;
        }

        void ReleaseGrids()
        {
            ReleaseGrid(dayGrid);
            ReleaseGrid(monthGrid);
            ReleaseGrid(yearGrid);
        }

        void ReleaseGrid(ButtonGrid grid)
        {
            foreach (CalendarDay item in grid.Items)
            {
                if (item != null)
                    item.Unbind(this);
            }

            grid.Items.Clear();
        }

        void SyncGrids()
        {
            bool isPlaying = Application.IsPlaying(gameObject);
            GameObject pickerPrefab = PickerButtonPrefab;

            if (!isPlaying || GridChanged(dayGrid, daysContainer, dayButtonPrefab))
                SyncGrid(dayGrid, daysContainer, dayButtonPrefab);
            if (!isPlaying || GridChanged(monthGrid, monthContainer, pickerPrefab))
                SyncGrid(monthGrid, monthContainer, pickerPrefab);
            if (!isPlaying || GridChanged(yearGrid, yearContainer, pickerPrefab))
                SyncGrid(yearGrid, yearContainer, pickerPrefab);
        }

        void SyncGrid(ButtonGrid grid, Transform container, GameObject prefab)
        {
            ReleaseGrid(grid);
            if (grid.CachedContainer != container || grid.CachedPrefab != prefab)
                DestroyTransitionCopy(grid);

            grid.CachedContainer = container;
            grid.CachedPrefab = prefab;
            grid.HasValidTemplate = HasValidTemplate(grid, container, prefab);

            // Keep existing content intact while references are being assigned or repaired.
            if (!grid.HasValidTemplate)
                return;

#if UNITY_EDITOR
            bool hierarchyChanged = false;
#endif
            for (int i = 0; i < container.childCount;)
            {
                Transform child = container.GetChild(i);
                CalendarDay item = child.GetComponent<CalendarDay>();
                if (grid.Items.Count >= grid.Count || !MatchesTemplate(item, prefab))
                {
                    RemoveGridObject(child.gameObject);
#if UNITY_EDITOR
                    hierarchyChanged = true;
#endif
                    continue;
                }

                PrepareGridButton(item, prefab);
                grid.Items.Add(item);
                i++;
            }

            while (grid.Items.Count < grid.Count)
            {
                GameObject buttonObject;
#if UNITY_EDITOR
                if (!Application.IsPlaying(gameObject) && UnityEditor.PrefabUtility.IsPartOfPrefabAsset(prefab))
                    buttonObject = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, container);
                else
#endif
                    buttonObject = Instantiate(prefab, container);

                CalendarDay item = buttonObject.GetComponent<CalendarDay>();
                PrepareGridButton(item, prefab);
                grid.Items.Add(item);
#if UNITY_EDITOR
                hierarchyChanged = true;
#endif
            }

#if UNITY_EDITOR
            if (hierarchyChanged && !Application.IsPlaying(gameObject))
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
        }

        bool MatchesTemplate(CalendarDay item, GameObject prefab)
        {
            if (item == null || item.button == null || !item.button.transform.IsChildOf(item.transform))
                return false;

            if (item.SourcePrefab != null)
                return item.SourcePrefab == prefab;

#if UNITY_EDITOR
            if (!Application.IsPlaying(gameObject) && UnityEditor.PrefabUtility.IsPartOfPrefabAsset(prefab))
            {
                string path = UnityEditor.AssetDatabase.GetAssetPath(prefab);
                return UnityEditor.PrefabUtility.GetCorrespondingObjectFromSourceAtPath(item.gameObject, path) == prefab;
            }
#endif
            // Adopt valid legacy children when no serialized source information exists
            return true;
        }

        void PrepareGridButton(CalendarDay item, GameObject prefab)
        {
            item.SourcePrefab = prefab;
            if (!item.gameObject.activeSelf)
            {
                item.gameObject.SetActive(true);
#if UNITY_EDITOR
                SaveEditorChanges(item.gameObject);
#endif
            }

            if (Application.IsPlaying(gameObject))
                item.Bind(this);
        }

        void RemoveGridObject(GameObject item)
        {
            if (item.TryGetComponent<CalendarDay>(out var day))
                day.Unbind(this);

            if (Application.IsPlaying(gameObject))
            {
                item.SetActive(false);
                item.transform.SetParent(null, false);
                Destroy(item);
            }
#if UNITY_EDITOR
            else { DestroyImmediate(item); }
#endif
        }

        ButtonGrid GetGrid(DisplayMode mode) => mode switch
        {
            DisplayMode.Month => monthGrid,
            DisplayMode.Year => yearGrid,
            _ => dayGrid
        };

        void FillGrid(DisplayMode mode, List<CalendarDay> items, DateTime month)
        {
            switch (mode)
            {
                case DisplayMode.Month: FillMonths(items, month.Year); break;
                case DisplayMode.Year: FillYears(items, GetDecadeStart(month.Year)); break;
                default: FillDays(items, month); break;
            }
        }

        void FillDays(List<CalendarDay> items, DateTime month)
        {
            DateTime firstDay = new(month.Year, month.Month, 1);
            DateTime today = DateTime.Today;
            long firstDayIndex = firstDay.Ticks / TimeSpan.TicksPerDay - (int)firstDay.DayOfWeek;
            long lastDayIndex = DateTime.MaxValue.Ticks / TimeSpan.TicksPerDay;

            for (int i = 0; i < items.Count; i++)
            {
                CalendarDay day = items[i];
                long dayIndex = firstDayIndex + i;
                bool validDate = dayIndex >= 0 && dayIndex <= lastDayIndex;
                DateTime date = validDate ? new DateTime(dayIndex * TimeSpan.TicksPerDay) : DateTime.MinValue;

                day.Date = date;
                day.SetLabel(validDate ? dayLabelCache[date.Day] : string.Empty);
                bool available = validDate && date >= minDate && date <= maxDate;
                SetButtonInteractable(day.button, available);

                // Unavailable dates share the look of the days outside the month
                if (!validDate)
                    day.SetState(0);
                else if (selectedDate.HasValue && date == selectedDate.Value.Date)
                    day.SetState(3);
                else if (!available)
                    day.SetState(0);
                else if (highlightToday && date == today)
                    day.SetState(2);
                else if (date.Month != month.Month || date.Year != month.Year)
                    day.SetState(0);
                else
                    day.SetState(1);
            }
        }

        void FillMonths(List<CalendarDay> items, int year)
        {
            DateTime today = DateTime.Today;

            // Like Windows, the page continues into the next year after December
            for (int i = 0; i < items.Count; i++)
            {
                CalendarDay item = items[i];
                int itemYear = year + i / 12;
                int itemMonth = i % 12 + 1;
                bool validDate = itemYear >= 1 && itemYear <= 9999;
                DateTime date = validDate ? new DateTime(itemYear, itemMonth, 1) : DateTime.MinValue;

                item.Date = date;
                item.SetLabel(validDate ? Months[itemMonth - 1].GetShortName() : string.Empty);
                bool available = validDate && IsMonthInRange(date);
                SetButtonInteractable(item.button, available);

                if (!validDate)
                    item.SetState(0);
                else if (selectedDate.HasValue && selectedDate.Value.Year == itemYear && selectedDate.Value.Month == itemMonth)
                    item.SetState(3);
                else if (!available)
                    item.SetState(0);
                else if (highlightToday && today.Year == itemYear && today.Month == itemMonth)
                    item.SetState(2);
                else if (i >= MonthsPerPage)
                    item.SetState(0);
                else
                    item.SetState(1);
            }
        }

        void FillYears(List<CalendarDay> items, int decadeStart)
        {
            int currentYear = DateTime.Today.Year;

            // Like Windows, the page continues into the next decade after its tenth year
            for (int i = 0; i < items.Count; i++)
            {
                CalendarDay item = items[i];
                int year = decadeStart + i;
                bool validDate = year >= 1 && year <= 9999;

                item.Date = validDate ? new DateTime(year, 1, 1) : DateTime.MinValue;
                item.SetLabel(validDate ? year.ToString() : string.Empty);
                bool available = validDate && IsYearInRange(year);
                SetButtonInteractable(item.button, available);

                if (!validDate)
                    item.SetState(0);
                else if (selectedDate.HasValue && selectedDate.Value.Year == year)
                    item.SetState(3);
                else if (!available)
                    item.SetState(0);
                else if (highlightToday && currentYear == year)
                    item.SetState(2);
                else if (i >= YearsPerPage)
                    item.SetState(0);
                else
                    item.SetState(1);
            }
        }
        #endregion

        #region Views
        RectTransform GetView(DisplayMode mode)
        {
            RectTransform view = mode switch
            {
                DisplayMode.Month => monthView != null ? monthView : monthContainer as RectTransform,
                DisplayMode.Year => yearView != null ? yearView : yearContainer as RectTransform,
                _ => dayView != null ? dayView : daysContainer as RectTransform
            };

            // Hiding a view must never disable this calendar
            return view != null && !transform.IsChildOf(view) ? view : null;
        }

        bool IsModeAvailable(DisplayMode mode)
        {
            if (mode == DisplayMode.Day)
                return true;

            ButtonGrid grid = GetGrid(mode);
            RectTransform view = GetView(mode);
            if (!grid.HasValidTemplate || grid.CachedContainer == null || view == null || !grid.CachedContainer.IsChildOf(view))
                return false;

            // Views must be separate so hiding one never hides another
            for (int i = 0; i <= (int)DisplayMode.Year; i++)
            {
                if ((DisplayMode)i != mode && Overlaps(view, GetView((DisplayMode)i)))
                    return false;
            }

            return true;
        }

        void ApplyViewVisibility()
        {
            if (!Application.IsPlaying(gameObject))
                return;

            for (int i = 0; i <= (int)DisplayMode.Year; i++)
            {
                DisplayMode mode = (DisplayMode)i;
                RectTransform view = GetView(mode);
                if (view == null || !IsModeAvailable(mode))
                    continue;

                bool visible = mode == displayMode;
                if (view.gameObject.activeSelf != visible)
                    view.gameObject.SetActive(visible);
            }

            HideTransitionCopy(dayGrid);
            HideTransitionCopy(monthGrid);
            HideTransitionCopy(yearGrid);
        }

        static void HideTransitionCopy(ButtonGrid grid)
        {
            if (grid.TransitionCopy != null && grid.TransitionCopy.gameObject.activeSelf)
                grid.TransitionCopy.gameObject.SetActive(false);
        }

        string GetHeaderText()
        {
            int year = currentDisplayMonth.Year;
            switch (displayMode)
            {
                case DisplayMode.Month:
                    return year.ToString();
                case DisplayMode.Year:
                    int decadeStart = GetDecadeStart(year);
                    return $"{Mathf.Max(decadeStart, 1)} - {Mathf.Min(decadeStart + YearsPerPage - 1, 9999)}";
                default:
                    return $"{Months[currentDisplayMonth.Month - 1].GetDisplayName()} {year}";
            }
        }

        static int GetDecadeStart(int year) => year - year % YearsPerPage;

        bool CanZoomOut() => displayMode switch
        {
            DisplayMode.Day => IsModeAvailable(DisplayMode.Month),
            DisplayMode.Month => IsModeAvailable(DisplayMode.Year),
            _ => false
        };

        void ClampDisplayMonth()
        {
            DateTime first = new(Mathf.Max(startYear, minDate.Year), 1, 1);
            if (minDate.Year == first.Year)
                first = new DateTime(minDate.Year, minDate.Month, 1);

            DateTime last = new(Mathf.Min(endYear, maxDate.Year), 12, 1);
            if (maxDate.Year == last.Year)
                last = new DateTime(maxDate.Year, maxDate.Month, 1);

            if (first > last)
                return;
            if (currentDisplayMonth < first)
                currentDisplayMonth = first;
            else if (currentDisplayMonth > last)
                currentDisplayMonth = last;
        }

        void PickMonth(DateTime month)
        {
            currentDisplayMonth = new DateTime(month.Year, month.Month, 1);
            SetDisplayMode(DisplayMode.Day);
        }

        void PickYear(int year)
        {
            currentDisplayMonth = new DateTime(year, currentDisplayMonth.Month, 1);
            SetDisplayMode(DisplayMode.Month);
        }
        #endregion

        #region Navigation
        void UpdateDisplay()
        {
            if (isRefreshing)
                return;

#if UNITY_EDITOR
            if (!Application.IsPlaying(gameObject) && (UnityEditor.EditorUtility.IsPersistent(this)
                || !gameObject.scene.IsValid() || UnityEditor.Undo.isProcessing
                || (daysContainer != null && (UnityEditor.EditorUtility.IsPersistent(daysContainer)
                || daysContainer.gameObject.scene != gameObject.scene))))
                return;
#endif

            isRefreshing = true;

            try
            {
                SyncGrids();

                if (Months == null)
                    CacheMonths();
                if (displayMode != DisplayMode.Day && !IsModeAvailable(displayMode))
                    displayMode = DisplayMode.Day;

                // Dropdown methods can generate UI and invoke close events; keep them in play mode.
                // SelectItem still closes the panel when events are suppressed, so skip unchanged selections.
                if (Application.IsPlaying(gameObject))
                {
                    int monthIndex = currentDisplayMonth.Month - 1;
                    if (monthDropdown != null && (monthDropdown.SelectedIndex != monthIndex || monthDropdown.SelectedItem == null))
                        monthDropdown.SelectItem(monthIndex, false);

                    if (yearDropdown != null)
                    {
                        int yearIndex = currentDisplayMonth.Year - startYear;
                        if (yearIndex >= 0 && yearIndex < yearDropdown.items.Count
                            && (yearDropdown.SelectedIndex != yearIndex || yearDropdown.SelectedItem == null))
                            yearDropdown.SelectItem(yearIndex, false);
                    }
                }

                TextProvider.UpdateText(dateLabel, GetHeaderText());
                UpdateNavigationButtons();

                // At runtime only the shown grid is filled, so a view zooming away keeps its page.
                // Edit mode fills every grid to preview them.
                if (Application.IsPlaying(gameObject))
                    FillGrid(displayMode, GetGrid(displayMode).Items, currentDisplayMonth);
                else
                {
                    for (int i = 0; i <= (int)DisplayMode.Year; i++)
                        FillGrid((DisplayMode)i, GetGrid((DisplayMode)i).Items, currentDisplayMonth);
                }
            }
            finally
            {
                isRefreshing = false;
            }
        }

        internal void OnDayClicked(CalendarDay day)
        {
            if (!Application.IsPlaying(gameObject) || !isActiveAndEnabled || day == null
                || day.button == null || !day.button.IsInteractable())
                return;

            Transform parent = day.transform.parent;
            if (parent == daysContainer && dayGrid.HasValidTemplate)
            {
                if (day.Date >= minDate && day.Date <= maxDate)
                    SelectDate(day.Date);
            }
            else if (parent == monthContainer && monthGrid.HasValidTemplate)
            {
                if (displayMode == DisplayMode.Month && IsMonthInRange(day.Date))
                    PickMonth(day.Date);
            }
            else if (parent == yearContainer && yearGrid.HasValidTemplate)
            {
                if (displayMode == DisplayMode.Year && IsYearInRange(day.Date.Year))
                    PickYear(day.Date.Year);
            }
        }

        bool IsMonthInRange(DateTime month)
        {
            if (month.Year < startYear || month.Year > endYear)
                return false;

            DateTime lastDay = new(month.Year, month.Month, DateTime.DaysInMonth(month.Year, month.Month));
            return lastDay >= minDate && month <= maxDate;
        }

        bool IsYearInRange(int year) => year >= startYear && year <= endYear;

        bool CanChangeMonth(int direction)
        {
            if ((direction < 0 && currentDisplayMonth.Year == 1 && currentDisplayMonth.Month == 1)
                || (direction > 0 && currentDisplayMonth.Year == 9999 && currentDisplayMonth.Month == 12))
                return false;

            return IsMonthInRange(currentDisplayMonth.AddMonths(direction));
        }

        bool CanChangePage(int direction)
        {
            switch (displayMode)
            {
                case DisplayMode.Month:
                    return IsYearInRange(currentDisplayMonth.Year + direction);
                case DisplayMode.Year:
                    int decadeStart = GetDecadeStart(currentDisplayMonth.Year) + direction * YearsPerPage;
                    return decadeStart + YearsPerPage - 1 >= startYear && decadeStart <= endYear;
                default:
                    return CanChangeMonth(direction);
            }
        }

        void ChangePage(int direction)
        {
            if (!isInitialized)
                Initialize();
            if (!CanChangePage(direction))
                return;

            int month = currentDisplayMonth.Month;
            DateTime page = displayMode switch
            {
                DisplayMode.Month => new DateTime(currentDisplayMonth.Year + direction, month, 1),
                DisplayMode.Year => new DateTime(Mathf.Clamp(currentDisplayMonth.Year + direction * YearsPerPage, startYear, endYear), month, 1),
                _ => currentDisplayMonth.AddMonths(direction)
            };

            ShowMonth(page);
        }

        int GetPageIndex(DateTime month) => displayMode switch
        {
            DisplayMode.Month => month.Year,
            DisplayMode.Year => GetDecadeStart(month.Year),
            _ => month.Year * 12 + month.Month
        };

        void ShowMonth(DateTime month)
        {
            DateTime previous = currentDisplayMonth;
            currentDisplayMonth = new DateTime(month.Year, month.Month, 1);
            int direction = GetPageIndex(currentDisplayMonth).CompareTo(GetPageIndex(previous));

            CompleteTransition();
            UpdateDisplay();

            if (direction != 0 && CanAnimate())
                StartSlide(GetGrid(displayMode), previous, direction);
        }

        void UpdateNavigationButtons()
        {
            SetButtonInteractable(previousMonthButton, CanChangePage(-1));
            SetButtonInteractable(nextMonthButton, CanChangePage(1));
            SetButtonInteractable(headerButton, CanZoomOut());
        }

        void SetButtonInteractable(Button button, bool value)
        {
            if (button == null || button.interactable == value)
                return;

            button.SetInteractable(value);
        }
        #endregion

        #region Transitions
        bool CanAnimate() => transitionDuration > 0f && isInitialized && isActiveAndEnabled && Application.IsPlaying(gameObject);

        void StartZoom(RectTransform from, RectTransform to, bool zoomOut)
        {
            // Going up shrinks both pages, going down grows them
            float outgoingScale = zoomOut ? 1f - zoomScale : 1f + zoomScale;
            float incomingScale = zoomOut ? 1f + zoomScale : 1f - zoomScale;

            to.gameObject.SetActive(true);
            outgoingLayer = CreateLayer(from, true, Vector2.zero, Vector2.zero, 1f, outgoingScale);
            incomingLayer = CreateLayer(to, false, Vector2.zero, Vector2.zero, incomingScale, 1f);
            BeginTransition();
        }

        void StartSlide(ButtonGrid grid, DateTime previous, int direction)
        {
            if (!grid.HasValidTemplate || grid.CachedContainer is not RectTransform container
                || !container.gameObject.activeInHierarchy || !EnsureTransitionCopy(grid, container))
                return;

            // Show the previous page on the copy, exactly where the live grid is
            RectTransform copy = grid.TransitionCopy;
            copy.anchorMin = container.anchorMin;
            copy.anchorMax = container.anchorMax;
            copy.pivot = container.pivot;
            copy.sizeDelta = container.sizeDelta;
            copy.anchoredPosition = container.anchoredPosition;
            copy.localRotation = container.localRotation;
            copy.localScale = container.localScale;
            if (copy.GetSiblingIndex() > container.GetSiblingIndex())
                copy.SetSiblingIndex(container.GetSiblingIndex());

            FillGrid(displayMode, grid.TransitionItems, previous);
            copy.gameObject.SetActive(true);

            Vector2 size = container.rect.size;
            Vector2 shift = (slideAxis == SlideAxis.Horizontal ? new Vector2(-size.x, 0f) : new Vector2(0f, size.y)) * (slideDistance * direction);

            // A parent layout group owns the live grid position, so only the copy can move
            bool positionDriven = container.parent.TryGetComponent(out LayoutGroup layout) && layout.isActiveAndEnabled;

            outgoingLayer = CreateLayer(copy, true, Vector2.zero, shift, 1f, 1f);
            incomingLayer = CreateLayer(container, false, positionDriven ? Vector2.zero : -shift, Vector2.zero, 1f, 1f);
            BeginTransition();
        }

        bool EnsureTransitionCopy(ButtonGrid grid, RectTransform container)
        {
            if (grid.TransitionCopy != null && grid.TransitionItems.Count == grid.Count)
                return true;

            DestroyTransitionCopy(grid);
            if (container == transform || container.parent == null)
                return false;

            // Day bindings are runtime listeners, so the copy can't select dates
            GameObject copy = Instantiate(container.gameObject, container.parent, false);
            copy.name = $"{container.name} (Transition)";
            grid.TransitionCopy = (RectTransform)copy.transform;

            for (int i = 0; i < grid.TransitionCopy.childCount; i++)
            {
                if (grid.TransitionCopy.GetChild(i).TryGetComponent(out CalendarDay item))
                    grid.TransitionItems.Add(item);
            }

            if (!copy.TryGetComponent(out LayoutElement element))
                element = copy.AddComponent<LayoutElement>();
            if (!copy.TryGetComponent(out CanvasGroup group))
                group = copy.AddComponent<CanvasGroup>();

            element.ignoreLayout = true;
            group.blocksRaycasts = false;

            if (grid.TransitionItems.Count == grid.Count)
                return true;

            DestroyTransitionCopy(grid);
            return false;
        }

        void DestroyTransitionCopy(ButtonGrid grid)
        {
            grid.TransitionItems.Clear();
            if (grid.TransitionCopy != null)
            {
                if (Application.IsPlaying(grid.TransitionCopy.gameObject))
                    Destroy(grid.TransitionCopy.gameObject);
                else
                    DestroyImmediate(grid.TransitionCopy.gameObject);
            }

            grid.TransitionCopy = null;
        }

        TransitionLayer CreateLayer(RectTransform rect, bool isOutgoing, Vector2 fromOffset, Vector2 toOffset, float fromScale, float toScale)
        {
            if (!rect.TryGetComponent(out CanvasGroup group))
                group = rect.gameObject.AddComponent<CanvasGroup>();

            TransitionLayer layer = new()
            {
                Rect = rect,
                Group = group,
                RestPosition = rect.anchoredPosition,
                RestScale = rect.localScale,
                RestAlpha = group.alpha,
                RestBlocksRaycasts = group.blocksRaycasts,
                FromOffset = fromOffset,
                ToOffset = toOffset,
                FromScale = fromScale,
                ToScale = toScale,
                IsOutgoing = isOutgoing
            };

            // The page that leaves can't be clicked while it fades
            if (isOutgoing)
                group.blocksRaycasts = false;

            return layer;
        }

        void BeginTransition()
        {
            ApplyTransition(0f);
            transition = EvoTween.Create(0f, 1f, transitionDuration)
                .WithEase(Ease.Linear)
                .WithTimeMode(useUnscaledTime ? TimeMode.Unscaled : TimeMode.Scaled)
                .OnComplete(transitionCompleteCallback)
                .Bind(this, TransitionUpdateCallback, TransitionPropertyKey);

            if (!transition.IsActive)
                FinishTransition();
        }

        static void ApplyTransitionValue(Calendar calendar, float time) => calendar.ApplyTransition(time);

        void ApplyTransition(float time)
        {
            float eased = TweenEase.Evaluate(transitionEase, time, customEase);
            ApplyLayer(outgoingLayer, time, eased);
            ApplyLayer(incomingLayer, time, eased);
        }

        static void ApplyLayer(in TransitionLayer layer, float time, float eased)
        {
            if (layer.Rect == null)
                return;

            layer.Rect.localScale = layer.RestScale * Mathf.LerpUnclamped(layer.FromScale, layer.ToScale, eased);
            layer.Rect.anchoredPosition = layer.RestPosition + Vector2.LerpUnclamped(layer.FromOffset, layer.ToOffset, eased);

            // Fade through: the leaving page is mostly gone before the new one shows, so they never read as one
            if (layer.Group != null)
            {
                float fade = layer.IsOutgoing ? 1f - EaseFade(time / FadeOutEnd) : EaseFade((time - FadeInStart) / (FadeInEnd - FadeInStart));
                layer.Group.alpha = layer.RestAlpha * fade;
            }
        }

        static float EaseFade(float time)
        {
            time = 1f - Mathf.Clamp01(time);
            return 1f - time * time;
        }

        void FinishTransition() => FinishTransition(true);

        void FinishTransition(bool hideOutgoing)
        {
            transition = default;
            RestoreLayer(ref outgoingLayer, hideOutgoing);
            RestoreLayer(ref incomingLayer, false);
        }

        static void RestoreLayer(ref TransitionLayer layer, bool hide)
        {
            if (layer.Rect != null)
            {
                layer.Rect.localScale = layer.RestScale;
                layer.Rect.anchoredPosition = layer.RestPosition;

                if (layer.Group != null)
                {
                    layer.Group.alpha = layer.RestAlpha;
                    layer.Group.blocksRaycasts = layer.RestBlocksRaycasts;
                }

                if (hide)
                    layer.Rect.gameObject.SetActive(false);
            }

            layer = default;
        }

        void CompleteTransition(bool hideOutgoing = true)
        {
            if (transition.IsActive)
                transition.Kill();

            if (outgoingLayer.Rect != null || incomingLayer.Rect != null)
                FinishTransition(hideOutgoing);
        }
        #endregion

        #region Public API
        /// <summary>
        /// Marks today's date, month, and year in the grids.
        /// </summary>
        public bool HighlightToday
        {
            get => highlightToday;
            set
            {
                if (highlightToday == value)
                    return;

                highlightToday = value;
                Refresh();
            }
        }

        /// <summary>
        /// Duration of the zoom and slide transitions. Zero switches instantly.
        /// </summary>
        public float TransitionDuration
        {
            get => transitionDuration;
            set => transitionDuration = Mathf.Max(0f, value);
        }

        /// <summary>
        /// Scale difference used when zooming between the day, month, and year views, from 0 to 0.5.
        /// </summary>
        public float ZoomScale
        {
            get => zoomScale;
            set => zoomScale = Mathf.Clamp(value, 0f, 0.5f);
        }

        /// <summary>
        /// Page slide distance relative to the grid size, from 0 (crossfade in place) to 1 (a full page).
        /// </summary>
        public float SlideDistance
        {
            get => slideDistance;
            set => slideDistance = Mathf.Clamp01(value);
        }

        /// <summary>
        /// Reuses the buttons in the day, month, and year containers and repairs missing or mismatched entries.
        /// Call after other scripts modify the calendar hierarchy or its references at runtime.
        /// </summary>
        public void Refresh()
        {
#if UNITY_EDITOR
            if (!Application.IsPlaying(gameObject))
            {
                RequestEditorRefresh();
                return;
            }
#endif
            if (!isInitialized)
            {
                Initialize();
                return;
            }

            UpdateDisplay();
            if (!transition.IsActive)
                ApplyViewVisibility();
        }

        /// <summary>
        /// Switches between the day, month, and year views. Month and year views require their container references.
        /// </summary>
        public void SetDisplayMode(DisplayMode mode, bool animate = true)
        {
            if (!Application.IsPlaying(gameObject))
                return;
            if (!isInitialized)
                Initialize();
            if (mode == displayMode || !IsModeAvailable(mode))
                return;

            CompleteTransition();

            DisplayMode previousMode = displayMode;
            RectTransform from = GetView(previousMode);
            RectTransform to = GetView(mode);
            bool zoom = animate && CanAnimate() && from != null && to != null && from != to && from.gameObject.activeInHierarchy;

            displayMode = mode;
            if (mode == DisplayMode.Day)
                ClampDisplayMonth();

            UpdateDisplay();

            if (zoom) { StartZoom(from, to, mode > previousMode); }
            else { ApplyViewVisibility(); }
        }

        /// <summary>
        /// Moves one level up: from days to months, and from months to years.
        /// </summary>
        public void ZoomOut()
        {
            if (!isInitialized)
                Initialize();
            if (CanZoomOut())
                SetDisplayMode(displayMode + 1);
        }

        /// <summary>
        /// Shows the previous month, year, or decade based on the current view.
        /// </summary>
        public void GoToPrevious() => ChangePage(-1);

        /// <summary>
        /// Shows the next month, year, or decade based on the current view.
        /// </summary>
        public void GoToNext() => ChangePage(1);

        public void SelectDate(DateTime date, bool invokeEvents = true)
        {
            if (!isInitialized)
                Initialize();

            selectedDate = date.Date;

            // Show the selected date's month
            ShowMonth(new DateTime(date.Year, date.Month, 1));

            // Invoke event
            if (invokeEvents && Application.IsPlaying(gameObject))
                onDateSelected?.Invoke(date.Date);
        }

        public void SetMinDate(DateTime date)
        {
            if (!isInitialized)
                Initialize();

            minDate = date.Date;
            if (maxDate < minDate)
                maxDate = minDate;

            UpdateYearRange();
            SetupYearDropdown();
            UpdateDisplay();
        }

        public void SetMaxDate(DateTime date)
        {
            if (!isInitialized)
                Initialize();

            maxDate = date.Date;
            if (minDate > maxDate)
                minDate = maxDate;

            UpdateYearRange();
            SetupYearDropdown();
            UpdateDisplay();
        }

        public void ClearSelection()
        {
            if (!isInitialized)
                Initialize();

            selectedDate = null;
            UpdateDisplay();
        }

        /// <summary>
        /// Shows the current month in the day view.
        /// </summary>
        public void GoToToday()
        {
            if (!isInitialized)
                Initialize();

            DateTime month = new(DateTime.Today.Year, DateTime.Today.Month, 1);
            if (displayMode == DisplayMode.Day) { ShowMonth(month); }
            else
            {
                currentDisplayMonth = month;
                SetDisplayMode(DisplayMode.Day);
            }
        }

        public DateTime? GetSelectedDate() => selectedDate;

        public void SelectToday() => SelectDate(DateTime.Now.Date);

        public void SetYearRange(int range)
        {
            if (!isInitialized)
                Initialize();

            yearRangeFromCurrent = Mathf.Clamp(range, 0, 100);
            UpdateYearRange();
            SetupYearDropdown();
            UpdateDisplay();
        }
        #endregion

#if EVO_LOCALIZATION
        void UpdateLocalization(Localization.LocalizationLanguage language = null)
        {
            if (!Application.IsPlaying(gameObject) || !enableLocalization || localizedObject == null)
                return;

            foreach (MonthData item in Months)
            {
                if (!string.IsNullOrEmpty(item.localizationKey))
                {
                    item.monthName = localizedObject.GetString(item.localizationKey);

                    // Without its own key, the short name follows the localized full name
                    item.shortName = !string.IsNullOrEmpty(item.shortLocalizationKey)
                        ? localizedObject.GetString(item.shortLocalizationKey) : null;
                }
                else if (!string.IsNullOrEmpty(item.shortLocalizationKey))
                {
                    item.shortName = localizedObject.GetString(item.shortLocalizationKey);
                }
            }

            if (monthDropdown != null)
                SetupMonthDropdown();

            UpdateDisplay();
        }
#endif

#if UNITY_EDITOR
        void OnValidate() => RequestEditorRefresh();

        void RequestEditorRefresh()
        {
            UnityEditor.EditorApplication.delayCall -= RefreshEditor;
            UnityEditor.EditorApplication.delayCall += RefreshEditor;
        }

        void OnPlayModeChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.EnteredPlayMode
                || state == UnityEditor.PlayModeStateChange.EnteredEditMode)
                RequestEditorRefresh();
        }

        void RefreshEditor()
        {
            UnityEditor.EditorApplication.delayCall -= RefreshEditor;

            if (this == null || !isActiveAndEnabled
                || UnityEditor.EditorUtility.IsPersistent(this) || !gameObject.scene.IsValid())
                return;

            if (isRefreshing || UnityEditor.EditorApplication.isCompiling
                || UnityEditor.EditorApplication.isUpdating || UnityEditor.Undo.isProcessing)
            {
                RequestEditorRefresh();
                return;
            }

            if (Application.IsPlaying(gameObject))
            {
                Refresh();
                return;
            }

            if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            // Enter Play Mode options may preserve this instance and its nonserialized state.
            if (isInitialized)
            {
                CompleteTransition();
                RemoveListeners();

#if EVO_LOCALIZATION
                Localization.LocalizationManager.Unsubscribe(UpdateLocalization);
#endif
                isInitialized = false;
            }

            // A prefab stage is editable, but references into another scene or stage are not owned here.
            if (daysContainer != null && (UnityEditor.EditorUtility.IsPersistent(daysContainer)
                || daysContainer.gameObject.scene != gameObject.scene))
                return;

            displayMode = DisplayMode.Day;
            CacheMonths();
            SetInitialDate();
            UpdateYearRange();
            UpdateDisplay();
        }

        // Saves grid repairs only. The preview (labels, states, header, interactable) follows today's date and is rebuilt
        // on every load, so saving it would dirty the scene or prefab whenever the date changes.
        internal static void SaveEditorChanges(UnityEngine.Object item)
        {
            if (Application.IsPlaying(item) || UnityEditor.EditorUtility.IsPersistent(item))
                return;

            UnityEditor.EditorUtility.SetDirty(item);
            if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(item))
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(item);
        }
#endif
    }
}
