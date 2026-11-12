---
name: evo-ui-setup-and-overview
description: "Use this skill whenever the user wants to install, set up, check, or find their way around Evo UI, the uGUI component library by Michsky — e.g. 'is evo ui installed', 'set up evo ui in my project', 'my evo ui buttons don't react to clicks', 'where are the evo ui prefabs', 'switch evo ui to UniText', 'text is missing on evo ui elements', 'open the evo ui demo'. Covers the install check, required packages (uGUI, Input System, TextMesh Pro), the EventSystem and input module, the text provider setting, the demo scene, the editor tools, and where files live. Do NOT use for creating specific controls (see evo-ui-create-elements), theming (see evo-ui-styler-themes), shape styling (see evo-ui-procedural-shape), or animation (see evo-tween-setup-and-overview). When in doubt whether a Unity UI request could involve Evo UI, use this skill — Prerequisites shows how to confirm the asset is installed."
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
  last-verified: "2026-11-03"
---

# Set Up Evo UI

Evo UI is a library of ready-made uGUI controls (buttons, sliders, dropdowns, charts, modal windows, and more) drawn with its own Procedural Shape renderer, themed through Styler presets, and animated with Evo Tween. This skill confirms the package is installed and working, prepares a scene for Evo UI input, picks the text provider, and shows where everything lives so the other Evo UI skills can be used.

## When to use this skill

- "is evo ui installed in this project?"
- "set up evo ui so i can start building menus"
- "my evo ui buttons don't respond when i click them"
- "the text on the evo ui prefabs is invisible / pink"
- "switch evo ui to UniText" or "convert my text to UniText"
- "where are the evo ui prefabs / icons / presets?"
- "open the evo ui demo scene"
- Any first request in a project where Evo UI has not been used yet.

Not for:
- Creating and configuring individual controls — see `evo-ui-create-elements`.
- Colors, fonts, and themes — see `evo-ui-styler-themes`.
- Corners, outlines, shadows, gradients — see `evo-ui-procedural-shape`.
- Upgrading an Evo UI v1 project — v2 cannot be imported over v1; follow the migration guide at https://evo.michsky.com/docs/evo-ui/migration.

## Prerequisites

- Evo UI 2.0.0 or newer (check the first line of `Assets/Evo/Evo UI/README.md`, which reads `## Evo UI 2.0.0`).
- Unity 6.0 (6000.0) or newer. Unity 2022.3 and earlier are not supported.
- Built-in, URP, or HDRP. All three are supported.
- Packages: Unity UI (`com.unity.ugui`), Input System (`com.unity.inputsystem`), and TextMesh Pro (part of `com.unity.ugui` in Unity 6). Evo Tween ships inside the same package at `Assets/Evo/Evo Tween` and is required.
- Active Input Handling set to **Input System Package (New)** or **Both**. The old Input Manager alone is not supported.

**Install check (run before anything else).** Run this in the Editor, for example from a temporary Editor script:

```csharp
using UnityEngine;

public static class EvoUIInstallCheck
{
    public static void Run()
    {
        bool evoUI = System.Type.GetType("Evo.UI.ProceduralShape, Evo.UI") != null;
        bool evoTween = System.Type.GetType("Evo.Tween.EvoTween, Evo.Tween") != null;
#if ENABLE_INPUT_SYSTEM
        bool inputSystem = true;
#else
        bool inputSystem = false;
#endif
        Debug.Log($"Evo UI: {evoUI}, Evo Tween: {evoTween}, Input System active: {inputSystem}");
    }
}
```

You can also confirm that the menu **Tools > Evo UI > UI Catalog** exists, or that the folder `Assets/Evo/Evo UI` exists. The type check is the most reliable because users can move the folder.

