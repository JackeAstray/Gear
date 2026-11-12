using System;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace Evo.Tween
{
    /// <summary>
    /// Shared storage that retains the peak number of concurrent tween instances.
    /// </summary>
    internal sealed class TweenObjectPool<T> where T : TweenInstance, new()
    {
        T[] pool;
        int count;
        int createdCount;

        public TweenObjectPool(int initialCapacity)
        {
            pool = new T[Mathf.Max(1, initialCapacity)];
            TweenPoolDiagnostics.RegisterPool(pool.Length);
        }

        public T Get()
        {
            if (count > 0)
            {
                TweenPoolDiagnostics.RecordRent(true);
                T reused = pool[--count];
                pool[count] = null;
                reused.OnRented();
                return reused;
            }

            TweenPoolDiagnostics.RecordRent(false);

            // Reserve space when growing, so completion and cancellation never resize storage
            if (createdCount == pool.Length)
            {
                int capacity = pool.Length;
                Array.Resize(ref pool, capacity * 2);
                TweenPoolDiagnostics.RegisterPool(pool.Length - capacity);
            }

            T created = new();
            createdCount++;
            created.OnRented();
            return created;
        }

        public void Release(T item)
        {
            pool[count++] = item;
            TweenPoolDiagnostics.RecordReturn(true);
        }
    }

    /// <summary>
    /// Reusable instance pool for setter-based value tweens.
    /// </summary>
    [NoAutoStaticsCleanup]
    internal static class TweenPool<T>
    {
        const int InitialPoolCapacity = 128;
        static readonly TweenObjectPool<TweenValue<T>> Pool = new(InitialPoolCapacity);

        public static TweenValue<T> Get() => Pool.Get();
        public static void Release(TweenValue<T> tween) => Pool.Release(tween);
    }

    /// <summary>
    /// Reusable instance pool for target-property tweens.
    /// </summary>
    [NoAutoStaticsCleanup]
    internal static class TweenTargetPool<TTarget, TValue> where TTarget : UnityEngine.Object
    {
        const int InitialPoolCapacity = 128;
        static readonly TweenObjectPool<TweenTargetValue<TTarget, TValue>> Pool = new(InitialPoolCapacity);

        public static TweenTargetValue<TTarget, TValue> Get() => Pool.Get();
        public static void Release(TweenTargetValue<TTarget, TValue> tween) => Pool.Release(tween);
    }

    /// <summary>
    /// Aggregates allocation, reuse, return, and overflow counters across initialized tween pools.
    /// </summary>
    [NoAutoStaticsCleanup]
    internal static class TweenPoolDiagnostics
    {
        public static int PooledCount { get; private set; }
        public static int PooledCapacity { get; private set; }
        public static int CreatedCount { get; private set; }
        public static int ReusedCount { get; private set; }
        public static int ReturnedCount { get; private set; }
        public static int DiscardedCount { get; private set; }

        public static void RegisterPool(int capacity) => PooledCapacity += Mathf.Max(0, capacity);

        public static void RecordRent(bool reused)
        {
            if (reused)
            {
                PooledCount = Mathf.Max(0, PooledCount - 1);
                ReusedCount++;
            }
            else
            {
                CreatedCount++;
            }
        }

        public static void RecordReturn(bool retained)
        {
            if (retained)
            {
                PooledCount++;
                ReturnedCount++;
            }
            else
            {
                DiscardedCount++;
            }
        }
    }
}