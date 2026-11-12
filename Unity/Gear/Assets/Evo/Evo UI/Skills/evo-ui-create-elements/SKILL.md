---
name: evo-ui-create-elements
description: "Use this skill whenever the user wants to add or wire up standard UI controls with Evo UI — e.g. 'add a play button', 'make a settings menu with a volume slider and a fullscreen toggle', 'dropdown with low/medium/high quality', 'add a name input field', 'switch for music', 'change the button text and icon', 'hook up the button click'. Covers creating any element from GameObject > Evo UI or the UI Catalog, how the Canvas and parent are chosen, setting content (text, icons, ranges, items), and connecting events at runtime or as saved Inspector listeners for Button, Toggle, Switch, Slider, Dropdown, Input Field, Selector, Radio Button Group, and Quantity Selector. Do NOT use for popups (see evo-ui-overlays), charts and lists (see evo-ui-charts-and-data), pages, tabs, and scroll views (see evo-ui-layout-and-navigation), or themes (see evo-ui-styler-themes). When in doubt whether a UI control request could involve Evo UI, use this skill — Prerequisites shows how to confirm the asset is installed."
metadata:
  asset: "Evo UI"
  publisher: "Michsky"
  asset-version: "2.0.0"
  skill-version: "1.0.1"
  unity: "6000.0+"
  render-pipelines: "Built-in, URP, HDRP"
  category: "tools/gui"
  asset-store-url: "https://assetstore.unity.com/packages/slug/410788"
  documentation-url: "https://evo.michsky.com/docs/evo-ui"
  support-url: "https://www.michsky.com/contact"
  last-verified: "2026-11-03"
---

# Create UI Elements with Evo UI

This skill lets an agent add Evo UI controls to a scene or prefab, set their content, and connect them to game code. Every element comes from a prefab template through **GameObject > Evo UI**, so the result is fully wired (child references, Styler mappings, animations) without manual assembly.

## When to use this skill

- "add a play button to the main menu"
- "make a settings panel with a volume slider, a fullscreen toggle and a quality dropdown"
- "add an input field for the player name"
- "i need an on/off switch for music"
- "add radio buttons for easy / normal / hard"
- "change the button label to Continue and give it an arrow icon"
- "make the button call StartGame when clicked"
- "add a color picker / calendar / timer / spinner / progress button"

Not for:
- Modal windows, toast notifications, tooltips, context menus, popovers — see `evo-ui-overlays`.
- Charts, List View, Counter, progress bars — see `evo-ui-charts-and-data`.
- Page Manager, Tab Manager, Stepper, scroll views, Carousel, Menu Bar, gamepad navigation — see `evo-ui-layout-and-navigation`.
- Colors, fonts, light/dark themes — see `evo-ui-styler-themes`. Corner radius, outlines, shadows — see `evo-ui-procedural-shape`.
- First-time project setup and EventSystem problems — see `evo-ui-setup-and-overview`.

## Prerequisites

- Evo UI 2.0.0+ on Unity 6000.0+, any render pipeline, Input System active.
- **Install check:** `System.Type.GetType("Evo.UI.Button, Evo.UI") != null`, or confirm the menu **GameObject > Evo UI > Open Catalog...** exists. If it fails, tell the user Evo UI isn't installed and link https://assetstore.unity.com/packages/slug/410788.
- TMP Essential Resources imported (`Assets/TextMesh Pro/` exists) when the text provider is TextMesh Pro. Otherwise labels don't render.
- Run creation code in Edit Mode, outside Play Mode.

**Name clashes.** `Evo.UI` defines `Button`, `Toggle`, `Slider`, `Scrollbar`, `ScrollRect`, and `Dropdown`, which also exist in `UnityEngine.UI`, and `ContextMenu`, which also exists in `UnityEngine`. A file with both `using UnityEngine.UI;` and `using Evo.UI;` fails to compile with CS0104 (ambiguous reference). Write `Evo.UI.Button` in full, or add an alias such as `using Button = Evo.UI.Button;`.

