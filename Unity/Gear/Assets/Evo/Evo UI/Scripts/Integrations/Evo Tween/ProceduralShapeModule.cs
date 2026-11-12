using System;
using UnityEngine;
using Evo.UI;

namespace Evo.Tween.Modules.EvoUI
{
    /// <summary>
    /// Animates the number of polygon sides.
    /// </summary>
    [Serializable, ModuleMenu("Evo UI/Procedural Shape/Shape/Polygon Sides")]
    public sealed class ProceduralShapePolygonSidesModule : ConstrainedIntModule<ProceduralShape>
    {
        protected override long PropertyKey => 723L;
        protected override int MinimumValue => ProceduralShape.MinPolygonSides;
        protected override int MaximumValue => ProceduralShape.MaxPolygonSides;
        protected override int Read() => Target.PolygonSides;
        protected override void Write(int value) => Target.PolygonSides = value;
    }

    /// <summary>
    /// Animates the shape angle in degrees, including multiple revolutions.
    /// </summary>
    [Serializable, ModuleMenu("Evo UI/Procedural Shape/Shape/Angle")]
    public sealed class ProceduralShapeAngleModule : FloatModule<ProceduralShape>
    {
        protected override long PropertyKey => 724L;
        protected override float Read() => Target.ShapeAngle;
        protected override void Write(float value) => Target.ShapeAngle = value;
    }

    /// <summary>
    /// Animates the horizontal and vertical shape skew in degrees.
    /// </summary>
    [Serializable, ModuleMenu("Evo UI/Procedural Shape/Shape/Skew")]
    public sealed class ProceduralShapeSkewModule : Vector2Module<ProceduralShape>
    {
        protected override long PropertyKey => 725L;
        protected override Vector2 Read() => Target.ShapeSkew;
        protected override void Write(Vector2 value) => Target.ShapeSkew = value;
    }

    /// <summary>
    /// Animates how much of a ProceduralShape remains visible through its configured clip method.
    /// </summary>
    [Serializable, ModuleMenu("Evo UI/Procedural Shape/Clipping/Amount")]
    public sealed class ProceduralShapeClipAmountModule : NormalizedFloatModule<ProceduralShape>
    {
        protected override long PropertyKey => 701L;
        protected override float Read() => Target.ClipAmount;
        protected override void Write(float value) => Target.ClipAmount = value;
    }

    /// <summary>
    /// Animates the extra edge softness around a ProceduralShape.
    /// </summary>
    [Serializable, ModuleMenu("Evo UI/Procedural Shape/Fill/Softness")]
    public sealed class ProceduralShapeSoftnessModule : ConstrainedFloatModule<ProceduralShape>
    {
        protected override long PropertyKey => 702L;
        protected override float MinimumValue => 0f;
        protected override float MaximumValue => 32f;
        protected override float Read() => Target.Softness;
        protected override void Write(float value) => Target.Softness = value;
    }

    /// <summary>
    /// Animates the custom fill color, including its alpha channel.
    /// </summary>
    [Serializable, ModuleMenu("Evo UI/Procedural Shape/Fill/Color")]
    public sealed class ProceduralShapeFillColorModule : ColorModule<ProceduralShape>
    {
        protected override long PropertyKey => 703L;
        protected override Color Read() => Target.FillColor;
        protected override void Write(Color value) => Target.FillColor = value;
    }

    /// <summary>
    /// Animates only the alpha channel of the custom fill color.
    /// </summary>
    [Serializable, ModuleMenu("Evo UI/Procedural Shape/Fill/Alpha")]
    public sealed class ProceduralShapeFillAlphaModule : ColorAlphaModule<ProceduralShape>
    {
        protected override long PropertyKey => 703L;
        protected override Color ReadColor() => Target.FillColor;
        protected override void WriteColor(Color color) => Target.FillColor = color;
    }

    /// <summary>
    /// Animates the fill-gradient angle in degrees.
    /// </summary>
    [Serializable, ModuleMenu("Evo UI/Procedural Shape/Fill/Gradient Angle")]
    public sealed class ProceduralShapeFillGradientAngleModule : ConstrainedFloatModule<ProceduralShape>
    {
        protected override long PropertyKey => 704L;
        protected override float MinimumValue => 0f;
        protected override float MaximumValue => 360f;
        protected override float Read() => Target.FillGradientAngle;
        protected override void Write(float value) => Target.FillGradientAngle = value;
    }

