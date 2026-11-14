using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace Gear.Utilities.UI
{
    /// <summary>
    /// TMP 文本动效：打字机、乱序显现、波浪、抖动、闪烁。挂在 <see cref="TMP_Text"/> 所在对象上即可。
    /// <para>
    /// 打字机用 <see cref="TMP_Text.maxVisibleCharacters"/>；乱序显现改写 TMP 文本后再逐字还原；
    /// 波浪/抖动/闪烁在 LateUpdate 里改顶点/颜色。文字内容变化时会自动重播。
    /// </para>
    /// </summary>
    [AddComponentMenu("UI/Gear/Text Effects")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed partial class GearTextEffects : MonoBehaviour
    {
        [Header("打字机")]
        [SerializeField] private bool typewriter = true;
        [SerializeField][Min(0f)] private float charactersPerSecond = 30f;
        [SerializeField][Min(0f)] private float startDelay;
        [SerializeField] private bool playOnEnable = true;
        [SerializeField] private UnityEvent onTypewriterCompleted = new UnityEvent();

        [Header("乱序显现")]
        [SerializeField] private bool scramble;
        [SerializeField][Min(0f)] private float scrambleDuration = 1.2f;
        [SerializeField][Min(1f)] private float scrambleFrequency = 24f;
        [SerializeField] private string scrambleCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        [Header("波浪")]
        [SerializeField] private bool wave;
        [SerializeField][Min(0f)] private float waveAmplitude = 6f;
        [SerializeField][Min(0f)] private float waveFrequency = 1.5f;
        [SerializeField] private float waveSpeed = 2f;

        [Header("抖动")]
        [SerializeField] private bool shake;
        [SerializeField][Min(0f)] private float shakeAmplitude = 2f;
        [SerializeField][Min(0f)] private float shakeFrequency = 12f;
        [SerializeField] private float shakeSpeed = 1f;

        [Header("闪烁")]
        [SerializeField] private bool shimmer;
        [SerializeField][Min(0f)] private float shimmerSpeed = 2f;
        [SerializeField][Range(0f, 1f)] private float shimmerWidth = 0.3f;
        [SerializeField][Range(0f, 1f)] private float shimmerIntensity = 0.5f;
        [SerializeField] private Color shimmerColor = Color.white;

        [SerializeField] private bool useUnscaledTime;

        private TMP_Text text;
        private float elapsed;
        private bool playing;
        private bool completed;
        private string lastText;
        private bool writingText;

        private bool scrambling;
        private float scrambleElapsed;
        private string originalText;

        public bool IsPlaying => playing || scrambling;
        public bool IsCompleted => completed;
        public UnityEvent OnTypewriterCompleted => onTypewriterCompleted;

        public bool Typewriter { get => typewriter; set => typewriter = value; }
        public float CharactersPerSecond { get => charactersPerSecond; set => charactersPerSecond = Mathf.Max(0f, value); }
        public float StartDelay { get => startDelay; set => startDelay = Mathf.Max(0f, value); }
        public bool PlayOnEnable { get => playOnEnable; set => playOnEnable = value; }
        public bool Scramble { get => scramble; set => scramble = value; }
        public float ScrambleDuration { get => scrambleDuration; set => scrambleDuration = Mathf.Max(0f, value); }
        public float ScrambleFrequency { get => scrambleFrequency; set => scrambleFrequency = Mathf.Max(1f, value); }
        public string ScrambleCharacters { get => scrambleCharacters; set => scrambleCharacters = value; }
        public bool Wave { get => wave; set => wave = value; }
        public float WaveAmplitude { get => waveAmplitude; set => waveAmplitude = Mathf.Max(0f, value); }
        public float WaveFrequency { get => waveFrequency; set => waveFrequency = Mathf.Max(0f, value); }
        public float WaveSpeed { get => waveSpeed; set => waveSpeed = value; }
        public bool Shake { get => shake; set => shake = value; }
        public float ShakeAmplitude { get => shakeAmplitude; set => shakeAmplitude = Mathf.Max(0f, value); }
        public float ShakeFrequency { get => shakeFrequency; set => shakeFrequency = Mathf.Max(0f, value); }
        public float ShakeSpeed { get => shakeSpeed; set => shakeSpeed = value; }
        public bool Shimmer { get => shimmer; set => shimmer = value; }
        public float ShimmerSpeed { get => shimmerSpeed; set => shimmerSpeed = Mathf.Max(0f, value); }
        public float ShimmerWidth { get => shimmerWidth; set => shimmerWidth = Mathf.Clamp01(value); }
        public float ShimmerIntensity { get => shimmerIntensity; set => shimmerIntensity = Mathf.Clamp01(value); }
        public Color ShimmerColor { get => shimmerColor; set => shimmerColor = value; }
        public bool UseUnscaledTime { get => useUnscaledTime; set => useUnscaledTime = value; }

        private void Awake()
        {
            text = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            if (text == null) text = GetComponent<TMP_Text>();
            if (text == null) return;

            lastText = text.text;

            // 编辑器里保持完整文本；只有 Play 模式才从 0 开始
            if (!Application.isPlaying)
            {
                text.maxVisibleCharacters = 99999;
                return;
            }

            if (playOnEnable) Play();
        }

        private void OnDisable()
        {
            RestoreOriginalText();
            if (text != null)
            {
                text.maxVisibleCharacters = 99999;
            }

            playing = false;
            scrambling = false;
        }

        /// <summary>从头播放：乱序优先于打字机；未开启时直接显示全文。</summary>
        public void Play()
        {
            if (text == null) text = GetComponent<TMP_Text>();
            if (text == null) return;

            elapsed = 0f;
            scrambleElapsed = 0f;
            completed = false;
            lastText = text.text;

            if (scramble && !string.IsNullOrEmpty(text.text))
            {
                originalText = text.text;
                scrambling = true;
                playing = false;
                text.maxVisibleCharacters = 99999;
                SetTextInternal(BuildScrambledText(originalText, 0, 0));
                return;
            }

            scrambling = false;

            if (!typewriter)
            {
                playing = false;
                completed = true;
                text.maxVisibleCharacters = 99999;
                return;
            }

            playing = true;
            text.maxVisibleCharacters = 0;
            text.ForceMeshUpdate();
        }

        /// <summary>停止动画并显示全文；波浪/抖动/闪烁仍继续。</summary>
        public void Stop()
        {
            RestoreOriginalText();
            scrambling = false;
            playing = false;
            if (text != null) text.maxVisibleCharacters = 99999;
        }

        /// <summary>立即完成当前显现并触发一次完成事件。</summary>
        public void Skip()
        {
            if (text == null) return;

            RestoreOriginalText();
            scrambling = false;
            playing = false;
            if (!completed)
            {
                completed = true;
                onTypewriterCompleted?.Invoke();
            }

            text.maxVisibleCharacters = 99999;
        }

        private void Update()
        {
            if (!Application.isPlaying || text == null) return;

            // 文本被翻译/业务改写时，重新开始一次显现
            if (!writingText && lastText != text.text)
            {
                lastText = text.text;
                if (scrambling)
                {
                    originalText = text.text;
                    scrambleElapsed = 0f;
                }
                else if (playing)
                {
                    elapsed = 0f;
                    completed = false;
                }
            }

            if (!scrambling)
            {
                if (!playing || !typewriter || text.textInfo == null) return;

                elapsed += DeltaTime;
                int count = text.textInfo.characterCount;
                if (count <= 0)
                {
                    if (string.IsNullOrEmpty(text.text))
                    {
                        playing = false;
                        completed = true;
                    }
                    return;
                }

                int visible = ComputeVisibleCharacters(elapsed - startDelay, charactersPerSecond, count);
                if (text.maxVisibleCharacters != visible)
                    text.maxVisibleCharacters = visible;

                if (!completed && visible >= count)
                {
                    completed = true;
                    playing = false;
                    onTypewriterCompleted?.Invoke();
                }

                return;
            }

            scrambleElapsed += DeltaTime;
            int total = CountVisibleCharacters(originalText);
            int revealed = ComputeScrambleReveal(scrambleElapsed, scrambleDuration, total);
            if (revealed >= total)
            {
                SetTextInternal(originalText);
                scrambling = false;
                completed = true;
                onTypewriterCompleted?.Invoke();
                return;
            }

            int frame = Mathf.FloorToInt(scrambleElapsed * scrambleFrequency);
            SetTextInternal(BuildScrambledText(originalText, revealed, frame));
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying || text == null || (!wave && !shake && !shimmer && !HasExtraEffects)) return;

            // 顶点/颜色动效需要从 TMP 的干净网格出发，避免每帧偏移累积
            text.ForceMeshUpdate();
            TMP_TextInfo info = text.textInfo;
            if (info == null) return;

            float time = useUnscaledTime ? Time.unscaledTime : Time.time;
            bool colorsChanged = false;

            for (int i = 0; i < info.characterCount; i++)
            {
                TMP_CharacterInfo character = info.characterInfo[i];
                if (!character.isVisible) continue;

                int vertexIndex = character.vertexIndex;
                TMP_MeshInfo meshInfo = info.meshInfo[character.materialReferenceIndex];
                for (int j = 0; j < 4 && vertexIndex + j < meshInfo.vertices.Length; j++)
                {
                    Vector3 vertex = meshInfo.vertices[vertexIndex + j];
                    if (wave)
                    {
                        float phase = (time * waveSpeed + i * waveFrequency) * Mathf.PI * 2f;
                        vertex.y += Mathf.Sin(phase) * waveAmplitude;
                    }

                    if (shake)
                    {
                        vertex.x += Mathf.Sin(time * shakeSpeed * 13.1f + i * 17.7f) * shakeAmplitude;
                        vertex.y += Mathf.Cos(time * shakeSpeed * 11.3f + i * 19.3f) * shakeAmplitude;
                    }

                    meshInfo.vertices[vertexIndex + j] = vertex;

                    if (shimmer && vertexIndex + j < meshInfo.colors32.Length)
                    {
                        float shimmerPhase = Mathf.Sin((time * shimmerSpeed - i * shimmerWidth) * Mathf.PI * 2f);
                        float highlight = Mathf.Clamp01(shimmerPhase) * shimmerIntensity;
                        if (highlight > 0f)
                        {
                            meshInfo.colors32[vertexIndex + j] =
                                Color.Lerp(meshInfo.colors32[vertexIndex + j], shimmerColor, highlight);
                            colorsChanged = true;
                        }
                    }
                }
            }

            if (HasExtraEffects)
                ApplyExtraPass(info, time, ref colorsChanged);

            text.UpdateVertexData(colorsChanged
                ? TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32
                : TMP_VertexDataUpdateFlags.Vertices);
        }

        private float DeltaTime => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        private void SetTextInternal(string value)
        {
            if (text == null || text.text == value) return;
            writingText = true;
            text.text = value;
            lastText = value;
            writingText = false;
        }

        private void RestoreOriginalText()
        {
            if (!scrambling || string.IsNullOrEmpty(originalText) || text == null) return;
            SetTextInternal(originalText);
        }

        private string BuildScrambledText(string source, int revealed, int frame)
        {
            if (string.IsNullOrEmpty(source)) return source;
            if (string.IsNullOrEmpty(scrambleCharacters)) return source;

            char[] buffer = source.ToCharArray();
            int visibleIndex = 0;
            bool inTag = false;
            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '<') { inTag = true; continue; }
                if (c == '>' && inTag) { inTag = false; continue; }
                if (inTag) continue;

                if (visibleIndex < revealed || char.IsWhiteSpace(c))
                {
                    visibleIndex++;
                    continue;
                }

                int hash = Mathf.Abs((i + 1) * 397 ^ (frame + 1) * 7919);
                buffer[i] = scrambleCharacters[hash % scrambleCharacters.Length];
                visibleIndex++;
            }

            return new string(buffer);
        }

        /// <summary>可见字符数：跳过 TMP 富文本标签，空格也算一个字符。</summary>
        public static int CountVisibleCharacters(string source)
        {
            if (string.IsNullOrEmpty(source)) return 0;

            int count = 0;
            bool inTag = false;
            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '<') { inTag = true; continue; }
                if (c == '>' && inTag) { inTag = false; continue; }
                if (!inTag) count++;
            }

            return count;
        }

        /// <summary>
        /// 打字机可见字符数：elapsed 秒 × 每秒字符数，夹在 0..characterCount。
        /// </summary>
        public static int ComputeVisibleCharacters(float elapsed, float charsPerSecond, int characterCount)
        {
            if (characterCount <= 0 || elapsed <= 0f || charsPerSecond <= 0f) return 0;
            return Mathf.Clamp(Mathf.FloorToInt(elapsed * charsPerSecond), 0, characterCount);
        }

        /// <summary>
        /// 乱序显现的已还原字符数。
        /// </summary>
        public static int ComputeScrambleReveal(float elapsed, float duration, int characterCount)
        {
            if (characterCount <= 0 || elapsed <= 0f) return 0;
            if (duration <= 0f) return characterCount;
            return Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(elapsed / duration) * characterCount), 0, characterCount);
        }
    }
}
