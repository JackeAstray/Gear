using UnityEngine;
using UnityEngine.UI;
using Evo.Tween.Modules.UGUI;

namespace Evo.Tween
{
    /// <summary>
    /// Fluent bindings backed by the UGUI modules used by serialized clips.
    /// </summary>
    public static class UGUITweenExtensions
    {
        public static TweenHandle BindToAnchoredPosition(this TweenBuilder<Vector2> tween, RectTransform target)
        {
            return tween.Bind<RectTransformPositionModule>(target);
        }

        public static TweenHandle BindToSlide(this TweenBuilder<Vector2> tween, RectTransform target)
        {
            return tween.Bind<RectTransformSlideModule>(target);
        }

        public static TweenHandle BindToSizeDelta(this TweenBuilder<Vector2> tween, RectTransform target)
        {
            return tween.Bind<RectTransformSizeModule>(target);
        }

        public static TweenHandle BindToLayoutSize(this TweenBuilder<Vector2> tween, RectTransform target)
        {
            return tween.Bind<RectTransformLayoutSizeModule>(target);
        }

        public static TweenHandle BindToAlpha(this TweenBuilder<float> tween, CanvasGroup target)
        {
            return tween.Bind<CanvasGroupAlphaModule>(target);
        }

        public static TweenHandle BindToInteractable(this TweenBuilder<bool> tween, CanvasGroup target)
        {
            return tween.Bind<CanvasGroupInteractableModule>(target);
        }

        public static TweenHandle BindToBlocksRaycasts(this TweenBuilder<bool> tween, CanvasGroup target)
        {
            return tween.Bind<CanvasGroupBlocksRaycastsModule>(target);
        }

        public static TweenHandle BindToIgnoreParentGroups(this TweenBuilder<bool> tween, CanvasGroup target)
        {
            return tween.Bind<CanvasGroupIgnoreParentGroupsModule>(target);
        }

        public static TweenHandle BindToPivot(this TweenBuilder<Vector2> tween, RectTransform target)
        {
            return tween.Bind<RectTransformPivotModule>(target);
        }

        public static TweenHandle BindToAnchorMin(this TweenBuilder<Vector2> tween, RectTransform target)
        {
            return tween.Bind<RectTransformAnchorMinModule>(target);
        }

        public static TweenHandle BindToAnchorMax(this TweenBuilder<Vector2> tween, RectTransform target)
        {
            return tween.Bind<RectTransformAnchorMaxModule>(target);
        }

        public static TweenHandle BindToOffsetMin(this TweenBuilder<Vector2> tween, RectTransform target)
        {
            return tween.Bind<RectTransformOffsetMinModule>(target);
        }

        public static TweenHandle BindToOffsetMax(this TweenBuilder<Vector2> tween, RectTransform target)
        {
            return tween.Bind<RectTransformOffsetMaxModule>(target);
        }

        public static TweenHandle BindToColor(this TweenBuilder<Color> tween, Graphic target)
        {
            return tween.Bind<GraphicColorModule>(target);
        }

        public static TweenHandle BindToAlpha(this TweenBuilder<float> tween, Graphic target)
        {
            return tween.Bind<GraphicAlphaModule>(target);
        }

        public static TweenHandle BindToFillAmount(this TweenBuilder<float> tween, Image target)
        {
            return tween.Bind<ImageFillAmountModule>(target);
        }

        public static TweenHandle BindToValue(this TweenBuilder<float> tween, Slider target)
        {
            return tween.Bind<SliderValueModule>(target);
        }

        public static TweenHandle BindToValue(this TweenBuilder<float> tween, Scrollbar target)
        {
            return tween.Bind<ScrollbarValueModule>(target);
        }

        public static TweenHandle BindToNormalizedPosition(this TweenBuilder<Vector2> tween, ScrollRect target)
        {
            return tween.Bind<ScrollRectPositionModule>(target);
        }

        public static TweenHandle BindToPreferredSize(this TweenBuilder<Vector2> tween, LayoutElement target)
        {
            return tween.Bind<LayoutElementPreferredSizeModule>(target);
        }

        public static TweenHandle BindToMinimumSize(this TweenBuilder<Vector2> tween, LayoutElement target)
        {
            return tween.Bind<LayoutElementMinimumSizeModule>(target);
        }

        public static TweenHandle BindToFlexibleSize(this TweenBuilder<Vector2> tween, LayoutElement target)
        {
            return tween.Bind<LayoutElementFlexibleSizeModule>(target);
        }
    }
}