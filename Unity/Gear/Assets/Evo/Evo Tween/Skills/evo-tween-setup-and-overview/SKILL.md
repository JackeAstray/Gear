---
name: evo-tween-setup-and-overview
description: "Use this skill whenever the user wants to check, set up, or understand Evo Tween, Michsky's tweening and UI animation engine that ships with Evo UI — e.g. 'is evo tween installed', 'how do i animate things in this project', 'which tween library does this project use', 'can i tween a light / camera fov / volume weight', 'why is the TMP or volume tween method missing', 'what is the [EvoTween Manager] object', 'open the tween timeline'. Covers the install check, module assemblies and the packages that enable them, the automatic runtime manager, the editor tools, where files live, and an index of the other Evo Tween skills. Do NOT use for building a specific animation (see evo-tween-tween-animator for Inspector animations, evo-tween-scripting for code tweens, evo-tween-sequence-presets for reusable sequences and the Timeline Window). When in doubt whether an animation or tweening request could involve Evo Tween, use this skill — Prerequisites shows how to confirm the asset is installed."
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

# Set Up Evo Tween

Evo Tween animates values over time: transforms, UI, TextMeshPro, audio, cameras, lights, materials, volumes, physics bodies, video, and Evo UI Procedural Shapes. Animations are built in the Inspector with **Tween Animator**, arranged in the **Tween Timeline** window, shared through **Sequence Preset** assets, or started from code with `EvoTween.Create(...)`. It reuses tween instances so steady-state playback doesn't allocate. This skill confirms it is installed, explains which modules are available, and points to the right skill for each job.

## When to use this skill

- "is evo tween in this project?"
- "what's the best way to animate ui here?"
- "can evo tween animate my light intensity / camera fov / post-processing volume?"
- "`BindToFontSize` doesn't exist" / "`BindToWeight` is missing"
- "what's the [EvoTween Manager] object in my hierarchy?"
- "open the tween timeline"

Not for:
- Building an animation in the Inspector — see `evo-tween-tween-animator`.
- Tweens from code — see `evo-tween-scripting`.
- Reusable presets and the Timeline Window — see `evo-tween-sequence-presets`.
- Evo UI controls themselves — see `evo-ui-setup-and-overview`.

## Prerequisites

- Evo Tween 2.0.0 or newer (first line of `Assets/Evo/Evo Tween/README.md` reads `## Evo Tween 2.0.0`). It ships inside the Evo UI package and Evo UI depends on it.
- Unity 6.0 (6000.0) or newer, any render pipeline.
- **Install check (run before anything else):**

```csharp
using UnityEngine;

public static class EvoTweenInstallCheck
{
    public static void Run()
    {
        bool core = System.Type.GetType("Evo.Tween.EvoTween, Evo.Tween") != null;
        bool ugui = System.Type.GetType("Evo.Tween.UGUITweenExtensions, Evo.Tween.Modules.UGUI") != null;
        bool evoUI = System.Type.GetType("Evo.Tween.ProceduralShapeTweenExtensions, Evo.Tween.Modules.EvoUI") != null;
        Debug.Log($"Evo Tween: {core}, UI/TMP module: {ugui}, Evo UI module: {evoUI}");
    }
}
```

You can also confirm the menu **Tools > Evo Tween > Tween Timeline** exists, or that **Add Component > Evo > Tween > Tween Animator** is listed.

If `Evo Tween` is false, tell the user: "Evo Tween isn't installed. It ships with Evo UI; import Evo UI from the Asset Store (https://assetstore.unity.com/packages/slug/410788) through **Window > Package Management > Package Manager > My Assets**."

## Quick start

Make an object pop in with no code.

1. Select a GameObject and add **Add Component > Evo > Tween > Tween Animator**. It starts with one state, `Default`, and **Play On Enable** on.
2. In the state, click **Add Clip** and choose **Transform > Scale**. Leave the clip's target empty to animate this object's Transform.
3. Turn off **From Current**, set **From** to `(0.8, 0.8, 0.8)` and **Target** to `(1, 1, 1)`, **Duration** `0.25`, **Ease** `Out Cubic`.
4. Use the Inspector preview controls, or enter Play Mode.

