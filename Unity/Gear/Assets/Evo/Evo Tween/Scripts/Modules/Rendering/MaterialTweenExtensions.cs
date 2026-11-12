using System;
using UnityEngine;
using Evo.Tween.Modules;
using Evo.Tween.Modules.Rendering;

namespace Evo.Tween
{
    /// <summary>
    /// Fluent bindings for renderer shader properties using non-instantiating material property blocks.
    /// </summary>
    public static class MaterialTweenExtensions
    {
        readonly struct PropertyConfiguration<TValue, TModule> : ITweenModuleConfiguration<TModule>
            where TValue : struct
            where TModule : MaterialPropertyModule<TValue>
        {
            readonly string propertyName;
            readonly int materialIndex;

            public PropertyConfiguration(string propertyName, int materialIndex)
            {
                this.propertyName = propertyName;
                this.materialIndex = materialIndex;
            }

            public void Apply(TModule module)
            {
                module.propertyName = propertyName;
                module.materialIndex = materialIndex;
            }
        }

        public static TweenHandle BindToMaterialFloat(this TweenBuilder<float> tween, Renderer target,
            string propertyName, int materialIndex = 0)
        {
            ValidateProperty(propertyName, materialIndex);
            return Bind<float, MaterialFloatModule>(tween, target, propertyName, materialIndex);
        }

        public static TweenHandle BindToMaterialColor(this TweenBuilder<Color> tween, Renderer target,
            string propertyName, int materialIndex = 0)
        {
            ValidateProperty(propertyName, materialIndex);
            return Bind<Color, MaterialColorModule>(tween, target, propertyName, materialIndex);
        }

        static TweenHandle Bind<TValue, TModule>(in TweenBuilder<TValue> tween, Renderer target, string propertyName, int materialIndex)
            where TValue : struct
            where TModule : MaterialPropertyModule<TValue>, IValueModule<TValue>, new()
        {
            PropertyConfiguration<TValue, TModule> configuration = new(propertyName, materialIndex);
            return tween.Bind<TModule, PropertyConfiguration<TValue, TModule>>(target, configuration);
        }

        static void ValidateProperty(string propertyName, int materialIndex)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
                throw new ArgumentException("A shader property name is required.", nameof(propertyName));
            if (materialIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(materialIndex));
        }
    }
}