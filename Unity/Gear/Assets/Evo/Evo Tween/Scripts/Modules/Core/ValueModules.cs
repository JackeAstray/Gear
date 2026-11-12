using System;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace Evo.Tween.Modules
{
    /// <summary>
    /// Exposes inspector bounds for the serialized From and Target values of a module.
    /// Implementations also enforce the same bounds during evaluation.
    /// </summary>
    public interface IModuleValueBounds
    {
        bool HasMinimum { get; }
        float Minimum { get; }
        bool HasMaximum { get; }
        float Maximum { get; }
    }

    /// <summary>
    /// Configures a module from an explicit value tween. Implemented by modules that can be
    /// used both by serialized clips and fluent code bindings.
    /// </summary>
    public interface IValueModule<TValue> where TValue : struct
    {
        void SetValues(TValue from, TValue to);
    }

    /// <summary>
    /// Shared target, capture, evaluation, and restoration lifecycle for single-value modules.
    /// The closed generic hierarchy is supported by Unity's managed-reference serializer.
    /// </summary>
    [Serializable]
    public abstract class ValueModule<TTarget, TValue> : TweenModule, IValueModule<TValue>
        where TTarget : Component where TValue : struct
    {
        [NonSerialized] TValue originalValue;
        [NonSerialized] TValue startValue;

        public sealed override Type TargetType => typeof(TTarget);
        protected TTarget Target => CapturedTarget as TTarget;
        internal sealed override bool AcceptsTarget(Component target) => target is TTarget;
        protected internal override bool CanHoldBeforeStart => !UseCurrentValue;

        protected abstract bool UseCurrentValue { get; }
        protected abstract TValue FromValue { get; }
        protected abstract TValue ToValue { get; }
        protected abstract TValue Read();
        protected abstract void Write(TValue value);
        protected abstract TValue Interpolate(TValue from, TValue to, float time);
        public abstract void SetValues(TValue from, TValue to);
        protected virtual TValue CaptureOriginalValue() => Read();
        protected virtual void RestoreOriginalValue(TValue value) => Write(value);
        protected virtual void OnValueStart() { }

        protected sealed override bool OnCapture()
        {
            originalValue = CaptureOriginalValue();
            return true;
        }

        protected sealed override void OnStart()
        {
            if (Target == null)
                return;

            startValue = UseCurrentValue ? Read() : FromValue;
            OnValueStart();
        }

        protected sealed override void OnEvaluate(float easedTime)
        {
            if (Target != null)
                Write(Interpolate(startValue, ToValue, easedTime));
        }

        protected sealed override void OnRestore()
        {
            if (Target != null)
                RestoreOriginalValue(originalValue);
        }

        protected override void OnRelease()
        {
            originalValue = default;
            startValue = default;
        }
    }

    /// <summary>
    /// Shared serialized values and interpolation for Vector3-property modules.
    /// </summary>
    [Serializable]
    public abstract class Vector3Module<TTarget> : ValueModule<TTarget, Vector3> where TTarget : Component
    {
        [ModuleField(ModuleFieldGroup.Values, ModuleFieldRole.FromCurrent)]
        [Tooltip("Capture the start value when this clip begins.")]
        public bool fromCurrent = true;

        [ModuleField(ModuleFieldGroup.Values, ModuleFieldRole.From)]
        public Vector3 from = Vector3.zero;

        [ModuleField(ModuleFieldGroup.Values, ModuleFieldRole.To, Label = "Target")]
        public Vector3 to = Vector3.zero;

        protected sealed override bool UseCurrentValue => fromCurrent;
        protected sealed override Vector3 FromValue => from;
        protected sealed override Vector3 ToValue => to;

        protected sealed override Vector3 Interpolate(Vector3 start, Vector3 end, float time)
        {
            return Vector3.LerpUnclamped(start, end, time);
        }

        public sealed override void SetValues(Vector3 start, Vector3 end)
        {
            fromCurrent = false;
            from = start;
            to = end;
        }

        protected override bool TryCopyTo(TweenModule destination)
        {
            if (!HasSerializedFieldCount(3))
                return false;

            Vector3Module<TTarget> copy = (Vector3Module<TTarget>)destination;
            copy.fromCurrent = fromCurrent;
            copy.from = from;
            copy.to = to;
            return true;
        }
    }

    /// <summary>
    /// Shared serialized values and interpolation for Vector2-property modules.
    /// </summary>
    [Serializable]
    public abstract class Vector2Module<TTarget> : ValueModule<TTarget, Vector2> where TTarget : Component
    {
        [ModuleField(ModuleFieldGroup.Values, ModuleFieldRole.FromCurrent)]
        [Tooltip("Capture the start value when this clip begins.")]
        public bool fromCurrent = true;

        [ModuleField(ModuleFieldGroup.Values, ModuleFieldRole.From)]
        public Vector2 from = Vector2.zero;

        [ModuleField(ModuleFieldGroup.Values, ModuleFieldRole.To, Label = "Target")]
        public Vector2 to = Vector2.zero;

        protected sealed override bool UseCurrentValue => fromCurrent;
        protected sealed override Vector2 FromValue => from;
        protected sealed override Vector2 ToValue => to;

        protected sealed override Vector2 Interpolate(Vector2 start, Vector2 end, float time)
        {
            return Vector2.LerpUnclamped(start, end, time);
        }

        public sealed override void SetValues(Vector2 start, Vector2 end)
        {
            fromCurrent = false;
            from = start;
            to = end;
        }

        protected override bool TryCopyTo(TweenModule destination)
        {
            if (!HasSerializedFieldCount(3))
                return false;

            Vector2Module<TTarget> copy = (Vector2Module<TTarget>)destination;
            copy.fromCurrent = fromCurrent;
            copy.from = from;
            copy.to = to;
            return true;
        }
    }

    /// <summary>
    /// Shared serialized values and interpolation for float-property modules.
    /// </summary>
    [Serializable]
    public abstract class FloatModule<TTarget> : ValueModule<TTarget, float> where TTarget : Component
    {
        [ModuleField(ModuleFieldGroup.Values, ModuleFieldRole.FromCurrent)]
        [Tooltip("Capture the start value when this clip begins.")]
        public bool fromCurrent = true;

        [ModuleField(ModuleFieldGroup.Values, ModuleFieldRole.From)]
        public float from;

        [ModuleField(ModuleFieldGroup.Values, ModuleFieldRole.To, Label = "Target")]
        public float to = 1f;

        protected sealed override bool UseCurrentValue => fromCurrent;
        protected sealed override float FromValue => from;
        protected sealed override float ToValue => to;
        
        protected virtual float Constrain(float value) => value;

        protected sealed override float Interpolate(float start, float end, float time)
        {
            return Constrain(Mathf.LerpUnclamped(start, end, time));
        }

        public sealed override void SetValues(float start, float end)
        {
            fromCurrent = false;
            from = Constrain(start);
            to = Constrain(end);
        }

        protected override bool TryCopyTo(TweenModule destination)
        {
            if (!HasSerializedFieldCount(3))
                return false;

            FloatModule<TTarget> copy = (FloatModule<TTarget>)destination;
            copy.fromCurrent = fromCurrent;
            copy.from = from;
            copy.to = to;
            return true;
        }
    }

    /// <summary>
    /// Shared float interpolation with runtime and inspector bounds supplied by the concrete module.
    /// An infinite bound leaves that side unconstrained.
    /// </summary>
    [Serializable]
    public abstract class ConstrainedFloatModule<TTarget> : FloatModule<TTarget>, IModuleValueBounds where TTarget : Component
    {
        protected virtual float MinimumValue => float.NegativeInfinity;
        protected virtual float MaximumValue => float.PositiveInfinity;

        bool IModuleValueBounds.HasMinimum => !float.IsNegativeInfinity(MinimumValue);
        float IModuleValueBounds.Minimum => MinimumValue;
        bool IModuleValueBounds.HasMaximum => !float.IsPositiveInfinity(MaximumValue);
        float IModuleValueBounds.Maximum => MaximumValue;

        protected sealed override float Constrain(float value) => Mathf.Clamp(value, MinimumValue, MaximumValue);
    }

    /// <summary>
    /// Shared zero-to-one float behavior for normalized component properties.
    /// </summary>
    [Serializable]
    public abstract class NormalizedFloatModule<TTarget> : ConstrainedFloatModule<TTarget> where TTarget : Component
    {
        protected sealed override float MinimumValue => 0f;
        protected sealed override float MaximumValue => 1f;
    }

    /// <summary>
    /// Shared non-negative float behavior for component properties with no upper bound.
    /// </summary>
    [Serializable]
    public abstract class NonNegativeFloatModule<TTarget> : ConstrainedFloatModule<TTarget> where TTarget : Component
    {
        protected sealed override float MinimumValue => 0f;
    }

    /// <summary>
    /// Shared serialized values and interpolation for integer-property modules.
    /// </summary>
    [Serializable]
    public abstract class IntModule<TTarget> : ValueModule<TTarget, int> where TTarget : Component
    {
        [ModuleField(ModuleFieldGroup.Values, ModuleFieldRole.FromCurrent)]
        [Tooltip("Capture the start value when this clip begins.")]
        public bool fromCurrent = true;

        [ModuleField(ModuleFieldGroup.Values, ModuleFieldRole.From)]
        public int from;

        [ModuleField(ModuleFieldGroup.Values, ModuleFieldRole.To, Label = "Target")]
        public int to = 1;

        protected sealed override bool UseCurrentValue => fromCurrent;
        protected sealed override int FromValue => from;
        protected sealed override int ToValue => to;

        protected virtual int Constrain(int value) => value;
       
        protected sealed override int Interpolate(int start, int end, float time)
        {
            return Constrain(Mathf.RoundToInt(Mathf.LerpUnclamped(start, end, time)));
        }

        public sealed override void SetValues(int start, int end)
        {
            fromCurrent = false;
            from = Constrain(start);
            to = Constrain(end);
        }

        protected override bool TryCopyTo(TweenModule destination)
        {
            if (!HasSerializedFieldCount(3))
                return false;

            IntModule<TTarget> copy = (IntModule<TTarget>)destination;
            copy.fromCurrent = fromCurrent;
            copy.from = from;
            copy.to = to;
            return true;
        }
    }

    /// <summary>
    /// Shared integer interpolation with runtime and inspector bounds supplied by the concrete module.
    /// </summary>
    [Serializable]
    public abstract class ConstrainedIntModule<TTarget> : IntModule<TTarget>, IModuleValueBounds where TTarget : Component
    {
        protected virtual int MinimumValue => int.MinValue;
        protected virtual int MaximumValue => int.MaxValue;

        bool IModuleValueBounds.HasMinimum => MinimumValue != int.MinValue;
        float IModuleValueBounds.Minimum => MinimumValue;
        bool IModuleValueBounds.HasMaximum => MaximumValue != int.MaxValue;
        float IModuleValueBounds.Maximum => MaximumValue;

        protected sealed override int Constrain(int value) => Mathf.Clamp(value, MinimumValue, MaximumValue);
    }

    /// <summary>
    /// Shared non-negative integer behavior for component properties with no upper bound.
    /// </summary>
    [Serializable]
    public abstract class NonNegativeIntModule<TTarget> : ConstrainedIntModule<TTarget> where TTarget : Component
    {
        protected sealed override int MinimumValue => 0;
    }

    /// <summary>
    /// Shared serialized values and discrete interpolation for bool-property modules.
    /// </summary>
    [Serializable]
    public abstract class BoolModule<TTarget> : ValueModule<TTarget, bool> where TTarget : Component
    {
        [ModuleField(ModuleFieldGroup.Values, ModuleFieldRole.FromCurrent)]
        [Tooltip("Capture the start value when this clip begins.")]
        public bool fromCurrent = true;

        [ModuleField(ModuleFieldGroup.Values, ModuleFieldRole.From)]
        public bool from;

        [ModuleField(ModuleFieldGroup.Values, ModuleFieldRole.To, Label = "Target")]
        public bool to = true;

        protected sealed override bool UseCurrentValue => fromCurrent;
        protected sealed override bool FromValue => from;
        protected sealed override bool ToValue => to;
        protected sealed override bool Interpolate(bool start, bool end, float time) => time < 0.5f ? start : end;

        public sealed override void SetValues(bool start, bool end)
        {
            fromCurrent = false;
            from = start;
            to = end;
        }

        protected override bool TryCopyTo(TweenModule destination)
        {
            if (!HasSerializedFieldCount(3))
                return false;

            BoolModule<TTarget> copy = (BoolModule<TTarget>)destination;
            copy.fromCurrent = fromCurrent;
            copy.from = from;
            copy.to = to;
            return true;
        }
    }

    /// <summary>
    /// Shared serialized values and interpolation for color-property modules.
    /// </summary>
    [Serializable]
    public abstract class ColorModule<TTarget> : ValueModule<TTarget, Color> where TTarget : Component
    {
        [ModuleField(ModuleFieldGroup.Values, ModuleFieldRole.FromCurrent)]
        [Tooltip("Capture the start color when this clip begins.")]
        public bool fromCurrent = true;

        [ModuleField(ModuleFieldGroup.Values, ModuleFieldRole.From)]
        public Color from = Color.white;

        [ModuleField(ModuleFieldGroup.Values, ModuleFieldRole.To, Label = "Target")]
        public Color to = Color.white;

        protected sealed override bool UseCurrentValue => fromCurrent;
        protected sealed override Color FromValue => from;
        protected sealed override Color ToValue => to;
        protected sealed override Color Interpolate(Color start, Color end, float time) => Color.LerpUnclamped(start, end, time);

        public sealed override void SetValues(Color start, Color end)
        {
            fromCurrent = false;
            from = start;
            to = end;
        }

        protected override bool TryCopyTo(TweenModule destination)
        {
            if (!HasSerializedFieldCount(3))
                return false;

            ColorModule<TTarget> copy = (ColorModule<TTarget>)destination;
            copy.fromCurrent = fromCurrent;
            copy.from = from;
            copy.to = to;
            return true;
        }
    }

    /// <summary>
    /// Shared alpha-channel behavior for components that expose a Color property.
    /// </summary>
    [Serializable]
    public abstract class ColorAlphaModule<TTarget> : NormalizedFloatModule<TTarget> where TTarget : Component
    {
        protected abstract Color ReadColor();

        protected abstract void WriteColor(Color color);

        protected sealed override float Read() => ReadColor().a;

        protected sealed override void Write(float value)
        {
            Color color = ReadColor();
            color.a = value;
            WriteColor(color);
        }
    }

    /// <summary>
    /// Shared MaterialPropertyBlock lifecycle for material float and color modules.
    /// It preserves the complete original block and never instantiates a material copy.
    /// </summary>
    [Serializable]
    public abstract class MaterialPropertyModule<TValue> : TweenModule, IValueModule<TValue> where TValue : struct
    {
        [ModuleField(ModuleFieldGroup.Settings, Label = "Material Index")]
        [Min(0), Tooltip("Renderer material slot that contains the shader property.")]
        public int materialIndex;

        [ModuleField(ModuleFieldGroup.Settings, Label = "Shader Property")]
        [Tooltip("Shader property name.")]
        public string propertyName;

        [NonSerialized] Material material;
        [NonSerialized] MaterialPropertyBlock originalBlock;
        [NonSerialized] MaterialPropertyBlock workingBlock;
        [NonSerialized] int propertyId;
        [NonSerialized] int capturedMaterialIndex;
        [NonSerialized] TValue startValue;

        public sealed override Type TargetType => typeof(Renderer);
        protected Renderer Target => CapturedTarget as Renderer;
        protected internal override long PropertyKey => ((long)(uint)materialIndex << 32) |
            (uint)Shader.PropertyToID(propertyName ?? string.Empty);
        protected internal override bool CanHoldBeforeStart => !UseCurrentValue;

        protected abstract bool UseCurrentValue { get; }
        protected abstract TValue FromValue { get; }
        protected abstract TValue ToValue { get; }
        protected abstract TValue ReadMaterial(Material source, int id);
        protected abstract TValue ReadBlock(MaterialPropertyBlock block, int id);
        protected abstract void WriteBlock(MaterialPropertyBlock block, int id, TValue value);
        protected abstract TValue Interpolate(TValue from, TValue to, float time);
        public abstract void SetValues(TValue from, TValue to);

        protected sealed override bool OnCapture()
        {
            if (!MaterialUtils.TryGetSharedMaterial(Target, materialIndex, out material) || string.IsNullOrWhiteSpace(propertyName))
            {
                material = null;
                return false;
            }

            propertyId = Shader.PropertyToID(propertyName);
            if (!material.HasProperty(propertyId))
            {
                material = null;
                return false;
            }

            originalBlock ??= new MaterialPropertyBlock();
            workingBlock ??= new MaterialPropertyBlock();
            capturedMaterialIndex = materialIndex;
            Target.GetPropertyBlock(originalBlock, capturedMaterialIndex);
            return true;
        }

        protected sealed override void OnStart()
        {
            if (Target == null)
                return;

            if (!UseCurrentValue)
            {
                startValue = FromValue;
                return;
            }

            Target.GetPropertyBlock(workingBlock, capturedMaterialIndex);
            startValue = workingBlock.HasProperty(propertyId)
                ? ReadBlock(workingBlock, propertyId)
                : ReadMaterial(material, propertyId);
        }

        protected sealed override void OnEvaluate(float easedTime)
        {
            if (Target == null)
                return;

            Target.GetPropertyBlock(workingBlock, capturedMaterialIndex);
            WriteBlock(workingBlock, propertyId, Interpolate(startValue, ToValue, easedTime));
            Target.SetPropertyBlock(workingBlock, capturedMaterialIndex);
        }

        protected sealed override void OnRestore()
        {
            if (Target != null && originalBlock != null)
                Target.SetPropertyBlock(originalBlock, capturedMaterialIndex);
        }

        protected sealed override void OnRelease()
        {
            originalBlock?.Clear();
            workingBlock?.Clear();
            material = null;
            propertyId = 0;
            capturedMaterialIndex = 0;
        }

        internal override void ResetPooledConfiguration()
        {
            propertyName = null;
            materialIndex = 0;
        }
    }

    /// <summary>
    /// Shared renderer material-slot validation that never instantiates a material copy.
    /// </summary>
    [NoAutoStaticsCleanup]
    internal static class MaterialUtils
    {
        static readonly List<Material> SharedMaterials = new(4);

        public static bool TryGetSharedMaterial(Renderer renderer, int materialIndex, out Material material)
        {
            material = null;
            if (renderer == null || materialIndex < 0)
                return false;

            if (materialIndex == 0)
            {
                material = renderer.sharedMaterial;
                return material != null;
            }

            try
            {
                renderer.GetSharedMaterials(SharedMaterials);
                if (materialIndex < SharedMaterials.Count)
                    material = SharedMaterials[materialIndex];
            }
            finally
            {
                SharedMaterials.Clear();
            }

            return material != null;
        }
    }
}