Expected result: on entering Play Mode the object scales from 0.8 to 1 in 0.25 s. The Hierarchy (in Play Mode) contains `[EvoTween Manager]`, created automatically.

To build the same thing from an editor script, see `evo-tween-tween-animator`.

## Workflows

### Workflow: Check which modules are available

**Goal:** Know which properties can be animated in this project.

**Steps:**
1. Modules compile only when their Unity package or module is present. Check `Packages/manifest.json` or use the type checks below.

| Module | Assembly | Needs | Animates |
| --- | --- | --- | --- |
| Core, Transform, Audio, Rendering | `Evo.Tween` | always | Position, rotation, scale; AudioSource volume, pitch, pan, spatial blend, reverb mix; Camera FOV, ortho size, background; Light color, intensity, range, spot angles, shadow strength, temperature, bounce; SpriteRenderer color, alpha, size; Renderer material float/color |
| UI and TextMeshPro | `Evo.Tween.Modules.UGUI` | `com.unity.ugui` 2.0.0+ (define `EVO_UGUI`) | RectTransform anchored position, slide, size, layout size, pivot, anchors, offsets; CanvasGroup alpha and flags; Graphic color/alpha; Image fill; Slider/Scrollbar value; ScrollRect position; LayoutElement sizes; TMP color, alpha, font size, spacing, max visible characters |
| Physics | `Evo.Tween.Modules.Physics` | `com.unity.modules.physics` (`EVO_PHYSICS`) | Rigidbody position, rotation, velocities |
| Physics 2D | `Evo.Tween.Modules.Physics2D` | `com.unity.modules.physics2d` (`EVO_PHYSICS_2D`) | Rigidbody2D position, rotation, velocities |
| Render Pipelines | `Evo.Tween.Modules.RenderPipelines` | `com.unity.render-pipelines.core` (`EVO_RP_CORE`, present with URP/HDRP) | Volume weight, priority, blend distance |
| Video | `Evo.Tween.Modules.Video` | `com.unity.modules.video` (`EVO_VIDEO`) | VideoPlayer speed, time, camera alpha |
| Evo UI | `Evo.Tween.Modules.EvoUI` (in `Assets/Evo/Evo UI/Scripts/Integrations/Evo Tween`) | Evo UI | Procedural Shape properties; Tween Animator **Interaction** triggers |

2. The defines are set automatically by the assemblies' version defines; never add them to Player Settings by hand.
3. If the module the user needs is missing, ask before installing its package. Installing URP, HDRP, or `com.unity.render-pipelines.core` changes the whole project, and Volumes have no effect until a URP or HDRP asset is assigned in **Project Settings > Graphics**. If the user doesn't want that, suggest animating something that is available.

