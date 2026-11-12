using System;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI
{
    /// <summary>
    /// Shared unscaled color transitions. Each target/channel has at most one active transition.
    /// </summary>
    [NoAutoStaticsCleanup]
    [AddComponentMenu("")]
    public sealed class ColorTransition : MonoBehaviour
    {
        // State
        static readonly Dictionary<(Behaviour, int), Track> tracks = new();
        static readonly List<Track> active = new();
        static readonly Stack<Track> pool = new();
        static ColorTransition runner;
        static Behaviour currentOwner;
        static AnimationCurve currentCurve;
        static bool currentEaseOut;
        static bool quitting;

        // Properties
        public static float Duration { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            tracks.Clear();
            active.Clear();
            pool.Clear();
            currentOwner = null;
            currentCurve = null;
            currentEaseOut = false;
            quitting = false;
            Duration = 0f;
            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;

            if (runner != null)
                Destroy(runner.gameObject);

            runner = null;
        }

        // Teardown still changes states (an EventSystem sends pointer exits from OnDisable), and a
        // transition started after the runner is destroyed would spawn a new host into the closing scene
        static void OnQuitting() => quitting = true;

        void OnDestroy()
        {
            if (runner != this)
                return;

            tracks.Clear();
            active.Clear();
            pool.Clear();
            runner = null;
        }

        void LateUpdate()
        {
            double now = Time.unscaledTimeAsDouble;

            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (i >= active.Count)
                    continue; // A custom setter may unregister other targets

                var track = active[i];
                if (track.target == null || !track.target.isActiveAndEnabled || track.owner == null || !track.owner.isActiveAndEnabled)
                {
                    Remove(track);
                    continue;
                }

                float t = Mathf.Clamp01((float)((now - track.startedAt) / track.duration));
                uint revision = track.revision;
                float progress = t >= 1f ? 1f : track.curve != null ? track.curve.Evaluate(t)
                    : track.easeOut ? Utilities.EaseOutQuadratic(t) : t;

                if (track.animate != null) { track.animate(progress); }
                else
                {
                    track.write(track.target, t >= 1f
                        ? track.endColor
                        : Utilities.UnpremultiplyColor(Vector4.LerpUnclamped(track.from, track.to, progress)));
                }

                if (t >= 1f && track.index >= 0 && track.revision == revision)
                    Remove(track);
            }
        }

        public static Scope Begin(Behaviour owner, float duration, AnimationCurve curve = null, bool easeOut = false)
        {
            return new(owner, duration, curve, easeOut);
        }

        public static void SetGraphicColor(Graphic target, Color color)
        {
            if (target != null)
                SetColor(target, 0, target.color, color, static (item, value) => ((Graphic)item).color = value);
        }

        /// <summary>
        /// Applies a color or retargets its transition from the displayed value.
        /// Use a stable channel per property and a cached/static setter to avoid delegate allocations.
        /// Channel zero is reserved for Graphic.color.
        /// </summary>
        public static void SetColor(Behaviour target, int channel, Color current, Color value, Action<Behaviour, Color> setter)
        {
            if (target == null || setter == null)
                return;

            var key = (target, channel);
            tracks.TryGetValue(key, out var track);
         
            if (!CanAnimate(target) || current == value)
            {
                if (track != null)
                    Remove(track);

                if (current != value)
                    setter(target, value);

                return;
            }

            track ??= Add(target, channel);
            track.revision++;
            track.owner = currentOwner != null ? currentOwner : target;
            track.startedAt = Time.unscaledTimeAsDouble;
            track.duration = Duration;
            track.curve = currentCurve;
            track.easeOut = currentEaseOut;
            track.from = Utilities.PremultiplyColor(current);
            track.to = Utilities.PremultiplyColor(value);
            track.endColor = value;
            track.write = setter;
            track.animate = null;
        }

        /// <summary>
        /// Releases transitions owned by or writing to a disabled/destroyed component.
        /// </summary>
        public static void Cancel(Behaviour owner)
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var track = active[i];
                if (ReferenceEquals(track.owner, owner) || ReferenceEquals(track.target, owner))
                    Remove(track);
            }
        }

        internal static void Cancel(Behaviour target, int channel)
        {
            if (tracks.TryGetValue((target, channel), out var track))
                Remove(track);
        }

        // Also used for compound color values, such as gradient keys, on the same clock
        internal static void Animate(Behaviour target, int channel, Action<float> update)
        {
            if (target == null)
                return;
            
            tracks.TryGetValue((target, channel), out var track);
           
            if (!CanAnimate(target))
            {
                if (track != null)
                    Remove(track);
               
                update(1f);
                return;
            }

            track ??= Add(target, channel);
            track.revision++;
            track.owner = currentOwner != null ? currentOwner : target;
            track.startedAt = Time.unscaledTimeAsDouble;
            track.duration = Duration;
            track.curve = currentCurve;
            track.easeOut = currentEaseOut;
            track.write = null;
            track.animate = update;
        }

        static bool CanAnimate(Behaviour target)
        {
            return Duration > 0f && target.isActiveAndEnabled && (currentOwner == null || currentOwner.isActiveAndEnabled);
        }

        static Track Add(Behaviour target, int channel)
        {
            var track = pool.Count > 0 ? pool.Pop() : new Track();
            track.target = target;
            track.channel = channel;
            track.index = active.Count;
            active.Add(track);
            tracks.Add((target, channel), track);
          
            if (runner == null)
            {
                var host = new GameObject("ColorTransitions") { hideFlags = HideFlags.HideInHierarchy };
                DontDestroyOnLoad(host);
                runner = host.AddComponent<ColorTransition>();
            }

            runner.enabled = true;
            return track;
        }

        static void Remove(Track track)
        {
            tracks.Remove((track.target, track.channel));
            int last = active.Count - 1;
            active[track.index] = active[last];
            active[track.index].index = track.index;
            active.RemoveAt(last);
            track.index = -1;
            track.revision++;
            track.target = null;
            track.owner = null;
            track.curve = null;
            track.write = null;
            track.animate = null;
            pool.Push(track);

            if (active.Count == 0 && runner != null)
                runner.enabled = false;
        }

        sealed class Track
        {
            public Behaviour target;
            public Behaviour owner;
            public int channel;
            public int index;
            public uint revision;
            public double startedAt;
            public float duration;
            public AnimationCurve curve;
            public bool easeOut;
            public Vector4 from;
            public Vector4 to;
            public Color endColor;
            public Action<Behaviour, Color> write;
            public Action<float> animate;
        }

        /// <summary>
        /// Scopes a duration, easing and owner for subsequent color changes.
        /// </summary>
        public readonly struct Scope : IDisposable
        {
            readonly Behaviour previousOwner;
            readonly float previousDuration;
            readonly AnimationCurve previousCurve;
            readonly bool previousEaseOut;

            internal Scope(Behaviour owner, float duration, AnimationCurve curve, bool easeOut)
            {
                previousOwner = currentOwner;
                previousDuration = Duration;
                previousCurve = currentCurve;
                previousEaseOut = currentEaseOut;
                currentEaseOut = easeOut;
                currentCurve = curve;
                currentOwner = owner;
                Duration = duration > 0f && !float.IsInfinity(duration) && !quitting && Application.isPlaying ? duration : 0f;
            }

            public void Dispose()
            {
                currentOwner = previousOwner;
                Duration = previousDuration;
                currentCurve = previousCurve;
                currentEaseOut = previousEaseOut;
            }
        }
    }
}