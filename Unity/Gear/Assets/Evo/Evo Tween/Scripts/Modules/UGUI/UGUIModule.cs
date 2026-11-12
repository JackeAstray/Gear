using System;
using UnityEngine;
using UnityEngine.UI;

namespace Evo.Tween.Modules.UGUI
{
    /// <summary>
    /// Animates a RectTransform between anchored-position values.
    /// </summary>
    [Serializable, ModuleMenu("UI/Rect Transform/Anchored Position")]
    public sealed class RectTransformPositionModule : Vector2Module<RectTransform>
    {
        protected override long PropertyKey => 100L;
        protected override Vector2 Read() => Target.anchoredPosition;
        protected override void Write(Vector2 value) => Target.anchoredPosition = value;
    }

    /// <summary>
    /// Slides a RectTransform by offsets relative to the anchored position established by its layout or positioning code.
    /// </summary>
    [Serializable, ModuleMenu("UI/Rect Transform/Slide")]
    public sealed class RectTransformSlideModule : TweenModule, IValueModule<Vector2>
    {
        [ModuleField(ModuleFieldGroup.Values, ModuleFieldRole.FromCurrent)]
        [Tooltip("Use the current anchored position as the start, without applying Start Offset.")]
        public bool fromCurrent = true;

        [ModuleField(ModuleFieldGroup.Values, ModuleFieldRole.From, Label = "Start Offset")]
        [Tooltip("Offset from the anchored position captured when this clip begins.")]
        public Vector2 from = Vector2.zero;

        [ModuleField(ModuleFieldGroup.Values, ModuleFieldRole.To, Label = "Target Offset")]
        [Tooltip("Offset from the anchored position captured when this clip begins.")]
        public Vector2 to = Vector2.zero;

        [NonSerialized] Vector2 originalValue;
        [NonSerialized] Vector2 startValue;
        [NonSerialized] Vector2 endValue;

        public override Type TargetType => typeof(RectTransform);
        protected override long PropertyKey => 100L;
        protected override bool CanHoldBeforeStart => !fromCurrent;
        RectTransform Target => CapturedTarget as RectTransform;

        protected override bool OnCapture()
        {
            originalValue = Target.anchoredPosition;
            return true;
        }

        protected override void OnStart()
        {
            if (Target == null)
                return;

            Vector2 origin = Target.anchoredPosition;
            startValue = fromCurrent ? origin : origin + from;
            endValue = origin + to;
        }

        protected override void OnEvaluate(float easedTime)
        {
            if (Target != null)
                Target.anchoredPosition = Vector2.LerpUnclamped(startValue, endValue, easedTime);
        }

        protected override void OnRestore()
        {
            if (Target != null)
                Target.anchoredPosition = originalValue;
        }

        public void SetValues(Vector2 start, Vector2 end)
        {
            fromCurrent = false;
            from = start;
            to = end;
        }

        protected override bool TryCopyTo(TweenModule destination)
        {
            RectTransformSlideModule copy = (RectTransformSlideModule)destination;
            copy.fromCurrent = fromCurrent;
            copy.from = from;
            copy.to = to;
            return true;
        }
    }

    /// <summary>
    /// Animates a RectTransform between size-delta values.
    /// </summary>
    [Serializable, ModuleMenu("UI/Rect Transform/Size Delta")]
    public sealed class RectTransformSizeModule : Vector2Module<RectTransform>
    {
        protected override long PropertyKey => 101L;
        protected override Vector2 Read() => Target.sizeDelta;
        protected override void Write(Vector2 value) => Target.sizeDelta = value;
    }

    /// <summary>
    /// Animates a RectTransform by size multipliers relative to the size established by its layout or sizing code.
    /// Apply the layout before playback; the resolved size is read once when the clip begins.
    /// </summary>
    [Serializable, ModuleMenu("UI/Rect Transform/Layout Size")]
    public sealed class RectTransformLayoutSizeModule : TweenModule, IValueModule<Vector2>
    {
        [ModuleField(ModuleFieldGroup.Values, ModuleFieldRole.FromCurrent)]
        [Tooltip("Use the current size as the start, without applying Start Multiplier.")]
        public bool fromCurrent = true;

        [ModuleField(ModuleFieldGroup.Values, ModuleFieldRole.From, Label = "Start Multiplier")]
        [Tooltip("Multiplier of the resolved width and height when this clip begins. Zero collapses an axis; one preserves its size.")]
        public Vector2 from = Vector2.one;

        [ModuleField(ModuleFieldGroup.Values, ModuleFieldRole.To, Label = "Target Multiplier")]
        [Tooltip("Multiplier of the resolved width and height when this clip begins. One matches the layout size.")]
        public Vector2 to = Vector2.one;

        [NonSerialized] Vector2 originalValue;
        [NonSerialized] Vector2 startValue;
        [NonSerialized] Vector2 endValue;