**Expected result:** the user knows whether the property they want has a module, or needs a custom one (https://evo.michsky.com/docs/evo-tween/for-developers).

### Workflow: Choose the right tool

**Goal:** Route an animation request.

**Steps:**
1. Match the request to a row below.
2. Open the named skill and follow its Quick start or workflow.

| The user wants | Use | Skill |
| --- | --- | --- |
| An animation authored in the Inspector: entrance, hover, press, loop, named states | **Tween Animator** component | `evo-tween-tween-animator` |
| A tween started from gameplay code, a custom value, a one-off move | `EvoTween.Create(...).BindTo...()` | `evo-tween-scripting` |
| The same animation on many objects, or visual timing edits | **Sequence Preset** + **Tween Timeline** | `evo-tween-sequence-presets` |
| To change how an Evo UI control animates (dropdown open, modal open, tab switch) | That component's own Tween Sequence fields | `evo-tween-sequence-presets` |
| Hover, press, or selected looks on Evo UI controls driven by their state | Tween Animator **Interaction** trigger, or Interactive's **Scale** effect | `evo-tween-tween-animator` |

**Expected result:** the request is handed to the matching skill.

### Workflow: Inspect running tweens

**Goal:** Debug what is animating at runtime.

**Steps:**
1. In Play Mode, select `[EvoTween Manager]` in the Hierarchy (it is created before the first scene and survives scene loads). Never add it to scenes or prefabs yourself.
2. The **Runtime** section lists alive tweens with target, status, update phase, time mode, speed, progress, delay, and loops. **Pools** shows reuse counters.
3. From code: `var manager = Evo.Tween.TweenManager.Instance;` (null outside Play Mode), then `manager.AliveTweenCount`, `PooledTweenCount`, `CreatedTweenCount`, `ReusedTweenCount`, and `manager.GetAliveTween(index)` (check `IsValid` on the result).

**Expected result:** the tween in question appears in the list (or doesn't, which means it was never started or already finished).

## Verification

- `System.Type.GetType("Evo.Tween.EvoTween, Evo.Tween") != null`.
- For UI or TMP bindings: `System.Type.GetType("Evo.Tween.UGUITweenExtensions, Evo.Tween.Modules.UGUI") != null`.
- **Tools > Evo Tween > Tween Timeline** opens a window titled **Tween Timeline**.
- In Play Mode, `Evo.Tween.TweenManager.Instance != null` and a `[EvoTween Manager]` object exists.
- No compile errors in the Console.

## API quick reference

| Entry point | Type | What it does |
| --- | --- | --- |
| `Evo.Tween.EvoTween.Create<T>(from, to, duration)` | Static method | Starts a code tween builder; bind with `BindTo...()` or `Bind(...)`. |
| `Evo.Tween.TweenAnimator` | Component (**Add Component > Evo > Tween > Tween Animator**) | Named animation states with Inspector-authored clips. |
| `Evo.Tween.TweenSequencePreset` | ScriptableObject (**Create > Evo > Tween > Sequence Preset**) | Shared clip definitions. |
| `Tools > Evo Tween > Tween Timeline` | Menu / window | Visual sequence editor with preview and recording. |
| `Evo.Tween.TweenManager.Instance` | Static property | Runtime manager and pool counters (Play Mode). |
| `Evo.Tween.Ease` | Enum | `Linear`, `In/Out/InOut` + `Sine`, `Quad`, `Cubic`, `Quart`, `Quint`, `Expo`, `Circ`, `Back`, `Elastic`, `Bounce`, and `Custom`. |

Other skills:
- `evo-tween-tween-animator` — Tween Animator states, clips, triggers, and playback control.
- `evo-tween-scripting` — `EvoTween.Create`, bindings, `TweenHandle`, loops, easing, custom values.
- `evo-tween-sequence-presets` — Sequence Presets, the Tween Timeline window, and Evo UI component sequences.
- `evo-ui-setup-and-overview` — the Evo UI component library that uses Evo Tween.

## Common issues

| Symptom | Cause | Fix |
| --- | --- | --- |
| `BindToAlpha` / `BindToFontSize` / `BindToAnchoredPosition` not found. | Missing `using Evo.Tween;`, or the UGUI module isn't compiled (no `com.unity.ugui` 2.0.0+). | Add `using Evo.Tween;`; make sure Unity UI is installed. |
| `BindToWeight` (Volume) not found. | Render Pipelines Core isn't installed (Built-in project). | Ask before installing URP/HDRP or `com.unity.render-pipelines.core` (a project-wide change; Volumes do nothing until a URP or HDRP asset is assigned). Otherwise, animate something else. |
| Procedural Shape modules or Interaction triggers are missing. | Evo UI's integration assembly isn't present. | Keep `Assets/Evo/Evo UI/Scripts/Integrations/Evo Tween` in the project. |
| A duplicate `[EvoTween Manager]` was saved in a scene. | Someone copied it during Play Mode. | Delete the saved copy; the manager creates itself. |
| Animations don't play in Edit Mode from code. | `Play` and code bindings need Play Mode. | Use the Inspector or Timeline preview in Edit Mode. |

## Boundaries

- Evo Tween animates component properties; it doesn't replace Unity's Animator for skeletal or keyframed character animation.
- It doesn't use Unity Timeline or Playable Director; the Tween Timeline window is its own editor.
- Physics tweens set body values; they don't simulate forces or collisions. Use FixedUpdate for them.
- Video and audio tweens change properties only; start playback separately.
- For a property no module covers, write a custom module (https://evo.michsky.com/docs/evo-tween/for-developers) or use `Bind(Action<T>)` in code.