## Quick start

Add a labeled button with an icon and confirm it exists.

1. Check that the open scene is the one the user means (`SceneManager.GetActiveScene().path`). The user can switch scenes while you work, so check again right before creating and before saving.
2. Select the parent object in the Hierarchy (a Canvas or any object under one). With nothing selected, Evo UI uses the first Canvas it finds, or creates `Canvas` + `Event System`.
3. Run **GameObject > Evo UI > Buttons > Button**. The new object is named `Button` and becomes the selection.
4. Set the label and icon, and save the scene:

```csharp
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class EvoCreatePlayButton
{
    public static void Run(string scenePath)
    {
        // Stop if the user switched scenes; FindAnyObjectByType searches every loaded scene.
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
            throw new System.InvalidOperationException("The open scene changed; ask the user which scene to use.");

        var canvas = Object.FindAnyObjectByType<Canvas>();
        Selection.activeGameObject = canvas != null ? canvas.gameObject : null;
        EditorApplication.ExecuteMenuItem("GameObject/Evo UI/Buttons/Button");

        GameObject go = Selection.activeGameObject;
        var button = go.GetComponent<Evo.UI.Button>();

        // Record the whole hierarchy: the setters also edit child text and image objects.
        Undo.RegisterFullObjectHierarchyUndo(go, "Configure Play Button");
        go.name = "Play Button";
        button.Text = "Play";
        button.Icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Evo/Evo UI/Sprites/Icons/Media/Play.png");

        EditorSceneManager.MarkSceneDirty(go.scene);
        EditorSceneManager.SaveScene(go.scene);
    }
}
```

Expected result: the Hierarchy shows `Canvas > Play Button` with an `Evo.UI.Button` component whose `Text` is `"Play"`. The icon is visible if the sprite path exists (check it first: icon file names vary per category, see *Assign icons*).

## Workflows

### Workflow: Create any element from the Create menu

**Goal:** Instantiate an Evo UI element at the right place in the hierarchy.

**Steps:**
1. Choose the parent: set `Selection.activeGameObject` to a scene object under a Canvas. Creation rules, in order:
   - The selected scene object becomes the parent (even a non-UI object, which leaves the element outside any Canvas and invisible, so always select a UI parent).
   - Otherwise, in Prefab Mode, the open prefab's root.
   - Otherwise, the first Canvas in the loaded scenes.
   - Otherwise, a new `Canvas` (Screen Space - Overlay, 2560 x 1440 reference resolution, match 0.5) and, if none exists, an `Event System` with `InputSystemUIInputModule`.
2. Run the menu item with `EditorApplication.ExecuteMenuItem("GameObject/Evo UI/<path>")`, using a path from the table below.
3. Read the created object from `Selection.activeGameObject`. It is an unpacked copy of the template (not a prefab instance), placed at local position 0 and registered with Undo.
4. When the active text provider is not TextMesh Pro, the copy's text is converted to that provider automatically.

Menu paths (all under `GameObject/Evo UI/`) and the main component on the created root:

