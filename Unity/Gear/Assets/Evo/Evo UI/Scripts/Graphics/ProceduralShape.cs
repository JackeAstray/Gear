using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;

namespace Evo.UI
{
    /// <summary>
    /// Renders anti-aliased procedural UI shapes using a four-vertex quad and an SDF shader.
    /// </summary>
    [AddComponentMenu("Evo/UI/Graphics/Procedural Shape"), HelpURL(Constants.HelpUrl + "procedural-shape")]
    [DisallowMultipleComponent, RequireComponent(typeof(CanvasRenderer), typeof(RectTransform)), NoAutoStaticsCleanup]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Evo.UI", null, "ProceduralRect")]
    public partial class ProceduralShape : MaskableGraphic, ICanvasRaycastFilter, IStylerHandler
    {
        // Styler
        [UnityEngine.Serialization.FormerlySerializedAs("presetSource")]
        [Tooltip("Locks the preset to the current default Styler Preset.")]
        public bool useDefaultStylerPreset = true;
        [SerializeField, Tooltip("The preset containing style definitions.")]
        private StylerPreset preset;
        [Tooltip("Shape Style ID resolved from the assigned Styler Preset.")]
        public string shapeStyleID = "";
        [Tooltip("Selectable that provides interaction states for enabled color layers.")]
        public Selectable interactableObject;

        // Shape
        [SerializeField, Tooltip("Procedural shape type.")]
        private ShapeType shape = ShapeType.Rectangle;
        [SerializeField, Range(MinPolygonSides, MaxPolygonSides), Tooltip("Number of polygon sides.")]
        private int polygonSides = 4;
        [SerializeField, Range(0f, 360f), Tooltip("Rotates the shape graphic about the rect center.")]
        private float shapeAngle = 0f;
        [SerializeField, Tooltip("X/Y skew in degrees. Deforms the entire graphic.")]
        private Vector2 shapeSkew = Vector2.zero;

        // Graphic
        [SerializeField, Tooltip("The source sprite image.")]
        private Sprite sprite;
        [SerializeField, Tooltip("Cover: fill the rect, crop the image." +
            "\n\nFit: preserve the image aspect ratio, or 1:1 without an image." +
            "\n\nStretch: ignore aspect ratio.")]
        private ScaleMode scaleMode = ScaleMode.Cover;
        [SerializeField, Tooltip("Raycast detection method.")]
        private RaycastMode raycastMode = RaycastMode.Standard;

        // Clipping
        [SerializeField, Tooltip("Clipping method.")]
        private ClipMethod clipMethod = ClipMethod.None;
        [SerializeField, Range(0, 3), Tooltip("Clip origin direction.")]
        private int clipOrigin = 0;
        [SerializeField, Range(0f, 1f), Tooltip("How much of the shape is visible.")]
        private float clipAmount = 1f;
        [SerializeField, Tooltip("Clockwise clipping direction.")]
        private bool clipClockwise = true;
        [SerializeField, Tooltip("Preserve corner radii on horizontal/vertical rectangle fills.")]
        private bool roundedClipEdges = false;

        // Fill
        [SerializeField, Tooltip("Fill the interior. Disable to render only the outline.")]
        private bool fillCenter = true;
        [SerializeField, Range(0f, 32f), Tooltip("Extra edge softness in pixels beyond the default 1-px AA band.")]
        private float softness = 0;
        [SerializeField, Tooltip("Add faint noise so soft shadows and gradients don't band on 8-bit screens.")]
        private bool dither = false;
        public ColorMode fillColorMode = ColorMode.Base;
        [SerializeField, Tooltip("Fill solid color.")]
        private Color fillColor = Color.white;
        public Styler.ColorStyle fillStyler = new();
        public Styler.GradientStyle fillGradientStyler = new();
        [SerializeField]
        private Gradient fillGradient = DefaultGradient();
        [SerializeField, Tooltip("Linear: along an angle. Radial: from the center to the edge. Angular: around the center.")]
        private GradientType fillGradientType = GradientType.Linear;
        [SerializeField, Tooltip("Local: map the gradient to this shape." +
            "\n\nScreen Space: map it across the screen viewport for continuous gradients.")]
        private GradientSpace fillGradientSpace = GradientSpace.Local;
        [SerializeField, Range(0f, 360f), Tooltip("Fill gradient angle in degrees.")]
        private float fillGradientAngle = 0f;
        [SerializeField, Range(0.1f, 5f), Tooltip("Fill gradient zoom. > 1 stretches, < 1 compresses.")]
        private float fillGradientZoom = 1f;
        [SerializeField, Tooltip("Swap the fill gradient start and end.")]
        private bool fillGradientReverse = false;

        // Pattern
        [SerializeField, Tooltip("Overlay a repeating pattern on the fill.")]
        private bool fillPattern = false;
        [SerializeField, Tooltip("Source of each pattern tile.")]
        private PatternSource patternSource = PatternSource.Shape;
        [SerializeField, Tooltip("Sprite tile. Use the same atlas texture as the fill sprite, with tight packing disabled.")]
        private Sprite patternSprite;
        [SerializeField]
        private ShapeType patternShape = ShapeType.Ellipse;
        [SerializeField, Range(MinPolygonSides, MaxPolygonSides)]
        private int patternPolygonSides = 4;
        [SerializeField, Min(0f), Tooltip("Tile corner radius in local pixels.")]
        private float patternRadius = 0f;
        [SerializeField, Tooltip("Tile bounds in local pixels. Sprites fit inside without stretching.")]
        private Vector2 patternSize = new(16f, 16f);
        [SerializeField, Tooltip("Horizontal and vertical space between tile bounds in local pixels.")]
        private Vector2 patternGap = new(16f, 16f);
        [SerializeField, Tooltip("Pattern translation in local pixels, before rotation. Can be animated.")]
        private Vector2 patternOffset = Vector2.zero;
        [SerializeField, Range(0f, 360f), Tooltip("Rotates the pattern about the shape center. Can be animated.")]
        private float patternAngle = 0f;
        [SerializeField, Tooltip("Fade the pattern with distance from an anchor in the drawing rect.")]
        private bool patternFalloff = false;
        [SerializeField, Tooltip("Anchor clamped to 0-1: (0, 0) is bottom left; (1, 1) is top right.")]
        private Vector2 patternFalloffOrigin = new(0.5f, 0.5f);
        [SerializeField, Min(0f), Tooltip("Distance in local pixels where the fade begins.")]
        private float patternFalloffStart = 0f;
        [SerializeField, Min(0.001f), Tooltip("Distance in local pixels where the pattern becomes invisible.")]
        private float patternFalloffEnd = 100f;
        public ColorMode patternColorMode = ColorMode.Custom;
        [SerializeField, Tooltip("Pattern solid color, independent of the fill.")]
        private Color patternColor = new(1f, 1f, 1f, 0.5f);
        public Styler.ColorStyle patternStyler = new();
        public Styler.GradientStyle patternGradientStyler = new();
        [SerializeField]
        private Gradient patternGradient = DefaultGradient();
        [SerializeField, Tooltip("Linear: along an angle. Radial: from the center to the edge. Angular: around the center.")]
        private GradientType patternGradientType = GradientType.Linear;
        [SerializeField]
        private GradientSpace patternGradientSpace = GradientSpace.Local;
        [SerializeField, Range(0f, 360f)]
        private float patternGradientAngle = 0f;
        [SerializeField, Range(0.1f, 5f)]
        private float patternGradientZoom = 1f;
        [SerializeField]
        private bool patternGradientReverse = false;