    /// <summary>
    /// Animates the fill-gradient zoom.
    /// </summary>
    [Serializable, ModuleMenu("Evo UI/Procedural Shape/Fill/Gradient Zoom")]
    public sealed class ProceduralShapeFillGradientZoomModule : ConstrainedFloatModule<ProceduralShape>
    {
        protected override long PropertyKey => 705L;
        protected override float MinimumValue => 0.1f;
        protected override float MaximumValue => 5f;
        protected override float Read() => Target.FillGradientZoom;
        protected override void Write(float value) => Target.FillGradientZoom = value;
    }

    /// <summary>
    /// Animates the number of sides in each polygon pattern tile.
    /// </summary>
    [Serializable, ModuleMenu("Evo UI/Procedural Shape/Pattern/Polygon Sides")]
    public sealed class ProceduralShapePatternPolygonSidesModule : ConstrainedIntModule<ProceduralShape>
    {
        protected override long PropertyKey => 726L;
        protected override int MinimumValue => ProceduralShape.MinPolygonSides;
        protected override int MaximumValue => ProceduralShape.MaxPolygonSides;
        protected override int Read() => Target.PatternPolygonSides;
        protected override void Write(int value) => Target.PatternPolygonSides = value;
    }

    /// <summary>
    /// Animates the pattern tile corner radius in local pixels.
    /// </summary>
    [Serializable, ModuleMenu("Evo UI/Procedural Shape/Pattern/Radius")]
    public sealed class ProceduralShapePatternRadiusModule : NonNegativeFloatModule<ProceduralShape>
    {
        protected override long PropertyKey => 727L;
        protected override float Read() => Target.PatternRadius;
        protected override void Write(float value) => Target.PatternRadius = value;
    }

    /// <summary>
    /// Animates the pattern tile bounds in local pixels.
    /// </summary>
    [Serializable, ModuleMenu("Evo UI/Procedural Shape/Pattern/Size")]
    public sealed class ProceduralShapePatternSizeModule : Vector2Module<ProceduralShape>
    {
        protected override long PropertyKey => 728L;
        protected override Vector2 Read() => Target.PatternSize;
        protected override void Write(Vector2 value) => Target.PatternSize = value;
    }

    /// <summary>
    /// Animates the horizontal and vertical spacing between pattern tiles.
    /// </summary>
    [Serializable, ModuleMenu("Evo UI/Procedural Shape/Pattern/Gap")]
    public sealed class ProceduralShapePatternGapModule : Vector2Module<ProceduralShape>
    {
        protected override long PropertyKey => 729L;
        protected override Vector2 Read() => Target.PatternGap;
        protected override void Write(Vector2 value) => Target.PatternGap = value;
    }

    /// <summary>
    /// Animates the pattern translation in local pixels before rotation.
    /// </summary>
    [Serializable, ModuleMenu("Evo UI/Procedural Shape/Pattern/Offset")]
    public sealed class ProceduralShapePatternOffsetModule : Vector2Module<ProceduralShape>
    {
        protected override long PropertyKey => 730L;
        protected override Vector2 Read() => Target.PatternOffset;
        protected override void Write(Vector2 value) => Target.PatternOffset = value;
    }

    /// <summary>
    /// Animates the pattern angle in degrees, including multiple revolutions.
    /// </summary>
    [Serializable, ModuleMenu("Evo UI/Procedural Shape/Pattern/Angle")]
    public sealed class ProceduralShapePatternAngleModule : FloatModule<ProceduralShape>
    {
        protected override long PropertyKey => 731L;
        protected override float Read() => Target.PatternAngle;
        protected override void Write(float value) => Target.PatternAngle = value;
    }

    /// <summary>
    /// Animates the normalized anchor of the pattern falloff.
    /// </summary>
    [Serializable, ModuleMenu("Evo UI/Procedural Shape/Pattern/Falloff Origin")]
    public sealed class ProceduralShapePatternFalloffOriginModule : Vector2Module<ProceduralShape>
    {
        protected override long PropertyKey => 732L;
        protected override Vector2 Read() => Target.PatternFalloffOrigin;
        protected override void Write(Vector2 value) => Target.PatternFalloffOrigin = value;
    }

