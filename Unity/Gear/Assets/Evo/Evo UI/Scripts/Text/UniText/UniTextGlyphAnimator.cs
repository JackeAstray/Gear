#if EVO_UNITEXT
using System;
using System.Collections.Generic;
using UnityEngine;
using LightSide;

namespace Evo.UI
{
    /// <summary>
    /// UniText glyph animator. A whole-text modifier offsets and fades glyphs while the mesh is generated,
    /// and draws extra digits as virtual glyphs, so a frame only regenerates the mesh from the cached layout.
    /// </summary>
    sealed class UniTextGlyphAnimator : TextGlyphAnimator
    {
        internal struct GlyphOp
        {
            public int cluster;
            public int fromCluster;
            public int codepoint; // Zero moves the real glyph, otherwise a virtual one is drawn
            public float slide;
            public float offsetY;
            public float heights;
            public float alpha;
        }

        /// <summary>
        /// Position of a glyph cell in layout space, where y grows downward.
        /// </summary>
        internal struct GlyphCell
        {
            public float centerX;
            public float baseline;
            public float ascent;
            public float descent;
            public bool valid;
        }

        readonly UniTextBase text;
        readonly Style style;
        readonly Action<UniTextCommitChanges> committedCallback;

        // Character to codepoint maps of the strings passed to Begin
        int[] fromClusters = Array.Empty<int>();
        int[] toClusters = Array.Empty<int>();
        int[] fromCodepoints = Array.Empty<int>();
        int fromLength;
        int toLength;
        int fromCount;

        // Codepoints the new text must be laid out with before any glyph is changed
        internal int[] targetCodepoints = Array.Empty<int>();
        internal int targetCount;

        // Layout the change started from, indexed by codepoint, and the top-left corner of the rect it was laid out in
        internal GlyphCell[] fromCells = Array.Empty<GlyphCell>();
        internal int fromCellCount;
        internal Vector2 fromOrigin;

        // Set while generating when the laid-out text does not match the new string, so frames stop rebuilding the mesh
        internal bool stale;

        List<GlyphOp> pending = new(32);
        internal List<GlyphOp> active = new(32);

        // New text waiting for the old one to be laid out, and the text it replaces
        string deferredTo;
        string deferredSource;

        internal UniTextGlyphAnimator(UniTextBase text) : base(text)
        {
            this.text = text;
            committedCallback = OnCommitted;

            // An instantiated text copies the style of the text it came from, without its animator
            var styles = text.Styles;
            for (int i = styles.Count - 1; i >= 0; i--)
            {
                if (styles[i]?.Modifier is GlyphModifier { owner: null })
                    styles.Remove(styles[i]);
            }

            style = Style.WholeText(new GlyphModifier { owner = this });
            styles.Add(style);
        }

        public override void Begin(string from, string to)
        {
            CancelDeferred();
            pending.Clear();
            active.Clear();
            stale = false;

            from ??= string.Empty;
            to ??= string.Empty;
            fromLength = Map(from, ref fromClusters, ref fromCodepoints, out fromCount);
            toLength = Map(to, ref toClusters, ref targetCodepoints, out targetCount);

            if (Capture())
            {
                UniTextBackend.Instance.SetText(text, to);
                return;
            }

            // The old text is not laid out yet, so the new one waits for its layout
            deferredTo = to;
            deferredSource = UniTextBackend.Instance.GetText(text);
            text.Committed += committedCallback;
            text.SetDirty(UniTextDirty.Mesh);
        }

        public override void MoveGlyph(int index, int fromIndex, float slide, float offsetY, float heights, float alpha)
            => Add(0, index, fromIndex, slide, offsetY, heights, alpha);

        public override void AddDigit(char digit, int index, int fromIndex, float slide, float offsetY, float heights, float alpha)
            => Add(digit, index, fromIndex, slide, offsetY, heights, alpha);

        public override void Apply()
        {
            (active, pending) = (pending, active);
            pending.Clear();

            // A text change rebuilds the mesh anyway, while a text that never matches would rebuild for nothing
            if (text != null && !stale)
                text.SetDirty(UniTextDirty.Mesh);
        }

        public override void End()
        {
            ApplyDeferred();
            pending.Clear();

            if (active.Count == 0)
                return;

            active.Clear();

            if (text != null)
                text.SetDirty(UniTextDirty.Mesh);
        }