        // Outline
        [SerializeField, Min(0f), Tooltip("Outline width in pixels, drawn inward from the shape edge.")]
        private float outlineWidth = 0f;
        [SerializeField, Tooltip("Draw a repeating dash pattern along the outline.")]
        private bool outlineDashed = false;
        [SerializeField, Min(0.1f), Tooltip("Dash length in local pixels. Rounded dashes are at least as long as the outline is wide.")]
        private float outlineDashLength = 12f;
        [SerializeField, Min(0f), Tooltip("Space between dashes in local pixels.")]
        private float outlineDashGap = 8f;
        [SerializeField, Tooltip("Dash offset in repeats. One moves the pattern by one dash and gap.")]
        private float outlineDashOffset = 0f;
        [SerializeField, Tooltip("Round the ends of each dash.")]
        private bool outlineDashRounded = true;
        public ColorMode outlineColorMode = ColorMode.Custom;
        [SerializeField, Tooltip("Outline solid color.")]
        private Color outlineColor = Color.gray;
        public Styler.ColorStyle outlineStyler = new();
        public Styler.GradientStyle outlineGradientStyler = new();
        [SerializeField]
        private Gradient outlineGradient = DefaultGradient();
        [SerializeField, Tooltip("Linear: along an angle. Radial: from the center to the edge. Angular: around the center.")]
        private GradientType outlineGradientType = GradientType.Linear;
        [SerializeField, Tooltip("Local: map the gradient to this shape. Screen Space: map it across the screen viewport for continuous gradients.")]
        private GradientSpace outlineGradientSpace = GradientSpace.Local;
        [SerializeField, Range(0f, 360f), Tooltip("Outline gradient angle in degrees.")]
        private float outlineGradientAngle = 0f;
        [SerializeField, Range(0.1f, 4), Tooltip("Outline gradient zoom.")]
        private float outlineGradientZoom = 1f;
        [SerializeField, Tooltip("Swap the outline gradient start and end.")]
        private bool outlineGradientReverse = false;

        // Radius
        [SerializeField, Tooltip("Shape radius unit.")]
        private RadiusMode radiusMode = RadiusMode.Pixels;
        [SerializeField, Tooltip("Give each corner a different radius. With Radius Sync, each corner fits its own padding instead of all corners sharing one radius.")]
        private bool independentCorners = false;
        [SerializeField, Tooltip("Enable squircle (smooth) corners instead of standard rounded corners.")]
        private bool squircleCorners = false;
        [SerializeField] 
        private Vector4 cornerRadius = Vector4.zero;
        [SerializeField, Tooltip("Syncs this radius to fit perfectly inside another ProceduralShape based on padding." +
            "\n\nMatch Parent: the shape on the direct parent." +
            "\n\nMatch Target: the assigned shape." +
            "\n\nMatch Ancestor: the nearest parent shape, or the nearest object with the target name under any parent.")]
        private RadiusSyncMode radiusSyncMode = RadiusSyncMode.None;
        [SerializeField] 
        private ProceduralShape radiusSyncTarget;
        [SerializeField, Tooltip("Each parent, nearest first, is searched for a child with this name. Accepts a relative path such as Frame/Background." +
            "\n\nLeave empty to use the nearest parent with a Procedural Shape.")]
        private string radiusSyncTargetName = "";

        // Inner Shadow
        [SerializeField]
        private Vector2 innerShadowOffset = Vector2.zero;
        [SerializeField, Min(0f), Tooltip("Expansion or contraction size of the inner shadow.")]
        private float innerShadowSize = 0f;
        [SerializeField, Min(0f), Tooltip("Blur radius.")]
        private float innerShadowSoftness = 0f;
        public ColorMode innerShadowColorMode = ColorMode.Custom;
        [SerializeField, Tooltip("Inner Shadow solid color.")]
        private Color innerShadowColor = new(0f, 0f, 0f, 0.5f);
        public Styler.ColorStyle innerShadowStyler = new();
        public Styler.GradientStyle innerShadowGradientStyler = new();
        [SerializeField]
        private Gradient innerShadowGradient = DefaultGradient();
        [SerializeField, Tooltip("Linear: along an angle. Radial: from the center to the edge. Angular: around the center.")]
        private GradientType innerShadowGradientType = GradientType.Linear;
        [SerializeField, Tooltip("Local: map the gradient to this shape. Screen Space: map it across the screen viewport for continuous gradients.")]
        private GradientSpace innerShadowGradientSpace = GradientSpace.Local;
        [SerializeField, Range(0f, 360f), Tooltip("Inner Shadow gradient angle in degrees.")]
        private float innerShadowGradientAngle = 0f;
        [SerializeField, Range(0.1f, 4f), Tooltip("Inner Shadow gradient zoom.")]
        private float innerShadowGradientZoom = 1f;
        [SerializeField, Tooltip("Swap the inner shadow gradient start and end.")]
        private bool innerShadowGradientReverse = false;

        // Outer Shadow
        [SerializeField] 
        private Vector2 outerShadowOffset = Vector2.zero;
        [SerializeField, Min(0f), Tooltip("Expansion or contraction size of the outer shadow.")]
        private float outerShadowSize = 0f;
        [SerializeField, Min(0f), Tooltip("Blur radius.")]
        private float outerShadowSoftness = 0f;
        [SerializeField, Tooltip("Remove the shadow from inside the original shape so it begins at the outer edge.")]
        private bool outerShadowOutsideOnly = false;
        public ColorMode outerShadowColorMode = ColorMode.Custom;
        [SerializeField, Tooltip("Outer Shadow solid color.")]
        private Color outerShadowColor = new(0f, 0f, 0f, 0.5f);
        public Styler.ColorStyle outerShadowStyler = new();
        public Styler.GradientStyle outerShadowGradientStyler = new();
        [SerializeField]
        private Gradient outerShadowGradient = DefaultGradient();
        [SerializeField, Tooltip("Linear: along an angle. Radial: from the center to the edge. Angular: around the center.")]
        private GradientType outerShadowGradientType = GradientType.Linear;
        [SerializeField, Tooltip("Local: map the gradient to this shape. Screen Space: map it across the screen viewport for continuous gradients.")]
        private GradientSpace outerShadowGradientSpace = GradientSpace.Local;
        [SerializeField, Range(0f, 360f), Tooltip("Outer Shadow gradient angle in degrees.")]
        private float outerShadowGradientAngle = 0f;
        [SerializeField, Range(0.1f, 4f), Tooltip("Outer Shadow gradient zoom.")]
        private float outerShadowGradientZoom = 1f;
        [SerializeField, Tooltip("Swap the outer shadow gradient start and end.")]
        private bool outerShadowGradientReverse = false;

        // Runtime State
        ProceduralShape cachedParentShape, registeredRadiusSource;
        RadiusSyncMode cachedRadiusSyncMode;
        List<ProceduralShape> radiusSyncDependents;
        bool radiusSyncSearchPending;
        int radiusSyncNotifyPass;
        static int radiusSyncPass, radiusSyncPassDepth;
        bool spriteTracked;
        // Set by Interactive Effects while the mesh is generated: effect rows need the full shader program
        internal bool meshHasEffectRow;
        // Enabled Interactive effects that target this shape ask for the full program before their row exists
        int effectRequests;
        static bool spriteAtlasTrackingInitialized;
        static readonly List<ProceduralShape> trackedTexturelessShapes = new();

        // Properties
        public bool SupportsRadius => shape != ShapeType.Ellipse;
        public bool UsesIndependentCorners => shape == ShapeType.Rectangle && independentCorners;
        public bool UsesSquircleCorners => shape == ShapeType.Rectangle && GetActiveSquircleCorners();
        public bool UsesRoundedClipEdges => roundedClipEdges &&
            ((shape == ShapeType.Rectangle && (clipMethod == ClipMethod.Horizontal || clipMethod == ClipMethod.Vertical)) ||
             (clipMethod == ClipMethod.Radial360 && !fillCenter && outlineWidth > 0.001f));
        bool HasInteraction => interactableInterface != null && AnyInteractionEnabled();
        bool UsesRadiusSync => shape == ShapeType.Rectangle && radiusSyncMode != RadiusSyncMode.None;

        /// <summary>
        /// The shape this rectangle resolves its radius from, or null when sync is off or finds no shape.
        /// </summary>
        public ProceduralShape RadiusSyncSource
        {
            get
            {
                if (!UsesRadiusSync)
                    return null;
                if (radiusSyncMode == RadiusSyncMode.MatchTarget)
                    return radiusSyncTarget;
                if (radiusSyncSearchPending && radiusSyncMode == RadiusSyncMode.MatchAncestor)
                    FindAncestorRadiusSource();

                return cachedParentShape;
            }
        }

