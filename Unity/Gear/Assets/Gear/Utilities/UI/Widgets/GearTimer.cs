using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace Gear.Utilities.UI.Widgets
{
    /// <summary>计时器模式。</summary>
    public enum GearTimerMode
    {
        /// <summary>秒表：从 0 往上累加。</summary>
        Stopwatch,

        /// <summary>倒计时：从 Duration 往下减，到 0 触发完成事件。</summary>
        Countdown,
    }

    /// <summary>
    /// 计时器：正计时（秒表）/ 倒计时，支持暂停继续、循环、unscaled 与格式化文本。
    /// <para>
    /// 状态机是纯逻辑：<see cref="Tick"/> 可手动推进，EditMode 用例不依赖 Update 时序。
    /// 倒计时完成只派发一次 <see cref="OnCompleted"/>；循环模式会立刻从 0 重新开始。
    /// </para>
    /// </summary>
    [AddComponentMenu("UI/Gear/Timer")]
    [DisallowMultipleComponent]
    public sealed class GearTimer : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private GearTimerMode mode = GearTimerMode.Countdown;
        [SerializeField, Min(0f)] private float duration = 60f;
        [SerializeField] private bool playOnEnable;
        [SerializeField] private bool unscaled = true;
        [SerializeField] private bool loop;
        [SerializeField, Min(0f)] private float timeMultiplier = 1f;
        [SerializeField] private bool useSystemClock;
        [SerializeField] private bool smoothProgress;
        [SerializeField, Min(0.01f)] private float progressSmoothing = 8f;
        [SerializeField] private bool reverseFill;
        [SerializeField] private string format = "mm:ss";
        [SerializeField] private UnityEvent onCompleted = new UnityEvent();

        private bool running;
        private float elapsed;
        private float displayProgress;
        private DateTime systemStart;
        private float elapsedAtStart;

        public UnityEvent OnCompleted => onCompleted;
        public event Action Completed;
        public event Action<float> ProgressChanged;

        public GearTimerMode Mode => mode;
        public bool IsRunning => running;
        public bool IsFinished => mode == GearTimerMode.Countdown && elapsed >= duration;
        public float Duration => duration;
        public float Elapsed => elapsed;

        /// <summary>倒计时剩余秒数；秒表返回已用秒数。</summary>
        public float Remaining => mode == GearTimerMode.Countdown ? Mathf.Max(0f, duration - elapsed) : elapsed;

        /// <summary>0..1 进度；Duration 为 0 时返回 0。</summary>
        public float Progress => duration <= 0f ? 0f : Mathf.Clamp01(elapsed / duration);

        /// <summary>平滑后的显示进度；未开启平滑时与 Progress 一致。</summary>
        public float DisplayProgress => reverseFill ? 1f - displayProgress : displayProgress;

        /// <summary>供进度条直接使用的填充值（已处理反向）。</summary>
        public float FillAmount => DisplayProgress;

        public float TimeMultiplier
        {
            get => timeMultiplier;
            set => timeMultiplier = Mathf.Max(0f, value);
        }

        public bool UseSystemClock
        {
            get => useSystemClock;
            set => useSystemClock = value;
        }

        public bool SmoothProgress
        {
            get => smoothProgress;
            set => smoothProgress = value;
        }

        public float ProgressSmoothing
        {
            get => progressSmoothing;
            set => progressSmoothing = Mathf.Max(0.01f, value);
        }

        public bool ReverseFill
        {
            get => reverseFill;
            set => reverseFill = value;
        }

        /// <summary>当前应显示的秒数（倒计时取剩余，秒表取已用）。</summary>
        public float DisplaySeconds => mode == GearTimerMode.Countdown ? Remaining : elapsed;

        public TMP_Text Label
        {
            get => label;
            set { label = value; Refresh(); }
        }

        public bool Loop
        {
            get => loop;
            set => loop = value;
        }

        public bool Unscaled
        {
            get => unscaled;
            set => unscaled = value;
        }

        /// <summary>格式串：HH/H 时、mm/m 分、ss/s 秒、ff/f 百分秒。</summary>
        public string Format
        {
            get => format;
            set { format = string.IsNullOrEmpty(value) ? "mm:ss" : value; Refresh(); }
        }

        private void OnEnable()
        {
            if (playOnEnable) Play();
            else Refresh();
        }

        private void OnDisable()
        {
            running = false;
        }

        private void Update()
        {
            Tick(unscaled ? Time.unscaledDeltaTime : Time.deltaTime);
        }

        /// <summary>配置模式与时长；不自动开始。</summary>
        public void Configure(GearTimerMode newMode, float newDuration, bool autoPlay, string newFormat = null)
        {
            mode = newMode;
            duration = Mathf.Max(0f, newDuration);
            if (!string.IsNullOrEmpty(newFormat)) format = newFormat;
            elapsed = 0f;
            displayProgress = 0f;
            running = autoPlay;
            if (running)
            {
                elapsedAtStart = 0f;
                systemStart = DateTime.UtcNow;
            }
            Refresh();
        }

        /// <summary>开始 / 继续。</summary>
        public void Play()
        {
            if (!running)
            {
                elapsedAtStart = elapsed;
                systemStart = DateTime.UtcNow;
            }

            running = true;
        }

        /// <summary>暂停；再次 <see cref="Play"/> 从当前进度继续。</summary>
        public void Pause() => running = false;

        public void Toggle()
        {
            running = !running;
        }

        /// <summary>回到起点并停止。</summary>
        public void ResetTimer()
        {
            elapsed = 0f;
            running = false;
            displayProgress = 0f;
            Refresh();
        }

        /// <summary>回到起点并立刻开始。</summary>
        public void Restart()
        {
            elapsed = 0f;
            displayProgress = 0f;
            Play();
            Refresh();
        }

        /// <summary>写入剩余（倒计时）或已用（秒表）秒数。</summary>
        public void SetSeconds(float seconds)
        {
            seconds = Mathf.Max(0f, seconds);
            elapsed = mode == GearTimerMode.Countdown
                ? Mathf.Clamp(duration - seconds, 0f, duration)
                : seconds;
            displayProgress = Progress;
            if (running)
            {
                elapsedAtStart = elapsed;
                systemStart = DateTime.UtcNow;
            }
            Refresh();
        }

        public void SetDuration(float newDuration)
        {
            duration = Mathf.Max(0f, newDuration);
            elapsed = Mathf.Min(elapsed, duration);
            Refresh();
        }

        /// <summary>推进计时；Update 与 EditMode 用例共用。</summary>
        public void Tick(float deltaTime)
        {
            if (!running || deltaTime <= 0f) return;

            float effectiveDelta = deltaTime * Mathf.Max(0f, timeMultiplier);
            if (useSystemClock)
                elapsed = elapsedAtStart + (float)(DateTime.UtcNow - systemStart).TotalSeconds;
            else
                elapsed += effectiveDelta;

            if (mode == GearTimerMode.Countdown && elapsed >= duration)
            {
                elapsed = duration;
                running = false;
                UpdateDisplayProgress(effectiveDelta);
                Refresh();
                Completed?.Invoke();
                onCompleted?.Invoke();
                if (loop)
                {
                    elapsed = 0f;
                    displayProgress = 0f;
                    elapsedAtStart = 0f;
                    systemStart = DateTime.UtcNow;
                    running = true;
                }

                return;
            }

            UpdateDisplayProgress(effectiveDelta);
            Refresh();
        }

        private void UpdateDisplayProgress(float delta)
        {
            float target = Progress;
            if (!smoothProgress)
            {
                displayProgress = target;
                return;
            }

            float t = 1f - Mathf.Exp(-progressSmoothing * Mathf.Max(0f, delta));
            displayProgress = Mathf.Lerp(displayProgress, target, t);
        }

        /// <summary>按 <see cref="Format"/> 渲染秒数。</summary>
        public string FormatTime(float seconds)
        {
            seconds = Mathf.Max(0f, seconds);
            int total = Mathf.FloorToInt(seconds);
            int hours = total / 3600;
            int minutes = total % 3600 / 60;
            int secs = total % 60;
            int hundredths = Mathf.FloorToInt((seconds - total) * 100f);
            if (hundredths > 99) hundredths = 99;

            var builder = new System.Text.StringBuilder(format.Length + 4);
            for (int i = 0; i < format.Length; i++)
            {
                char current = format[i];
                char next = i + 1 < format.Length ? format[i + 1] : ' ';
                switch (current)
                {
                    case 'H':
                        if (next == 'H') { builder.Append(hours.ToString("00")); i++; }
                        else builder.Append(hours);
                        break;
                    case 'm':
                        if (next == 'm') { builder.Append(minutes.ToString("00")); i++; }
                        else builder.Append(minutes);
                        break;
                    case 's':
                        if (next == 's') { builder.Append(secs.ToString("00")); i++; }
                        else builder.Append(secs);
                        break;
                    case 'f':
                        if (next == 'f') { builder.Append(hundredths.ToString("00")); i++; }
                        else builder.Append(hundredths);
                        break;
                    default:
                        builder.Append(current);
                        break;
                }
            }

            return builder.ToString();
        }

        private void Refresh()
        {
            if (!smoothProgress) displayProgress = Progress;
            if (label != null) label.text = FormatTime(DisplaySeconds);
            ProgressChanged?.Invoke(DisplayProgress);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            duration = Mathf.Max(0f, duration);
            if (string.IsNullOrEmpty(format)) format = "mm:ss";
            elapsed = Mathf.Clamp(elapsed, 0f, duration);
            Refresh();
        }
#endif
    }
}
