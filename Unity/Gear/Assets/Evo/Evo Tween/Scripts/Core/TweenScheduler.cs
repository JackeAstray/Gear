using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using Evo.Tween.Diagnostics;

namespace Evo.Tween
{
    /// <summary>
    /// Central player-loop scheduler shared by code tweens and TweenAnimator components.
    /// Each update phase keeps its own list, so a phase only visits the tweens it advances.
    /// </summary>
    [NoAutoStaticsCleanup]
    internal static class TweenScheduler
    {
        const int PhaseCount = 3;

        static readonly List<ITweenInstance>[] PhaseTweens = { new(32), new(8), new(8) };
        static readonly Dictionary<PropertyBinding, TweenInstance> PropertyTweens = new(32);
        static bool isQuitting;
        static bool isUpdating;
        static bool isClearing;
        static bool clearRequested;
        static int updatingPhase = -1;

        public static int ActiveCount => PhaseTweens[0].Count + PhaseTweens[1].Count + PhaseTweens[2].Count;
        public static int ActiveCapacity => PhaseTweens[0].Capacity + PhaseTweens[1].Capacity + PhaseTweens[2].Capacity;

        readonly struct PropertyBinding : IEquatable<PropertyBinding>
        {
            readonly UnityEngine.Object target;
            readonly long propertyKey;

            public PropertyBinding(UnityEngine.Object target, long propertyKey)
            {
                this.target = target;
                this.propertyKey = propertyKey;
            }

            // Unity keeps one managed wrapper per native object, so reference identity is object identity.
            // It also stays valid after the object is destroyed, when the binding still has to be removed.
            public bool Equals(PropertyBinding other) => ReferenceEquals(target, other.target) && propertyKey == other.propertyKey;
            public override bool Equals(object obj) => obj is PropertyBinding other && Equals(other);
            public override int GetHashCode() => unchecked((RuntimeHelpers.GetHashCode(target) * 397) ^ propertyKey.GetHashCode());
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            isUpdating = false;
            isClearing = false;
            clearRequested = false;
            Clear();
            PropertyTweens.Clear();
            isQuitting = false;
            updatingPhase = -1;
        }

        public static bool Add(ITweenInstance tween)
        {
            if (tween == null || isQuitting || isClearing || clearRequested || !TweenManager.EnsureExists())
                return false;

            bool isAlive;

            try { isAlive = tween.IsAlive; }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }

            if (!isAlive)
                return false;

            int phase = GetPhaseIndex(tween.UpdatePhase);
            List<ITweenInstance> list = PhaseTweens[phase];
            tween.ScheduledPhase = phase;
            tween.SchedulerIndex = list.Count;
            list.Add(tween);

            if (tween is TweenInstance instance && tween is ITweenPropertyTarget property)
                RegisterProperty(instance, property);

            return true;
        }

