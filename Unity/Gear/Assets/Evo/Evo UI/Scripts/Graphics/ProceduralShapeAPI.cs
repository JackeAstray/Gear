using UnityEngine;

namespace Evo.UI
{
    public partial class ProceduralShape
    {
        public override bool raycastTarget
        {
            get => base.raycastTarget;
            set
            {
                base.raycastTarget = value && !operandOnly;

                if (!value)
                    raycastMode = RaycastMode.None;
                else if (raycastMode == RaycastMode.None)
                    raycastMode = RaycastMode.Standard;
            }
        }

        public ShapeType Shape
        {
            get => shape;
            set
            {
                if (shape == value)
                    return;

                shape = value;
                UpdateCachedParent();
                SetVerticesDirty();
            }
        }

        public int PolygonSides
        {
            get => polygonSides;
            set
            {
                value = Mathf.Clamp(value, MinPolygonSides, MaxPolygonSides);
                if (polygonSides == value)
                    return;

                polygonSides = value;
                SetVerticesDirty();
            }
        }

        public float ShapeAngle
        {
            get => shapeAngle;
            set
            {
                value = Mathf.Repeat(value, 360f);
                if (shapeAngle == value)
                    return;

                shapeAngle = value;
                SetVerticesDirty();
            }
        }

        public Vector2 ShapeSkew
        {
            get => shapeSkew;
            set
            {
                value = ClampShapeSkew(value);
                if (shapeSkew.Equals(value))
                    return;

                shapeSkew = value;
                SetVerticesDirty();
            }
        }

        public Sprite Sprite
        {
            get => sprite;
            set
            {
                if (sprite == value)
                    return;

                sprite = value;
                UpdateSpriteTracking();

                if (fillPattern)
                    UpdateStyler();

                SetAllDirty();
            }
        }

        public ScaleMode ImageScaleMode
        {
            get => scaleMode;
            set
            {
                if (scaleMode == value)
                    return;

                scaleMode = value;
                SetVerticesDirty();
            }
        }

        public RaycastMode RaycastDetectionMode
        {
            get => raycastMode;
            set
            {
                if (raycastMode == value)
                    return;

                raycastMode = value;
                base.raycastTarget = !operandOnly && raycastMode != RaycastMode.None;
            }
        }

        public ClipMethod ClippingMethod
        {
            get => clipMethod;
            set
            {
                if (clipMethod == value)
                    return;

                clipMethod = value;
                clipOrigin = Mathf.Clamp(clipOrigin, 0, MaxOriginForMethod());
                SetVerticesDirty();
            }
        }

        public int ClipOrigin
        {
            get => clipOrigin;
            set
            {
                value = Mathf.Clamp(value, 0, MaxOriginForMethod());
                if (clipOrigin == value)
                    return;

                clipOrigin = value;
                SetVerticesDirty();
            }
        }

        public float ClipAmount
        {
            get => clipAmount;
            set
            {
                value = Mathf.Clamp01(value);
                if (clipAmount == value)
                    return;

                clipAmount = value;
                SetVerticesDirty();
            }
        }

        public bool ClipClockwise
        {
            get => clipClockwise;
            set
            {
                if (clipClockwise == value)
                    return;

                clipClockwise = value;
                SetVerticesDirty();
            }
        }

        public bool RoundedClipEdges
        {
            get => roundedClipEdges;
            set
            {
                if (roundedClipEdges == value)
                    return;

                roundedClipEdges = value;
                SetVerticesDirty();
            }
        }

        public bool FillCenter
        {
            get => fillCenter;
            set
            {
                if (fillCenter == value)
                    return;

                fillCenter = value;

                if (fillPattern)
                    UpdateStyler();

                UpdateBackdropBlur();
                SetVerticesDirty();
                SetMaterialDirty();
            }
        }

        public float Softness
        {
            get => softness;
            set
            {
                value = Mathf.Clamp(value, 0f, 32f);
                if (softness == value)
                    return;

                softness = value;
                SetVerticesDirty();
            }
        }

