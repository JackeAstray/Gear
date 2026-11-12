#if !UNITY_6000_5_OR_NEWER
using System;

namespace Evo
{
    /// <summary>
    /// Stand-in for Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanupAttribute, which Unity 6.5+ provides.
    /// Lets Evo code use [NoAutoStaticsCleanup] without version checks; it does nothing on older versions.
    /// Code inside Evo namespaces resolves to this before its using directives, so no extra using is needed.
    /// There's intentionally no AutoStaticsCleanup stand-in: it would silently skip the reset on older versions.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Event)]
    public sealed class NoAutoStaticsCleanupAttribute : Attribute { }
}

namespace Unity.Scripting.LifecycleManagement
{
    // Gives the namespace a type so 'using Unity.Scripting.LifecycleManagement;' compiles before 6.5
    static class EvoNamespaceAnchor { }
}
#endif
