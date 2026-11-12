---
name: evo-ui-procedural-shape
description: "Use this skill whenever the user wants to change how a UI graphic looks with Evo UI's Procedural Shape — e.g. 'round the corners of this panel', 'make the button a pill', 'add a drop shadow to the card', 'give it an outline', 'dashed border', 'gradient background', 'make a circular avatar', 'progress ring', 'draw a star / hexagon / heart', 'the corner radius keeps resetting'. Covers shape types, corner radius (pixels, percent, independent, squircle, radius sync), fill color modes and gradients, outlines and dashes, inner and outer shadows, clipping for progress, patterns, shape operations, sprites, raycast modes, dithering, and sprite export (sliced or tintable sprites, and replacing a shape with an Image). Do NOT use for project-wide colors and themes (see evo-ui-styler-themes), blur and other effects (see evo-ui-effects), or creating controls (see evo-ui-create-elements). When in doubt whether a UI shape or styling request could involve Evo UI, use this skill — Prerequisites shows how to confirm the asset is installed."
metadata:
  asset: "Evo UI"
  publisher: "Michsky"
  asset-version: "2.0.0"
  skill-version: "1.4.1"
  unity: "6000.0+"
  render-pipelines: "Built-in, URP, HDRP"
  category: "tools/gui"
  asset-store-url: "https://assetstore.unity.com/packages/slug/410788"
  documentation-url: "https://evo.michsky.com/docs/evo-ui"
  support-url: "https://www.michsky.com/contact"
  last-verified: "2026-11-11"
---

# Style Shapes with Procedural Shape

`Evo.UI.ProceduralShape` is a uGUI `Graphic` (a replacement for `Image`) that draws rectangles, ellipses, polygons, stars, hearts, and chevrons in a shader, with rounded corners, fills, gradients, outlines, dashed outlines, inner and outer shadows, clipping, patterns, and an optional sprite, all on one component. Every Evo UI control uses it for its backgrounds. This skill shows how to configure it in the Inspector or from code.

## When to use this skill

- "round the corners of this panel to 24px"
- "make this button a pill shape"
- "add a soft drop shadow under the card"
- "give the panel a 2px outline" / "make the border dashed"
- "use a purple to blue gradient background"
- "make the profile picture a circle"
- "make a circular progress ring that fills up"
- "draw a hexagon / star / heart"
- "subtract a circle from this rectangle"
- "why does the radius go back to 16 after i change it"
- "bake this panel into a 9-sliced sprite" / "replace these shapes with regular Images"

Not for:
- Colors shared across the UI and themes — see `evo-ui-styler-themes`.
- Backdrop blur, soft masks, glow, ripples — see `evo-ui-effects`.
- Progress bars and timers as complete controls — see `evo-ui-charts-and-data`.

## Prerequisites

- Evo UI 2.0.0+ on Unity 6000.0+, any render pipeline.
- **Install check:** `System.Type.GetType("Evo.UI.ProceduralShape, Evo.UI") != null`, or confirm **Add Component > Evo > UI > Graphics > Procedural Shape** exists. If it fails, tell the user Evo UI isn't installed and link https://assetstore.unity.com/packages/slug/410788.
- The object must be under a Canvas (it has a RectTransform and draws as UI).

Two rules decide whether a change shows up:
1. **Color modes pick the source.** Each layer (fill, outline, inner shadow, outer shadow, pattern) has a `ColorMode`: `Base` (the Graphic's `color`), `Custom` (the layer's own color, e.g. `FillColor`), `Gradient`, `Styler`, or `StylerGradient`. Setting `FillColor` has no effect while `fillColorMode` is `Styler` or `Base`. Set the mode first.
2. **Shape Styles own groups.** When `shapeStyleID` names a Shape Style in the shape's Styler preset, that style overrides its groups (Graphic, Shape including corners, Fill, Outline, Inner Shadow, Outer Shadow) on every update. The Evo UI Button background uses the `Background` style, which owns corners and outline. To change those locally, clear `shapeStyleID` (or edit the style in the preset for a shared change).

