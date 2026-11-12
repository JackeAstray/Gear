---
name: evo-ui-charts-and-data
description: "Use this skill whenever the user wants to show numbers or tables in their UI with Evo UI — e.g. 'add a bar chart of weekly sales', 'line graph of player count', 'donut chart of storage used', 'radar chart of character stats', 'leaderboard table', 'import this csv into a table', 'animate the score counting up', 'health bar', 'loading bar', '60 second countdown timer'. Covers Vertical, Horizontal, Line, Pie, and Radar charts, List View tables (columns, rows, CSV, virtualized rows), Counter, Progress Bar, Distribution Bar, and Timer: creating them, feeding data from code, and refreshing. Do NOT use for scroll lists of custom rows (see evo-ui-layout-and-navigation), sliders and other inputs (see evo-ui-create-elements), or shape styling (see evo-ui-procedural-shape). When in doubt whether a chart, table, counter, or progress request could involve Evo UI, use this skill — Prerequisites shows how to confirm the asset is installed."
metadata:
  asset: "Evo UI"
  publisher: "Michsky"
  asset-version: "2.0.0"
  skill-version: "1.1.0"
  unity: "6000.0+"
  render-pipelines: "Built-in, URP, HDRP"
  category: "tools/gui"
  asset-store-url: "https://assetstore.unity.com/packages/slug/410788"
  documentation-url: "https://evo.michsky.com/docs/evo-ui"
  support-url: "https://www.michsky.com/contact"
  last-verified: "2026-11-04"
---

# Display Data with Evo UI

Evo UI includes procedural charts (vertical and horizontal bar, line, pie/donut, radar), a **List View** table, an animated **Counter**, **Progress Bar** (horizontal, vertical, radial), **Distribution Bar** (a stacked share bar), and **Timer** (countdown, count-up, or clock). This skill covers creating each one and feeding it data from editor scripts or game code.

## When to use this skill

- "bar chart showing kills per match"
- "line chart of daily active users with a filled area"
- "donut chart: 65% used, 35% free"
- "radar chart for speed, power, control, defense"
- "leaderboard table with rank, name, score columns"
- "load this CSV into a table"
- "coins counter that rolls up when you collect gold"
- "loading screen progress bar with a percentage"
- "60 second countdown that ends the round"

Not for:
- Long lists of custom row prefabs — see `evo-ui-layout-and-navigation` (virtualized Scroll Rect).
- Sliders, selectors, and other input controls — see `evo-ui-create-elements`.
- A bare progress ring drawn with one shape — see `evo-ui-procedural-shape` (clipping).

## Prerequisites

- Evo UI 2.0.0+ on Unity 6000.0+, any render pipeline.
- **Install check:** `System.Type.GetType("Evo.UI.VerticalChart, Evo.UI") != null`, or confirm the menu **GameObject > Evo UI > Data Display > Vertical Chart** exists. If it fails, tell the user Evo UI isn't installed and link https://assetstore.unity.com/packages/slug/410788.
- TMP Essential Resources imported (labels are text).

Menu items under **GameObject > Evo UI**:

| Element | Menu path | Root component (`Evo.UI`) |
| --- | --- | --- |
| Bar charts | `Data Display/Vertical Chart`, `Data Display/Horizontal Chart` | `VerticalChart`, `HorizontalChart` |
| Line chart | `Data Display/Line Chart` | `LineChart` |
| Pie / donut | `Data Display/Pie Chart` | `PieChart` |
| Radar | `Data Display/Radar Chart` | `RadarChart` |
| Table | `Data Display/List View` | `ListView` |
| Counter | `Data Display/Counter` | `Counter` |
| Progress | `Progress and Loading/Progress Bar (Horizontal)`, `(Vertical)`, `(Radial)` | `ProgressBar` |
| Stacked share | `Progress and Loading/Distribution Bar` | `DistributionBar` |
| Timer | `Date and Time/Timer`, `Timer (Horizontal)`, `Timer (Vertical)`, `Timer (Radial)` | `Timer` |

## Quick start

Create a bar chart with real data.

1. Select the Canvas (or a panel) and run **GameObject > Evo UI > Data Display > Vertical Chart**.
2. Replace the sample data and redraw:

```csharp
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class EvoWeeklyChart
{
    public static void Run()
    {
        var canvas = Object.FindAnyObjectByType<Canvas>();
        Selection.activeGameObject = canvas.gameObject;
        EditorApplication.ExecuteMenuItem("GameObject/Evo UI/Data Display/Vertical Chart");

        GameObject go = Selection.activeGameObject;
        var chart = go.GetComponent<Evo.UI.VerticalChart>();
        Undo.RegisterFullObjectHierarchyUndo(go, "Fill Chart");
        go.name = "Weekly Sales Chart";

        chart.ClearData();
        chart.AutoScale = true;
        chart.ShowValues = true;
        string[] days = { "Mon", "Tue", "Wed", "Thu", "Fri" };
        float[] sales = { 42f, 55f, 38f, 71f, 64f };
        for (int i = 0; i < days.Length; i++)
            chart.AddDataPoint(days[i], sales[i]);

        chart.DrawChart();
        EditorSceneManager.MarkSceneDirty(go.scene);
    }
}
```

Expected result: `Weekly Sales Chart` shows five labeled bars with values; `chart.dataPoints.Count == 5`. Charts draw in Edit Mode too, so the result is visible without entering Play Mode.

## Workflows

### Workflow: Bar and line charts

**Goal:** Plot labeled values.

**Steps:**
1. Data: `ClearData()`, `AddDataPoint(string label, float value)`, `SetDataPoint(int index, string label, float value)`, `RemoveDataPoint(int index)`, `UpdateData(List<T.DataPoint>)`; the list is `dataPoints`. Call `DrawChart()` after editing `dataPoints` directly.
2. Range: `AutoScale = true` (default) derives the range; set `AutoScale = false` and `MaximumValue` (bar charts) or `ValueRange` (`Vector2`, line chart) for a fixed range. Line chart: `IncludeZero`.
3. Appearance (properties refresh the chart): `ScaleDivisions` (1–50), `ShowGrid`, `ShowAxis`, `ShowCategoryLabels`, `ShowScaleLabels`, `ShowValues`, `LabelRotationMode`, `DisplayFormat` (`NumberDisplayFormat`). Bars: `BarSpacing`, `BarCornerRadius`, `UseDataColors`. Line: `Curve` (`LineChart.CurveMode.Straight` / `Smooth`), `LineThickness`, `ShowFill`, `FillOpacity`, `ShowPoints`, `PointSize`.
4. `HorizontalChart` has the same data API as `VerticalChart` with bars running sideways.

```csharp
lineChart.ClearData();
lineChart.Curve = Evo.UI.LineChart.CurveMode.Smooth;
lineChart.ShowFill = true;
lineChart.AddDataPoint("Mon", 1200f);
lineChart.AddDataPoint("Tue", 1850f);
lineChart.AddDataPoint("Wed", 1600f);
lineChart.DrawChart();
```

**Expected result:** `dataPoints.Count` equals the number of added points, and the chart shows them in order.

### Workflow: Pie, donut, and radar charts

**Goal:** Show shares or a stat profile.

**Steps:**
1. Pie: `ClearData()`, `AddDataPoint(string label, float value, Color color)`, `SetDataPoint(int index, string label, float value, Color? color = null)`. `InnerRadiusRatio` 0 = full pie, 0.6 = donut with a 60% hole (max 0.95). `SliceSpacing` adds gaps, `StartAngle` rotates. Labels: `ShowLabels`, `ShowPercentages`, `MinimumLabelPercentage`. Legend: `ShowLegend` with a `LegendContainer` (`RectTransform`), `LegendValues`.
2. Radar: `ClearDataPoints()` (note the different name), `AddDataPoint(string label, float value)`, `SetDataPoint(string label, float value)` or by index, `UpdateAllData(float[] values)` in the current order, `MaximumValue` for the outer ring.
3. Call `DrawChart()` after direct list edits.

```csharp
pie.ClearData();
pie.InnerRadiusRatio = 0.6f;
pie.AddDataPoint("Used", 65f, new Color32(56, 189, 248, 255));
pie.AddDataPoint("Free", 35f, new Color32(71, 85, 105, 255));
pie.DrawChart();

radar.ClearDataPoints();
radar.MaximumValue = 100f;
radar.AddDataPoint("Speed", 80f);
radar.AddDataPoint("Power", 65f);
radar.AddDataPoint("Control", 90f);
radar.DrawChart();
```

**Expected result:** the pie shows two slices at 65% and 35%; the radar shows a three-axis polygon.

### Workflow: Chart hover, clicks, and tooltips

**Goal:** React when the player hovers or clicks a bar, line point, pie slice, or radar point.

