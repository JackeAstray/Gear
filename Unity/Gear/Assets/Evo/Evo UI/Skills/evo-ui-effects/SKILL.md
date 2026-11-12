---
name: evo-ui-effects
description: "Use this skill whenever the user wants a visual effect on their UI with Evo UI — e.g. 'blur the game behind the pause menu', 'frosted glass panel', 'fade out the edges of my scroll view', 'make the text wave / shake / type out like a typewriter', 'glow on the border when hovering', 'ripple when clicking buttons', 'light that follows the mouse', 'curve the hud', 'tilt the card when hovering'. Covers Backdrop Blur (component and Procedural Shape fill), Soft Mask, Text Effects on TMP text, Border Glow, Ripple, and Spotlight on Interactive controls or standalone, plus an index of the other effect components (Curved Canvas, Rect Sway, Rect Parallax, Dock Magnifier, Stroke Dash, Warning Stripe, and more). Do NOT use for shapes, outlines, and shadows (see evo-ui-procedural-shape) or motion (see evo-tween-tween-animator). When in doubt whether a UI effect request could involve Evo UI, use this skill — Prerequisites shows how to confirm the asset is installed."
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
  last-verified: "2026-11-05"
---

# Add Visual Effects with Evo UI

Evo UI ships effect components for uGUI: **Backdrop Blur** (blurred camera image behind a panel), **Soft Mask** (masks with soft edges), **Text Effects** (character animation on TextMesh Pro text), and pointer feedback (**Border Glow**, **Ripple**, **Spotlight**) that can run on any Interactive control. This skill shows how to add and configure them, and lists the other effects with their components.

## When to use this skill

- "blur the gameplay behind the pause menu"
- "glassy blurred sidebar"
- "fade the top and bottom of the scroll list"
- "mask this panel with soft rounded edges"
- "dialogue text that types out letter by letter"
- "make the word LEGENDARY wave and shimmer"
- "glow along the button border near the cursor"
- "material ripple when clicking"
- "cursor spotlight on cards"
- "bend the HUD like a curved screen"

Not for:
- Corners, outlines, shadows, gradients — see `evo-ui-procedural-shape`.
- Moving, scaling, or fading objects over time — see `evo-tween-tween-animator`.
- Hover and press color changes — see `evo-ui-styler-themes`.

## Prerequisites

- Evo UI 2.0.0+ on Unity 6000.0+. Built-in, URP, and HDRP are supported, including Backdrop Blur (no Renderer Feature or Custom Pass volume to set up).
- **Install check:** `System.Type.GetType("Evo.UI.SoftMask, Evo.UI") != null`, or confirm **Add Component > Evo > UI > Effects** lists Soft Mask. If it fails, tell the user Evo UI isn't installed and link https://assetstore.unity.com/packages/slug/410788.
- Text Effects and Smooth TMP Gradient require TextMesh Pro text, whatever text provider is selected.
- Backdrop Blur is not supported in XR/VR (stereo cameras are skipped).

All effect components are under **Add Component > Evo > UI > Effects**: Backdrop Blur, Border Glow, Button Group Dimmer, Curved Canvas, Dock Magnifier, Dot Matrix Loader, Image Gradient, Ink Panel, Pixel Curtain, Rect Parallax, Rect Sway, Ripple, Smooth TMP Gradient, Soft Mask, Spotlight, Stroke Dash, Text Effects (TMP), Topographic Skin, Warning Stripe.

## Quick start

Blur the game behind a pause panel.

1. The blur samples the camera image (the 3D or 2D scene, plus Screen Space - Camera and World Space UI). Screen Space - Overlay UI is never in the capture, so another overlay panel behind the pause menu won't appear blurred.
2. Select the panel's Procedural Shape background (or create one with **GameObject > Evo UI > Procedural Shape**) and enable blur:

```csharp
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class EvoBlurPanel
{
    public static void Run(Evo.UI.ProceduralShape background)
    {
        Undo.RecordObject(background, "Enable Backdrop Blur");
        background.BackdropBlurEnabled = true;

        var blur = background.BackdropBlurSettings;
        blur.resolution = Evo.UI.BackdropBlur.Resolution.Half;
        blur.iterations = 3;     // 1–5
        blur.radius = 3f;        // 0.5–6
        blur.sourceCamera = Camera.main; // Assign explicitly in multi-camera scenes.
        blur.brightness = 0.85f; // Below 1 darkens what the panel shows.
        background.UpdateRect(); // Apply in-place edits to the settings object.

        // The blur is multiplied by the fill, so keep the fill white and opaque.
        background.fillColorMode = Evo.UI.ProceduralShape.ColorMode.Custom;
        background.FillColor = Color.white;
        background.UpdateRect();
        EditorSceneManager.MarkSceneDirty(background.gameObject.scene);
    }
}
```

