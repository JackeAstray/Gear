using System.Collections.Generic;
using UnityEngine;
using Evo.Tween.Editor;

namespace Evo.Tween.Modules.Transforms.Editor
{
    public sealed class TransformTweenRecording : ITweenRecordingProvider
    {
        public IEnumerable<TweenRecordingBinding> GetBindings()
        {
            yield return new TweenRecordingBinding<TransformPositionModule, Transform, Vector3>("position",
                new[] { "m_LocalPosition", "m_AnchoredPosition" },
                (module, target) => module.coordinateSpace == CoordinateSpace.Local ? target.localPosition : target.position,
                (module, target, value) =>
                {
                    if (module.coordinateSpace == CoordinateSpace.Local) { target.localPosition = value; }
                    else { target.position = value; }
                }, target => target is not RectTransform);

            yield return new TweenRecordingBinding<TransformRotationModule, Transform, Quaternion>("rotation",
                new[] { "m_LocalRotation", "m_LocalEulerAnglesHint" },
                (module, target) => module.coordinateSpace == CoordinateSpace.Local ? target.localRotation : target.rotation,
                (module, target, value) =>
                {
                    if (module.coordinateSpace == CoordinateSpace.Local) { target.localRotation = value; }
                    else { target.rotation = value; }
                });

            yield return new TweenRecordingBinding<TransformScaleModule, Transform, Vector3>("scale",
                new[] { "m_LocalScale" }, (module, target) => target.localScale,
                (module, target, value) => target.localScale = value);
        }
    }
}