        protected ProceduralShape()
        {
#if !UNITY_6000_6_OR_NEWER
            // Before Unity 6.6, Graphic defaults to the allocating legacy Mesh path
            useLegacyMeshGeneration = false;
#endif
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EnsureStyleUploadCallback();
            base.raycastTarget = !operandOnly && raycastMode != RaycastMode.None;
            UpdateOperationTracking();
      
            Styler.SyncDefaultPreset(ref preset, useDefaultStylerPreset);
            Styler.Register(this);
            EnsureCanvasChannels();
            UpdateCachedParent();
            UpdateSpriteTracking();
            UpdateStyler();
            UpdateBackdropBlur();
            SetMaterialDirty();
#if UNITY_EDITOR
            // A shape enabled in edit mode can become the source of an earlier Match Ancestor search
            ScheduleEditorAncestorRefresh();
#endif
        }

        protected override void OnDisable()
        {
            UntrackOperations();
            ReleaseBackdropBlur();
            Styler.Unregister(this);
            UnsubscribeInteraction();
            ReleaseRadiusSource();
            base.OnDisable();
            UntrackSprite();
            // Disabled shapes keep their style row in play mode, but not their say in the page's shader program
            stylePage?.SetFullShader(styleSlot, false);

#if UNITY_EDITOR
            // Retain style rows while pooled objects are disabled; OnDestroy releases them
            if (!Application.isPlaying || UnityEditor.EditorApplication.isCompiling)
                ReleasePooledResources();
#endif
        }

        protected override void OnDestroy()
        {
            UntrackOperations();
            ReleaseBackdropBlur();
            Styler.Unregister(this);
            UnsubscribeInteraction();
            ReleaseRadiusSource();
            ResetRadiusSyncDependents(true);
            base.OnDestroy();
            UntrackSprite();
            ReleasePooledResources();
        }

        protected override void OnTransformParentChanged()
        {
            gradientRefreshHold++;
            try { base.OnTransformParentChanged(); }
            finally { gradientRefreshHold--; }

            EnsureCanvasChannels();
            UpdateCachedParent();
            ResetRadiusSyncDependents(false);
            SetVerticesDirty();
        }

        protected override void OnCanvasHierarchyChanged()
        {
            // The base call marks the material dirty
            gradientRefreshHold++;
            try { base.OnCanvasHierarchyChanged(); }
            finally { gradientRefreshHold--; }

            EnsureCanvasChannels();
        }

        protected override void OnDidApplyAnimationProperties()
        {
            shapeSkew = ClampShapeSkew(shapeSkew);
            patternFalloffOrigin = ClampPatternOrigin(patternFalloffOrigin);
            base.OnDidApplyAnimationProperties();
            base.raycastTarget = !operandOnly && raycastMode != RaycastMode.None;
            UpdateOperationTracking();
            UpdateCachedParent(false);
            UpdateSpriteTracking();
            UpdateBackdropBlur();
        }

        #region Sprite Atlas Integration
        /// <summary>
        /// Keeps textureless sprites registered until a compatible Sprite Atlas becomes available.
        /// </summary>
        void UpdateSpriteTracking()
        {
            if (isActiveAndEnabled && ((sprite != null && sprite.texture == null) ||
                (fillPattern && patternSource == PatternSource.Sprite && patternSprite != null && patternSprite.texture == null)))
                TrackSprite();
            else
                UntrackSprite();
        }

        void TrackSprite()
        {
            if (spriteTracked)
                return;

            if (!spriteAtlasTrackingInitialized)
            {
                SpriteAtlasManager.atlasRegistered += RebuildTexturelessShapes;
                spriteAtlasTrackingInitialized = true;
            }

            trackedTexturelessShapes.Add(this);
            spriteTracked = true;
        }

        void UntrackSprite()
        {
            if (!spriteTracked)
                return;

            trackedTexturelessShapes.Remove(this);
            spriteTracked = false;
        }

        /// <summary>
        /// Rebuilds tracked graphics whose sprites can bind to a newly registered atlas.
        /// </summary>
        static void RebuildTexturelessShapes(SpriteAtlas spriteAtlas)
        {
            for (int i = trackedTexturelessShapes.Count - 1; i >= 0; i--)
            {
                ProceduralShape graphic = trackedTexturelessShapes[i];
                if (graphic == null)
                {
                    trackedTexturelessShapes.RemoveAt(i);
                    continue;
                }

                if ((graphic.sprite != null && spriteAtlas.CanBindTo(graphic.sprite)) ||
                    (graphic.patternSprite != null && spriteAtlas.CanBindTo(graphic.patternSprite)))
                {
                    trackedTexturelessShapes.RemoveAt(i);
                    graphic.spriteTracked = false;
                    graphic.UpdateSpriteTracking();
                    if (graphic.fillPattern)
                        graphic.UpdateStyler();
                    graphic.SetAllDirty();
                }
            }
        }
        #endregion

        #region Mesh Generation
        const int AlphaSplitFlag = 64;
        // Dithering flips the inner softness sign; zero softness carries it as this value, below the shader's 0.001 threshold
        const float DitherSoftness = 0.00001f;

        /// <summary>
        /// Builds the repeated per-vertex payload for the procedural quad.
        /// </summary>
        struct VertexGenerator
        {
            // Sprite UV Mapping
            public Rect rect;
            public float uvMinX, uvMaxX, uvMinY, uvMaxY;
            public float invRectWidth, invRectHeight;

            // Packed Shader Payload
            public Vector3 normal;
            public Vector4 tangent;
            public Color32 color;
            public Vector4 uv1, uv2, uv3;

            /// <summary>
            /// Creates one quad vertex with extrapolated sprite UVs and packed SDF data.
            /// </summary>
            public UIVertex Generate(float x, float y, float sx, float sy, Matrix4x4 transform)
            {
                UIVertex vertex = Generate(x, y, sx, sy);
                vertex.position = transform.MultiplyPoint3x4(vertex.position);
                return vertex;
            }