Expected result: in Play Mode (and the Game view), the panel shows a blurred, slightly darkened copy of the scene behind it, with the panel's rounded corners. `background.BackdropBlurEnabled == true`. For a colored tint, put a second Procedural Shape with a translucent fill on top of the panel; a translucent fill on the blurred shape itself lets the sharp scene show through.

## Workflows

### Workflow: Backdrop Blur component

**Goal:** Blur behind an Image-style rectangle (sprite alpha defines the shape).

**Steps:**
1. Add **Evo > UI > Effects > Backdrop Blur** to a UI object. It is an `Image`, so it replaces any Image on that object; set its sprite and `color`. The blur is multiplied by `color`, so keep its alpha at 1.
2. Configure `settings` (`BackdropBlur.Settings`): `enabled` (default true), `sourceCamera` (null resolves the Canvas camera, then Main Camera), `resolution` (`Full`, `Half` default, `Quarter`, `Eighth`), `updateInterval` (camera renders between captures, default 1), `iterations` (default 2), `radius` (default 3), `saturation` (0 gray – 1 unchanged), `brightness` (1 unchanged), `noise`.
3. Call `Refresh()` after changing settings from code. `IsReady` reports a usable capture; `ResolvedCamera` shows the camera in use.
4. Prefer lower `resolution` and fewer `iterations` on mobile; raise `updateInterval` for static backgrounds.

**Expected result:** `blur.IsReady` becomes true in Play Mode and the rectangle shows the blurred scene.

### Workflow: Soft Mask

**Goal:** Clip children with soft edges, such as a scroll view that fades at the top and bottom.

**Steps:**
1. Add **Evo > UI > Effects > Soft Mask** to the Graphic that defines the mask area (it requires a Graphic: Image, Procedural Shape, etc.). Everything below it in the hierarchy is masked.
2. The mask follows the Graphic's shape: an Image's sprite alpha (use a sprite with soft, faded edges for a fade), or a Procedural Shape's geometry, corners, and `Softness` (edge softness in pixels).
3. `ShowMaskGraphic` (show or hide the mask Graphic itself), `InvertMask` (mask the inside instead). Up to four nested Soft Masks (`SoftMask.MaxMaskDepth`). A single mask with a sprite or an unclipped rectangle or ellipse shape is the cheap case on mobile; nested masks, and masks from clipped or other shapes, use a heavier evaluator with its own first-use shader compile.
4. For a scroll view, put Soft Mask on the **Viewport** and remove or disable its `RectMask2D`/`Mask` if you want only the soft edge.
5. Call `Refresh()` after hierarchy changes that must apply immediately.

```csharp
// Editor script; the viewport's Graphic must be a Procedural Shape (replace an Image first, see evo-ui-procedural-shape).
GameObject viewport = scroll.viewport.gameObject;
var shape = viewport.GetComponent<Evo.UI.ProceduralShape>();
Undo.RecordObject(shape, "Soft Viewport");
shape.Softness = 24f;

// Don't use ?? with Unity components: a missing component is a fake null in the Editor.
var mask = viewport.GetComponent<Evo.UI.SoftMask>();
if (mask == null)
    mask = Undo.AddComponent<Evo.UI.SoftMask>(viewport);

mask.ShowMaskGraphic = false;
mask.Refresh();
```

**Expected result:** items in the viewport fade out over about 24 px at the edges instead of being cut hard.

### Workflow: Text Effects on TMP text

**Goal:** Animate characters: typewriter, scramble, palette, alpha pulse, shimmer, wave, bounce, shake, glitch, pulse, swing, wiggle.

**Steps:**
1. Add **Evo > UI > Effects > Text Effects (TMP)** next to a `TMP_Text` component (it requires one).
2. Each effect is a settings object on a read-only property (`Typewriter`, `Scramble`, `Palette`, `AlphaPulse`, `Shimmer`, `Wave`, `Bounce`, `Shake`, `Glitch`, `Pulse`, `Swing`, `Wiggle`) with `enabled` and `scope` (`TextEffects.EffectScope.EntireText` or `TaggedCharacters`).
3. For tagged scope, wrap words in the effect's tag, e.g. `<wave>these words</wave>`, and keep TMP **Rich Text** on. The tag alone doesn't enable the effect.
4. Set text with `SetText(string value, bool startTypewriter = false, bool startScramble = false)`. Control the typewriter with `PlayTypewriter()`, `SkipTypewriter()`, and `IsTyping`; `Typewriter.speed` is characters per second (default 30). `UseUnscaledTime` keeps effects running during a pause. Call `Refresh()` after editing settings.

