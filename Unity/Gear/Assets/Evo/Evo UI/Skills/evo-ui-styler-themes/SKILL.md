---
name: evo-ui-styler-themes
description: "Use this skill whenever the user wants to theme, recolor, or restyle Evo UI as a whole, or switch themes — e.g. 'make all my evo ui buttons blue', 'change the accent color everywhere', 'add a dark mode / light mode toggle', 'create my own theme', 'use my font for all ui text', 'why does my color keep changing back', 'make this one button a different color', 'button hover color'. Covers Styler Presets (colors, fonts, gradients, sprites, audio, text and shape styles), the default preset, Styler Object, the Style Provider and Use Default Preset options, applying presets in the Editor, runtime theme switching with transitions, and local overrides that stop Styler from resetting a value. Do NOT use for the geometry of a single shape such as corners, outlines, or shadows (see evo-ui-procedural-shape) or for creating controls (see evo-ui-create-elements). When in doubt whether a color, font, or theme request could involve Evo UI, use this skill — Prerequisites shows how to confirm the asset is installed."
metadata:
  asset: "Evo UI"
  publisher: "Michsky"
  asset-version: "2.0.0"
  skill-version: "1.1.2"
  unity: "6000.0+"
  render-pipelines: "Built-in, URP, HDRP"
  category: "tools/gui"
  asset-store-url: "https://assetstore.unity.com/packages/slug/410788"
  documentation-url: "https://evo.michsky.com/docs/evo-ui"
  support-url: "https://www.michsky.com/contact"
  last-verified: "2026-11-11"
---

# Theme UI with Evo UI Styler

Styler keeps shared colors, fonts, gradients, sprites, audio clips, text styles, and Procedural Shape styles in a **Styler Preset** asset. Evo UI components ask the preset for values by ID (for example `"Accent"`), so changing one entry restyles every control that uses it, and swapping the preset switches the whole theme. This skill covers creating and editing presets, assigning them, switching themes at runtime, and overriding a single object locally.

## When to use this skill

- "change the accent color of all my evo ui elements to orange"
- "make my own theme instead of the default one"
- "add a button that switches between dark and light mode"
- "use Montserrat for all the ui text"
- "the button color keeps resetting when i change it"
- "make only the delete button red"
- "change the hover / pressed color of buttons"
- "rename the 'Primary' color id without breaking things"

Not for:
- Corner radius, outlines, shadows, gradients on one graphic — see `evo-ui-procedural-shape` (it also covers per-layer Styler mappings on a shape).
- Creating controls — see `evo-ui-create-elements`.
- Animating colors with a timeline or code — see `evo-tween-tween-animator`.

## Prerequisites

- Evo UI 2.0.0+ on Unity 6000.0+, any render pipeline.
- **Install check:** `System.Type.GetType("Evo.UI.StylerPreset, Evo.UI") != null`, or confirm the menu **Tools > Evo UI > Styler Browser** exists. If it fails, tell the user Evo UI isn't installed and link https://assetstore.unity.com/packages/slug/410788.
- The bundled presets are `Assets/Evo/Evo UI/Resources/Styler Presets/Default.asset` (dark) and `Default (Light).asset`. Never edit them for a user's theme: package updates overwrite them. Copy them first.

Key concepts:
- **Style Provider** (`Evo.UI.StyleProvider`): `Custom = 0` uses values set on the component; `Styler = 1` resolves them from a preset by ID.
- **Use Default Preset** (`useDefaultStylerPreset`): when on, the component follows the project's default preset. **In Edit Mode it is re-synced to the default on every update**, so assigning another preset to it has no lasting effect until this is turned off.
- IDs in the bundled presets — colors: `Primary`, `Primary (Dim)`, `Primary (Tint)`, `Primary (Soft)`, `Secondary`, `Accent`, `Accent (Tint)`, `Accent (Soft)`, `Background`, `Background (Dim)`, `Background (Tint)`, `Negative`, `Shadow`; fonts: `Light`, `Regular`, `Bold`; gradient: `Red Blue`; shape styles: `Radius`, `Background`, `Background (Pill)`, `Background (Shadow)`, `Background (Container)`; audio: `Hover SFX`, `Click SFX`, `Notification SFX`.

## Quick start

Create a user theme from the default, change its accent color, and make it the project default.

1. Copy the bundled preset into a **Resources** folder the user owns (a default preset must be in a Resources folder):