| Category | Menu items | Root component (`Evo.UI`) |
| --- | --- | --- |
| Essentials | `Procedural Shape`, `Image`, `Text` | `ProceduralShape`; Unity `Image` + `StylerObject`; TMP text + `StylerObject` |
| Buttons | `Buttons/Button`, `Buttons/Button (Outline)`, `Buttons/Button (Pill)`, `Buttons/Button (Pill Outline)`, `Buttons/Button (Icon Only)`, `Buttons/Button (Magnetic Sway)` | `Button` |
| Buttons | `Buttons/Progress Button`, `Buttons/Progress Button (Alt)` | `ProgressButton` |
| Color | `Color/Color Picker (Compact)`, `(Square)`, `(Wheel)`, `(Palette)` | `ColorPicker`; `PaletteColorPicker` for Palette |
| Date and Time | `Date and Time/Calendar`, `Date and Time/Time Picker`, `Date and Time/Timer`, `Timer (Horizontal)`, `Timer (Vertical)`, `Timer (Radial)` | `Calendar`, `TimePicker`, `Timer` |
| Progress and Loading | `Progress and Loading/Spinner (Horizontal)`, `(Vertical)`, `(Radial)` | `Spinner` |
| Selection | `Selection/Dropdown`, `Selection/Dropdown (Input)`, `Selection/Dropdown (Multi Select)` | `Dropdown`; `Dropdown` + `DropdownInput`; `DropdownMultiSelect` |
| Selection | `Selection/Selector`, `Selection/Quantity Selector` | `Selector`, `QuantitySelector` |
| Sliders | `Sliders/Slider`, `Sliders/Slider (Alt)`, `Sliders/Slider (Input)` | `Slider` |
| Sliders | `Sliders/Radial Slider`, `Sliders/Radial Slider (Knob)`, `Sliders/Range Slider`, `Sliders/Comparison Slider` | `RadialSlider`, `RangeSlider`, `ComparisonSlider` |
| Text Input | `Text Input/Input Field`, `(Icon)`, `(Minimal)`, `(Multi Line)`, `(Multi Line Scrollbar)` | `InputFieldEnhancer` + `Interactive` |
| Toggles and Switches | `Toggles and Switches/Toggle` | `Toggle` |
| Toggles and Switches | `Toggles and Switches/Switch`, `Switch (Indicator)`, `Switch (Label)`, `Switch (Indicator + Label)` | `Switch` |
| Toggles and Switches | `Toggles and Switches/Radio Button`, `Radio Button (Alt)` | `Button` |
| Toggles and Switches | `Toggles and Switches/Radio Button Group`, `Radio Button Group (Alt)` | `RadioButtonGroup` |

Write variants in full, e.g. `GameObject/Evo UI/Toggles and Switches/Switch (Label)`. Charts, List View, Counter, progress bars, overlays, and layout items are covered by their own skills. **GameObject > Evo UI > Open Catalog...** opens the UI Catalog, where a human can browse previews and set **Placement** (next to the selection, inside it, or at the Canvas root).

Use the Create menu rather than **Add Component** for composite controls such as Dropdown. A component added by hand has none of its child references assigned.

**Expected result:** a new object named after the template (for example `Switch (Label)`) under the chosen parent, with the root component listed above.

### Workflow: Set content from editor code

**Goal:** Configure the created controls before saving the scene.

**Steps:**
1. Call `Undo.RegisterFullObjectHierarchyUndo(root, "...")` first. Setters such as `Button.Text` also change child objects.
2. Use the properties and methods below.
3. Call `EditorSceneManager.MarkSceneDirty(root.scene)`, then save. In Prefab Mode, the prefab stage saves the change.

| Control | Content API |
| --- | --- |
| `Button` | `Text` (empty hides the label), `Icon` (`null` hides the icon), `EnableText`, `EnableIcon`, `TextSize`, `IconSize`, `Padding` (`RectOffset`), `Spacing`, `ReverseArrangement`, `DynamicScale` (turn off when a layout group sizes the button), `AllowDoubleClick`. |
| `Toggle` | `IsOn`, `SetValue(bool value, bool invokeEvents = true)`, `SetValueWithoutNotify(bool)`, `ToggleValue()`. |
| `Switch` | `IsOn`, `SetValue(bool)`, `SetValue(bool value, bool invokeEvents)`, `Toggle()`, `handleDuration`. |
| `Slider` (extends `UnityEngine.UI.Slider`) | `minValue`, `maxValue`, `wholeNumbers`, `value`, `SetValueWithoutNotify(float)`, `displayMultiplier`, `textFormat` (must contain `{0}`), `displayFormat` (`NumberDisplayFormat`). |
| `Dropdown` | `ClearAllItems()`, `AddItems(params string[])`, `AddItem(string label, Sprite icon = null)`, `RemoveItem(int)`, `SelectItem(int index, bool invokeEvents)`, `SelectItem(string label)`, `SelectedIndex`, `items` (call `RefreshItems()` after editing the list directly), `maxHeight` (default 240), `itemHeight` (default 40). |
| `Selector` | `ClearItems()`, `AddItems(params string[])`, `AddItem(string text, Sprite icon = null)`, `SetSelection(int index, bool animated, bool invokeEvents = true)`, `loop`, `items` (call `Refresh()` after direct edits). |
| `QuantitySelector` | `minQuantity` (default 1), `maxQuantity` (default 99), `SetQuantity(int value, bool animate = true)`, `CurrentQuantity`. |
| `RadioButtonGroup` | Each **direct child** with a `Button` is one option, indexed by child order. Duplicate a child to add an option, then call `Initialize()`. `SetButton(int index, bool animate, bool invokeEvents = true)`, `selectedIndex` (-1 = none), `allowDeselection`. |
| `InputFieldEnhancer` | `source` is the provider's input field (`TMP_InputField` for TextMesh Pro). Use `Evo.UI.TextProvider.SetValue(field.source, "text", notify: false)` and `TextProvider.GetValue(field.source)`. For the placeholder or content type, cast: `(TMPro.TMP_InputField)field.source`. |

