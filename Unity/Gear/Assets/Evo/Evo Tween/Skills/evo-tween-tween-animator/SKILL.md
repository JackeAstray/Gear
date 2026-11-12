---
name: evo-tween-tween-animator
description: "Use this skill whenever the user wants to animate an object or UI element without writing tween code, using Evo Tween's Tween Animator component — e.g. 'make this panel fade and slide in when it opens', 'pop the button bigger on hover', 'animate the menu in and out', 'loop a floating / pulsing animation', 'play the Show animation when i click', 'hover animation for evo ui buttons', 'my tween animator doesn't play'. Covers states, clips (modules, From Current, From/Target, Flow, Delay, Duration, Ease, loops), Manual, Pointer Event, and Interaction triggers, playback settings (Play On Enable, Restore On Stop, Time Mode, Update Phase, Loop), building states from editor scripts, and controlling playback from code. Do NOT use for tweens started purely from code (see evo-tween-scripting) or shared presets and the Timeline Window (see evo-tween-sequence-presets). When in doubt whether an animation request could involve Evo Tween, use this skill — Prerequisites shows how to confirm the asset is installed."
metadata:
  asset: "Evo Tween"
  publisher: "Michsky"
  asset-version: "2.0.0"
  skill-version: "1.0.1"
  unity: "6000.0+"
  render-pipelines: "Built-in, URP, HDRP"
  category: "tools/gui"
  asset-store-url: "https://assetstore.unity.com/packages/slug/410788"
  documentation-url: "https://evo.michsky.com/docs/evo-tween"
  support-url: "https://www.michsky.com/contact"
  last-verified: "2026-11-03"
---

# Animate Objects with Tween Animator

`Evo.Tween.TweenAnimator` plays named **states**. Each state owns a **Tween Sequence**: an ordered list of **clips**, where each clip animates one property (a module such as **Transform > Scale** or **UI > Canvas Group > Alpha**) from a start value to a target with its own timing and easing. States play from code, from UnityEvents, from pointer events, or by following an Evo UI control's interaction state. This skill covers setting it up in the Inspector and from editor scripts, and controlling it at runtime.

## When to use this skill

- "fade + slide the settings panel in when it opens, and out when it closes"
- "scale the button to 1.05 when hovered and back when the mouse leaves"
- "make the coin icon bob up and down forever"
- "play a shake animation when the player takes damage"
- "add an intro animation that plays when the menu appears"
- "the animation snaps back when it's interrupted"
- "tween animator does nothing when i call Play"

Not for:
- Tweens created from scripts with `EvoTween.Create` — see `evo-tween-scripting`.
- Sharing one animation across many objects, or editing timing visually — see `evo-tween-sequence-presets`.
- The built-in open/close animations of Evo UI controls (Dropdown, Modal Window, Tab Manager) — those sequences live on the controls; see `evo-tween-sequence-presets`.

## Prerequisites

- Evo Tween 2.0.0+ (ships with Evo UI) on Unity 6000.0+, any render pipeline.
- **Install check:** `System.Type.GetType("Evo.Tween.TweenAnimator, Evo.Tween") != null`, or confirm **Add Component > Evo > Tween > Tween Animator** exists. If it fails, tell the user Evo Tween isn't installed; it comes with Evo UI (https://assetstore.unity.com/packages/slug/410788).
- UI clips (RectTransform, Canvas Group, Graphic, Image) need the UGUI module (`com.unity.ugui` 2.0.0+). **Interaction** triggers and Procedural Shape clips need Evo UI.
- Pointer Event triggers need an EventSystem with an input module and a raycast target on the object.
- `Play`, `Trigger`, and the runtime events need Play Mode; in Edit Mode use the Inspector preview controls or the Timeline Window.

## Quick start

Give a panel `Show` and `Hide` states from an editor script, then play them.

