using System;
using UnityEngine;

namespace Gear.Utilities.UI.Motion
{
    /// <summary>
    /// 全局动效偏好：关闭后入场/倾斜立即到完成态，方便弱机、无障碍或调试时一键降级。
    /// </summary>
    public static class GearMotionSettings
    {
        public static bool Enabled { get; private set; } = true;

        public static event Action<bool> EnabledChanged;

        public static void SetEnabled(bool enabled)
        {
            if (Enabled == enabled) return;
            Enabled = enabled;
            EnabledChanged?.Invoke(enabled);
        }

        public static void Toggle()
        {
            SetEnabled(!Enabled);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Enabled = true;
            EnabledChanged = null;
        }
    }
}