        public bool Dither
        {
            get => dither;
            set
            {
                if (dither == value)
                    return;

                dither = value;
                SetVerticesDirty();
            }
        }

        public Color FillColor
        {
            get => fillColor;
            set
            {
                if (fillColor == value)
                    return;

                fillColor = value;
                SetStylerColorsDirty(0);
            }
        }

        /// <summary>
        /// Gradient definition. Call UpdateRect() after editing its keys or mode in place.
        /// </summary>
        public Gradient FillGradient
        {
            get => fillGradient;
            set
            {
                if (fillGradient == value)
                    return;

                fillGradient = value;
                SetMaterialDirty();
            }
        }

        public GradientType FillGradientType
        {
            get => fillGradientType;
            set
            {
                if (fillGradientType == value)
                    return;

                fillGradientType = value;
                SetStylerColorsDirty(0);
            }
        }

        public GradientSpace FillGradientSpace
        {
            get => fillGradientSpace;
            set
            {
                if (fillGradientSpace == value)
                    return;

                fillGradientSpace = value;
                SetStylerColorsDirty(0);
            }
        }

        public float FillGradientAngle
        {
            get => fillGradientAngle;
            set
            {
                value = Mathf.Repeat(value, 360f);
                if (fillGradientAngle == value)
                    return;

                fillGradientAngle = value;
                SetStylerColorsDirty(0);
            }
        }

        public float FillGradientZoom
        {
            get => fillGradientZoom;
            set
            {
                value = Mathf.Clamp(value, 0.1f, 5f);
                if (fillGradientZoom == value)
                    return;

                fillGradientZoom = value;
                SetStylerColorsDirty(0);
            }
        }

        public bool FillGradientReverse
        {
            get => fillGradientReverse;
            set
            {
                if (fillGradientReverse == value)
                    return;

                fillGradientReverse = value;
                SetStylerColorsDirty(0);
            }
        }

        public bool FillPattern
        {
            get => fillPattern;
            set
            {
                if (fillPattern == value)
                    return;

                fillPattern = value;
                UpdateSpriteTracking();
                UpdateStyler();
                SetAllDirty();
            }
        }

        public PatternSource PatternType
        {
            get => patternSource;
            set
            {
                if (patternSource == value)
                    return;

                patternSource = value;
                UpdateSpriteTracking();
                UpdateStyler();
                SetAllDirty();
            }
        }

        public Sprite PatternSprite
        {
            get => patternSprite;
            set
            {
                if (patternSprite == value)
                    return;

                patternSprite = value;
                UpdateSpriteTracking();
                UpdateStyler();
                SetAllDirty();
            }
        }

        public ShapeType PatternShape
        {
            get => patternShape;
            set
            {
                if (patternShape == value)
                    return;

                patternShape = value;
                SetPatternDirty();
            }
        }

        public int PatternPolygonSides
        {
            get => patternPolygonSides;
            set
            {
                value = Mathf.Clamp(value, MinPolygonSides, MaxPolygonSides);
                if (patternPolygonSides == value)
                    return;

                patternPolygonSides = value;
                SetPatternDirty();
            }
        }

        public float PatternRadius
        {
            get => patternRadius;
            set
            {
                value = Mathf.Max(0f, value);
                if (patternRadius == value)
                    return;

                patternRadius = value;
                SetPatternDirty();
            }
        }

        public Vector2 PatternSize
        {
            get => patternSize;
            set
            {
                value = Vector2.Max(value, Vector2.one * 0.1f);
                if (patternSize == value)
                    return;

                patternSize = value;
                SetPatternDirty();
            }
        }

        public Vector2 PatternGap
        {
            get => patternGap;
            set
            {
                value = Vector2.Max(value, Vector2.zero);
                if (patternGap == value)
                    return;

                patternGap = value;
                SetPatternDirty();
            }
        }

        public Vector2 PatternOffset
        {
            get => patternOffset;
            set
            {
                if (patternOffset == value)
                    return;

                patternOffset = value;
                SetPatternDirty();
            }
        }

