using System;
using UnityEngine;
using UnityEngine.TextCore;
using TMPro;

namespace Evo.UI
{
    /// <summary>
    /// Composes character effects over fresh TMP geometry. Custom tags never replace the source text.
    /// </summary>
    [AddComponentMenu("Evo/UI/Effects/Text Effects (TMP)"), HelpURL(Constants.HelpUrl)]
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(TMP_Text))]
    public class TextEffects : MonoBehaviour, ITextPreprocessor
    {
        // Playback
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField] private TypewriterFX typewriter = new();
        [SerializeField] private ScrambleFX scramble = new();

        // Color & Overlay
        [SerializeField] private PaletteFX palette = new();
        [SerializeField] private AlphaPulseFX alphaPulse = new();
        [SerializeField] private ShimmerFX shimmer = new();

        // Motion
        [SerializeField] private WaveFX wave = new();
        [SerializeField] private BounceFX bounce = new();
        [SerializeField] private ShakeFX shake = new();
        [SerializeField] private GlitchFX glitch = new();

        // Emphasis
        [SerializeField] private PulseFX pulse = new();
        [SerializeField] private SwingFX swing = new();
        [SerializeField] private WiggleFX wiggle = new();

        // Pipeline
        Effect[] effects;
        Effect[] activeEffects;
        int activeEffectCount;
        TMP_VertexDataUpdateFlags activeChannels;
        TMP_VertexDataUpdateFlags modifiedChannels;
        TMP_Text textComponent;
        ITextPreprocessor previousPreprocessor;
        Action<TMP_TextInfo> textRebuiltHandler;
        bool attached;
        bool hasSnapshot;
        bool forceRefresh;
        double elapsedTime;
        readonly MeshSnapshot snapshot = new();

        // Parsing
        TagParser parser;
        string preprocessedSource;
        bool preprocessedForMesh;
        ulong[] characterMasks = Array.Empty<ulong>();
        ulong usedTags;

        // Properties
        public TMP_Text TextComponent => textComponent;
        public bool IsTyping => typewriter.IsPlaying;
        public bool IsScrambling => scramble.IsPlaying;
        public bool UseUnscaledTime { get => useUnscaledTime; set => useUnscaledTime = value; }
        public TypewriterFX Typewriter => typewriter;
        public ScrambleFX Scramble => scramble;
        public PaletteFX Palette => palette;
        public AlphaPulseFX AlphaPulse => alphaPulse;
        public ShimmerFX Shimmer => shimmer;
        public WaveFX Wave => wave;
        public BounceFX Bounce => bounce;
        public ShakeFX Shake => shake;
        public GlitchFX Glitch => glitch;
        public PulseFX Pulse => pulse;
        public SwingFX Swing => swing;
        public WiggleFX Wiggle => wiggle;
        bool IsAnimating => Application.IsPlaying(gameObject) || IsPreviewing;

        void OnEnable()
        {
            Initialize();
            Attach();
            elapsedTime = 0d;

            if (!Application.IsPlaying(gameObject))
                return;

            if (typewriter.enabled && typewriter.playOnEnable)
                PlayTypewriter();

            if (scramble.enabled && scramble.playOnEnable)
                PlayScramble();
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            StopPreview();
#endif
            Detach();
        }

        void OnDestroy() => Detach();

        void LateUpdate()
        {
            if (Application.IsPlaying(gameObject))
                Tick(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);
        }

        #region Initialization
        void Initialize()
        {
            if (textComponent == null)
                TryGetComponent(out textComponent);
            if (effects != null)
                return;

            // Registration defines composition order and tag IDs. The frame loop knows only Effect.
            effects = new Effect[12];
            activeEffects = new Effect[effects.Length];
            RegisterEffects();
            parser = new TagParser(effects);
        }

        void RegisterEffects()
        {
            // Undo and serialization can replace nested settings objects. Keep cached references current.
            Register(0, typewriter ??= new());
            Register(1, scramble ??= new());
            Register(2, wave ??= new());
            Register(3, bounce ??= new());
            Register(4, shake ??= new());
            Register(5, wiggle ??= new());
            Register(6, swing ??= new());
            Register(7, pulse ??= new());
            Register(8, glitch ??= new());
            Register(9, palette ??= new());
            Register(10, alphaPulse ??= new());
            Register(11, shimmer ??= new());
        }

        void Register(int index, Effect effect)
        {
            if (!ReferenceEquals(effects[index], effect))
                effects[index]?.Stop(this);

            effects[index] = effect;
            effect.Mask = 1UL << index;
        }

        void Attach()
        {
            if (attached || textComponent == null)
                return;

            previousPreprocessor = textComponent.textPreprocessor;
            if (ReferenceEquals(previousPreprocessor, this))
                previousPreprocessor = null;

            // One handler for the component's lifetime, so enabling and disabling don't allocate delegates
            textRebuiltHandler ??= OnTextRebuilt;
            textComponent.textPreprocessor = this;
            textComponent.OnPreRenderText += textRebuiltHandler;
            attached = true;
            forceRefresh = true;

            // Text set from a buffer (Counter, Timer, sliders) skips preprocessors, so it is set again as a string.
            // Only buffer text allocates when read. The editor keeps an equal string, which TMP would skip, so it is cleared first.
            if (Application.IsPlaying(gameObject))
            {
                string current = textComponent.text;
                textComponent.text = string.Empty;
                textComponent.text = current;
            }

            MarkTextDirty();
        }

        void Detach()
        {
            if (!attached)
                return;

            StopEffects();

            if (textComponent != null)
            {
                textComponent.OnPreRenderText -= textRebuiltHandler;
                if (ReferenceEquals(textComponent.textPreprocessor, this))
                    textComponent.textPreprocessor = previousPreprocessor;

                MarkTextDirty();
                if (textComponent.isActiveAndEnabled)
                    textComponent.ForceMeshUpdate();
            }

            previousPreprocessor = null;
            attached = false;
            hasSnapshot = false;
            modifiedChannels = 0;
        }

        void MarkTextDirty()
        {
            textComponent.havePropertiesChanged = true;
            textComponent.SetVerticesDirty();
            textComponent.SetLayoutDirty();
        }

        void StopEffects()
        {
            for (int i = 0; i < effects.Length; i++)
                effects[i].Stop(this);
        }
        #endregion

        #region Pipeline
        void Tick(float deltaTime)
        {
            if (!attached || textComponent == null || !textComponent.isActiveAndEnabled)
                return;

            deltaTime = Mathf.Max(0f, deltaTime);
            elapsedTime += deltaTime;
            bool rebuilt = false;

            if (forceRefresh || !hasSnapshot || textComponent.havePropertiesChanged)
            {
                if (forceRefresh)
                    RegisterEffects();

                forceRefresh = false;
                textComponent.ForceMeshUpdate();
                rebuilt = true;
            }

            for (int i = 0; i < effects.Length; i++)
            {
                Effect effect = effects[i];
                if (effect.NeedsTick)
                {
                    if (effect.enabled) { effect.Tick(this, deltaTime); }
                    else { effect.Stop(this); }
                }
            }

            // Visibility and external layout changes must produce a clean baseline before deformation
            if (textComponent.havePropertiesChanged)
            {
                textComponent.ForceMeshUpdate();
                return;
            }

            if (rebuilt || !hasSnapshot || textComponent.textInfo.characterCount == 0)
                return;

            PrepareEffects();
            TMP_VertexDataUpdateFlags channels = modifiedChannels | activeChannels;

            if (channels == 0)
                return;

            // TMP sorts after OnPreRenderText. Work in source order and restore its render order.
            bool reverse = textComponent.geometrySortingOrder == VertexSortingOrder.Reverse;
            if (reverse)
                ReverseGeometry();

            snapshot.Restore(textComponent.textInfo, channels);
            ApplyEffects();

            if (reverse)
                ReverseGeometry();

            textComponent.UpdateVertexData(channels);
        }

        void OnTextRebuilt(TMP_TextInfo info)
        {
            if (forceRefresh)
                RegisterEffects();

            snapshot.Capture(info, textComponent.firstVisibleCharacter);
            hasSnapshot = true;
            modifiedChannels = 0;
            int count = info.characterCount;
            EnsureCapacity(ref characterMasks, count);
            usedTags = 0;

            // Buffer/numeric TMP setters bypass ITextPreprocessor. Never reuse their predecessor's tags.
            bool useTags = preprocessedForMesh && parser.UsedTags != 0 && ReferenceEquals(textComponent.textPreprocessor, this)
                && string.Equals(textComponent.text, preprocessedSource, StringComparison.Ordinal);
            preprocessedForMesh = false;

            if (useTags)
            {
                TMP_CharacterInfo[] characters = info.characterInfo;
                for (int i = 0; i < count; i++)
                {
                    ulong mask = parser.GetMask(characters[i].index);
                    characterMasks[i] = mask;
                    usedTags |= mask;
                }
            }
            else
            {
                Array.Clear(characterMasks, 0, count);
            }

            for (int i = 0; i < effects.Length; i++)
                effects[i].Rebuild(this);

            if (IsAnimating)
            {
                PrepareEffects();
                ApplyEffects();
            }
        }

        void PrepareEffects()
        {
            activeEffectCount = 0;
            activeChannels = 0;

            for (int i = 0; i < effects.Length; i++)
            {
                Effect effect = effects[i];
                if (!effect.enabled || (effect.scope == EffectScope.TaggedCharacters && (usedTags & effect.Mask) == 0))
                    continue;

                TMP_VertexDataUpdateFlags channels = effect.Channels;
                if (channels == 0)
                    continue;

                effect.Prepare(elapsedTime, snapshot.MinX, snapshot.MaxX);
                activeEffects[activeEffectCount++] = effect;
                activeChannels |= channels;
            }
        }

        void ApplyEffects()
        {
            modifiedChannels = activeChannels;
            if (modifiedChannels == 0)
                return;

            TMP_Text text = textComponent;
            TMP_TextInfo info = text.textInfo;
            TMP_CharacterInfo[] characters = info.characterInfo;
            TMP_MeshInfo[] meshes = info.meshInfo;

            // Effects never change what TMP shows, so the visibility limits are read once
            int maxCharacters = text.maxVisibleCharacters;
            int maxLines = text.maxVisibleLines;
            int firstCharacter = text.firstVisibleCharacter;
            bool paged = text.overflowMode == TextOverflowModes.Page;
            int page = text.pageToDisplay;
            CharacterFrame frame = default;

            for (int i = 0; i < info.characterCount; i++)
            {
                ref readonly TMP_CharacterInfo character = ref characters[i];
                if (!character.isVisible || i >= maxCharacters || character.lineNumber >= maxLines || i < firstCharacter
                    || (paged && character.pageNumber + 1 != page))
                    continue;

                int material = character.materialReferenceIndex;
                int vertex = character.vertexIndex;
                if (!snapshot.Contains(material, vertex + 3))
                    continue;

                ref readonly TMP_MeshInfo mesh = ref meshes[material];
                frame.Load(i, characters, mesh, vertex, modifiedChannels);
                ulong mask = characterMasks[i];

                for (int j = 0; j < activeEffectCount; j++)
                {
                    Effect effect = activeEffects[j];
                    if (effect.scope == EffectScope.EntireText || (mask & effect.Mask) != 0)
                        effect.Apply(ref frame);
                }

                frame.Write(mesh, modifiedChannels);
            }
        }

        void ReverseGeometry()
        {
            TMP_TextInfo info = textComponent.textInfo;
            for (int i = 0; i < info.materialCount; i++)
                info.meshInfo[i].SortGeometry(VertexSortingOrder.Reverse);
        }
        #endregion

        #region Public API
        /// <summary>
        /// Sets source text, including optional effect tags. Settings control whether each tag is active.
        /// </summary>
        public void SetText(string value, bool startTypewriter = false, bool startScramble = false)
        {
            Initialize();

            if (textComponent == null)
                return;

            SkipTypewriter();
            scramble.Stop(this);
            textComponent.text = value ?? string.Empty;

            if (startTypewriter)
                PlayTypewriter();
            if (startScramble)
                PlayScramble();
        }

        public void PlayTypewriter() => Play(typewriter);

        public void PlayScramble() => Play(scramble);

        void Play(Effect effect)
        {
            Initialize();

            if (forceRefresh)
                RegisterEffects();

            if (!isActiveAndEnabled || effect == null || !effect.enabled || textComponent == null || !IsAnimating)
                return;

            Attach();
            effect.Play(this);
            Tick(0f);
        }

        public void SkipTypewriter()
        {
            if (textComponent != null)
                typewriter.Stop(this);
        }

        public void SkipScramble()
        {
            scramble.Stop(this);
            if (IsAnimating)
                Tick(0f);
        }

        /// <summary>
        /// Rebuilds layout and cached geometry on the next frame.
        /// </summary>
        public void Refresh() => forceRefresh = true;
        #endregion

        #region Mesh Cache
        static void EnsureCapacity<T>(ref T[] buffer, int required)
        {
            if (buffer == null || buffer.Length < required)
                Array.Resize(ref buffer, Mathf.NextPowerOfTwo(Mathf.Max(16, required)));
        }

        sealed class MeshSnapshot
        {
            Vector3[][] vertices = Array.Empty<Vector3[]>();
            Color32[][] colors = Array.Empty<Color32[]>();
            Vector4[][] uvs = Array.Empty<Vector4[]>();
            int[] counts = Array.Empty<int>();
            int materialCount;
            public float MinX { get; private set; }
            public float MaxX { get; private set; }

            public void Capture(TMP_TextInfo info, int firstCharacter)
            {
                materialCount = info.materialCount;
                EnsureCapacity(ref vertices, materialCount);
                EnsureCapacity(ref colors, materialCount);
                EnsureCapacity(ref uvs, materialCount);
                EnsureCapacity(ref counts, materialCount);

                for (int i = 0; i < materialCount; i++)
                {
                    ref readonly TMP_MeshInfo mesh = ref info.meshInfo[i];
                    int count = mesh.vertexCount;
                    EnsureCapacity(ref vertices[i], count);
                    EnsureCapacity(ref colors[i], count);
                    EnsureCapacity(ref uvs[i], count);
                    counts[i] = count;
                    Array.Copy(mesh.vertices, vertices[i], count);
                    Array.Copy(mesh.colors32, colors[i], count);
                    Array.Copy(mesh.uvs0, uvs[i], count);
                }

                float min = float.PositiveInfinity;
                float max = float.NegativeInfinity;
                TMP_CharacterInfo[] characters = info.characterInfo;

                // Metrics remain available for characters hidden by the typewriter. TMP never lays out characters
                // before firstVisibleCharacter, and their stale metrics shift on every rebuild, so they are skipped.
                for (int i = Mathf.Max(0, firstCharacter); i < info.characterCount; i++)
                {
                    ref readonly TMP_CharacterInfo character = ref characters[i];
                    min = Mathf.Min(min, Mathf.Min(character.origin, character.xAdvance));
                    max = Mathf.Max(max, Mathf.Max(character.origin, character.xAdvance));
                }

                if (min > max)
                {
                    min = 0f;
                    max = 1f;
                }

                MinX = min;
                MaxX = max;
            }

            public bool Contains(int material, int vertex) => material >= 0 && material < materialCount && vertex < counts[material];

            public void Restore(TMP_TextInfo info, TMP_VertexDataUpdateFlags channels)
            {
                int materials = Mathf.Min(materialCount, info.materialCount);
                for (int i = 0; i < materials; i++)
                {
                    ref readonly TMP_MeshInfo mesh = ref info.meshInfo[i];
                    int count = Mathf.Min(counts[i], mesh.vertexCount);
                    if ((channels & TMP_VertexDataUpdateFlags.Vertices) != 0)
                        Array.Copy(vertices[i], mesh.vertices, count);
                    if ((channels & TMP_VertexDataUpdateFlags.Colors32) != 0)
                        Array.Copy(colors[i], mesh.colors32, count);
                    if ((channels & TMP_VertexDataUpdateFlags.Uv0) != 0)
                        Array.Copy(uvs[i], mesh.uvs0, count);
                }
            }
        }
        #endregion

        #region Tag Parsing
        string ITextPreprocessor.PreprocessText(string text)
        {
            Initialize();
            preprocessedSource = text ?? string.Empty;
            preprocessedForMesh = true;
            string input = previousPreprocessor != null ? previousPreprocessor.PreprocessText(text) : text;
            return parser.Parse(input ?? string.Empty, textComponent.richText);
        }

        sealed class TagParser
        {
            readonly Effect[] effects;
            readonly int[] depths;
            char[] buffer = Array.Empty<char>();
            ulong[] masks = Array.Empty<ulong>();
            string cachedInput;
            string output;
            bool cachedRichText;
            int outputLength;

            public ulong UsedTags { get; private set; }

            public TagParser(Effect[] effects)
            {
                this.effects = effects;
                depths = new int[effects.Length];
            }

            public ulong GetMask(int sourceIndex) => sourceIndex >= 0 && sourceIndex < outputLength ? masks[sourceIndex] : 0;

            public string Parse(string input, bool richText)
            {
                if (cachedRichText == richText && string.Equals(input, cachedInput, StringComparison.Ordinal))
                    return output;

                cachedInput = input;
                cachedRichText = richText;
                outputLength = 0;
                UsedTags = 0;

                if (!richText || input.IndexOf('<') < 0)
                    return output = input;

                // Tags are only removed, so the output never outgrows the input
                EnsureCapacity(ref masks, input.Length);
                EnsureCapacity(ref buffer, input.Length);
                Array.Clear(depths, 0, depths.Length);

                ulong mask = 0;
                bool noParse = false;
                bool removedTag = false;

                for (int i = 0; i < input.Length;)
                {
                    int end = input[i] == '<' ? FindTagEnd(input, i + 1) : -1;
                    if (end >= 0)
                    {
                        bool close = input[i + 1] == '/';
                        int start = i + (close ? 2 : 1);
                        int length = end - start;
                        if (Matches(input, start, length, "noparse")) { noParse = !close; }
                        else if (!noParse)
                        {
                            int effectIndex = FindEffect(input, start, length);
                            if (effectIndex >= 0)
                            {
                                if (close) { depths[effectIndex] = Mathf.Max(0, depths[effectIndex] - 1); }
                                else { depths[effectIndex]++; }

                                ulong bit = effects[effectIndex].Mask;
                                if (depths[effectIndex] > 0) { mask |= bit; }
                                else { mask &= ~bit; }

                                removedTag = true;
                                i = end + 1;
                                continue;
                            }
                        }

                        // Preserve whole TMP tags, including quoted values containing angle brackets
                        while (i <= end)
                            Append(input[i++], mask);

                        continue;
                    }

                    Append(input[i++], mask);
                }

                return output = removedTag ? new string(buffer, 0, outputLength) : input;
            }

            void Append(char value, ulong mask)
            {
                buffer[outputLength] = value;
                masks[outputLength++] = mask;
                UsedTags |= mask;
            }

            int FindEffect(string input, int start, int length)
            {
                for (int i = 0; i < effects.Length; i++)
                {
                    if (Matches(input, start, length, effects[i].Tag))
                        return i;
                }
                return -1;
            }

            static bool Matches(string input, int start, int length, string tag)
            {
                return tag != null && length == tag.Length
                    && string.Compare(input, start, tag, 0, length, StringComparison.OrdinalIgnoreCase) == 0;
            }

            static int FindTagEnd(string input, int start)
            {
                char quote = '\0';
                for (int i = start; i < input.Length; i++)
                {
                    char value = input[i];
                    if (quote != '\0') { if (value == quote) quote = '\0'; }
                    else if (value == '"' || value == '\'') { quote = value; }
                    else if (value == '>') { return i; }
                    else if (value == '<') { return -1; }
                }
                return -1;
            }
        }
        #endregion

        #region Effect Base
        public enum EffectScope { EntireText, TaggedCharacters }
        public enum ColorMode { Base, Gradient }
        public enum ScrambleMode { OneShot, Continuous }
        public enum RevealOrder { LeftToRight, RightToLeft, Random }

        [Serializable]
        public abstract class Effect
        {
            public bool enabled;
            [Tooltip("Tagged Characters limits this effect to its matching <tag>...</tag> spans. Enabled is always the master switch.")]
            public EffectScope scope;

            internal ulong Mask;
            internal virtual string Tag => null;
            internal virtual TMP_VertexDataUpdateFlags Channels => TMP_VertexDataUpdateFlags.Vertices;
            internal virtual bool NeedsTick => false;
            protected double time;

            internal virtual void Tick(TextEffects owner, float deltaTime) { }
            internal virtual void Play(TextEffects owner) { }
            internal virtual void Stop(TextEffects owner) { }
            internal virtual void Rebuild(TextEffects owner) { }
            internal virtual void Prepare(double elapsed, float minX, float maxX) => time = elapsed;
            internal virtual void Apply(ref CharacterFrame frame) { }

            protected float Sine(float speed, float phase) => (float)Math.Sin(time * speed + phase);
        }

        internal struct CharacterFrame
        {
            public int Index;
            public float X0, X1, X2, X3;
            public Vector3 V0, V1, V2, V3;
            public Color C0, C1, C2, C3;
            public Vector4 U0, U1, U2, U3;
            TMP_CharacterInfo[] characters;
            Color32[] colors;
            int vertex;
            bool colorsLoaded;
            public readonly ref readonly TMP_CharacterInfo Character => ref characters[Index];
            public readonly Vector3 Center => (V0 + V2) * 0.5f;

            // One frame is reused for every character. UVs are only read when an active effect writes them.
            public void Load(int index, TMP_CharacterInfo[] characters, in TMP_MeshInfo mesh, int vertex, TMP_VertexDataUpdateFlags channels)
            {
                Index = index;
                this.characters = characters;
                this.vertex = vertex;
                colors = mesh.colors32;
                colorsLoaded = false;
                Vector3[] vertices = mesh.vertices;
                V0 = vertices[vertex]; V1 = vertices[vertex + 1];
                V2 = vertices[vertex + 2]; V3 = vertices[vertex + 3];
                X0 = V0.x; X1 = V1.x; X2 = V2.x; X3 = V3.x;

                if ((channels & TMP_VertexDataUpdateFlags.Uv0) != 0)
                {
                    Vector4[] uvs = mesh.uvs0;
                    U0 = uvs[vertex]; U1 = uvs[vertex + 1];
                    U2 = uvs[vertex + 2]; U3 = uvs[vertex + 3];
                }
            }

            /// <summary>
            /// Effects call this before touching C0-C3. Characters no color effect changes keep their restored bytes,
            /// which skips both conversions (a byte converted to Color and back is unchanged).
            /// </summary>
            public void LoadColors()
            {
                if (colorsLoaded)
                    return;

                colorsLoaded = true;
                C0 = colors[vertex]; C1 = colors[vertex + 1];
                C2 = colors[vertex + 2]; C3 = colors[vertex + 3];
            }

            public void Offset(Vector3 offset)
            {
                V0 += offset; V1 += offset; V2 += offset; V3 += offset;
            }

            public void Scale(Vector3 pivot, float x, float y)
            {
                Vector3 scale = new(x, y, 1f);
                V0 = pivot + Vector3.Scale(V0 - pivot, scale);
                V1 = pivot + Vector3.Scale(V1 - pivot, scale);
                V2 = pivot + Vector3.Scale(V2 - pivot, scale);
                V3 = pivot + Vector3.Scale(V3 - pivot, scale);
            }

            public void Rotate(Vector3 pivot, float degrees)
            {
                float radians = degrees * Mathf.Deg2Rad;
                float sin = Mathf.Sin(radians);
                float cos = Mathf.Cos(radians);
                RotateVertex(ref V0, pivot, sin, cos);
                RotateVertex(ref V1, pivot, sin, cos);
                RotateVertex(ref V2, pivot, sin, cos);
                RotateVertex(ref V3, pivot, sin, cos);
            }

            static void RotateVertex(ref Vector3 vertex, Vector3 pivot, float sin, float cos)
            {
                float x = vertex.x - pivot.x;
                float y = vertex.y - pivot.y;
                vertex.x = pivot.x + x * cos - y * sin;
                vertex.y = pivot.y + x * sin + y * cos;
            }

            public void MultiplyAlpha(float alpha)
            {
                LoadColors();
                C0.a *= alpha;
                C1.a *= alpha;
                C2.a *= alpha;
                C3.a *= alpha;
            }

            public readonly void Write(in TMP_MeshInfo mesh, TMP_VertexDataUpdateFlags channels)
            {
                if ((channels & TMP_VertexDataUpdateFlags.Vertices) != 0)
                {
                    Vector3[] vertices = mesh.vertices;
                    vertices[vertex] = V0; vertices[vertex + 1] = V1;
                    vertices[vertex + 2] = V2; vertices[vertex + 3] = V3;
                }

                if (colorsLoaded)
                {
                    colors[vertex] = C0; colors[vertex + 1] = C1;
                    colors[vertex + 2] = C2; colors[vertex + 3] = C3;
                }

                if ((channels & TMP_VertexDataUpdateFlags.Uv0) != 0)
                {
                    Vector4[] uvs = mesh.uvs0;
                    uvs[vertex] = U0; uvs[vertex + 1] = U1;
                    uvs[vertex + 2] = U2; uvs[vertex + 3] = U3;
                }
            }
        }
        #endregion

        #region Effects
        [Serializable]
        public sealed class TypewriterFX : Effect
        {
            public bool playOnEnable = true;
            [Min(0.1f), Tooltip("Characters revealed per second, including spaces and line breaks.")]
            public float speed = 30f;
            [Min(0f)] public float startDelay;
            [Min(0f), Tooltip("Extra pause after punctuation.")]
            public float punctuationDelay = 0.12f;
            public AudioClip audioClip;
            [Tooltip("Optional. Uses an AudioSource on this object when left empty.")]
            public AudioSource audioSource;
            [Range(0f, 1f)] public float volume = 0.5f;

            bool playing;
            int visibleCount;
            int previousLimit;
            double remainingDelay;
            AudioSource playbackSource;
            public bool IsPlaying => playing;
            internal override TMP_VertexDataUpdateFlags Channels => 0;
            internal override bool NeedsTick => playing;
            double Interval => 1d / Mathf.Max(0.1f, speed);

            // Hides every character without a rebuild of its own: the owner's next Tick rebuilds once for the new text and limit
            internal override void Play(TextEffects owner)
            {
                Stop(owner);
                previousLimit = owner.textComponent.maxVisibleCharacters;
                visibleCount = 0;
                remainingDelay = Mathf.Max(0f, startDelay) + Interval;
                playbackSource = audioSource;

                if (playbackSource == null && audioClip != null)
                    owner.TryGetComponent(out playbackSource);

                playing = true;
                owner.textComponent.maxVisibleCharacters = 0;
            }

            internal override void Tick(TextEffects owner, float deltaTime)
            {
                TMP_Text text = owner.textComponent;
                TMP_TextInfo info = text.textInfo;
                int total = Mathf.Min(info.characterCount, Mathf.Max(0, previousLimit));
                double interval = Interval;
                float pause = Mathf.Max(0f, punctuationDelay);
                remainingDelay -= deltaTime;
                bool playSound = false;
                // Bound catch-up work after a stall, retaining the remaining time for subsequent frames
                int processed = 0;
                while (visibleCount < total && remainingDelay <= 0d && processed++ < 256)
                {
                    ref readonly TMP_CharacterInfo character = ref info.characterInfo[visibleCount++];
                    playSound |= character.isVisible;
                    remainingDelay += interval;
                    if (IsPunctuation(character.character))
                        remainingDelay += pause;
                }

                if (playSound && !owner.IsPreviewing && audioClip != null && playbackSource != null && playbackSource.isActiveAndEnabled)
                    playbackSource.PlayOneShot(audioClip, Mathf.Clamp01(volume));

                if (visibleCount >= total) { Stop(owner); }
                else { text.maxVisibleCharacters = visibleCount; }
            }

            internal override void Stop(TextEffects owner)
            {
                if (!playing)
                    return;

                playing = false;

                if (owner.textComponent != null)
                    owner.textComponent.maxVisibleCharacters = previousLimit;

                playbackSource = null;
            }

            static bool IsPunctuation(char value) => value == '.' || value == ',' || value == '!' || value == '?' || value == ':' || value == ';';
        }

        [Serializable]
        public sealed class ScrambleFX : Effect
        {
            public ScrambleMode mode;
            public bool playOnEnable = true;
            [Min(0f)] public float startDelay;
            [Min(0f), Tooltip("Keeps the original text fully readable after each Continuous decode. Zero scrambles without pauses.")]
            public float restDuration = 1.5f;
            [Min(0.01f), Tooltip("Total decode time per burst, excluding Start Delay and Rest Duration.")]
            public float duration = 1.2f;
            public RevealOrder revealOrder;
            [Range(0f, 1f), Tooltip("Spreads character settling across the duration. Zero settles all characters together.")]
            public float stagger = 0.65f;
            [Min(0.1f), Tooltip("Character changes per second. Slows down as each character settles before completion or a readable rest.")]
            public float frequency = 24f;
            [Range(0f, 1f), Tooltip("Fraction of characters scrambled in Continuous mode.")]
            public float intensity = 0.35f;
            [Tooltip("Uses characters already present in each character's font atlas. Spaces, punctuation and sprites in the source stay intact.")]
            public string characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            public int seed = 17;

            bool playing;
            int characterCount;
            double burstTime;
            uint burst;
            int[] ranks = Array.Empty<int>();
            GlyphPool[] pools = Array.Empty<GlyphPool>();
            public bool IsPlaying => playing;
            internal override string Tag => "scramble";
            internal override bool NeedsTick => playing;
            internal override TMP_VertexDataUpdateFlags Channels => playing && time >= Delay && characterCount > 0
                && (mode != ScrambleMode.Continuous || restDuration <= 0f || GetElapsed(out _) < Duration)
                ? TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Uv0 : 0;
            float Delay => Mathf.Max(0f, startDelay);
            float Duration => Mathf.Max(0.01f, duration);

            // Scramble reads the rebuilt text when it starts
            internal override void Play(TextEffects owner)
            {
                owner.textComponent.ForceMeshUpdate();
                time = 0d;
                playing = characterCount > 0;
            }

            internal override void Stop(TextEffects owner) => playing = false;

            internal override void Tick(TextEffects owner, float deltaTime)
            {
                time += deltaTime;
                if (characterCount == 0 || (mode == ScrambleMode.OneShot && time >= Delay + Duration))
                    Stop(owner);
            }

            // Playback keeps its own time. Only the burst position is shared by every character.
            internal override void Prepare(double elapsed, float minX, float maxX) => burstTime = GetElapsed(out burst);

            double GetElapsed(out uint cycle)
            {
                double elapsed = Math.Max(0d, time - Delay);
                cycle = 0;
                if (mode == ScrambleMode.Continuous && restDuration > 0f)
                {
                    double length = (double)Duration + restDuration;
                    cycle = TimeStep(elapsed / length);
                    elapsed %= length;
                }
                return elapsed;
            }

            internal override void Rebuild(TextEffects owner)
            {
                characterCount = 0;
                if (!enabled)
                    return;

                TMP_TextInfo info = owner.textComponent.textInfo;
                EnsureCapacity(ref ranks, info.characterCount);
                EnsureCapacity(ref pools, info.materialCount);

                for (int i = 0; i < info.characterCount; i++)
                {
                    ranks[i] = -1;
                    ref readonly TMP_CharacterInfo character = ref info.characterInfo[i];
                    if (character.elementType != TMP_TextElementType.Character || !char.IsLetterOrDigit(character.character)
                        || character.fontAsset == null || character.textElement?.glyph == null
                        || (scope == EffectScope.TaggedCharacters && (owner.characterMasks[i] & Mask) == 0))
                        continue;

                    Glyph glyph = character.alternativeGlyph ?? character.textElement.glyph;
                    GlyphPool pool = pools[character.materialReferenceIndex] ??= new();
                    pool.Refresh(character.fontAsset, glyph.atlasIndex, characters);
                    if (pool.Count > 0 && glyph.metrics.width > 0f && glyph.metrics.height > 0f)
                        ranks[i] = characterCount++;
                }
            }

            internal override void Apply(ref CharacterFrame frame)
            {
                int rank = ranks[frame.Index];
                if (rank < 0)
                    return;

                double elapsed = burstTime;
                uint key = Key(frame.Index, seed) ^ unchecked(burst * 2891336453u);
                double sample = elapsed * Mathf.Max(0.1f, frequency);
                if (mode == ScrambleMode.OneShot || restDuration > 0f)
                {
                    float position = characterCount > 1 ? (float)rank / (characterCount - 1) : 1f;
                    if (characterCount > 1 && revealOrder == RevealOrder.RightToLeft) { position = 1f - position; }
                    else if (characterCount > 1 && revealOrder == RevealOrder.Random) { position = Noise(key, 0); }

                    double settleTime = Duration * Mathf.Lerp(1f - Mathf.Clamp01(stagger), 1f, position);
                    if (elapsed >= settleTime)
                        return;

                    // Integrate a falling change rate so the pattern never runs backwards near the reveal
                    sample *= 1d - 0.35d * elapsed / settleTime;
                }

                uint step = TimeStep(sample + Noise(key ^ SecondStream, 0));
                if (mode == ScrambleMode.Continuous && Noise(key ^ ThirdStream, step) >= Mathf.Clamp01(intensity))
                    return;

                ref readonly TMP_CharacterInfo character = ref frame.Character;
                Glyph original = character.alternativeGlyph ?? character.textElement.glyph;
                Glyph replacement = pools[character.materialReferenceIndex].GetGlyph(original, Noise(key, step));
                if (replacement != original)
                    ReplaceGlyph(ref frame, character.fontAsset, original, replacement);
            }

            static void ReplaceGlyph(ref CharacterFrame frame, TMP_FontAsset font, Glyph original, Glyph replacement)
            {
                int atlasWidth = font.atlasWidth;
                int atlasHeight = font.atlasHeight;
                GlyphMetrics source = original.metrics;
                GlyphMetrics target = replacement.metrics;
                GlyphRect rect = replacement.glyphRect;
                float paddingX = original.glyphRect.x - frame.U0.x * atlasWidth;
                float paddingY = original.glyphRect.y - frame.U0.y * atlasHeight;
                float width = Mathf.Max(0.0001f, source.width + paddingX * 2f);
                float height = Mathf.Max(0.0001f, source.height + paddingY * 2f);
                Vector3 horizontal = (frame.V3 - frame.V0) / width;
                Vector3 vertical = (frame.V1 - frame.V0) / height;

                // Keep the replacement inside its character slot without stretching its proportions
                float fit = Mathf.Min(1f, Mathf.Max(source.width, source.horizontalAdvance) / target.width);
                Vector3 bottom = frame.V0 + horizontal * ((width - (target.width + paddingX * 2f) * fit) * 0.5f)
                    + vertical * ((target.horizontalBearingY - target.height - paddingY) * fit
                        - (source.horizontalBearingY - source.height - paddingY));
                frame.V0 = bottom;
                frame.V1 = bottom + vertical * ((target.height + paddingY * 2f) * fit);
                frame.V2 = frame.V1 + horizontal * ((target.width + paddingX * 2f) * fit);
                frame.V3 = bottom + horizontal * ((target.width + paddingX * 2f) * fit);

                // Preserve TMP's packed atlas and SDF data in UV.zw, including the bold sign
                frame.U0.x = frame.U1.x = (rect.x - paddingX) / atlasWidth;
                frame.U2.x = frame.U3.x = (rect.x + rect.width + paddingX) / atlasWidth;
                frame.U0.y = frame.U3.y = (rect.y - paddingY) / atlasHeight;
                frame.U1.y = frame.U2.y = (rect.y + rect.height + paddingY) / atlasHeight;
                frame.U0.w *= fit; frame.U1.w *= fit;
                frame.U2.w *= fit; frame.U3.w *= fit;
            }

            sealed class GlyphPool
            {
                TMP_FontAsset font;
                int atlas;
                int lookupCount;
                string characters;
                Glyph[] glyphs = Array.Empty<Glyph>();
                public int Count { get; private set; }

                public Glyph GetGlyph(Glyph original, float sample)
                {
                    int start = (int)(sample * Count);
                    float width = Mathf.Max(original.metrics.width, original.metrics.horizontalAdvance);
                    Glyph replacement = original;
                    float narrowest = float.PositiveInfinity;
                    for (int i = 0; i < Count; i++)
                    {
                        Glyph glyph = glyphs[(start + i) % Count];
                        if (glyph == original)
                            continue;

                        if (glyph.metrics.width <= width)
                            return glyph;

                        if (glyph.metrics.width < narrowest)
                        {
                            replacement = glyph;
                            narrowest = glyph.metrics.width;
                        }
                    }
                    return replacement;
                }

                public void Refresh(TMP_FontAsset font, int atlas, string characters)
                {
                    int count = font.characterLookupTable.Count;
                    if (this.font == font && this.atlas == atlas && lookupCount == count && this.characters == characters)
                        return;

                    this.font = font;
                    this.atlas = atlas;
                    this.characters = characters;
                    lookupCount = count;
                    Count = 0;

                    if (string.IsNullOrEmpty(characters))
                        return;

                    EnsureCapacity(ref glyphs, characters.Length);
                    for (int i = 0; i < characters.Length; i++)
                    {
                        char value = characters[i];
                        if (char.IsWhiteSpace(value) || char.IsControl(value) || char.IsSurrogate(value) || characters.IndexOf(value) != i
                            || !font.characterLookupTable.TryGetValue(value, out TMP_Character character))
                            continue;

                        Glyph glyph = character.glyph;
                        if (glyph != null && glyph.atlasIndex == atlas && glyph.glyphRect.width > 0 && glyph.glyphRect.height > 0
                            && glyph.metrics.width > 0f && glyph.metrics.height > 0f)
                            glyphs[Count++] = glyph;
                    }
                }
            }
        }

        [Serializable]
        public sealed class WaveFX : Effect
        {
            [Min(0f)] public float speed = 4f;
            [Min(0f)] public float height = 5f;
            [Min(0f), Tooltip("Phase offset in radians between adjacent characters.")]
            public float density = 0.5f;
            internal override string Tag => "wave";
            internal override void Apply(ref CharacterFrame frame) => frame.Offset(Vector3.up * (Sine(speed, frame.Index * density) * height));
        }

        [Serializable]
        public sealed class BounceFX : Effect
        {
            [Min(0f)] public float speed = 3f;
            [Min(0f)] public float height = 5f;
            [Min(0f)] public float density = 0.5f;
            internal override string Tag => "bounce";
            internal override void Apply(ref CharacterFrame frame) => frame.Offset(Vector3.up * (Mathf.Abs(Sine(speed, frame.Index * density)) * height));
        }

        [Serializable]
        public sealed class ShakeFX : Effect
        {
            [Min(0f)] public float speed = 12f;
            [Min(0f), Tooltip("Maximum offset on either axis, in local text units.")]
            public float strength = 2f;
            public int seed = 1;
            uint step;
            float blend;
            internal override string Tag => "shake";

            internal override void Prepare(double elapsed, float minX, float maxX)
            {
                double sample = elapsed * Mathf.Max(0f, speed);
                step = TimeStep(sample);
                blend = Mathf.SmoothStep(0f, 1f, (float)(sample - Math.Floor(sample)));
            }

            internal override void Apply(ref CharacterFrame frame)
            {
                uint key = Key(frame.Index, seed);
                uint next = unchecked(step + 1);
                float x = Mathf.Lerp(Noise(key, step), Noise(key, next), blend) * 2f - 1f;
                float y = Mathf.Lerp(Noise(key ^ SecondStream, step), Noise(key ^ SecondStream, next), blend) * 2f - 1f;
                frame.Offset(new Vector3(x, y, 0f) * Mathf.Max(0f, strength));
            }
        }

        [Serializable]
        public sealed class GlitchFX : Effect
        {
            [Min(0f), Tooltip("Discrete glitch patterns per second. Does not change the source text or font atlas.")]
            public float frequency = 15f;
            [Range(0f, 1f)] public float intensity = 0.1f;
            [Min(0f)] public float displacement = 6f;
            [Range(0f, 1f)] public float stretch = 0.4f;
            [Range(0f, 1f)] public float flicker = 0.5f;
            public int seed = 7;
            uint step;
            internal override string Tag => "glitch";
            internal override TMP_VertexDataUpdateFlags Channels => TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32;
            internal override void Prepare(double elapsed, float minX, float maxX) => step = TimeStep(elapsed * Mathf.Max(0f, frequency));

            internal override void Apply(ref CharacterFrame frame)
            {
                uint key = Key(frame.Index, seed);
                if (Noise(key, step) >= Mathf.Clamp01(intensity))
                    return;

                float offset = Noise(key ^ SecondStream, step) * 2f - 1f;
                frame.Scale(frame.Center, 1f + offset * Mathf.Clamp01(stretch), 1f);
                frame.Offset(new Vector3(offset * displacement, 0f, 0f));
                frame.MultiplyAlpha(1f - Noise(key ^ ThirdStream, step) * Mathf.Clamp01(flicker));
            }
        }

        // Salts that derive independent noise streams from one character key
        const uint SecondStream = 0x68bc21eb;
        const uint ThirdStream = 0x02e5be93;

        static uint Key(int index, int seed) => unchecked((uint)(index * 747796405 + seed));

        static uint TimeStep(double value)
        {
            // The modulo only changes steps past uint range, so the common case skips it
            double step = Math.Floor(value);
            return step >= 0d && step < 4294967296d ? (uint)step : (uint)(step % 4294967296d);
        }

        static float Noise(uint key, uint step)
        {
            unchecked
            {
                uint value = key ^ (step * 2891336453u);
                value = (value ^ (value >> 16)) * 2246822519u;
                value = (value ^ (value >> 13)) * 3266489917u;
                return ((value ^ (value >> 16)) & 0x00ffffff) / 16777216f;
            }
        }

        // Moves a color toward a tint and multiplies the given alpha by the tint's
        static void Tint(ref Color color, Color tint, float amount, float alpha)
        {
            color = Color.LerpUnclamped(color, tint, amount);
            color.a = alpha * Mathf.Lerp(1f, tint.a, amount);
        }

        [Serializable]
        public sealed class WiggleFX : Effect
        {
            [Min(0f)] public float speed = 3f;
            [Min(0f)] public float angle = 10f;
            public float density = 1f;
            internal override string Tag => "wiggle";
            internal override void Apply(ref CharacterFrame frame) => frame.Rotate(frame.Center, Sine(speed, frame.Index * density) * angle);
        }

        [Serializable]
        public sealed class SwingFX : Effect
        {
            [Min(0f)] public float speed = 3f;
            [Min(0f)] public float angle = 15f;
            public float density = 1f;
            internal override string Tag => "swing";
            internal override void Apply(ref CharacterFrame frame) => frame.Rotate((frame.V1 + frame.V2) * 0.5f, Sine(speed, frame.Index * density) * angle);
        }

        [Serializable]
        public sealed class PulseFX : Effect
        {
            [Min(0f)] public float speed = 3f;
            [Range(0f, 0.95f), Tooltip("Scale oscillates between 1 minus Amount and 1 plus Amount.")]
            public float amount = 0.2f;
            public float density = 0.5f;
            internal override string Tag => "pulse";
            internal override void Apply(ref CharacterFrame frame)
            {
                float scale = 1f + Sine(speed, frame.Index * density) * Mathf.Clamp(amount, 0f, 0.95f);
                frame.Scale(frame.Center, scale, scale);
            }
        }

        [Serializable]
        public sealed class PaletteFX : Effect
        {
            public Gradient gradient = CreateRainbow();
            [Tooltip("Gradient travel per second. Negative values reverse the direction.")]
            public float speed = 1f;
            [Min(0f)] public float density = 0.1f;
            [Range(0f, 1f)] public float blend = 1f;
            internal override string Tag => "palette";
            internal override TMP_VertexDataUpdateFlags Channels => TMP_VertexDataUpdateFlags.Colors32;

            internal override void Apply(ref CharacterFrame frame)
            {
                if (gradient == null)
                    return;

                // Cosine yoyo has no wrap seam and slows to a stop at both ends
                float position = (float)(0.5d - 0.5d * Math.Cos((time * speed + frame.Index * density) * Math.PI));
                Color tint = gradient.Evaluate(position);
                float amount = Mathf.Clamp01(blend);
                frame.LoadColors();
                Tint(ref frame.C0, tint, amount, frame.C0.a);
                Tint(ref frame.C1, tint, amount, frame.C1.a);
                Tint(ref frame.C2, tint, amount, frame.C2.a);
                Tint(ref frame.C3, tint, amount, frame.C3.a);
            }

            static Gradient CreateRainbow()
            {
                Gradient result = new();
                GradientColorKey[] colors = new GradientColorKey[7];

                for (int i = 0; i < colors.Length; i++)
                    colors[i] = new GradientColorKey(Color.HSVToRGB(i / 6f, 1f, 1f), i / 6f);

                result.SetKeys(colors, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
                return result;
            }
        }

        [Serializable]
        public sealed class AlphaPulseFX : Effect
        {
            [Min(0f)] public float speed = 4f;
            [Range(0f, 1f)] public float minAlpha = 0.1f;
            public float density = 1f;
            internal override string Tag => "alphapulse";
            internal override TMP_VertexDataUpdateFlags Channels => TMP_VertexDataUpdateFlags.Colors32;
            internal override void Apply(ref CharacterFrame frame)
            {
                frame.MultiplyAlpha(Mathf.Lerp(Mathf.Clamp01(minAlpha), 1f, (Sine(speed, -frame.Index * density) + 1f) * 0.5f));
            }
        }

        [Serializable]
        public sealed class ShimmerFX : Effect
        {
            public ColorMode colorMode = ColorMode.Base;
            [Tooltip("Color and opacity across the beam, from left to right.")]
            public Gradient gradient = new();
            [Tooltip("Sweeps per second, excluding Rest Duration. Negative values sweep right to left.")]
            public float speed = 1f;
            [Min(0f)] public float restDuration = 0.3f;
            [Range(0.01f, 2f), Tooltip("Full beam width relative to the text's horizontal extent.")]
            public float width = 0.6f;
            [Range(0.01f, 1f), Tooltip("Fraction of each half-beam used for the smooth edge transition.")]
            public float softness = 0.9f;
            [Range(0f, 1f)] public float baseOpacity = 0.1f;

            float min;
            float inverseWidth;
            float beamCenter;
            float halfWidth;
            float edgeWidth;
            float opacity;
            bool resting;
            internal override string Tag => "shimmer";
            internal override TMP_VertexDataUpdateFlags Channels => TMP_VertexDataUpdateFlags.Colors32;

            internal override void Prepare(double elapsed, float minX, float maxX)
            {
                min = minX;
                inverseWidth = 1f / Mathf.Max(0.0001f, maxX - minX);
                halfWidth = Mathf.Max(0.01f, width) * 0.5f;
                edgeWidth = halfWidth * Mathf.Clamp(softness, 0.01f, 1f);
                opacity = Mathf.Clamp01(baseOpacity);
                float duration = 1f / Mathf.Max(0.0001f, Mathf.Abs(speed));
                double cycle = elapsed % (duration + Mathf.Max(0f, restDuration));
                resting = cycle >= duration;
                float progress = speed == 0f ? 0.5f : (float)(cycle / duration);

                if (speed < 0f)
                    progress = 1f - progress;

                beamCenter = Mathf.Lerp(-halfWidth, 1f + halfWidth, progress);
            }

            internal override void Apply(ref CharacterFrame frame)
            {
                if (resting)
                {
                    frame.MultiplyAlpha(opacity);
                    return;
                }

                frame.LoadColors();
                ApplyPair(ref frame.C0, ref frame.C1, frame.X0, frame.X1);
                ApplyPair(ref frame.C3, ref frame.C2, frame.X3, frame.X2);
            }

            // Left and right corners share x unless the glyph is slanted, so one beam sample serves both
            void ApplyPair(ref Color first, ref Color second, float firstX, float secondX)
            {
                bool tinted = Sample(firstX, out float amount, out Color tint);
                ApplyColor(ref first, amount, tinted, tint);

                if (secondX != firstX)
                    tinted = Sample(secondX, out amount, out tint);

                ApplyColor(ref second, amount, tinted, tint);
            }

            bool Sample(float x, out float amount, out Color tint)
            {
                float offset = (x - min) * inverseWidth - beamCenter;
                float edge = Mathf.Clamp01((halfWidth - Mathf.Abs(offset)) / edgeWidth);
                amount = edge * edge * (3f - 2f * edge);
                bool tinted = colorMode == ColorMode.Gradient && gradient != null && amount > 0f;
                tint = tinted ? gradient.Evaluate(Mathf.Clamp01(offset / (halfWidth * 2f) + 0.5f)) : default;
                return tinted;
            }

            void ApplyColor(ref Color color, float amount, bool tinted, Color tint)
            {
                float alpha = color.a * Mathf.Lerp(opacity, 1f, amount);
                if (tinted) { Tint(ref color, tint, amount, alpha); }
                else { color.a = alpha; }
            }
        }
        #endregion

#if UNITY_EDITOR
        // Preview state is never serialized. The editor owns ticking and all save/reload cleanup.
        public bool IsPreviewing { get; private set; }

        public void StartPreview()
        {
            if (Application.isPlaying || !isActiveAndEnabled)
                return;

            StopPreview();
            Initialize();
            Attach();
            IsPreviewing = true;
            elapsedTime = 0d;
            Refresh();

            if (typewriter.enabled)
                PlayTypewriter();

            if (scramble.enabled)
                PlayScramble();

            Tick(0f);
        }

        public void UpdatePreview(float deltaTime)
        {
            if (IsPreviewing && !Application.isPlaying)
                Tick(deltaTime);
        }

        public void StopPreview()
        {
            if (!IsPreviewing)
                return;

            IsPreviewing = false;
            StopEffects();
            elapsedTime = 0d;

            if (textComponent != null && textComponent.isActiveAndEnabled)
                textComponent.ForceMeshUpdate();
        }

        void OnValidate() => forceRefresh = true;
#else
        public bool IsPreviewing => false;
#endif
    }
}
