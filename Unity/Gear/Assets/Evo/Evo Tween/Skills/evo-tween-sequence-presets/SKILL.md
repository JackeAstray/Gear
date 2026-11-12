---
name: evo-tween-sequence-presets
description: "Use this skill whenever the user wants to reuse, share, or visually edit Evo Tween animations, or change the built-in animations of Evo UI controls — e.g. 'use the same entrance animation on all my panels', 'change the dropdown open animation', 'make the modal window pop in slower', 'edit the tab switch transition', 'open the tween timeline', 'record keyframes by moving the object', 'stagger these clips', 'detach this from the preset'. Covers Sequence Preset assets (create, assign, bind targets by clip index, detach), Parallel and Sequential timing, the Tween Timeline window (arrange, preview, record), the open, close, and transition sequences on Evo UI components, and TweenSequenceSampler. Do NOT use for building a Tween Animator from scratch (see evo-tween-tween-animator) or code tweens (see evo-tween-scripting). When in doubt whether an animation reuse or timing request could involve Evo Tween, use this skill — Prerequisites shows how to confirm the asset is installed."
metadata:
  asset: "Evo Tween"
  publisher: "Michsky"
  asset-version: "2.0.0"
  skill-version: "1.0.0"
  unity: "6000.0+"
  render-pipelines: "Built-in, URP, HDRP"
  category: "tools/gui"
  asset-store-url: "https://assetstore.unity.com/packages/slug/410788"
  documentation-url: "https://evo.michsky.com/docs/evo-tween"
  support-url: "https://www.michsky.com/contact"
  last-verified: "2026-11-03"
---

# Reuse Animations with Sequence Presets and Timeline