```csharp
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Evo.Tween;
using Evo.Tween.Modules.UGUI;   // CanvasGroupAlphaModule, RectTransformSlideModule

public static class EvoPanelAnimation
{
    // Adds a state copied from the last one, renames it, and clears its clips. Returns its index.
    static int AddState(TweenAnimator animator, string id)
    {
        var so = new SerializedObject(animator);
        var states = so.FindProperty("states");
        states.arraySize++;
        var state = states.GetArrayElementAtIndex(states.arraySize - 1);
        state.FindPropertyRelative("id").stringValue = id;
        state.FindPropertyRelative("sequence").FindPropertyRelative("clips").arraySize = 0;
        so.ApplyModifiedProperties();
        return states.arraySize - 1;
    }

    static TweenClip Clip(TweenModule module, float duration, ClipFlow flow = ClipFlow.Parallel)
    {
        var clip = new TweenClip { Flow = flow };
        clip.Settings.Module = module;
        clip.Settings.Duration = duration;
        clip.Settings.Ease = Ease.OutCubic;
        return clip;
    }

    public static void Run(GameObject panel)
    {
        if (panel.GetComponent<CanvasGroup>() == null)
            Undo.AddComponent<CanvasGroup>(panel);

        var animator = panel.GetComponent<TweenAnimator>();
        if (animator == null)
            animator = Undo.AddComponent<TweenAnimator>(panel); // Starts with one state, "Default".

        int hideIndex = AddState(animator, "Hide");

        Undo.RecordObject(animator, "Panel Animation");
        animator.playOnEnable = true; // Plays the default state (index 0) on enable.

        var show = animator.GetState(0);
        show.id = "Show";
        show.sequence.TimeMode = TimeMode.Unscaled; // Works while the game is paused.
        show.sequence.InlineClips.Clear();
        show.sequence.InlineClips.Add(Clip(new CanvasGroupAlphaModule { fromCurrent = false, from = 0f, to = 1f }, 0.2f));
        show.sequence.InlineClips.Add(Clip(new RectTransformSlideModule { fromCurrent = false, from = new Vector2(0f, -40f), to = Vector2.zero }, 0.3f));

        var hide = animator.GetState(hideIndex);
        hide.sequence.TimeMode = TimeMode.Unscaled;
        hide.restoreOnStop = false;
        hide.sequence.InlineClips.Add(Clip(new CanvasGroupAlphaModule { fromCurrent = true, to = 0f }, 0.15f));

        EditorUtility.SetDirty(animator);
        EditorSceneManager.MarkSceneDirty(panel.scene);
    }
}
```

At runtime: `panel.GetComponent<TweenAnimator>().Play("Hide")`.

Expected result: the panel has a `TweenAnimator` with states `Show` (index 0, default) and `Hide`. `animator.GetStateIndex("Hide") == 1` and `animator.GetState(0).sequence.Count == 2`. In Play Mode the panel fades in while sliding up 40 units, and `Play("Hide")` fades it out.

## Workflows

### Workflow: Add a Tween Animator and clips in the Inspector

**Goal:** Author an animation by hand.