        public override void Dispose()
        {
            End();

            if (text != null)
                text.Styles.Remove(style);
        }

        void Add(int codepoint, int index, int fromIndex, float slide, float offsetY, float heights, float alpha)
        {
            int cluster = (uint)index < (uint)toLength ? toClusters[index] : -1;
            int fromCluster = (uint)fromIndex < (uint)fromLength ? fromClusters[fromIndex] : -1;
            if (cluster < 0 && (codepoint == 0 || fromCluster < 0))
                return;

            pending.Add(new GlyphOp
            {
                cluster = cluster,
                fromCluster = fromCluster,
                codepoint = codepoint,
                slide = slide,
                offsetY = offsetY,
                heights = heights,
                alpha = alpha
            });
        }

        void OnCommitted(UniTextCommitChanges _)
        {
            Capture();
            ApplyDeferred();
        }

        /// <summary>
        /// Sets the deferred text, unless the text was set elsewhere in the meantime, in which case that text stays.
        /// </summary>
        void ApplyDeferred()
        {
            string to = deferredTo;
            string source = deferredSource;
            CancelDeferred();

            if (to != null && text != null && UniTextBackend.Instance.GetText(text) == source)
                UniTextBackend.Instance.SetText(text, to);
        }

        void CancelDeferred()
        {
            if (deferredTo == null)
                return;
            if (text != null)
                text.Committed -= committedCallback;

            deferredTo = null;
            deferredSource = null;
        }

        /// <summary>
        /// Reads the cells of the displayed glyphs when they show the old string.
        /// Digits may differ, as a retargeted roll starts from the digits it currently shows.
        /// </summary>
        bool Capture()
        {
            fromCellCount = 0;

            UniTextBuffers buffers = text.Buffers;
            ReadOnlySpan<int> codepoints = text.RenderedCodepoints;
            if (buffers == null || !Matches(codepoints, fromCodepoints, fromCount))
                return false;

            EnsureCapacity(ref fromCells, fromCount);
            Array.Clear(fromCells, 0, fromCount);
            fromCellCount = fromCount;
            ReadCells(text.ResultGlyphs, buffers, text.CurrentFontSize, fromCells, fromCount);

            Rect rect = text.GetPaddedRect();
            fromOrigin = new Vector2(rect.xMin, rect.yMax);
            return true;
        }

        static bool Matches(ReadOnlySpan<int> laidOut, int[] expected, int count)
        {
            if (laidOut.Length != count)
                return false;

            for (int i = 0; i < count; i++)
            {
                if (laidOut[i] != expected[i] && !(IsDigit(laidOut[i]) && IsDigit(expected[i])))
                    return false;
            }

            return true;
        }

        static bool IsDigit(int codepoint) => codepoint >= '0' && codepoint <= '9';

        /// <summary>
        /// Fills the cell of the first shaped glyph of each codepoint.
        /// </summary>
        internal static void ReadCells(ReadOnlySpan<PositionedGlyph> glyphs, UniTextBuffers buffers, float fontSize, GlyphCell[] cells, int count)
        {
            ReadOnlySpan<ShapedGlyph> shaped = buffers.shapedGlyphs.AsSpan();
            float glyphScale = buffers.GetGlyphScale(fontSize);

            for (int i = 0; i < glyphs.Length; i++)
            {
                ref readonly PositionedGlyph glyph = ref glyphs[i];
                int cluster = glyph.cluster;
                if ((uint)glyph.shapedGlyphIndex >= (uint)shaped.Length || (uint)cluster >= (uint)count || cells[cluster].valid)
                    continue;

                // The pen baseline, without the shaper's own offset
                float baseline = glyph.y + shaped[glyph.shapedGlyphIndex].offsetY * glyphScale;
                cells[cluster] = new GlyphCell
                {
                    centerX = (glyph.left + glyph.right) * 0.5f,
                    baseline = baseline,
                    ascent = baseline - glyph.top,
                    descent = glyph.bottom - baseline,
                    valid = true
                };
            }
        }

