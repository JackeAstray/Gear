using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Gear.Utilities.UI.Widgets
{
    /// <summary>一周的起始日。</summary>
    public enum GearWeekStart
    {
        Sunday = 0,
        Monday = 1,
    }

    /// <summary>日历层级：日 / 月 / 年。</summary>
    public enum GearCalendarView
    {
        Day = 0,
        Month = 1,
        Year = 2,
    }

    /// <summary>月视图中的一个日期格。</summary>
    public readonly struct GearCalendarDay
    {
        public readonly DateTime Date;
        public readonly bool InMonth;
        public readonly bool IsToday;
        public readonly bool IsSelected;

        public GearCalendarDay(DateTime date, bool inMonth, bool isToday, bool isSelected)
        {
            Date = date;
            InMonth = inMonth;
            IsToday = isToday;
            IsSelected = isSelected;
        }

        public int Day => Date.Day;
    }

    /// <summary>
    /// 月视图日历：6 行网格、周起始日偏移、今天/选中标记、翻月。
    /// <para>
    /// 日期网格是纯逻辑（<see cref="BuildMonthGrid"/>），EditMode 可直接断言；
    /// 传入 header/grid/template 后才会实例化并刷新格子按钮。
    /// </para>
    /// </summary>
    [AddComponentMenu("UI/Gear/Calendar")]
    [DisallowMultipleComponent]
    public sealed class GearCalendar : MonoBehaviour
    {
        [SerializeField] private TMP_Text headerLabel;
        [SerializeField] private RectTransform gridContent;
        [SerializeField] private Button dayTemplate;
        [SerializeField] private Button previousMonthButton;
        [SerializeField] private Button nextMonthButton;
        [SerializeField] private Button previousYearButton;
        [SerializeField] private Button nextYearButton;
        [SerializeField] private Button zoomOutButton;
        [SerializeField] private GearWeekStart weekStart = GearWeekStart.Sunday;
        [SerializeField] private Color normalColor = new Color(0.16f, 0.18f, 0.25f, 1f);
        [SerializeField] private Color mutedColor = new Color(0.16f, 0.18f, 0.25f, 0.4f);
        [SerializeField] private Color todayColor = new Color(0.14f, 0.75f, 0.95f, 1f);
        [SerializeField] private Color selectedColor = new Color(0.49f, 0.36f, 0.99f, 1f);
        [SerializeField] private string headerFormat = "{0} 年 {1} 月";
        [SerializeField] private string monthHeaderFormat = "{0} 年";
        [SerializeField] private string yearHeaderFormat = "{0}–{1}";
        [SerializeField, Range(4, 24)] private int yearRange = 12;
        [SerializeField] private GearCalendarView view = GearCalendarView.Day;
        [SerializeField] private UnityEvent<string> onDateSelected = new UnityEvent<string>();

        [SerializeField, Range(1, 12)] private int month = 1;
        [SerializeField] private int year;
        [SerializeField] private bool hasSelection;
        [SerializeField] private string selectedDateText;
        private DateTime selectedDate;

        private readonly List<Button> cells = new List<Button>();

        public UnityEvent<string> OnDateSelected => onDateSelected;
        public event Action<DateTime> DateSelected;

        public int Year => year;
        public int Month => month;
        public GearCalendarView View => view;
        public int YearRange => yearRange;
        public int YearPageStart => Mathf.Max(1, year - Mathf.Max(0, year % yearRange));
        public DateTime VisibleMonth => new DateTime(Mathf.Max(1, year), Mathf.Clamp(month, 1, 12), 1);
        public DateTime? SelectedDate => hasSelection ? selectedDate : (DateTime?)null;

        /// <summary>当前 Culture 的 12 个月名；缺项回退英文。</summary>
        public string[] MonthNames
        {
            get
            {
                string[] names = CultureInfo.CurrentCulture.DateTimeFormat.MonthNames;
                var result = new string[12];
                for (int i = 0; i < 12; i++)
                {
                    result[i] = i < names.Length && !string.IsNullOrEmpty(names[i])
                        ? names[i]
                        : CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(i + 1);
                }

                return result;
            }
        }

        public TMP_Text HeaderLabel { get => headerLabel; set { headerLabel = value; RefreshHeader(); } }
        public RectTransform GridContent { get => gridContent; set { gridContent = value; RefreshGrid(); } }
        public Button DayTemplate { get => dayTemplate; set { dayTemplate = value; RefreshGrid(); } }

        public Button PreviousMonthButton
        {
            get => previousMonthButton;
            set { previousMonthButton = value; WireButtons(); }
        }

        public Button NextMonthButton
        {
            get => nextMonthButton;
            set { nextMonthButton = value; WireButtons(); }
        }

        public Button PreviousYearButton
        {
            get => previousYearButton;
            set { previousYearButton = value; WireButtons(); }
        }

        public Button NextYearButton
        {
            get => nextYearButton;
            set { nextYearButton = value; WireButtons(); }
        }

        public Button ZoomOutButton
        {
            get => zoomOutButton;
            set { zoomOutButton = value; WireButtons(); }
        }

        public GearWeekStart WeekStart
        {
            get => weekStart;
            set { weekStart = value; RefreshGrid(); }
        }

        public Color NormalColor { get => normalColor; set => normalColor = value; }
        public Color MutedColor { get => mutedColor; set => mutedColor = value; }
        public Color TodayColor { get => todayColor; set => todayColor = value; }
        public Color SelectedColor { get => selectedColor; set => selectedColor = value; }

        private void OnEnable()
        {
            RestoreSelection();
            EnsureMonth();
            WireButtons();
            RefreshHeader();
            RefreshGrid();
        }

        private void OnDisable()
        {
            UnwireButtons();
        }

        private void WireButtons()
        {
            UnwireButtons();
            if (previousMonthButton != null) previousMonthButton.onClick.AddListener(PreviousPage);
            if (nextMonthButton != null) nextMonthButton.onClick.AddListener(NextPage);
            if (previousYearButton != null) previousYearButton.onClick.AddListener(PreviousYear);
            if (nextYearButton != null) nextYearButton.onClick.AddListener(NextYear);
            if (zoomOutButton != null) zoomOutButton.onClick.AddListener(ZoomOut);
        }

        private void UnwireButtons()
        {
            if (previousMonthButton != null) previousMonthButton.onClick.RemoveListener(PreviousPage);
            if (nextMonthButton != null) nextMonthButton.onClick.RemoveListener(NextPage);
            if (previousYearButton != null) previousYearButton.onClick.RemoveListener(PreviousYear);
            if (nextYearButton != null) nextYearButton.onClick.RemoveListener(NextYear);
            if (zoomOutButton != null) zoomOutButton.onClick.RemoveListener(ZoomOut);
        }

        private void RestoreSelection()
        {
            if (!hasSelection || string.IsNullOrEmpty(selectedDateText)) return;
            if (DateTime.TryParseExact(selectedDateText, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateTime parsed))
            {
                selectedDate = parsed.Date;
            }
        }

        /// <summary>写入可视月份（1-12），不改变选中日期。</summary>
        public void SetVisibleMonth(int newYear, int newMonth, bool notify = false)
        {
            year = Mathf.Max(1, newYear);
            month = Mathf.Clamp(newMonth, 1, 12);
            RefreshHeader();
            RefreshGrid();

            if (notify)
            {
                onDateSelected?.Invoke(VisibleMonth.ToString("yyyy-MM-dd"));
                DateSelected?.Invoke(VisibleMonth);
            }
        }

        public void PreviousMonth()
        {
            DateTime previous = VisibleMonth.AddMonths(-1);
            SetVisibleMonth(previous.Year, previous.Month);
        }

        public void NextMonth()
        {
            DateTime next = VisibleMonth.AddMonths(1);
            SetVisibleMonth(next.Year, next.Month);
        }

        /// <summary>上一年（保持月份，最小到 1 年）。</summary>
        public void PreviousYear()
        {
            if (year <= 1) return;
            SetVisibleMonth(year - 1, month);
        }

        /// <summary>下一年（保持月份，最大到 9999 年）。</summary>
        public void NextYear()
        {
            if (year >= 9999) return;
            SetVisibleMonth(year + 1, month);
        }

        /// <summary>选中日期（会同步可视月份）；非法日期忽略。</summary>
        public bool SelectDate(DateTime date, bool notify = true)
        {
            if (date.Year < 1) return false;

            year = date.Year;
            month = date.Month;
            selectedDate = date.Date;
            selectedDateText = selectedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            hasSelection = true;

            RefreshHeader();
            RefreshGrid();

            if (notify)
            {
                onDateSelected?.Invoke(selectedDate.ToString("yyyy-MM-dd"));
                DateSelected?.Invoke(selectedDate);
            }

            return true;
        }

        /// <summary>选中当前月份中的某一天；越界返回 false。</summary>
        public bool SelectDay(int day, bool notify = true)
        {
            if (day < 1 || day > DateTime.DaysInMonth(Mathf.Max(1, year), Mathf.Clamp(month, 1, 12))) return false;
            return SelectDate(new DateTime(year, month, day), notify);
        }

        public void ClearSelection()
        {
            hasSelection = false;
            selectedDateText = string.Empty;
            RefreshGrid();
        }

        /// <summary>重新渲染（Inspector 预览与外部驱动共用）。</summary>
        public void Refresh()
        {
            EnsureMonth();
            RefreshHeader();
            RefreshGrid();
        }

        /// <summary>
        /// 生成当前月份的日期网格：起始按 <see cref="WeekStart"/> 对齐，行数随月份变化。
        /// </summary>
        public List<GearCalendarDay> BuildMonthGrid()
        {
            return BuildMonthGrid(DateTime.Today);
        }

        public List<GearCalendarDay> BuildMonthGrid(DateTime today)
        {
            EnsureMonth();

            DateTime first = new DateTime(year, month, 1);
            int offset = ((int)first.DayOfWeek - (int)weekStart + 7) % 7;
            DateTime start;
            try
            {
                start = first.AddDays(-offset);
            }
            catch (ArgumentOutOfRangeException)
            {
                // 年 1 月 1 日再往前回退会越界；退化为不补前导格
                start = first;
                offset = 0;
            }
            int daysInMonth = DateTime.DaysInMonth(year, month);
            int total = Mathf.CeilToInt((offset + daysInMonth) / 7f) * 7;

            var result = new List<GearCalendarDay>(total);
            for (int i = 0; i < total; i++)
            {
                DateTime date = start.AddDays(i);
                bool inMonth = date.Year == year && date.Month == month;
                bool isToday = date.Date == today.Date;
                bool isSelected = hasSelection && date.Date == selectedDate.Date;
                result.Add(new GearCalendarDay(date, inMonth, isToday, isSelected));
            }

            return result;
        }

        private void EnsureMonth()
        {
            if (year <= 0)
            {
                DateTime today = DateTime.Today;
                year = today.Year;
                month = today.Month;
            }

            month = Mathf.Clamp(month, 1, 12);
        }

        private void RefreshHeader()
        {
            if (headerLabel == null) return;
            switch (view)
            {
                case GearCalendarView.Month:
                    headerLabel.text = string.Format(monthHeaderFormat, year);
                    break;
                case GearCalendarView.Year:
                    int start = YearPageStart;
                    headerLabel.text = string.Format(yearHeaderFormat, start, start + yearRange - 1);
                    break;
                default:
                    headerLabel.text = string.Format(headerFormat, year, month);
                    break;
            }
        }

        /// <summary>切换日/月/年视图。</summary>
        public void SetView(GearCalendarView newView)
        {
            if (view == newView) return;
            view = newView;
            RefreshHeader();
            RefreshGrid();
        }

        public void ZoomIn()
        {
            if (view == GearCalendarView.Year) SetView(GearCalendarView.Month);
            else if (view == GearCalendarView.Month) SetView(GearCalendarView.Day);
        }

        public void ZoomOut()
        {
            if (view == GearCalendarView.Day) SetView(GearCalendarView.Month);
            else if (view == GearCalendarView.Month) SetView(GearCalendarView.Year);
        }

        public void SelectMonth(int newMonth)
        {
            month = Mathf.Clamp(newMonth, 1, 12);
            view = GearCalendarView.Day;
            RefreshHeader();
            RefreshGrid();
        }

        public void SelectYear(int newYear)
        {
            year = Mathf.Max(1, newYear);
            view = GearCalendarView.Month;
            RefreshHeader();
            RefreshGrid();
        }

        /// <summary>当前视图上一页（日=上月，月=上一年，年=上一段年份）。</summary>
        public void PreviousPage()
        {
            if (view == GearCalendarView.Day) PreviousMonth();
            else if (view == GearCalendarView.Month) SetVisibleMonth(year - 1, month);
            else SetVisibleMonth(Mathf.Max(1, YearPageStart - yearRange), month);
        }

        public void NextPage()
        {
            if (view == GearCalendarView.Day) NextMonth();
            else if (view == GearCalendarView.Month) SetVisibleMonth(year + 1, month);
            else SetVisibleMonth(YearPageStart + yearRange, month);
        }

        private void RefreshGrid()
        {
            if (gridContent == null || dayTemplate == null) return;
            if (view == GearCalendarView.Day)
            {
                RefreshDayGrid();
                return;
            }

            int count = 12;
            EnsureCells(count);
            string[] monthNames = view == GearCalendarView.Month ? MonthNames : null;
            int startYear = YearPageStart;

            for (int i = 0; i < count; i++)
            {
                Button cell = cells[i];
                cell.gameObject.SetActive(true);
                TMP_Text label = cell.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.text = view == GearCalendarView.Month
                        ? monthNames[i]
                        : (startYear + i).ToString();
                }

                cell.interactable = true;
                bool selected = view == GearCalendarView.Month
                    ? hasSelection && selectedDate.Year == year && selectedDate.Month == i + 1
                    : hasSelection && selectedDate.Year == startYear + i;
                Graphic graphic = cell.targetGraphic;
                if (graphic != null) graphic.color = selected ? selectedColor : normalColor;

                int captured = i + 1;
                cell.onClick.RemoveAllListeners();
                if (view == GearCalendarView.Month)
                    cell.onClick.AddListener(() => SelectMonth(captured));
                else
                {
                    int capturedYear = startYear + captured - 1;
                    cell.onClick.AddListener(() => SelectYear(capturedYear));
                }
            }
        }

        private void RefreshDayGrid()
        {
            List<GearCalendarDay> days = BuildMonthGrid();
            EnsureCells(days.Count);

            for (int i = 0; i < cells.Count; i++)
            {
                Button cell = cells[i];
                bool used = i < days.Count;
                cell.gameObject.SetActive(used);
                if (!used) continue;

                GearCalendarDay day = days[i];
                TMP_Text label = cell.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = day.Day.ToString();

                cell.interactable = day.InMonth;
                Graphic graphic = cell.targetGraphic;
                if (graphic != null)
                {
                    graphic.color = day.IsSelected ? selectedColor
                        : day.IsToday ? todayColor
                        : day.InMonth ? normalColor
                        : mutedColor;
                }

                DateTime date = day.Date;
                cell.onClick.RemoveAllListeners();
                cell.onClick.AddListener(() => SelectDate(date));
            }
        }

        private void EnsureCells(int count)
        {
            if (dayTemplate == null || gridContent == null) return;

            // 进入 Play 时非序列化的 cells 会清空，但场景里已经存在生成的格子；
            // 先把现有子按钮接管回来，避免重复实例化（日期会叠两层）。
            if (cells.Count == 0)
            {
                for (int i = 0; i < gridContent.childCount; i++)
                {
                    Button existing = gridContent.GetChild(i).GetComponent<Button>();
                    if (existing != null && existing != dayTemplate) cells.Add(existing);
                }
            }

            while (cells.Count < count)
            {
                Button cell = Instantiate(dayTemplate, gridContent);
                cell.gameObject.SetActive(true);
                cells.Add(cell);
            }

            for (int i = 0; i < cells.Count; i++)
            {
                cells[i].gameObject.SetActive(i < count);
            }

            // 多出来的旧格子（历史重复生成）隐藏掉
            for (int i = 0; i < gridContent.childCount; i++)
            {
                GameObject child = gridContent.GetChild(i).gameObject;
                if (child == dayTemplate.gameObject) continue;
                Button button = child.GetComponent<Button>();
                if (button != null && !cells.Contains(button)) child.SetActive(false);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            month = Mathf.Clamp(month, 1, 12);
            if (string.IsNullOrEmpty(headerFormat)) headerFormat = "{0} 年 {1} 月";
            RefreshHeader();
        }
#endif
    }
}