Every Evo Tween animation is a **Tween Sequence**: an ordered clip list with one clock. Sequences live in Tween Animator states and directly on Evo UI components (for example a Dropdown's open and close animations). A **Sequence Preset** (`TweenSequencePreset` asset) stores clip definitions once so many sequences can share them, while each sequence keeps its own target bindings. The **Tween Timeline** window edits any sequence visually, with preview and property recording. This skill covers all three.

## When to use this skill

- "every panel should use the same slide-in animation"
- "make one hover animation and use it on all cards"
- "the dropdown opens too fast, slow it down"
- "change the modal window open animation to a scale pop"
- "make the tab transition slide instead of fade"
- "open the tween timeline and line up these clips"
- "record the animation by dragging the object in the scene"
- "this object should have its own variation of the shared animation"

Not for:
- Creating a Tween Animator and its states — see `evo-tween-tween-animator`.
- Tweens from code — see `evo-tween-scripting`.

## Prerequisites

- Evo Tween 2.0.0+ (ships with Evo UI) on Unity 6000.0+.
- **Install check:** `System.Type.GetType("Evo.Tween.TweenSequencePreset, Evo.Tween") != null`, or confirm the menu **Tools > Evo Tween > Tween Timeline** exists. If it fails, tell the user Evo Tween isn't installed; it comes with Evo UI (https://assetstore.unity.com/packages/slug/410788).
- Keep the user's presets outside `Assets/Evo/Evo Tween` and `Assets/Evo/Evo UI`, so package updates don't replace them.
- Timeline recording works in Edit Mode on scene objects, with Unity's Animation Mode stopped.

How sequences time clips (read before editing timing):
- **Parallel** places a clip at the current group's start plus its own **Delay**.
- **Sequential** starts a new group after every clip of the previous group finishes, then adds its Delay. The first clip always starts the first group.
- A group ends when its longest clip (delay + duration × iterations) ends. Reordering clips can change groups.
- **Time Mode** (`Scaled` / `Unscaled`) belongs to the consuming sequence, even with a preset.

## Quick start

Create a shared "panel in" preset and use it on two panels.

```csharp
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Evo.Tween;
using Evo.Tween.Modules.UGUI;   // CanvasGroupAlphaModule, RectTransformSlideModule

public static class EvoSharedPanelIn
{
    public static void Run(TweenAnimator panelA, TweenAnimator panelB)
    {
        const string path = "Assets/UI/Animations/Panel In.asset";
        if (!AssetDatabase.IsValidFolder("Assets/UI"))
            AssetDatabase.CreateFolder("Assets", "UI");
        if (!AssetDatabase.IsValidFolder("Assets/UI/Animations"))
            AssetDatabase.CreateFolder("Assets/UI", "Animations");

        var preset = ScriptableObject.CreateInstance<TweenSequencePreset>();

        var fade = new TweenClip();
        fade.Settings.Module = new CanvasGroupAlphaModule { fromCurrent = false, from = 0f, to = 1f };
        fade.Settings.Duration = 0.2f;
        preset.Clips.Add(fade);

        var slide = new TweenClip();
        slide.Settings.Module = new RectTransformSlideModule { fromCurrent = false, from = new Vector2(0f, -40f), to = Vector2.zero };
        slide.Settings.Duration = 0.3f;
        preset.Clips.Add(slide);

        AssetDatabase.CreateAsset(preset, path);
        AssetDatabase.SaveAssets();

        foreach (var animator in new[] { panelA, panelB })
        {
            Undo.RecordObject(animator, "Assign Panel In");
            var sequence = animator.GetState(animator.DefaultStateIndex).sequence;
            sequence.Preset = preset;        // Empty targets resolve on each panel's own GameObject.
            EditorUtility.SetDirty(animator);
        }

        EditorSceneManager.MarkSceneDirty(panelA.gameObject.scene);
    }
}
```

Each panel needs a `CanvasGroup` (the alpha clip's target type). Expected result: `Assets/UI/Animations/Panel In.asset` exists with 2 clips; both panels' default-state sequences report `sequence.Preset == preset` and `sequence.Count == 2`. In Play Mode both panels fade and slide in.

## Workflows

### Workflow: Create and assign a Sequence Preset

**Goal:** Share clip definitions between objects.

**Steps:**
1. Project window: **Create > Evo > Tween > Sequence Preset** (or `ScriptableObject.CreateInstance<TweenSequencePreset>()` + `AssetDatabase.CreateAsset`). Add clips in its Inspector, or in code through `preset.Clips` (a mutable `List<TweenClip>`). `CopyFrom(IReadOnlyList<TweenClip>)` replaces the clips with copies of another list (target references removed).
2. Select a consumer (a Tween Animator state, or an Evo UI component's sequence) and assign the asset to the sequence's **Preset** field (`sequence.Preset = preset`).
3. Targets: the preset stores definitions only. Each consumer binds targets by **clip index** with `sequence.SetTarget(index, component)` (read with `GetTarget(index)`); empty targets resolve on the consumer's own GameObject.
4. Preview on the consumer with the Inspector controls or the Timeline Window.

**Expected result:** several consumers show the same clips; editing the preset changes all of them.

### Workflow: Edit, vary, or detach a shared animation

**Goal:** Change the shared animation, or give one object its own variation.

**Steps:**
1. Shared change: on a consumer, **Edit Preset** selects the asset; or edit the preset in the Timeline Window. Value and timing edits affect every consumer.
2. After adding, removing, or reordering preset clips, re-check each consumer's target bindings (they are stored by index).
3. One-off variation: **Detach** in the Timeline Window, or `sequence.DetachPreset()` (copies the preset's clips and the current targets inline, then removes the preset reference). The asset is unchanged.
4. Clearing the **Preset** field instead brings back whatever inline clips the sequence had before; it does not copy the preset.
5. At runtime, after changing a preset, call `RefreshState(index)` or `PrepareStates()` on affected Tween Animators.

**Expected result:** after Detach, `sequence.Preset == null` and `sequence.InlineClips.Count` equals the preset's clip count.

### Workflow: Change an Evo UI control's built-in animation

**Goal:** Restyle how an Evo UI component opens, closes, or transitions.

**Steps:**
1. These components own their sequences; no extra Tween Animator is needed:

| Component (`Evo.UI`) | Sequence fields |
| --- | --- |
| `Dropdown` (and Input / Multi Select) | `openAnimation`, `closeAnimation` |
| `ModalWindow` | `openAnimation`, `closeAnimation` |
| `Notification` | `openAnimation`, `closeAnimation` |
| `Popover` | `openAnimation`, `closeAnimation` |
| `TabManager` | `inAnimation`, `outAnimation` |
| `TooltipPreset` (on the tooltip preset prefab) | `openAnimation`, `closeAnimation` |
| `ContextMenuPreset` (on the menu preset prefab) | `openAnimation`, `closeAnimation` |
| `ContextMenuSubmenu` (on submenu rows) | `OpenAnimation`, `CloseAnimation` |

2. Select the component, open **Tools > Evo Tween > Tween Timeline**, and choose the sequence in the header's sequence selector. Adjust durations, eases, and clips, or assign a Sequence Preset to share one look across controls.
3. From code, edit the clips directly, e.g. slow every clip of a dropdown's open animation:

```csharp
Undo.RecordObject(dropdown, "Slower Dropdown");
foreach (var clip in dropdown.openAnimation.InlineClips)
    clip.Settings.Duration *= 1.5f;
EditorUtility.SetDirty(dropdown);
```

4. For Tooltip and Context Menu, edit the preset **prefab** (a copy in the user's folder, not the package original), since the sequences live there.

**Expected result:** the control opens with the new timing in Play Mode; `dropdown.openAnimation.Duration` reflects the change.

### Workflow: Use the Tween Timeline window

**Goal:** Arrange and preview clips visually.

**Steps:**
1. Select the component, then **Tools > Evo Tween > Tween Timeline** (`EditorApplication.ExecuteMenuItem("Tools/Evo Tween/Tween Timeline")`). Choose the sequence (each Tween Animator state has its own). Use the **lock** button to keep editing it while selecting other objects.
2. Rows are clips. **Add Clip** (or right-click empty space) adds one; drag a clip body to change its delay, drag edges to trim, drag the row grip to reorder, change **Flow** to join or start a group. Drag a GameObject or component from the Hierarchy/Inspector onto a track to set its target, or below the tracks to add a clip for it.
3. Multi-select (**Shift**, **Ctrl / Cmd**, box drag) to edit shared fields; **Choreography > Stagger** offsets each clip by **Stagger Step**; **Align Starts** aligns them.
4. Preview: click or drag the ruler to scrub; **Space** plays or pauses; **Stop** restores scene values; **L** loops. **A** fits all, **F** frames the selection. **Snap** locks to edges, the playhead, or the **FPS** grid (hold **Ctrl / Cmd** to place freely).
5. Shortcuts: **Ctrl / Cmd + C / V / D** copy, paste, duplicate; **Delete** removes; **Left/Right** steps a frame; **Shift + Left/Right** jumps to clip edges; **Home/End** go to start/end. Edits support Undo.

**Expected result:** the sequence's clips reflect the arranged timing; the preview matches Play Mode playback.

### Workflow: Record property changes

**Goal:** Create or shape clips by editing the object directly.

**Steps:**
1. In Edit Mode, select the scene component, open the Timeline, lock it if needed, and place the playhead.
2. Enable **Record** (the ruler turns red), then change a supported property in the Inspector or with a Scene view handle (Transforms, supported uGUI and TMP properties, audio, selected rendering properties, Evo UI shapes).
3. Move the playhead and edit again. At a clip's start or end, the edit updates that endpoint; inside a non-looping clip, it splits the clip with a waypoint; for a property without a clip, it adds one.
4. Turn Record off (restores scene values) and play to review.
5. With a preset attached, endpoint edits update the shared preset; adding or splitting clips detaches first. Detach manually beforehand to keep every edit local.

**Expected result:** new or updated clips appear for the edited properties.

### Workflow: Sample a sequence manually

**Goal:** Drive a sequence by a custom value (a slider, scroll position, or cutscene time).

**Steps:**
1. Create `new TweenSequenceSampler(ownerGameObject, sequence)` once.
2. `Sample(seconds)` applies that time (capturing the targets' values on first use); `Duration` is the sequence length; `Refresh(normalizedTime)` rebuilds after edits; `Restore()` puts the captured values back; `Release()` keeps the sampled values; `Dispose()` restores and frees the sampler, so call `Release()` first to keep the result.

```csharp
using UnityEngine;
using Evo.Tween;

public class ScrubbedIntro : MonoBehaviour
{
    [SerializeField] private TweenAnimator source;
    private TweenSequenceSampler sampler;

    private void Start() => sampler = new TweenSequenceSampler(source.gameObject, source.GetState(0).sequence);

    public void OnProgress(float normalized) => sampler.Sample(normalized * sampler.Duration);

    private void OnDestroy()
    {
        sampler?.Release(); // Keep the last sampled pose.
        sampler?.Dispose();
    }
}
```

**Expected result:** moving the progress value scrubs the animation without Tween Animator playing it.

## Verification

- Preset asset: `AssetDatabase.LoadAssetAtPath<Evo.Tween.TweenSequencePreset>(path).Count` equals the intended clip count, and each clip's `Settings.Module != null`.
- Consumers: `sequence.Preset == preset`; `sequence.GetTarget(i)` is set where a target other than the owner is needed.
- Detached sequences: `sequence.Preset == null` and `InlineClips.Count` matches.
- Evo UI component edits: the component's sequence (`openAnimation`, etc.) shows the changed `Duration`; the scene or prefab is saved.
- Timeline: **Tools > Evo Tween > Tween Timeline** opens and shows the selected sequence.
- No Console errors.

## API quick reference

| Entry point | Type | What it does |
| --- | --- | --- |
| `Evo.Tween.TweenSequencePreset` | ScriptableObject | `Clips` (mutable), `Count`, `CopyFrom(clips)`. **Create > Evo > Tween > Sequence Preset**. |
| `Evo.Tween.TweenSequence` | Class | `Preset`, `InlineClips`, `Clips`, `Count`, `Duration`, `TimeMode`, `GetTarget(i)`, `SetTarget(i, component)`, `DetachPreset(copyClips = true)`, `Clone()`. |
| `Evo.Tween.TweenSequenceSampler` | Class | `Sample(time)`, `Refresh(normalizedTime)`, `Restore()`, `Release()`, `Dispose()`. |
| `Tools > Evo Tween > Tween Timeline` | Menu / window | Visual editing, preview, recording. |
| `TweenAnimator.RefreshState(i)` / `PrepareStates()` | Methods | Rebuild prepared states after runtime preset edits. |

Full reference: https://evo.michsky.com/docs/evo-tween/sequence-preset, https://evo.michsky.com/docs/evo-tween/tween-sequence, https://evo.michsky.com/docs/evo-tween/timeline-window.

## Common issues

| Symptom | Cause | Fix |
| --- | --- | --- |
| A shared preset animates the wrong objects. | Targets are bound by clip index and the preset's clips were reordered. | Rebind with `SetTarget(index, ...)` on each consumer. |
| Editing one object's animation changed every object. | It uses a shared preset. | **Detach** (or `DetachPreset()`) before the one-off change. |
| Clearing the Preset field brought back an old animation. | Clearing restores the sequence's previous inline clips. | Use **Detach** to keep the preset's clips. |
| The Timeline is empty. | No component with a serialized Tween Sequence is selected, the wrong sequence is chosen, or the window is locked to another object. | Select the component, pick the sequence in the header, unlock. |
| Record is unavailable. | Play Mode, Unity Animation Mode, a prefab asset (not a scene object), or overlapping clips on the same property. | Use Edit Mode on a scene object, stop Animation Mode, resolve the overlap. |
| Runtime preset changes don't appear. | Tween Animator prepares states ahead. | `RefreshState(index)` or `PrepareStates()`. |
| A content clip starts too late. | A Sequential clip waits for the whole previous group, including its longest clip. | Use Parallel with a Delay, or shorten the previous group. |
| A changed Evo UI animation reverted after a package update. | The package's prefab (e.g. Tooltip Preset) was edited directly. | Work on a copy in the user's folder and assign it. |

## Boundaries

- Presets share clip definitions, not targets or Time Mode; each consumer keeps its own.
- Recording supports the properties with recording providers (Transforms, supported uGUI/TMP, audio, selected rendering, Evo UI shapes); tweenable doesn't always mean recordable.
- The Tween Timeline isn't Unity's Timeline package; it doesn't use Timeline assets or Playable Directors.
- `TweenClip.Trigger` on a preset's `Clips` ignores the consumer's target bindings; play preset sequences through a Tween Animator state or a `TweenSequenceSampler`.
