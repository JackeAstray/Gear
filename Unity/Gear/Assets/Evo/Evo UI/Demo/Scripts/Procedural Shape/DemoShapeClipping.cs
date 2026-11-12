using UnityEngine;
using Evo.Tween;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Clipping tab: three activity rings, each an outline-only ellipse clipped around its center.
    /// Randomize, or Play every few seconds, sends the rings to new amounts, outer ring first. The rings grow in
    /// whenever the tab opens, and Daily goal divides every amount. The options start from the outer ring's clipping.
    /// One Evo Tween runs each change; every ring follows it a little behind the one outside it.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoShapeClipping : MonoBehaviour
    {
        [SerializeField] private ProceduralShape[] rings;

        [Header("Options")]
        [SerializeField] private Slider goal;
        [SerializeField] private Switch roundedEdges;
        [SerializeField] private Switch reverse;
        [SerializeField] private Switch play;

        const float Duration = 0.9f;
        const float Stagger = 0.07f;
        const float Interval = 2.6f;
        const float MinAmount = 0.15f;
        const float MinChange = 0.6f;
        const int MotionKey = 1;

        // Progress toward the goal; each ring shows it divided by the goal scale
        float[] from;
        float[] to;
        float goalScale = 1f;
        float changeTime;
        float nextRandomize;
        bool playing;
        TweenHandle motion;

        void Awake()
        {
            from = new float[rings.Length];
            to = new float[rings.Length];
            for (int i = 0; i < rings.Length; i++)
                to[i] = rings[i].ClipAmount;

            goalScale = goal.value / 100f;
            playing = play.IsOn;
            DemoUtils.Show(roundedEdges, rings[0].RoundedClipEdges);
            DemoUtils.Show(reverse, !rings[0].ClipClockwise);
        }

        void OnEnable()
        {
            for (int i = 0; i < from.Length; i++)
                from[i] = 0f;

            Begin();
        }

        void OnDisable() => motion.Kill();

        void Update()
        {
            if (playing && Time.unscaledTime >= nextRandomize)
                Randomize();
        }

        public void Randomize()
        {
            // Start from where the rings are now, so a press during a change doesn't jump
            for (int i = 0; i < rings.Length; i++)
                from[i] = Progress(i, changeTime);

            // Pick amounts that differ enough from the current ones to read as a change
            float change = 0f;
            for (int attempt = 0; attempt < 16 && change < MinChange; attempt++)
            {
                change = 0f;
                for (int i = 0; i < rings.Length; i++)
                {
                    to[i] = Mathf.Round(Random.Range(MinAmount, 1f) * 100f) / 100f;
                    change += Mathf.Abs(to[i] - from[i]);
                }
            }

            Begin();
        }

        public void SetGoal(float value)
        {
            goalScale = value / 100f;
            if (to != null)
                Apply(changeTime);
        }

        public void SetRoundedClip(bool value)
        {
            foreach (var ring in rings)
                ring.RoundedClipEdges = value;
        }

        public void SetClipReverse(bool value)
        {
            foreach (var ring in rings)
                ring.ClipClockwise = !value;
        }

        public void SetPlay(bool value)
        {
            playing = value;
            nextRandomize = Time.unscaledTime + Interval;
        }

        // The tween runs the seconds since the change began; binding applies the start at once
        void Begin()
        {
            nextRandomize = Time.unscaledTime + Interval;
            float total = Duration + Stagger * (rings.Length - 1);
            motion = EvoTween.Create(0f, total, total)
                .WithEase(Ease.Linear)
                .WithTimeMode(TimeMode.Unscaled)
                .Bind(this, static (demo, time) => demo.Apply(time), MotionKey);
        }

        void Apply(float time)
        {
            changeTime = time;
            for (int i = 0; i < rings.Length; i++)
                rings[i].ClipAmount = Progress(i, time) / goalScale;
        }

        // Ease out cubic, each ring a little behind the one outside it
        float Progress(int ring, float elapsed)
        {
            float t = 1f - Mathf.Clamp01((elapsed - ring * Stagger) / Duration);
            return Mathf.Lerp(from[ring], to[ring], 1f - t * t * t);
        }
    }
}
