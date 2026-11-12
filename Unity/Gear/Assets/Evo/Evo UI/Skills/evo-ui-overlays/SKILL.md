---
name: evo-ui-overlays
description: "Use this skill whenever the user wants a popup, dialog, or temporary message in their UI with Evo UI — e.g. 'show an are you sure dialog before quitting', 'add a confirm / cancel popup', 'show a toast that says saved', 'pop a notification when an achievement unlocks', 'show a tooltip when hovering this button', 'add a right click menu with rename and delete', 'make a little dropdown panel that opens when i click the profile icon', 'close the popup when clicking outside'. Covers Modal Window, Toast Notification, Tooltip, Context Menu (with submenus), and Popover: creating them, setting content, opening and closing from code, start and close behavior, stacking, and custom presets. Do NOT use for dropdown selection lists (see evo-ui-create-elements), full screens and tabs (see evo-ui-layout-and-navigation), or blur behind a dialog (see evo-ui-effects). When in doubt whether a popup or overlay request could involve Evo UI, use this skill — Prerequisites shows how to confirm the asset is installed."
metadata:
  asset: "Evo UI"
  publisher: "Michsky"
  asset-version: "2.0.0"
  skill-version: "1.0.2"
  unity: "6000.0+"
  render-pipelines: "Built-in, URP, HDRP"
  category: "tools/gui"
  asset-store-url: "https://assetstore.unity.com/packages/slug/410788"
  documentation-url: "https://evo.michsky.com/docs/evo-ui"
  support-url: "https://www.michsky.com/contact"
  last-verified: "2026-11-04"
---

# Show Dialogs, Toasts, and Tooltips with Evo UI

Evo UI has five overlay components: **Modal Window** (a dialog with confirm and cancel), **Notification** (a toast that closes itself), **Tooltip** (hover help), **Context Menu** (a right-click menu with submenus), and **Popover** (a panel that toggles open and closed). All animate with Evo Tween sequences. This skill covers creating each one, filling in its content, and opening it from game code.

## When to use this skill

- "ask 'quit to desktop?' with yes and no buttons"
- "show a 'game saved' toast in the corner for 3 seconds"
- "show a notification when the player levels up"
- "add a tooltip that explains this stat when you hover it"
- "right click an inventory slot to get use / drop / split"
- "clicking the bell opens a small panel, clicking outside closes it"

Not for:
- Dropdown lists and selectors — see `evo-ui-create-elements`.
- Switching full screens, tabs, or wizard steps — see `evo-ui-layout-and-navigation`.
- Blurring the game behind a dialog — see `evo-ui-effects` (Backdrop Blur).
- Changing overlay colors and fonts globally — see `evo-ui-styler-themes`.

## Prerequisites

- Evo UI 2.0.0+ on Unity 6000.0+, any render pipeline, Input System active, an EventSystem with `InputSystemUIInputModule` in the scene.
- **Install check:** `System.Type.GetType("Evo.UI.ModalWindow, Evo.UI") != null`, or confirm the menu **GameObject > Evo UI > Overlays > Modal Window** exists. If it fails, tell the user Evo UI isn't installed and link https://assetstore.unity.com/packages/slug/410788.
- Overlay templates live in `Assets/Evo/Evo UI/Prefabs/Overlays/`: `Modal Window.prefab`, `Modal Window (Alt).prefab`, `Toast Notification.prefab`, `Popover.prefab`, `Tooltip Preset.prefab`, `Context Menu.prefab` (+ row prefabs `Context Menu - Button.prefab`, `Context Menu - Button (Negative).prefab`, `Context Menu - Separator.prefab`, `Context Menu - Submenu.prefab`).
- `Evo.UI.ContextMenu` clashes with `UnityEngine.ContextMenu`. Add `using ContextMenu = Evo.UI.ContextMenu;` or write the full name.

## Quick start

Add a quit confirmation dialog that stays hidden until requested.