If `Evo UI` is false, stop and tell the user: "Evo UI isn't installed in this project. Import it from the Asset Store (https://assetstore.unity.com/packages/slug/410788) through **Window > Package Management > Package Manager > My Assets**." If `Input System active` is false, tell the user to set **Edit > Project Settings > Player > Other Settings > Active Input Handling** to **Input System Package (New)** or **Both**. Unity restarts the Editor to apply it, so ask before you change it.

## Quick start

Prepare an empty scene for Evo UI and confirm everything renders.

1. Open or create a scene (**File > New Scene > Basic (Built-in)**, or the template for your pipeline).
2. Run **GameObject > Evo UI > Buttons > Button**. If the scene has no Canvas, Evo UI creates one named `Canvas` (Screen Space - Overlay, Canvas Scaler set to Scale With Screen Size at 2560 x 1440, match 0.5, plus a Graphic Raycaster) and an `Event System` with `InputSystemUIInputModule`.
3. If Unity asks to import **TMP Essential Resources**, accept, or run **Window > TextMeshPro > Import TMP Essential Resources**. Without them, Evo UI text does not render and the UI Catalog cannot draw previews.
4. Enter Play Mode and click the button. It reacts to hover and press.

The same steps as editor code:

```csharp
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

public static class EvoUIQuickStart
{
    public static void Run()
    {
        Selection.activeGameObject = null; // Let Evo UI choose or create the Canvas.
        EditorApplication.ExecuteMenuItem("GameObject/Evo UI/Buttons/Button");

        GameObject button = Selection.activeGameObject;
        Debug.Log(button != null ? $"Created {button.name} under {button.transform.parent.name}" : "Creation failed");

        var eventSystem = Object.FindAnyObjectByType<EventSystem>();
        bool inputModule = eventSystem != null
            && eventSystem.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() != null;
        Debug.Log($"EventSystem with InputSystemUIInputModule: {inputModule}");
    }
}
```

Expected result: the Hierarchy contains `Canvas > Button` and `Event System`, the button shows the label "Button", and the Console has no errors.

## Workflows

### Workflow: Fix the EventSystem for an existing scene

**Goal:** Make Evo UI controls react to mouse, touch, keyboard, and gamepad in a scene that already has UI.