            public UIVertex Generate(float x, float y, float sx, float sy)
            {
                // Calculate correct 0-1 percentage mapping relative to the rect boundaries (not the expanded bounds)
                float tx = (x - rect.xMin) * invRectWidth;
                float ty = (y - rect.yMin) * invRectHeight;

                // Extrapolate the mapped Sprite UV out to the expanded edge of the vertex
                float uvX = Mathf.LerpUnclamped(uvMinX, uvMaxX, tx);
                float uvY = Mathf.LerpUnclamped(uvMinY, uvMaxY, ty);

                return new UIVertex
                {
                    position = new Vector3(x, y),
                    normal = normal,
                    tangent = tangent,
                    color = color,
                    uv0 = new Vector4(sx, sy, uvX, uvY), // Pass SDF coords (XY) and Sprite UVs (ZW) together
                    uv1 = uv1,
                    uv2 = uv2,
                    uv3 = uv3
                };
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            meshHasEffectRow = false;

            if (operandOnly)
                return;

            Rect rect = GetDrawingRect();
            int styleIndex = GetMeshStyleIndex();
            WriteShapeOperations(rect);
            meshOperationCount = activeOperationCount;
            meshOperationBounds = operationBounds;
            meshOperationStretch = operationDistanceStretch;

            if (rect.width < 0.001f || rect.height < 0.001f)
                return;
            if (!fillCenter && outlineWidth <= 0.001f && !InnerShadowActive && !OuterShadowActive)
                return;
            if (clipMethod != ClipMethod.None && clipAmount <= 0f)
                return;

            float safeSoftness = Mathf.Max(0f, softness);
            float safeOuterSize = Mathf.Max(0f, outerShadowSize);
            float safeOuterSoftness = Mathf.Max(0f, outerShadowSoftness);
            float shapeExpand = safeSoftness + 2f;
            Vector2 shapePadding = Vector2.one * shapeExpand;
            Vector2 shadowPadding = Vector2.one * (safeOuterSize + safeOuterSoftness + 2f);

            if (activeOperationCount > 0)
            {
                shapePadding *= operationDistanceStretch;
                shadowPadding *= operationDistanceStretch;
            }

            if (UsesRoundedClipEdges && shape == ShapeType.Ellipse)
            {
                // Arc distances stretch with an elliptical rect. Expand its
                // quad by the same scale so softness and shadows cannot crop.
                float minDimension = Mathf.Min(rect.width, rect.height);
                float scaleX = rect.width / minDimension;
                float scaleY = rect.height / minDimension;
                Vector2 scale = new(scaleX, scaleY);
                shapePadding = Vector2.Scale(shapePadding, scale);
                shadowPadding = Vector2.Scale(shadowPadding, scale);
            }

            // Bound the union of the shape and its offset shadow.
            // Symmetric padding shades an unnecessary strip opposite the shadow offset.
            Vector2 minPadding = Vector2.Max(shapePadding, shadowPadding - outerShadowOffset);
            Vector2 maxPadding = Vector2.Max(shapePadding, shadowPadding + outerShadowOffset);
            Rect meshRect = rect;

            if (activeOperationCount > 0) { meshRect = operationBounds; }
            else if (clipMethod == ClipMethod.Horizontal || clipMethod == ClipMethod.Vertical)
            {
                float amount = Mathf.Clamp01(clipAmount);

                // Avoid shading the empty portion of a progress bar: the shader clips everything past the cut,
                // shadows and effects included, and the padding below covers its antialiasing.
                // Keep the original rect below for radii, sprite UVs, and gradient mapping.
                bool fromMin = (Mathf.Clamp(clipOrigin, 0, 1) == 0) == clipClockwise;
                if (clipMethod == ClipMethod.Horizontal)
                {
                    meshRect.width *= amount;
                    if (!fromMin)
                        meshRect.x = rect.xMax - meshRect.width;
                }
                else
                {
                    meshRect.height *= amount;
                    if (!fromMin)
                        meshRect.y = rect.yMax - meshRect.height;
                }
            }

            float xMin = meshRect.xMin - minPadding.x;
            float xMax = meshRect.xMax + maxPadding.x;
            float yMin = meshRect.yMin - minPadding.y;
            float yMax = meshRect.yMax + maxPadding.y;
            float cx = rect.center.x;
            float cy = rect.center.y;

            // Shape flags: bit 0 = fill, bit 1 = squircle, bits 2-5 = shape, bit 6 = split alpha
            int shapeAndFlags = (fillCenter ? 1 : 0) + (UsesSquircleCorners ? 2 : 0) + ShaderShapeID * 4;
            if (sprite != null && sprite.associatedAlphaSplitTexture != null)
                shapeAndFlags += AlphaSplitFlag;

            shapeAndFlags += styleIndex * StyleIndexStride;

            // Negative softness enables patterns without adding a vertex channel or changing shape flags
            float packedSoftness = PatternActive && stylePage != null ? -safeSoftness - 1f : safeSoftness;

            // UV1 = rect size, signed edge softness, shape flags and style index
            // Negative rect height skips the white fill texture for compatible automatic materials
            // UV2 = signed outline width (negative enables dashes), signed inner softness (negative enables dithering),
            // clip amount, clip flags
            // UV3 = resolved radii or round-arc data; Tangent = outer shadow; Normal = inner shadow
            // Automatic materials read both shadows from the style row instead (see OuterShadowData)
            // Negative width enables operations without another vertex channel or keyword
            Vector4 uv1 = new(rect.width, rect.height, packedSoftness, shapeAndFlags);
            if (activeOperationCount > 0)
                uv1.x = -uv1.x;
            if (styleIndex > 0 && stylePage.optimizedData && sprite == null)
                uv1.y = -uv1.y;

            Vector4 uv2 = new(Mathf.Max(0f, outlineWidth), Mathf.Max(0f, innerShadowSoftness), Mathf.Clamp01(clipAmount), GetPackedClipConfig()); 
            if (outlineDashed && outlineDashGap > 0f)
                uv2.x = -uv2.x;
            if (dither)
                uv2.y = -Mathf.Max(uv2.y, DitherSoftness);
           
            Vector4 uv3 = GetShaderRadii(rect);
            Vector4 tangent = OuterShadowData;
            Vector4 normal = InnerShadowData;
            if (styleIndex > 0)
            {
                stylePage.WriteShadows(styleSlot, tangent, normal);

                // Without a pattern or operations the pattern row goes back to the page
                if (packedSoftness >= 0f && activeOperationCount == 0)
                    stylePage.ReleasePatternRow(styleSlot);
            }

            Color32 c32 = color;
            Vector4 spriteUV = sprite != null ? UnityEngine.Sprites.DataUtility.GetOuterUV(sprite) : new Vector4(0, 0, 1, 1);
            float uvMinX = spriteUV.x;
            float uvMaxX = spriteUV.z;
            float uvMinY = spriteUV.y;
            float uvMaxY = spriteUV.w;
            Rect spriteRect = activeOperationCount > 0 ? operationBounds : rect;

            // Handle Preserve Aspect Cover (crop UV mapping to fit bounds)
            if (sprite != null && scaleMode == ScaleMode.Cover && spriteRect.width > 0.001f && spriteRect.height > 0.001f && sprite.rect.height > 0.001f)
            {
                float rectAspect = spriteRect.width / spriteRect.height;
                float spriteAspect = sprite.rect.width / sprite.rect.height;
                if (spriteAspect > rectAspect)
                {
                    // Sprite is relatively wider than the rect. Crop left and right.
                    float scale = rectAspect / spriteAspect;
                    float uvWidth = (spriteUV.z - spriteUV.x) * scale;
                    float uvCenter = (spriteUV.x + spriteUV.z) * 0.5f;
                    uvMinX = uvCenter - uvWidth * 0.5f;
                    uvMaxX = uvCenter + uvWidth * 0.5f;
                }
                else
                {
                    // Sprite is relatively taller than the rect. Crop top and bottom.
                    float scale = spriteAspect / rectAspect;
                    float uvHeight = (spriteUV.w - spriteUV.y) * scale;
                    float uvCenter = (spriteUV.y + spriteUV.w) * 0.5f;
                    uvMinY = uvCenter - uvHeight * 0.5f;
                    uvMaxY = uvCenter + uvHeight * 0.5f;
                }
            }

            var vGen = new VertexGenerator
            {
                rect = spriteRect,
                uvMinX = uvMinX,
                uvMaxX = uvMaxX,
                uvMinY = uvMinY,
                uvMaxY = uvMaxY,
                invRectWidth = 1f / spriteRect.width,
                invRectHeight = 1f / spriteRect.height,
                normal = normal,
                tangent = tangent,
                color = c32,
                uv1 = uv1,
                uv2 = uv2,
                uv3 = uv3
            };

            if (HasShapeTransform)
            {
                Matrix4x4 shapeTransform = GetShapeTransform(rect.center);
                vh.AddVert(vGen.Generate(xMin, yMin, xMin - cx, yMin - cy, shapeTransform));
                vh.AddVert(vGen.Generate(xMin, yMax, xMin - cx, yMax - cy, shapeTransform));
                vh.AddVert(vGen.Generate(xMax, yMax, xMax - cx, yMax - cy, shapeTransform));
                vh.AddVert(vGen.Generate(xMax, yMin, xMax - cx, yMin - cy, shapeTransform));
            }
            else
            {
                vh.AddVert(vGen.Generate(xMin, yMin, xMin - cx, yMin - cy));
                vh.AddVert(vGen.Generate(xMin, yMax, xMin - cx, yMax - cy));
                vh.AddVert(vGen.Generate(xMax, yMax, xMax - cx, yMax - cy));
                vh.AddVert(vGen.Generate(xMax, yMin, xMax - cx, yMin - cy));
            }

            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }

        /// <summary>
        /// Called by Interactive effects on enable and disable, so the program compiles and switches with the Canvas
        /// rather than on the first hover.
        /// </summary>
        internal void SetEffectRequest(bool request)
        {
            effectRequests = Mathf.Max(0, effectRequests + (request ? 1 : -1));
            UpdateShaderProgram();
        }

