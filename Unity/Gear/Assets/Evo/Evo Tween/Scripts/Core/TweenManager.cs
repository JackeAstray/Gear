using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using Evo.Tween.Diagnostics;

namespace Evo.Tween
{
    /// <summary>
    /// Persistent runtime owner for EvoTween scheduling and diagnostics.
    /// It is created before the first scene and survives scene changes.
    /// </summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-32000), NoAutoStaticsCleanup]
    [AddComponentMenu("")]
    public sealed class TweenManager : MonoBehaviour
    {
        const string ManagerName = "[EvoTween Manager]";

        static TweenManager instance;
        static bool isQuitting;

        /// <summary>
        /// Current manager instance, or null outside Play Mode.
        /// </summary>
        public static TweenManager Instance => instance;

        /// <summary>
        /// Number of alive code and component tweens.
        /// </summary>
        public int AliveTweenCount => TweenScheduler.ActiveCount;

        /// <summary>
        /// Allocated capacity of the scheduler's active list.
        /// </summary>
        public int ActiveTweenCapacity => TweenScheduler.ActiveCapacity;

        /// <summary>
        /// Number of tween instances currently retained in all initialized pools.
        /// </summary>
        public int PooledTweenCount => TweenPoolDiagnostics.PooledCount;

        /// <summary>
        /// Combined allocated capacity of all initialized tween pools.
        /// </summary>
        public int PooledTweenCapacity => TweenPoolDiagnostics.PooledCapacity;

        /// <summary>
        /// Total tween instances allocated during the current managed-domain lifetime.
        /// </summary>
        public int CreatedTweenCount => TweenPoolDiagnostics.CreatedCount;

        /// <summary>
        /// Total tween instances rented from a pool during the current managed-domain lifetime.
        /// </summary>
        public int ReusedTweenCount => TweenPoolDiagnostics.ReusedCount;

        /// <summary>
        /// Total tween instances returned to a pool during the current managed-domain lifetime.
        /// </summary>
        public int ReturnedTweenCount => TweenPoolDiagnostics.ReturnedCount;

        /// <summary>
        /// Legacy overflow counter. Pools now retain their peak concurrency instead of discarding instances.
        /// </summary>
        public int DiscardedTweenCount => TweenPoolDiagnostics.DiscardedCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
            isQuitting = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap() => EnsureExists();

        internal static bool EnsureExists()
        {
            if (isQuitting)
                return false;

            // A live manager only exists in Play Mode, so the common path skips the native play-state query
            if (instance != null)
                return true;

            if (!Application.isPlaying)
                return false;

            TweenManager existing = Object.FindAnyObjectByType<TweenManager>(FindObjectsInactive.Include);
            if (existing != null)
            {
                instance = existing;
                instance.gameObject.SetActive(true);
                instance.enabled = true;
                instance.Initialize();
                return true;
            }

            GameObject managerObject = new(ManagerName);
            instance = managerObject.AddComponent<TweenManager>();
            return instance != null;
        }

        /// <summary>
        /// Returns a snapshot of an alive tween without exposing the scheduler collection.
        /// </summary>
        public TweenDebugInfo GetAliveTween(int index) => TweenScheduler.GetDebugInfo(index);

        void Initialize()
        {
            if (gameObject.name != ManagerName)
                gameObject.name = ManagerName;

            if (transform.parent != null)
                transform.SetParent(null);

            DontDestroyOnLoad(gameObject);
        }

        void Awake()
        {
            if (instance != null && instance != this)
            {
                enabled = false;
                Destroy(gameObject);
                return;
            }

            instance = this;
            Initialize();
        }

        void Update()
        {
            if (instance != this)
                return;

            TweenScheduler.Update(UpdatePhase.Update, Time.deltaTime, Time.unscaledDeltaTime);
        }

        void LateUpdate()
        {
            if (instance != this)
                return;

            TweenScheduler.Update(UpdatePhase.LateUpdate, Time.deltaTime, Time.unscaledDeltaTime);
        }

        void FixedUpdate()
        {
            if (instance != this)
                return;

            TweenScheduler.Update(UpdatePhase.FixedUpdate, Time.fixedDeltaTime, Time.fixedUnscaledDeltaTime);
        }

        void OnDestroy()
        {
            if (instance != this)
                return;

            instance = null;

            if (!isQuitting)
                TweenScheduler.NotifyManagerDestroyed();
        }

        void OnApplicationQuit()
        {
            if (instance != this)
                return;

            isQuitting = true;
            TweenScheduler.NotifyApplicationQuit();
        }
    }
}