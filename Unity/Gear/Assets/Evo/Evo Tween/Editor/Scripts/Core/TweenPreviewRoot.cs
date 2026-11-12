using System;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace Evo.Tween.Editor
{
    /// <summary>
    /// Points editor previews and the Tween Timeline at the GameObject a component's sequences play on at runtime.
    /// Components without a registered resolver play on their own GameObject.
    /// </summary>
    [NoAutoStaticsCleanup]
    public static class TweenPreviewRoot
    {
        static readonly Dictionary<Type, Func<Component, GameObject>> resolvers = new();

        /// <summary>
        /// Registers where previews of <typeparamref name="T"/> sequences play. Return null when there is nothing to preview on.
        /// Empty clip targets of redirected owners stay empty, so they keep following the runtime GameObject.
        /// </summary>
        public static void Register<T>(Func<T, GameObject> resolver) where T : Component
        {
            if (resolver == null)
                throw new ArgumentNullException(nameof(resolver));

            resolvers[typeof(T)] = component => resolver((T)component);
        }

        /// <summary>
        /// True when the owner's sequences play on a GameObject other than its own.
        /// </summary>
        public static bool IsRedirected(Component owner) => owner != null && TryGetResolver(owner.GetType(), out _);

        /// <summary>
        /// Returns the GameObject that empty clip targets of the owner's sequences resolve against.
        /// </summary>
        public static GameObject Resolve(Component owner)
        {
            if (owner == null)
                return null;

            return TryGetResolver(owner.GetType(), out Func<Component, GameObject> resolver) ? resolver(owner) : owner.gameObject;
        }

        static bool TryGetResolver(Type type, out Func<Component, GameObject> resolver)
        {
            for (; type != null && type != typeof(Component); type = type.BaseType)
            {
                if (resolvers.TryGetValue(type, out resolver))
                    return true;
            }

            resolver = null;
            return false;
        }
    }
}