        public float PatternAngle
        {
            get => patternAngle;
            set
            {
                value = Mathf.Repeat(value, 360f);
                if (patternAngle == value)
                    return;

                patternAngle = value;
                SetPatternDirty();
            }
        }

        public bool PatternFalloff
        {
            get => patternFalloff;
            set
            {
                if (patternFalloff == value)
                    return;

                patternFalloff = value;
                SetPatternDirty();
            }
        }

        public Vector2 PatternFalloffOrigin
        {
            get => patternFalloffOrigin;
            set
            {
                value = ClampPatternOrigin(value);
                if (patternFalloffOrigin == value)
                    return;

                patternFalloffOrigin = value;
                SetPatternDirty();
            }
        }

        public float PatternFalloffStart
        {
            get => patternFalloffStart;
            set
            {
                value = Mathf.Max(0f, value);
                if (patternFalloffStart == value)
                    return;

                patternFalloffStart = value;
                SetPatternDirty();
            }
        }

        public float PatternFalloffEnd
        {
            get => patternFalloffEnd;
            set
            {
                value = Mathf.Max(0.001f, value);
                if (patternFalloffEnd == value)
                    return;

                patternFalloffEnd = value;
                SetPatternDirty();
            }
        }

        public Color PatternColor
        {
            get => patternColor;
            set
            {
                if (patternColor == value)
                    return;

                patternColor = value;
                SetPatternDirty();
            }
        }

        public Gradient PatternGradient
        {
            get => patternGradient;
            set
            {
                if (patternGradient == value)
                    return;

                patternGradient = value;
                SetPatternDirty(true);
            }
        }

        public GradientType PatternGradientType
        {
            get => patternGradientType;
            set
            {
                if (patternGradientType == value)
                    return;

                patternGradientType = value;
                SetPatternDirty();
            }
        }

        public GradientSpace PatternGradientSpace
        {
            get => patternGradientSpace;
            set
            {
                if (patternGradientSpace == value)
                    return;

                patternGradientSpace = value;
                SetPatternDirty();
            }
        }

        public float PatternGradientAngle
        {
            get => patternGradientAngle;
            set
            {
                value = Mathf.Repeat(value, 360f);
                if (patternGradientAngle == value)
                    return;

                patternGradientAngle = value;
                SetPatternDirty();
            }
        }

        public float PatternGradientZoom
        {
            get => patternGradientZoom;
            set
            {
                value = Mathf.Clamp(value, 0.1f, 5f);
                if (patternGradientZoom == value)
                    return;

                patternGradientZoom = value;
                SetPatternDirty();
            }
        }

        public bool PatternGradientReverse
        {
            get => patternGradientReverse;
            set
            {
                if (patternGradientReverse == value)
                    return;

                patternGradientReverse = value;
                SetPatternDirty();
            }
        }

        public float OutlineWidth
        {
            get => outlineWidth;
            set
            {
                value = Mathf.Max(0f, value);
                if (outlineWidth == value)
                    return;

                bool wasActive = outlineWidth > 0.001f;
                outlineWidth = value;
                SetLayerValueDirty(wasActive, outlineWidth > 0.001f);
            }
        }

        public bool OutlineDashed
        {
            get => outlineDashed;
            set
            {
                if (outlineDashed == value)
                    return;

                outlineDashed = value;
                SetOutlineDashDirty(true);
            }
        }

        public float OutlineDashLength
        {
            get => outlineDashLength;
            set
            {
                value = Mathf.Max(0.1f, value);
                if (outlineDashLength == value)
                    return;

                outlineDashLength = value;
                SetOutlineDashDirty();
            }
        }

        public float OutlineDashGap
        {
            get => outlineDashGap;
            set
            {
                value = Mathf.Max(0f, value);
                if (outlineDashGap == value)
                    return;

                outlineDashGap = value;
                SetOutlineDashDirty(true);
            }
        }

        public float OutlineDashOffset
        {
            get => outlineDashOffset;
            set
            {
                if (outlineDashOffset == value)
                    return;

                outlineDashOffset = value;
                SetOutlineDashDirty();
            }
        }