    /// <summary>
    /// Animates the distance in local pixels where the pattern fade begins.
    /// </summary>
    [Serializable, ModuleMenu("Evo UI/Procedural Shape/Pattern/Falloff Start")]
    public sealed class ProceduralShapePatternFalloffStartModule : NonNegativeFloatModule<ProceduralShape>
    {
        protected override long PropertyKey => 733L;
        protected override float Read() => Target.PatternFalloffStart;
        protected override void Write(float value) => Target.PatternFalloffStart = value;
    }

    /// <summary>
    /// Animates the distance in local pixels where the pattern becomes invisible.
    /// </summary>
    [Serializable, ModuleMenu("Evo UI/Procedural Shape/Pattern/Falloff End")]
    public sealed class ProceduralShapePatternFalloffEndModule : ConstrainedFloatModule<ProceduralShape>
    {
        protected override long PropertyKey => 734L;
        protected override float MinimumValue => 0.001f;
        protected override float Read() => Target.PatternFalloffEnd;
        protected override void Write(float value) => Target.PatternFalloffEnd = value;
    }

    /// <summary>
    /// Animates the custom pattern color, including its alpha channel.
    /// </summary>
    [Serializable, ModuleMenu("Evo UI/Procedural Shape/Pattern/Color")]
    public sealed class ProceduralShapePatternColorModule : ColorModule<ProceduralShape>
    {
        protected override long PropertyKey => 735L;
        protected override Color Read() => Target.PatternColor;
        protected override void Write(Color value) => Target.PatternColor = value;
    }

    /// <summary>
    /// Animates only the alpha channel of the custom pattern color.
    /// </summary>
    [Serializable, ModuleMenu("Evo UI/Procedural Shape/Pattern/Alpha")]
    public sealed class ProceduralShapePatternAlphaModule : ColorAlphaModule<ProceduralShape>
    {
        protected override long PropertyKey => 735L;
        protected override Color ReadColor() => Target.PatternColor;
        protected override void WriteColor(Color color) => Target.PatternColor = color;
    }

    /// <summary>
    /// Animates the pattern-gradient angle in degrees.
    /// </summary>
    [Serializable, ModuleMenu("Evo UI/Procedural Shape/Pattern/Gradient Angle")]
    public sealed class ProceduralShapePatternGradientAngleModule : ConstrainedFloatModule<ProceduralShape>
    {
        protected override long PropertyKey => 736L;
        protected override float MinimumValue => 0f;
        protected override float MaximumValue => 360f;
        protected override float Read() => Target.PatternGradientAngle;
        protected override void Write(float value) => Target.PatternGradientAngle = value;
    }

    /// <summary>
    /// Animates the pattern-gradient zoom.
    /// </summary>
    [Serializable, ModuleMenu("Evo UI/Procedural Shape/Pattern/Gradient Zoom")]
    public sealed class ProceduralShapePatternGradientZoomModule : ConstrainedFloatModule<ProceduralShape>
    {
        protected override long PropertyKey => 737L;
        protected override float MinimumValue => 0.1f;
        protected override float MaximumValue => 5f;
        protected override float Read() => Target.PatternGradientZoom;
        protected override void Write(float value) => Target.PatternGradientZoom = value;
    }

    /// <summary>
    /// Animates the inward outline width.
    /// </summary>
    [Serializable, ModuleMenu("Evo UI/Procedural Shape/Outline/Width")]
    public sealed class ProceduralShapeOutlineWidthModule : NonNegativeFloatModule<ProceduralShape>
    {
        protected override long PropertyKey => 706L;
        protected override float Read() => Target.OutlineWidth;
        protected override void Write(float value) => Target.OutlineWidth = value;
    }

    /// <summary>
    /// Animates the outline dash length in local pixels.
    /// </summary>
    [Serializable, ModuleMenu("Evo UI/Procedural Shape/Outline/Dash Length")]
    public sealed class ProceduralShapeOutlineDashLengthModule : ConstrainedFloatModule<ProceduralShape>
    {
        protected override long PropertyKey => 738L;
        protected override float MinimumValue => 0.1f;
        protected override float Read() => Target.OutlineDashLength;
        protected override void Write(float value) => Target.OutlineDashLength = value;
    }

