using UnityEngine;
using Evo.UI;
using Evo.Tween.Modules.EvoUI;

namespace Evo.Tween
{
    /// <summary>
    /// Fluent bindings backed by the ProceduralShape modules used by serialized clips.
    /// </summary>
    public static class ProceduralShapeTweenExtensions
    {
        public static TweenHandle BindToPolygonSides(this TweenBuilder<int> tween, ProceduralShape target)
        {
            return tween.Bind<ProceduralShapePolygonSidesModule>(target);
        }

        public static TweenHandle BindToShapeAngle(this TweenBuilder<float> tween, ProceduralShape target)
        {
            return tween.Bind<ProceduralShapeAngleModule>(target);
        }

        public static TweenHandle BindToShapeSkew(this TweenBuilder<Vector2> tween, ProceduralShape target)
        {
            return tween.Bind<ProceduralShapeSkewModule>(target);
        }

        public static TweenHandle BindToClipAmount(this TweenBuilder<float> tween, ProceduralShape target)
        {
            return tween.Bind<ProceduralShapeClipAmountModule>(target);
        }

        public static TweenHandle BindToSoftness(this TweenBuilder<float> tween, ProceduralShape target)
        {
            return tween.Bind<ProceduralShapeSoftnessModule>(target);
        }

        public static TweenHandle BindToFillColor(this TweenBuilder<Color> tween, ProceduralShape target)
        {
            return tween.Bind<ProceduralShapeFillColorModule>(target);
        }

        public static TweenHandle BindToFillAlpha(this TweenBuilder<float> tween, ProceduralShape target)
        {
            return tween.Bind<ProceduralShapeFillAlphaModule>(target);
        }

        public static TweenHandle BindToFillGradientAngle(this TweenBuilder<float> tween, ProceduralShape target)
        {
            return tween.Bind<ProceduralShapeFillGradientAngleModule>(target);
        }

        public static TweenHandle BindToFillGradientZoom(this TweenBuilder<float> tween, ProceduralShape target)
        {
            return tween.Bind<ProceduralShapeFillGradientZoomModule>(target);
        }

        public static TweenHandle BindToPatternPolygonSides(this TweenBuilder<int> tween, ProceduralShape target)
        {
            return tween.Bind<ProceduralShapePatternPolygonSidesModule>(target);
        }

        public static TweenHandle BindToPatternRadius(this TweenBuilder<float> tween, ProceduralShape target)
        {
            return tween.Bind<ProceduralShapePatternRadiusModule>(target);
        }

        public static TweenHandle BindToPatternSize(this TweenBuilder<Vector2> tween, ProceduralShape target)
        {
            return tween.Bind<ProceduralShapePatternSizeModule>(target);
        }

        public static TweenHandle BindToPatternGap(this TweenBuilder<Vector2> tween, ProceduralShape target)
        {
            return tween.Bind<ProceduralShapePatternGapModule>(target);
        }

        public static TweenHandle BindToPatternOffset(this TweenBuilder<Vector2> tween, ProceduralShape target)
        {
            return tween.Bind<ProceduralShapePatternOffsetModule>(target);
        }

        public static TweenHandle BindToPatternAngle(this TweenBuilder<float> tween, ProceduralShape target)
        {
            return tween.Bind<ProceduralShapePatternAngleModule>(target);
        }

        public static TweenHandle BindToPatternFalloffOrigin(this TweenBuilder<Vector2> tween, ProceduralShape target)
        {
            return tween.Bind<ProceduralShapePatternFalloffOriginModule>(target);
        }

        public static TweenHandle BindToPatternFalloffStart(this TweenBuilder<float> tween, ProceduralShape target)
        {
            return tween.Bind<ProceduralShapePatternFalloffStartModule>(target);
        }

        public static TweenHandle BindToPatternFalloffEnd(this TweenBuilder<float> tween, ProceduralShape target)
        {
            return tween.Bind<ProceduralShapePatternFalloffEndModule>(target);
        }

        public static TweenHandle BindToPatternColor(this TweenBuilder<Color> tween, ProceduralShape target)
        {
            return tween.Bind<ProceduralShapePatternColorModule>(target);
        }

        public static TweenHandle BindToPatternAlpha(this TweenBuilder<float> tween, ProceduralShape target)
        {
            return tween.Bind<ProceduralShapePatternAlphaModule>(target);
        }

        public static TweenHandle BindToPatternGradientAngle(this TweenBuilder<float> tween, ProceduralShape target)
        {
            return tween.Bind<ProceduralShapePatternGradientAngleModule>(target);
        }

        public static TweenHandle BindToPatternGradientZoom(this TweenBuilder<float> tween, ProceduralShape target)
        {
            return tween.Bind<ProceduralShapePatternGradientZoomModule>(target);
        }

        public static TweenHandle BindToOutlineWidth(this TweenBuilder<float> tween, ProceduralShape target)
        {
            return tween.Bind<ProceduralShapeOutlineWidthModule>(target);
        }