        public bool OutlineDashRounded
        {
            get => outlineDashRounded;
            set
            {
                if (outlineDashRounded == value)
                    return;

                outlineDashRounded = value;
                SetOutlineDashDirty();
            }
        }

        public Color OutlineColor
        {
            get => outlineColor;
            set
            {
                if (outlineColor == value)
                    return;

                outlineColor = value;
                SetStylerColorsDirty(1);
            }
        }

        /// <summary>
        /// Gradient definition. Call UpdateRect() after editing its keys or mode in place.
        /// </summary>
        public Gradient OutlineGradient
        {
            get => outlineGradient;
            set
            {
                if (outlineGradient == value)
                    return;

                outlineGradient = value;
                SetMaterialDirty();
            }
        }

        public GradientType OutlineGradientType
        {
            get => outlineGradientType;
            set
            {
                if (outlineGradientType == value)
                    return;

                outlineGradientType = value;
                SetStylerColorsDirty(1);
            }
        }

        public GradientSpace OutlineGradientSpace
        {
            get => outlineGradientSpace;
            set
            {
                if (outlineGradientSpace == value)
                    return;

                outlineGradientSpace = value;
                SetStylerColorsDirty(1);
            }
        }

        public float OutlineGradientAngle
        {
            get => outlineGradientAngle;
            set
            {
                value = Mathf.Repeat(value, 360f);
                if (outlineGradientAngle == value)
                    return;

                outlineGradientAngle = value;
                SetStylerColorsDirty(1);
            }
        }

        public float OutlineGradientZoom
        {
            get => outlineGradientZoom;
            set
            {
                value = Mathf.Clamp(value, 0.1f, 4f);
                if (outlineGradientZoom == value)
                    return;

                outlineGradientZoom = value;
                SetStylerColorsDirty(1);
            }
        }

        public bool OutlineGradientReverse
        {
            get => outlineGradientReverse;
            set
            {
                if (outlineGradientReverse == value)
                    return;

                outlineGradientReverse = value;
                SetStylerColorsDirty(1);
            }
        }

        public RadiusMode CornerRadiusMode
        {
            get => radiusMode;
            set
            {
                if (radiusMode == value)
                    return;

                radiusMode = value;
                SetVerticesDirty();
            }
        }

        public bool IndependentCorners
        {
            get => independentCorners;
            set
            {
                if (independentCorners == value)
                    return;

                independentCorners = value;
                SetVerticesDirty();
            }
        }

        public bool SquircleCorners
        {
            get => squircleCorners;
            set
            {
                if (squircleCorners == value)
                    return;

                squircleCorners = value;
                SetVerticesDirty();
            }
        }

        /// <summary>
        /// Stored corner radii in top-left, top-right, bottom-right, bottom-left order.
        /// </summary>
        public Vector4 CornerRadius
        {
            get => cornerRadius;
            set
            {
                value = Vector4.Max(Vector4.zero, value);
                if (cornerRadius == value)
                    return;

                cornerRadius = value;
                SetVerticesDirty();
            }
        }

        public RadiusSyncMode RadiusSynchronizationMode
        {
            get => radiusSyncMode;
            set
            {
                if (radiusSyncMode == value)
                    return;

                radiusSyncMode = value;
                UpdateCachedParent();
                SetVerticesDirty();
            }
        }

        public ProceduralShape RadiusSyncTarget
        {
            get => radiusSyncTarget;
            set
            {
                if (radiusSyncTarget == value)
                    return;

                radiusSyncTarget = value;
                UpdateCachedParent();
                SetVerticesDirty();
            }
        }

        /// <summary>
        /// Name or relative path that MatchAncestor looks for under each parent, nearest first.
        /// Empty uses the nearest parent with a ProceduralShape.
        /// </summary>
        public string RadiusSyncTargetName
        {
            get => radiusSyncTargetName;
            set
            {
                value ??= "";
                if (radiusSyncTargetName == value)
                    return;

                radiusSyncTargetName = value;
                UpdateCachedParent();
                SetVerticesDirty();
            }
        }

