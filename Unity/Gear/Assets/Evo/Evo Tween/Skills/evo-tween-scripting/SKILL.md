---
name: evo-tween-scripting
description: "Use this skill whenever the user wants to animate or tween something from C# with Evo Tween — e.g. 'tween this panel's alpha from code', 'move the object to this position over 0.5 seconds', 'fade out then disable', 'animate the score value', 'tween the light intensity', 'ping pong forever', 'cancel the tween when the object is destroyed', 'tween not working when the game is paused', 'replace DOTween / LeanTween calls'. Covers EvoTween.Create, the BindTo... bindings (Transform, UI, TMP, audio, camera, light, material, volume, physics, video, Procedural Shape), builder options (ease, delay, loops, yoyo, speed, time mode, update phase, OnComplete), TweenHandle control, custom value binding, and lifetime and conflict rules. Do NOT use for Inspector-authored animations (see evo-tween-tween-animator) or presets and the Timeline Window (see evo-tween-sequence-presets). When in doubt whether a code animation request could involve Evo Tween, use this skill — Prerequisites shows how to confirm the asset is installed."
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

# Tween Values from Code with Evo Tween

`EvoTween.Create(from, to, duration)` returns a value-type builder. Configure it with `With...` methods, then call a binding such as `BindToScale(transform)`. The binding starts playback and returns a `TweenHandle` for pausing, completing, or cancelling. Tween instances are pooled, so steady-state playback doesn't allocate. This skill covers the builder, every binding family, handle control, and the lifetime rules that prevent bugs.

## When to use this skill

- "fade this CanvasGroup in over 0.2s from code"
- "move the enemy to the waypoint with an ease"
- "punch-scale the button when clicked"
- "count the gold value up and write it to a label"
- "tween the camera FOV for a zoom"
- "pulse the light intensity forever"
- "stop the tween when the object is disabled"
- "the tween doesn't run while paused"
- "port these DOTween calls to evo tween"

Not for:
- Animations authored in the Inspector with states and triggers — see `evo-tween-tween-animator`.
- Reusable Sequence Presets and the Timeline Window — see `evo-tween-sequence-presets`.
- Module availability and setup — see `evo-tween-setup-and-overview`.

## Prerequisites

- Evo Tween 2.0.0+ (ships with Evo UI) on Unity 6000.0+.
- **Install check:** `System.Type.GetType("Evo.Tween.EvoTween, Evo.Tween") != null`. If it fails, tell the user Evo Tween isn't installed; it comes with Evo UI (https://assetstore.unity.com/packages/slug/410788).
- Add `using Evo.Tween;`. All bindings are extension methods in that namespace.
- Binding families need their modules: UI and TMP bindings need `com.unity.ugui` 2.0.0+; Volume bindings need Render Pipelines Core; Rigidbody bindings need the physics modules; Procedural Shape bindings need Evo UI. See `evo-tween-setup-and-overview`.
- Tweens run in Play Mode only.

## Quick start

Fade a menu in and out, safe to call repeatedly and during a pause.

```csharp
using UnityEngine;
using Evo.Tween;

public class MenuFade : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    private TweenHandle fade;

    public void Show()
    {
        fade.Kill(); // Safe on a default or finished handle.
        group.blocksRaycasts = true;
        fade = EvoTween.Create(group.alpha, 1f, 0.2f)   // Start from the current value.
            .WithEase(Ease.OutCubic)
            .WithTimeMode(TimeMode.Unscaled)              // Keeps running at Time.timeScale = 0.
            .BindToAlpha(group);
    }

    public void Hide()
    {
        fade.Kill();
        group.blocksRaycasts = false;                     // Alpha alone doesn't stop input.
        fade = EvoTween.Create(group.alpha, 0f, 0.15f)
            .WithTimeMode(TimeMode.Unscaled)
            .BindToAlpha(group);
    }

    private void OnDisable() => fade.Kill();
}
```

Expected result: in Play Mode, `Show()` raises `group.alpha` to 1 in 0.2 s and `Hide()` lowers it to 0 in 0.15 s, even while paused; `fade.IsActive` is true during each fade.

## Workflows

### Workflow: Create, configure, and bind

**Goal:** Start a tween on a property.

