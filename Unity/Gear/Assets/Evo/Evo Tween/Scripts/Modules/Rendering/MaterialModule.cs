using System;
using UnityEngine;

namespace Evo.Tween.Modules.Rendering
{
    /// <summary>
    /// Animates a renderer shader color without cloning its material.
    /// </summary>
    [Serializable, ModuleMenu("Rendering/Material/Color")]
    public sealed class MaterialColorModule : MaterialPropertyModule<Color>
    {
        [ModuleField(ModuleFieldGroup.Values, ModuleFieldRole.FromCurrent)]
        [Tooltip("Capture the current property value when this clip begins.")]
        public bool fromCurrent = true;

        [ModuleField(ModuleFieldGroup.Values, ModuleFieldRole.From)]
        public Color from = Color.white;

        [ModuleField(ModuleFieldGroup.Values, ModuleFieldRole.To, Label = "Target")]
        public Color to = Color.white;

        public MaterialColorModule() => propertyName = "_BaseColor";

        protected override bool UseCurrentValue => fromCurrent;
        protected override Color FromValue => from;
        protected override Color ToValue => to;
        protected override Color ReadMaterial(Material source, int id) => source.GetColor(id);
        protected override Color ReadBlock(MaterialPropertyBlock block, int id) => block.GetColor(id);
        protected override void WriteBlock(MaterialPropertyBlock block, int id, Color value) => block.SetColor(id, value);
        protected override Color Interpolate(Color start, Color end, float time) => Color.LerpUnclamped(start, end, time);

        public override void SetValues(Color start, Color end)
        {
            fromCurrent = false;
            from = start;
            to = end;
        }

        protected override bool TryCopyTo(TweenModule destination)
        {
            MaterialColorModule copy = (MaterialColorModule)destination;
            copy.fromCurrent = fromCurrent;
            copy.from = from;
            copy.to = to;
            copy.materialIndex = materialIndex;
            copy.propertyName = propertyName;
            return true;
        }
    }

    /// <summary>
    /// Animates a renderer shader float without cloning its material.
    /// </summary>
    [Serializable, ModuleMenu("Rendering/Material/Float")]
    public sealed class MaterialFloatModule : MaterialPropertyModule<float>
    {
        [ModuleField(ModuleFieldGroup.Values, ModuleFieldRole.FromCurrent)]
        [Tooltip("Capture the current property value when this clip begins.")]
        public bool fromCurrent = true;

        [ModuleField(ModuleFieldGroup.Values, ModuleFieldRole.From)]
        public float from;

        [ModuleField(ModuleFieldGroup.Values, ModuleFieldRole.To, Label = "Target")]
        public float to = 1f;

        public MaterialFloatModule() => propertyName = "_Cutoff";

        protected override bool UseCurrentValue => fromCurrent;
        protected override float FromValue => from;
        protected override float ToValue => to;
        protected override float ReadMaterial(Material source, int id) => source.GetFloat(id);
        protected override float ReadBlock(MaterialPropertyBlock block, int id) => block.GetFloat(id);
        protected override void WriteBlock(MaterialPropertyBlock block, int id, float value) => block.SetFloat(id, value);
        protected override float Interpolate(float start, float end, float time) => Mathf.LerpUnclamped(start, end, time);

        public override void SetValues(float start, float end)
        {
            fromCurrent = false;
            from = start;
            to = end;
        }

        protected override bool TryCopyTo(TweenModule destination)
        {
            MaterialFloatModule copy = (MaterialFloatModule)destination;
            copy.fromCurrent = fromCurrent;
            copy.from = from;
            copy.to = to;
            copy.materialIndex = materialIndex;
            copy.propertyName = propertyName;
            return true;
        }
    }
}