        public Vector2 InnerShadowOffset
        {
            get => innerShadowOffset;
            set
            {
                if (innerShadowOffset == value)
                    return;

                bool wasActive = InnerShadowActive;
                innerShadowOffset = value;
                SetLayerValueDirty(wasActive, InnerShadowActive);
            }
        }

        public float InnerShadowSize
        {
            get => innerShadowSize;
            set
            {
                value = Mathf.Max(0f, value);
                if (innerShadowSize == value)
                    return;

                bool wasActive = InnerShadowActive;
                innerShadowSize = value;
                SetLayerValueDirty(wasActive, InnerShadowActive);
            }
        }

        public float InnerShadowSoftness
        {
            get => innerShadowSoftness;
            set
            {
                value = Mathf.Max(0f, value);
                if (innerShadowSoftness == value)
                    return;

                bool wasActive = InnerShadowActive;
                innerShadowSoftness = value;
                SetLayerValueDirty(wasActive, InnerShadowActive);
            }
        }

        public Color InnerShadowColor
        {
            get => innerShadowColor;
            set
            {
                if (innerShadowColor == value)
                    return;

                innerShadowColor = value;
                SetStylerColorsDirty(2);
            }
        }

        /// <summary>
        /// Gradient definition. Call UpdateRect() after editing its keys or mode in place.
        /// </summary>
        public Gradient InnerShadowGradient
        {
            get => innerShadowGradient;
            set
            {
                if (innerShadowGradient == value)
                    return;

                innerShadowGradient = value;
                SetMaterialDirty();
            }
        }

        public GradientType InnerShadowGradientType
        {
            get => innerShadowGradientType;
            set
            {
                if (innerShadowGradientType == value)
                    return;

                innerShadowGradientType = value;
                SetStylerColorsDirty(2);
            }
        }

        public GradientSpace InnerShadowGradientSpace
        {
            get => innerShadowGradientSpace;
            set
            {
                if (innerShadowGradientSpace == value)
                    return;

                innerShadowGradientSpace = value;
                SetStylerColorsDirty(2);
            }
        }

        public float InnerShadowGradientAngle
        {
            get => innerShadowGradientAngle;
            set
            {
                value = Mathf.Repeat(value, 360f);
                if (innerShadowGradientAngle == value)
                    return;

                innerShadowGradientAngle = value;
                SetStylerColorsDirty(2);
            }
        }

        public float InnerShadowGradientZoom
        {
            get => innerShadowGradientZoom;
            set
            {
                value = Mathf.Clamp(value, 0.1f, 4f);
                if (innerShadowGradientZoom == value)
                    return;

                innerShadowGradientZoom = value;
                SetStylerColorsDirty(2);
            }
        }

        public bool InnerShadowGradientReverse
        {
            get => innerShadowGradientReverse;
            set
            {
                if (innerShadowGradientReverse == value)
                    return;

                innerShadowGradientReverse = value;
                SetStylerColorsDirty(2);
            }
        }

        public Vector2 OuterShadowOffset
        {
            get => outerShadowOffset;
            set
            {
                if (outerShadowOffset == value)
                    return;

                bool wasActive = OuterShadowActive;
                outerShadowOffset = value;
                SetLayerValueDirty(wasActive, OuterShadowActive);
            }
        }

        public float OuterShadowSize
        {
            get => outerShadowSize;
            set
            {
                value = Mathf.Max(0f, value);
                if (outerShadowSize == value)
                    return;

                bool wasActive = OuterShadowActive;
                outerShadowSize = value;
                SetLayerValueDirty(wasActive, OuterShadowActive);
            }
        }

        public float OuterShadowSoftness
        {
            get => outerShadowSoftness;
            set
            {
                value = Mathf.Max(0f, value);
                if (outerShadowSoftness == value)
                    return;

                bool wasActive = OuterShadowActive;
                outerShadowSoftness = value;
                SetLayerValueDirty(wasActive, OuterShadowActive);
            }
        }