        // Mesh modifiers run inside the base call; the page's shader program follows the finished mesh
        protected override void UpdateGeometry()
        {
            base.UpdateGeometry();
            UpdateShaderProgram();
        }
        #endregion

        #region Drawing Rect and Radius Resolution
        /// <summary>
        /// Gets the local-space rect used to generate and raycast the shape.
        /// </summary>
        public Rect GetDrawingRect()
        {
            Rect rect = rectTransform.rect;
            if (scaleMode == ScaleMode.Fit)
                rect = CalculateFitRect(rect, sprite);

            return rect;
        }

        static Rect CalculateFitRect(Rect rect, Sprite sprite)
        {
            // Prevent Divide-by-Zero / NaN geometry if an invalid/empty rect or sprite is passed
            if (rect.width <= 0.001f || rect.height <= 0.001f
                || (sprite != null && (sprite.rect.height <= 0.001f || sprite.rect.width <= 0.001f)))
                return rect;

            // Shapes without an image use a square drawing rect to preserve their proportions.
            float spriteAspect = sprite != null ? sprite.rect.width / sprite.rect.height : 1f;
            float rectAspect = rect.width / rect.height;
            if (spriteAspect > rectAspect)
            {
                float newHeight = rect.width / spriteAspect;
                return new Rect(rect.x, rect.center.y - newHeight * 0.5f, rect.width, newHeight);
            }
            else
            {
                float newWidth = rect.height * spriteAspect;
                return new Rect(rect.center.x - newWidth * 0.5f, rect.y, newWidth, rect.height);
            }
        }

        /// <summary>
        /// Enables every Canvas vertex channel used by the packed procedural payload.
        /// </summary>
        void EnsureCanvasChannels()
        {
            if (canvas != null)
            {
                var required = AdditionalCanvasShaderChannels.TexCoord1 |
                               AdditionalCanvasShaderChannels.TexCoord2 |
                               AdditionalCanvasShaderChannels.TexCoord3 |
                               AdditionalCanvasShaderChannels.Tangent |
                               AdditionalCanvasShaderChannels.Normal;

                if ((canvas.additionalShaderChannels & required) != required)
                    canvas.additionalShaderChannels |= required;
            }
        }

        /// <summary>
        /// Resolves the final shader-order radii for a drawing rect.
        /// </summary>
        public Vector4 GetRadiiPixels(Rect rect) => GetRadiiPixels(rect, !HasRadiusSyncCycle());

        Vector4 GetRadiiPixels(Rect rect, bool allowRadiusSync)
        {
            if (!SupportsRadius)
                return Vector4.zero;

            float maxR = Mathf.Max(0f, Mathf.Min(rect.width, rect.height) * 0.5f);

            // Intercept manual radii if we are syncing
            if (allowRadiusSync)
            {
                ProceduralShape target = RadiusSyncSource;
                if (target != null && target != this)
                    return ClampRadii(CalculateSyncedRadii(target, rect, allowRadiusSync), maxR);
            }

            float P(float val) => Mathf.Clamp(radiusMode == RadiusMode.Percentage ? val * 0.01f * maxR : val, 0f, maxR);

            if (!UsesIndependentCorners)
            {
                float uniformRadius = P(cornerRadius.x);
                return new Vector4(uniformRadius, uniformRadius, uniformRadius, uniformRadius);
            }

            // TopRight, BottomRight, TopLeft, BottomLeft mapping
            return new Vector4(P(cornerRadius.y), P(cornerRadius.z), P(cornerRadius.x), P(cornerRadius.w));
        }

        /// <summary>
        /// Uses cycle detection to prevent recursive radius-sync chains.
        /// </summary>
        bool HasRadiusSyncCycle()
        {
            ProceduralShape slow = RadiusSyncSource;
            ProceduralShape fast = slow != null ? slow.RadiusSyncSource : null;

            while (slow != null && fast != null)
            {
                if (slow == fast)
                    return true;

                slow = slow.RadiusSyncSource;
                fast = fast.RadiusSyncSource;
                fast = fast != null ? fast.RadiusSyncSource : null;
            }

            return false;
        }

        bool GetActiveSquircleCorners()
        {
            ProceduralShape target = HasRadiusSyncCycle() ? null : RadiusSyncSource;
            if (target != null && target != this)
            {
                while (target.RadiusSyncSource != null)
                    target = target.RadiusSyncSource;

                return target.squircleCorners;
            }

            return squircleCorners;
        }

        static Vector4 ClampRadii(Vector4 radii, float maxRadius)
        {
            radii.x = Mathf.Clamp(radii.x, 0f, maxRadius);
            radii.y = Mathf.Clamp(radii.y, 0f, maxRadius);
            radii.z = Mathf.Clamp(radii.z, 0f, maxRadius);
            radii.w = Mathf.Clamp(radii.w, 0f, maxRadius);
            return radii;
        }

        /// <summary>
        /// Calculates inset radii from the target shape and the local padding between both rects.
        /// Independent corners fit their own padding; linked corners share one radius.
        /// </summary>
        Vector4 CalculateSyncedRadii(ProceduralShape target, Rect cRect, bool allowRadiusSync)
        {
            // Get the target's final resolved radii using its actual drawing rect
            Rect pRect = target.GetDrawingRect();
            Vector4 pRadii = target.GetRadiiPixels(pRect, allowRadiusSync);

            // Calculate local bounds using matrices
            Vector2 min = cRect.min;
            Vector2 max = cRect.max;

            Matrix4x4 childToTarget = target.transform.worldToLocalMatrix * transform.localToWorldMatrix;

            Vector3 bl = childToTarget.MultiplyPoint3x4(new(min.x, min.y, 0));
            Vector3 tl = childToTarget.MultiplyPoint3x4(new(min.x, max.y, 0));
            Vector3 tr = childToTarget.MultiplyPoint3x4(new(max.x, max.y, 0));
            Vector3 br = childToTarget.MultiplyPoint3x4(new(max.x, min.y, 0));

            float minX = Mathf.Min(Mathf.Min(bl.x, tl.x), Mathf.Min(tr.x, br.x));
            float maxX = Mathf.Max(Mathf.Max(bl.x, tl.x), Mathf.Max(tr.x, br.x));
            float minY = Mathf.Min(Mathf.Min(bl.y, tl.y), Mathf.Min(tr.y, br.y));
            float maxY = Mathf.Max(Mathf.Max(bl.y, tl.y), Mathf.Max(tr.y, br.y));

            // Determine padding on all 4 sides for aligned rects
            float padLeft = minX - pRect.xMin;
            float padRight = pRect.xMax - maxX;
            float padBottom = minY - pRect.yMin;
            float padTop = pRect.yMax - maxY;

            // Convert target-space inset radii back to child-local units
            float scaleX = Mathf.Max(0.0001f, childToTarget.MultiplyVector(Vector3.right).magnitude);
            float scaleY = Mathf.Max(0.0001f, childToTarget.MultiplyVector(Vector3.up).magnitude);

            // Insetting a corner by unequal paddings gives an ellipse; independent corners draw the circle of the same area.
            // A corner as far from the target's corner as its radius, on either axis, stays square.
            if (UsesIndependentCorners)
            {
                float Fit(float radius, float padX, float padY)
                {
                    float x = (radius - padX) / scaleX, y = (radius - padY) / scaleY;
                    return x <= 0f || y <= 0f ? 0f : x == y ? x : Mathf.Sqrt(x * y);
                }

                return new Vector4(Fit(pRadii.x, padRight, padTop), Fit(pRadii.y, padRight, padBottom), Fit(pRadii.z, padLeft, padTop), Fit(pRadii.w, padLeft, padBottom));
            }

            // A single corner radius approximates unequal padding or non-uniform scale
            float R(float radius, float padX, float padY) => Mathf.Max(0f, Mathf.Max((radius - padX) / scaleX, (radius - padY) / scaleY));
            float outTR = R(pRadii.x, padRight, padTop);
            float outBR = R(pRadii.y, padRight, padBottom);
            float outTL = R(pRadii.z, padLeft, padTop);
            float outBL = R(pRadii.w, padLeft, padBottom);

            // Share the roundest fit so uneven padding, such as a scrollbar gutter, cannot square off one side.
            // Corners past the target edge are skipped; a shape outside on every corner keeps the roundest overall.
            const float edgeTolerance = -0.01f;
            bool left = padLeft >= edgeTolerance, right = padRight >= edgeTolerance;
            bool bottom = padBottom >= edgeTolerance, top = padTop >= edgeTolerance;
            float shared = -1f;
            if (right && top)
                shared = Mathf.Max(shared, outTR);
            if (right && bottom)
                shared = Mathf.Max(shared, outBR);
            if (left && top)
                shared = Mathf.Max(shared, outTL);
            if (left && bottom)
                shared = Mathf.Max(shared, outBL);
            if (shared < 0f)
                shared = Mathf.Max(Mathf.Max(outTR, outBR), Mathf.Max(outTL, outBL));

            return new Vector4(shared, shared, shared, shared);
        }