    /// <summary>
    /// Animates the space between outline dashes in local pixels.
    /// </summary>
    [Serializable, ModuleMenu("Evo UI/Procedural Shape/Outline/Dash Gap")]
    public sealed class ProceduralShapeOutlineDashGapModule : NonNegativeFloatModule<ProceduralShape>
    {
        protected override long PropertyKey => 739L;
        protected override float Read() => Target.OutlineDashGap;
        protected override void Write(float value) => Target.OutlineDashGap = value;
    }

    /// <summary>
    /// Animates the outline dash offset in repeats.
    /// </summary>
    [Serializable, ModuleMenu("Evo UI/Procedural Shape/Outline/Dash Offset")]
    public sealed class ProceduralShapeOutlineDashOffsetModule : FloatModule<ProceduralShape>
    {
        protected override long PropertyKey => 740L;
        protected override float Read() => Target.OutlineDashOffset;
        protected override void Write(float value) => Target.OutlineDashOffset = value;
    }

    /// <summary>
    /// Animates the custom outline color, including its alpha channel.
    /// </summary>
    [Serializable, ModuleMenu("Evo UI/Procedural Shape/Outline/Color")]
    public sealed class ProceduralShapeOutlineColorModule : ColorModule<ProceduralShape>
    {
        protected override long PropertyKey => 707L;
        protected override Color Read() => Target.OutlineColor;
        protected override void Write(Color value) => Target.OutlineColor = value;
    }

    /// <summary>
    /// Animates only the alpha channel of the custom outline color.
    /// </summary>
    [Serializable, ModuleMenu("Evo UI/Procedural Shape/Outline/Alpha")]
    public sealed class ProceduralShapeOutlineAlphaModule : ColorAlphaModule<ProceduralShape>
    {
        protected override long PropertyKey => 707L;
        protected override Color ReadColor() => Target.OutlineColor;
        protected override void WriteColor(Color color) => Target.OutlineColor = color;
    }

    /// <summary>
    /// Animates the outline-gradient angle in degrees.
    /// </summary>
    [Serializable, ModuleMenu("Evo UI/Procedural Shape/Outline/Gradient Angle")]
    public sealed class ProceduralShapeOutlineGradientAngleModule : ConstrainedFloatModule<ProceduralShape>
    {
        protected override long PropertyKey => 708L;
        protected override float MinimumValue => 0f;
        protected override float MaximumValue => 360f;
        protected override float Read() => Target.OutlineGradientAngle;
        protected override void Write(float value) => Target.OutlineGradientAngle = value;
    }

    /// <summary>
    /// Animates the outline-gradient zoom.
    /// </summary>
    [Serializable, ModuleMenu("Evo UI/Procedural Shape/Outline/Gradient Zoom")]
    public sealed class ProceduralShapeOutlineGradientZoomModule : ConstrainedFloatModule<ProceduralShape>
    {
        protected override long PropertyKey => 709L;
        protected override float MinimumValue => 0.1f;
        protected override float MaximumValue => 4f;
        protected override float Read() => Target.OutlineGradientZoom;
        protected override void Write(float value) => Target.OutlineGradientZoom = value;
    }

    /// <summary>
    /// Animates all four stored corner radii in top-left, top-right, bottom-right, bottom-left order.
    /// </summary>
    [Serializable, ModuleMenu("Evo UI/Procedural Shape/Radius/Corner Radii")]
    public sealed class ProceduralShapeCornerRadiusModule : ValueModule<ProceduralShape, Vector4>
    {
        [ModuleField(ModuleFieldGroup.Values, ModuleFieldRole.FromCurrent)]
        [Tooltip("Capture the start radii when this clip begins.")]
        public bool fromCurrent = true;

        [ModuleField(ModuleFieldGroup.Values, ModuleFieldRole.From)]
        public Vector4 from = Vector4.zero;

        [ModuleField(ModuleFieldGroup.Values, ModuleFieldRole.To, Label = "Target")]
        public Vector4 to = Vector4.zero;

        protected override long PropertyKey => 710L;
        protected override bool UseCurrentValue => fromCurrent;
        protected override Vector4 FromValue => from;
        protected override Vector4 ToValue => to;
        protected override Vector4 Read() => Target.CornerRadius;
        protected override void Write(Vector4 value) => Target.CornerRadius = value;
        protected override Vector4 Interpolate(Vector4 start, Vector4 end, float time) => Vector4.LerpUnclamped(start, end, time);

        public override void SetValues(Vector4 start, Vector4 end)
        {
            fromCurrent = false;
            from = start;
            to = end;
        }