Example: build a settings group under a selected panel.

```csharp
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class EvoBuildSettings
{
    static GameObject Create(string menuPath, Transform parent, string name)
    {
        Selection.activeGameObject = parent.gameObject;
        EditorApplication.ExecuteMenuItem("GameObject/Evo UI/" + menuPath);
        GameObject go = Selection.activeGameObject;
        Undo.RegisterFullObjectHierarchyUndo(go, "Configure " + name);
        go.name = name;
        return go;
    }

    public static void Run()
    {
        Transform panel = Selection.activeTransform; // A UI object under a Canvas.

        var volume = Create("Sliders/Slider", panel, "Volume Slider").GetComponent<Evo.UI.Slider>();
        volume.minValue = 0f;
        volume.maxValue = 1f;
        volume.wholeNumbers = false;
        volume.displayMultiplier = 100f;
        volume.textFormat = "{0}%";
        volume.SetValueWithoutNotify(0.8f);

        var fullscreen = Create("Toggles and Switches/Switch (Label)", panel, "Fullscreen Switch").GetComponent<Evo.UI.Switch>();
        fullscreen.SetValue(true, false);

        var quality = Create("Selection/Dropdown", panel, "Quality Dropdown").GetComponent<Evo.UI.Dropdown>();
        quality.ClearAllItems();
        quality.AddItems("Low", "Medium", "High");
        quality.SelectItem(2, false);

        EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
    }
}
```

The objects are stacked at the parent's center. Add a `VerticalLayoutGroup` to the parent, or set each `RectTransform.anchoredPosition`, to lay them out. A Button sizes itself around its content during a layout pass, so its `rect` can read 0 after creation, even in a later call while the Editor is in the background. Before positioning from sizes, call `button.UpdateLayout()` and `LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform)`; a layout group avoids the problem.

**Expected result:** three new children under the panel with the values above. `quality.SelectedIndex == 2` and `quality.items.Count == 3`.

### Workflow: Connect events

**Goal:** Make controls call game code.

**Steps:**
1. Pick the event:

| Control | Event | Payload |
| --- | --- | --- |
| `Button` (and any `Interactive`) | `onClick` (click or submit), `onDoubleClick` (needs `AllowDoubleClick`), `onPointerEnter`, `onPointerExit`, `onPointerDown`, `onPointerUp`, `onSelect`, `onDeselect`, `onSubmit` | none |
| `Toggle`, `Switch` | `onValueChanged` | `bool` |
| `Slider` | `onValueChanged` (inherited from Unity) | `float` (stored value, not the display multiplier) |
| `Dropdown` | `onItemSelected` | `int` source index; also `onOpen`, `onClose` |
| `DropdownMultiSelect` | `onItemToggled` (`int`, `bool`), `onSelectionChanged` (`List<int>`) | |
| `Selector`, `RadioButtonGroup` | `onSelectionChanged` | `int` |
| `QuantitySelector` | `onValueChanged` | `int` |
| `InputFieldEnhancer` | `onSubmit` | `string` |