        /// <summary>
        /// Maps each UTF-16 index of the string to its codepoint index, as UniText clusters count codepoints.
        /// </summary>
        static int Map(string value, ref int[] clusters, ref int[] codepoints, out int count)
        {
            EnsureCapacity(ref clusters, value.Length);
            EnsureCapacity(ref codepoints, value.Length);
            count = 0;

            for (int i = 0; i < value.Length; i++)
            {
                clusters[i] = count;

                if (char.IsHighSurrogate(value[i]) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
                {
                    codepoints[count] = char.ConvertToUtf32(value[i], value[i + 1]);
                    clusters[++i] = count;
                }
                else { codepoints[count] = value[i]; }

                count++;
            }

            return value.Length;
        }

        internal static void EnsureCapacity<T>(ref T[] buffer, int required)
        {
            if (buffer.Length < required)
                buffer = new T[Mathf.NextPowerOfTwo(Mathf.Max(16, required))];
        }

        /// <summary>
        /// Applies the animator's current frame while UniText generates the mesh, which may run on a worker thread.
        /// </summary>
        [Serializable, HideFromTypeSelector]
        sealed partial class GlyphModifier : BaseModifier
        {
            [NonSerialized] internal UniTextGlyphAnimator owner;
            [NonSerialized] Action beforeGenerateMeshCallback;
            [NonSerialized] Action glyphCallback;
            [NonSerialized] UniTextMeshGenerator subscribedGenerator;
            [NonSerialized] Vector2 meshOrigin;

            // Resolved for the mesh being generated
            [NonSerialized] GlyphCell[] cells = Array.Empty<GlyphCell>();
            [NonSerialized] int[] moves = Array.Empty<int>();
            [NonSerialized] Vector2[] moveOffsets = Array.Empty<Vector2>();
            [NonSerialized] float[] virtualAlphas = Array.Empty<float>();
            [NonSerialized] int clusterCount;
            [NonSerialized] int virtualStart;
            [NonSerialized] int virtualCount;
            [NonSerialized] bool resolved;

            protected override void OnEnable()
            {
                beforeGenerateMeshCallback ??= OnBeforeGenerateMesh;
                glyphCallback ??= OnGlyph;
                uniText.BeforeGenerateMesh.Subscribe(beforeGenerateMeshCallback);

                subscribedGenerator = uniText.MeshGenerator;
                subscribedGenerator?.onGlyph.Subscribe(glyphCallback, UniTextMeshGenerator.GlyphOpacityOrder);
            }

            protected override void OnDisable()
            {
                uniText.BeforeGenerateMesh.Unsubscribe(beforeGenerateMeshCallback);

                // A replaced generator dropped its subscribers when it was disposed
                if (subscribedGenerator != null && subscribedGenerator == uniText.MeshGenerator)
                    subscribedGenerator.onGlyph.Unsubscribe(glyphCallback);

                subscribedGenerator = null;
                resolved = false;
            }

            // Layout positions are relative to the rect's top-left corner, which moves when the rect resizes
            protected override void PrepareForParallel()
            {
                Rect rect = uniText.GetPaddedRect();
                meshOrigin = new Vector2(rect.xMin, rect.yMax);
            }

            // Digits drawn as virtual glyphs must be in the atlas before the mesh is generated
            protected override void OnApply(in RangeApplyContext context)
            {
                for (uint digit = '0'; digit <= '9'; digit++)
                    buffers.RequestVirtualCodepoint(digit);
            }

            void OnBeforeGenerateMesh()
            {
                resolved = false;
                List<GlyphOp> ops = owner?.active;
                if (ops == null || ops.Count == 0)
                    return;

                // Changes apply only once the new text is laid out
                ReadOnlySpan<int> codepoints = buffers.codepoints.AsSpan();
                int count = codepoints.Length;
                owner.stale = !Matches(codepoints, owner.targetCodepoints, owner.targetCount);
                if (owner.stale)
                    return;

                ReadOnlySpan<PositionedGlyph> glyphs = uniText.ResultGlyphs;
                float fontSize = uniText.CurrentFontSize;

                EnsureCapacity(ref cells, count);
                EnsureCapacity(ref moves, count);
                EnsureCapacity(ref moveOffsets, count);
                Array.Clear(cells, 0, count);
                moves.AsSpan(0, count).Fill(-1);
                clusterCount = count;
                ReadCells(glyphs, buffers, fontSize, cells, count);

                UniTextFontProvider fontProvider = uniText.FontProvider;
                int faceCluster = FindFaceCluster(count);

                // Moves the old layout into the current rect's space, where y grows downward
                Vector2 shift = new(owner.fromOrigin.x - meshOrigin.x, meshOrigin.y - owner.fromOrigin.y);
                virtualStart = buffers.virtualPositionedGlyphs.count;
                virtualCount = 0;
                EnsureCapacity(ref virtualAlphas, ops.Count);

                for (int i = 0; i < ops.Count; i++)
                {
                    GlyphOp op = ops[i];
                    GlyphCell to = op.cluster >= 0 ? cells[op.cluster] : default;
                    GlyphCell from = op.fromCluster >= 0 && op.fromCluster < owner.fromCellCount ? owner.fromCells[op.fromCluster] : default;
                    from.centerX += shift.x;
                    from.baseline += shift.y;
                    if (op.codepoint == 0)
                    {
                        if (!to.valid)
                            continue;

                        float slideX = from.valid ? (from.centerX - to.centerX) * (1f - op.slide) : 0f;
                        moves[op.cluster] = i;
                        moveOffsets[op.cluster] = new Vector2(slideX, op.offsetY + op.heights * (to.ascent + to.descent));
                        continue;
                    }

                    if (!to.valid && !from.valid)
                        continue;

                    GlyphCell cell = !from.valid ? to : !to.valid ? from : Lerp(from, to, op.slide);
                    int face = to.valid ? op.cluster : faceCluster;
                    if (face < 0 || !buffers.TryResolveInjectedGlyph(fontProvider, (uint)op.codepoint, face, fontSize, out InjectedGlyph glyph))
                        continue;

                    // Layout y grows downward, while offsets lift the glyph
                    float x = cell.centerX - glyph.Advance * 0.5f;
                    float y = cell.baseline - (op.offsetY + op.heights * (cell.ascent + cell.descent));

                    buffers.virtualPositionedGlyphs.Add(new PositionedGlyph
                    {
                        glyphId = (int)glyph.GlyphIndex,
                        cluster = face,
                        x = x,
                        y = y,
                        fontId = glyph.FontId,
                        shapedGlyphIndex = -1,
                        left = x,
                        right = x + glyph.Advance,
                        top = y - cell.ascent,
                        bottom = y + cell.descent
                    });

                    virtualAlphas[virtualCount++] = op.alpha;
                }

                // Virtual glyphs follow the positioned ones in the generator's glyph indices
                virtualStart += glyphs.Length;
                resolved = true;
            }

            void OnGlyph()
            {
                if (!resolved)
                    return;

                UniTextMeshGenerator generator = uniText.MeshGenerator;
                int baseIndex = generator.faceBaseIdx;
                if (generator.isVirtualGlyph)
                {
                    int index = generator.currentPositionedIndex - virtualStart;
                    if ((uint)index < (uint)virtualCount)
                        ScaleAlpha(generator, baseIndex, virtualAlphas[index]);
                    return;
                }

                int cluster = generator.currentCluster;
                if ((uint)cluster >= (uint)clusterCount || moves[cluster] < 0)
                    return;

                Vector2 offset = moveOffsets[cluster];
                if (offset != Vector2.zero)
                    generator.OffsetQuad(baseIndex, offset.x, offset.y);

                ScaleAlpha(generator, baseIndex, owner.active[moves[cluster]].alpha);
            }

            int FindFaceCluster(int count)
            {
                for (int i = 0; i < count; i++)
                {
                    if (cells[i].valid && IsDigit(buffers.codepoints[i]))
                        return i;
                }
                return count > 0 && cells[0].valid ? 0 : -1;
            }

            static GlyphCell Lerp(in GlyphCell from, in GlyphCell to, float t) => new()
            {
                centerX = Mathf.LerpUnclamped(from.centerX, to.centerX, t),
                baseline = Mathf.LerpUnclamped(from.baseline, to.baseline, t),
                ascent = Mathf.LerpUnclamped(from.ascent, to.ascent, t),
                descent = Mathf.LerpUnclamped(from.descent, to.descent, t),
                valid = true
            };

            static void ScaleAlpha(UniTextMeshGenerator generator, int baseIndex, float alpha)
            {
                if (alpha >= 1f)
                    return;

                // Reading the colors bakes the text color into this mesh, so only fading glyphs read them
                Span<Color32> colors = generator.Colors;
                float scale = Mathf.Max(0f, alpha);
                for (int i = baseIndex; i < baseIndex + 4; i++)
                    colors[i].a = (byte)(colors[i].a * scale + 0.5f);
            }
        }
    }
}
#endif