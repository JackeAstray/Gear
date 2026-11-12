---
name: evo-ui-layout-and-navigation
description: "Use this skill whenever the user wants to organize UI into screens, tabs, steps, or scrolling content with Evo UI, or make it work with keyboard and gamepad — e.g. 'add tabs for video / audio / controls', 'switch between main menu screens', 'swipeable pages like a phone app', 'make an onboarding wizard with next and back', 'scroll list with 1000 items', 'smooth scrolling', 'infinite scroll', 'image carousel that auto slides', 'controller navigation doesn't work in my menu', 'keep focus inside the pause menu'. Covers Tab Manager, Page Manager, Stepper, Evo UI Scroll Rect (smooth, snapping, infinite, virtualized lists), Carousel, Menu Bar, and UI Navigation Container. Do NOT use for single controls such as buttons and dropdowns (see evo-ui-create-elements), popups (see evo-ui-overlays), or data tables (see evo-ui-charts-and-data). When in doubt whether a menu structure, scrolling, or navigation request could involve Evo UI, use this skill — Prerequisites shows how to confirm the asset is installed."
metadata:
  asset: "Evo UI"
  publisher: "Michsky"
  asset-version: "2.0.0"
  skill-version: "1.0.0"
  unity: "6000.0+"
  render-pipelines: "Built-in, URP, HDRP"
  category: "tools/gui"
  asset-store-url: "https://assetstore.unity.com/packages/slug/410788"
  documentation-url: "https://evo.michsky.com/docs/evo-ui"
  support-url: "https://www.michsky.com/contact"
  last-verified: "2026-11-03"
---

# Build Menus and Navigation with Evo UI

