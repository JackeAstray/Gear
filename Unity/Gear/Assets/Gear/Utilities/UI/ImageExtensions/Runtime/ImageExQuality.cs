using UnityEngine;

namespace Gear.Utilities.UI.ImageExtensions
{
    /// <summary>
    /// ImageEx 全局画质档：Lite 通过 shader keyword 跳过高成本叠加效果，Full 保持完整效果。
    /// </summary>
    public static class GearImageExQuality
    {
        public enum Mode
        {
            Full = 0,
            Lite = 1,
        }

        public const string LiteKeyword = "GEAR_UI_LITE";

        public static Mode Current { get; private set; } = Mode.Full;

        public static void SetMode(Mode mode)
        {
            Current = mode;
            if (mode == Mode.Lite) Shader.EnableKeyword(LiteKeyword);
            else Shader.DisableKeyword(LiteKeyword);
        }

        public static void SetLite(bool lite) => SetMode(lite ? Mode.Lite : Mode.Full);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = Mode.Full;
            Shader.DisableKeyword(LiteKeyword);
        }
    }
}