        public static void Update(UpdatePhase updatePhase, float deltaTime, float unscaledDeltaTime)
        {
            if (isUpdating || isClearing)
                return;

            int phase = GetPhaseIndex(updatePhase);
            List<ITweenInstance> list = PhaseTweens[phase];
            if (list.Count == 0)
            {
                FlushDeferredClear();
                return;
            }

            isUpdating = true;
            updatingPhase = phase;

            try
            {
                for (int i = list.Count - 1; i >= 0 && !clearRequested; i--)
                {
                    ITweenInstance tween = list[i];
                    bool keepAlive = false;

                    try
                    {
                        // A phase changed while this list was updating is applied when the tween is next visited
                        if (GetPhaseIndex(tween.UpdatePhase) != phase && tween.IsAlive)
                        {
                            Move(list, i, tween);
                            continue;
                        }

                        keepAlive = tween.Tick(deltaTime, unscaledDeltaTime);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception, tween as UnityEngine.Object);
                    }

                    if (!keepAlive)
                        Remove(list, i, tween);
                }
            }
            finally
            {
                isUpdating = false;
                updatingPhase = -1;

                if (clearRequested)
                    Clear();
            }
        }

        /// <summary>
        /// Moves a scheduled tween to the list of its new update phase. The list being updated is left untouched,
        /// since moving would reorder tweens it has not visited yet; its loop moves the tween instead.
        /// </summary>
        internal static void NotifyPhaseChanged(ITweenInstance tween)
        {
            int current = tween.ScheduledPhase;
            if ((uint)current >= PhaseCount || isClearing || current == updatingPhase || GetPhaseIndex(tween.UpdatePhase) == current)
                return;

            List<ITweenInstance> list = PhaseTweens[current];
            int index = tween.SchedulerIndex;
            if ((uint)index < (uint)list.Count && ReferenceEquals(list[index], tween))
                Move(list, index, tween);
        }

        public static TweenDebugInfo GetDebugInfo(int index)
        {
            for (int phase = 0; phase < PhaseCount; phase++)
            {
                List<ITweenInstance> list = PhaseTweens[phase];
                if ((uint)index >= (uint)list.Count)
                {
                    index -= list.Count;
                    continue;
                }

                ITweenInstance tween = list[index];
                if (IsDestroyed(tween))
                    return default;

                try { return tween.GetDebugInfo(); }
                catch (Exception exception)
                {
                    Debug.LogException(exception, tween as UnityEngine.Object);
                    return default;
                }
            }

            return default;
        }

        public static void NotifyManagerDestroyed() => Clear();

        internal static void FlushDeferredClear()
        {
            if (clearRequested && !isUpdating)
                Clear();
        }

        public static void NotifyApplicationQuit()
        {
            isQuitting = true;
            Clear();
        }

        static int GetPhaseIndex(UpdatePhase updatePhase) => (uint)updatePhase < PhaseCount ? (int)updatePhase : 0;

        static void RegisterProperty(TweenInstance instance, ITweenPropertyTarget property)
        {
            UnityEngine.Object target = property.Target;
            if (target is null)
                return;

            long propertyKey = property.PropertyKey;
            instance.RegisteredTarget = target;
            instance.RegisteredPropertyKey = propertyKey;

            PropertyBinding binding = new(target, propertyKey);
            PropertyTweens.TryGetValue(binding, out TweenInstance previous);
            PropertyTweens[binding] = instance;

            if (previous == null || ReferenceEquals(previous, instance))
                return;

            previous.Kill();

            // While a list is updating, the killed tween is removed when its own list next visits it
            if (!isUpdating && !previous.IsEvaluating)
            {
                int phase = previous.ScheduledPhase;
                int index = previous.SchedulerIndex;
                if ((uint)phase < PhaseCount && (uint)index < (uint)PhaseTweens[phase].Count && ReferenceEquals(PhaseTweens[phase][index], previous))
                    Remove(PhaseTweens[phase], index, previous);
            }
        }

        static void UnregisterProperty(ITweenInstance tween)
        {
            if (tween is not TweenInstance instance || instance.RegisteredTarget is null)
                return;

            PropertyBinding binding = new(instance.RegisteredTarget, instance.RegisteredPropertyKey);
            if (PropertyTweens.TryGetValue(binding, out TweenInstance registered) && ReferenceEquals(registered, instance))
                PropertyTweens.Remove(binding);

            instance.RegisteredTarget = null;
            instance.RegisteredPropertyKey = 0L;
        }

        static void Move(List<ITweenInstance> list, int index, ITweenInstance tween)
        {
            RemoveSlot(list, index);

            int phase = GetPhaseIndex(tween.UpdatePhase);
            List<ITweenInstance> destination = PhaseTweens[phase];
            tween.ScheduledPhase = phase;
            tween.SchedulerIndex = destination.Count;
            destination.Add(tween);
        }

        static void Remove(List<ITweenInstance> list, int index, ITweenInstance tween)
        {
            RemoveSlot(list, index);
            tween.SchedulerIndex = -1;
            tween.ScheduledPhase = -1;
            UnregisterProperty(tween);

            if (IsDestroyed(tween))
                return;

            try { tween.OnRemoved(); }
            catch (Exception exception) { Debug.LogException(exception, tween as UnityEngine.Object); }
        }

        // Swap-back removal. Lists are updated from the end, so the moved tween has already been visited.
        static void RemoveSlot(List<ITweenInstance> list, int index)
        {
            int lastIndex = list.Count - 1;
            if (index != lastIndex)
            {
                ITweenInstance moved = list[lastIndex];
                list[index] = moved;
                moved.SchedulerIndex = index;
            }

            list.RemoveAt(lastIndex);
        }

        static bool IsDestroyed(ITweenInstance tween) => tween is UnityEngine.Object unityObject && unityObject == null;

        static void Clear()
        {
            if (isUpdating)
            {
                clearRequested = true;
                return;
            }

            if (isClearing)
                return;

            for (int phase = 0; phase < PhaseCount; phase++)
            {
                List<ITweenInstance> list = PhaseTweens[phase];
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] is TweenInstance instance && instance.IsEvaluating)
                    {
                        clearRequested = true;
                        return;
                    }
                }
            }

            clearRequested = false;
            isClearing = true;

            try
            {
                for (int phase = 0; phase < PhaseCount; phase++)
                {
                    List<ITweenInstance> list = PhaseTweens[phase];
                    while (list.Count > 0)
                    {
                        int index = list.Count - 1;
                        ITweenInstance tween = list[index];
                        list.RemoveAt(index);
                        tween.SchedulerIndex = -1;
                        tween.ScheduledPhase = -1;
                        UnregisterProperty(tween);

                        if (IsDestroyed(tween))
                            continue;

                        try { tween.OnRemoved(); }
                        catch (Exception exception) { Debug.LogException(exception, tween as UnityEngine.Object); }
                    }
                }
            }
            finally
            {
                isClearing = false;
            }
        }
    }
}