**Steps:**
1. `EvoTween.Create<T>(T from, T to, float duration)`. Supported `T`: `float`, `int` (rounded), `bool` (switches at eased progress 0.5), `Vector2`, `Vector3`, `Vector4`, `Color`, `Quaternion` (spherical interpolation).
2. The code API uses the `from` you pass. To start from the current value, read it (`transform.localScale`, `group.alpha`) when you create the tween.
3. Configure (each returns an updated copy, so chain them or reassign):

| Method | Default | Notes |
| --- | --- | --- |
| `WithEase(Ease ease, AnimationCurve customCurve = null)` | `Ease.Linear` | Serialized clips default to OutCubic; code defaults to Linear. `Ease.Custom` uses the curve. |
| `WithDelay(float seconds)` | 0 | Waits before interpolating. |
| `WithLoops(int iterations, LoopMode loop = LoopMode.Restart)` | 1 iteration | `0` = infinite with Restart or Yoyo. Yoyo counts each direction: `WithLoops(2, LoopMode.Yoyo)` is out and back. |
| `WithSpeed(float speed)` | 1 | Playback multiplier. |
| `WithTimeMode(TimeMode mode)` | `Scaled` | `Unscaled` for pause menus. |
| `WithUpdatePhase(UpdatePhase phase)` | `Update` | `LateUpdate`, or `FixedUpdate` for Rigidbody bindings (not automatic). |
| `OnComplete(Action callback)` | none | Called on normal completion and on `Complete()`, not on `Kill()`. |

4. Bind. The binding starts playback and returns a `TweenHandle`.

```csharp
TweenHandle h = EvoTween.Create(Vector3.one * 0.8f, Vector3.one, 0.25f)
    .WithEase(Ease.OutBack)
    .BindToScale(transform);
```

**Expected result:** the property animates and `h.IsActive` is true until it finishes.

### Workflow: Pick a binding

**Goal:** Find the `BindTo...` method for the target.

**Steps:**
1. Find the target component type in the table.
2. Create the tween with the binding's value type (`EvoTween.Create<float>`, `<Vector2>`, `<Color>`, ...).
3. Call the binding with the component. If no row fits, use `Bind(...)` (see *Animate a custom value*).