        public static TweenHandle BindToOutlineDashLength(this TweenBuilder<float> tween, ProceduralShape target)
        {
            return tween.Bind<ProceduralShapeOutlineDashLengthModule>(target);
        }

        public static TweenHandle BindToOutlineDashGap(this TweenBuilder<float> tween, ProceduralShape target)
        {
            return tween.Bind<ProceduralShapeOutlineDashGapModule>(target);
        }

        public static TweenHandle BindToOutlineDashOffset(this TweenBuilder<float> tween, ProceduralShape target)
        {
            return tween.Bind<ProceduralShapeOutlineDashOffsetModule>(target);
        }

        public static TweenHandle BindToOutlineColor(this TweenBuilder<Color> tween, ProceduralShape target)
        {
            return tween.Bind<ProceduralShapeOutlineColorModule>(target);
        }

        public static TweenHandle BindToOutlineAlpha(this TweenBuilder<float> tween, ProceduralShape target)
        {
            return tween.Bind<ProceduralShapeOutlineAlphaModule>(target);
        }

        public static TweenHandle BindToOutlineGradientAngle(this TweenBuilder<float> tween, ProceduralShape target)
        {
            return tween.Bind<ProceduralShapeOutlineGradientAngleModule>(target);
        }

        public static TweenHandle BindToOutlineGradientZoom(this TweenBuilder<float> tween, ProceduralShape target)
        {
            return tween.Bind<ProceduralShapeOutlineGradientZoomModule>(target);
        }

        public static TweenHandle BindToCornerRadius(this TweenBuilder<Vector4> tween, ProceduralShape target)
        {
            return tween.Bind<ProceduralShapeCornerRadiusModule>(target);
        }

        public static TweenHandle BindToCornerRadius(this TweenBuilder<float> tween, ProceduralShape target)
        {
            return tween.Bind<ProceduralShapeUniformCornerRadiusModule>(target);
        }

        public static TweenHandle BindToInnerShadowOffset(this TweenBuilder<Vector2> tween, ProceduralShape target)
        {
            return tween.Bind<ProceduralShapeInnerShadowOffsetModule>(target);
        }

        public static TweenHandle BindToInnerShadowSize(this TweenBuilder<float> tween, ProceduralShape target)
        {
            return tween.Bind<ProceduralShapeInnerShadowSizeModule>(target);
        }

        public static TweenHandle BindToInnerShadowSoftness(this TweenBuilder<float> tween, ProceduralShape target)
        {
            return tween.Bind<ProceduralShapeInnerShadowSoftnessModule>(target);
        }

        public static TweenHandle BindToInnerShadowColor(this TweenBuilder<Color> tween, ProceduralShape target)
        {
            return tween.Bind<ProceduralShapeInnerShadowColorModule>(target);
        }

        public static TweenHandle BindToInnerShadowAlpha(this TweenBuilder<float> tween, ProceduralShape target)
        {
            return tween.Bind<ProceduralShapeInnerShadowAlphaModule>(target);
        }

        public static TweenHandle BindToInnerShadowGradientAngle(this TweenBuilder<float> tween, ProceduralShape target)
        {
            return tween.Bind<ProceduralShapeInnerShadowGradientAngleModule>(target);
        }

        public static TweenHandle BindToInnerShadowGradientZoom(this TweenBuilder<float> tween, ProceduralShape target)
        {
            return tween.Bind<ProceduralShapeInnerShadowGradientZoomModule>(target);
        }

        public static TweenHandle BindToOuterShadowOffset(this TweenBuilder<Vector2> tween, ProceduralShape target)
        {
            return tween.Bind<ProceduralShapeOuterShadowOffsetModule>(target);
        }

        public static TweenHandle BindToOuterShadowSize(this TweenBuilder<float> tween, ProceduralShape target)
        {
            return tween.Bind<ProceduralShapeOuterShadowSizeModule>(target);
        }

        public static TweenHandle BindToOuterShadowSoftness(this TweenBuilder<float> tween, ProceduralShape target)
        {
            return tween.Bind<ProceduralShapeOuterShadowSoftnessModule>(target);
        }

        public static TweenHandle BindToOuterShadowColor(this TweenBuilder<Color> tween, ProceduralShape target)
        {
            return tween.Bind<ProceduralShapeOuterShadowColorModule>(target);
        }

        public static TweenHandle BindToOuterShadowAlpha(this TweenBuilder<float> tween, ProceduralShape target)
        {
            return tween.Bind<ProceduralShapeOuterShadowAlphaModule>(target);
        }

        public static TweenHandle BindToOuterShadowGradientAngle(this TweenBuilder<float> tween, ProceduralShape target)
        {
            return tween.Bind<ProceduralShapeOuterShadowGradientAngleModule>(target);
        }

        public static TweenHandle BindToOuterShadowGradientZoom(this TweenBuilder<float> tween, ProceduralShape target)
        {
            return tween.Bind<ProceduralShapeOuterShadowGradientZoomModule>(target);
        }
    }
}