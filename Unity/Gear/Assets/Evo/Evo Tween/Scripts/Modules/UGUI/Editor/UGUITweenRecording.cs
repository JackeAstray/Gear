using System.Collections.Generic;
using UnityEngine;
using Evo.Tween.Editor;
using UnityEngine.UI;

namespace Evo.Tween.Modules.UGUI.Editor
{
    public sealed class UGUITweenRecording : ITweenRecordingProvider
    {
        public IEnumerable<TweenRecordingBinding> GetBindings()
        {
            yield return new TweenRecordingBinding<GraphicAlphaModule, Graphic, float>("color",
                new[] { "m_Color.a" }, (module, target) => target.color.a,
                (module, target, value) => { Color color = target.color; color.a = value; target.color = color; }, target => false);

            yield return new TweenRecordingBinding<RectTransformSlideModule, RectTransform, Vector2>("position",
                new[] { "m_AnchoredPosition", "m_LocalPosition.x", "m_LocalPosition.y" }, (module, target) => target.anchoredPosition,
                (module, target, value) => target.anchoredPosition = value, target => false,
                unavailableReason: "Slide uses relative offsets. Use Anchored Position to record absolute poses.");

            yield return new TweenRecordingBinding<RectTransformLayoutSizeModule, RectTransform, Vector2>("sizeDelta",
                new[] { "m_SizeDelta" }, (module, target) => target.sizeDelta,
                (module, target, value) => target.sizeDelta = value, target => false,
                unavailableReason: "Layout Size uses relative multipliers. Use Size Delta to record absolute sizes.");

            yield return new TweenRecordingBinding<LayoutElementPreferredSizeModule, LayoutElement, Vector2>("preferredSize",
                new[] { "m_PreferredWidth", "m_PreferredHeight" }, (module, target) => new Vector2(target.preferredWidth, target.preferredHeight),
                (module, target, value) => { target.preferredWidth = value.x; target.preferredHeight = value.y; });

            yield return new TweenRecordingBinding<LayoutElementMinimumSizeModule, LayoutElement, Vector2>("minimumSize",
                new[] { "m_MinWidth", "m_MinHeight" }, (module, target) => new Vector2(target.minWidth, target.minHeight),
                (module, target, value) => { target.minWidth = value.x; target.minHeight = value.y; });

            yield return new TweenRecordingBinding<LayoutElementFlexibleSizeModule, LayoutElement, Vector2>("flexibleSize",
                new[] { "m_FlexibleWidth", "m_FlexibleHeight" }, (module, target) => new Vector2(target.flexibleWidth, target.flexibleHeight),
                (module, target, value) => { target.flexibleWidth = value.x; target.flexibleHeight = value.y; });

            yield return new TweenRecordingBinding<RectTransformPositionModule, RectTransform, Vector2>("position",
                new[] { "m_AnchoredPosition", "m_LocalPosition.x", "m_LocalPosition.y" }, (module, target) => target.anchoredPosition,
                (module, target, value) => target.anchoredPosition = value);

            yield return new TweenRecordingBinding<RectTransformSizeModule, RectTransform, Vector2>("sizeDelta",
                new[] { "m_SizeDelta" }, (module, target) => target.sizeDelta,
                (module, target, value) => target.sizeDelta = value);

            yield return new TweenRecordingBinding<RectTransformPivotModule, RectTransform, Vector2>("pivot",
                new[] { "m_Pivot" }, (module, target) => target.pivot,
                (module, target, value) => target.pivot = value);

            yield return new TweenRecordingBinding<RectTransformAnchorMinModule, RectTransform, Vector2>("anchorMin",
                new[] { "m_AnchorMin" }, (module, target) => target.anchorMin,
                (module, target, value) => target.anchorMin = value);

            yield return new TweenRecordingBinding<RectTransformAnchorMaxModule, RectTransform, Vector2>("anchorMax",
                new[] { "m_AnchorMax" }, (module, target) => target.anchorMax,
                (module, target, value) => target.anchorMax = value);

            yield return new TweenRecordingBinding<CanvasGroupAlphaModule, CanvasGroup, float>("alpha",
                new[] { "m_Alpha" }, (module, target) => target.alpha,
                (module, target, value) => target.alpha = value);

            yield return new TweenRecordingBinding<GraphicColorModule, Graphic, Color>("color",
                new[] { "m_Color" }, (module, target) => target.color,
                (module, target, value) => target.color = value);

            yield return new TweenRecordingBinding<ImageFillAmountModule, Image, float>("fillAmount",
                new[] { "m_FillAmount" }, (module, target) => target.fillAmount,
                (module, target, value) => target.fillAmount = value);

            yield return new TweenRecordingBinding<SliderValueModule, Slider, float>("value",
                new[] { "m_Value" }, (module, target) => target.value,
                (module, target, value) => target.SetValueWithoutNotify(value));

            yield return new TweenRecordingBinding<ScrollbarValueModule, Scrollbar, float>("value",
                new[] { "m_Value" }, (module, target) => target.value,
                (module, target, value) => target.SetValueWithoutNotify(value));
        }
    }
}