2. **At runtime** (in a MonoBehaviour), use `AddListener` and remove it in `OnDestroy`:

```csharp
using UnityEngine;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private Evo.UI.Button playButton;
    [SerializeField] private Evo.UI.Switch musicSwitch;

    private void Start()
    {
        playButton.onClick.AddListener(StartGame);
        musicSwitch.onValueChanged.AddListener(SetMusic);
    }

    private void OnDestroy()
    {
        if (playButton != null)
            playButton.onClick.RemoveListener(StartGame);
        if (musicSwitch != null)
            musicSwitch.onValueChanged.RemoveListener(SetMusic);
    }

    public void StartGame() => Debug.Log("Start the game here.");
    public void SetMusic(bool enabled) => Debug.Log($"Music: {enabled}");
}
```

3. **In the Editor** (saved with the scene, visible in the Inspector), `AddListener` is not serialized. Add persistent listeners instead:

```csharp
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class EvoWireMenu
{
    public static void Run(Evo.UI.Button playButton, Evo.UI.Switch musicSwitch, MainMenu menu)
    {
        Undo.RecordObjects(new Object[] { playButton, musicSwitch }, "Wire Menu Events");
        UnityEventTools.AddPersistentListener(playButton.onClick, menu.StartGame);
        UnityEventTools.AddPersistentListener(musicSwitch.onValueChanged, menu.SetMusic);
        EditorSceneManager.MarkSceneDirty(playButton.gameObject.scene);
    }
}
```

The target method must be public on a `UnityEngine.Object` (a component in the scene) and match the event's payload type.

**Expected result:** `playButton.onClick.GetPersistentEventCount() == 1` and `GetPersistentMethodName(0) == "StartGame"`. In Play Mode, clicking the button logs the message.

### Workflow: Assign icons

**Goal:** Use one of the bundled icons on a Button, Image, or Procedural Shape.

**Steps:**
1. Find the sprite. Icons live in `Assets/Evo/Evo UI/Sprites/Icons/<Category>/<Name>.png` (categories include Arrows, Interface, Media, System, Status, Gaming, and more). Search instead of guessing the name: `AssetDatabase.FindAssets("Gear t:Sprite", new[] { "Assets/Evo/Evo UI/Sprites/Icons" })`.
2. Load it with `AssetDatabase.LoadAssetAtPath<Sprite>(path)` and assign `button.Icon = sprite`, `image.sprite = sprite`, or `shape.Sprite = sprite`.
3. A human can do the same with **Tools > Evo UI > Icon Browser**, which also picks a resolution.

**Expected result:** the sprite field is not null and the icon object is active (`button.EnableIcon == true`).

## Verification

- The created object exists under the intended parent and has the root component from the table (`go.GetComponent<Evo.UI.Switch>() != null`).
- The parent chain contains a `Canvas` (`go.GetComponentInParent<Canvas>() != null`).
- Values read back as set: `button.Text`, `slider.minValue`/`maxValue`, `dropdown.items.Count`, `dropdown.SelectedIndex`, `toggle.IsOn`.
- Editor-wired events: `GetPersistentEventCount()` and `GetPersistentMethodName(i)` on the UnityEvent.
- The scene is saved (`!go.scene.isDirty` after `EditorSceneManager.SaveScene`), and the Console has no errors or CS0104 ambiguity errors.

## API quick reference

