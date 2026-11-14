using TMPro;
using UnityEngine;

namespace Gear.Utilities.UI
{
    /// <summary>
    /// GearTextEffects 的扩展动效（颜色循环/透明脉冲/弹跳/摇摆/摆动/故障/整体脉冲）。
    /// 独立 partial 文件，避免主文件继续膨胀。
    /// </summary>
    public sealed partial class GearTextEffects
    {
        [Header("颜色循环")]
        [SerializeField] private bool palette;
        [SerializeField] private Color paletteColorA = new Color(0.49f, 0.36f, 0.99f);
        [SerializeField] private Color paletteColorB = new Color(0.14f, 0.75f, 0.95f);
        [SerializeField] private float paletteSpeed = 1f;
        [SerializeField] private float paletteSpread = 0.15f;

        [Header("透明度脉冲")]
        [SerializeField] private bool alphaPulse;
        [SerializeField] private float alphaPulseSpeed = 2f;
        [SerializeField, Range(0f, 1f)] private float alphaPulseMin = 0.35f;

        [Header("弹跳")]
        [SerializeField] private bool bounce;
        [SerializeField] private float bounceAmplitude = 8f;
        [SerializeField] private float bounceSpeed = 3f;
        [SerializeField] private float bouncePhase = 0.25f;

        [Header("摇摆（逐字旋转）")]
        [SerializeField] private bool swing;
        [SerializeField] private float swingAngle = 8f;
        [SerializeField] private float swingSpeed = 2f;
        [SerializeField] private float swingPhase = 0.3f;

        [Header("左右摆动")]
        [SerializeField] private bool wiggle;
        [SerializeField] private float wiggleAmplitude = 3f;
        [SerializeField] private float wiggleSpeed = 4f;
        [SerializeField] private float wigglePhase = 0.5f;

        [Header("故障")]
        [SerializeField] private bool glitch;
        [SerializeField] private float glitchAmplitude = 4f;
        [SerializeField] private float glitchInterval = 0.12f;

        [Header("整体脉冲")]
        [SerializeField] private bool pulse;
        [SerializeField] private float pulseAmount = 0.08f;
        [SerializeField] private float pulseSpeed = 2f;

        private bool HasExtraEffects =>
            palette || alphaPulse || bounce || swing || wiggle || glitch || pulse;

        public bool Palette { get => palette; set => palette = value; }
        public Color PaletteColorA { get => paletteColorA; set => paletteColorA = value; }
        public Color PaletteColorB { get => paletteColorB; set => paletteColorB = value; }
        public float PaletteSpeed { get => paletteSpeed; set => paletteSpeed = value; }
        public float PaletteSpread { get => paletteSpread; set => paletteSpread = value; }

        public bool AlphaPulse { get => alphaPulse; set => alphaPulse = value; }
        public float AlphaPulseSpeed { get => alphaPulseSpeed; set => alphaPulseSpeed = value; }
        public float AlphaPulseMin { get => alphaPulseMin; set => alphaPulseMin = Mathf.Clamp01(value); }

        public bool Bounce { get => bounce; set => bounce = value; }
        public float BounceAmplitude { get => bounceAmplitude; set => bounceAmplitude = Mathf.Max(0f, value); }
        public float BounceSpeed { get => bounceSpeed; set => bounceSpeed = value; }
        public float BouncePhase { get => bouncePhase; set => bouncePhase = value; }

        public bool Swing { get => swing; set => swing = value; }
        public float SwingAngle { get => swingAngle; set => swingAngle = value; }
        public float SwingSpeed { get => swingSpeed; set => swingSpeed = value; }
        public float SwingPhase { get => swingPhase; set => swingPhase = value; }

        public bool Wiggle { get => wiggle; set => wiggle = value; }
        public float WiggleAmplitude { get => wiggleAmplitude; set => wiggleAmplitude = Mathf.Max(0f, value); }
        public float WiggleSpeed { get => wiggleSpeed; set => wiggleSpeed = value; }
        public float WigglePhase { get => wigglePhase; set => wigglePhase = value; }

        public bool Glitch { get => glitch; set => glitch = value; }
        public float GlitchAmplitude { get => glitchAmplitude; set => glitchAmplitude = Mathf.Max(0f, value); }
        public float GlitchInterval { get => glitchInterval; set => glitchInterval = Mathf.Max(0.01f, value); }