        public bool OuterShadowOutsideOnly
        {
            get => outerShadowOutsideOnly;
            set
            {
                if (outerShadowOutsideOnly == value)
                    return;

                outerShadowOutsideOnly = value;
                SetVerticesDirty();
            }
        }

        public Color OuterShadowColor
        {
            get => outerShadowColor;
            set
            {
                if (outerShadowColor == value)
                    return;

                outerShadowColor = value;
                SetStylerColorsDirty(3);
            }
        }

        /// <summary>
        /// Gradient definition. Call UpdateRect() after editing its keys or mode in place.
        /// </summary>
        public Gradient OuterShadowGradient
        {
            get => outerShadowGradient;
            set
            {
                if (outerShadowGradient == value)
                    return;

                outerShadowGradient = value;
                SetMaterialDirty();
            }
        }

        public GradientType OuterShadowGradientType
        {
            get => outerShadowGradientType;
            set
            {
                if (outerShadowGradientType == value)
                    return;

                outerShadowGradientType = value;
                SetStylerColorsDirty(3);
            }
        }

        public GradientSpace OuterShadowGradientSpace
        {
            get => outerShadowGradientSpace;
            set
            {
                if (outerShadowGradientSpace == value)
                    return;

                outerShadowGradientSpace = value;
                SetStylerColorsDirty(3);
            }
        }

        public float OuterShadowGradientAngle
        {
            get => outerShadowGradientAngle;
            set
            {
                value = Mathf.Repeat(value, 360f);
                if (outerShadowGradientAngle == value)
                    return;

                outerShadowGradientAngle = value;
                SetStylerColorsDirty(3);
            }
        }

        public float OuterShadowGradientZoom
        {
            get => outerShadowGradientZoom;
            set
            {
                value = Mathf.Clamp(value, 0.1f, 4f);
                if (outerShadowGradientZoom == value)
                    return;

                outerShadowGradientZoom = value;
                SetStylerColorsDirty(3);
            }
        }

        public bool OuterShadowGradientReverse
        {
            get => outerShadowGradientReverse;
            set
            {
                if (outerShadowGradientReverse == value)
                    return;

                outerShadowGradientReverse = value;
                SetStylerColorsDirty(3);
            }
        }

        /// <summary>
        /// Reads the first corner radius; assigning writes the same non-negative radius to all four corners.
        /// Does not change IndependentCorners or the radius unit.
        /// </summary>
        public float UniformCornerRadius
        {
            get => cornerRadius.x;
            set
            {
                float radius = Mathf.Max(0f, value);
                CornerRadius = new Vector4(radius, radius, radius, radius);
            }
        }

        /// <summary>
        /// Re-resolves cached state and schedules both geometry and material updates.
        /// Use after editing a Gradient, Styler.ColorStyle, or BackdropBlur.Settings object in place,
        /// or after creating, renaming or moving the MatchAncestor source in another branch at runtime.
        /// </summary>
        public void UpdateRect()
        {
            base.raycastTarget = !operandOnly && raycastMode != RaycastMode.None;
            UpdateCachedParent();
            UpdateSpriteTracking();
            UpdateStyler();
            UpdateBackdropBlur();
            SetVerticesDirty();
        }

        #region Obsolete API
        [System.Obsolete("Use Sprite instead.")]
        public void SetSprite(Sprite newSprite) => Sprite = newSprite;

        [System.Obsolete("Use CornerRadius instead.")]
        public void SetCornerRadius(Vector4 radius) => CornerRadius = radius;

        [System.Obsolete("Use CornerRadius instead.")]
        public void SetCornerRadius(float radius) => CornerRadius = Vector4.one * Mathf.Max(0f, radius);

        [System.Obsolete("Use useDefaultStylerPreset instead.")]
        public StylerObject.PresetSource presetSource
        {
            get => useDefaultStylerPreset ? StylerObject.PresetSource.Default : StylerObject.PresetSource.UserDefined;
            set => useDefaultStylerPreset = value == StylerObject.PresetSource.Default;
        }
        #endregion
    }
}