| Target type | Bindings (value type) |
| --- | --- |
| `Transform` | `BindToPosition`, `BindToLocalPosition` (`Vector3`); `BindToRotation`, `BindToLocalRotation` (`Quaternion`, use `Quaternion.Euler`); `BindToScale` (`Vector3`, local) |
| `RectTransform` | `BindToAnchoredPosition`, `BindToSlide` (offset from the captured position), `BindToSizeDelta`, `BindToLayoutSize` (multipliers), `BindToPivot`, `BindToAnchorMin`, `BindToAnchorMax`, `BindToOffsetMin`, `BindToOffsetMax` (`Vector2`) |
| `CanvasGroup` | `BindToAlpha` (`float`); `BindToInteractable`, `BindToBlocksRaycasts`, `BindToIgnoreParentGroups` (`bool`) |
| `Graphic` (Image, Text, Procedural Shape base color) | `BindToColor` (`Color`), `BindToAlpha` (`float`) |
| `Image` | `BindToFillAmount` (`float`) |
| `Slider`, `Scrollbar` | `BindToValue` (`float`) |
| `ScrollRect` | `BindToNormalizedPosition` (`Vector2`) |
| `LayoutElement` | `BindToPreferredSize`, `BindToMinimumSize`, `BindToFlexibleSize` (`Vector2`) |
| `TMP_Text` | `BindToColor` (`Color`); `BindToAlpha`, `BindToFontSize`, `BindToCharacterSpacing`, `BindToWordSpacing`, `BindToLineSpacing` (`float`); `BindToMaxVisibleCharacters` (`int`) |
| `AudioSource` | `BindToVolume`, `BindToPitch`, `BindToStereoPan`, `BindToSpatialBlend`, `BindToReverbZoneMix` (`float`) |
| `Camera` | `BindToFieldOfView`, `BindToOrthographicSize` (`float`), `BindToBackgroundColor` (`Color`) |
| `Light` | `BindToColor`; `BindToIntensity`, `BindToRange`, `BindToSpotAngle`, `BindToInnerSpotAngle`, `BindToShadowStrength`, `BindToColorTemperature`, `BindToBounceIntensity` (`float`) |
| `SpriteRenderer` | `BindToColor`, `BindToAlpha`, `BindToSize` (`Vector2`) |
| `Renderer` | `BindToMaterialFloat(renderer, "_Prop", materialIndex: 0)`, `BindToMaterialColor` (uses property blocks, no material instance) |
| `Volume` | `BindToWeight`, `BindToPriority`, `BindToBlendDistance` (`float`) |
| `Rigidbody` | `BindToPosition`, `BindToLinearVelocity`, `BindToAngularVelocity` (`Vector3`); `BindToRotation` (`Quaternion`) |
| `Rigidbody2D` | `BindToPosition`, `BindToLinearVelocity` (`Vector2`); `BindToRotation`, `BindToAngularVelocity` (`float`) |
| `VideoPlayer` | `BindToPlaybackSpeed`, `BindToTime`, `BindToTargetCameraAlpha` (`float`) |
| `Evo.UI.ProceduralShape` | `BindToFillColor`, `BindToFillAlpha`, `BindToOutlineWidth`, `BindToOutlineColor`, `BindToOutlineDashOffset`, `BindToCornerRadius` (`float` or `Vector4`), `BindToClipAmount`, `BindToShapeAngle`, `BindToSoftness`, `BindToOuterShadowSoftness`, `BindToOuterShadowColor`, and more per layer (see https://evo.michsky.com/docs/evo-tween/evo-ui) |

The `from`/`to` type must match the binding's value type, or the call doesn't compile.

**Expected result:** the chosen binding compiles and animates the property.

### Workflow: Control a running tween

**Goal:** Pause, finish, or cancel.

**Steps:**
1. Keep the `TweenHandle` in a field.
2. `IsActive` (alive, including paused), `IsPaused`, `Pause()`, `Resume()`, `Complete()` (jumps to the final value and invokes `OnComplete`), `Kill()` (cancels, keeps the current value, no callback). The `With...` methods are also available on the handle.
3. Handles are version-checked structs: a stale handle never controls a reused tween, and calling methods on a default handle is safe.
4. To replay, create a new tween; a finished tween can't be restarted.

```csharp
TweenHandle pulse = EvoTween.Create(1f, 3f, 0.6f)
    .WithEase(Ease.InOutSine)
    .WithLoops(0, LoopMode.Yoyo)   // Forever, back and forth.
    .BindToIntensity(lamp);        // UnityEngine.Light

// Later:
pulse.Kill();
```

**Expected result:** the light pulses between 1 and 3 until `Kill()`.

### Workflow: Sequence code tweens

**Goal:** Chain steps or run something after a tween.

**Steps:**
1. Use `OnComplete` to start the next step, or `WithDelay` to offset parallel tweens.
2. For multi-step choreography that designers will edit, use a Tween Animator state or a Sequence Preset instead (`evo-tween-tween-animator`, `evo-tween-sequence-presets`).

```csharp
void FadeOutAndDisable(CanvasGroup group)
{
    EvoTween.Create(group.alpha, 0f, 0.2f)
        .OnComplete(() => group.gameObject.SetActive(false))
        .BindToAlpha(group);
}
```

**Expected result:** the group fades out, then its GameObject is deactivated.

### Workflow: Animate a custom value

**Goal:** Tween something without a ready-made binding (a field, a shader global, a UI label).

**Steps:**
1. Simple setter: `EvoTween.Create(0f, 100f, 1f).Bind(v => score = v)`. It has no target lifetime, so `Kill()` it if the callback touches an object that can be destroyed.
2. Target-aware setter (no capturing lambda, stops when the target is destroyed, takes part in conflict handling): `Bind(target, static (t, v) => t.Value = v, propertyKey)` with a stable, unique `int` key per property on that target.

```csharp
using UnityEngine;
using Evo.Tween;

public class HealthBar : MonoBehaviour
{
    private const int FillKey = 10001; // Stable and unique for this property on this target.
    private TweenHandle tween;

    public float Fill { get; private set; } = 1f;

    public void SetHealth(float normalized)
    {
        tween = EvoTween.Create(Fill, Mathf.Clamp01(normalized), 0.3f)
            .WithEase(Ease.OutCubic)
            .Bind(this, static (bar, value) => bar.Fill = value, FillKey);
    }

    private void OnDisable() => tween.Kill();
}
```

**Expected result:** `Fill` eases to the new value; a second `SetHealth` call replaces the first tween because it uses the same target and key.

## Verification

- The script compiles with `using Evo.Tween;` and no CS1061 ("does not contain a definition for BindTo...").
- In Play Mode, the handle's `IsActive` is true right after binding, and false after `duration * iterations + delay`.
- The property ends at `to` (or `from` after an even number of Yoyo iterations).
- `Evo.Tween.TweenManager.Instance.AliveTweenCount` drops back after the tweens finish (no leaked infinite loops).
- The Profiler shows no per-frame GC from tweens after warm-up.

## API quick reference

| Entry point | Type | What it does |
| --- | --- | --- |
| `EvoTween.Create<T>(from, to, duration)` | Static method | Returns `TweenBuilder<T>`. |
| `TweenBuilder<T>.WithEase / WithDelay / WithLoops / WithSpeed / WithTimeMode / WithUpdatePhase / OnComplete` | Methods | Configure; each returns a copy. |
| `TweenBuilder<T>.BindTo...(target)` | Extension methods | Bind to a component property and start; return `TweenHandle`. |
| `TweenBuilder<T>.Bind(Action<T>)`, `Bind(target, setter, propertyKey)` | Methods | Custom setters. |
| `TweenHandle` | Struct | `IsActive`, `IsPaused`, `Pause()`, `Resume()`, `Complete()`, `Kill()`. |
| `Ease`, `LoopMode`, `TimeMode`, `UpdatePhase` | Enums | Easing (`Linear`, `OutCubic`, `InOutSine`, `OutBack`, `OutElastic`, `OutBounce`, ...), `None`/`Restart`/`Yoyo`, `Scaled`/`Unscaled`, `Update`/`LateUpdate`/`FixedUpdate`. |

Full reference: https://evo.michsky.com/docs/evo-tween/scripting and https://evo.michsky.com/docs/evo-tween/modules.

## Common issues

| Symptom | Cause | Fix |
| --- | --- | --- |
| CS1061: no `BindToAlpha` on `TweenBuilder<float>`. | Missing `using Evo.Tween;`, module not compiled, or wrong value type for that binding. | Add the using; check the module; match `T` (e.g. `float` for alpha, `Color` for color). |
| The tween doesn't run during a pause. | Scaled time. | `.WithTimeMode(TimeMode.Unscaled)`. |
| The tween keeps running after the object is disabled. | Disabling doesn't cancel code tweens (destroying the target does). | `Kill()` the handle in `OnDisable`. |
| Starting a new move cancels another tween on the same object. | Same target + property key cancels the previous tween (by design). | Expected; use different properties, or a child object. |
| The jump at the start of an interrupted tween. | `from` was a fixed value. | Pass the current value as `from`. |
| `OnComplete` didn't fire. | The tween was killed. | `Kill()` never invokes it; use `Complete()` to finish with the callback. |
| A Rigidbody tween jitters. | Running in Update. | `.WithUpdatePhase(UpdatePhase.FixedUpdate)`. |
| A faded-out panel still blocks clicks. | Alpha doesn't change input. | Set `blocksRaycasts`/`interactable`, or bind `BindToBlocksRaycasts`. |
| A plain `Bind(v => ...)` throws after a scene change. | Its lambda touches a destroyed object; plain setters have no target lifetime. | Use the target-aware overload or `Kill()` it on disable. |

## Boundaries

- No built-in tween sequencer object in code; chain with `OnComplete`/`WithDelay`, or use Tween Animator / Sequence Presets for editable choreography.
- Property conflict handling applies to target-bound code tweens with the same key; it doesn't coordinate with Tween Animator states, Unity Animators, or layout groups writing the same property.
- Physics tweens set body values; collisions and forces aren't simulated by the tween.
- Material bindings use MaterialPropertyBlocks; coordinate with other scripts that write blocks on the same renderer.
- For properties used in the Inspector clip menu too, write a custom module (https://evo.michsky.com/docs/evo-tween/for-developers).
