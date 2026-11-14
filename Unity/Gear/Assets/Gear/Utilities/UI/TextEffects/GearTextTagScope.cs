using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace Gear.Utilities.UI
{
    /// <summary>标签作用域支持的内建动效。</summary>
    public enum GearTextTagKind
    {
        Wave,
        Shake,
        Rainbow,
        Bounce,
        Pulse,
    }

    /// <summary>一段被标签包住的字符区间（索引基于去掉标签后的可见文本）。</summary>
    public readonly struct GearTextTagRange
    {
        public readonly GearTextTagKind Kind;
        public readonly int Start;
        public readonly int End;

        public GearTextTagRange(GearTextTagKind kind, int start, int end)
        {
            Kind = kind;
            Start = start;
            End = end;
        }

        public int Length => Mathf.Max(0, End - Start);
    }

    /// <summary>
    /// 文本标签作用域：用 <c>&lt;wave&gt;…&lt;/wave&gt;</c> 之类的标签，把动效限制在局部字符区间。
    /// <para>
    /// 通过 TMP 的 <see cref="ITextPreprocessor"/> 在排版前剥掉标签并记录区间，
    /// 再在 <see cref="TMP_Text.OnPreRenderText"/> 里只改这些区间的顶点/颜色。
    /// 未识别的标签（含 TMP 富文本）原样保留；未闭合标签按到文本末尾处理。
    /// </para>
    /// <para>
    /// 与 <c>LanguagesSystem</c> 的关系：预处理发生在 <c>UIText</c> 写入翻译文本之后
    /// （TMP SetText 内部调用），所以顺序无关；只要翻译串保留标签即可生效。
    /// </para>
    /// </summary>
    [AddComponentMenu("UI/Gear/Text Tag Scope")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class GearTextTagScope : MonoBehaviour, ITextPreprocessor
    {
        private static readonly string[] OpenTags = { "<wave>", "<shake>", "<rainbow>", "<bounce>", "<pulse>" };
        private static readonly string[] CloseTags = { "</wave>", "</shake>", "</rainbow>", "</bounce>", "</pulse>" };

        [SerializeField] private TMP_Text text;
        [SerializeField] private bool parseOnEnable = true;
        [SerializeField] private bool useUnscaledTime = true;

        [Header("Wave")]
        [SerializeField, Min(0f)] private float waveAmplitude = 6f;
        [SerializeField, Min(0f)] private float waveFrequency = 1.5f;
        [SerializeField] private float waveSpeed = 2f;

        [Header("Shake")]
        [SerializeField, Min(0f)] private float shakeAmplitude = 2.5f;
        [SerializeField] private float shakeSpeed = 1f;

        [Header("Rainbow")]
        [SerializeField] private float rainbowSpeed = 0.25f;
        [SerializeField, Range(0f, 1f)] private float rainbowSaturation = 0.7f;
        [SerializeField, Range(0f, 1f)] private float rainbowValue = 1f;
        [SerializeField, Min(0f)] private float rainbowSpread = 0.08f;

        [Header("Bounce")]
        [SerializeField, Min(0f)] private float bounceAmplitude = 7f;
        [SerializeField] private float bounceSpeed = 3f;

        [Header("Pulse")]
        [SerializeField, Range(0f, 0.5f)] private float pulseAmount = 0.12f;
        [SerializeField] private float pulseSpeed = 2f;

        private readonly List<GearTextTagRange> ranges = new List<GearTextTagRange>();
        private readonly List<int> waveStart = new List<int>();
        private readonly List<int> shakeStart = new List<int>();
        private readonly List<int> rainbowStart = new List<int>();
        private readonly List<int> bounceStart = new List<int>();
        private readonly List<int> pulseStart = new List<int>();

        public IReadOnlyList<GearTextTagRange> Ranges => ranges;
        public TMP_Text Text { get => text; set { Unregister(); text = value; Register(); } }
        public bool ParseOnEnable { get => parseOnEnable; set => parseOnEnable = value; }

        private void Awake()
        {
            if (text == null) text = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            if (text == null) text = GetComponent<TMP_Text>();
            if (text == null) return;

            Register();
            if (parseOnEnable && !string.IsNullOrEmpty(text.text) && text.text.IndexOf('<') >= 0)
            {
                text.SetText(text.text);
            }
        }

        private void OnDisable()
        {
            Unregister();
        }

        private void Register()
        {
            if (text == null) return;
            text.textPreprocessor = this;
            text.OnPreRenderText -= ApplyRanges;
            text.OnPreRenderText += ApplyRanges;
        }

        private void Unregister()
        {
            if (text == null) return;
            if (ReferenceEquals(text.textPreprocessor, this)) text.textPreprocessor = null;
            text.OnPreRenderText -= ApplyRanges;
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying || text == null || ranges.Count == 0) return;

            // 与 GearTextEffects 同挂时由它负责重建；单独使用时这里重建一次，避免顶点偏移累积
            if (GetComponent<GearTextEffects>() == null) text.ForceMeshUpdate();
        }

        /// <summary>重新注册预处理并刷新一次（编辑器/运行时手动驱动）。</summary>
        public void Refresh()
        {
            if (text == null) text = GetComponent<TMP_Text>();
            if (text == null) return;

            Register();
            text.SetText(text.text);
        }

        /// <summary>TMP 排版前调用：剥掉标签并记录字符区间。</summary>
        public string PreprocessText(string value)
        {
            ranges.Clear();
            waveStart.Clear();
            shakeStart.Clear();
            rainbowStart.Clear();
            bounceStart.Clear();
            pulseStart.Clear();

            string stripped = Parse(value, ranges, waveStart, shakeStart, rainbowStart, bounceStart, pulseStart);
            return stripped;
        }

        /// <summary>解析标签：返回去掉已知标签后的文本，ranges 为可见字符区间。</summary>
        public static string Parse(string source, List<GearTextTagRange> output, List<int> waveStack,
            List<int> shakeStack, List<int> rainbowStack, List<int> bounceStack, List<int> pulseStack)
        {
            if (string.IsNullOrEmpty(source)) return source ?? string.Empty;

            var builder = new StringBuilder(source.Length);
            int index = 0;
            int character = 0;
            while (index < source.Length)
            {
                if (source[index] == '<' && TryReadTag(source, index, out GearTextTagKind kind, out bool closing, out int length))
                {
                    List<int> stack = StackFor(kind, waveStack, shakeStack, rainbowStack, bounceStack, pulseStack);
                    if (closing)
                    {
                        if (stack != null && stack.Count > 0)
                        {
                            int start = stack[stack.Count - 1];
                            stack.RemoveAt(stack.Count - 1);
                            if (character > start) output?.Add(new GearTextTagRange(kind, start, character));
                        }
                    }
                    else
                    {
                        stack?.Add(character);
                    }

                    index += length;
                    continue;
                }

                if (source[index] == '<' && TryReadGenericTag(source, index, out int genericEnd))
                {
                    // TMP 富文本标签：保留在输出里，但不计入可见字符位置
                    builder.Append(source, index, genericEnd - index);
                    index = genericEnd;
                    continue;
                }

                char current = source[index];
                builder.Append(current);
                if (char.IsHighSurrogate(current) && index + 1 < source.Length && char.IsLowSurrogate(source[index + 1]))
                {
                    builder.Append(source[index + 1]);
                    index++;
                }

                character++;
                index++;
            }

            CloseOpen(output, waveStack, GearTextTagKind.Wave, character);
            CloseOpen(output, shakeStack, GearTextTagKind.Shake, character);
            CloseOpen(output, rainbowStack, GearTextTagKind.Rainbow, character);
            CloseOpen(output, bounceStack, GearTextTagKind.Bounce, character);
            CloseOpen(output, pulseStack, GearTextTagKind.Pulse, character);

            return builder.ToString();
        }

        private static void CloseOpen(List<GearTextTagRange> output, List<int> stack, GearTextTagKind kind, int end)
        {
            if (stack == null) return;
            for (int i = 0; i < stack.Count; i++)
            {
                if (end > stack[i]) output?.Add(new GearTextTagRange(kind, stack[i], end));
            }

            stack.Clear();
        }

        private static List<int> StackFor(GearTextTagKind kind, List<int> wave, List<int> shake,
            List<int> rainbow, List<int> bounce, List<int> pulse)
        {
            switch (kind)
            {
                case GearTextTagKind.Wave: return wave;
                case GearTextTagKind.Shake: return shake;
                case GearTextTagKind.Rainbow: return rainbow;
                case GearTextTagKind.Bounce: return bounce;
                case GearTextTagKind.Pulse: return pulse;
                default: return null;
            }
        }

        private static bool TryReadTag(string source, int index, out GearTextTagKind kind, out bool closing, out int length)
        {
            kind = GearTextTagKind.Wave;
            closing = false;
            length = 0;

            for (int i = 0; i < OpenTags.Length; i++)
            {
                if (Matches(source, index, OpenTags[i]))
                {
                    kind = (GearTextTagKind)i;
                    closing = false;
                    length = OpenTags[i].Length;
                    return true;
                }

                if (Matches(source, index, CloseTags[i]))
                {
                    kind = (GearTextTagKind)i;
                    closing = true;
                    length = CloseTags[i].Length;
                    return true;
                }
            }

            return false;
        }

        private static bool Matches(string source, int index, string tag)
        {
            if (index + tag.Length > source.Length) return false;
            for (int i = 0; i < tag.Length; i++)
            {
                char a = char.ToLowerInvariant(source[index + i]);
                if (a != tag[i]) return false;
            }

            return true;
        }

        private static bool TryReadGenericTag(string source, int index, out int end)
        {
            end = index;
            int close = source.IndexOf('>', index + 1);
            if (close < 0 || close - index > 64) return false;

            int first = index + 1;
            if (first >= close) return false;
            if (source[first] == '/') first++;
            if (first >= close || !char.IsLetter(source[first])) return false;

            for (int i = index + 1; i < close; i++)
            {
                char c = source[i];
                if (char.IsLetterOrDigit(c) || c == '/' || c == '=' || c == '#' || c == '%'
                    || c == '-' || c == '_' || c == ' ' || c == '"' || c == '\'' || c == '.' || c == ',')
                {
                    continue;
                }

                return false;
            }

            end = close + 1;
            return true;
        }

        private void ApplyRanges(TMP_TextInfo info)
        {
            if (info == null || ranges.Count == 0) return;

            float time = useUnscaledTime ? Time.unscaledTime : Time.time;
            for (int r = 0; r < ranges.Count; r++)
            {
                GearTextTagRange range = ranges[r];
                int start = Mathf.Clamp(range.Start, 0, info.characterCount);
                int end = Mathf.Clamp(range.End, 0, info.characterCount);
                for (int i = start; i < end; i++)
                {
                    TMP_CharacterInfo character = info.characterInfo[i];
                    if (!character.isVisible) continue;

                    TMP_MeshInfo mesh = info.meshInfo[character.materialReferenceIndex];
                    int vertexIndex = character.vertexIndex;
                    if (vertexIndex + 3 >= mesh.vertices.Length) continue;

                    switch (range.Kind)
                    {
                        case GearTextTagKind.Wave:
                            float phase = (time * waveSpeed + i * waveFrequency) * Mathf.PI * 2f;
                            float waveOffset = Mathf.Sin(phase) * waveAmplitude;
                            for (int v = 0; v < 4; v++) mesh.vertices[vertexIndex + v].y += waveOffset;
                            break;
                        case GearTextTagKind.Shake:
                            for (int v = 0; v < 4; v++)
                            {
                                Vector3 vertex = mesh.vertices[vertexIndex + v];
                                vertex.x += Mathf.Sin(time * shakeSpeed * 13.1f + i * 17.7f) * shakeAmplitude;
                                vertex.y += Mathf.Cos(time * shakeSpeed * 11.3f + i * 19.3f) * shakeAmplitude;
                                mesh.vertices[vertexIndex + v] = vertex;
                            }
                            break;
                        case GearTextTagKind.Rainbow:
                            Color color = Color.HSVToRGB(
                                Mathf.Repeat(time * rainbowSpeed + i * rainbowSpread, 1f),
                                rainbowSaturation, rainbowValue);
                            for (int v = 0; v < 4; v++)
                            {
                                color.a = mesh.colors32[vertexIndex + v].a / 255f;
                                mesh.colors32[vertexIndex + v] = color;
                            }
                            break;
                        case GearTextTagKind.Bounce:
                            float bouncePhase = time * bounceSpeed + i * 0.25f;
                            float bounceOffset = Mathf.Abs(Mathf.Sin(bouncePhase)) * bounceAmplitude;
                            for (int v = 0; v < 4; v++) mesh.vertices[vertexIndex + v].y += bounceOffset;
                            break;
                        case GearTextTagKind.Pulse:
                            float scale = 1f + Mathf.Sin(time * pulseSpeed + i * 0.15f) * pulseAmount;
                            Vector3 center = (mesh.vertices[vertexIndex] + mesh.vertices[vertexIndex + 1]
                                + mesh.vertices[vertexIndex + 2] + mesh.vertices[vertexIndex + 3]) * 0.25f;
                            for (int v = 0; v < 4; v++)
                            {
                                Vector3 vertex = mesh.vertices[vertexIndex + v];
                                mesh.vertices[vertexIndex + v] = center + (vertex - center) * scale;
                            }
                            break;
                    }
                }
            }
        }
    }
}
