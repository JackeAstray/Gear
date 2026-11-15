using Gear.Utilities.UI.Widgets;
using UnityEngine;

namespace Gear.UI.Showcase
{
    /// <summary>
    /// 样例用的小动画：让若干 <see cref="GearProgressBar"/> 像电平表一样呼吸。
    /// 不是通用组件；只服务 UI 展厅的整屏样例。
    /// </summary>
    public sealed class ShowcaseLevelPulse : MonoBehaviour
    {
        private GearProgressBar[] meters;
        private float[] baseValues;
        private float[] speeds;
        private float[] phases;
        private float timer;

        public void Configure(GearProgressBar[] targetMeters, float[] bases, float[] meterSpeeds, float[] meterPhases)
        {
            meters = targetMeters;
            baseValues = bases;
            speeds = meterSpeeds;
            phases = meterPhases;
            timer = 0f;
            Apply(Time.unscaledTime);
        }

        private void Update()
        {
            timer -= Time.unscaledDeltaTime;
            if (timer > 0f) return;
            timer = 0.05f;
            Apply(Time.unscaledTime);
        }

        private void Apply(float time)
        {
            if (meters == null) return;
            for (int i = 0; i < meters.Length; i++)
            {
                GearProgressBar meter = meters[i];
                if (meter == null) continue;
                float speed = speeds != null && i < speeds.Length ? speeds[i] : 2f;
                float phase = phases != null && i < phases.Length ? phases[i] : 0f;
                float baseValue = baseValues != null && i < baseValues.Length ? baseValues[i] : 0.5f;
                float value = baseValue
                    + Mathf.Sin(time * speed + phase) * 0.10f
                    + Mathf.Sin(time * speed * 0.37f + phase * 1.7f) * 0.05f;
                meter.SetValueImmediate(Mathf.Clamp(value, 0.04f, 1f));
            }
        }
    }
}