        /// <summary>
        /// Refreshes the radius-sync source and the change notifications this shape receives from it.
        /// MatchAncestor defers its search to the next radius read, so repeated hierarchy changes cost one search.
        /// </summary>
        void UpdateCachedParent(bool hierarchyChanged = true)
        {
            RadiusSyncMode mode = UsesRadiusSync ? radiusSyncMode : RadiusSyncMode.None;

            // Animation and styles cannot move objects, so a MatchAncestor search stays valid until the mode changes
            if (!hierarchyChanged && mode == RadiusSyncMode.MatchAncestor && cachedRadiusSyncMode == mode)
                return;

            cachedRadiusSyncMode = mode;
            cachedParentShape = null;
            radiusSyncSearchPending = mode == RadiusSyncMode.MatchAncestor;
#if UNITY_EDITOR
            TrackEditorAncestorSearch(radiusSyncSearchPending && isActiveAndEnabled);
#endif
            if (radiusSyncSearchPending)
                return;

            if (mode == RadiusSyncMode.MatchParent && transform.parent != null)
                transform.parent.TryGetComponent(out cachedParentShape);

            RegisterRadiusSource(mode == RadiusSyncMode.MatchTarget ? radiusSyncTarget : cachedParentShape);
        }

        void ReleaseRadiusSource()
        {
            RegisterRadiusSource(null);
#if UNITY_EDITOR
            TrackEditorAncestorSearch(false);
#endif
        }

        /// <summary>
        /// Walks the parents nearest first. Each step is one native lookup without allocations.
        /// </summary>
        void FindAncestorRadiusSource()
        {
            radiusSyncSearchPending = false;
            cachedParentShape = null;

            Transform self = transform;
            string targetName = radiusSyncTargetName;
            bool byName = !string.IsNullOrEmpty(targetName);

            for (Transform parent = self.parent; parent != null; parent = parent.parent)
            {
                // A named match may be this shape or one of its children; keep looking further up
                Transform candidate = byName ? parent.Find(targetName) : parent;
                if (candidate != null && candidate.TryGetComponent(out ProceduralShape source) && (!byName || !candidate.IsChildOf(self)))
                {
                    cachedParentShape = source;
                    break;
                }
            }

            RegisterRadiusSource(cachedParentShape);
        }

        /// <summary>
        /// Subscribes to rebuilds of the source so synced radii follow its rect and radius.
        /// </summary>
        void RegisterRadiusSource(ProceduralShape source)
        {
            if (!isActiveAndEnabled || source == this)
                source = null;
            if (ReferenceEquals(registeredRadiusSource, source))
                return;

            if (registeredRadiusSource != null)
                registeredRadiusSource.radiusSyncDependents?.Remove(this);

            registeredRadiusSource = source;
            if (source != null)
                (source.radiusSyncDependents ??= new List<ProceduralShape>()).Add(this);
        }

        public override void SetVerticesDirty()
        {
            base.SetVerticesDirty();

            if (radiusSyncDependents != null && radiusSyncDependents.Count > 0)
                SetRadiusSyncDependentsDirty();
        }

        void SetRadiusSyncDependentsDirty()
        {
            // Each outer call starts a pass; reaching a shape twice in one pass means a sync cycle
            if (radiusSyncPassDepth == 0)
                radiusSyncPass++;
            else if (radiusSyncNotifyPass == radiusSyncPass)
                return;

            radiusSyncNotifyPass = radiusSyncPass;
            radiusSyncPassDepth++;
            try
            {
                for (int i = 0; i < radiusSyncDependents.Count; i++)
                    radiusSyncDependents[i].SetVerticesDirty();
            }
            finally { radiusSyncPassDepth--; }
        }

        /// <summary>
        /// Makes MatchAncestor dependents search again after this shape moves or is destroyed.
        /// </summary>
        void ResetRadiusSyncDependents(bool destroyed)
        {
            if (radiusSyncDependents == null)
                return;

            for (int i = radiusSyncDependents.Count - 1; i >= 0; i--)
            {
                ProceduralShape dependent = radiusSyncDependents[i];
                if (dependent.cachedRadiusSyncMode == RadiusSyncMode.MatchAncestor)
                    dependent.radiusSyncSearchPending = true;

                if (destroyed)
                {
                    dependent.registeredRadiusSource = null;
                    dependent.SetVerticesDirty();
                }
            }

            if (destroyed)
                radiusSyncDependents.Clear();
        }

#if UNITY_EDITOR
        static readonly List<ProceduralShape> editorAncestorSearches = new();
        static bool editorAncestorRefreshPending, editorHierarchyHooked;
        bool editorAncestorTracked;

        /// <summary>
        /// Edit mode only: catches sources created, renamed or moved after the search ran.
        /// Play mode keeps the player's behavior; call UpdateRect() after such changes.
        /// </summary>
        void TrackEditorAncestorSearch(bool track)
        {
            if (editorAncestorTracked == track)
                return;

            editorAncestorTracked = track;
            if (!track)
            {
                editorAncestorSearches.Remove(this);
                return;
            }

            if (!editorHierarchyHooked)
            {
                UnityEditor.EditorApplication.hierarchyChanged += ScheduleEditorAncestorRefresh;
                editorHierarchyHooked = true;
            }

            editorAncestorSearches.Add(this);
        }

        static void ScheduleEditorAncestorRefresh()
        {
            if (editorAncestorRefreshPending || editorAncestorSearches.Count == 0 || Application.isPlaying)
                return;

            editorAncestorRefreshPending = true;
            UnityEditor.EditorApplication.delayCall += RefreshEditorAncestorSearches;
        }

        static void RefreshEditorAncestorSearches()
        {
            editorAncestorRefreshPending = false;
            if (Application.isPlaying)
                return;

            for (int i = editorAncestorSearches.Count - 1; i >= 0; i--)
            {
                ProceduralShape shape = editorAncestorSearches[i];
                if (shape == null)
                {
                    editorAncestorSearches.RemoveAt(i);
                    continue;
                }

                // An inspector edit not yet applied by RefreshEditorPreview
                if (!shape.UsesRadiusSync || shape.radiusSyncMode != RadiusSyncMode.MatchAncestor)
                    continue;

                ProceduralShape previous = shape.cachedParentShape;
                bool wasPending = shape.radiusSyncSearchPending;
                shape.FindAncestorRadiusSource();
                if (wasPending || !ReferenceEquals(previous, shape.cachedParentShape))
                    shape.SetVerticesDirty();
            }
        }
#endif
        #endregion

        #region Raycasting and Fill Clipping
        const int RoundedClipFlag = 256;

        // Ellipses do not use corner radii. Reuse that payload for the arc's
        // bisector and half-sweep so the fragment shader needs no trigonometry.
        internal Vector4 GetShaderRadii(Rect rect)
        {
            if (!UsesRoundedClipEdges || shape != ShapeType.Ellipse)
                return SupportsRadius ? GetRadiiPixels(rect) : Vector4.zero;

            float halfSweep = Mathf.Clamp01(clipAmount) * Mathf.PI;
            float start = (Mathf.Clamp(clipOrigin, 0, 3) - 1) * (Mathf.PI * 0.5f);
            float middle = start + (clipClockwise ? -halfSweep : halfSweep);
            return new Vector4(Mathf.Cos(middle), Mathf.Sin(middle), Mathf.Sin(halfSweep), Mathf.Cos(halfSweep));
        }

