using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Evo.UI.Editor
{
    using Layers = ProceduralShape.SpriteLayers;

    /// <summary>
    /// Bakes Procedural Shapes into sprite assets, works out their 9-slice borders, and replaces shapes with Images.
    /// </summary>
    public static class ShapeSpriteExporter
    {
        // Extra local units drawn when looking for borders, so even a pill gets rows and columns to stretch
        const float SliceMargin = 16f;

        public enum ShadowHandling { ChildImage = 0, Drop = 1 }

        [Serializable]
        public class Settings
        {
            public string folder = "Assets";
            public string fileName = "";
            public int scale = 1;
            public bool sliced;
            public bool tint;
            public bool replace;
            public bool stylerObject = true;
            public ShadowHandling shadow = ShadowHandling.ChildImage;
        }

        /// <summary>
        /// One baked texture, ready to save.
        /// </summary>
        public class Bake : IDisposable
        {
            public Texture2D texture;
            /// <summary>
            /// 9-slice borders in pixels: left, bottom, right, top.
            /// </summary>
            public Vector4 border;
            public bool slicedX, slicedY;
            /// <summary>
            /// How far the texture reaches past the rect in local units: left, bottom, right, top.
            /// </summary>
            public Vector4 overflow;
            public Vector2 pivot;

            public bool Sliced => slicedX || slicedY;

            public void Dispose()
            {
                if (texture != null)
                    Object.DestroyImmediate(texture);

                texture = null;
            }
        }

        /// <summary>
        /// What an export would produce: the baked textures, and anything that limits or blocks it.
        /// </summary>
        public class Plan : IDisposable
        {
            public ProceduralShape shape;
            /// <summary>
            /// The shape's sprite. With Tintable color, an outline with its own color goes to outline instead.
            /// </summary>
            public Bake main, outline, shadow;
            public ProceduralShape.SpriteTint tint;
            /// <summary>
            /// Blocks the export.
            /// </summary>
            public string error;
            /// <summary>
            /// Why slicing covers fewer axes than asked for.
            /// </summary>
            public string sliceNote;
            public bool hasOuterShadow;
            public bool mainStyler, outlineStyler;
            public readonly List<string> notes = new();

            public void Dispose()
            {
                main?.Dispose();
                outline?.Dispose();
                shadow?.Dispose();
                main = outline = shadow = null;
            }
        }

        #region Planning
        /// <summary>
        /// Renders and analyzes the export without writing anything.
        /// </summary>
        public static Plan Prepare(ProceduralShape shape, Settings settings)
        {
            var plan = new Plan { shape = shape };
            if (shape == null)
            {
                plan.error = "Choose a Procedural Shape.";
                return plan;
            }

            plan.hasOuterShadow = shape.HasOuterShadow;
            plan.tint = shape.GetSpriteTint();

            if (settings.tint && plan.tint.error != null)
            {
                plan.error = plan.tint.error;
                return plan;
            }

            bool splitOutline = settings.tint && plan.tint.separateOutline;
            if (settings.replace)
            {
                plan.error = GetReplaceError(shape);
                if (plan.error != null)
                    return plan;

                plan.mainStyler = settings.tint && settings.stylerObject && UsesStylerObject(plan.tint.main);
                plan.outlineStyler = splitOutline && settings.stylerObject && UsesStylerObject(plan.tint.outline);
                CollectReplaceNotes(plan, settings);
            }

            // An Image draws only inside its rect, so a replacement keeps the outer shadow apart
            Layers layers = settings.replace && plan.hasOuterShadow ? Layers.Fill | Layers.Outline : Layers.All;
            if (splitOutline)
                layers &= ~Layers.Outline;

            var render = new ProceduralShape.SpriteRenderSettings
            {
                scale = Mathf.Max(1, settings.scale),
                tint = settings.tint,
                layers = layers,
                cropToRect = settings.replace,
                // An Image takes the Graphic alpha in its color, unless a Styler Object will own that color
                skipAlpha = settings.replace && !plan.mainStyler
            };

            try
            {
                plan.main = BakeShape(shape, render, settings.sliced, out plan.sliceNote);
                if (splitOutline)
                {
                    render.layers = Layers.Outline;
                    render.skipAlpha = settings.replace && !plan.outlineStyler;
                    plan.outline = BakeShape(shape, render, plan.main.Sliced, out _);
                }

                if (settings.replace && plan.hasOuterShadow && settings.shadow == ShadowHandling.ChildImage)
                {
                    render.layers = Layers.OuterShadow;
                    render.tint = false;
                    render.skipAlpha = false;
                    render.cropToRect = false;
                    plan.shadow = BakeShape(shape, render, plan.main.Sliced, out _);
                }
            }
            catch (Exception exception)
            {
                plan.Dispose();
                plan.error = exception.Message;
            }

            return plan;
        }

        /// <summary>
        /// A Styler Object can keep this layer's color in sync: Styler colors, and custom colors that follow interaction states.
        /// </summary>
        public static bool UsesStylerObject(ProceduralShape.SpriteTintLayer layer)
        {
            return layer.style != null && (layer.mode == ProceduralShape.ColorMode.Styler || (layer.mode == ProceduralShape.ColorMode.Custom && layer.interaction));
        }

        static Bake BakeShape(ProceduralShape shape, ProceduralShape.SpriteRenderSettings render, bool sliced, out string sliceNote)
        {
            sliceNote = null;
            Vector2 size = shape.rectTransform.rect.size;
            Vector2 pivot = shape.rectTransform.pivot;
            if (sliced)
            {
                string limit = shape.GetSliceLimits(out bool x, out bool y);
                Bake bake = x || y ? BakeSliced(shape, render, size, pivot, x, y, ref limit) : null;
                if (bake != null)
                {
                    if (!bake.slicedX || !bake.slicedY)
                    {
                        sliceNote = (bake.slicedX ? "Only the width is sliced, so height changes stretch the whole sprite. "
                            : "Only the height is sliced, so width changes stretch the whole sprite. ") + limit;
                    }

                    return bake;
                }

                sliceNote = $"Can't be sliced: {limit} It exports as Simple.";
            }

            render.size = default;
            var simple = new Bake { texture = shape.RenderSprite(render, out Rect bounds) };
            simple.overflow = GetOverflow(bounds, size, pivot);
            simple.pivot = new Vector2(-bounds.xMin / bounds.width, -bounds.yMin / bounds.height);
            return simple;
        }

        /// <summary>
        /// Draws the shape larger than its rect, then keeps one of the rows and columns that stretching would repeat.
        /// An axis whose rows or columns all differ keeps its size and isn't sliced.
        /// </summary>
        static Bake BakeSliced(ProceduralShape shape, ProceduralShape.SpriteRenderSettings render, Vector2 size, Vector2 pivot,
            bool allowX, bool allowY, ref string limit)
        {
            int tolerance = shape.Dither ? 2 : 1;
            render.size = size + new Vector2(allowX ? SliceMargin : 0f, allowY ? SliceMargin : 0f);
            Texture2D texture = shape.RenderSprite(render, out Rect bounds);
            Color32[] pixels = texture.GetPixels32();
            int x0 = 0, x1 = 0, y0 = 0, y1 = 0;
            bool x = allowX && FindStretch(pixels, texture.width, texture.height, true, tolerance, out x0, out x1);
            bool y = allowY && FindStretch(pixels, texture.width, texture.height, false, tolerance, out y0, out y1);

            if (x != allowX || y != allowY)
            {
                limit ??= "The shape changes across its whole size.";
                Object.DestroyImmediate(texture);
                if (!x && !y)
                    return null;

                // Draw the axis that doesn't slice at its real size
                render.size = size + new Vector2(x ? SliceMargin : 0f, y ? SliceMargin : 0f);
                texture = shape.RenderSprite(render, out bounds);
                pixels = texture.GetPixels32();
                x = x && FindStretch(pixels, texture.width, texture.height, true, tolerance, out x0, out x1);
                y = y && FindStretch(pixels, texture.width, texture.height, false, tolerance, out y0, out y1);
                if (!x && !y)
                {
                    Object.DestroyImmediate(texture);
                    return null;
                }
            }

            var bake = new Bake { slicedX = x, slicedY = y };
            bake.texture = Compact(texture, pixels, x, x0, x1, y, y0, y1, out bake.border);
            bake.overflow = GetOverflow(bounds, render.size, pivot);
            bake.pivot = new Vector2(0.5f, 0.5f);
            Object.DestroyImmediate(texture);
            return bake;
        }

        static Vector4 GetOverflow(Rect bounds, Vector2 size, Vector2 pivot)
        {
            var rect = new Rect(-pivot * size, size);
            return new Vector4(rect.xMin - bounds.xMin, rect.yMin - bounds.yMin, bounds.xMax - rect.xMax, bounds.yMax - rect.yMax);
        }

        /// <summary>
        /// Finds the longest run of identical, visible columns (or rows). Each line is compared with the first of its run,
        /// so slow changes never add up to one run.
        /// </summary>
        static bool FindStretch(Color32[] pixels, int width, int height, bool columns, int tolerance, out int start, out int end)
        {
            int count = columns ? width : height;
            start = end = -1;
            int best = 0;

            for (int a = 0; a < count;)
            {
                int b = a;
                if (!IsEmptyLine(pixels, width, height, columns, a))
                {
                    while (b + 1 < count && SameLine(pixels, width, height, columns, tolerance, a, b + 1))
                        b++;
                }

                if (b > a && b - a + 1 > best)
                {
                    best = b - a + 1;
                    start = a;
                    end = b;
                }

                a = b + 1;
            }

            // The first and last lines of the run stay with the borders, and one between them stretches
            return best >= 3;
        }

        static int PixelIndex(int width, bool columns, int line, int i) => columns ? i * width + line : line * width + i;

        static bool IsEmptyLine(Color32[] pixels, int width, int height, bool columns, int line)
        {
            int length = columns ? height : width;
            for (int i = 0; i < length; i++)
            {
                if (pixels[PixelIndex(width, columns, line, i)].a > 0)
                    return false;
            }

            return true;
        }

        static bool SameLine(Color32[] pixels, int width, int height, bool columns, int tolerance, int lineA, int lineB)
        {
            int length = columns ? height : width;
            int limit = tolerance * 255;
            for (int i = 0; i < length; i++)
            {
                Color32 a = pixels[PixelIndex(width, columns, lineA, i)];
                Color32 b = pixels[PixelIndex(width, columns, lineB, i)];

                // Premultiplied, so nearly transparent pixels can't differ by their meaningless color
                if (Mathf.Abs(a.a - b.a) > tolerance || Mathf.Abs(a.r * a.a - b.r * b.a) > limit
                    || Mathf.Abs(a.g * a.a - b.g * b.a) > limit || Mathf.Abs(a.b * a.a - b.b * b.a) > limit)
                    return false;
            }

            return true;
        }

        static Texture2D Compact(Texture2D source, Color32[] pixels, bool x, int x0, int x1, bool y, int y0, int y1, out Vector4 border)
        {
            int width = source.width;
            int height = source.height;
            List<int> columns = KeptLines(width, x, x0, x1);
            List<int> rows = KeptLines(height, y, y0, y1);
            var output = new Color32[columns.Count * rows.Count];

            for (int row = 0; row < rows.Count; row++)
            {
                for (int column = 0; column < columns.Count; column++)
                    output[row * columns.Count + column] = pixels[rows[row] * width + columns[column]];
            }

            border = new Vector4(x ? x0 + 1 : 0, y ? y0 + 1 : 0, x ? width - x1 : 0, y ? height - y1 : 0);
            var texture = new Texture2D(columns.Count, rows.Count, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixels32(output);
            texture.Apply(false, false);
            return texture;
        }

        // Up to and including the run's first line, one line from inside it, then from its last line on
        static List<int> KeptLines(int count, bool sliced, int start, int end)
        {
            var lines = new List<int>(count);
            for (int i = 0; i < count; i++)
            {
                if (!sliced || i <= start + 1 || i >= end)
                    lines.Add(i);
            }

            return lines;
        }
        #endregion

        #region Preview
        /// <summary>
        /// The export as its Images draw it at the shape's size: sliced sprites stretched, tinted, with the shadow and
        /// outline Images in their places. At most maxSize pixels on its longer side. The caller destroys the texture.
        /// mainRect receives where the main sprite lands, in texture pixels from the bottom left.
        /// </summary>
        public static Texture2D CreatePreview(Plan plan, Settings settings, int maxSize, out Rect mainRect, out float pixelsPerSourcePixel)
        {
            mainRect = default;
            pixelsPerSourcePixel = 1f;
            if (plan?.main == null)
                return null;

            Vector2 size = plan.shape.rectTransform.rect.size;
            float scale = Mathf.Max(1, settings.scale);
            Color graphicColor = plan.shape.color;
            var layers = new List<(Bake bake, Color tint)>();
            if (plan.shadow != null)
                layers.Add((plan.shadow, Color.white));

            layers.Add((plan.main, GetPreviewColor(settings, plan.tint.main, plan.mainStyler, graphicColor)));
            if (plan.outline != null)
                layers.Add((plan.outline, GetPreviewColor(settings, plan.tint.outline, plan.outlineStyler, graphicColor)));

            // The area every layer covers, in local units around the rect
            Vector4 area = Vector4.zero;
            foreach (var (bake, _) in layers)
                area = Vector4.Max(area, bake.overflow);

            float fullWidth = (size.x + area.x + area.z) * scale;
            float fullHeight = (size.y + area.y + area.w) * scale;
            float k = Mathf.Min(1f, maxSize / Mathf.Max(fullWidth, fullHeight));
            int width = Mathf.Max(1, Mathf.RoundToInt(fullWidth * k));
            int height = Mathf.Max(1, Mathf.RoundToInt(fullHeight * k));
            var output = new Color[width * height];

            foreach (var (bake, tint) in layers)
            {
                Texture2D texture = bake.texture;
                Color32[] pixels = texture.GetPixels32();
                float layerWidth = (size.x + bake.overflow.x + bake.overflow.z) * scale;
                float layerHeight = (size.y + bake.overflow.y + bake.overflow.w) * scale;
                float left = (area.x - bake.overflow.x) * scale;
                float bottom = (area.y - bake.overflow.y) * scale;
                if (bake == plan.main)
                    mainRect = new Rect(left * k, bottom * k, layerWidth * k, layerHeight * k);

                for (int y = 0; y < height; y++)
                {
                    float fy = (y + 0.5f) / k - bottom;
                    if (fy < 0f || fy >= layerHeight)
                        continue;

                    int sy = MapSlice(fy, layerHeight, texture.height, bake.slicedY, bake.border.y, bake.border.w);
                    for (int x = 0; x < width; x++)
                    {
                        float fx = (x + 0.5f) / k - left;
                        if (fx < 0f || fx >= layerWidth)
                            continue;

                        int sx = MapSlice(fx, layerWidth, texture.width, bake.slicedX, bake.border.x, bake.border.z);
                        Color source = pixels[sy * texture.width + sx];
                        float alpha = source.a * tint.a;
                        Color under = output[y * width + x];
                        output[y * width + x] = new Color(source.r * tint.r * alpha, source.g * tint.g * alpha, source.b * tint.b * alpha, alpha) + under * (1f - alpha);
                    }
                }
            }

            // Back to straight alpha for display
            for (int i = 0; i < output.Length; i++)
            {
                Color c = output[i];
                output[i] = c.a > 0f ? new Color(c.r / c.a, c.g / c.a, c.b / c.a, c.a) : Color.clear;
            }

            var preview = new Texture2D(width, height, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
            preview.SetPixels(output);
            preview.Apply(false, false);
            pixelsPerSourcePixel = k;
            return preview;
        }

        // Where a pixel of the stretched Image samples its sprite: borders map one to one, the middle repeats the center line
        static int MapSlice(float position, float length, int textureLength, bool sliced, float startBorder, float endBorder)
        {
            float source = !sliced ? position * textureLength / length
                : position < startBorder ? position
                : position >= length - endBorder ? textureLength - (length - position)
                : startBorder;

            return Mathf.Clamp((int)source, 0, textureLength - 1);
        }

        static Color GetPreviewColor(Settings settings, ProceduralShape.SpriteTintLayer layer, bool styler, Color graphicColor)
        {
            if (settings.replace)
                return GetImageColor(settings.tint, layer, styler, graphicColor);
            if (!settings.tint)
                return Color.white;

            // Exported alone, the sprite keeps the Graphic alpha, so only the layer's own color applies
            Color color = layer.color;
            return new Color(color.r, color.g, color.b, layer.mode == ProceduralShape.ColorMode.Base ? 1f : color.a);
        }
        #endregion

        #region Saving
        /// <summary>
        /// The asset path of the sprite, or of one of its extra sprites, such as " Outline" or " Shadow".
        /// </summary>
        public static string GetPath(Settings settings, string suffix = "")
        {
            return $"{settings.folder.TrimEnd('/')}/{SanitizeFileName(settings.fileName)}{suffix}.png";
        }

        public static string SanitizeFileName(string fileName)
        {
            fileName = (fileName ?? "").Trim();
            foreach (char character in Path.GetInvalidFileNameChars())
                fileName = fileName.Replace(character, '_');

            return fileName;
        }

        /// <summary>
        /// Why the folder or file name can't be used, or null.
        /// </summary>
        public static string GetPathError(Settings settings)
        {
            if (string.IsNullOrEmpty(settings.folder) || !AssetDatabase.IsValidFolder(settings.folder)
                || !(settings.folder == "Assets" || settings.folder.StartsWith("Assets/", StringComparison.Ordinal)))
                return "Choose a folder inside Assets.";
            if (string.IsNullOrEmpty(SanitizeFileName(settings.fileName)))
                return "Enter a file name.";

            return null;
        }

        /// <summary>
        /// Every file the plan writes.
        /// </summary>
        public static List<string> GetPaths(Plan plan, Settings settings)
        {
            var paths = new List<string> { GetPath(settings) };
            if (plan?.outline != null)
                paths.Add(GetPath(settings, " Outline"));
            if (plan?.shadow != null)
                paths.Add(GetPath(settings, " Shadow"));

            return paths;
        }

        /// <summary>
        /// Saves the plan's sprites and, if asked, replaces the shape. Returns a summary for the window.
        /// </summary>
        public static string Export(Plan plan, Settings settings, out Object result)
        {
            float pixelsPerUnit = (plan.shape.canvas != null ? plan.shape.canvas.referencePixelsPerUnit : 100f) * Mathf.Max(1, settings.scale);
            string path = GetPath(settings);
            Sprite sprite = SaveSprite(plan.main, path, pixelsPerUnit);
            Sprite outlineSprite = plan.outline != null ? SaveSprite(plan.outline, GetPath(settings, " Outline"), pixelsPerUnit) : null;
            Sprite shadowSprite = plan.shadow != null ? SaveSprite(plan.shadow, GetPath(settings, " Shadow"), pixelsPerUnit) : null;
            result = sprite;

            string files = Path.GetFileName(path) + (outlineSprite != null ? $" and {Path.GetFileName(GetPath(settings, " Outline"))}" : "");
            string size = $"{plan.main.texture.width} x {plan.main.texture.height}";
            if (!settings.replace)
                return $"Exported {files} ({size}).";

            result = Replace(plan, settings, sprite, outlineSprite, shadowSprite, out List<string> report);
            report.Insert(0, $"Replaced the shape with an Image using {files} ({size}).");
            return string.Join("\n", report);
        }

        static Sprite SaveSprite(Bake bake, string path, float pixelsPerUnit)
        {
            bool existed = AssetImporter.GetAtPath(path) is TextureImporter;
            File.WriteAllBytes(path, bake.texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.spriteBorder = bake.border;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.npotScale = TextureImporterNPOTScale.None;

            // A replaced sprite keeps the compression and filtering chosen for it, as long as its size still fits
            int maxSize = Mathf.Max(32, Mathf.NextPowerOfTwo(Mathf.Max(bake.texture.width, bake.texture.height)));
            if (!existed)
            {
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = maxSize;
            }
            else if (importer.maxTextureSize < maxSize) { importer.maxTextureSize = maxSize; }

            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteMeshType = SpriteMeshType.FullRect;
            textureSettings.spriteAlignment = (int)SpriteAlignment.Custom;
            textureSettings.spritePivot = bake.pivot;
            importer.SetTextureSettings(textureSettings);
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        #endregion

        #region Replacing
        /// <summary>
        /// Why the shape can't be replaced with an Image, or null.
        /// </summary>
        public static string GetReplaceError(ProceduralShape shape)
        {
            if (EditorUtility.IsPersistent(shape))
                return "Only shapes in a scene or an open prefab can be replaced. Open the prefab first.";
            if (Application.isPlaying)
                return "Exit Play Mode to replace the shape, since changes made in Play Mode are lost.";

            foreach (Component component in shape.GetComponents<Component>())
            {
                if (component != null && component != shape && NeedsShape(component.GetType()))
                    return $"{ObjectNames.NicifyVariableName(component.GetType().Name)} needs a Procedural Shape on this object.";
            }

            return null;
        }

        static void CollectReplaceNotes(Plan plan, Settings settings)
        {
            ProceduralShape shape = plan.shape;
            ProceduralShape.SpriteTint tint = plan.tint;
            if (settings.tint)
            {
                string layer = tint.mainIsFill ? "fill" : "outline";
                if (plan.mainStyler)
                    plan.notes.Add(tint.main.interaction ? $"A Styler Object keeps the {layer} color and its interaction states." : $"A Styler Object keeps the {layer}'s Styler color.");
                else if (UsesStylerObject(tint.main))
                    plan.notes.Add($"The Image gets the {layer}'s current color, so theme and state changes won't reach it.");

                if (tint.separateOutline)
                {
                    plan.notes.Add(plan.outlineStyler ? "The outline goes to a child Image with its own Styler Object." : "The outline goes to a child Image.");
                }
            }
            else if (shape.fillColorMode == ProceduralShape.ColorMode.Styler || shape.outlineColorMode == ProceduralShape.ColorMode.Styler)
            {
                plan.notes.Add("Styler colors are baked into the sprite. Choose Tintable to keep them themeable.");
            }

            if (plan.hasOuterShadow)
            {
                plan.notes.Add(settings.shadow == ShadowHandling.ChildImage
                    ? "An Image can't draw outside its rect, so the outer shadow moves to a child Image."
                    : "The outer shadow is left out.");
            }

            if (!string.IsNullOrEmpty(shape.shapeStyleID))
                plan.notes.Add($"The {shape.shapeStyleID} Shape Style is baked in as it looks now, so preset changes to its shape won't reach the Image.");

            if (shape.RaycastDetectionMode == ProceduralShape.RaycastMode.Shape)
                plan.notes.Add("The Image takes raycasts on its whole rect, not only the shape.");

            foreach (Component dependent in FindGraphicDependents(shape.gameObject))
                plan.notes.Add($"{ObjectNames.NicifyVariableName(dependent.GetType().Name)} needs a Graphic, so it's removed and added back with the same settings.");
        }

        static bool NeedsShape(Type type) => GetRequiredTypes(type).Exists(required => required.IsAssignableFrom(typeof(ProceduralShape)) && !required.IsAssignableFrom(typeof(Image)));

        static bool NeedsGraphic(Type type) => GetRequiredTypes(type).Exists(required => required.IsAssignableFrom(typeof(ProceduralShape)));

        static List<Type> GetRequiredTypes(Type type)
        {
            var types = new List<Type>();
            foreach (RequireComponent attribute in type.GetCustomAttributes(typeof(RequireComponent), true))
            {
                if (attribute.m_Type0 != null)
                    types.Add(attribute.m_Type0);
                if (attribute.m_Type1 != null)
                    types.Add(attribute.m_Type1);
                if (attribute.m_Type2 != null)
                    types.Add(attribute.m_Type2);
            }

            return types;
        }

        // Unity won't remove the only Graphic while these need one, and an object holds one Graphic at a time
        static List<Component> FindGraphicDependents(GameObject gameObject)
        {
            var dependents = new List<Component>();
            foreach (Component component in gameObject.GetComponents<Component>())
            {
                if (component != null && component is not Graphic && NeedsGraphic(component.GetType()))
                    dependents.Add(component);
            }

            return dependents;
        }

        struct Reference
        {
            public Component owner;
            public string path;
            public bool fits;
        }

        // What the Styler Objects need from the shape, read before it goes
        struct StylerSource
        {
            public Selectable interactable;
            public StylerPreset preset;
            public bool useDefaultPreset;
        }

        /// <summary>
        /// Swaps the shape for an Image with the sprite, in one undo step. References to the shape move to the Image where their type allows.
        /// </summary>
        static Image Replace(Plan plan, Settings settings, Sprite sprite, Sprite outlineSprite, Sprite shadowSprite, out List<string> report)
        {
            report = new List<string>();
            ProceduralShape shape = plan.shape;
            GameObject gameObject = shape.gameObject;

            bool raycastTarget = shape.raycastTarget;
            Vector4 raycastPadding = shape.raycastPadding;
            bool maskable = shape.maskable;
            Color graphicColor = shape.color;
            var stylerSource = new StylerSource { interactable = shape.interactableObject, preset = shape.Preset, useDefaultPreset = shape.useDefaultStylerPreset };
            ProceduralShape.SpriteTint tint = plan.tint;
            List<Reference> references = FindReferences(shape);

            List<Component> components = new(gameObject.GetComponents<Component>());
            int shapeIndex = components.IndexOf(shape);
            List<Component> dependents = FindGraphicDependents(gameObject);
            var dependentData = new List<(Type type, string json, int index)>();
            foreach (Component dependent in dependents)
                dependentData.Add((dependent.GetType(), EditorJsonUtility.ToJson(dependent), components.IndexOf(dependent)));

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Replace with Image");

            for (int i = dependents.Count - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(dependents[i]);

            Undo.DestroyObjectImmediate(shape);
            if (shape != null)
            {
                Undo.RevertAllDownToGroup(group);
                throw new InvalidOperationException("Unity couldn't remove the Procedural Shape. See the Console for the component that depends on it.");
            }

            Image image = Undo.AddComponent<Image>(gameObject);
            image.sprite = sprite;
            image.type = plan.main.Sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.pixelsPerUnitMultiplier = 1f;
            image.raycastTarget = raycastTarget;
            image.raycastPadding = raycastPadding;
            image.maskable = maskable;
            image.color = GetImageColor(settings.tint, tint.main, plan.mainStyler, graphicColor);

            // Back in their places, with the Image where the shape was
            var restored = new Dictionary<Component, Component>();
            var order = new List<(Component component, int index)> { (image, shapeIndex) };
            for (int i = 0; i < dependentData.Count; i++)
            {
                Component copy = Undo.AddComponent(gameObject, dependentData[i].type);
                EditorJsonUtility.FromJsonOverwrite(dependentData[i].json, copy);
                restored[dependents[i]] = copy;
                order.Add((copy, dependentData[i].index));
            }

            order.Sort((a, b) => a.index.CompareTo(b.index));
            foreach (var (component, index) in order)
                MoveComponentTo(component, index);

            int updated = 0;
            foreach (Reference reference in references)
            {
                // Owners removed above are found by instance, since they compare equal to null now
                Component owner = restored.TryGetValue(reference.owner, out Component copy) ? copy : reference.owner;
                if (owner == null)
                    continue;

                if (!reference.fits)
                {
                    string message = $"{owner.name} > {ObjectNames.NicifyVariableName(owner.GetType().Name)} ({reference.path}) needs a Procedural Shape, so it lost its reference.";
                    report.Add(message);
                    Debug.LogWarning($"[Evo] {message}", owner);
                    continue;
                }

                using var serializedObject = new SerializedObject(owner);
                SerializedProperty property = serializedObject.FindProperty(reference.path);
                if (property == null)
                    continue;

                property.objectReferenceValue = image;
                serializedObject.ApplyModifiedProperties();
                updated++;
            }

            if (updated > 0)
                report.Add(updated == 1 ? "1 reference now points to the Image." : $"{updated} references now point to the Image.");

            if (plan.mainStyler)
            {
                if (gameObject.TryGetComponent(out StylerObject existing))
                {
                    report.Add($"Kept the existing Styler Object, which targets {(existing.targetGraphic != null ? existing.targetGraphic.name : "nothing")}.");
                }
                else
                {
                    ConfigureStyler(Undo.AddComponent<StylerObject>(gameObject), image, tint.main, stylerSource);
                    report.Add(tint.main.interaction ? "Added a Styler Object with the color and its interaction states." : "Added a Styler Object with the Styler color.");
                }
            }

            // Children draw after the Image and before the content: the shadow outside the shape, then the outline
            int childIndex = 0;
            if (shadowSprite != null)
                AddChildImage(gameObject, "Shadow", plan.shadow, shadowSprite, maskable, Color.white, childIndex++, false, default, stylerSource);

            if (outlineSprite != null)
            {
                Color outlineColor = GetImageColor(true, tint.outline, plan.outlineStyler, graphicColor);
                AddChildImage(gameObject, "Outline", plan.outline, outlineSprite, maskable, outlineColor, childIndex, plan.outlineStyler, tint.outline, stylerSource);
                if (plan.outlineStyler)
                    report.Add(tint.outline.interaction ? "Added an Outline child with a Styler Object for its color and interaction states." : "Added an Outline child with a Styler Object for its color.");
            }

            Undo.CollapseUndoOperations(group);
            return image;
        }

        // The sprite leaves out what the Image color carries: the tinted color, and the Graphic alpha unless a Styler Object owns the color
        static Color GetImageColor(bool tint, ProceduralShape.SpriteTintLayer layer, bool styler, Color graphicColor)
        {
            if (!tint)
                return new Color(1f, 1f, 1f, graphicColor.a);
            if (layer.mode == ProceduralShape.ColorMode.Base)
                return layer.color;

            Color color = layer.color;
            return new Color(color.r, color.g, color.b, color.a * (styler ? 1f : graphicColor.a));
        }

        static void MoveComponentTo(Component component, int index)
        {
            for (int guard = 0; guard < 64; guard++)
            {
                Component[] current = component.GetComponents<Component>();
                int position = Array.IndexOf(current, component);
                if (position <= index || !UnityEditorInternal.ComponentUtility.MoveComponentUp(component))
                    return;
            }
        }

        static void ConfigureStyler(StylerObject styler, Image image, ProceduralShape.SpriteTintLayer layer, StylerSource source)
        {
            Styler.ColorStyle style = layer.style;
            styler.useDefaultStylerPreset = source.useDefaultPreset;
            styler.Preset = source.preset;
            styler.targetGraphic = image;
            styler.objectType = StylerObject.ObjectType.Graphic;
            styler.useCustomColor = layer.mode == ProceduralShape.ColorMode.Custom;
            styler.colorID = style.color.stylerID;
            styler.overrideAlpha = style.color.overrideAlpha;
            styler.alphaOverride = style.color.alphaOverride;
            styler.enableInteraction = layer.interaction;
            styler.interactableObject = source.interactable;
            styler.enableColorInteraction = true;
            styler.disabledColor = CopyMapping(style.disabledColor);
            styler.normalColor = CopyMapping(style.normalColor);
            styler.highlightedColor = CopyMapping(style.highlightedColor);
            styler.pressedColor = CopyMapping(style.pressedColor);
            styler.selectedColor = CopyMapping(style.selectedColor);
            styler.UpdateStyler();
        }

        static ColorMapping CopyMapping(ColorMapping mapping)
        {
            mapping ??= new ColorMapping();
            return new ColorMapping
            {
                stylerID = mapping.stylerID,
                color = mapping.color,
                overrideAlpha = mapping.overrideAlpha,
                alphaOverride = mapping.alphaOverride
            };
        }

        // Built completely before it's registered, so one record holds it. LayoutElement keeps layout groups from placing it.
        static void AddChildImage(GameObject parent, string name, Bake bake, Sprite sprite, bool maskable, Color color, int siblingIndex,
            bool addStyler, ProceduralShape.SpriteTintLayer layer, StylerSource source)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement))
            {
                layer = parent.layer
            };

            var rect = (RectTransform)child.transform;
            rect.SetParent(parent.transform, false);
            rect.SetSiblingIndex(siblingIndex);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = ((RectTransform)parent.transform).pivot;
            rect.offsetMin = new Vector2(-bake.overflow.x, -bake.overflow.y);
            rect.offsetMax = new Vector2(bake.overflow.z, bake.overflow.w);

            var image = child.GetComponent<Image>();
            image.sprite = sprite;
            image.type = bake.Sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.raycastTarget = false;
            image.maskable = maskable;
            image.color = color;
            child.GetComponent<LayoutElement>().ignoreLayout = true;

            if (addStyler)
                ConfigureStyler(child.AddComponent<StylerObject>(), image, layer, source);

            Undo.RegisterCreatedObjectUndo(child, "Replace with Image");
        }

        /// <summary>
        /// Every serialized reference to the shape in its scene or prefab stage.
        /// </summary>
        static List<Reference> FindReferences(ProceduralShape shape)
        {
            var references = new List<Reference>();
            var scene = shape.gameObject.scene;
            if (!scene.IsValid())
                return references;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Component component in root.GetComponentsInChildren<Component>(true))
                {
                    if (component == null || component == shape || component is Transform || component is CanvasRenderer)
                        continue;

                    using var serializedObject = new SerializedObject(component);
                    SerializedProperty property = serializedObject.GetIterator();
                    bool enter = true;
                    while (property.Next(enter))
                    {
                        // Lists of plain values can be long and never hold references
                        enter = property.propertyType == SerializedPropertyType.Generic && !(property.isArray && IsValueType(property.arrayElementType));
                        if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == shape)
                            references.Add(new Reference { owner = component, path = property.propertyPath, fits = AcceptsImage(property) });
                    }
                }
            }

            return references;
        }

        static bool IsValueType(string elementType) => elementType switch
        {
            "int" or "float" or "double" or "bool" or "char" or "string" or "UInt8" or "SInt8" or "UInt16" or "SInt16"
                or "UInt32" or "SInt32" or "UInt64" or "SInt64" or "Vector2f" or "Vector3f" or "Vector4f" or "ColorRGBA" or "Quaternionf" => true,
            _ => false
        };

        // An object field's type shows as PPtr<$Graphic> for scripts and PPtr<Component> for built-in types
        static bool AcceptsImage(SerializedProperty property)
        {
            string type = property.type;
            if (!type.StartsWith("PPtr<", StringComparison.Ordinal))
                return false;

            string name = type.Substring(5, type.Length - 6).TrimStart('$');
            for (Type candidate = typeof(Image); candidate != null; candidate = candidate.BaseType)
            {
                if (candidate.Name == name)
                    return true;
            }

            return false;
        }
        #endregion
    }
}