1. Select the Canvas and run **GameObject > Evo UI > Overlays > Modal Window**. It is created as `Modal Window`.
2. Set its content, hide it, and save:

```csharp
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class EvoAddQuitDialog
{
    public static void Run()
    {
        var canvas = Object.FindAnyObjectByType<Canvas>();
        Selection.activeGameObject = canvas.gameObject;
        EditorApplication.ExecuteMenuItem("GameObject/Evo UI/Overlays/Modal Window");

        GameObject go = Selection.activeGameObject;
        var modal = go.GetComponent<Evo.UI.ModalWindow>();
        Undo.RegisterFullObjectHierarchyUndo(go, "Configure Quit Dialog");
        go.name = "Quit Dialog";
        modal.Title = "Quit to desktop?";
        modal.Description = "Unsaved progress will be lost.";
        go.SetActive(false); // Start hidden; Open() activates it.

        EditorSceneManager.MarkSceneDirty(go.scene);
        EditorSceneManager.SaveScene(go.scene);
    }
}
```

3. At runtime, open it and react to the buttons:

```csharp
using UnityEngine;

public class QuitFlow : MonoBehaviour
{
    [SerializeField] private Evo.UI.ModalWindow quitDialog;

    private void Awake() => quitDialog.onConfirm.AddListener(Application.Quit);
    public void AskToQuit() => quitDialog.Open();
}
```

Expected result: the Hierarchy has an inactive `Quit Dialog` with `ModalWindow.Title == "Quit to desktop?"`. In Play Mode, `AskToQuit()` activates and animates it in, and the confirm button invokes `onConfirm`.

## Workflows

### Workflow: Modal Window

**Goal:** Ask the user to confirm or cancel an action.

**Steps:**
1. Create it with **GameObject > Evo UI > Overlays > Modal Window** (or **Modal Window (Alt)**, which has a negative confirm button and a dimmed background).
2. Content: `Title`, `Description`, `Icon` (a `Sprite`, `null` hides it). The buttons are `confirmButton` and `cancelButton` (`Evo.UI.Button`), so their labels can be set with `modal.confirmButton.Text = "Quit"`.
3. Behavior fields:
   - `closeOnConfirm` / `closeOnCancel` (default true). Turn `closeOnConfirm` off to validate input first, then call `Close()` yourself.
   - `closeBehavior`: `ModalWindow.CloseBehavior.Disable` (default) or `Destroy`.
   - `navigationMode`: `Free` (default) or `Focused` (keyboard and gamepad selection stays on the confirm and cancel buttons).
   - **Start Behavior** (Inspector, serialized `startBehavior`): `Open` (default, opens when the scene starts) or `Disabled`. Leaving the GameObject inactive in the scene also keeps it closed until `Open()`.
4. Runtime: `Open()`, `Close()`, `IsOpen`; events `onOpen`, `onClose`, `onConfirm`, `onCancel`.
5. To set Start Behavior from an editor script:

```csharp
var so = new SerializedObject(modal);
so.FindProperty("startBehavior").enumValueIndex = 1; // 0 = Open, 1 = Disabled
so.ApplyModifiedProperties();
```

**Expected result:** the window opens only when `Open()` is called, and `IsOpen` reflects its state.

### Workflow: Toast notifications

**Goal:** Show a short message that closes itself.

**Steps:**
1. Create the template once: **GameObject > Evo UI > Overlays > Toast Notification**, adjust it, and save it as a prefab in the user's folder (for example `Assets/UI/Prefabs/Toast.prefab`) with `PrefabUtility.SaveAsPrefabAsset`. Delete the scene copy if it's only a template.
2. Create a container where toasts appear (e.g. an empty RectTransform anchored to the top right, optionally with a `VerticalLayoutGroup`). With `enableStacking` on (default), toasts in the same area are coordinated.
3. Spawn from code with `Notification.Create(preset, icon, title, description, parent, openAfter = true)`. Spawned toasts get `destroyAfter = true`:

```csharp
using UnityEngine;

public class Toasts : MonoBehaviour
{
    [SerializeField] private GameObject toastPrefab;   // Prefab with Evo.UI.Notification on its root.
    [SerializeField] private RectTransform toastArea;
    [SerializeField] private Sprite saveIcon;

    public void ShowSaved()
    {
        var toast = Evo.UI.Notification.Create(toastPrefab, saveIcon, "Saved", "Your progress is stored.", toastArea, openAfter: false);
        toast.autoCloseDelay = 3f;
        toast.Open();
    }
}
```

4. Use steps 1–3 for toasts that game events trigger (saves, achievements, level-ups): they can repeat and overlap, and spawned toasts stack and destroy themselves. Only for one fixed message that never stacks, you can instead keep a single toast in the scene and call `SetContent(icon, title, description)` then `Open()`.
5. Settings: `autoClose` (default true), `autoCloseDelay` (seconds, default 3), `showDurationBar` with `durationBar` (a Procedural Shape clipped by the remaining time), `playOnEnable` (open when enabled; off in the supplied prefab), `destroyAfter`, `ForceClose()` for an immediate close. With `soundEffects` on, the supplied toast plays the Styler audio ID `Notification SFX` when it opens (`openSFX`; `closeSFX` is empty).

**Expected result:** a `Notification` appears under `toastArea`, closes after 3 s, and is destroyed.

### Workflow: Tooltips

**Goal:** Show help text when the pointer hovers (or a controller selects) an element.