        /// <summary>
        /// CPU counterpart of Evo_UI_GetShapeDistance's rounded clip. Ellipses retain
        /// their stretched circular stroke; linear rectangles resize with their corner radii.
        /// </summary>
        float GetRoundedClipSDF(Vector2 p, Vector2 halfSize, Vector4 radii)
        {
            float amount = Mathf.Clamp01(clipAmount);
            if (amount <= 0f)
                return 100000f;

            if (clipMethod == ClipMethod.Horizontal || clipMethod == ClipMethod.Vertical)
            {
                bool fromMin = (Mathf.Clamp(clipOrigin, 0, 1) == 0) == clipClockwise;
                float direction = fromMin ? -1f : 1f;

                if (clipMethod == ClipMethod.Horizontal)
                {
                    p.x -= direction * halfSize.x * (1f - amount);
                    halfSize.x *= amount;
                }
                else
                {
                    p.y -= direction * halfSize.y * (1f - amount);
                    halfSize.y *= amount;
                }

                radii = ClampRadii(radii, Mathf.Min(halfSize.x, halfSize.y));
                return GetRoundedRectSDF(p, halfSize, radii, UsesSquircleCorners);
            }

            if (shape != ShapeType.Ellipse)
            {
                float halfWidth = outlineWidth * 0.5f;
                float centerDistance = GetShapeSDF(p, halfSize, radii, UsesSquircleCorners) + halfWidth;
                if (amount >= 1f)
                    return Mathf.Abs(centerDistance) - halfWidth;

                float clipDistance = Mathf.Max(GetClipSDF(p, halfSize, amount, Mathf.Clamp(clipOrigin, 0, 3)), 0f);
                return new Vector2(centerDistance, clipDistance).magnitude - halfWidth;
            }

            float radius = Mathf.Min(halfSize.x, halfSize.y);
            float capRadius = Mathf.Min(outlineWidth, radius) * 0.5f;
            float centerRadius = radius - capRadius;
            p = new Vector2(p.x * radius / halfSize.x, p.y * radius / halfSize.y);
            float radialDistance = Mathf.Abs(p.magnitude - centerRadius);

            if (amount >= 1f)
                return radialDistance - capRadius;

            Vector2 q = new(Mathf.Abs(p.x * radii.y - p.y * radii.x), p.x * radii.x + p.y * radii.y);
            float distance = radii.w * q.x > radii.z * q.y ? (q - new Vector2(radii.z, radii.w) * centerRadius).magnitude : radialDistance;
            return distance - capRadius;
        }

        /// <summary>
        /// Packs fill clipping and outer-shadow flags into the shader vertex payload.
        /// </summary>
        public float GetPackedClipConfig()
        {
            // Bits 0-2 = method, 3-5 = origin, 6 = clockwise, 7 = outside-only shadow, 8 = rounded clip edges
            int method = (int)clipMethod;
            int origin = Mathf.Clamp(clipOrigin, 0, MaxOriginForMethod());
            int cw = clipClockwise ? 1 : 0;
            int outsideOnly = outerShadowOutsideOnly ? 1 : 0;
            return method + origin * 8 + cw * 64 + outsideOnly * 128 + (UsesRoundedClipEdges ? RoundedClipFlag : 0);
        }

        int MaxOriginForMethod() => clipMethod switch
        {
            ClipMethod.Horizontal => 1,
            ClipMethod.Vertical => 1,
            ClipMethod.Radial360 => 3,
            _ => 0,
        };

        public bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
        {
            if (operandOnly || raycastMode == RaycastMode.None)
                return false;
            if (raycastMode == RaycastMode.Standard)
                return true;

            // Custom shape test (alpha hit without memory/texture sampling)
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, sp, eventCamera, out Vector2 localPoint))
            {
                Rect r = GetDrawingRect();
                if (r.width < 0.001f || r.height < 0.001f)
                    return false;

                Vector2 halfSize = new(r.width * 0.5f, r.height * 0.5f);
                Vector2 center = r.center;
                Vector2 p = localPoint - center;
                if (HasShapeTransform)
                    p = GetShapeTransform(Vector2.zero, true).MultiplyPoint3x4(p);

                float dist = GetPrimitiveDistance(p, r, false);
                dist = ApplyShapeOperations(dist, p);
                return dist <= 0f && (UsesRoundedClipEdges || IsClipLocationValid(p, halfSize));
            }

            return false;
        }

        bool IsClipLocationValid(Vector2 p, Vector2 halfSize)
        {
            if (clipMethod == ClipMethod.None)
                return true;

            float amount = Mathf.Clamp01(clipAmount);
            if (amount >= 1f)
                return true;
            if (amount <= 0f)
                return false;

            int origin = Mathf.Clamp(clipOrigin, 0, MaxOriginForMethod());
            float clipDist = GetClipSDF(p, halfSize, amount, origin);
            return clipDist <= 0f;
        }

        /// <summary>
        /// Evaluates the CPU counterpart of the shader fill-clip distance.
        /// </summary>
        float GetClipSDF(Vector2 p, Vector2 halfSize, float amount, int origin)
        {
            if (clipMethod == ClipMethod.Horizontal)
            {
                bool fromMin = (origin == 0) == clipClockwise;
                float boundary = fromMin ? Mathf.Lerp(-halfSize.x, halfSize.x, amount) : Mathf.Lerp(halfSize.x, -halfSize.x, amount);
                return fromMin ? p.x - boundary : boundary - p.x;
            }

            if (clipMethod == ClipMethod.Vertical)
            {
                bool fromMin = (origin == 0) == clipClockwise;
                float boundary = fromMin ? Mathf.Lerp(-halfSize.y, halfSize.y, amount) : Mathf.Lerp(halfSize.y, -halfSize.y, amount);
                return fromMin ? p.y - boundary : boundary - p.y;
            }

            float startAngle = origin switch
            {
                0 => -Mathf.PI * 0.5f,
                1 => 0f,
                2 => Mathf.PI * 0.5f,
                _ => Mathf.PI,
            };

            float angle = Mathf.Atan2(p.y, p.x) - startAngle;
            angle = clipClockwise ? -angle : angle;
            float radial = Mathf.Repeat(angle / (Mathf.PI * 2f) + 1f, 1f);
            float signedFraction = radial <= amount ? -Mathf.Min(radial, amount - radial) : Mathf.Min(radial - amount, 1f - radial);
            float angleDistance = Mathf.Min(Mathf.Abs(signedFraction) * Mathf.PI * 2f, Mathf.PI * 0.5f);
            return Mathf.Sign(signedFraction) * Mathf.Sin(angleDistance) * p.magnitude;
        }
        #endregion

        #region Styler Integration
        IStylerInteractable interactableInterface;
        InteractionState currentInteractionState = InteractionState.Normal;
        Color stylerFillColor = Color.white;
        Color stylerPatternColor = Color.white;
        Color stylerOutlineColor = Color.white;
        Color stylerInnerShadowColor = Color.white;
        Color stylerOuterShadowColor = Color.white;
        System.Action<InteractionState> interactionStateChanged;

        public StylerPreset Preset
        {
            get => preset;
            set
            {
                if (preset == value)
                    return;

                preset = value;
                UpdateStyler();
            }
        }

        public void UpdateStyler()
        {
            Styler.SyncDefaultPreset(ref preset, useDefaultStylerPreset);

            bool shapeStyleChanged = ApplyShapeStyle();
            if (shapeStyleChanged)
            {
                // A style can change the shape type, not the hierarchy
                UpdateCachedParent(false);
                UpdateSpriteTracking();
#if UNITY_EDITOR
                backdropBlur?.Validate(this, UpdateBackdropBlur);
#else
                UpdateBackdropBlur();
#endif
                SetVerticesDirty();
            }

            EnsureStylerSettings();
            RefreshInteractionSubscription();

            IStylerInteractable stateProvider = interactableInterface ?? interactableObject as IStylerInteractable;
            bool useInteraction = stateProvider != null;
            currentInteractionState = useInteraction ? stateProvider.InteractionState : InteractionState.Normal;
            ResolveStylerColors(currentInteractionState, useInteraction, true);
            SetMaterialDirty();
        }