```csharp
using UnityEditor;
using UnityEngine;

public static class EvoCreateTheme
{
    public static void Run()
    {
        const string source = "Assets/Evo/Evo UI/Resources/Styler Presets/Default.asset";
        const string folder = "Assets/UI/Resources";
        const string target = folder + "/My Theme.asset";

        if (!AssetDatabase.IsValidFolder("Assets/UI"))
            AssetDatabase.CreateFolder("Assets", "UI");
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets/UI", "Resources");

        AssetDatabase.CopyAsset(source, target);
        var preset = AssetDatabase.LoadAssetAtPath<Evo.UI.StylerPreset>(target);
        preset.SetColor("Accent", new Color32(255, 136, 0, 255)); // Adds the ID if it is missing.
        EditorUtility.SetDirty(preset);
        AssetDatabase.SaveAssetIfDirty(preset);
        Selection.activeObject = preset;
    }
}
```

2. With the preset selected, click **Set as Default Preset** at the top of its Inspector. Evo UI stores the choice in `Assets/Evo/Evo UI/Resources/Styler Presets/Config.txt` (the preset's path relative to its Resources folder, here `My Theme`).
3. Every component with **Use Default Preset** on now uses `My Theme`. The **GameObject > Evo UI** templates have it on, so elements created afterwards follow it too.

Expected result: `Evo.UI.Styler.GetDefaultPreset(false, true).name == "My Theme"`, and Evo UI elements that use the `Accent` color turn orange in the Scene view.

## Workflows

### Workflow: Edit theme values

**Goal:** Change a color, font, or other entry for every element that uses it.

**Steps:**
1. Select the preset (**Tools > Evo UI > Open Default Styler**, Ctrl+Shift+M, opens the default) or open **Tools > Evo UI > Styler Browser**.
2. Edit entries in the Inspector. In code, use the `StylerPreset` methods: `SetColor(id, color)`, `SetFont(id, TMP_FontAsset)`, `SetGradient(id, gradient)`, `SetSprite(id, sprite)`, `SetAudio(id, clip)` (each adds the entry when the ID is missing), `AddShapeStyle(id)`, `AddTextStyle(id)`, and `Remove...(id)`. Read with `TryGetColor(id, out Color)` and the other `TryGet...` methods.
3. After editing from code, save and refresh the scene's components:

```csharp
EditorUtility.SetDirty(preset);
AssetDatabase.SaveAssetIfDirty(preset);
Evo.UI.Styler.ApplyPresetTo(preset, canvasGameObject, includeChildren: true); // Re-applies to handlers already using it.
```

4. For a UniText project, each Font entry holds one font per provider; assign the UniText font in the Inspector (see the text provider docs).

**Expected result:** `preset.TryGetColor("Accent", out var c)` returns the new value, and styled elements show it.

### Workflow: Apply a preset to objects in the Editor

**Goal:** Make a panel, a scene, or a prefab use a specific preset instead of the default.

**Steps:**
1. Human route: open **Tools > Evo UI > Styler Browser**, select the preset, and select the objects. The **Apply** button at the bottom applies to the selection; the arrow button to its left shows or hides the options above it: **Selection** (with **Include Children**), **Scenes** (loaded scenes), or **Project**. While the options are hidden, the button's label names the target and its tooltip says how many components it updates. Project scope saves prefabs and closed scenes and can't be fully undone, so confirm with the user first.
2. Code route: turn off **Use Default Preset** and assign the preset through serialized properties, which is what the Browser does:

```csharp
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class EvoApplyPreset
{
    public static int Run(Evo.UI.StylerPreset preset, GameObject root)
    {
        int count = 0;

        foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour is not Evo.UI.IStylerHandler handler)
                continue;

            var so = new SerializedObject(behaviour);
            var useDefault = so.FindProperty("useDefaultStylerPreset");
            var reference = so.FindProperty("preset") ?? so.FindProperty("stylerPreset");
            if (reference == null || reference.propertyType != SerializedPropertyType.ObjectReference)
                continue;

            if (useDefault != null)
                useDefault.boolValue = false;

            reference.objectReferenceValue = preset;
            so.ApplyModifiedProperties(); // Records Undo and prefab overrides.
            handler.UpdateStyler();
            count++;
        }

        EditorSceneManager.MarkSceneDirty(root.scene);
        return count;
    }
}
```

**Expected result:** every `IStylerHandler` under `root` reports `handler.Preset == preset`, and `useDefaultStylerPreset` is false on them.

### Workflow: Switch themes at runtime (dark / light mode)

**Goal:** Let the player switch the whole UI between presets in a build.

**Steps:**
1. Create the two presets (copy `Default.asset` and `Default (Light).asset`, then edit them). Keep the **same IDs** in both, so an element that asks for `Accent` finds it in either theme.
2. Add a script and call `Styler.ApplyPreset`:

```csharp
using UnityEngine;
using Evo.UI;

public class ThemeSwitcher : MonoBehaviour
{
    [SerializeField] private StylerPreset darkTheme;
    [SerializeField] private StylerPreset lightTheme;

    public void SetDarkMode(bool dark)
    {
        // Registered handlers switch now; handlers enabled later in this session inherit it.
        Styler.ApplyPreset(dark ? darkTheme : lightTheme, isPersistent: true, transitionDuration: 0.25f);
        PlayerPrefs.SetInt("DarkMode", dark ? 1 : 0); // Styler does not save the choice between sessions.
    }

    private void Start() => SetDarkMode(PlayerPrefs.GetInt("DarkMode", 1) == 1);
}
```

3. Wire `SetDarkMode` to a Switch's `onValueChanged` (see `evo-ui-create-elements`).
4. To restyle only part of the UI, use `Styler.ApplyPresetTo(preset, panelGameObject, includeChildren: true, transitionDuration: 0.25f)`. It also reaches inactive children and doesn't change the persistent preset.
5. For a screen that must keep its own look during theme switches, turn on **Keep On Global Apply** (`keepOnGlobalApply`) in that screen's preset **Settings**. `ApplyPreset` then skips its objects; `ApplyPresetTo` still changes them.

**Expected result:** in Play Mode, calling `SetDarkMode(false)` blends supported colors and gradients over 0.25 s; fonts, sprites, and shape geometry switch immediately. `Styler.PersistentPreset == lightTheme`.

### Workflow: Override one object locally

**Goal:** Give one element its own color without Styler resetting it.

**Steps:**
1. On a **Styler Object**: set `useCustomColor = true`, then set the target Graphic's color; or keep the preset color and change only opacity with `overrideAlpha = true` and `alphaOverride` (0–1). Call `UpdateStyler()` after changing fields from code.
2. On an **Interactive** control (Button, Switch, ...): set `styleProvider = StyleProvider.Custom` to use its own audio mappings.
3. On a **Procedural Shape** layer: set `fillColorMode` (or `outlineColorMode`, `innerShadowColorMode`, `outerShadowColorMode`, `patternColorMode`) to `ProceduralShape.ColorMode.Custom` and assign `FillColor` and so on; call `UpdateRect()`. See `evo-ui-procedural-shape`.
4. To use a different ID instead, set `colorID` (Styler Object) or the layer's `fillStyler.color.stylerID` (Procedural Shape), then refresh.

```csharp
var styler = deleteButtonLabel.GetComponent<Evo.UI.StylerObject>();
styler.colorID = "Negative";
styler.UpdateStyler();
```

**Expected result:** the object keeps its color after entering Play Mode and after another preset is applied.

### Workflow: Interaction state colors

**Goal:** Change hover, pressed, selected, and disabled colors.

**Steps:**
1. For a text or graphic with a **Styler Object**: set `enableInteraction = true`, assign `interactableObject` (the control's `Selectable`, e.g. the Evo UI Button), keep `enableColorInteraction = true`, and set the IDs on `disabledColor`, `normalColor`, `highlightedColor`, `pressedColor`, `selectedColor` (each a `ColorMapping` with `stylerID`, and `overrideAlpha`/`alphaOverride`). Fonts per state use `enableFontInteraction` and the `...Font` mappings.
2. For a **Procedural Shape** layer in Styler mode: set `fillStyler.enableInteraction = true` and the same five mappings on `fillStyler`; assign the shape's `interactableObject`.
3. Call `UpdateStyler()` after editing from code.

**Expected result:** in Play Mode, hovering and pressing the control blends to the mapped colors.

### Workflow: Rename an ID safely

**Goal:** Rename an entry that components already use.

**Steps:**
1. Select the preset, expand the item, type the new ID in its **ID** field, and press Enter. The rename runs when the field commits; Escape cancels it.
2. If other presets in `Assets` have the same ID, a dialog offers **Rename in All** (keeps theme presets matching), **Only This Preset**, or **Cancel**. If the new ID is a former ID of another item, the dialog warns that objects still using it will switch to this item.
3. The old ID stays on the item as a **former ID** (`Styler.Item.formerIDs`), and the item shows "Renamed from ...". Components, prefabs, and code that still use the old ID keep resolving to the item, with no scene or prefab changes.
4. Optional cleanup: click **Update References** on the item. It opens the ID Migrator with the former ID as source and the current ID as target. Pick a scope (selected objects, loaded scenes, prefab assets, or the entire project) and run it. Ask the user before a project-wide run, then save modified open scenes.
5. Click **Clear** to drop the former IDs once nothing uses them. Objects that still do will show the ID as missing.

An item added in the inspector during this Editor session counts as new until an object picks it in a Styler dropdown. Renaming a new item changes only this preset, keeps no former ID, and shows no dialog unless the new ID is another item's former ID.

From code or a script, `preset.TryGetColor("OldID", out _)` keeps returning the renamed item until its former IDs are cleared. The ID Migrator (**Tools > Evo UI > Styler Browser > ID Migrator**) also keeps the source ID as a former ID when it renames an entry.

**Expected result:** right after the rename, nothing changes visually. Inspector dropdowns of components that still store the old ID show `NewID (was OldID)` until **Update References** rewrites them.

## Verification

- `Evo.UI.Styler.GetDefaultPreset(false, true)` returns the intended default preset.
- The preset contains the IDs: `preset.TryGetColor("Accent", out _)` is true.
- Targeted handlers: `((Evo.UI.IStylerHandler)component).Preset == preset`, and `useDefaultStylerPreset == false` where a non-default preset was applied.
- Styled colors read back: `styler.targetGraphic.color` (Styler Object) or `shape.FillColor` (Procedural Shape) matches the preset value, allowing for `alphaOverride`.
- Play Mode theme switch: `Styler.PersistentPreset` equals the applied preset after `ApplyPreset(..., isPersistent: true)`.
- No `[Styler] No default preset found.` warning in the Console.

## API quick reference

| Entry point | Type | What it does |
| --- | --- | --- |
| `Evo.UI.StylerPreset` | ScriptableObject | Theme asset (**Create > Evo > UI > Styler Preset**); `Set...`, `Add...`, `Remove...`, `TryGet...` per entry type (an ID also matches an item's former IDs); `keepOnGlobalApply`. |
| `Evo.UI.Styler.Item.formerIDs` | Field | Old IDs of a renamed item that still resolve to it. A current ID always wins, and `Add...` with a former ID takes it over. |
| `Evo.UI.Styler.ApplyPreset(preset, isPersistent = true, transitionDuration = 0f)` | Static method | Switches every registered handler; persistent in Play Mode for the session. |
| `Evo.UI.Styler.ApplyPresetTo(preset, GameObject, includeChildren = true, transitionDuration = 0f)` | Static method | Applies to one hierarchy, including inactive children. |
| `Evo.UI.Styler.GetDefaultPreset(generateLog = true, bypassCache = false)` | Static method | Resolves the default preset (Config.txt, else `Resources/Styler Presets/Default`). |
| `Evo.UI.Styler.PersistentPreset` | Static property | Runtime preset inherited by handlers that register later; `null` stops inheritance. |
| `Evo.UI.StylerObject` | Component | Styles a Graphic, Text, Image, or Image Gradient by ID; state colors; local overrides. |
| `Evo.UI.IStylerHandler` | Interface | `Preset` and `UpdateStyler()`; implemented by Evo UI components and usable by custom ones. |
| `Tools > Evo UI > Styler Browser` | Window | Preset management, scoped apply, ID Migrator. |

Deeper reference: https://evo.michsky.com/docs/evo-ui/styler (Styler Preset, Styler Object, Styler Browser, IStylerHandler pages).

## Common issues

| Symptom | Cause | Fix |
| --- | --- | --- |
| A color set on a Graphic changes back. | A Styler Object or the component's Styler mapping re-applies the preset. | Use a local override (*Override one object locally*), or change the preset entry for a shared change. |
| An assigned preset reverts to the default in the Editor. | **Use Default Preset** is on; in Edit Mode it re-syncs to the default. | Set `useDefaultStylerPreset = false` before assigning `Preset`. |
| **Set as Default Preset** shows "Invalid Location". | The preset isn't inside a folder named `Resources`. | Move it to e.g. `Assets/UI/Resources/`. |
| Theme switch does nothing in the Editor's Edit Mode. | `ApplyPreset` targets registered handlers, which register in Play Mode; it isn't persistent outside Play Mode. | Use the Styler Browser or *Apply a preset to objects in the Editor*. |
| After switching, some elements show white or missing colors. | The new preset lacks IDs used by the old one. | Keep identical IDs across theme presets; add the missing entries. Rename with **Rename in All** so theme presets keep matching. |
| Objects that used a renamed item's old ID now show a different item's value. | A new or renamed item took that ID, which takes it over from the old item's former IDs. | Run **Update References** on the renamed item before reusing its old ID, or pick another ID. |
| One screen should not follow the theme. | It shares the global preset. | Give it its own preset with **Keep On Global Apply** on. |
| The user's theme edits disappeared after updating Evo UI. | The bundled `Default.asset` was edited directly. | Keep themes in the user's own folder; restore from version control. |
| The chosen theme resets on the next launch. | Runtime presets last only for the session. | Save the choice (e.g. `PlayerPrefs`) and apply it on startup. |

## Boundaries

- Transitions blend colors and gradients only; fonts, sprites, text styles, and shape geometry change instantly.
- The Styler Browser and **Set as Default Preset** are editor-only; builds use the default stored in Config.txt plus runtime `ApplyPreset`/`ApplyPresetTo` calls.
- Styler doesn't style plain uGUI components on its own; add a Styler Object to them, or implement `IStylerHandler` in custom components (https://evo.michsky.com/docs/evo-ui/styler/istyler-handler).
- Styler doesn't save player preferences.