```csharp
using UnityEngine;

public class DialogueLine : MonoBehaviour
{
    [SerializeField] private Evo.UI.TextEffects effects;

    public void Show(string line)
    {
        effects.Wave.enabled = true;
        effects.Wave.scope = Evo.UI.TextEffects.EffectScope.TaggedCharacters;
        effects.Typewriter.enabled = true;
        effects.Typewriter.speed = 35f;
        effects.SetText(line, startTypewriter: true); // e.g. "You found the <wave>Moonblade</wave>!"
    }

    public void OnSkipPressed()
    {
        if (effects.IsTyping)
            effects.SkipTypewriter();
    }
}
```

**Expected result:** in Play Mode the line types out at 35 characters per second and the tagged word waves.

### Workflow: Pointer feedback on controls (Ripple, Spotlight, Border Glow)

**Goal:** Add click ripples, a cursor-following light, or an edge glow.

**Steps:**
1. On an Evo UI control (anything deriving from `Interactive`, such as Button), enable the settings objects on the control itself: `ripple`, `spotlight`, `borderGlow`. Each has `enabled`, `targetGraphic` (the Graphic it draws in, usually the background Procedural Shape), `color`, and `ignoreGraphicAlpha` (keep the effect visible on transparent or ghost backgrounds).
2. Ripple settings: `coverage` (default 1.5), `softness`, `centered`, `duration`, `fadeOutDuration` (up to 8 at once). Spotlight: `visibility` (`HoverOnly` / `Always`), `radius` (default 100), `softness`, `followDuration`, fades. Border Glow: `visibility`, `width` (default 2), `ambient`, `radius` (default 100), `softness`, `followDuration`, fades.
3. On non-Interactive objects, add the standalone components **Ripple**, **Spotlight**, or **Border Glow** (Add Component > Evo > UI > Effects); their settings are the `ripple`, `spotlight`, and `borderGlow` fields. `Ripple.Create()` emits one from code; `BorderGlow.Show()` / `Hide()`.

```csharp
// Buttons from GameObject > Evo UI already have targetGraphic assigned to their background; only enable and tune.
Undo.RecordObject(button, "Button Effects");
button.ripple.enabled = true;
button.ripple.color = new Color(1f, 1f, 1f, 0.2f);
button.borderGlow.enabled = true;
button.borderGlow.color = Color.cyan;
EditorUtility.SetDirty(button);
```