**Steps:**
1. Add **Evo > Tween > Tween Animator**. Only one per GameObject; use several states instead of several animators.
2. Use the state selector at the top to choose a state; **+** adds one. The state menu duplicates, reorders, deletes, or sets a state as default (**Set as Default**).
3. Give each state a unique, case-sensitive **State ID** (`Show` and `show` differ; duplicates resolve to the first).
4. **Add Clip** and pick a module. Common ones: **Transform > Position / Rotation / Scale**, **UI > Rect Transform > Anchored Position / Slide / Size Delta**, **UI > Canvas Group > Alpha**, **UI > Graphic > Color / Alpha**, **UI > Image > Fill Amount**, **Evo UI > Procedural Shape > ...**.
5. Target: leave empty to use a matching component on this GameObject (children aren't searched); assign one explicitly for child objects.
6. Values: **From Current** (start from the value when the clip begins; best for interruptible transitions), **From**, **Target**.
7. Timing: **Flow** (`Parallel` starts with the current group, `Sequential` waits for the previous group to finish), **Delay**, **Duration** (default 0.3), **Ease** (default Out Cubic; **Custom** uses a curve), **Loop** (`None`, `Restart`, `Yoyo`) and **Iterations** (finite per clip).
8. Preview with the Inspector controls, or open **Tools > Evo Tween > Tween Timeline** for a visual view.

**Expected result:** the state lists the clips, and the preview plays the motion in Edit Mode.

### Workflow: Clips from editor code

**Goal:** Build clips programmatically, as in the Quick start.

**Steps:**
1. Create `new TweenClip()`, set `clip.Settings.Module` to a module instance, and set `clip.Settings.Duration`, `Delay`, `Ease`, `CustomEase`, `Loop`, `Iterations`; `clip.Flow`; `clip.Target` for a component other than the owner.
2. Module classes expose `fromCurrent`, `from`, `to`. They live in sub-namespaces, so add the matching `using` (the base types `TweenClip`, `TweenModule`, `Ease` are in `Evo.Tween`):

| Inspector menu | Class | Namespace | Value type |
| --- | --- | --- | --- |
| Transform > Position | `TransformPositionModule` (`coordinateSpace`: `CoordinateSpace.Local` default or `World`) | `Evo.Tween.Modules.Transforms` | `Vector3` |
| Transform > Rotation | `TransformRotationModule` | `Evo.Tween.Modules.Transforms` | `Quaternion` |
| Transform > Scale | `TransformScaleModule` | `Evo.Tween.Modules.Transforms` | `Vector3` |
| UI > Rect Transform > Anchored Position | `RectTransformPositionModule` | `Evo.Tween.Modules.UGUI` | `Vector2` |
| UI > Rect Transform > Slide | `RectTransformSlideModule` (offsets from the captured position) | `Evo.Tween.Modules.UGUI` | `Vector2` |
| UI > Rect Transform > Size Delta | `RectTransformSizeModule` | `Evo.Tween.Modules.UGUI` | `Vector2` |
| UI > Canvas Group > Alpha | `CanvasGroupAlphaModule` | `Evo.Tween.Modules.UGUI` | `float` |
| UI > Canvas Group > Interactable / Blocks Raycasts | `CanvasGroupInteractableModule`, `CanvasGroupBlocksRaycastsModule` | `Evo.Tween.Modules.UGUI` | `bool` |
| UI > Graphic > Color / Alpha | `GraphicColorModule`, `GraphicAlphaModule` | `Evo.Tween.Modules.UGUI` | `Color` / `float` |
| UI > Image > Fill Amount | `ImageFillAmountModule` | `Evo.Tween.Modules.UGUI` | `float` |

Other families: TMP modules in `Evo.Tween.Modules.TMP`, audio in `Evo.Tween.Modules.Audio`, camera/light/sprite/material in `Evo.Tween.Modules.Rendering`, Rigidbody in `Evo.Tween.Modules.Physics`, Volume in `Evo.Tween.Modules.RenderPipelines`, video in `Evo.Tween.Modules.Video`, Procedural Shape in `Evo.Tween.Modules.EvoUI`. To find a class by its menu name, search the source for `ModuleMenu("...")`.

3. Add clips with `state.sequence.InlineClips.Add(clip)`. Record Undo and call `EditorUtility.SetDirty(animator)` afterwards.
4. New states can't be added through the public API (`States` is read-only); use the `SerializedObject` pattern from the Quick start, then edit the state object returned by `GetState(index)`.
5. Other state fields (all public on `TweenAnimator.AnimationState`): `trigger`, `pointerEvents`, `pointerButton`, `interactionStates`, `restartOnTrigger`, `loop`, `iterations` (0 = infinite when looping), `playbackSpeed`, `updatePhase`, `restoreOnStop`. Set the default state through the state menu (**Set as Default**) or the serialized `defaultState` index.

**Expected result:** `state.sequence.Count` equals the number of clips added, and each clip's `Settings.Module` is non-null.

### Workflow: Triggers

**Goal:** Play states automatically from input or control state.

**Steps:**
1. **Manual** (default): play from code or a UnityEvent. In a Button's **On Click**, pick `TweenAnimator.Play (string)` and type the state ID.
2. **Pointer Event**: `trigger = TriggerMode.PointerEvent`, `pointerEvents` flags (`Enter`, `Exit`, `Down`, `Up`, `Click`, `Select`, `Deselect`, `Submit`, `Cancel`; several can be combined), `pointerButton` (`Left` default, `Any`, `Right`, `Middle`) filters Down, Up, and Click.
3. **Interaction** (Evo UI): `trigger = TriggerMode.Interaction`, `interactionStates` flags (`Normal`, `Highlighted`, `Pressed`, `Selected`, `Disabled`), and set the animator's `interactionSource` to a component implementing `Evo.UI.IStylerInteractable` (Evo UI Button, Switch, Toggle, Slider, ...). Keyboard and gamepad focus counts as `Highlighted`, the same as hover. `Selected` is a persistent state set with `SetState(InteractionState.Selected)`, not controller focus.
4. `restartOnTrigger` decides whether re-triggering the current state replays it. The first matching state in the list wins. Call `RebindTriggers()` after changing triggers or the source from code at runtime.

Hover scale on an Evo UI button (states `Normal` and `Highlighted`):

```csharp
// Needs: using Evo.Tween; using Evo.Tween.Modules.Transforms; and the Clip helper from the Quick start.
// After creating two states as in the Quick start (index 0 "Normal", index 1 "Highlighted"):
animator.interactionSource = button;          // Evo.UI.Button
animator.playOnEnable = false;

var normal = animator.GetState(0);
normal.trigger = TweenAnimator.TriggerMode.Interaction;
normal.interactionStates = TweenAnimator.InteractionTrigger.Normal;
normal.sequence.InlineClips.Add(Clip(new TransformScaleModule { fromCurrent = true, to = Vector3.one }, 0.15f));

var highlighted = animator.GetState(1);
highlighted.trigger = TweenAnimator.TriggerMode.Interaction;
highlighted.interactionStates = TweenAnimator.InteractionTrigger.Highlighted | TweenAnimator.InteractionTrigger.Selected;
highlighted.sequence.InlineClips.Add(Clip(new TransformScaleModule { fromCurrent = true, to = Vector3.one * 1.05f }, 0.15f));
```

Put this Tween Animator on a **child visual** of the button (and set the clips' `Target` to that child's Transform) when the button sits in a layout group, so the hit area and layout stay fixed. For a simple per-state scale, Evo UI's built-in **Interactive > Scale** effect (`button.scaleEffect`) does the same without a Tween Animator. The Button template ships with it on (1.05 when highlighted), so check `button.scaleEffect.enabled` first and don't stack a Tween Animator scale on top of it.

**Expected result:** in Play Mode, hovering the button or focusing it with a keyboard or gamepad scales it to 1.05, and leaving returns it to 1, continuing smoothly from the current scale.

### Workflow: Control playback from code

**Goal:** Drive states from gameplay.

**Steps:**
1. `Play()` restarts the default state; `Play("Show")` or `Play(index)` restarts that state. `Trigger("Show")` respects **Restart On Trigger**.
2. `Pause()`, `Resume()`, `TogglePause()`, `Restart()`, `Stop()` (uses the state's **Restore On Stop**), `Stop(false)` (keep current values), `Complete()` (jump to the end).
3. Read `IsPlaying`, `IsPaused`, `State`, `ActiveStateIndex`, `ActiveState`, `Duration`, `CurrentTime`, `NormalizedTime`; `GetStateIndex(id)` returns -1 for unknown IDs.
4. C# events: `Started`, `Paused`, `Resumed`, `Stopped`, `Completed` (each `Action<TweenAnimator>`), `Updated` (`Action<TweenAnimator, float>` with normalized progress). Unsubscribe in `OnDisable`.
5. After editing state data at runtime, call `RefreshState(index)` or `PrepareStates()` (stops playback).

```csharp
using UnityEngine;
using Evo.Tween;

public class DamageFeedback : MonoBehaviour
{
    [SerializeField] private TweenAnimator animator;

    private void OnEnable() => animator.Completed += OnFinished;
    private void OnDisable() => animator.Completed -= OnFinished;

    public void OnHit() => animator.Play("Shake");
    private void OnFinished(TweenAnimator a) => Debug.Log($"{a.ActiveState.id} finished");
}
```

**Expected result:** calling `OnHit()` in Play Mode plays `Shake` from the start and logs when it completes.

### Workflow: Loops and timing settings

**Goal:** Loop, pause-proof, or physics-safe animations.

**Steps:**
1. Whole-state loops: set the state's `loop` to `LoopMode.Restart` or `Yoyo` and `iterations` to `0` for infinite (Yoyo counts each direction as one iteration). Clip loops are always finite.
2. Pause menus: set the sequence's **Time Mode** to **Unscaled** (`state.sequence.TimeMode = TimeMode.Unscaled`).
3. Physics targets (Rigidbody modules): set the state's **Update Phase** to **Fixed Update**.
4. `playbackSpeed`: `1` normal, `0` holds.
5. **Restore On Stop** (default on) puts captured values back when the state is stopped or the component is disabled. Normal completion keeps the final values. Turn it off for "Hide" states that should stay hidden.

**Expected result:** a bobbing icon uses `Yoyo` with `iterations = 0` and repeats forever in Play Mode.

## Verification

- `GetComponent<Evo.Tween.TweenAnimator>()` exists, and only one per GameObject.
- `animator.States.Count` and each `GetStateIndex(id)` match the intended states; IDs are unique.
- Each state's `sequence.Count > 0`, each clip has `Settings.Module != null`, and explicit targets are non-null where children are animated.
- Clip targets exist: a Canvas Group Alpha clip needs a `CanvasGroup` on the owner (or as `Target`).
- Interaction states: `animator.interactionSource is Evo.UI.IStylerInteractable`.
- In Play Mode: after `Play("Show")`, `IsPlaying` is true and, after `Duration`, the target values equal the clips' targets.
- No Console errors.

## API quick reference

| Entry point | Type | What it does |
| --- | --- | --- |
| `Evo.Tween.TweenAnimator` | Component | `Play()`, `Play(id)`, `Trigger(id)`, `Stop(restore)`, `Pause()`, `Resume()`, `Complete()`, `GetState(i)`, `GetStateIndex(id)`, `playOnEnable`, `interactionSource`. |
| `TweenAnimator.AnimationState` | Class | `id`, `trigger`, `pointerEvents`, `interactionStates`, `restartOnTrigger`, `loop`, `iterations`, `playbackSpeed`, `updatePhase`, `restoreOnStop`, `sequence`. |
| `Evo.Tween.TweenSequence` | Class | `InlineClips`, `Clips`, `Count`, `Duration`, `TimeMode`, `Preset`. |
| `Evo.Tween.TweenClip` | Class | `Target`, `Flow`, `Settings` (`Module`, `Duration`, `Delay`, `Ease`, `Loop`, `Iterations`). |
| Module classes (`TransformScaleModule`, `CanvasGroupAlphaModule`, ...) | Classes | `fromCurrent`, `from`, `to` per property. |

Full reference: https://evo.michsky.com/docs/evo-tween/tween-animator and https://evo.michsky.com/docs/evo-tween/tween-clip.

## Common issues

| Symptom | Cause | Fix |
| --- | --- | --- |
| `Play("show")` does nothing. | IDs are case-sensitive, or the call ran outside Play Mode or on a disabled component. | Use the exact ID; check `GetStateIndex(id) >= 0`; call in Play Mode. |
| A clip has no visible effect. | Its target is empty and the owner lacks that component, or From equals Target. | Assign the target (children aren't searched); check values. |
| The object snaps when an animation is interrupted. | From Current is off. | Turn on **From Current** for interruptible transitions. |
| The object resets when stopped or disabled. | **Restore On Stop** is on (default). | Turn it off for that state, or call `Stop(false)`. |
| Nothing animates during the pause menu. | Scaled time with `Time.timeScale = 0`. | Set the sequence **Time Mode** to **Unscaled**. |
| Hover states don't fire. | No EventSystem/input module or raycast target (Pointer Event), or the Interaction source isn't an Evo UI control. | Fix input setup; assign an `IStylerInteractable` source; `RebindTriggers()` after runtime changes. |
| The animation fights a layout group, Styler, or another animation. | Two systems write the same property. | Animate a child visual, use Slide/Layout Size modules, or give the property one owner. |
| A runtime edit to a state doesn't show. | States are prepared ahead of time. | `RefreshState(index)` or `PrepareStates()`. |
| Can't add a second Tween Animator. | One per GameObject. | Add another state, or put the second animator on a child. |

## Boundaries

- Tween Animator plays property tweens; it isn't a replacement for Unity's Animator with skeletal animation, blend trees, or animation clips.
- Clip loops are finite; infinite repetition is a state setting (`iterations = 0`).
- Only one state plays at a time per animator; switching states keeps the values on screen.
- Interaction triggers require Evo UI controls (`IStylerInteractable`); a standard Unity Button needs Pointer Event triggers or UnityEvent calls instead.
- Custom properties need a custom module (https://evo.michsky.com/docs/evo-tween/for-developers).