**Steps:**
1. Find the scene's `EventSystem`. Evo UI only creates one when it also creates the Canvas, so a scene with an existing Canvas may have none.
2. If there is no EventSystem, run **GameObject > UI > Event System** (**GameObject > UI (Canvas) > Event System** from Unity 6.6).
3. If the EventSystem has `StandaloneInputModule`, replace it with `InputSystemUIInputModule` (the Inspector shows a **Replace with InputSystemUIInputModule** button). Run the code below in the open Editor (for example through your agent's Unity command tool). Don't edit the `.unity` file directly while the scene is open: the Editor keeps working on its own copy in memory and overwrites the edit the next time the scene is saved.

```csharp
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

public static class EvoUIFixEventSystem
{
    public static void Run()
    {
        var eventSystem = Object.FindAnyObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            var go = new GameObject("Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Undo.RegisterCreatedObjectUndo(go, "Create Event System");
        }
        else if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
        {
            var old = eventSystem.GetComponent<StandaloneInputModule>();
            if (old != null)
                Undo.DestroyObjectImmediate(old);

            Undo.AddComponent<InputSystemUIInputModule>(eventSystem.gameObject);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
    }
}
```

4. Check that every Canvas showing Evo UI has a `GraphicRaycaster`.

**Expected result:** exactly one EventSystem with `InputSystemUIInputModule`, and the controls react in Play Mode.

### Workflow: Choose the text provider

**Goal:** Use TextMesh Pro (default) or UniText for every Evo UI text, input field, and font.

**Steps:**
1. For UniText, install its package (`media.lightside.unitext`, version 3.40 or newer) first. A scripting define alone does not enable it.
2. Open **Edit > Project Settings > Evo UI** and choose a provider under **Text Provider**.
3. Existing text keeps its provider. To convert it, use **Convert Existing Text** on the same page: pick **Open Scenes** and/or **Prefabs** (with a **Folder**), optionally **Include All Text**, then select **Convert**. Leave **Include Evo UI Prefabs** off; the Create menu already converts new objects, and package updates replace those templates.
4. Ask the user to commit or back up first. Converted prefabs are saved immediately and cannot be undone.
5. Assign a font for the new provider on each Font entry of the Styler Presets in use (see `evo-ui-styler-themes`).

**Expected result:** `ProjectSettings/Evo/EvoUISettings.json` contains `"textProvider": "TMP"` or `"textProvider": "UniText"`, and new elements from **GameObject > Evo UI** use that provider. The conversion result is shown under the **Convert** button and skipped items are listed in the Console.

### Workflow: Explore the package and the demo

**Goal:** Show the user what is available, or find a file.

**Steps:**
1. Open the demo scene `Assets/Evo/Evo UI/Demo/Evo UI Demo.unity` and enter Play Mode to browse every component.
2. Open **Tools > Evo UI > UI Catalog** (also **GameObject > Evo UI > Open Catalog...**) to browse all creatable elements with previews, search by keyword, and create one by double-click, **Enter**, **Create**, or drag and drop.
3. Use the file map below to locate assets.

| Path | Contents |
| --- | --- |
| `Assets/Evo/Evo UI/Prefabs/<Category>/` | Templates used by **GameObject > Evo UI** (Buttons, Color, Data Display, Date and Time, Essentials, Layout and Navigation, Overlays, Progress and Loading, Selection, Sliders, Text Input, Toggles and Switches). |
| `Assets/Evo/Evo UI/Resources/Styler Presets/` | `Default.asset` (dark) and `Default (Light).asset` Styler presets. |
| `Assets/Evo/Evo UI/Sprites/Icons/<Category>/` | Icon PNGs, e.g. `Interface/Home.png`, `System/Gear.png`. |
| `Assets/Evo/Evo UI/Scripts/` | Runtime code, namespace `Evo.UI`, assembly `Evo.UI`. |
| `Assets/Evo/Evo UI/Editor/` | Editor tools, namespace `Evo.UI.Editor`. |
| `Assets/Evo/Evo UI/Demo/` | Demo scene, prefabs, and scripts. |
| `Assets/Evo/Evo Tween/` | Evo Tween animation engine used by Evo UI. |
| `Assets/Evo/Shared/` | Editor GUI library shared by Evo products. Do not delete. |

**Expected result:** the user knows where to look, and the Catalog or demo is open.

## Verification

- `System.Type.GetType("Evo.UI.ProceduralShape, Evo.UI") != null` and `System.Type.GetType("Evo.Tween.EvoTween, Evo.Tween") != null`.
- The scripting define `EVO_UI` is present in **Player Settings > Scripting Define Symbols**. Evo UI adds it to every build target automatically; never add or remove it by hand.
- The scene has exactly one `EventSystem` with `InputSystemUIInputModule`, and each UI Canvas has a `GraphicRaycaster`.
- `Assets/TextMesh Pro/` exists (TMP Essential Resources imported) when the provider is TextMesh Pro.
- `ProjectSettings/Evo/EvoUISettings.json` names the intended provider.
- The Console shows no errors after a domain reload and after entering Play Mode.

## API quick reference

| Entry point | Type | What it does |
| --- | --- | --- |
| `GameObject > Evo UI > ...` | Menu | Creates any Evo UI element from its prefab; creates a Canvas and EventSystem if needed. |
| `Tools > Evo UI > UI Catalog` | Menu / window | Browse, search, and create elements with previews. |
| `Tools > Evo UI > Open Default Styler` (Ctrl+Shift+M) | Menu | Selects the default Styler Preset. |
| `Tools > Evo UI > Styler Browser` | Menu / window | Manage and apply Styler Presets. |
| `Tools > Evo UI > Icon Browser` | Menu / window | Search icons and apply them to Image, RawImage, Procedural Shape, or Button. |
| `Tools > Evo UI > Sprite Optimizer` | Menu / window | Bulk-edits texture filtering and mipmap settings for selected textures. |
| `Tools > Evo UI > Pick Visible UI in Scene View` | Menu toggle | Scene view clicks select the top visible graphic instead of empty containers. |
| `Edit > Project Settings > Evo UI` | Settings page | Text Provider selection and Convert Existing Text. |
| `Evo.UI.TextProvider.Active` | Static property | The provider in use (`TextBackend`, `Id` is `"TMP"` or `"UniText"`). |

Other skills:
- `evo-ui-create-elements` — create buttons, toggles, sliders, dropdowns, input fields, and other controls; set their content and events.
- `evo-ui-styler-themes` — shared colors, fonts, and shape styles; light/dark theme switching.
- `evo-ui-procedural-shape` — corners, fills, gradients, outlines, shadows, clipping, and patterns.
- `evo-ui-overlays` — modal windows, toast notifications, tooltips, context menus, and popovers.
- `evo-ui-layout-and-navigation` — page and tab managers, stepper, scroll views and virtualized lists, carousel, keyboard and gamepad navigation.
- `evo-ui-charts-and-data` — bar, line, pie, and radar charts, List View, Counter, progress bars.
- `evo-ui-effects` — Soft Mask, Backdrop Blur, Border Glow, Text Effects, and other visual effects.
- `evo-tween-setup-and-overview` — the Evo Tween animation engine used by Evo UI.

## Common issues

| Symptom | Cause | Fix |
| --- | --- | --- |
| Controls don't react to clicks or keys. | No EventSystem, the EventSystem uses `StandaloneInputModule`, the Canvas has no `GraphicRaycaster`, or a transparent graphic or Raycast Area covers the control. | Follow *Fix the EventSystem*; check the Canvas; check CanvasGroup **Interactable** / **Blocks Raycasts** and modal blockers. |
| Errors mentioning `UnityEngine.Input` or the input module on Play. | Active Input Handling is set to Input Manager (Old). | Set it to **Input System Package (New)** or **Both** (Editor restart). |
| Text is invisible, or the Catalog says "Import TMP Essential Resources to render previews". | TMP Essential Resources are not imported. | **Window > TextMeshPro > Import TMP Essential Resources**. |
| Dialog "Prefab not found at: ... Please check your Evo UI installation." | The `Prefabs` folder was moved, renamed, or deleted. The Create menu looks for it next to the folder holding `Resources/Styler Presets/Default.asset`. | Restore the original layout of `Assets/Evo/Evo UI`, or reimport the package. |
| Hundreds of compile errors after importing. | v2 was imported over Evo UI v1. | Restore the project from version control and follow https://evo.michsky.com/docs/evo-ui/migration. |
| A Scene view click selects a different object than expected. | Evo UI's visible-graphic picking skips empty containers and invisible hit areas. | Click again to cycle, or turn off **Tools > Evo UI > Pick Visible UI in Scene View**. |
| UniText can't be selected. | The UniText package isn't installed; Evo UI falls back to TextMesh Pro. | Install `media.lightside.unitext`, then select it in **Project Settings > Evo UI**. |

## Boundaries

- Evo UI is built on uGUI (Canvas, RectTransform). It does not provide UI Toolkit (UXML/USS) controls; use Unity's UI Toolkit for those.
- The old Input Manager is not supported; Evo UI v2 requires the Input System.
- Backdrop Blur is not compatible with XR/VR (including visionOS).
- Text Effects and Smooth TMP Gradient require TextMesh Pro; they do not work with UniText.
- The Styler Browser, UI Catalog, Icon Browser, and Sprite Optimizer are editor-only. Use the runtime APIs described in the other skills for player builds.
- Keep the user's own presets, prefabs, and sprites outside `Assets/Evo/Evo UI` so package updates don't overwrite them.
- Upgrading from v1 needs the separate Evo UI Migration Tool, which is not part of this package.
