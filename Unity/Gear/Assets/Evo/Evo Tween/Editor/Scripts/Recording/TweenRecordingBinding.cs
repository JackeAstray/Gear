using System;
using System.Collections.Generic;
using UnityEngine;
using Evo.Tween.Modules;

namespace Evo.Tween.Editor
{
    /// <summary>
    /// Implement in a module package's editor assembly. Providers are discovered once per domain reload.
    /// Bindings own property paths and value conversion; the timeline has no knowledge of module types.
    /// </summary>
    public interface ITweenRecordingProvider
    {
        IEnumerable<TweenRecordingBinding> GetBindings();
    }

    public abstract class TweenRecordingBinding
    {
        public abstract Type ModuleType { get; }
        public abstract Type TargetType { get; }
        public abstract string Channel { get; }
        public virtual int Priority => 0;
        public virtual string UnavailableReason => null;
        public virtual bool CanCreate(Component target) => true;
        public abstract bool Matches(string propertyPath);
        public abstract TweenModule CreateModule();
        public abstract object Read(TweenModule module, Component target);
        public abstract void Restore(TweenModule module, Component target, object value);
        public abstract void SetValues(TweenModule module, object from, object to);
    }

    /// <summary>
    /// Typed adapter for absolute-value modules. Supply module-aware readers for coordinate-space options.
    /// Relative or compound modules can implement TweenRecordingBinding directly.
    /// </summary>
    public sealed class TweenRecordingBinding<TModule, TTarget, TValue> : TweenRecordingBinding
        where TModule : TweenModule, IValueModule<TValue>, new()
        where TTarget : Component where TValue : struct
    {
        readonly string channel;
        readonly string[] paths;
        readonly Func<TModule, TTarget, TValue> read;
        readonly Action<TModule, TTarget, TValue> restore;
        readonly Func<TTarget, bool> canCreate;
        readonly int priority;
        readonly string unavailableReason;

        public override Type ModuleType => typeof(TModule);
        public override Type TargetType => typeof(TTarget);
        public override string Channel => channel;
        public override int Priority => priority;
        public override string UnavailableReason => unavailableReason;

        public TweenRecordingBinding(string channel, string[] paths, Func<TModule, TTarget, TValue> read,
            Action<TModule, TTarget, TValue> restore, Func<TTarget, bool> canCreate = null, int priority = 0, string unavailableReason = null)
        {
            this.channel = channel;
            this.paths = paths;
            this.read = read;
            this.restore = restore;
            this.canCreate = canCreate;
            this.priority = priority;
            this.unavailableReason = unavailableReason;
        }

        public override bool CanCreate(Component target) => canCreate?.Invoke((TTarget)target) ?? true;
        public override TweenModule CreateModule() => new TModule();
        public override object Read(TweenModule module, Component target) => read((TModule)module, (TTarget)target);
        public override void Restore(TweenModule module, Component target, object value) => restore((TModule)module, (TTarget)target, (TValue)value);
        public override void SetValues(TweenModule module, object from, object to) => ((TModule)module).SetValues((TValue)from, (TValue)to);

        public override bool Matches(string propertyPath)
        {
            for (int i = 0; i < paths.Length; i++)
            {
                if (propertyPath == paths[i] || propertyPath.StartsWith(paths[i] + ".", StringComparison.Ordinal))
                    return true;
            }
            return false;
        }
    }
}