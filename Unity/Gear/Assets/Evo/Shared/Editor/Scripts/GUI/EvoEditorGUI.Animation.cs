using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;

namespace Evo.EditorTools
{
    public static partial class EvoEditorGUI
    {
        /// <summary>
        /// Asks the view being drawn for another frame. Call it from GUI code on every event while an animation runs.
        /// </summary>
        static void RequestAnimationFrame()
        {
            // Layout may also run while UI Toolkit measures the view, outside its event handling
            if (Event.current.type != EventType.Layout)
                HandleUtility.Repaint();
        }

        /// <summary>
        /// Editor time for animations. It holds still between editor updates, so every GUI pass of a frame lays out
        /// the same animated sizes (UI Toolkit measures an inspector before it paints it).
        /// </summary>
        [NoAutoStaticsCleanup]
        static class AnimationClock
        {
            // Updates outnumber frames, so the clock waits a moment before it stops, rather than resubscribing every frame
            const double IdleTimeout = 1d;

            static readonly EditorApplication.CallbackFunction tick = Tick;
            static double time;
            static double lastReadTime;
            static bool ticking;

            public static double Time
            {
                get
                {
                    if (!ticking)
                    {
                        ticking = true;
                        time = EditorApplication.timeSinceStartup;
                        EditorApplication.update += tick;
                    }

                    lastReadTime = time;
                    return time;
                }
            }

            static void Tick()
            {
                time = EditorApplication.timeSinceStartup;

                // Stops once animations are over, so an idle editor pays nothing
                if (time - lastReadTime > IdleTimeout)
                {
                    ticking = false;
                    EditorApplication.update -= tick;
                }
            }
        }

        /// <summary>
        /// A number easing from one value to another over editor time, with a cubic ease-out.
        /// </summary>
        readonly struct Transition
        {
            readonly float from;
            readonly float to;
            readonly double startTime;
            readonly double duration;

            public Transition(float from, float to, double startTime, double duration)
            {
                this.from = from;
                this.to = to;
                this.startTime = startTime;
                this.duration = duration;
            }

            public bool IsRunning(double time) => time - startTime < duration;

            public float Evaluate(double time)
            {
                if (!IsRunning(time))
                    return to;

                float remaining = 1f - Mathf.Clamp01((float)((time - startTime) / duration));
                return to + (from - to) * remaining * remaining * remaining;
            }
        }

        /// <summary>
        /// Eases a collapsible region open and closed. Content drawn between <see cref="BeginContent"/> and
        /// <see cref="EndContent"/> is clipped to the animated height, and the layout after it follows along.
        /// </summary>
        sealed class ExpandAnimation
        {
            readonly double duration;
            Transition transition;
            bool expanded;
            bool animating;
            bool grouped;

            // Visible fraction of the content, sampled on Layout so every event of a GUI pass sees the same layout
            float progress;

            public ExpandAnimation(bool expanded, double duration)
            {
                this.expanded = expanded;
                this.duration = duration;
                progress = expanded ? 1f : 0f;
            }

            /// <summary>
            /// True while the content rests open or closed, so a control can let go of the animation until the next click.
            /// </summary>
            public bool IsSettled => !animating && progress == (expanded ? 1f : 0f);

            /// <summary>
            /// True once the last change has eased all the way, even if the content wasn't drawn since.
            /// </summary>
            public bool IsFinished(double time) => !animating || !transition.IsRunning(time);

            /// <summary>
            /// Returns whether to draw the content, which stays true while it closes. A change eases in when
            /// <paramref name="animate"/> is set (a click) and jumps to the new state otherwise.
            /// </summary>
            public bool BeginContent(bool expanded, bool animate)
            {
                if (expanded != this.expanded)
                    SetExpanded(expanded, animate);

                if (Event.current.type == EventType.Layout)
                    progress = Sample();

                // Grouped only mid-animation, so settled content keeps its exact layout
                grouped = progress > 0f && progress < 1f;
                if (grouped)
                    EditorGUILayout.BeginFadeGroup(progress);

                if (animating)
                    RequestAnimationFrame();

                return progress > 0f;
            }

            public void EndContent()
            {
                if (!grouped)
                    return;

                grouped = false;
                EditorGUILayout.EndFadeGroup();
            }

            void SetExpanded(bool value, bool animate)
            {
                float target = value ? 1f : 0f;
                expanded = value;

                if (!animate)
                {
                    animating = false;
                    return;
                }

                // Reversing midway continues from the current height at the same speed
                double time = AnimationClock.Time;
                float start = animating ? transition.Evaluate(time) : progress;
                transition = new Transition(start, target, time, duration * Mathf.Abs(target - start));
                animating = true;
            }

            float Sample()
            {
                if (animating)
                {
                    double time = AnimationClock.Time;
                    animating = transition.IsRunning(time);
                    if (animating)
                        return transition.Evaluate(time);
                }

                return expanded ? 1f : 0f;
            }
        }
    }
}
