using System;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using Evo.Tween.Modules;

namespace Evo.Tween
{
    /// <summary>
    /// Entry point for allocation-conscious fluent tweens.
    /// </summary>
    public static class EvoTween
    {
        /// <summary>
        /// Creates a value tween description. Playback begins when the builder is bound.
        /// </summary>
        public static TweenBuilder<T> Create<T>(T from, T to, float duration) where T : struct
        {
            if (!SupportedValue<T>.IsSupported)
                throw UnsupportedValueType<T>();

            return new TweenBuilder<T>(from, to, duration);
        }

        internal static TweenHandle Bind<T>(in TweenBuilder<T> builder, Action<T> setter) where T : struct
        {
            if (setter == null)
                throw new ArgumentNullException(nameof(setter));
            if (!Application.isPlaying)
                return default;

            TweenValue<T> tween = TweenPool<T>.Get();

            try
            {
                tween.ApplySettings(builder);
                tween.Configure(builder.From, builder.To, builder.Duration, setter, SupportedValue<T>.Interpolator);
                return Schedule(tween);
            }
            catch
            {
                tween.OnRemoved();
                throw;
            }
        }

        internal static TweenHandle Bind<TTarget, TValue>(in TweenBuilder<TValue> builder, TTarget target,
            Action<TTarget, TValue> setter, int propertyKey) where TTarget : UnityEngine.Object where TValue : struct
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (setter == null)
                throw new ArgumentNullException(nameof(setter));
            if (!Application.isPlaying)
                return default;

            TweenTargetValue<TTarget, TValue> tween = TweenTargetPool<TTarget, TValue>.Get();

            try
            {
                tween.ApplySettings(builder);
                tween.Configure(target, builder.From, builder.To, builder.Duration, setter, SupportedValue<TValue>.Interpolator, propertyKey);
                return Schedule(tween);
            }
            catch
            {
                tween.OnRemoved();
                throw;
            }
        }

        internal static TweenHandle Bind<T>(in TweenBuilder<T> builder, Component target, TweenModule module) where T : struct
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (module == null)
                throw new ArgumentNullException(nameof(module));
            if (module is not IValueModule<T>)
                throw new ArgumentException($"{module.GetType().Name} cannot animate {typeof(T).Name} values.", nameof(module));
            if (!Application.isPlaying)
                return default;

            TweenModule runtimeModule = module.RentRuntime();
            TweenModuleInstance tween = TweenModuleInstance.Get();
            bool configured = false;

            try
            {
                IValueModule<T> valueModule = (IValueModule<T>)runtimeModule;
                valueModule.SetValues(builder.From, builder.To);
                configured = true;
                tween.ApplySettings(builder);

                if (!tween.Configure(target, runtimeModule, builder.Duration))
                {
                    tween.OnRemoved();
                    return default;
                }

                return Schedule(tween);
            }
            catch
            {
                if (!configured)
                    runtimeModule.ReturnRuntime();

                tween.OnRemoved();
                throw;
            }
        }

        internal static TweenHandle Bind<T, TModule, TConfiguration>(in TweenBuilder<T> builder, Component target,
            in TConfiguration configuration)
            where T : struct
            where TModule : TweenModule, IValueModule<T>, new()
            where TConfiguration : struct, ITweenModuleConfiguration<TModule>
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (!Application.isPlaying)
                return default;

            TweenModuleValueInstance<T, TModule> tween = TweenModuleValueInstance<T, TModule>.Get();

            try
            {
                tween.ApplySettings(builder);

                if (!tween.Configure(target, builder.From, builder.To, builder.Duration, configuration))
                {
                    tween.OnRemoved();
                    return default;
                }

                return Schedule(tween);
            }
            catch
            {
                tween.OnRemoved();
                throw;
            }
        }

        static TweenHandle Schedule(TweenInstance tween)
        {
            if (!TweenScheduler.Add(tween))
            {
                tween.OnRemoved();
                return default;
            }

            return new TweenHandle(tween, tween.Version);
        }

        static NotSupportedException UnsupportedValueType<T>()
        {
            return new NotSupportedException($"EvoTween.Create<{typeof(T).Name}> is not supported. " +
                "Supported value types are float, int, bool, Vector2, Vector3, Vector4, Color, and Quaternion.");
        }

        [NoAutoStaticsCleanup]
        internal static class SupportedValue<T> where T : struct
        {
            public static readonly Func<T, T, float, T> Interpolator = ResolveInterpolator();
            public static readonly bool IsSupported = Interpolator != null;

            static Func<T, T, float, T> ResolveInterpolator()
            {
                Type type = typeof(T);

                if (type == typeof(float))
                    return (Func<T, T, float, T>)(object)(Func<float, float, float, float>)Mathf.LerpUnclamped;
                if (type == typeof(int))
                    return (Func<T, T, float, T>)(object)(Func<int, int, float, int>)InterpolateInt;
                if (type == typeof(bool))
                    return InterpolateBool;
                if (type == typeof(Vector2))
                    return (Func<T, T, float, T>)(object)(Func<Vector2, Vector2, float, Vector2>)Vector2.LerpUnclamped;
                if (type == typeof(Vector3))
                    return (Func<T, T, float, T>)(object)(Func<Vector3, Vector3, float, Vector3>)Vector3.LerpUnclamped;
                if (type == typeof(Vector4))
                    return (Func<T, T, float, T>)(object)(Func<Vector4, Vector4, float, Vector4>)Vector4.LerpUnclamped;
                if (type == typeof(Color))
                    return (Func<T, T, float, T>)(object)(Func<Color, Color, float, Color>)Color.LerpUnclamped;
                if (type == typeof(Quaternion))
                    return (Func<T, T, float, T>)(object)(Func<Quaternion, Quaternion, float, Quaternion>)Quaternion.SlerpUnclamped;

                return null;
            }

            static int InterpolateInt(int from, int to, float time) => Mathf.RoundToInt(Mathf.LerpUnclamped(from, to, time));

            static T InterpolateBool(T from, T to, float time) => time < 0.5f ? from : to;
        }
    }
}