using System.Collections.Generic;
using UnityEngine;
using Evo.Tween.Editor;
using Evo.UI;

namespace Evo.Tween.Modules.EvoUI.Editor
{
    public sealed class ProceduralShapeTweenRecording : ITweenRecordingProvider
    {
        public IEnumerable<TweenRecordingBinding> GetBindings()
        {
            yield return new TweenRecordingBinding<ProceduralShapePolygonSidesModule, ProceduralShape, int>("polygonSides",
                new[] { "polygonSides" }, (module, target) => target.PolygonSides,
                (module, target, value) => target.PolygonSides = value);

            yield return new TweenRecordingBinding<ProceduralShapeAngleModule, ProceduralShape, float>("shapeAngle",
                new[] { "shapeAngle" }, (module, target) => target.ShapeAngle,
                (module, target, value) => target.ShapeAngle = value);

            yield return new TweenRecordingBinding<ProceduralShapeSkewModule, ProceduralShape, Vector2>("shapeSkew",
                new[] { "shapeSkew" }, (module, target) => target.ShapeSkew,
                (module, target, value) => target.ShapeSkew = value);

            yield return new TweenRecordingBinding<ProceduralShapeClipAmountModule, ProceduralShape, float>("clipAmount",
                new[] { "clipAmount" }, (module, target) => target.ClipAmount,
                (module, target, value) => target.ClipAmount = value);

            yield return new TweenRecordingBinding<ProceduralShapeSoftnessModule, ProceduralShape, float>("softness",
                new[] { "softness" }, (module, target) => target.Softness,
                (module, target, value) => target.Softness = value);

            yield return new TweenRecordingBinding<ProceduralShapeFillColorModule, ProceduralShape, Color>("fillColor",
                new[] { "fillColor" }, (module, target) => target.FillColor,
                (module, target, value) => target.FillColor = value);

            yield return new TweenRecordingBinding<ProceduralShapeFillAlphaModule, ProceduralShape, float>("fillColor",
                new[] { "fillColor.a" }, (module, target) => target.FillColor.a,
                (module, target, value) => { Color color = target.FillColor; color.a = value; target.FillColor = color; }, target => false);

            yield return new TweenRecordingBinding<ProceduralShapeFillGradientAngleModule, ProceduralShape, float>("fillGradientAngle",
                new[] { "fillGradientAngle" }, (module, target) => target.FillGradientAngle,
                (module, target, value) => target.FillGradientAngle = value);

            yield return new TweenRecordingBinding<ProceduralShapeFillGradientZoomModule, ProceduralShape, float>("fillGradientZoom",
                new[] { "fillGradientZoom" }, (module, target) => target.FillGradientZoom,
                (module, target, value) => target.FillGradientZoom = value);

            yield return new TweenRecordingBinding<ProceduralShapePatternPolygonSidesModule, ProceduralShape, int>("patternPolygonSides",
                new[] { "patternPolygonSides" }, (module, target) => target.PatternPolygonSides,
                (module, target, value) => target.PatternPolygonSides = value);

            yield return new TweenRecordingBinding<ProceduralShapePatternRadiusModule, ProceduralShape, float>("patternRadius",
                new[] { "patternRadius" }, (module, target) => target.PatternRadius,
                (module, target, value) => target.PatternRadius = value);

            yield return new TweenRecordingBinding<ProceduralShapePatternSizeModule, ProceduralShape, Vector2>("patternSize",
                new[] { "patternSize" }, (module, target) => target.PatternSize,
                (module, target, value) => target.PatternSize = value);

            yield return new TweenRecordingBinding<ProceduralShapePatternGapModule, ProceduralShape, Vector2>("patternGap",
                new[] { "patternGap" }, (module, target) => target.PatternGap,
                (module, target, value) => target.PatternGap = value);

            yield return new TweenRecordingBinding<ProceduralShapePatternOffsetModule, ProceduralShape, Vector2>("patternOffset",
                new[] { "patternOffset" }, (module, target) => target.PatternOffset,
                (module, target, value) => target.PatternOffset = value);

            yield return new TweenRecordingBinding<ProceduralShapePatternAngleModule, ProceduralShape, float>("patternAngle",
                new[] { "patternAngle" }, (module, target) => target.PatternAngle,
                (module, target, value) => target.PatternAngle = value);

            yield return new TweenRecordingBinding<ProceduralShapePatternFalloffOriginModule, ProceduralShape, Vector2>("patternFalloffOrigin",
                new[] { "patternFalloffOrigin" }, (module, target) => target.PatternFalloffOrigin,
                (module, target, value) => target.PatternFalloffOrigin = value);

            yield return new TweenRecordingBinding<ProceduralShapePatternFalloffStartModule, ProceduralShape, float>("patternFalloffStart",
                new[] { "patternFalloffStart" }, (module, target) => target.PatternFalloffStart,
                (module, target, value) => target.PatternFalloffStart = value);

            yield return new TweenRecordingBinding<ProceduralShapePatternFalloffEndModule, ProceduralShape, float>("patternFalloffEnd",
                new[] { "patternFalloffEnd" }, (module, target) => target.PatternFalloffEnd,
                (module, target, value) => target.PatternFalloffEnd = value);

            yield return new TweenRecordingBinding<ProceduralShapePatternColorModule, ProceduralShape, Color>("patternColor",
                new[] { "patternColor" }, (module, target) => target.PatternColor,
                (module, target, value) => target.PatternColor = value);

            yield return new TweenRecordingBinding<ProceduralShapePatternAlphaModule, ProceduralShape, float>("patternColor",
                new[] { "patternColor.a" }, (module, target) => target.PatternColor.a,
                (module, target, value) => { Color color = target.PatternColor; color.a = value; target.PatternColor = color; }, target => false);

            yield return new TweenRecordingBinding<ProceduralShapePatternGradientAngleModule, ProceduralShape, float>("patternGradientAngle",
                new[] { "patternGradientAngle" }, (module, target) => target.PatternGradientAngle,
                (module, target, value) => target.PatternGradientAngle = value);

            yield return new TweenRecordingBinding<ProceduralShapePatternGradientZoomModule, ProceduralShape, float>("patternGradientZoom",
                new[] { "patternGradientZoom" }, (module, target) => target.PatternGradientZoom,
                (module, target, value) => target.PatternGradientZoom = value);

            yield return new TweenRecordingBinding<ProceduralShapeOutlineWidthModule, ProceduralShape, float>("outlineWidth",
                new[] { "outlineWidth" }, (module, target) => target.OutlineWidth,
                (module, target, value) => target.OutlineWidth = value);

            yield return new TweenRecordingBinding<ProceduralShapeOutlineDashLengthModule, ProceduralShape, float>("outlineDashLength",
                new[] { "outlineDashLength" }, (module, target) => target.OutlineDashLength,
                (module, target, value) => target.OutlineDashLength = value);

            yield return new TweenRecordingBinding<ProceduralShapeOutlineDashGapModule, ProceduralShape, float>("outlineDashGap",
                new[] { "outlineDashGap" }, (module, target) => target.OutlineDashGap,
                (module, target, value) => target.OutlineDashGap = value);

            yield return new TweenRecordingBinding<ProceduralShapeOutlineDashOffsetModule, ProceduralShape, float>("outlineDashOffset",
                new[] { "outlineDashOffset" }, (module, target) => target.OutlineDashOffset,
                (module, target, value) => target.OutlineDashOffset = value);

            yield return new TweenRecordingBinding<ProceduralShapeOutlineColorModule, ProceduralShape, Color>("outlineColor",
                new[] { "outlineColor" }, (module, target) => target.OutlineColor,
                (module, target, value) => target.OutlineColor = value);

            yield return new TweenRecordingBinding<ProceduralShapeOutlineAlphaModule, ProceduralShape, float>("outlineColor",
                new[] { "outlineColor.a" }, (module, target) => target.OutlineColor.a,
                (module, target, value) => { Color color = target.OutlineColor; color.a = value; target.OutlineColor = color; }, target => false);

            yield return new TweenRecordingBinding<ProceduralShapeOutlineGradientAngleModule, ProceduralShape, float>("outlineGradientAngle",
                new[] { "outlineGradientAngle" }, (module, target) => target.OutlineGradientAngle,
                (module, target, value) => target.OutlineGradientAngle = value);

            yield return new TweenRecordingBinding<ProceduralShapeOutlineGradientZoomModule, ProceduralShape, float>("outlineGradientZoom",
                new[] { "outlineGradientZoom" }, (module, target) => target.OutlineGradientZoom,
                (module, target, value) => target.OutlineGradientZoom = value);

            yield return new TweenRecordingBinding<ProceduralShapeCornerRadiusModule, ProceduralShape, Vector4>("cornerRadius",
                new[] { "cornerRadius" }, (module, target) => target.CornerRadius,
                (module, target, value) => target.CornerRadius = value);

            yield return new TweenRecordingBinding<ProceduralShapeUniformCornerRadiusModule, ProceduralShape, float>("cornerRadius",
                new[] { "cornerRadius" }, (module, target) => target.CornerRadius.x,
                (module, target, value) => target.UniformCornerRadius = value, target => false,
                unavailableReason: "Uniform Corner Radius writes all four corners. Use Corner Radii to record complete radius poses.");

            yield return new TweenRecordingBinding<ProceduralShapeInnerShadowOffsetModule, ProceduralShape, Vector2>("innerShadowOffset",
                new[] { "innerShadowOffset" }, (module, target) => target.InnerShadowOffset,
                (module, target, value) => target.InnerShadowOffset = value);

            yield return new TweenRecordingBinding<ProceduralShapeInnerShadowSizeModule, ProceduralShape, float>("innerShadowSize",
                new[] { "innerShadowSize" }, (module, target) => target.InnerShadowSize,
                (module, target, value) => target.InnerShadowSize = value);

            yield return new TweenRecordingBinding<ProceduralShapeInnerShadowSoftnessModule, ProceduralShape, float>("innerShadowSoftness",
                new[] { "innerShadowSoftness" }, (module, target) => target.InnerShadowSoftness,
                (module, target, value) => target.InnerShadowSoftness = value);

            yield return new TweenRecordingBinding<ProceduralShapeInnerShadowColorModule, ProceduralShape, Color>("innerShadowColor",
                new[] { "innerShadowColor" }, (module, target) => target.InnerShadowColor,
                (module, target, value) => target.InnerShadowColor = value);

            yield return new TweenRecordingBinding<ProceduralShapeInnerShadowAlphaModule, ProceduralShape, float>("innerShadowColor",
                new[] { "innerShadowColor.a" }, (module, target) => target.InnerShadowColor.a,
                (module, target, value) => { Color color = target.InnerShadowColor; color.a = value; target.InnerShadowColor = color; }, target => false);

            yield return new TweenRecordingBinding<ProceduralShapeInnerShadowGradientAngleModule, ProceduralShape, float>("innerShadowGradientAngle",
                new[] { "innerShadowGradientAngle" }, (module, target) => target.InnerShadowGradientAngle,
                (module, target, value) => target.InnerShadowGradientAngle = value);

            yield return new TweenRecordingBinding<ProceduralShapeInnerShadowGradientZoomModule, ProceduralShape, float>("innerShadowGradientZoom",
                new[] { "innerShadowGradientZoom" }, (module, target) => target.InnerShadowGradientZoom,
                (module, target, value) => target.InnerShadowGradientZoom = value);

            yield return new TweenRecordingBinding<ProceduralShapeOuterShadowOffsetModule, ProceduralShape, Vector2>("outerShadowOffset",
                new[] { "outerShadowOffset" }, (module, target) => target.OuterShadowOffset,
                (module, target, value) => target.OuterShadowOffset = value);

            yield return new TweenRecordingBinding<ProceduralShapeOuterShadowSizeModule, ProceduralShape, float>("outerShadowSize",
                new[] { "outerShadowSize" }, (module, target) => target.OuterShadowSize,
                (module, target, value) => target.OuterShadowSize = value);

            yield return new TweenRecordingBinding<ProceduralShapeOuterShadowSoftnessModule, ProceduralShape, float>("outerShadowSoftness",
                new[] { "outerShadowSoftness" }, (module, target) => target.OuterShadowSoftness,
                (module, target, value) => target.OuterShadowSoftness = value);

            yield return new TweenRecordingBinding<ProceduralShapeOuterShadowColorModule, ProceduralShape, Color>("outerShadowColor",
                new[] { "outerShadowColor" }, (module, target) => target.OuterShadowColor,
                (module, target, value) => target.OuterShadowColor = value);

            yield return new TweenRecordingBinding<ProceduralShapeOuterShadowAlphaModule, ProceduralShape, float>("outerShadowColor",
                new[] { "outerShadowColor.a" }, (module, target) => target.OuterShadowColor.a,
                (module, target, value) => { Color color = target.OuterShadowColor; color.a = value; target.OuterShadowColor = color; }, target => false);

            yield return new TweenRecordingBinding<ProceduralShapeOuterShadowGradientAngleModule, ProceduralShape, float>("outerShadowGradientAngle",
                new[] { "outerShadowGradientAngle" }, (module, target) => target.OuterShadowGradientAngle,
                (module, target, value) => target.OuterShadowGradientAngle = value);

            yield return new TweenRecordingBinding<ProceduralShapeOuterShadowGradientZoomModule, ProceduralShape, float>("outerShadowGradientZoom",
                new[] { "outerShadowGradientZoom" }, (module, target) => target.OuterShadowGradientZoom,
                (module, target, value) => target.OuterShadowGradientZoom = value);
        }
    }
}