        protected override bool TryCopyTo(TweenModule destination)
        {
            ProceduralShapeCornerRadiusModule copy = (ProceduralShapeCornerRadiusModule)destination;
            copy.fromCurrent = fromCurrent;
            copy.from = from;
            copy.to = to;
            return true;
        }
    }

    /// <summary>
    /// Animates a uniform value across all four stored corner radii.
    /// </summary>
    [Serializable, ModuleMenu("Evo UI/Procedural Shape/Radius/Uniform Corner Radius")]
    public sealed class ProceduralShapeUniformCornerRadiusModule : NonNegativeFloatModule<ProceduralShape>
    {
        [NonSerialized] Vector4 originalRadii;

        protected override long PropertyKey => 710L;
        protected override float Read() => Target.CornerRadius.x;
        protected override void Write(float value) => Target.UniformCornerRadius = value;

        protected override float CaptureOriginalValue()
        {
            originalRadii = Target.CornerRadius;
            return originalRadii.x;
        }

        protected override void RestoreOriginalValue(float value) => Target.CornerRadius = originalRadii;
    }

    /// <summary>
    /// Animates the inner-shadow offset.
    /// </summary>
    [Serializable, ModuleMenu("Evo UI/Procedural Shape/Inner Shadow/Offset")]
    public sealed class ProceduralShapeInnerShadowOffsetModule : Vector2Module<ProceduralShape>
    {
        protected override long PropertyKey => 711L;
        protected override Vector2 Read() => Target.InnerShadowOffset;
        protected override void Write(Vector2 value) => Target.InnerShadowOffset = value;
    }

    /// <summary>
    /// Animates the inner-shadow expansion size.
    /// </summary>
    [Serializable, ModuleMenu("Evo UI/Procedural Shape/Inner Shadow/Size")]
    public sealed class ProceduralShapeInnerShadowSizeModule : NonNegativeFloatModule<ProceduralShape>
    {
        protected override long PropertyKey => 712L;
        protected override float Read() => Target.InnerShadowSize;
        protected override void Write(float value) => Target.InnerShadowSize = value;
    }

    /// <summary>
    /// Animates the inner-shadow blur radius.
    /// </summary>
    [Serializable, ModuleMenu("Evo UI/Procedural Shape/Inner Shadow/Softness")]
    public sealed class ProceduralShapeInnerShadowSoftnessModule : NonNegativeFloatModule<ProceduralShape>
    {
        protected override long PropertyKey => 713L;
        protected override float Read() => Target.InnerShadowSoftness;
        protected override void Write(float value) => Target.InnerShadowSoftness = value;
    }

    /// <summary>
    /// Animates the custom inner-shadow color, including its alpha channel.
    /// </summary>
    [Serializable, ModuleMenu("Evo UI/Procedural Shape/Inner Shadow/Color")]
    public sealed class ProceduralShapeInnerShadowColorModule : ColorModule<ProceduralShape>
    {
        protected override long PropertyKey => 714L;
        protected override Color Read() => Target.InnerShadowColor;
        protected override void Write(Color value) => Target.InnerShadowColor = value;
    }

    /// <summary>
    /// Animates only the alpha channel of the custom inner-shadow color.
    /// </summary>
    [Serializable, ModuleMenu("Evo UI/Procedural Shape/Inner Shadow/Alpha")]
    public sealed class ProceduralShapeInnerShadowAlphaModule : ColorAlphaModule<ProceduralShape>
    {
        protected override long PropertyKey => 714L;
        protected override Color ReadColor() => Target.InnerShadowColor;
        protected override void WriteColor(Color color) => Target.InnerShadowColor = color;
    }

    /// <summary>
    /// Animates the inner-shadow gradient angle in degrees.
    /// </summary>
    [Serializable, ModuleMenu("Evo UI/Procedural Shape/Inner Shadow/Gradient Angle")]
    public sealed class ProceduralShapeInnerShadowGradientAngleModule : ConstrainedFloatModule<ProceduralShape>
    {
        protected override long PropertyKey => 715L;
        protected override float MinimumValue => 0f;
        protected override float MaximumValue => 360f;
        protected override float Read() => Target.InnerShadowGradientAngle;
        protected override void Write(float value) => Target.InnerShadowGradientAngle = value;
    }