        public bool Pulse { get => pulse; set => pulse = value; }
        public float PulseAmount { get => pulseAmount; set => pulseAmount = value; }
        public float PulseSpeed { get => pulseSpeed; set => pulseSpeed = value; }

        /// <summary>
        /// 第二次网格修改：逐字做位移/旋转/缩放与颜色变化。必须在主 LateUpdate 的
        /// UpdateVertexData 之前调用。
        /// </summary>
        private void ApplyExtraPass(TMP_TextInfo info, float time, ref bool colorsChanged)
        {
            for (int i = 0; i < info.characterCount; i++)
            {
                TMP_CharacterInfo character = info.characterInfo[i];
                if (!character.isVisible) continue;

                int vertexIndex = character.vertexIndex;
                TMP_MeshInfo mesh = info.meshInfo[character.materialReferenceIndex];
                Vector3 center = (character.topLeft + character.bottomRight) * 0.5f;

                for (int j = 0; j < 4 && vertexIndex + j < mesh.vertices.Length; j++)
                {
                    int index = vertexIndex + j;
                    Vector3 offset = mesh.vertices[index] - center;
                    Vector3 added = Vector3.zero;

                    if (wiggle)
                    {
                        float phase = (time * wiggleSpeed + i * wigglePhase) * Mathf.PI * 2f;
                        added.x += Mathf.Sin(phase) * wiggleAmplitude;
                    }

                    if (bounce)
                    {
                        float phase = (time * bounceSpeed + i * bouncePhase) * Mathf.PI * 2f;
                        added.y += Mathf.Abs(Mathf.Sin(phase)) * bounceAmplitude;
                    }

                    if (glitch)
                    {
                        float step = Mathf.Floor(time / Mathf.Max(0.01f, glitchInterval));
                        added.x += (Hash01(i * 4 + j, step) - 0.5f) * 2f * glitchAmplitude;
                        added.y += (Hash01(i * 7 + j + 13, step) - 0.5f) * 2f * glitchAmplitude;
                    }

                    Vector3 transformed = offset;
                    if (swing)
                    {
                        float angle = Mathf.Sin((time * swingSpeed + i * swingPhase) * Mathf.PI * 2f)
                            * swingAngle * Mathf.Deg2Rad;
                        float cos = Mathf.Cos(angle);
                        float sin = Mathf.Sin(angle);
                        transformed = new Vector3(offset.x * cos - offset.y * sin, offset.x * sin + offset.y * cos, offset.z);
                    }

                    if (pulse)
                    {
                        float scale = 1f + Mathf.Sin(time * pulseSpeed * Mathf.PI * 2f) * pulseAmount;
                        transformed *= scale;
                    }

                    mesh.vertices[index] = center + transformed + added;

                    if ((palette || alphaPulse) && index < mesh.colors32.Length)
                    {
                        Color32 color = mesh.colors32[index];

                        if (palette)
                        {
                            float t = 0.5f + 0.5f * Mathf.Sin(
                                (time * paletteSpeed + i * paletteSpread) * Mathf.PI * 2f);
                            Color mixed = Color.Lerp(paletteColorA, paletteColorB, t);
                            color.r = (byte)(Mathf.Clamp01(mixed.r) * 255f);
                            color.g = (byte)(Mathf.Clamp01(mixed.g) * 255f);
                            color.b = (byte)(Mathf.Clamp01(mixed.b) * 255f);
                        }

                        if (alphaPulse)
                        {
                            float t = Mathf.Lerp(alphaPulseMin, 1f,
                                0.5f + 0.5f * Mathf.Sin(time * alphaPulseSpeed * Mathf.PI * 2f + i * 0.35f * Mathf.PI * 2f));
                            color.a = (byte)(color.a * t);
                        }

                        mesh.colors32[index] = color;
                        colorsChanged = true;
                    }
                }
            }
        }

        private static float Hash01(int a, float b)
        {
            float value = Mathf.Sin(a * 12.9898f + b * 78.233f) * 43758.5453f;
            return value - Mathf.Floor(value);
        }
    }
}