**Steps:**
1. Add **Evo > UI > UI Elements > Tooltip** to the target object. The target needs a raycast target Graphic (or a Collider plus **Is 3D Object** for scene objects).
2. Assign `tooltipPreset` to `Assets/Evo/Evo UI/Prefabs/Overlays/Tooltip Preset.prefab`, or a copy of it.
3. Set content with `SetContent(title, description, icon = null)` or the `Title`, `Description`, and `Icon` properties (they also refresh a visible tooltip).
4. Options: `showDelay` (default 0), `followCursor` (default true), `maxWidth` (default 400), `offsetPosition`, `offsetDistance`, `screenEdgePadding`, `customContent` (a prefab used instead of the preset layout; Tooltip positions it but doesn't fill its text). `Target` (a `RectTransform`, runtime only) places the tooltip against another rect when `followCursor` is off; null uses the Tooltip's own object. Charts use it for their hovered point (see `evo-ui-charts-and-data`).

```csharp
using UnityEditor;
using UnityEngine;

public static class EvoAddTooltip
{
    public static void Run(GameObject target)
    {
        var tooltip = Undo.AddComponent<Evo.UI.Tooltip>(target);
        tooltip.tooltipPreset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Evo/Evo UI/Prefabs/Overlays/Tooltip Preset.prefab");
        tooltip.SetContent("Strength", "Increases melee damage by 2 per point.");
        tooltip.showDelay = 0.3f;
        EditorUtility.SetDirty(tooltip);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(target.scene);
    }
}
```

5. Closed tooltips are pooled (`Tooltip.MaxPoolSize`, default 3, shared). Don't keep `Instance` after it closes.

**Expected result:** in Play Mode, hovering the target shows the tooltip after 0.3 s; `tooltip.IsVisible` is true while shown.

### Workflow: Context menus

**Goal:** Open a menu of actions on right-click (or another button).

**Steps:**
1. Add **Evo > UI > UI Elements > Context Menu** to the target (needs a raycast target, or a Collider plus **Is 3D Object** for scene objects).
2. Assign `menuPreset` to `Assets/Evo/Evo UI/Prefabs/Overlays/Context Menu.prefab` (a prefab with `ContextMenuPreset` on its root).
3. Fill `menuItems` with `ContextMenu.Item` entries: `name`, `icon`, `type` (`ContextMenu.Item.ItemType.Button`, `Separator`, `Submenu`, `CustomObject`), `onClick`, `subItems` for submenus, `buttonPrefab` to give one row its own prefab (e.g. the Negative button row for "Delete"), `customObject` for custom rows.
4. Options: `triggerButton` (`ContextMenu.MouseButton.Right` default; `Left`, `Middle`, `None` disables automatic opening), `usePointerPosition` (default true), `closeOnItemClick`, `closeOnOutsideClick`, `blockUIWhileOpen`. Open from code with `Show()`; close with `Hide()` or `HideImmediate()`.

```csharp
using UnityEngine;
using ContextMenu = Evo.UI.ContextMenu;

public class SlotMenu : MonoBehaviour
{
    [SerializeField] private ContextMenu menu;

    private void Awake()
    {
        menu.triggerButton = ContextMenu.MouseButton.Right;
        menu.menuItems.Clear();

        var use = new ContextMenu.Item { name = "Use" };
        use.onClick.AddListener(() => Debug.Log("Use"));
        menu.menuItems.Add(use);

        menu.menuItems.Add(new ContextMenu.Item { type = ContextMenu.Item.ItemType.Separator });

        var more = new ContextMenu.Item { name = "More", type = ContextMenu.Item.ItemType.Submenu };
        var drop = new ContextMenu.Item { name = "Drop" };
        drop.onClick.AddListener(() => Debug.Log("Drop"));
        more.subItems.Add(drop);
        menu.menuItems.Add(more);
    }
}
```

Item changes take effect the next time the menu opens. `Item.GeneratedObject` exists only while that row is shown.

**Expected result:** right-clicking the target in Play Mode opens a menu with "Use", a separator, and "More > Drop"; `menu.IsVisible` is true while open.

### Workflow: Popover panels

**Goal:** Toggle a small panel open and closed from a button.

**Steps:**
1. Create **GameObject > Evo UI > Overlays > Popover**, or add **Evo > UI > Animation > Popover** to an existing panel.
2. Fields: `manageGameObjectState` (default on, activates and deactivates the panel), `closeOnOutsideClick` (default on), `closeOnEscapeKey` (default on), `openAnimation` / `closeAnimation` (Evo Tween sequences).
3. Wire the trigger button's `onClick` to `Toggle()`; `Open()`, `Close()`, and `SetStateImmediate(bool)` are also available; `onStateChanged` reports `true` / `false`.
4. Keep the controller script and the trigger button **outside** the popover panel. With `manageGameObjectState`, closing deactivates the panel and everything in it.

**Expected result:** clicking the trigger opens the panel; clicking outside or pressing Escape closes it; `popover.isOpen` follows.

### Workflow: Custom-looking overlays

**Goal:** Restyle an overlay without editing package files.

**Steps:**
1. Copy the template (for example `Tooltip Preset.prefab` or `Context Menu.prefab` and its row prefabs) into the user's folder, or make prefab variants.
2. Edit it in Prefab Mode. Keep the references on the root component assigned: Tooltip Preset needs Title Text, Description Text, Icon Image; Context Menu Preset needs Item Container, Button Prefab, Separator Prefab, and Submenu Item Prefab (and each submenu row's Submenu Prefab).
3. Assign the copy to the target's `tooltipPreset` or `menuPreset`.
4. Colors and fonts usually come from Styler; see `evo-ui-styler-themes`. Open and close motion is in each component's **Open Animation** / **Close Animation** sequences; see `evo-tween-sequence-presets`.

**Expected result:** the overlay uses the new look, and the files under `Assets/Evo/Evo UI` are unchanged.

## Verification

- Modal: `GetComponent<Evo.UI.ModalWindow>()` exists, `Title`/`Description` read back, the GameObject is inactive (or Start Behavior is `Disabled`) when it should start hidden, and `IsOpen` becomes true after `Open()` in Play Mode.
- Toast: `Notification.Create(...)` returns non-null, a child appears under the container, and it is destroyed after `autoCloseDelay` plus the close animation.
- Tooltip: `tooltipPreset` is assigned (or `customContent`), `Title` is set, and the target has a raycast target Graphic. In Play Mode, `IsVisible` is true while hovering.
- Context Menu: `menuPreset` is assigned, `menuItems.Count` is as expected, and `GetItem("Use") != null`.
- Popover: `isOpen` toggles with `Toggle()`; the controller is not a child of the popover.
- No `NullReferenceException` or "preset" errors in the Console.

## API quick reference

| Entry point | Type | What it does |
| --- | --- | --- |
| `Evo.UI.ModalWindow` | Component | `Title`, `Description`, `Icon`, `Open()`, `Close()`, `onConfirm`, `onCancel`, `closeOnConfirm`, `closeBehavior`, `navigationMode`. |
| `Evo.UI.Notification` | Component | `Create(preset, icon, title, description, parent, openAfter = true)` (static), `SetContent`, `Open()`, `Close()`, `ForceClose()`, `autoClose`, `autoCloseDelay`. |
| `Evo.UI.Tooltip` | Component | `SetContent(title, description, icon = null)`, `tooltipPreset`, `customContent`, `showDelay`, `Target`, `Show()`, `Hide()`, `IsVisible`. |
| `Evo.UI.ContextMenu` | Component | `menuItems` (`ContextMenu.Item`), `menuPreset`, `triggerButton`, `Show()`, `Hide()`, `GetItem(name)`. |
| `Evo.UI.Popover` | Component | `Toggle()`, `Open()`, `Close()`, `SetStateImmediate(bool)`, `onStateChanged`. |
| `GameObject > Evo UI > Overlays > ...` | Menu | Toast Notification, Modal Window, Modal Window (Alt), Popover. Tooltip and Context Menu are components added to a target. |

## Common issues

| Symptom | Cause | Fix |
| --- | --- | --- |
| The modal shows as soon as the scene starts. | Start Behavior is `Open` (default). | Set Start Behavior to `Disabled`, or leave the GameObject inactive; call `Open()` when needed. |
| `Open()` does nothing. | The modal's parent is inactive, or it is already open. | Activate the parent hierarchy; check `IsOpen`. |
| "[Notification] A notification preset object is required." / "A parent transform must be specified." | `Create` got a null prefab or parent. | Pass a prefab asset with `Notification` on its root and a parent under a Canvas. |
| A scene toast never appears. | `playOnEnable` is off in the supplied prefab. | Call `Open()`, or turn on `playOnEnable`. |
| A tooltip or context menu never opens. | The target has no raycast target, no EventSystem, or (3D) no Collider and **Is 3D Object** off. | Add a raycast target Graphic or Raycast Area, check the EventSystem, set up the Collider. |
| A context menu throws on open. | `menuPreset` isn't assigned or lacks `ContextMenuPreset`. | Assign `Prefabs/Overlays/Context Menu.prefab` or a valid copy. |
| CS0104 'ContextMenu' is ambiguous. | `UnityEngine.ContextMenu` attribute clashes. | `using ContextMenu = Evo.UI.ContextMenu;`. |
| The popover's own button stops working after closing. | The trigger or controller sits inside the popover panel, which was deactivated. | Move them outside the panel. |

## Boundaries

- These are uGUI overlays; they render on a Canvas. Tooltips and context menus on 3D objects need a Collider and a Canvas to draw on (`targetCanvas` when the automatic choice doesn't fit).
- Modal Window doesn't queue multiple dialogs; manage a queue in game code if several can be requested at once.
- Notification stacking coordinates toasts that share a display area; it isn't a full message queue with priorities.
- Tooltip custom content isn't filled in automatically; its own scripts set its text (they get `OnEnable`/`OnDisable` each time, `Awake`/`Start` once).
- Blur behind overlays needs Backdrop Blur (`evo-ui-effects`), which samples camera output and excludes Screen Space - Overlay UI.