    /// <summary>
    /// Animates the inner-shadow gradient zoom.
    /// </summary>
    [Serializable, ModuleMenu("Evo UI/Procedural Shape/Inner Shadow/Gradient Zoom")]
    public sealed class ProceduralShapeInnerShadowGradientZoomModule : ConstrainedFloatModule<ProceduralShape>
    {
        protected override long PropertyKey => 716L;
        protected override float MinimumValue => 0.1f;
        protected override float MaximumValue => 4f;
        protected override float Read() => Target.InnerShadowGradientZoom;
        protected override void Write(float value) => Target.InnerShadowGradientZoom = value;
    }

    /// <summary>
    /// Animates the outer-shadow offset.
    /// </summary>
    [Serializable, ModuleMenu("Evo UI/Procedural Shape/Outer Shadow/Offset")]
    public sealed class ProceduralShapeOuterShadowOffsetModule : Vector2Module<ProceduralShape>
    {
        protected override long PropertyKey => 717L;
        protected override Vector2 Read() => Target.OuterShadowOffset;
        protected override void Write(Vector2 value) => Target.OuterShadowOffset = value;
    }

    /// <summary>
    /// Animates the outer-shadow expansion size.
    /// </summary>
    [Serializable, ModuleMenu("Evo UI/Procedural Shape/Outer Shadow/Size")]
    public sealed class ProceduralShapeOuterShadowSizeModule : NonNegativeFloatModule<ProceduralShape>
    {
        protected override long PropertyKey => 718L;
        protected override float Read() => Target.OuterShadowSize;
        protected override void Write(float value) => Target.OuterShadowSize = value;
    }

    /// <summary>
    /// Animates the outer-shadow blur radius.
    /// </summary>
    [Serializable, ModuleMenu("Evo UI/Procedural Shape/Outer Shadow/Softness")]
    public sealed class ProceduralShapeOuterShadowSoftnessModule : NonNegativeFloatModule<ProceduralShape>
    {
        protected override long PropertyKey => 719L;
        protected override float Read() => Target.OuterShadowSoftness;
        protected override void Write(float value) => Target.OuterShadowSoftness = value;
    }

    /// <summary>
    /// Animates the custom outer-shadow color, including its alpha channel.
    /// </summary>
    [Serializable, ModuleMenu("Evo UI/Procedural Shape/Outer Shadow/Color")]
    public sealed class ProceduralShapeOuterShadowColorModule : ColorModule<ProceduralShape>
    {
        protected override long PropertyKey => 720L;
        protected override Color Read() => Target.OuterShadowColor;
        protected override void Write(Color value) => Target.OuterShadowColor = value;
    }

    /// <summary>
    /// Animates only the alpha channel of the custom outer-shadow color.
    /// </summary>
    [Serializable, ModuleMenu("Evo UI/Procedural Shape/Outer Shadow/Alpha")]
    public sealed class ProceduralShapeOuterShadowAlphaModule : ColorAlphaModule<ProceduralShape>
    {
        protected override long PropertyKey => 720L;
        protected override Color ReadColor() => Target.OuterShadowColor;
        protected override void WriteColor(Color color) => Target.OuterShadowColor = color;
    }

    /// <summary>
    /// Animates the outer-shadow gradient angle in degrees.
    /// </summary>
    [Serializable, ModuleMenu("Evo UI/Procedural Shape/Outer Shadow/Gradient Angle")]
    public sealed class ProceduralShapeOuterShadowGradientAngleModule : ConstrainedFloatModule<ProceduralShape>
    {
        protected override long PropertyKey => 721L;
        protected override float MinimumValue => 0f;
        protected override float MaximumValue => 360f;
        protected override float Read() => Target.OuterShadowGradientAngle;
        protected override void Write(float value) => Target.OuterShadowGradientAngle = value;
    }

    /// <summary>
    /// Animates the outer-shadow gradient zoom.
    /// </summary>
    [Serializable, ModuleMenu("Evo UI/Procedural Shape/Outer Shadow/Gradient Zoom")]
    public sealed class ProceduralShapeOuterShadowGradientZoomModule : ConstrainedFloatModule<ProceduralShape>
    {
        protected override long PropertyKey => 722L;
        protected override float MinimumValue => 0.1f;
        protected override float MaximumValue => 4f;
        protected override float Read() => Target.OuterShadowGradientZoom;
        protected override void Write(float value) => Target.OuterShadowGradientZoom = value;
    }
}