        bool ApplyShapeStyle()
        {
            return preset != null && !string.IsNullOrEmpty(shapeStyleID) &&
                preset.TryGetShapeStyleItem(shapeStyleID, out Styler.ShapeStyleItem shapeStyle) && shapeStyle.ApplyTo(this);
        }

        void EnsureStylerSettings()
        {
            fillStyler ??= new Styler.ColorStyle();
            patternStyler ??= new Styler.ColorStyle();
            outlineStyler ??= new Styler.ColorStyle();
            innerShadowStyler ??= new Styler.ColorStyle();
            outerShadowStyler ??= new Styler.ColorStyle();
        }

        static bool IsInteractionEnabled(ColorMode mode, Styler.ColorStyle style, Styler.GradientStyle gradientStyle = null)
        {
            return mode == ColorMode.StylerGradient ? gradientStyle != null && gradientStyle.enableInteraction
                : (mode == ColorMode.Custom || mode == ColorMode.Styler) && style != null && style.enableInteraction;
        }

        bool AnyInteractionEnabled()
        {
            return (PatternActive && IsInteractionEnabled(patternColorMode, patternStyler, patternGradientStyler))
                || IsInteractionEnabled(fillColorMode, fillStyler, fillGradientStyler)
                || IsInteractionEnabled(outlineColorMode, outlineStyler, outlineGradientStyler)
                || IsInteractionEnabled(innerShadowColorMode, innerShadowStyler, innerShadowGradientStyler)
                || IsInteractionEnabled(outerShadowColorMode, outerShadowStyler, outerShadowGradientStyler);
        }

        Color ResolveLayerColor(ColorMode mode, Color customColor, Styler.ColorStyle style, InteractionState state, bool useInteraction)
        {
            bool interactionEnabled = useInteraction && IsInteractionEnabled(mode, style);
            if (mode == ColorMode.Styler)
                return style.ResolveColor(preset, state, interactionEnabled);
            if (interactionEnabled)
                return Styler.GetColor(StyleProvider.Custom, style.GetMapping(state), null);

            return customColor;
        }

        Color GetLayerColor(ColorMode mode, Color customColor, Styler.ColorStyle style, Color resolvedColor)
        {
            return mode == ColorMode.Styler || (IsInteractionEnabled(mode, style) && interactableObject is IStylerInteractable)
                ? resolvedColor : customColor;
        }

        void RefreshInteractionSubscription()
        {
            IStylerInteractable next = Application.isPlaying && isActiveAndEnabled && AnyInteractionEnabled()
                ? interactableObject as IStylerInteractable : null;
            if (ReferenceEquals(interactableInterface, next))
                return;

            UnsubscribeInteraction();
            interactableInterface = next;
            if (interactableInterface != null)
            {
                interactionStateChanged ??= OnInteractableStateChanged;
                interactableInterface.OnStateChanged += interactionStateChanged;
            }
        }

        void UnsubscribeInteraction()
        {
            if (interactableInterface != null)
                interactableInterface.OnStateChanged -= interactionStateChanged;

            interactableInterface = null;
            ColorTransition.Cancel(this);
        }

        void OnInteractableStateChanged(InteractionState newState)
        {
            if (currentInteractionState == newState || !isActiveAndEnabled)
                return;

            currentInteractionState = newState;
            float duration = interactableInterface != null ? interactableInterface.TransitionDuration : 0f;
            using (ColorTransition.Begin(this, duration, easeOut: true))
                ResolveStylerColors(newState, true);
        }

        void ResolveStylerColors(InteractionState state, bool useInteraction, bool refreshGradients = false)
        {
            ResolveStylerGradients(state, useInteraction, refreshGradients);

            if (PatternActive)
            {
                ColorTransition.SetColor(this, 5, stylerPatternColor,
                    ResolveLayerColor(patternColorMode, patternColor, patternStyler, state, useInteraction),
                    static (target, value) =>
                    {
                        var shape = (ProceduralShape)target;
                        shape.stylerPatternColor = value;
                        shape.SetPatternDirty();
                    });
            }

            ColorTransition.SetColor(this, 1, stylerFillColor,
                ResolveLayerColor(fillColorMode, fillColor, fillStyler, state, useInteraction),
                static (target, value) =>
                {
                    var shape = (ProceduralShape)target;
                    shape.stylerFillColor = value;
                    shape.SetStylerColorsDirty(0);
                });

            ColorTransition.SetColor(this, 2, stylerOutlineColor,
                ResolveLayerColor(outlineColorMode, outlineColor, outlineStyler, state, useInteraction),
                static (target, value) => 
                { 
                    var shape = (ProceduralShape)target;
                    shape.stylerOutlineColor = value;
                    shape.SetStylerColorsDirty(1);
                });
           
            ColorTransition.SetColor(this, 3, stylerInnerShadowColor,
                ResolveLayerColor(innerShadowColorMode, innerShadowColor, innerShadowStyler, state, useInteraction),
                static (target, value) => 
                { 
                    var shape = (ProceduralShape)target; 
                    shape.stylerInnerShadowColor = value; 
                    shape.SetStylerColorsDirty(2);
                });

            ColorTransition.SetColor(this, 4, stylerOuterShadowColor,
                ResolveLayerColor(outerShadowColorMode, outerShadowColor, outerShadowStyler, state, useInteraction),
                static (target, value) => 
                { 
                    var shape = (ProceduralShape)target;
                    shape.stylerOuterShadowColor = value;
                    shape.SetStylerColorsDirty(3);
                });
        }
        #endregion

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            backdropBlur?.Validate(this, UpdateBackdropBlur);
            clipOrigin = Mathf.Clamp(clipOrigin, 0, MaxOriginForMethod());
            polygonSides = Mathf.Clamp(polygonSides, MinPolygonSides, MaxPolygonSides);
            patternPolygonSides = Mathf.Clamp(patternPolygonSides, MinPolygonSides, MaxPolygonSides);
            patternSize = Vector2.Max(patternSize, Vector2.one * 0.1f);
            patternGap = Vector2.Max(patternGap, Vector2.zero);
            patternRadius = Mathf.Max(0f, patternRadius);
            shapeSkew = ClampShapeSkew(shapeSkew);
            patternFalloffOrigin = ClampPatternOrigin(patternFalloffOrigin);
            patternFalloffStart = Mathf.Max(0f, patternFalloffStart);
            patternFalloffEnd = Mathf.Max(patternFalloffStart + 0.001f, patternFalloffEnd);

            UnityEditor.EditorApplication.delayCall -= RefreshEditorPreview;
            UnityEditor.EditorApplication.delayCall += RefreshEditorPreview;
        }

        void RefreshEditorPreview()
        {
            if (this == null || !isActiveAndEnabled)
                return;

            base.raycastTarget = !operandOnly && raycastMode != RaycastMode.None;
            UpdateOperationTracking();
            EnsureCanvasChannels();
            UpdateCachedParent();
            UpdateSpriteTracking();
            UpdateStyler();
            SetVerticesDirty();

            // SetVerticesDirty already rebuilt subscribed children; connect Match Parent children
            // that resolved before this parent had its shape
            if (!Application.isPlaying)
            {
                foreach (Transform child in transform)
                {
                    if (child.TryGetComponent(out ProceduralShape childShape) && childShape.UsesRadiusSync
                        && childShape.radiusSyncMode == RadiusSyncMode.MatchParent && childShape.registeredRadiusSource != this)
                    {
                        childShape.UpdateCachedParent();
                        childShape.SetVerticesDirty();
                    }
                }
            }
        }

        protected override void Reset()
        {
            base.Reset();

            embeddedShader = GetDefaultShader();
            fillGradient = DefaultGradient();
            outlineGradient = DefaultGradient();
            innerShadowGradient = DefaultGradient();
            outerShadowGradient = DefaultGradient();
            preset = Styler.GetDefaultPreset(false);
        }
#endif
    }
}