| Entry point | Type | What it does |
| --- | --- | --- |
| `EditorApplication.ExecuteMenuItem("GameObject/Evo UI/...")` | Editor API | Creates an element from its template under the selection. |
| `Evo.UI.Button` | Component | Label, icon, layout, `onClick`. Inherits `Interactive`. |
| `Evo.UI.Interactive` | Component | Base of Evo UI controls: pointer events, `SetInteractable(bool)`, `SetState(InteractionState, bool instant = false)`, `scaleEffect`, `ripple`, `spotlight`, `borderGlow`. |
| `Evo.UI.Toggle` / `Evo.UI.Switch` | Component | Boolean controls with `IsOn` and `onValueChanged`. |
| `Evo.UI.Slider` | Component | Unity Slider with formatted label (`displayMultiplier`, `textFormat`). |
| `Evo.UI.Dropdown` | Component | Item list, selection, filtering, `onItemSelected`. |
| `Evo.UI.Selector`, `Evo.UI.RadioButtonGroup`, `Evo.UI.QuantitySelector` | Component | Choice and number pickers. |
| `Evo.UI.InputFieldEnhancer` | Component | Wraps the provider input field; `onSubmit`, `Focus()`. |
| `Evo.UI.TextProvider` | Static class | Provider-neutral `GetText`/`SetText` for text and `GetValue`/`SetValue` for input fields. |
| `UnityEditor.Events.UnityEventTools.AddPersistentListener` | Editor API | Saves an event listener with the scene. |

Full per-component reference: https://evo.michsky.com/docs/evo-ui (UI Elements section). Each component's **Help** icon in the Inspector opens its page.

## Common issues

| Symptom | Cause | Fix |
| --- | --- | --- |
| CS0104 "'Button' is an ambiguous reference between 'UnityEngine.UI.Button' and 'Evo.UI.Button'". | Both namespaces are imported. | Use `Evo.UI.Button` in full or `using Button = Evo.UI.Button;`. Same for Toggle, Slider, Scrollbar, ScrollRect, Dropdown, and `ContextMenu`. |
| The element is created but not visible. | It was parented to a non-UI object, or to a Canvas the camera doesn't render. | Select a UI parent before creating; check `GetComponentInParent<Canvas>()`. |
| Elements ended up in a different scene than intended. | The user switched scenes between your calls, and creation used the open scene. | Check `SceneManager.GetActiveScene().path` before creating and before saving; ask if it changed. |
| Buttons positioned by code overlap. | Their size was read in the same call that created them, before the layout pass (`rect` reads 0). | Use a layout group, or call `LayoutRebuilder.ForceRebuildLayoutImmediate` before reading sizes. |
| The label or icon change is lost after reopening the scene. | Edits were made without marking the scene dirty, or only the root was recorded. | `Undo.RegisterFullObjectHierarchyUndo(root, ...)`, `MarkSceneDirty`, then save. |
| A listener added in the Editor does nothing in Play Mode. | `AddListener` in Edit Mode is not serialized. | Use `UnityEventTools.AddPersistentListener`, or add the listener at runtime in `Start`. |
| A button inside a layout group won't resize. | **Dynamic Scale** sizes the button around its content. | Set `button.DynamicScale = false` when the parent layout owns the size. |
| The dropdown list shows no items after editing `items` directly. | Generated rows weren't rebuilt. | Call `RefreshItems()`, or use `AddItem`/`AddItems`. |
| A Dropdown added with Add Component throws errors or shows nothing. | Its header, scroll rect, item parent, and item prefab references are empty. | Delete it and create it from **GameObject > Evo UI > Selection > Dropdown**. |
| Color changes on a control revert. | Styler re-applies the preset colors. | See `evo-ui-styler-themes` (local overrides, Style Provider Custom). |

## Boundaries

- Creation needs the Editor; at runtime, instantiate your own prefab (`Object.Instantiate`) made from an Evo UI element.
- Created elements are unpacked copies. Editing a template under `Assets/Evo/Evo UI/Prefabs` changes future creations and is overwritten by package updates; copy the prefab into the user's folder for a reusable custom version.
- Evo UI controls are uGUI components; they do not work inside UI Toolkit documents.
- Visual state (Styler) and input are separate: a disabled look does not block input. Use `SetInteractable(false)` to stop a control from responding.
- For control animation beyond the built-in states, use Evo Tween (`evo-tween-tween-animator`).
