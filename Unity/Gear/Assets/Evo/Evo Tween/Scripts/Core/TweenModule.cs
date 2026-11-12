using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace Evo.Tween
{
	/// <summary>
	/// Type-specific values, options, and evaluation behavior for a <see cref="TweenClip"/>.
	/// Derive from this class and add <see cref="ModuleMenuAttribute"/> to expose a custom type.
	/// </summary>
	[Serializable, NoAutoStaticsCleanup]
	public abstract class TweenModule
	{
		static readonly Dictionary<Type, FieldInfo[]> SerializedFieldCache = new();
        static readonly Dictionary<Type, RuntimePool> RuntimePools = new();

        [NonSerialized] Component capturedTarget;
        [NonSerialized] bool hasRuntimeState;
        [NonSerialized] RuntimePool runtimePool;
        [NonSerialized] TweenModule nextPooled;

        sealed class RuntimePool
        {
            public TweenModule Head;
            public bool CanReuse;
        }

		/// <summary>
		/// Component type accepted by this module's clip target.
		/// </summary>
		public abstract Type TargetType { get; }

        /// <summary>
        /// Runtime key used to prevent two code-driven tweens from writing the same target property.
        /// Override when multiple module types write the same underlying property.
        /// </summary>
        protected internal virtual long PropertyKey => GetType().TypeHandle.Value.ToInt64();

        /// <summary>
        /// Whether the start value is known before the clip begins, so Hold Before Start can apply it early.
        /// Return false while the start value is read from the target when the clip begins (From Current).
        /// Modules derived from ValueModule already implement this; other custom modules opt in by overriding it.
        /// </summary>
        protected internal virtual bool CanHoldBeforeStart => false;

        /// <summary>
        /// Current uneased progress through the clip duration, from zero to one.
        /// Use this for behavior that must be independent of the selected ease.
        /// </summary>
        protected float NormalizedTime { get; private set; }

        /// <summary>
        /// Active clip duration in seconds.
        /// </summary>
        protected float Duration { get; private set; }

        /// <summary>
        /// Component captured for the active playback or preview run.
        /// </summary>
        protected Component CapturedTarget => capturedTarget;
        internal bool HasTarget => capturedTarget != null;

        // Takes this module's PropertyKey from the caller, since computing it can cost a shader property lookup
        internal bool WritesSameProperty(TweenModule other, long propertyKey)
        {
            return other != null && ReferenceEquals(capturedTarget, other.capturedTarget) && other.PropertyKey == propertyKey;
        }

        // Custom Clone overrides retain their existing ownership and deep-copy behavior
        internal TweenModule RentRuntime()
        {
            Type type = GetType();
            if (!RuntimePools.TryGetValue(type, out RuntimePool pool))
            {
                TweenModule first = Clone();
                pool = new RuntimePool
                {
                    CanReuse = type.GetMethod(nameof(Clone), Type.EmptyTypes).DeclaringType == typeof(TweenModule) && TryCopyTo(first)
                };
                RuntimePools.Add(type, pool);
                first.runtimePool = pool.CanReuse ? pool : null;
                return first;
            }

            if (!pool.CanReuse)
                return Clone();

            TweenModule copy = pool.Head;
            if (copy == null) { copy = Clone(); }
            else
            {
                pool.Head = copy.nextPooled;
                copy.nextPooled = null;
            }

            copy.runtimePool = pool;
            TryCopyTo(copy);
            return copy;
        }

        internal void ReturnRuntime()
        {
            RuntimePool pool = runtimePool;
            runtimePool = null;

            try { Release(); }
            finally
            {
                if (pool != null)
                {
                    ResetPooledConfiguration();
                    nextPooled = pool.Head;
                    pool.Head = this;
                }
            }
        }

        /// <summary>
        /// Copies supported built-in configurations without reflection or boxing.
        /// Unknown custom fields fall back to Clone so their values are never silently omitted.
        /// </summary>
        protected virtual bool TryCopyTo(TweenModule destination) => false;

        protected bool HasSerializedFieldCount(int count) => GetSerializedFields(GetType()).Length == count;

        /// <summary>
        /// Creates an independent runtime copy of this serialized configuration.
        /// Non-serialized playback state is deliberately not copied. Override this method when a
        /// custom module contains mutable serialized reference types that require a deep copy.
        /// </summary>
        public virtual TweenModule Clone()
		{
			Type moduleType = GetType();
			if (Activator.CreateInstance(moduleType) is not TweenModule clone)
				throw new InvalidOperationException($"Could not create tween module '{moduleType.FullName}'. " +
					$"Modules require a parameterless constructor.");

			FieldInfo[] fields = GetSerializedFields(moduleType);
			for (int i = 0; i < fields.Length; i++)
				fields[i].SetValue(clone, fields[i].GetValue(this));

			return clone;
		}

		internal bool Capture(Component explicitTarget, GameObject owner)
        {
            Release();

            if (explicitTarget != null)
                capturedTarget = AcceptsTarget(explicitTarget) ? explicitTarget : null;
            else if (owner != null)
                capturedTarget = owner.GetComponent(TargetType);

            if (capturedTarget == null)
                return false;

            hasRuntimeState = true;
            if (OnCapture())
                return true;

            Release();
            return false;
        }

        /// <summary>
        /// Whether a component is an instance of <see cref="TargetType"/>. Typed modules replace the reflection check.
        /// </summary>
        internal virtual bool AcceptsTarget(Component target) => TargetType.IsInstanceOfType(target);

		internal void Start(float duration)
		{
			Duration = TweenMath.SanitizeNonNegative(duration);
			OnStart();
		}

		internal void Evaluate(float easedTime, float normalizedTime)
		{
			NormalizedTime = Mathf.Clamp01(normalizedTime);
			OnEvaluate(easedTime);
		}

		internal void Restore()
		{
            if (!hasRuntimeState)
                return;

            try
            {
                if (capturedTarget != null)
                    OnRestore();
            }
            finally { Release(); }
        }

        internal void RestoreAfterFailure(UnityEngine.Object context)
        {
            try { Restore(); }
            catch (Exception exception) { Debug.LogException(exception, context); }
        }

        internal void Release()
        {
            if (!hasRuntimeState)
            {
                capturedTarget = null;
                NormalizedTime = 0f;
                Duration = 0f;
                return;
            }

            // Clear the guard first so cleanup remains exactly-once even if custom code is re-entrant.
            hasRuntimeState = false;
            try { OnRelease(); }
            finally
            {
                capturedTarget = null;
                NormalizedTime = 0f;
                Duration = 0f;
            }
        }

        /// <summary>
        /// Clears managed configuration references before a reusable runtime module returns to its pool.
        /// Authoring copies never call this hook.
        /// </summary>
        internal virtual void ResetPooledConfiguration() { }

		/// <summary>
		/// Captures the target and its restorable value. Return false when evaluation is unavailable.
		/// </summary>
		protected abstract bool OnCapture();

		/// <summary>
		/// Captures values that must be read when this clip actually begins.
		/// </summary>
		protected virtual void OnStart() { }

		/// <summary>
		/// Applies the module at an eased value that normally ranges from zero to one.
		/// </summary>
		protected abstract void OnEvaluate(float easedTime);

		/// <summary>
		/// Restores state captured in <see cref="OnCapture"/>.
		/// </summary>
		protected abstract void OnRestore();

        /// <summary>
        /// Clears runtime-only state without restoring the animated value.
        /// </summary>
        protected virtual void OnRelease() { }

		static FieldInfo[] GetSerializedFields(Type moduleType)
		{
			if (SerializedFieldCache.TryGetValue(moduleType, out FieldInfo[] fields))
				return fields;

			List<FieldInfo> serializedFields = new();
			Stack<Type> hierarchy = new();

			for (Type type = moduleType; type != null && type != typeof(TweenModule); type = type.BaseType)
				hierarchy.Push(type);

			while (hierarchy.Count > 0)
			{
				FieldInfo[] declaredFields = hierarchy.Pop().GetFields(
					BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

				for (int i = 0; i < declaredFields.Length; i++)
				{
					FieldInfo field = declaredFields[i];
					if (IsSerializedField(field))
						serializedFields.Add(field);
				}
			}

			fields = serializedFields.ToArray();
			SerializedFieldCache.Add(moduleType, fields);
			return fields;
		}

		static bool IsSerializedField(FieldInfo field)
		{
			if (field.IsStatic || field.IsInitOnly || field.IsNotSerialized)
				return false;

			return field.IsPublic || field.IsDefined(typeof(SerializeField), true) || field.IsDefined(typeof(SerializeReference), true);
		}
	}
}