This skill covers the Evo UI components that structure a UI: **Tab Manager** (buttons that switch content panels), **Page Manager** (swipeable pages), **Stepper** (a step-by-step flow), **Scroll Rect** (Unity's ScrollRect plus smooth scrolling, snapping, infinite scrolling, and virtualized lists), **Carousel**, **Menu Bar**, and **UI Navigation Container** (keyboard and gamepad focus inside a panel).

## When to use this skill

- "settings screen with Video, Audio and Controls tabs"
- "main menu → options → credits screens"
- "swipe between pages like a mobile app"
- "first-launch setup wizard with Continue and Back"
- "show a list of 5000 leaderboard entries without lag"
- "make the scroll view smooth with the mouse wheel"
- "a featured news carousel that slides every 5 seconds"
- "gamepad can't move between buttons in my pause menu"
- "keep keyboard focus inside the dialog"

Not for:
- Individual controls — see `evo-ui-create-elements`.
- Modal windows, toasts, tooltips, context menus, popovers — see `evo-ui-overlays`.
- Tables with rows and columns — see `evo-ui-charts-and-data` (List View).
- Changing the Tab Manager transition motion — its sequences are Evo Tween; see `evo-tween-sequence-presets`.

## Prerequisites

- Evo UI 2.0.0+ on Unity 6000.0+, any render pipeline, Input System active.
- An EventSystem with `InputSystemUIInputModule` (required for UI Navigation Container and for keyboard/gamepad input).
- **Install check:** `System.Type.GetType("Evo.UI.TabManager, Evo.UI") != null`, or confirm the menu **GameObject > Evo UI > Layout and Navigation > Tab Manager** exists. If it fails, tell the user Evo UI isn't installed and link https://assetstore.unity.com/packages/slug/410788.
- `Evo.UI.ScrollRect` and `Evo.UI.Scrollbar` clash with `UnityEngine.UI` names; use the full names or an alias.

Menu items (all under **GameObject > Evo UI > Layout and Navigation**): `Page Manager`, `Tab Manager`, `Stepper`, `Carousel`, `Scroll View (Horizontal)`, `Scroll View (Vertical)`, `Scrollbar (Horizontal)`, `Scrollbar (Vertical)`, `Menu Bar`. Components are also under **Add Component > Evo > UI > Layout** (Scroll Rect, Tab Manager, Page Manager, Stepper, Flexible Grid Layout, Reorderable List), **Evo > UI > UI Elements > Carousel**, and **Evo > UI > Navigation > UI Navigation Container**.

## Quick start

Create a Tab Manager and open a tab from code.

1. Select the Canvas (or a panel) and run **GameObject > Evo UI > Layout and Navigation > Tab Manager**. The template comes with three tabs, IDs `Welcome`, `Indicator`, and `Scripting`, each with a tab button and a content panel.
2. Rename the IDs and the button labels to the user's sections (for example `Video`, `Audio`, `Controls`), and replace the content panels' children with the user's controls.
3. Open a tab from code:

```csharp
using UnityEngine;

public class SettingsScreen : MonoBehaviour
{
    [SerializeField] private Evo.UI.TabManager tabs;

    private void OnEnable() => tabs.OpenTab("Video", animate: false);
    public void ShowAudio() => tabs.OpenTab("Audio");
}
```

Expected result: the Hierarchy contains `Tab Manager` with an `Evo.UI.TabManager` component; `tabs.tabs.Count == 3`; in Play Mode, clicking a tab button switches the panel and `tabs.CurrentTabID` changes.

## Workflows

### Workflow: Tabs (Tab Manager)

**Goal:** Switch content panels with buttons.

**Steps:**
1. Each entry in `tabs` is a `TabManager.Tab` with `tabID` (stable name for scripts), `tabObject` (the content `RectTransform`), `tabButton` (an `Evo.UI.Button`), and `firstSelected` (the object to select for keyboard and gamepad when the tab opens).
2. To add a tab in the Editor: duplicate an existing content panel and tab button, then add an entry:

```csharp
using UnityEditor;
using UnityEngine;

public static class EvoAddTab
{
    public static void Run(Evo.UI.TabManager manager, RectTransform content, Evo.UI.Button button, string id)
    {
        Undo.RecordObject(manager, "Add Tab");
        manager.tabs.Add(new Evo.UI.TabManager.Tab { tabID = id, tabObject = content, tabButton = button });
        button.Text = id;
        EditorUtility.SetDirty(manager);
        EditorUtility.SetDirty(button);
    }
}
```

At runtime, use `AddTab(tab)`, `RemoveTab(index)` or `RemoveTab(id)`, and wrap several changes in `BeginTabsEdit()` / `EndTabsEdit()`.
3. Navigate with `OpenTab(int index, bool animate = true)`, `OpenTab(string id, bool animate = true)`, `OpenNextTab()`, `OpenPreviousTab()`, `OpenFirstTab()`, `OpenLastTab()`. Read `CurrentTabIndex`, `CurrentTabID`, `CurrentTab`; listen to `onTabChanged` (`int`).
4. Options: `disableInvisibleTabs` (default on), `useUnscaledTime` (default on, so tabs work in a paused game), `animateDirectionally`, `activationTiming`, and the `inAnimation` / `outAnimation` Evo Tween sequences.

**Expected result:** `OpenTab("Audio")` makes the Audio panel the only visible one, and `CurrentTabID == "Audio"`.

### Workflow: Swipeable pages (Page Manager)

**Goal:** Pages that the user can swipe or step through.

**Steps:**
1. Create **GameObject > Evo UI > Layout and Navigation > Page Manager**. The template has pages `Home`, `Search`, `Inbox`, `Profile`.
2. Each `PageManager.Page` has `pageID`, `pageObject` (`RectTransform`), and an optional `pageButton`. The component also uses `container` and an optional `indicator`.
3. Navigate with `OpenPage(int index, bool animate = true)`, `OpenNextPage()`, `OpenPreviousPage()`. Read `CurrentPageIndex`, `CurrentPage`, `IsTransitioning`; listen to `onPageChanged` (`int`). After editing `pages`, call `PositionPages()` (or wrap edits in `BeginPagesEdit()` / `EndPagesEdit()`).
4. Set `HandleNestedScrolling = true` when pages contain scroll views, so vertical scrolling and horizontal swiping don't fight.

**Expected result:** swiping or calling `OpenNextPage()` moves to the next page and `onPageChanged` fires with its index.

### Workflow: Step-by-step flow (Stepper)

**Goal:** A wizard with indicators and Back / Continue buttons.

**Steps:**
1. Create **GameObject > Evo UI > Layout and Navigation > Stepper**.
2. Each `Stepper.Item` has `title`, `nextText` (label for the continue button on that step), `content` (a `CanvasGroup`), `canContinue` (default true), and `onEnter`. Call `Refresh()` after editing `items`.
3. Gate a step on validation: `SetCanContinue(false)` when the step opens, then `SetCanContinue(true)` once the input is valid. `CanContinue` reports the current state.
4. Navigate with `NextStep()`, `PreviousStep()`, `GoToStep(index)`, `TryGoToStep(index)` (returns `bool`), `Complete()`, `Restart()`. Options: `allowStepSkipping` (default true), `allowBackwardNavigation` (default true). Events: `onStepChanged` (`int`), `onCompleted`.

```csharp
using UnityEngine;

public class Onboarding : MonoBehaviour
{
    [SerializeField] private Evo.UI.Stepper stepper;

    private void Start()
    {
        stepper.allowStepSkipping = false;
        stepper.SetCanContinue(false);
        stepper.onCompleted.AddListener(() => Debug.Log("Onboarding done"));
    }

    // Connect to the name field's value-changed or submit event.
    public void OnNameChanged(string value) => stepper.SetCanContinue(!string.IsNullOrWhiteSpace(value));
}
```

**Expected result:** the Continue button stays unusable until a name is entered; finishing the last step invokes `onCompleted` and `IsCompleted` is true.

### Workflow: Scroll views, snapping, and infinite scroll

**Goal:** Scrollable content with better input.

**Steps:**
1. Create **GameObject > Evo UI > Layout and Navigation > Scroll View (Vertical)** (or Horizontal). Its hierarchy is `Scroll View (Vertical) > Viewport > Content`, and Content already has a layout group and a `ContentSizeFitter`. Replace the sample `Item` children with the user's items.
2. `EnableSmoothScroll = true` smooths wheel and trackpad input (`SmoothScrollTime` in unscaled seconds). Dragging and explicit positioning stay immediate.
3. Snapping and focus: `SnapToElement(int index, float offset = 0)`, `SnapToElementInstant(index)`, `SnapToElement(RectTransform)`, `ScrollToNext()`, `ScrollToPrevious()`; `onItemFocused` (`int`).
4. `EnableInfiniteScroll = true` repeats content along **one** axis (turn the other axis off).
5. The rest is Unity's ScrollRect API (`horizontal`, `vertical`, `normalizedPosition`, `content`, `viewport`).

**Expected result:** wheel scrolling eases instead of jumping; `SnapToElement(3)` centers the fourth item.

### Workflow: Virtualized list for thousands of items

**Goal:** Show a long data list while only creating the visible rows.

**Steps:**
1. Use a vertical (or horizontal) Evo UI Scroll View and a row prefab whose root has a component you can bind (for example an Evo UI Button, or your own row script).
2. In Play Mode code, set `EnableVirtualization = true`, then bind with `CreateVirtualizedList`, then set the count. The checkbox alone doesn't convert existing children into data.

```csharp
using UnityEngine;
using Evo.UI;
using ScrollRect = Evo.UI.ScrollRect;

public class Leaderboard : MonoBehaviour
{
    [SerializeField] private ScrollRect scroll;
    [SerializeField] private Evo.UI.Button rowPrefab;
    private VirtualizedList<Evo.UI.Button> list;
    private string[] names;

    private void Start()
    {
        names = new string[5000];
        for (int i = 0; i < names.Length; i++)
            names[i] = $"Player {i + 1}";

        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.EnableVirtualization = true;
        list = scroll.CreateVirtualizedList(rowPrefab,
            (row, index) => row.Text = $"#{index + 1}  {names[index]}",
            new VirtualizedListSettings { itemSize = 48f, spacing = 4f });
        list.SetItemCount(names.Length);
    }

    private void OnDestroy() => list?.Dispose();
}
```

3. Settings (`VirtualizedListSettings`): `itemSize` (default 40, along the scroll axis, Canvas units), `spacing`, `padding`, `enableNavigation`, `loopNavigation`, and `getItemSize` (`Func<int, float>`) for variable sizes. `ScrollRect.VirtualizationOverscan` keeps extra rows ready.
4. List control: `SetItemCount(count, resetPosition = false)`, `RefreshItems()`, `RefreshItem(index)`, `ScrollToIndex(index, alignment = 0f)`, `EnsureVisible(index)`, `TryGetItem(index, out row)`, `TrimPool()`, `Dispose()` (restores the previous content layout).
5. Reset everything item-specific in the bind callback (rows are reused). Add listeners once in `createItem`, or remove them in `unbindItem`.

**Expected result:** the Content object holds only enough rows to fill the viewport (plus overscan), `scroll.ItemCount == 5000`, and scrolling shows the right names.

### Workflow: Carousel

**Goal:** Featured content that slides automatically or with buttons.

**Steps:**
1. Create **GameObject > Evo UI > Layout and Navigation > Carousel**.
2. Fill `items` (`Carousel.Item`: `title`, `description`, `background` sprite, `onClick`), then call `Refresh()`.
3. Options: `autoSlide` (default true) and `autoSlideTimer` (seconds, default 3), `showNeighbors` (default false), `neighborOpacity` (0–1, default 0.5), `maxVisibleNeighbors` (default 1).
4. Navigate with `NextItem()`, `PreviousItem()`, `GoToItem(index)`; read `CurrentIndex`, `IsTransitioning`.

**Expected result:** the carousel shows the first item and advances every `autoSlideTimer` seconds in Play Mode.

### Workflow: Keyboard and gamepad navigation

**Goal:** Make a panel fully usable without a mouse and keep focus inside it.

**Steps:**
1. Evo UI controls are `Selectable`s, so Unity's navigation (arrows, WASD, D-pad, stick, Submit) works with the EventSystem's `InputSystemUIInputModule`.
2. Add **Evo > UI > Navigation > UI Navigation Container** to the panel. Defaults: `autoFetchOnEnable` (collects child Selectables), `restrictNavigation` (directional moves stay inside), `tabNavigation` with `tabOrder` `Hierarchy` (or `Position`) and `wrapAround`, `selectOnEnable = EnableSelection.Default`, `restoreSelectionOnDisable` (returns focus when the panel closes).
3. Set `defaultSelection` (first selected control) and optionally `fallbackSelection`; turn on `ensureSelection` so selection recovers when the mouse clears it (`recoveryMode`: `RestoreLast`, `Default`, `First`, `Nearest`). Exclude controls with `excludedFromTabOrder`.
4. After adding or removing controls at runtime, call `RefreshNavigation()`; call `SelectDefault()` to focus the default control.
5. For Tab Manager panels, set each tab's `firstSelected` so the right control is focused when a tab opens.

**Expected result:** in Play Mode, the D-pad moves between the panel's controls only, Tab cycles through them, and closing the panel returns focus to the previous control.

## Verification

- The component exists on the created root: `GetComponent<Evo.UI.TabManager>()`, `PageManager`, `Stepper`, `Carousel`, `Evo.UI.ScrollRect`, or `UINavigationContainer`.
- Tab Manager: every `tabs[i]` has non-null `tabObject` and `tabButton`, and IDs are unique; `OpenTab(id)` in Play Mode sets `CurrentTabID`.
- Page Manager: `pages.Count` matches; `OpenPage(1, false)` sets `CurrentPageIndex == 1`.
- Stepper: `items.Count` matches, and every item has a `content` CanvasGroup.
- Virtualized list: in Play Mode, `scroll.HasVirtualizedList` and `scroll.ItemCount == expected`, and the Content child count is far below the item count.
- Navigation: `EventSystem.current.currentSelectedGameObject` is inside the panel after `SelectDefault()`.
- No errors in the Console.

## API quick reference

| Entry point | Type | What it does |
| --- | --- | --- |
| `Evo.UI.TabManager` | Component | `tabs`, `OpenTab(id/index, animate)`, `OpenNextTab()`, `AddTab`, `RemoveTab`, `onTabChanged`. |
| `Evo.UI.PageManager` | Component | `pages`, `OpenPage(index, animate)`, `OpenNextPage()`, `HandleNestedScrolling`, `onPageChanged`. |
| `Evo.UI.Stepper` | Component | `items`, `SetCanContinue(bool)`, `NextStep()`, `PreviousStep()`, `GoToStep`, `onCompleted`. |
| `Evo.UI.ScrollRect` | Component (extends Unity ScrollRect) | `EnableSmoothScroll`, `EnableInfiniteScroll`, `EnableVirtualization`, `SnapToElement`, `CreateVirtualizedList<T>`. |
| `Evo.UI.VirtualizedList<T>` | Class | `SetItemCount`, `RefreshItems`, `ScrollToIndex`, `EnsureVisible`, `Dispose`. |
| `Evo.UI.Carousel` | Component | `items`, `autoSlide`, `NextItem()`, `GoToItem(index)`, `Refresh()`. |
| `Evo.UI.UINavigationContainer` | Component | Panel focus: `restrictNavigation`, `defaultSelection`, `ensureSelection`, `RefreshNavigation()`, `SelectDefault()`. |

Menu Bar (desktop-style File / Edit menus), Flexible Grid Layout, Radial Layout Group, Reorderable List, Rect Dragger, and Marquee Selection are documented at https://evo.michsky.com/docs/evo-ui (Layout section).

## Common issues

| Symptom | Cause | Fix |
| --- | --- | --- |
| `OpenTab("Audio")` does nothing. | No tab has that exact ID (IDs are matched exactly), or the entry's `tabObject` is null. | Check `tabs[i].tabID` spelling and references. |
| Two tab panels show at once. | Panels were activated by hand, outside the manager. | Let Tab Manager control visibility; keep `disableInvisibleTabs` on. |
| Tabs don't switch while the game is paused. | `useUnscaledTime` was turned off and `Time.timeScale` is 0. | Turn `useUnscaledTime` back on. |
| A swipe page with a scroll view scrolls and swipes at the same time. | Nested drag handling is off. | `HandleNestedScrolling = true`. |
| Enabling Virtualization doesn't reduce objects. | No list is bound; the checkbox doesn't convert children. | Call `CreateVirtualizedList(...)` and `SetItemCount(...)` in Play Mode. |
| Rows show stale text or duplicated click handlers. | Rows are recycled. | Set all row content in the bind callback; add listeners in `createItem` or remove them in `unbindItem`. |
| Infinite scroll behaves oddly. | Both axes are enabled. | Use only `horizontal` or only `vertical`. |
| Gamepad or keyboard can't reach controls. | No selection, StandaloneInputModule, or Navigation set to None/Explicit without targets. | Use `InputSystemUIInputModule`, add a UI Navigation Container with `defaultSelection`, check each Selectable's Navigation. |
| Focus jumps out of the pause menu. | Directional navigation isn't restricted. | UI Navigation Container with `restrictNavigation = true`. |

## Boundaries

- Virtualized lists support a single row or column (no grids) and are created in Play Mode.
- Infinite scroll works on one axis at a time; scrollbars keep the original list endpoints.
- Page Manager has no `OpenPage(string)` overload; use the index (`pages.FindIndex(p => p.pageID == "Inbox")`).
- These components are uGUI only; they don't manage UI Toolkit documents or scene loading. Use `SceneManager` for scene transitions.
- Tab Manager transitions are Evo Tween sequences (`inAnimation`, `outAnimation`); edit them in the Inspector or Timeline (`evo-tween-sequence-presets`). Page Manager uses its own `transitionCurve`, and Stepper uses the Duration and Ease settings under **Animation**.
