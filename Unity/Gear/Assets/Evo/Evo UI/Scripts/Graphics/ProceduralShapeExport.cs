#if UNITY_EDITOR
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Evo.UI
{
    public partial class ProceduralShape
    {
        #region Sprite Export
        /// <summary>
        /// Editor only. The layers <see cref="RenderSprite"/> draws. Zero draws them all.
        /// </summary>
        [System.Flags]
        public enum SpriteLayers
        {
            /// <summary>
            /// The fill, with its sprite, pattern and inner shadow.
            /// </summary>
            Fill = 1,
            Outline = 2,
            /// <summary>
            /// Drawn only outside the shape when neither the fill nor the outline is drawn,
            /// so a shadow Image can sit behind the shape's own Image.
            /// </summary>
            OuterShadow = 4,
            All = Fill | Outline | OuterShadow
        }

        /// <summary>
        /// Editor only. How <see cref="RenderSprite"/> draws the shape.
        /// </summary>
        public struct SpriteRenderSettings
        {
            /// <summary>
            /// Texture pixels per local unit. Zero means one.
            /// </summary>
            public float scale;

            /// <summary>
            /// Draws the shape at this size instead of its rect's, with corner radii kept in pixels. Zero keeps the rect size.
            /// </summary>
            public Vector2 size;

            /// <summary>
            /// Limits the texture to the rect, the area an Image draws in, instead of everything the shape draws.
            /// </summary>
            public bool cropToRect;

            public SpriteLayers layers;

            /// <summary>
            /// Draws the tinted layers (see <see cref="GetSpriteTint"/>) in white, so an Image color can set them.
            /// </summary>
            public bool tint;

            /// <summary>
            /// Leaves the Graphic color's alpha out of the texture, for an Image whose color takes it instead.
            /// </summary>
            public bool skipAlpha;
        }

        /// <summary>
        /// Editor only. A layer that a white sprite leaves to its Image color.
        /// </summary>
        public readonly struct SpriteTintLayer
        {
            /// <summary>
            /// Where the layer gets its color: Base, Custom or Styler.
            /// </summary>
            public readonly ColorMode mode;

            /// <summary>
            /// The layer's current color. For Base, the Graphic color, alpha included.
            /// </summary>
            public readonly Color color;

            /// <summary>
            /// The layer's Styler color and interaction colors.
            /// </summary>
            public readonly Styler.ColorStyle style;

            /// <summary>
            /// The color follows the interaction states of interactableObject.
            /// </summary>
            public readonly bool interaction;

            public SpriteTintLayer(ColorMode mode, Color color, Styler.ColorStyle style, bool interaction)
            {
                this.mode = mode;
                this.color = color;
                this.style = style;
                this.interaction = interaction;
            }
        }

        /// <summary>
        /// Editor only. How the shape splits into white sprites for tinting, from <see cref="GetSpriteTint"/>.
        /// </summary>
        public readonly struct SpriteTint
        {
            /// <summary>
            /// Why the shape can't be drawn for tinting, or null when it can.
            /// </summary>
            public readonly string error;

            /// <summary>
            /// The fill, or the outline of a shape without a fill. Layers that share its color are white too, and black ones stay black.
            /// </summary>
            public readonly SpriteTintLayer main;

            public readonly bool mainIsFill;

            /// <summary>
            /// The outline has a color of its own, so it needs its own sprite (<see cref="SpriteLayers.Outline"/>) and Image color.
            /// </summary>
            public readonly bool separateOutline;

            public readonly SpriteTintLayer outline;

            public SpriteTint(string error) : this() => this.error = error;

            public SpriteTint(SpriteTintLayer main, bool mainIsFill, bool separateOutline, SpriteTintLayer outline)
            {
                error = null;
                this.main = main;
                this.mainIsFill = mainIsFill;
                this.separateOutline = separateOutline;
                this.outline = outline;
            }
        }

        const int ExportLayerCount = 5;
        static readonly string[] exportLayerNames = { "fill", "outline", "inner shadow", "outer shadow", "pattern" };
        static readonly InteractionState[] exportStates =
        {
            InteractionState.Disabled, InteractionState.Normal, InteractionState.Highlighted, InteractionState.Pressed, InteractionState.Selected
        };

        /// <summary>
        /// Editor only. Bakes the shape into a texture with straight alpha and sRGB colors, at one pixel per local unit times the scale.
        /// Parent masks, effects and backdrop blur are left out; screen space gradients use the texture as the screen.
        /// Bounds receive the drawn area in local units. The caller destroys the texture.
        /// </summary>
        public Texture2D RenderSprite(SpriteRenderSettings settings, out Rect bounds)
        {
            float scale = settings.scale > 0f ? settings.scale : 1f;
            bool resized = settings.size.x > 0f && settings.size.y > 0f;
            SpriteLayers layers = settings.layers == 0 ? SpriteLayers.All : settings.layers;

            // A disabled shape's Styler colors and rows may be stale, so it draws from a copy too
            if (!settings.tint && layers == SpriteLayers.All && !resized && isActiveAndEnabled)
                return RenderExport(scale, settings.cropToRect, settings.skipAlpha, out bounds);

            // Operations sample other shapes relative to this one, which a copy can't do
            if (ShapeOperationCount > 0)
                throw new System.InvalidOperationException("Shape Operations combine this shape with others, so it can only be exported as it's drawn.");

            float[] whiteLayers = null;
            if (settings.tint)
            {
                whiteLayers = new float[ExportLayerCount];
                SpriteTint tint = AnalyzeSpriteTint(whiteLayers);
                if (tint.error != null)
                    throw new System.InvalidOperationException(tint.error);

                if (tint.separateOutline && (layers & SpriteLayers.Outline) != 0)
                {
                    if ((layers & SpriteLayers.Fill) != 0)
                        throw new System.InvalidOperationException("The outline has a color of its own, so draw it on its own with SpriteLayers.Outline.");

                    for (int i = 0; i < whiteLayers.Length; i++)
                        whiteLayers[i] = -1f;

                    whiteLayers[1] = 1f;
                }
            }

            GameObject root = null;
            try
            {
                ProceduralShape copy = CreateExportCopy(resized ? settings.size : rectTransform.rect.size, layers, whiteLayers, out root);
                return copy.RenderExport(scale, settings.cropToRect, settings.skipAlpha, out bounds);
            }
            finally
            {
                if (root != null)
                    DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Editor only. Works out how the shape draws in white for an Image color to tint: the fill (or the outline of a shape
        /// without one) and the layers sharing its color turn white, and black layers stay black. An outline with its own solid
        /// color gets its own sprite. Anything else with its own color can't be tinted, since the Image color would multiply it too.
        /// </summary>
        public SpriteTint GetSpriteTint() => AnalyzeSpriteTint(new float[ExportLayerCount]);

        /// <summary>
        /// Editor only. The outer shadow draws, which reaches outside the rect.
        /// </summary>
        public bool HasOuterShadow => OuterShadowActive;

        /// <summary>
        /// Editor only. Which axes a 9-sliced sprite can stretch the way the shape does at another size, and why an axis can't, or null.
        /// Pixels alone can't tell: a curve's flat top or a slow gradient repeats a few rows too.
        /// </summary>
        public string GetSliceLimits(out bool x, out bool y)
        {
            x = y = false;
            if (scaleMode == ScaleMode.Fit)
                return "Fit scale mode keeps the shape's proportions.";
            if (sprite != null)
                return "The sprite inside the shape would stretch.";
            if (shape != ShapeType.Rectangle)
                return $"{shape} shapes curve along their whole edge.";
            if (HasShapeTransform)
                return "Rotation and skew change the whole shape.";
            if (PatternActive)
                return "The pattern would stretch.";
            if (outlineWidth > 0.001f && outlineDashed && outlineDashGap > 0f)
                return "The outline dashes would stretch.";
            if (clipMethod != ClipMethod.None && clipAmount < 1f)
                return "Clipping cuts across the shape.";

            x = y = true;
            string reason = null;
            LimitSliceByGradient(fillCenter, fillColorMode, fillGradientType, fillGradientSpace, fillGradientAngle, ref x, ref y, ref reason);
            LimitSliceByGradient(outlineWidth > 0.001f, outlineColorMode, outlineGradientType, outlineGradientSpace, outlineGradientAngle, ref x, ref y, ref reason);
            LimitSliceByGradient(InnerShadowActive, innerShadowColorMode, innerShadowGradientType, innerShadowGradientSpace, innerShadowGradientAngle, ref x, ref y, ref reason);
            LimitSliceByGradient(OuterShadowActive, outerShadowColorMode, outerShadowGradientType, outerShadowGradientSpace, outerShadowGradientAngle, ref x, ref y, ref reason);
            return reason;
        }

        // A linear gradient along one axis still stretches cleanly along the other
        static void LimitSliceByGradient(bool active, ColorMode mode, GradientType type, GradientSpace space, float angle, ref bool x, ref bool y, ref string reason)
        {
            if (!active || !IsGradientMode(mode))
                return;

            reason = "A gradient changes across the shape.";
            if (type != GradientType.Linear || space != GradientSpace.Local)
            {
                x = y = false;
                return;
            }

            float radians = angle * Mathf.Deg2Rad;
            if (Mathf.Abs(Mathf.Cos(radians)) > 0.001f)
                x = false;
            if (Mathf.Abs(Mathf.Sin(radians)) > 0.001f)
                y = false;
        }

        // whiteLayers receives the alpha each layer of the main sprite draws with in white, or -1 for layers drawn as they are
        SpriteTint AnalyzeSpriteTint(float[] whiteLayers)
        {
            for (int i = 0; i < whiteLayers.Length; i++)
                whiteLayers[i] = -1f;

            if (ShapeOperationCount > 0)
                return new SpriteTint("Shape Operations combine this shape with others, so it can only be exported as it's drawn.");

            int primary = fillCenter ? 0 : outlineWidth > 0.001f ? 1 : -1;
            if (primary < 0)
                return new SpriteTint("The shape has no fill or outline to tint.");

            GetExportLayer(primary, out ColorMode mode, out Color custom, out Styler.ColorStyle style, out Gradient _);
            if (IsGradientMode(mode))
                return new SpriteTint($"The {exportLayerNames[primary]} uses a gradient, which one Image color can't reproduce.");

            var provider = interactableObject as IStylerInteractable;
            bool useInteraction = provider != null;
            InteractionState state = useInteraction ? provider.InteractionState : InteractionState.Normal;
            Color primaryColor = ResolveExportColor(mode, custom, style, state, useInteraction);
            whiteLayers[primary] = 1f;
            bool separateOutline = false;
            SpriteTintLayer outline = default;

            for (int layer = 0; layer < ExportLayerCount; layer++)
            {
                if (layer == primary || !GetExportLayer(layer, out ColorMode layerMode, out Color layerCustom, out Styler.ColorStyle layerStyle, out Gradient gradient))
                    continue;

                if (SharesExportColor(layerMode, layerCustom, layerStyle, mode, custom, style, useInteraction))
                {
                    float alpha = ResolveExportColor(layerMode, layerCustom, layerStyle, state, useInteraction).a;
                    whiteLayers[layer] = primaryColor.a > 0.0001f ? Mathf.Clamp01(alpha / primaryColor.a) : 1f;
                }
                else if (layer == 1 && !IsGradientMode(layerMode) && !IsBlackExportLayer(layerMode, layerCustom, layerStyle, gradient, useInteraction))
                {
                    separateOutline = true;
                    outline = CreateTintLayer(layerMode, layerCustom, layerStyle, state, useInteraction);
                }
                else if (!IsBlackExportLayer(layerMode, layerCustom, layerStyle, gradient, useInteraction))
                {
                    return new SpriteTint($"The {exportLayerNames[layer]} has a color of its own, which the Image color would tint as well. " +
                        $"Make it black or give it the {exportLayerNames[primary]}'s color.");
                }
            }

            return new SpriteTint(CreateTintLayer(mode, custom, style, state, useInteraction), primary == 0, separateOutline, outline);
        }

        SpriteTintLayer CreateTintLayer(ColorMode mode, Color custom, Styler.ColorStyle style, InteractionState state, bool useInteraction)
        {
            style ??= new Styler.ColorStyle();
            Color layerColor = mode == ColorMode.Base ? color : ResolveExportColor(mode, custom, style, state, useInteraction);
            return new SpriteTintLayer(mode, layerColor, style, useInteraction && IsInteractionEnabled(mode, style));
        }


        bool GetExportLayer(int layer, out ColorMode mode, out Color custom, out Styler.ColorStyle style, out Gradient gradient)
        {
            switch (layer)
            {
                case 0:
                    mode = fillColorMode; custom = fillColor; style = fillStyler; gradient = fillGradient;
                    return fillCenter;
                case 1:
                    mode = outlineColorMode; custom = outlineColor; style = outlineStyler; gradient = outlineGradient;
                    return outlineWidth > 0.001f;
                case 2:
                    mode = innerShadowColorMode; custom = innerShadowColor; style = innerShadowStyler; gradient = innerShadowGradient;
                    return InnerShadowActive;
                case 3:
                    mode = outerShadowColorMode; custom = outerShadowColor; style = outerShadowStyler; gradient = outerShadowGradient;
                    return OuterShadowActive;
                default:
                    mode = patternColorMode; custom = patternColor; style = patternStyler; gradient = patternGradient;
                    return PatternActive;
            }
        }

        Color ResolveExportColor(ColorMode mode, Color custom, Styler.ColorStyle style, InteractionState state, bool useInteraction)
        {
            if (mode == ColorMode.Base)
                return new Color(color.r, color.g, color.b, 1f);

            return ResolveLayerColor(mode, custom, style ?? new Styler.ColorStyle(), state, useInteraction);
        }

        static bool SharesExportColor(ColorMode mode, Color custom, Styler.ColorStyle style,
            ColorMode primaryMode, Color primaryCustom, Styler.ColorStyle primaryStyle, bool useInteraction)
        {
            if (mode != primaryMode)
                return false;
            if (mode == ColorMode.Base)
                return true;
            if (mode != ColorMode.Custom && mode != ColorMode.Styler)
                return false;

            style ??= new Styler.ColorStyle();
            primaryStyle ??= new Styler.ColorStyle();
            bool states = useInteraction && style.enableInteraction;
            if (states != (useInteraction && primaryStyle.enableInteraction))
                return false;

            if (!states)
                return mode == ColorMode.Styler ? style.color.stylerID == primaryStyle.color.stylerID : SameRGB(custom, primaryCustom);

            foreach (InteractionState state in exportStates)
            {
                ColorMapping mapping = style.GetMapping(state);
                ColorMapping primaryMapping = primaryStyle.GetMapping(state);
                if (mode == ColorMode.Styler ? mapping.stylerID != primaryMapping.stylerID : !SameRGB(mapping.color, primaryMapping.color))
                    return false;
            }

            return true;
        }

        bool IsBlackExportLayer(ColorMode mode, Color custom, Styler.ColorStyle style, Gradient gradient, bool useInteraction)
        {
            if (mode == ColorMode.Gradient)
            {
                if (gradient == null)
                    return false;

                foreach (GradientColorKey key in gradient.colorKeys)
                {
                    if (!IsBlack(key.color))
                        return false;
                }

                return true;
            }

            if (mode == ColorMode.StylerGradient)
                return false;
            if (!useInteraction || !IsInteractionEnabled(mode, style))
                return IsBlack(ResolveExportColor(mode, custom, style, InteractionState.Normal, false));

            foreach (InteractionState state in exportStates)
            {
                if (!IsBlack(ResolveExportColor(mode, custom, style, state, true)))
                    return false;
            }

            return true;
        }

        static bool IsBlack(Color value) => value.r <= 0.002f && value.g <= 0.002f && value.b <= 0.002f;

        static bool SameRGB(Color a, Color b) => Mathf.Abs(a.r - b.r) < 0.002f && Mathf.Abs(a.g - b.g) < 0.002f && Mathf.Abs(a.b - b.b) < 0.002f;

        void SetExportLayerColor(int layer, Color value)
        {
            var style = new Styler.ColorStyle();
            switch (layer)
            {
                case 0: fillColorMode = ColorMode.Custom; fillColor = value; fillStyler = style; break;
                case 1: outlineColorMode = ColorMode.Custom; outlineColor = value; outlineStyler = style; break;
                case 2: innerShadowColorMode = ColorMode.Custom; innerShadowColor = value; innerShadowStyler = style; break;
                case 3: outerShadowColorMode = ColorMode.Custom; outerShadowColor = value; outerShadowStyler = style; break;
                default: patternColorMode = ColorMode.Custom; patternColor = value; patternStyler = style; break;
            }
        }

        /// <summary>
        /// Builds a hidden copy on its own canvas, so changes for the export never touch this shape, its canvas or the scene.
        /// Radius sync and styles are resolved into the copy, since it has neither the hierarchy nor needs the style again.
        /// </summary>
        ProceduralShape CreateExportCopy(Vector2 size, SpriteLayers layers, float[] whiteLayers, out GameObject root)
        {
            const HideFlags hidden = HideFlags.HideAndDontSave;
            root = EditorUtility.CreateGameObjectWithHideFlags("Procedural Shape Export", hidden, typeof(RectTransform), typeof(Canvas));
            var exportCanvas = root.GetComponent<Canvas>();
            exportCanvas.renderMode = RenderMode.WorldSpace;

            Canvas sourceCanvas = canvas;
            if (sourceCanvas != null)
            {
                exportCanvas.vertexColorAlwaysGammaSpace = sourceCanvas.vertexColorAlwaysGammaSpace;
                exportCanvas.referencePixelsPerUnit = sourceCanvas.referencePixelsPerUnit;
            }

            // Inactive until configured, so it enables once with the final values
            GameObject copyObject = EditorUtility.CreateGameObjectWithHideFlags(name, hidden, typeof(RectTransform));
            copyObject.SetActive(false);
            copyObject.transform.SetParent(root.transform, false);
            var copy = copyObject.AddComponent<ProceduralShape>();
            EditorUtility.CopySerialized(this, copy);
            copy.hideFlags = hidden;
            copy.enabled = true;

            Vector4 radii = GetRadiiPixels(GetDrawingRect());
            copy.cornerRadius = new Vector4(radii.z, radii.x, radii.y, radii.w);
            copy.radiusMode = RadiusMode.Pixels;
            copy.independentCorners = true;
            copy.squircleCorners = GetActiveSquircleCorners();
            copy.radiusSyncMode = RadiusSyncMode.None;
            copy.radiusSyncTarget = null;
            copy.shapeStyleID = "";
            copy.shapeOperations = System.Array.Empty<ShapeOperation>();
            copy.backdropBlur = new BackdropBlur.Settings { enabled = false };

            if ((layers & SpriteLayers.Fill) == 0)
            {
                copy.fillCenter = false;
                copy.fillPattern = false;
                copy.innerShadowSize = 0f;
                copy.innerShadowSoftness = 0f;
                copy.innerShadowOffset = Vector2.zero;
            }

            if ((layers & SpriteLayers.Outline) == 0)
                copy.outlineWidth = 0f;

            if ((layers & SpriteLayers.OuterShadow) == 0)
            {
                copy.outerShadowSize = 0f;
                copy.outerShadowSoftness = 0f;
                copy.outerShadowOffset = Vector2.zero;
            }
            else if ((layers & (SpriteLayers.Fill | SpriteLayers.Outline)) == 0) { copy.outerShadowOutsideOnly = true; }

            if (whiteLayers != null)
            {
                for (int layer = 0; layer < whiteLayers.Length; layer++)
                {
                    if (whiteLayers[layer] >= 0f)
                        copy.SetExportLayerColor(layer, new Color(1f, 1f, 1f, whiteLayers[layer]));
                }
            }

            var copyRect = (RectTransform)copyObject.transform;
            copyRect.anchorMin = copyRect.anchorMax = new Vector2(0.5f, 0.5f);
            copyRect.pivot = rectTransform.pivot;
            copyRect.sizeDelta = size;
            copyObject.SetActive(true);
            return copy;
        }

        Texture2D RenderExport(float scale, bool cropToRect, bool skipAlpha, out Rect bounds)
        {
            Mesh mesh = null;
            Material exportMaterial = null;
            RenderTexture target = null;
            Texture2D readback = null;
            Texture2D texture = null;
            RenderTexture previousTarget = RenderTexture.active;
            bool previousSRGBWrite = GL.sRGBWrite;
            Vector4 previousScreenParams = Shader.GetGlobalVector("_ScreenParams");
            Vector4 previousProjectionParams = Shader.GetGlobalVector("_ProjectionParams");
            bounds = default;

            try
            {
                Canvas.ForceUpdateCanvases();
                UpdateCachedParent();

                if (m_Material == null)
                    ResolveMaterialAndTextures();

                mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
                using (var vertices = new VertexHelper())
                {
                    OnPopulateMesh(vertices);
                    vertices.FillMesh(mesh);
                }

                if (mesh.vertexCount == 0)
                    throw new System.InvalidOperationException("The shape has no visible geometry to export.");

                stylePage?.Upload();

                // Keep the layout rect and pivot, extending for the quad's padding, unless the export stops at the rect
                Rect rect = rectTransform.rect;
                if (cropToRect) { bounds = rect; }
                else
                {
                    Vector2 min = Vector2.Min(rect.min, mesh.bounds.min);
                    Vector2 max = Vector2.Max(rect.max, mesh.bounds.max);
                    bounds = Rect.MinMaxRect(Mathf.Floor(min.x), Mathf.Floor(min.y), Mathf.Ceil(max.x), Mathf.Ceil(max.y));
                }

                int width = Mathf.Max(1, Mathf.RoundToInt(bounds.width * scale));
                int height = Mathf.Max(1, Mathf.RoundToInt(bounds.height * scale));
                int maxSize = Mathf.Min(SystemInfo.maxTextureSize, 8192);
                if (bounds.width <= 0f || bounds.height <= 0f || width > maxSize || height > maxSize || (long)width * height > 16777216)
                    throw new System.InvalidOperationException($"Export size {width} x {height} exceeds the supported size (up to {maxSize} per side and 16 megapixels).");
                if (!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat))
                    throw new System.NotSupportedException("Sprite export requires floating-point render texture support.");

                // Direct mesh drawing bypasses the Canvas Color32 conversion
                bool linear = IsCanvasLinear();
                if (linear || skipAlpha)
                {
                    Color32[] colors = mesh.colors32;
                    for (int i = 0; i < colors.Length; i++)
                    {
                        Color32 vertexColor = colors[i];
                        if (skipAlpha)
                            vertexColor.a = 255;

                        colors[i] = linear ? SyncColorPrecision(vertexColor, true) : vertexColor;
                    }

                    mesh.colors32 = colors;
                }

                exportMaterial = new Material(material)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    mainTexture = mainTexture
                };
                Sprite textureSprite = sprite != null ? sprite : PatternActive && patternSource == PatternSource.Sprite ? patternSprite : null;
                exportMaterial.SetTexture("_AlphaTex", textureSprite != null ? textureSprite.associatedAlphaSplitTexture : null);
                exportMaterial.SetInt("_StencilComp", (int)CompareFunction.Always);
                exportMaterial.SetInt("_StencilOp", (int)StencilOp.Keep);
                exportMaterial.SetInt("_ColorMask", (int)ColorWriteMask.All);
                exportMaterial.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
                exportMaterial.SetFloat("_SoftMask_Count", 0f);
                exportMaterial.SetFloat("_UIVertexColorAlwaysGammaSpace", canvas != null && canvas.vertexColorAlwaysGammaSpace ? 1f : 0f);
                exportMaterial.DisableKeyword("UNITY_UI_CLIP_RECT");
                exportMaterial.DisableKeyword("UNITY_UI_ALPHACLIP");
                exportMaterial.DisableKeyword("EVO_UI_BACKDROP");

                // The full program draws every shape; the light one depends on which shapes share the canvas
                exportMaterial.DisableKeyword(LiteShaderKeyword);

                target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
                RenderTexture.active = target;
                GL.sRGBWrite = false;
                Shader.SetGlobalVector("_ScreenParams", new Vector4(width, height, 1f + 1f / width, 1f + 1f / height));
                Shader.SetGlobalVector("_ProjectionParams", new Vector4(SystemInfo.graphicsUVStartsAtTop ? -1f : 1f, -1f, 1f, 1f));
                GL.Clear(false, true, Color.clear);
                GL.PushMatrix();

                try
                {
                    GL.modelview = Matrix4x4.identity;
                    GL.LoadProjectionMatrix(Matrix4x4.Ortho(bounds.xMin, bounds.xMax, bounds.yMin, bounds.yMax, -1f, 1f));
                    if (!exportMaterial.SetPass(0))
                        throw new System.InvalidOperationException("The shape material cannot be rendered for export.");
                    Graphics.DrawMeshNow(mesh, Matrix4x4.identity);
                }
                finally
                {
                    GL.PopMatrix();
                }

                readback = new Texture2D(width, height, TextureFormat.RGBAFloat, false, true) { hideFlags = HideFlags.HideAndDontSave };
                readback.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                var pixels = readback.GetPixelData<Color>(0);

                // The quad pads the shape for antialiasing, so keep only the pixels that draw, and never less than the rect
                var area = new RectInt(0, 0, width, height);
                if (!cropToRect)
                {
                    area = GetDrawnArea(pixels, width, height, (rect.min - bounds.min) * scale, (rect.max - bounds.min) * scale);
                    bounds = Rect.MinMaxRect(bounds.xMin + area.xMin / scale, bounds.yMin + area.yMin / scale,
                        bounds.xMin + area.xMax / scale, bounds.yMin + area.yMax / scale);
                }

                texture = new Texture2D(area.width, area.height, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                var output = texture.GetPixelData<Color32>(0);
                bool linearSpace = QualitySettings.activeColorSpace == ColorSpace.Linear;
                for (int y = 0; y < area.height; y++)
                {
                    for (int x = 0; x < area.width; x++)
                    {
                        // PNG stores straight alpha. Unpremultiply before encoding linear RGB as sRGB.
                        Color pixel = pixels[(y + area.y) * width + x + area.x];
                        if (pixel.a > 0f)
                        {
                            float alpha = pixel.a;
                            pixel /= alpha;
                            if (linearSpace)
                                pixel = pixel.gamma;

                            pixel.a = alpha;
                        }
                        else { pixel = Color.clear; }
                        output[y * area.width + x] = pixel;
                    }
                }

                texture.Apply(false, false);
                Texture2D result = texture;
                texture = null;
                return result;
            }
            finally
            {
                RenderTexture.active = previousTarget;
                GL.sRGBWrite = previousSRGBWrite;
                Shader.SetGlobalVector("_ScreenParams", previousScreenParams);
                Shader.SetGlobalVector("_ProjectionParams", previousProjectionParams);
                if (target != null)
                    RenderTexture.ReleaseTemporary(target);
                if (readback != null)
                    DestroyImmediate(readback);
                if (texture != null)
                    DestroyImmediate(texture);
                if (exportMaterial != null)
                    DestroyImmediate(exportMaterial);
                if (mesh != null)
                    DestroyImmediate(mesh);
                if (!isActiveAndEnabled)
                    ReleasePooledResources();
            }
        }

        // Every pixel with visible alpha, joined with the rect so the layout area always stays. Antialiasing can leave
        // alpha 1/255 next to an edge, which no screen shows, so it doesn't count.
        static RectInt GetDrawnArea(NativeArray<Color> pixels, int width, int height, Vector2 rectMin, Vector2 rectMax)
        {
            int xMin = Mathf.Clamp(Mathf.FloorToInt(rectMin.x + 0.001f), 0, width);
            int yMin = Mathf.Clamp(Mathf.FloorToInt(rectMin.y + 0.001f), 0, height);
            int xMax = Mathf.Clamp(Mathf.CeilToInt(rectMax.x - 0.001f), xMin, width);
            int yMax = Mathf.Clamp(Mathf.CeilToInt(rectMax.y - 0.001f), yMin, height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (pixels[y * width + x].a < 1.5f / 255f)
                        continue;

                    xMin = Mathf.Min(xMin, x);
                    xMax = Mathf.Max(xMax, x + 1);
                    yMin = Mathf.Min(yMin, y);
                    yMax = Mathf.Max(yMax, y + 1);
                }
            }

            return new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
        }
        #endregion
    }
}
#endif