**Steps:**
1. Turn on `Interactable` (Inspector: **Interaction > Interactable**). Charts added with **Add Component** start with it on; the Create menu templates and existing charts have it off. It works at runtime only and needs an EventSystem.
2. Events pass the data point index: `onPointClicked`, `onPointEntered`, `onPointExited` (`UnityEvent<int>`, Inspector: **Events**). `HoveredIndex` is the point under the pointer, or -1.
3. Hover effects: `HoverExpand` (local UI units, default 4: bars and points grow, pie slices move out), `DimmedOpacity` (opacity of the other points, default 0.6; 1 turns dimming off), `HoverDuration` (seconds on unscaled time, default 0.2). `PointerAreaMode`: `ChartBase.PointerArea.Category` (default; the whole column, line x band, or radar sector) or `Element` (only the drawn bar or marker; gaps pass through). Pie charts always use the slice.
4. Tooltip: add a **Tooltip** component to an object other than the chart or its parents, assign its `tooltipPreset`, and set the chart's `tooltip` field. The chart fills `Title` with the point label and `Description` with its value (pie: the percentage when Show Percentages is on). With the Tooltip's `followCursor` off, it is placed against the hovered bar or point (`Tooltip.Target`).
5. `HighlightedIndex` highlights a point from code like a hover, without events or the tooltip; -1 clears it.

```csharp
using UnityEngine;

public class SalesChartClicks : MonoBehaviour
{
    [SerializeField] private Evo.UI.VerticalChart chart;
    [SerializeField] private Evo.UI.Tooltip pointTooltip;

    private void Start()
    {
        chart.Interactable = true;
        chart.tooltip = pointTooltip;
        chart.onPointClicked.AddListener(index => Debug.Log("Clicked " + chart.dataPoints[index].label));
    }
}
```

**Expected result:** in Play Mode, hovering a bar grows it and dims the others, the tooltip shows its label and value, and clicking it logs the label.

### Workflow: Tables with List View

**Goal:** Show rows and columns, such as a leaderboard.