        public override Type TargetType => typeof(RectTransform);
        protected override long PropertyKey => 101L;
        protected override bool CanHoldBeforeStart => !fromCurrent;
        RectTransform Target => CapturedTarget as RectTransform;

        protected override bool OnCapture()
        {
            originalValue = Target.sizeDelta;
            return true;
        }

        protected override void OnStart()
        {
            if (Target == null)
                return;

            Vector2 size = Target.rect.size;
            Vector2 sizeDelta = Target.sizeDelta;

            // Resolve actual sizes to sizeDelta once, including stretched anchors.
            startValue = fromCurrent ? sizeDelta : sizeDelta + Vector2.Scale(size, from - Vector2.one);
            endValue = sizeDelta + Vector2.Scale(size, to - Vector2.one);
        }

        protected override void OnEvaluate(float easedTime)
        {
            if (Target != null)
                Target.sizeDelta = Vector2.LerpUnclamped(startValue, endValue, easedTime);
        }

        protected override void OnRestore()
        {
            if (Target != null)
                Target.sizeDelta = originalValue;
        }

        public void SetValues(Vector2 start, Vector2 end)
        {
            fromCurrent = false;
            from = start;
            to = end;
        }

        protected override bool TryCopyTo(TweenModule destination)
        {
            RectTransformLayoutSizeModule copy = (RectTransformLayoutSizeModule)destination;
            copy.fromCurrent = fromCurrent;
            copy.from = from;
            copy.to = to;
            return true;
        }
    }

    /// <summary>
    /// Animates only CanvasGroup.alpha, leaving all interaction flags unchanged.
    /// </summary>
    [Serializable, ModuleMenu("UI/Canvas Group/Alpha")]
    public sealed class CanvasGroupAlphaModule : NormalizedFloatModule<CanvasGroup>
    {
        protected override long PropertyKey => 102L;
        protected override float Read() => Target.alpha;
        protected override void Write(float value) => Target.alpha = value;
    }

    /// <summary>
    /// Switches a CanvasGroup's interactable state halfway through the tween.
    /// </summary>
    [Serializable, ModuleMenu("UI/Canvas Group/Interactable")]
    public sealed class CanvasGroupInteractableModule : BoolModule<CanvasGroup>
    {
        protected override long PropertyKey => 103L;
        protected override bool Read() => Target.interactable;
        protected override void Write(bool value) => Target.interactable = value;
    }

    /// <summary>
    /// Switches whether a CanvasGroup blocks raycasts halfway through the tween.
    /// </summary>
    [Serializable, ModuleMenu("UI/Canvas Group/Blocks Raycasts")]
    public sealed class CanvasGroupBlocksRaycastsModule : BoolModule<CanvasGroup>
    {
        protected override long PropertyKey => 104L;
        protected override bool Read() => Target.blocksRaycasts;
        protected override void Write(bool value) => Target.blocksRaycasts = value;
    }

    /// <summary>
    /// Switches whether a CanvasGroup ignores parent groups halfway through the tween.
    /// </summary>
    [Serializable, ModuleMenu("UI/Canvas Group/Ignore Parent Groups")]
    public sealed class CanvasGroupIgnoreParentGroupsModule : BoolModule<CanvasGroup>
    {
        protected override long PropertyKey => 105L;
        protected override bool Read() => Target.ignoreParentGroups;
        protected override void Write(bool value) => Target.ignoreParentGroups = value;
    }

    /// <summary>
    /// Animates a RectTransform's normalized pivot.
    /// </summary>
    [Serializable, ModuleMenu("UI/Rect Transform/Pivot")]
    public sealed class RectTransformPivotModule : Vector2Module<RectTransform>
    {
        protected override long PropertyKey => 106L;
        protected override Vector2 Read() => Target.pivot;
        protected override void Write(Vector2 value) => Target.pivot = value;
    }

    /// <summary>
    /// Animates a RectTransform's lower normalized anchor.
    /// </summary>
    [Serializable, ModuleMenu("UI/Rect Transform/Anchor Min")]
    public sealed class RectTransformAnchorMinModule : Vector2Module<RectTransform>
    {
        protected override long PropertyKey => 107L;
        protected override Vector2 Read() => Target.anchorMin;
        protected override void Write(Vector2 value) => Target.anchorMin = value;
    }

    /// <summary>
    /// Animates a RectTransform's upper normalized anchor.
    /// </summary>
    [Serializable, ModuleMenu("UI/Rect Transform/Anchor Max")]
    public sealed class RectTransformAnchorMaxModule : Vector2Module<RectTransform>
    {
        protected override long PropertyKey => 108L;
        protected override Vector2 Read() => Target.anchorMax;
        protected override void Write(Vector2 value) => Target.anchorMax = value;
    }