For a control built by hand, assign `targetGraphic` first (for example the background child's Procedural Shape).

**Expected result:** in Play Mode, clicking the button emits a ripple inside its background, and moving the pointer near its edge lights the border.

### Workflow: Other effect components

**Goal:** Pick the right component for other requests.

**Steps:**
1. Match the request to a row below.
2. Add the component from **Add Component > Evo > UI > Effects** (or `Undo.AddComponent<Evo.UI.CurvedCanvas>(go)` and so on), then configure it in the Inspector.
3. Read its docs page under https://evo.michsky.com/docs/evo-ui (Effects) before scripting it; these components aren't covered in detail here.

| Request | Component | What it does |
| --- | --- | --- |
| "curve the HUD / cylindrical screen" | `CurvedCanvas` | Bends descendant UI geometry into a cylinder. |
| "tilt the card when hovering" | `RectSway` | Tilts an element as the pointer moves over it. |
| "parallax layers that follow the mouse" | `RectParallax` | Moves a UI layer in response to the pointer. |
| "macOS-style dock magnification" | `DockMagnifier` | Enlarges items near the pointer along a dock. |
| "animated dashed border / marching ants" | `StrokeDash` | Animated dashed rectangular border (a Graphic itself). |
| "hazard / warning stripes" | `WarningStripe` | Scrolling stripe pattern through an Image. |
| "gradient on an Image or text" | `ImageGradient` | Vertex gradient on a UI Graphic. |
| "pixel dissolve transition" | `PixelCurtain` | Reveals or hides a Graphic with a directional pixel transition. |
| "ink splash reveal" | `InkPanel` | Moving ink silhouette with a radial reveal and droplets. |
| "topographic contour lines" | `TopographicSkin` | Animated contour lines on a UI image. |
| "dot matrix loading animation" | `DotMatrixLoader` | Grid of dots with procedural loading patterns. |
| "dim the other buttons when hovering one" | `ButtonGroupDimmer` | Smoothly dims the non-hovered buttons of a group. |
| "smooth TMP gradient" | `SmoothTMPGradient` | Smooth gradient coloring on TMP text (TMP only). |

**Expected result:** the component is on the object and its Inspector settings produce the effect in the Scene or Game view.

## Verification

- The component exists: `GetComponent<Evo.UI.SoftMask>()`, `BackdropBlur`, `TextEffects`, `Ripple`, etc.
- Blur: `shape.BackdropBlurEnabled` or `blur.settings.enabled` is true; in Play Mode `BackdropBlur.IsReady` is true; any UI that must appear in the blur is on a Screen Space - Camera or World Space Canvas.
- Soft Mask: it sits on a GameObject with a Graphic; masked children are its descendants; nesting depth ≤ 4.
- Text Effects: the object has a `TMP_Text`; the intended effect has `enabled == true`; tagged text contains the matching tag and `richText` is on.
- Interactive effects: `enabled == true` and `targetGraphic != null` on each enabled settings object.
- No shader or missing-material errors in the Console.

## API quick reference

| Entry point | Type | What it does |
| --- | --- | --- |
| `Evo.UI.BackdropBlur` | Component (`Image`) | Blurred camera capture; `settings`, `Refresh()`, `IsReady`. |
| `ProceduralShape.BackdropBlurEnabled` / `BackdropBlurSettings` | Properties | Blur as a Procedural Shape fill (shares `BackdropBlur.Settings`). |
| `Evo.UI.SoftMask` | Component | Soft-edged masking of descendants; `ShowMaskGraphic`, `InvertMask`, `Refresh()`. |
| `Evo.UI.TextEffects` | Component (needs `TMP_Text`) | Character effects; `SetText(value, startTypewriter, startScramble)`, `PlayTypewriter()`, `SkipTypewriter()`. |
| `Interactive.ripple` / `.spotlight` / `.borderGlow` | Settings fields | Pointer feedback on Evo UI controls. |
| `Evo.UI.Ripple`, `Evo.UI.Spotlight`, `Evo.UI.BorderGlow` | Components | Standalone versions; `Ripple.Create()`, `BorderGlow.Show()`/`Hide()`. |

## Common issues

| Symptom | Cause | Fix |
| --- | --- | --- |
| The blur doesn't include a UI panel behind it. | Screen Space - Overlay UI is never captured. | Put the background UI on a Screen Space - Camera or World Space Canvas, or blur only the 3D scene. |
| The blur shows the wrong view or nothing. | It resolved a different camera. | Set `sourceCamera` explicitly; check `ResolvedCamera`. |
| The panel looks like a faint tinted pane with the sharp scene behind it. | The fill color (or Backdrop Blur `color`) is translucent; the blur is multiplied by it. | Keep it white and opaque; darken with `brightness`, or tint with a separate shape on top. |
| Blur is expensive on mobile. | Full resolution and many iterations, captured every frame. | `resolution = Quarter`, fewer `iterations`, higher `updateInterval`. |
| Soft Mask has no effect on some children. | They use a custom shader without soft-mask support, or the mask nesting is deeper than 4. | Use Evo UI / standard UI / TMP shaders; reduce nesting. |
| Soft Mask edge is hard. | The mask Graphic has no soft alpha. | Use a sprite with faded edges, or set Procedural Shape `Softness`. |
| `<wave>` shows up as literal text or does nothing. | Rich Text is off, or the effect isn't enabled with Tagged Characters scope. | Turn on TMP Rich Text; set `Wave.enabled = true` and `scope = TaggedCharacters`. |
| Text Effects can't be added, or is skipped during text conversion. | The text isn't TextMesh Pro. | Use a TMP text for that object. |
| A ripple or glow is invisible on a ghost button. | The target Graphic is transparent. | `ignoreGraphicAlpha = true`. |
| Effects stop during the pause menu. | Scaled time. | Use the effect's unscaled time option (e.g. `TextEffects.UseUnscaledTime`). |

## Boundaries

- Backdrop Blur samples camera output only (no Screen Space - Overlay UI) and doesn't run on XR/VR stereo cameras.
- Soft Mask supports up to four nested levels and only shaders that read its data (Evo UI, standard UI, and TMP shaders).
- Text Effects and Smooth TMP Gradient need TextMesh Pro; UniText text isn't supported by them.
- Ripple draws at most eight concurrent ripples per target.
- For full-screen post-processing (bloom, vignette, depth of field) use the render pipeline's Volume framework, not these UI effects.