## Quick start

Turn a selected UI object into a rounded card with a shadow.

1. Select the object. If it has an `Image`, remove it (both are Graphics and can't share a GameObject), or create a new shape with **GameObject > Evo UI > Procedural Shape**.
2. Run:

```csharp
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Evo.UI;

public static class EvoMakeCard
{
    public static void Run()
    {
        GameObject go = Selection.activeGameObject;
        var shape = go.GetComponent<ProceduralShape>();
        if (shape == null)
            shape = Undo.AddComponent<ProceduralShape>(go);

        Undo.RecordObject(shape, "Style Card");
        shape.shapeStyleID = "";                       // Keep a preset Shape Style from overriding the values below.
        shape.Shape = ProceduralShape.ShapeType.Rectangle;
        shape.CornerRadiusMode = ProceduralShape.RadiusMode.Pixels;
        shape.UniformCornerRadius = 24f;

        shape.fillColorMode = ProceduralShape.ColorMode.Custom;
        shape.FillColor = new Color32(30, 34, 46, 255);

        shape.outerShadowColorMode = ProceduralShape.ColorMode.Custom;
        shape.OuterShadowColor = new Color(0f, 0f, 0f, 0.35f);
        shape.OuterShadowOffset = new Vector2(0f, -8f);
        shape.OuterShadowSize = 4f;
        shape.OuterShadowSoftness = 24f;

        shape.UpdateRect();                            // Applies the color-mode fields changed above.
        PrefabUtility.RecordPrefabInstancePropertyModifications(shape);
        EditorSceneManager.MarkSceneDirty(go.scene);
    }
}
```

Expected result: the object shows a dark card with 24 px corners and a soft shadow below it; `shape.CornerRadius == new Vector4(24, 24, 24, 24)`.

## Workflows

### Workflow: Shape and corners

**Goal:** Choose the outline of the graphic and its corner rounding.

**Steps:**
1. Set `Shape` to `Rectangle`, `Ellipse`, `Polygon` (`PolygonSides` 3–8), `Star`, `Heart`, or `Chevron`. `ShapeAngle` rotates it (degrees) and `ShapeSkew` shears it without changing the RectTransform.
2. Corners (rectangles and other non-ellipse shapes):
   - `CornerRadiusMode`: `Pixels`, or `Percentage` where 100 equals half the shorter side. A pill is `Percentage` with radius 100.
   - `UniformCornerRadius = r` sets all four corners. `CornerRadius` is a `Vector4` in top-left, top-right, bottom-right, bottom-left order; set `IndependentCorners = true` for different values (rectangles only).
   - `SquircleCorners = true` gives smooth squircle corners (rectangles only).
3. Inset shapes that follow an outer shape: set `RadiusSynchronizationMode` to `MatchParent` (nearest parent shape), `MatchTarget` (with `RadiusSyncTarget`), or `MatchAncestor` (searches parents; `RadiusSyncTargetName` can be a name or a path like `Frame/Background`). The radius is derived from the source minus the padding between them. With `IndependentCorners` off, every corner shares one radius (the roundest fit). With it on, each corner fits its own two paddings, so corners that sit away from the source's rounded corners stay square, such as the bottom of a header or the inner side of a sidebar. At runtime, call `UpdateRect()` after creating or moving the source in another branch.

```csharp
shape.CornerRadiusMode = ProceduralShape.RadiusMode.Percentage;
shape.UniformCornerRadius = 100f; // Pill
```

**Expected result:** `shape.Shape`, `shape.CornerRadius`, and `shape.CornerRadiusMode` read back as set; the Scene view shows the new outline.

### Workflow: Fills and gradients

**Goal:** Color the interior with a solid color or a gradient.

**Steps:**
1. Solid: `fillColorMode = ColorMode.Custom; FillColor = color;` or keep `Base` and set the Graphic's `color`.
2. Gradient: set `fillColorMode = ColorMode.Gradient`, assign a new `Gradient` to `FillGradient`, and choose `FillGradientType` (`Linear`, `Radial`, `Angular`), `FillGradientAngle` (degrees), `FillGradientZoom`, `FillGradientReverse`, and `FillGradientSpace` (`Local` per shape, `ScreenSpace` for one continuous gradient across several shapes).
3. `FillCenter = false` leaves only the outline (use with an outline width).
4. Call `UpdateRect()` after changing a `...ColorMode` field or editing a `Gradient` in place.

```csharp
var gradient = new Gradient();
gradient.SetKeys(
    new[] { new GradientColorKey(new Color32(124, 58, 237, 255), 0f), new GradientColorKey(new Color32(37, 99, 235, 255), 1f) },
    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });

shape.fillColorMode = ProceduralShape.ColorMode.Gradient;
shape.FillGradient = gradient;
shape.FillGradientType = ProceduralShape.GradientType.Linear;
shape.FillGradientAngle = 45f;
shape.UpdateRect();
```

**Expected result:** the shape shows a diagonal purple-to-blue gradient.

### Workflow: Outlines and dashed borders

**Goal:** Draw a border.

**Steps:**
1. Set `outlineColorMode = ColorMode.Custom` and `OutlineColor`, or `Gradient` with `OutlineGradient`.
2. Set `OutlineWidth` in pixels. The outline is drawn inward, so it doesn't change the shape's size.
3. Dashes: `OutlineDashed = true`, `OutlineDashLength`, `OutlineDashGap` (local pixels), `OutlineDashOffset` (in repeats: +1 moves by one dash and gap, so animating it makes the border crawl), `OutlineDashRounded`.
4. Call `UpdateRect()` after the color-mode change.

**Expected result:** `shape.OutlineWidth > 0` and the border is visible inside the edge.

### Workflow: Shadows

**Goal:** Add depth with an outer (drop) shadow or an inner shadow.

**Steps:**
1. Outer: `outerShadowColorMode = ColorMode.Custom`, `OuterShadowColor` (with alpha), `OuterShadowOffset`, `OuterShadowSize` (expansion), `OuterShadowSoftness` (blur radius). `OuterShadowOutsideOnly = true` keeps the shadow out of the shape's interior (for translucent shapes).
2. Inner: `innerShadowColorMode`, `InnerShadowColor`, `InnerShadowOffset`, `InnerShadowSize`, `InnerShadowSoftness`.
3. Large soft shadows or slow gradients that show banding: set `Dither = true` on that shape only.
4. Call `UpdateRect()`.

**Expected result:** the shadow renders around (or inside) the shape. The shadow draws outside the RectTransform, so check that a parent mask doesn't clip it.

### Workflow: Images, avatars, and hit testing

**Goal:** Show a sprite inside a shape, such as a round avatar.

**Steps:**
1. Assign `Sprite`. `ImageScaleMode`: `Stretch`, `Cover` (fill and crop), or `Fit` (keep aspect).
2. For a round avatar: `Shape = Ellipse`, `ImageScaleMode = Cover`. A mask object is not needed.
3. `RaycastDetectionMode`: `Standard` (rectangle), `Shape` (follows the drawn shape, ignores sprite alpha), or `None` (no hits; good for decoration).

**Expected result:** the sprite is cropped to the shape.

### Workflow: Clipping for progress rings and bars

**Goal:** Reveal part of a shape, e.g. a radial progress ring.

**Steps:**
1. `ClippingMethod`: `Horizontal`, `Vertical`, or `Radial360`; `ClipAmount` 0–1 is the visible part; `ClipOrigin` picks the starting edge; `ClipClockwise` picks the radial direction.
2. `RoundedClipEdges = true` keeps corner radii on horizontal and vertical fills, or rounds the ends of an outline-only radial fill.
3. Ring example: `Shape = Ellipse`, `FillCenter = false`, `OutlineWidth = 12`, `ClippingMethod = Radial360`, `RoundedClipEdges = true`, then set `ClipAmount` each frame or tween it.

```csharp
public void SetProgress(ProceduralShape ring, float progress)
{
    ring.ClippingMethod = ProceduralShape.ClipMethod.Radial360;
    ring.RoundedClipEdges = true;
    ring.ClipAmount = Mathf.Clamp01(progress);
}
```

**Expected result:** at `ClipAmount = 0.25` a quarter of the ring is drawn. For a ready-made control with labels and events, use the Progress Bar (Radial) from `evo-ui-charts-and-data`.

### Workflow: Patterns and shape operations

**Goal:** Decorate a fill with repeating tiles, or combine shapes.

**Steps:**
1. Pattern: `FillPattern = true`, `PatternType` (`Shape` or `Sprite`), `PatternShape`, `PatternSize`, `PatternGap`, `PatternOffset`, `PatternAngle`, `patternColorMode` + `PatternColor`; `PatternFalloff` with `PatternFalloffOrigin` (0–1), `PatternFalloffStart`, `PatternFalloffEnd` fades tiles by distance. Sprite tiles must share the fill sprite's atlas texture, and the pattern sprite needs tight packing and atlas rotation turned off (`PatternSpriteCompatible` reports this). On mobile, `Rectangle` and `Ellipse` tiles in a `Base`, `Custom`, or `Styler` color are far cheaper than sprite tiles, gradient colors, other tile shapes, or a pattern on a clipped shape, which switch the whole Canvas to the full shader program (see Boundaries).
2. Operations: call `SetShapeOperations(new[] { new ProceduralShape.ShapeOperation { target = otherShape, operation = ProceduralShape.OperationType.Subtract, smoothing = 0f } })`. Up to four operations (`Union`, `Subtract`, `Intersect`, `Exclude`), applied in order. Targets must be active, coplanar, and on the same root Canvas. Set `OperandOnly = true` on a target to use its geometry without drawing it. Pass `null` to clear.

**Expected result:** `shape.FillPattern` is true and tiles render, or `shape.ShapeOperationCount == 1` and the cutout is visible.

### Workflow: Export a shape as a sprite

**Goal:** Bake a shape into a PNG sprite, optionally 9-sliced or tintable, and optionally replace the shape with an `Image` that uses it.

**Steps:**
1. In the Inspector, open the Procedural Shape component's context menu (the ⋮ button) and choose **Export as Sprite...**. A floating **Export as Sprite** window opens for that shape only; open it again from another shape's menu to export that one. It previews the result (slice borders as guides) and lists what each option will do.
2. **Folder** (inside `Assets`, remembered per project) and **Name** (defaults to the object name). Exporting over an existing sprite rewrites it in place: its GUID, references, compression and filtering stay; the window warns when that happens.
3. **Scale** 1x–4x draws that many pixels per unit. The sprite's Pixels Per Unit is the Canvas's Reference Pixels Per Unit times the scale, so it shows at the same size.
4. **Type:** `Simple`, or `Sliced` for 9-slice borders. The borders are measured, not guessed: the window draws the shape larger than its rect and keeps one of the rows and columns that stretching repeats. Rectangles (rounded, squircle, with outlines, inner and outer shadows) slice on both axes; a linear gradient at 0°/180° or 90°/270° slices across its direction only. Ellipses, polygons, stars, hearts, chevrons, rotation or skew, sprites, patterns, dashed outlines, partial clipping, Fit scale mode and other gradients can't be sliced; the window says why and exports Simple.
5. **Color:** `Baked` keeps every color. `Tintable` makes the fill (or the outline of a shape without a fill) white, so the Image color sets it. Layers that share its color turn white too and black ones stay black. An outline with its own solid color gets its own white sprite (`<Name> Outline.png`). Gradient fills, and other layers with their own color, can't be tintable.
6. Optional **Replace with Image** (scene or open prefab, Edit Mode): swaps the shape for an `Image` with the sprite (Type Sliced or Simple) in one undo step, keeping the component's place, raycast target and padding, and maskable. The Graphic alpha goes to the Image color.
   - References to the shape in fields that accept the Image (`Graphic`, `MaskableGraphic`, `Component`, `Object`) move to it, such as a Button's Target Graphic. Fields typed `ProceduralShape` lose the reference; the window reports each one and logs a warning.
   - With `Tintable`, **Add Styler Object** adds a `StylerObject` with the layer's color ID and, if it uses interaction states, the same state colors and `interactableObject`. A separate outline becomes a child Image named `Outline` with its own Styler Object.
   - An Image draws only inside its rect, so an outer shadow goes to a child Image named `Shadow` (drawn outside the shape) or is dropped (**Outer Shadow**: Child Image / Drop). Child Images ignore layout and raycasts.
   - Components that require a Graphic (Soft Mask, Image Gradient, Curved Canvas Graphic) are removed and added back with the same settings. Shapes with Shape Operations export only as Simple and Baked.
7. Click **Export Sprite** (or **Export and Replace**). Undo (Ctrl+Z) restores the shape, its references and the hierarchy; the PNG files stay.

From code (Editor only), the same steps:

```csharp
var settings = new Evo.UI.Editor.ShapeSpriteExporter.Settings
{
    folder = "Assets/UI/Sprites", fileName = "Card", scale = 2, sliced = true, tint = true, replace = true
};

using (var plan = Evo.UI.Editor.ShapeSpriteExporter.Prepare(shape, settings))
{
    if (plan.error != null) Debug.LogWarning(plan.error);
    else Debug.Log(Evo.UI.Editor.ShapeSpriteExporter.Export(plan, settings, out _));
}
```

`Prepare` renders without writing and reports `error`, `sliceNote` and `notes`; `Export` writes the files and replaces. The folder must exist.

**Expected result:** a PNG sprite (with borders when sliced) in the folder. With Replace, the object looks the same with an `Image` (plus `Outline`/`Shadow` children where needed), and the sprite is static: shape properties, Shape Styles and gradients no longer apply. Export again with the same name to update every Image that uses it.

## Verification

- `GetComponent<Evo.UI.ProceduralShape>() != null` on the target, with no `Image` on the same object.
- Properties read back as set: `Shape`, `CornerRadius`, `CornerRadiusMode`, `FillColor`, `OutlineWidth`, `OuterShadowSoftness`, `ClipAmount`.
- Color modes match intent: e.g. `fillColorMode == ProceduralShape.ColorMode.Custom` when a custom `FillColor` was set.
- `shapeStyleID` is empty, or the named style doesn't own the groups changed locally.
- After `EditorSceneManager.SaveScene`, reopening the scene shows the same values (no Styler reset).
- The Console shows no errors.

## API quick reference

| Entry point | Type | What it does |
| --- | --- | --- |
| `Evo.UI.ProceduralShape` | Component (`MaskableGraphic`) | The shape renderer. **Add Component > Evo > UI > Graphics > Procedural Shape**, or **GameObject > Evo UI > Procedural Shape**. |
| `Shape`, `PolygonSides`, `ShapeAngle`, `ShapeSkew` | Properties | Geometry. |
| `CornerRadiusMode`, `UniformCornerRadius`, `CornerRadius`, `IndependentCorners`, `SquircleCorners`, `RadiusSynchronizationMode` | Properties | Corners and radius sync. |
| `fillColorMode`, `outlineColorMode`, `innerShadowColorMode`, `outerShadowColorMode`, `patternColorMode` | Fields (`ProceduralShape.ColorMode`) | Where each layer's color comes from. |
| `FillColor`, `FillGradient`, `OutlineWidth`, `OutlineDashed`, `OuterShadow...`, `InnerShadow...` | Properties | Layer values. |
| `ClippingMethod`, `ClipAmount`, `ClipOrigin`, `ClipClockwise`, `RoundedClipEdges` | Properties | Partial reveal / progress. |
| `Sprite`, `ImageScaleMode`, `RaycastDetectionMode`, `Dither`, `Softness` | Properties | Image, hit testing, quality. |
| `shapeStyleID`, `Preset`, `useDefaultStylerPreset`, `interactableObject`, `fillStyler` | Fields / property | Styler integration and state colors. |
| `UpdateRect()` | Method | Re-applies after field changes or in-place Gradient / style edits. |
| `SetShapeOperations(ShapeOperation[])` | Method | Boolean operations with up to four other shapes. |
| `ProceduralShape.WarmUpShaders(fullProgram, lightProgram, softMask)` | Static method | Compiles shader programs now instead of on their first draw; call it from a loading screen. Nothing calls it automatically. |
| `Evo.UI.Editor.ShapeSpriteExporter.Prepare(shape, settings)` / `Export(plan, settings, out result)` | Editor static methods | What the **Export as Sprite** window runs: bake, slice, save and optionally replace with an Image. |
| `RenderSprite(SpriteRenderSettings, out Rect bounds)`, `GetSpriteTint()`, `GetSliceLimits(out x, out y)`, `HasOuterShadow` | Editor-only members | Render the shape (or some of its `SpriteLayers`) to a `Texture2D`, trimmed to what it draws but never smaller than its rect; work out how it splits for tinting and which axes can be sliced. |

Property setters refresh the shape themselves; public fields (`...ColorMode`, `shapeStyleID`, `fillStyler`) need `UpdateRect()`. Full property list: https://evo.michsky.com/docs/evo-ui/procedural-shape. Evo Tween can animate many of these properties (`evo-tween-tween-animator`, modules under **Evo UI > Procedural Shape**).

## Common issues

| Symptom | Cause | Fix |
| --- | --- | --- |
| The corner radius or outline goes back to the old value. | `shapeStyleID` points to a Shape Style that owns that group (e.g. Button backgrounds use `Background`). | Clear `shapeStyleID` and call `UpdateRect()`, or change the Shape Style in the preset for every user. |
| `FillColor` changes nothing. | `fillColorMode` is `Base` or `Styler`. | Set `fillColorMode = ColorMode.Custom`, then `UpdateRect()`. |
| Gradient edits don't appear. | The `Gradient` object was edited in place. | Assign a new Gradient to `FillGradient`, or call `UpdateRect()`. |
| "Can't add component ... because it conflicts with Image". | A GameObject can have only one Graphic. | Remove the `Image` (keep its sprite in `Sprite`) or add the shape to a child. |
| The shadow is cut off. | A parent `RectMask2D`, Mask, or Soft Mask clips outside the rect. | Add padding to the mask, move the shape out of the masked area, or reduce `OuterShadowSize`/softness. |
| Visible bands in a big soft glow. | 8-bit color banding. | `Dither = true` on that shape. |
| Clicks pass through transparent corners, or hit outside the visible shape. | `RaycastDetectionMode` is `Standard` (rectangle). | Use `Shape`. Use `None` for purely decorative shapes. |
| Pattern sprite doesn't show. | It isn't on the same atlas texture as the fill sprite, or uses tight packing or rotation. | Check `PatternSpriteCompatible`; fix the sprite's atlas and packing settings. |
| **Export as Sprite** says "Can't be sliced" or slices only one axis. | The shape changes along that axis at any size: a curved shape, gradient, pattern, dashes, sprite, rotation, or clipping. | Export Simple, or remove that feature (for a gradient, use 0°/180° or 90°/270° to slice across it). |
| **Tintable** is refused ("has a color of its own"). | An inner shadow, pattern or outer shadow uses a color that isn't black and isn't the fill's; an Image color would tint it too. | Make that layer black or give it the fill's color, or export Baked. Outlines with their own color are fine: they get their own sprite. |
| After **Replace with Image**, a field shows None. | The field's type is `ProceduralShape`, which an Image can't fill (the window lists these). | Point it at the Image if its type allows, or undo and keep the shape. |
| Radius sync uses the wrong source. | `MatchAncestor` found another shape. | Set `RadiusSyncTargetName`, or use `MatchTarget` with `RadiusSyncTarget`; check `RadiusSyncSource`. |
| A synced header or sidebar is rounded on the side that faces the middle of the window. | `IndependentCorners` is off, so all corners share one radius. | Set `IndependentCorners = true` on the synced shape; each corner then fits its own padding. |
| Low frame rate on Android or other mobile devices while the UI is visible (the Profiler shows a long `Device.Present` wait, so the GPU is the bottleneck). | Some active shape on the same Canvas, even a small one, needs the full shader program: an inner shadow, dashed outline, star, heart, or chevron shape, shape operations, an Interactive effect, or a pattern of sprite, gradient-colored, or polygon, star, heart, or chevron tiles, or on a clipped shape. That switches every shape on the Canvas, including big backgrounds, to the full program (see Boundaries). | Remove those features from the Canvas's shapes, or move the shapes that need them under their own nested Canvas so the Canvas with the big shapes stays on the light program. Gradients, outlines, outer shadows, clipping, polygons, backdrop blur, and solid `Rectangle` or `Ellipse` pattern tiles are fine. |
| The game freezes once, for up to a few seconds on WebGL, the first time a screen or popup appears that has an inner shadow, dashed outline, star, heart, or chevron, shape operations, an Interactive effect, or a sprite or gradient pattern. | Its Canvas switches to the full shader program, which compiles the first time it draws. | Call `ProceduralShape.WarmUpShaders()` from the loading screen once the scene or prefab with those shapes has loaded (`softMask: true` if they are inside a Soft Mask). Skip it in projects that never use those features, where it would add a compile that otherwise never happens. A frosted (backdrop blur) panel that first appears on a screen whose Canvases all use the full program compiles the light program instead: pass `lightProgram: true` as well. |

## Boundaries

- Procedural Shape is a uGUI Graphic only; it doesn't render on SpriteRenderers, 3D meshes, or UI Toolkit.
- Corner radii apply to rectangles and other cornered shapes, not ellipses. Independent and squircle corners and radius sync are rectangle-only.
- Polygons have 3–8 sides. Freeform or Bezier shapes aren't supported; use a sprite for those.
- Shape operations sample only geometry and clipping of up to four targets on the same root Canvas, not their colors, shadows, or nested operations.
- A pixel costs more than a plain `Image` pixel (about 2x for a rounded rectangle on desktop), but one shape replaces the layered sliced Images of a card (shadow, background, border), so typical layouts cost about the same on a Canvas that uses only light features. All shapes on a Canvas share one material and batch together; that material draws with a light shader program until one active shape on the Canvas needs the full one. The light program covers rectangles, ellipses, and polygons with every color mode (`Base`, `Custom`, `Styler`, and gradients) for the fill, outline, and outer shadow, plus corners, sprites, softness, rotation, dithering, fill clipping (progress bars and rings, rounded edges included), backdrop blur, and patterns of `Rectangle` or `Ellipse` tiles in a solid or `Base` color on an unclipped shape. Inner shadows, dashed outlines, the star, heart, and chevron shapes, shape operations, Interactive effects, and other patterns (sprite tiles, gradient colors, polygon, star, heart, or chevron tiles, or a pattern on a clipped shape) switch that whole Canvas to the full program; there, gradients, clipping, and polygons cost about 3x more per pixel on desktop and about 10x more on mobile GPUs (Mali). Backdrop blur shapes draw with their own material and keep the light program unless they need the full one themselves. On mobile, keep those features off Canvases with large or full-screen shapes, or give them their own nested Canvas. Custom materials always use the full program. Each program compiles once, the first time a Canvas draws with it (up to a few seconds on WebGL); `ProceduralShape.WarmUpShaders()` moves that compile to a loading screen. Different sprite textures, masks, blur settings, and custom materials split batches.
- Shape Style and per-layer Styler mappings are covered in `evo-ui-styler-themes`; backdrop blur fill is covered in `evo-ui-effects`.