    /// <summary>
    /// Animates a RectTransform's lower-left offset relative to its anchors.
    /// </summary>
    [Serializable, ModuleMenu("UI/Rect Transform/Offset Min")]
    public sealed class RectTransformOffsetMinModule : Vector2Module<RectTransform>
    {
        protected override long PropertyKey => 109L;
        protected override Vector2 Read() => Target.offsetMin;
        protected override void Write(Vector2 value) => Target.offsetMin = value;
    }

    /// <summary>
    /// Animates a RectTransform's upper-right offset relative to its anchors.
    /// </summary>
    [Serializable, ModuleMenu("UI/Rect Transform/Offset Max")]
    public sealed class RectTransformOffsetMaxModule : Vector2Module<RectTransform>
    {
        protected override long PropertyKey => 110L;
        protected override Vector2 Read() => Target.offsetMax;
        protected override void Write(Vector2 value) => Target.offsetMax = value;
    }

    /// <summary>
    /// Animates a Graphic color, including its alpha channel.
    /// </summary>
    [Serializable, ModuleMenu("UI/Graphic/Color")]
    public sealed class GraphicColorModule : ColorModule<Graphic>
    {
        protected override long PropertyKey => 111L;
        protected override Color Read() => Target.color;
        protected override void Write(Color value) => Target.color = value;
    }

    /// <summary>
    /// Animates only the alpha channel of a Graphic color.
    /// </summary>
    [Serializable, ModuleMenu("UI/Graphic/Alpha")]
    public sealed class GraphicAlphaModule : ColorAlphaModule<Graphic>
    {
        protected override long PropertyKey => 111L;
        protected override Color ReadColor() => Target.color;
        protected override void WriteColor(Color color) => Target.color = color;
    }

    /// <summary>
    /// Animates an Image's radial, horizontal, or vertical fill amount from zero to one.
    /// </summary>
    [Serializable, ModuleMenu("UI/Image/Fill Amount")]
    public sealed class ImageFillAmountModule : NormalizedFloatModule<Image>
    {
        protected override long PropertyKey => 113L;
        protected override float Read() => Target.fillAmount;
        protected override void Write(float value) => Target.fillAmount = value;
    }

    /// <summary>
    /// Animates a Slider's current value while preserving the Slider's configured range.
    /// </summary>
    [Serializable, ModuleMenu("UI/Slider/Value")]
    public sealed class SliderValueModule : FloatModule<Slider>
    {
        protected override long PropertyKey => 114L;
        protected override float Read() => Target.value;
        protected override void Write(float value) => Target.value = value;
    }

    /// <summary>
    /// Animates a Scrollbar's normalized value from zero to one.
    /// </summary>
    [Serializable, ModuleMenu("UI/Scrollbar/Value")]
    public sealed class ScrollbarValueModule : NormalizedFloatModule<Scrollbar>
    {
        protected override long PropertyKey => 115L;
        protected override float Read() => Target.value;
        protected override void Write(float value) => Target.value = value;
    }

    /// <summary>
    /// Animates a ScrollRect's normalized horizontal and vertical position.
    /// </summary>
    [Serializable, ModuleMenu("UI/Scroll Rect/Position")]
    public sealed class ScrollRectPositionModule : Vector2Module<ScrollRect>
    {
        protected override long PropertyKey => 116L;
        protected override Vector2 Read() => Target.normalizedPosition;
        protected override void Write(Vector2 value) => Target.normalizedPosition = value;
    }

    /// <summary>
    /// Animates a LayoutElement's preferred width and height.
    /// </summary>
    [Serializable, ModuleMenu("UI/Layout Element/Preferred Size")]
    public sealed class LayoutElementPreferredSizeModule : Vector2Module<LayoutElement>
    {
        protected override long PropertyKey => 117L;
        protected override Vector2 Read() => new(Target.preferredWidth, Target.preferredHeight);
        protected override void Write(Vector2 value)
        {
            Target.preferredWidth = value.x;
            Target.preferredHeight = value.y;
        }
    }

    /// <summary>
    /// Animates a LayoutElement's minimum width and height.
    /// </summary>
    [Serializable, ModuleMenu("UI/Layout Element/Minimum Size")]
    public sealed class LayoutElementMinimumSizeModule : Vector2Module<LayoutElement>
    {
        protected override long PropertyKey => 118L;
        protected override Vector2 Read() => new(Target.minWidth, Target.minHeight);
        protected override void Write(Vector2 value)
        {
            Target.minWidth = value.x;
            Target.minHeight = value.y;
        }
    }

    /// <summary>
    /// Animates a LayoutElement's flexible width and height.
    /// </summary>
    [Serializable, ModuleMenu("UI/Layout Element/Flexible Size")]
    public sealed class LayoutElementFlexibleSizeModule : Vector2Module<LayoutElement>
    {
        protected override long PropertyKey => 119L;
        protected override Vector2 Read() => new(Target.flexibleWidth, Target.flexibleHeight);
        protected override void Write(Vector2 value)
        {
            Target.flexibleWidth = value.x;
            Target.flexibleHeight = value.y;
        }
    }
}