**Steps:**
1. Create **GameObject > Evo UI > Data Display > List View**.
2. Columns: `columns` is a `List<ListViewColumn>` (`columnName`, `columnIcon`, `width`, `useFlexibleWidth`, `alignment`). Add with `AddColumn(new ListViewColumn { columnName = "Rank", width = 80, useFlexibleWidth = false })`; remove with `RemoveColumn(index)`.
3. Rows: `ClearRows()`, `AddRow(params string[] values)` (values in column order), `RemoveRow(index)`, `SetCellValue(row, column, text)`, `SetCellData(row, column, value, icon = null, customObject = null)`, `GetCellValue(row, column)`. Call `Rebuild()` after structural changes made directly to `columns`/`rows`; `Refresh()` for settings changes.
4. CSV: `ImportFromCSV(csvText, hasHeaders = true, clearExisting = true)` and `ExportToCSV(includeHeaders = true)`. CSV keeps text only (no sprites or custom objects).
5. Sizing: `scaleMode` `ListView.ScaleMode.Fixed` (default; keeps row height and scrolls) or `Automatic` (rows share the List View's height, no scrolling). For long tables in Fixed mode, `virtualizeRows = true` keeps cells only for visible rows. Call `Refresh()` after changing either.
6. Interaction: `onCellClicked` (`int row, int column`), `onRowShown` (`int`), `ScrollToRow(row, alignment = 0, instant = false)`, `EnsureVisible(row, column = -1, instant = false)`.

```csharp
using UnityEngine;

public class LeaderboardTable : MonoBehaviour
{
    [SerializeField] private Evo.UI.ListView table;

    private void Start()
    {
        table.columns.Clear();
        table.AddColumn(new Evo.UI.ListViewColumn { columnName = "Rank", width = 80f, useFlexibleWidth = false });
        table.AddColumn(new Evo.UI.ListViewColumn { columnName = "Player" });
        table.AddColumn(new Evo.UI.ListViewColumn { columnName = "Score", width = 120f, useFlexibleWidth = false });

        table.ClearRows();
        table.AddRow("1", "Nova", "12400");
        table.AddRow("2", "Kai", "11950");
        table.AddRow("3", "Rin", "10200");
        table.Rebuild();

        table.onCellClicked.AddListener((row, column) => Debug.Log(table.GetCellValue(row, 1)));
    }
}
```

**Expected result:** `table.ColumnCount == 3`, `table.RowCount == 3`, and the header shows Rank, Player, Score.

### Workflow: Animated numbers with Counter

**Goal:** Show a number that animates when it changes (score, coins).

**Steps:**
1. Create **GameObject > Evo UI > Data Display > Counter**. Its `textFormat` defaults to `"${0}"`; change it (for example `"{0}"` or `"Score: {0}"`), keeping `{0}`.
2. `SetValueInstant(x)` to initialize, then `SetValue(x)`, `AddToValue(amount)`, or `SubtractFromValue(amount)` to animate. `value` is the target; `CurrentValue` is what is displayed during animation; `IsAnimating`.
3. Options: `displayFormat` (`NumberDisplayFormat`, e.g. `Fixed0`), `rollThroughDigits` (default true), `counterDuration`, `digitStagger`, `useUnscaledTime` (default true).

```csharp
coins.textFormat = "{0}";
coins.SetValueInstant(0);
coins.AddToValue(250); // Rolls from 0 to 250.
```

**Expected result:** in Play Mode the text rolls to 250 and `coins.value == 250`.

### Workflow: Progress bars and distribution bars

**Goal:** Show loading, health, or XP progress, or a split between parts.

**Steps:**
1. Create **GameObject > Evo UI > Progress and Loading > Progress Bar (Horizontal)** (or Vertical, Radial).
2. Set `MinValue` / `MaxValue`, then assign `Value` whenever progress changes (`onValueChanged` reports the value). `SetValueWithoutNotify(v)` skips the event; `SetValueInstant(v)` skips smoothing.
3. Label: `displayMultiplier` (e.g. 100 for a 0–1 range), `textFormat` (e.g. `"{0}%"`), `displayFormat`. Fill: `enableSmoothing`, `smoothingDuration`, `invertFill`.
4. Distribution Bar: `segments` (`DistributionBar.Segment`: `label`, `value`, `fillRect`, `valueText`); `SetValue(index, value, sendCallback = true, instant = false)`; `SetValues(values)` needs exactly one value per segment; `textDisplay` (`Percentage` or `Value`); `Refresh()` after direct edits.

```csharp
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadingScreen : MonoBehaviour
{
    [SerializeField] private Evo.UI.ProgressBar bar;

    private System.Collections.IEnumerator Start()
    {
        bar.MinValue = 0f;
        bar.MaxValue = 1f;
        bar.displayMultiplier = 100f;
        bar.textFormat = "{0}%";
        bar.SetValueInstant(0f);

        var load = SceneManager.LoadSceneAsync("Game");
        while (!load.isDone)
        {
            bar.Value = Mathf.Clamp01(load.progress / 0.9f);
            yield return null;
        }
    }
}
```

**Expected result:** the bar fills from 0% to 100% while the scene loads.

### Workflow: Countdown timer

**Goal:** Count down (or up) and react when time runs out.

**Steps:**
1. Create **GameObject > Evo UI > Date and Time > Timer** (or a Horizontal / Vertical / Radial variant with a fill).
2. Set `duration` (seconds, default 60) and `countDown` (default true), call `ResetTimer()`, then `StartTimer()` (or enable **Auto Start**). `PauseTimer()` / `ResumeTimer()`; `StopTimer()` invokes `onTimerStop`; finishing invokes `onTimerComplete`.
3. `AddTime(seconds)` / `SubtractTime(seconds)`, `GetRemainingTime()`, `GetElapsedTime()`, `IsRunning`, `IsCompleted`. `useUnscaledTime` keeps it running when `Time.timeScale` is 0. `useSystemClock` turns it into a clock of the local time instead.

```csharp
roundTimer.useSystemClock = false;
roundTimer.duration = 60f;
roundTimer.countDown = true;
roundTimer.ResetTimer();
roundTimer.onTimerComplete.AddListener(EndRound);
roundTimer.StartTimer();
```

**Expected result:** in Play Mode the display counts down from 60 seconds to zero (in the timer's chosen time format) and `EndRound` runs once.

## Verification

- The component exists on the created root (see the table in Prerequisites).
- Charts: `dataPoints.Count` matches, labels match the inputs, and the chart's generated children exist after `DrawChart()`.
- List View: `ColumnCount`, `RowCount`, and `GetCellValue(r, c)` match the intended data.
- Counter: `value` equals the target; `textFormat` contains `{0}`.
- Progress Bar: `MinValue <= Value <= MaxValue`; `textFormat` contains `{0}`.
- Timer: `duration` and `countDown` are as intended; in Play Mode `IsRunning` is true after `StartTimer()`.
- Scene saved and no Console errors.

## API quick reference

| Entry point | Type | What it does |
| --- | --- | --- |
| `Evo.UI.VerticalChart` / `HorizontalChart` | Component | `AddDataPoint(label, value)`, `ClearData()`, `AutoScale`, `MaximumValue`, `DrawChart()`. |
| `Evo.UI.LineChart` | Component | Same data API; `Curve`, `ShowFill`, `LineThickness`, `ValueRange`. |
| `Evo.UI.PieChart` | Component | `AddDataPoint(label, value, color)`, `InnerRadiusRatio`, `SliceSpacing`, legend options. |
| `Evo.UI.RadarChart` | Component | `AddDataPoint`, `SetDataPoint(label, value)`, `UpdateAllData(float[])`, `ClearDataPoints()`, `MaximumValue`. |
| `Evo.UI.ChartBase` (all charts) | Base class | `Interactable`, `onPointClicked` / `onPointEntered` / `onPointExited` (`int` index), `HoveredIndex`, `HighlightedIndex`, `tooltip`, `HoverExpand`, `DimmedOpacity`, `PointerAreaMode`. |
| `Evo.UI.ListView` | Component | `AddColumn`, `AddRow(params string[])`, `SetCellValue`, `ImportFromCSV`, `Rebuild()`, `onCellClicked`. |
| `Evo.UI.Counter` | Component | `SetValue`, `SetValueInstant`, `AddToValue`, `textFormat`. |
| `Evo.UI.ProgressBar` | Component | `Value`, `MinValue`, `MaxValue`, `displayMultiplier`, `textFormat`. |
| `Evo.UI.DistributionBar` | Component | `segments`, `SetValue(index, value)`, `SetValues(values)`. |
| `Evo.UI.Timer` | Component | `duration`, `countDown`, `ResetTimer()`, `StartTimer()`, `onTimerComplete`. |

Full property lists: https://evo.michsky.com/docs/evo-ui (UI Elements section: each chart, List View, Counter, Progress Bar, Distribution Bar, Timer).

## Common issues

| Symptom | Cause | Fix |
| --- | --- | --- |
| The chart still shows the sample data. | Data was appended without clearing, or `dataPoints` was edited without redrawing. | `ClearData()` (radar: `ClearDataPoints()`) before adding; call `DrawChart()` after direct edits. |
| Bars are all the same height or clipped. | `AutoScale` is off with a small `MaximumValue`. | Turn `AutoScale` on, or set `MaximumValue` above the largest value. |
| Pie slices vanish. | `SliceSpacing` is wider than the slices. | Lower `SliceSpacing`. |
| Hovering or clicking a chart does nothing. | `Interactable` is off, the scene has no EventSystem, or it's Edit Mode. | Set `Interactable = true` and test in Play Mode with an EventSystem. |
| The chart tooltip opens over the whole chart or flickers. | The Tooltip component is on the chart or one of its parents. | Put the Tooltip on another object and assign it to the chart's `tooltip`. |
| A card that contains the chart no longer gets clicks over the bars. | An interactable chart takes clicks over its points. | Use `PointerArea.Element` so gaps pass through, or turn `Interactable` off on that chart. |
| Counter shows `$` before the number. | Default `textFormat` is `"${0}"`. | Set `textFormat = "{0}"` (or your own template). |
| A table row has empty cells. | The row has fewer values than there are columns. | Pass one value per column to `AddRow`. |
| A big List View is slow. | All rows build cells. | `scaleMode = Fixed`, `virtualizeRows = true`, then `Refresh()`. |
| `GetCellObject` returns null. | With Virtualize Rows on, rows out of view have no cell objects. | Scroll the row into view (`ScrollToRow`) or use the data API. |
| The timer doesn't move during a pause menu. | Scaled time with `Time.timeScale = 0`. | `useUnscaledTime = true`. |
| `SetValues` throws `ArgumentException`. | The value count differs from `SegmentCount`. | Pass exactly one value per segment. |

## Boundaries

- Charts take label/value data only; there are no scatter, candlestick, or multi-series charts. Overlay two charts or use a dedicated charting package for those.
- List View cells are text, icon, or custom object; CSV import and export keep text only.
- Line and bar charts plot categories in order; there is no numeric x-axis.
- Chart points react to the pointer only; keyboard and gamepad navigation between points isn't built in (drive `HighlightedIndex` from your own input instead).
- Timer's system clock mode shows the time of day, not a countdown to a date.
- For a custom-looking progress ring without labels, a single Procedural Shape with clipping is lighter (`evo-ui-procedural-shape`).
