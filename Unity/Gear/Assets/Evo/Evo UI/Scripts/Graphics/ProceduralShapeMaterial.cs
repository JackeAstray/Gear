using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Evo.UI
{
    /// <summary>
    /// Material, shader, and gradient resource handling for <see cref="ProceduralShape"/>.
    /// </summary>
    public partial class ProceduralShape
    {
        #region Shader Configuration
        [SerializeField, HideInInspector] Shader embeddedShader;
        static Shader cachedShader;

        const string ShaderName = "Evo/UI/Procedural Shape";
        const int GradientRes = 256;

        static readonly int ID_FillColor = Shader.PropertyToID("_FillColor");
        static readonly int ID_FillColorMode = Shader.PropertyToID("_FillColorMode");
        static readonly int ID_FillGradientTex = Shader.PropertyToID("_FillGradientTex");
        static readonly int ID_FillGradientAngle = Shader.PropertyToID("_FillGradientAngle");
        static readonly int ID_FillGradientDirection = Shader.PropertyToID("_FillGradientDirection");
        static readonly int ID_FillGradientZoom = Shader.PropertyToID("_FillGradientZoom");
        static readonly int ID_FillGradientReverse = Shader.PropertyToID("_FillGradientReverse");

        static readonly int ID_OutlineColor = Shader.PropertyToID("_OutlineColor");
        static readonly int ID_OutlineDash = Shader.PropertyToID("_OutlineDash");
        static readonly int ID_OutlineColorMode = Shader.PropertyToID("_OutlineColorMode");
        static readonly int ID_OutlineGradientTex = Shader.PropertyToID("_OutlineGradientTex");
        static readonly int ID_OutlineGradientAngle = Shader.PropertyToID("_OutlineGradientAngle");
        static readonly int ID_OutlineGradientDirection = Shader.PropertyToID("_OutlineGradientDirection");
        static readonly int ID_OutlineGradientZoom = Shader.PropertyToID("_OutlineGradientZoom");
        static readonly int ID_OutlineGradientReverse = Shader.PropertyToID("_OutlineGradientReverse");

        static readonly int ID_InnerShadowColor = Shader.PropertyToID("_InnerShadowColor");
        static readonly int ID_InnerShadowColorMode = Shader.PropertyToID("_InnerShadowColorMode");
        static readonly int ID_InnerShadowGradientTex = Shader.PropertyToID("_InnerShadowGradientTex");
        static readonly int ID_InnerShadowGradientAngle = Shader.PropertyToID("_InnerShadowGradientAngle");
        static readonly int ID_InnerShadowGradientDirection = Shader.PropertyToID("_InnerShadowGradientDirection");
        static readonly int ID_InnerShadowGradientZoom = Shader.PropertyToID("_InnerShadowGradientZoom");
        static readonly int ID_InnerShadowGradientReverse = Shader.PropertyToID("_InnerShadowGradientReverse");

        static readonly int ID_OuterShadowColor = Shader.PropertyToID("_OuterShadowColor");
        static readonly int ID_OuterShadowColorMode = Shader.PropertyToID("_OuterShadowColorMode");
        static readonly int ID_OuterShadowGradientTex = Shader.PropertyToID("_OuterShadowGradientTex");
        static readonly int ID_OuterShadowGradientAngle = Shader.PropertyToID("_OuterShadowGradientAngle");
        static readonly int ID_OuterShadowGradientDirection = Shader.PropertyToID("_OuterShadowGradientDirection");
        static readonly int ID_OuterShadowGradientZoom = Shader.PropertyToID("_OuterShadowGradientZoom");
        static readonly int ID_OuterShadowGradientReverse = Shader.PropertyToID("_OuterShadowGradientReverse");
        #endregion

        #region Material State
        MaterialKey currentMatKey;
        Material pooledMaterial, interactionMaterial;
        ulong fillTexKey, outlineTexKey, innerShadowTexKey, outerShadowTexKey;

        static Texture2D whiteTex;
        static readonly Dictionary<MaterialKey, MatEntry> matPool = new();
        static readonly Dictionary<ulong, TexEntry> texPool = new();

        bool InnerShadowActive => innerShadowSize > 0.001f || innerShadowSoftness > 0.001f || innerShadowOffset.sqrMagnitude > 0.001f;
        bool OuterShadowActive => outerShadowSize > 0.001f || outerShadowSoftness > 0.001f || outerShadowOffset.sqrMagnitude > 0.001f;
        #endregion

        #region Gradient Defaults
        static readonly GradientColorKey[] defaultGradientColorKeys = { new(Color.white, 0f), new(Color.gray, 1f) };
        static readonly GradientAlphaKey[] defaultGradientAlphaKeys = { new(1f, 0f), new(1f, 1f) };
        static readonly Color32[] gradientPixels32 = new Color32[GradientRes];
        static readonly byte[] linearColorBytes = CreateLinearColorBytes();

        static byte[] CreateLinearColorBytes()
        {
            var bytes = new byte[256];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = ((Color32)new Color(i / 255f, 0f, 0f, 1f).linear).r;

            return bytes;
        }

        static Color32 LinearColor32(Color32 color) => new(linearColorBytes[color.r], linearColorBytes[color.g], linearColorBytes[color.b], color.a);

        static Gradient DefaultGradient()
        {
            var g = new Gradient();
            g.SetKeys(defaultGradientColorKeys, defaultGradientAlphaKeys);
            return g;
        }
        #endregion

        #region Styler Gradients
        GradientTransition[] stylerGradients;

        Color32[] GetStylerGradientPixels(int layer, ColorMode mode) => mode == ColorMode.StylerGradient
            && stylerGradients != null ? stylerGradients[layer]?.pixels : null;

        Gradient GetLayerGradient(int layer, ColorMode mode, Gradient fallback) => mode == ColorMode.StylerGradient
            && stylerGradients != null ? stylerGradients[layer]?.gradient ?? fallback : fallback;

        void ResolveStylerGradients(InteractionState state, bool useInteraction, bool refresh)
        {
            ResolveLayerGradient(0, fillColorMode, fillGradientStyler, fillGradient, state, useInteraction, refresh);
            ResolveLayerGradient(1, outlineColorMode, outlineGradientStyler, outlineGradient, state, useInteraction, refresh);
            ResolveLayerGradient(2, innerShadowColorMode, innerShadowGradientStyler, innerShadowGradient, state, useInteraction, refresh);
            ResolveLayerGradient(3, outerShadowColorMode, outerShadowGradientStyler, outerShadowGradient, state, useInteraction, refresh);
            ResolveLayerGradient(4, patternColorMode, patternGradientStyler, patternGradient, state, useInteraction, refresh);
        }

        bool IsGradientLayerActive(int layer) => layer switch
        {
            0 => fillCenter && fillColorMode == ColorMode.StylerGradient,
            1 => outlineWidth > 0.001f && outlineColorMode == ColorMode.StylerGradient,
            2 => InnerShadowActive && innerShadowColorMode == ColorMode.StylerGradient,
            3 => OuterShadowActive && outerShadowColorMode == ColorMode.StylerGradient,
            _ => PatternActive && patternColorMode == ColorMode.StylerGradient
        };

        void ResolveLayerGradient(int layer, ColorMode mode, Styler.GradientStyle style, Gradient fallback,
            InteractionState state, bool useInteraction, bool refresh)
        {
            if (mode != ColorMode.StylerGradient)
            {
                if (stylerGradients != null && stylerGradients[layer] != null)
                    ColorTransition.Cancel(this, layer + 6);

                return;
            }

            stylerGradients ??= new GradientTransition[5];
            stylerGradients[layer] ??= new GradientTransition(this, layer);
            Gradient gradient = style != null ? style.ResolveGradient(preset, state, useInteraction, fallback) : fallback;
            float alpha = style != null ? style.ResolveAlphaOverride(state, useInteraction) : -1f;
            stylerGradients[layer].SetGradient(gradient, alpha, refresh);
        }

        // Sample once when the target changes. Reuse buffers and the shared color clock;
        // only the existing atlas row changes during a transition.
        sealed class GradientTransition
        {
            readonly ProceduralShape owner;
            readonly int layer;
            readonly Vector4[] from = new Vector4[GradientRes];
            readonly Color32[] to = new Color32[GradientRes];
            readonly System.Action<float> update;
            public readonly Color32[] pixels = new Color32[GradientRes];
            public Gradient gradient;
            float alpha = -1f;
            bool initialized;

            public GradientTransition(ProceduralShape owner, int layer)
            {
                this.owner = owner;
                this.layer = layer;
                update = Update;
            }

            public void SetGradient(Gradient value, float alphaOverride, bool refresh)
            {
                if (!refresh && initialized && ReferenceEquals(gradient, value) && alpha == alphaOverride)
                    return;

                gradient = value;
                alpha = alphaOverride;
                bool animate = initialized && ColorTransition.Duration > 0f && owner.isActiveAndEnabled
                    && owner.IsGradientLayerActive(layer) && owner.stylePage != null && owner.m_Material == null;
                if (alpha >= 0f)
                    SampleWithAlphaOverride(value);

                bool changed = false;
                for (int i = 0; i < GradientRes; i++)
                {
                    Color32 sample = alpha >= 0f ? to[i] : value != null ? value.Evaluate((float)i / (GradientRes - 1)) : Color.white;
                    from[i] = Utilities.PremultiplyColor(pixels[i]);
                    to[i] = sample;
                    changed |= PackColor(sample) != PackColor(pixels[i]);
                    if (!animate)
                        pixels[i] = sample;
                }

                initialized = true;
                if (animate && changed) { ColorTransition.Animate(owner, layer + 6, update); }
                else
                {
                    ColorTransition.Cancel(owner, layer + 6);
                    Write();
                }
            }

            // The peak alpha becomes the override and relative fades keep their shape. Peak texels get
            // the exact byte a Styler color override would, so a uniform ramp matches a solid color.
            void SampleWithAlphaOverride(Gradient value)
            {
                int peak = 0;
                for (int i = 0; i < GradientRes; i++)
                {
                    to[i] = value != null ? value.Evaluate((float)i / (GradientRes - 1)) : Color.white;
                    if (to[i].a > peak)
                        peak = to[i].a;
                }

                // Integer rounding keeps resampling cheap
                int target = (int)Mathf.Round(alpha * 255f);
                for (int i = 0; i < GradientRes; i++)
                    to[i].a = (byte)(peak > 0 ? (to[i].a * target * 2 + peak) / (peak * 2) : target);
            }

            void Update(float t)
            {
                for (int i = 0; i < GradientRes; i++)
                    pixels[i] = t >= 1f ? to[i] : Utilities.UnpremultiplyColor(Vector4.LerpUnclamped(from[i], Utilities.PremultiplyColor(to[i]), t));

                Write();
            }

            void Write()
            {
                if (!owner.IsGradientLayerActive(layer))
                    return;

                if (owner.m_Material == null && owner.stylePage != null && owner.stylePage.material)
                    owner.stylePage.WriteGradient(owner.styleSlot, layer, gradient, pixels);
                else
                    owner.SetMaterialDirty();
            }
        }
        #endregion

        #region Unity Material Overrides
        public override Texture mainTexture
        {
            get
            {
                if (sprite != null && sprite.texture != null)
                    return sprite.texture;
                if (PatternActive && stylePage != null && patternSource == PatternSource.Sprite)
                    return patternSprite.texture;
                if (m_Material != null && m_Material.mainTexture != null)
                    return m_Material.mainTexture;

                return whiteTex ? whiteTex : (whiteTex = Texture2D.whiteTexture);
            }
        }

        public override Material defaultMaterial
        {
            get
            {
                if (stylePage == null && pooledMaterial == null && interactionMaterial == null)
                    ResolveMaterialAndTextures();
                if (stylePage != null)
                    return blurSource != null ? blurSource.GetMaterial(stylePage.material, null, true) : stylePage.material;
                if (interactionMaterial != null)
                    return blurSource != null ? blurSource.GetMaterial(interactionMaterial) : interactionMaterial;

                Material result = pooledMaterial != null ? pooledMaterial : base.defaultMaterial;
                return blurSource != null ? blurSource.GetMaterial(result) : result;
            }
        }

        protected override void UpdateMaterial()
        {
            if (!IsActive())
                return;

            EnsureCanvasChannels();

            if (m_Material == null) { ResolveMaterialAndTextures(); }
            else { ReleasePooledResources(); }

            // Switching between automatic and explicit materials can change the
            // encoded row even when Unity only marked the material dirty. The style write above set the program.
            if (meshStyleIndex != StyleIndex)
                UpdateGeometry();

            base.UpdateMaterial();

            if (m_Material == null)
            {
                stylePage?.ApplyProgram(canvasRenderer);
                blurSource?.BindRenderedMaterials();
            }

            Sprite textureSprite = sprite != null ? sprite : PatternActive && patternSource == PatternSource.Sprite ? patternSprite : null;
            Texture2D alphaTexture = textureSprite != null ? textureSprite.associatedAlphaSplitTexture : null;
            canvasRenderer.SetAlphaTexture(alphaTexture);
        }
        #endregion

        #region Gradient and Material Pooling
        /// <summary>
        /// Hashes the rendered ramp without allocating Unity's gradient key arrays.
        /// Styler ramps pass their samples, which include the alpha override.
        /// </summary>
        static ulong ComputeGradientHash(Gradient g, Color32[] samples)
        {
            if (g == null)
                return 0;

            unchecked
            {
                ulong hash = 14695981039346656037ul;
                for (int i = 0; i < GradientRes; i++)
                    hash = MixGradientHash(hash, PackColor(samples != null ? samples[i] : g.Evaluate((float)i / (GradientRes - 1))));

                return hash;
            }
        }

        static ulong MixGradientHash(ulong hash, uint value) => unchecked((hash ^ value) * 1099511628211ul);

        static uint PackColor(Color32 color) => (uint)(color.r | color.g << 8 | color.b << 16 | color.a << 24);

        static ulong GetGradientKey(Gradient g, Color32[] samples, bool isLinear)
        {
            if (g == null)
                return 0;

            ulong key = MixGradientHash(ComputeGradientHash(g, samples), isLinear ? 1u : 0u);
            return key != 0 ? key : 1;
        }

        bool IsCanvasLinear()
        {
            if (QualitySettings.activeColorSpace != ColorSpace.Linear)
                return false;
            if (canvas != null)
                return !canvas.vertexColorAlwaysGammaSpace;

            return true;
        }

        static Color SyncColorPrecision(Color col, bool isLinear) => SyncColorPrecision((Color32)col, isLinear);

        // Canvas converts the incoming Color32 to linear, then stores another Color32
        static Color SyncColorPrecision(Color32 col, bool isLinear) => isLinear ? (Color)(Color32)((Color)col).linear : (Color)col;

        static Shader GetDefaultShader()
        {
            if (!cachedShader)
                cachedShader = Shader.Find(ShaderName);

            return cachedShader;
        }

        /// <summary>
        /// Resolves shared gradient textures and the material matching the active visual state.
        /// </summary>
        void ResolveMaterialAndTextures()
        {
            Shader shader = embeddedShader ? embeddedShader : GetDefaultShader();
            if (TryResolveBatchedStyle(shader))
            {
                ReleaseLegacyResources();
                return;
            }

            bool isLinear = IsCanvasLinear();
            bool fillActive = fillCenter;
            bool outlineActive = outlineWidth > 0.001f;
            bool innerShadowActive = InnerShadowActive;
            bool outerShadowActive = OuterShadowActive;

            ulong newFillTexKey = fillActive && IsGradientMode(fillColorMode) ? GetGradientKey(GetLayerGradient(0, fillColorMode, fillGradient), GetStylerGradientPixels(0, fillColorMode), isLinear) : 0;
            ulong newOutlineTexKey = outlineActive && IsGradientMode(outlineColorMode) ? GetGradientKey(GetLayerGradient(1, outlineColorMode, outlineGradient), GetStylerGradientPixels(1, outlineColorMode), isLinear) : 0;
            ulong newInnerShadowTexKey = innerShadowActive && IsGradientMode(innerShadowColorMode) ? GetGradientKey(GetLayerGradient(2, innerShadowColorMode, innerShadowGradient), GetStylerGradientPixels(2, innerShadowColorMode), isLinear) : 0;
            ulong newOuterShadowTexKey = outerShadowActive && IsGradientMode(outerShadowColorMode) ? GetGradientKey(GetLayerGradient(3, outerShadowColorMode, outerShadowGradient), GetStylerGradientPixels(3, outerShadowColorMode), isLinear) : 0;

            UpdatePooledTexture(ref fillTexKey, newFillTexKey, GetLayerGradient(0, fillColorMode, fillGradient),
                GetStylerGradientPixels(0, fillColorMode), isLinear);
            UpdatePooledTexture(ref outlineTexKey, newOutlineTexKey, GetLayerGradient(1, outlineColorMode, outlineGradient),
                GetStylerGradientPixels(1, outlineColorMode), isLinear);
            UpdatePooledTexture(ref innerShadowTexKey, newInnerShadowTexKey, GetLayerGradient(2, innerShadowColorMode, innerShadowGradient),
                GetStylerGradientPixels(2, innerShadowColorMode), isLinear);
            UpdatePooledTexture(ref outerShadowTexKey, newOuterShadowTexKey, GetLayerGradient(3, outerShadowColorMode, outerShadowGradient),
                GetStylerGradientPixels(3, outerShadowColorMode), isLinear);

            var matKey = new MaterialKey(
                new ColorKey(fillActive ? GetMaterialColorMode(fillColorMode, fillGradientSpace, fillGradientType) : 0, fillTexKey,
                    fillGradientAngle, fillGradientZoom, fillGradientReverse,
                    GetLayerColor(fillColorMode, fillColor, fillStyler, stylerFillColor)),
                new ColorKey(outlineActive ? GetMaterialColorMode(outlineColorMode, outlineGradientSpace, outlineGradientType) : 0, outlineTexKey,
                    outlineGradientAngle, outlineGradientZoom, outlineGradientReverse,
                    GetLayerColor(outlineColorMode, outlineColor, outlineStyler, stylerOutlineColor)),
                new ColorKey(innerShadowActive ? GetMaterialColorMode(innerShadowColorMode, innerShadowGradientSpace, innerShadowGradientType) : 0,
                    innerShadowTexKey, innerShadowGradientAngle, innerShadowGradientZoom, innerShadowGradientReverse,
                    GetLayerColor(innerShadowColorMode, innerShadowColor, innerShadowStyler, stylerInnerShadowColor)),
                new ColorKey(outerShadowActive ? GetMaterialColorMode(outerShadowColorMode, outerShadowGradientSpace, outerShadowGradientType) : 0,
                    outerShadowTexKey, outerShadowGradientAngle, outerShadowGradientZoom, outerShadowGradientReverse,
                    GetLayerColor(outerShadowColorMode, outerShadowColor, outerShadowStyler, stylerOuterShadowColor)),
                isLinear, shader, GetOutlineDashData()
            );

            if (Application.isPlaying && HasInteraction)
            {
                ReleasePooledMaterial(currentMatKey);
                currentMatKey = default;
                pooledMaterial = null;

                if (interactionMaterial == null || interactionMaterial.shader != shader)
                {
                    ReleaseInteractionMaterial();
                    interactionMaterial = CreateMaterial(shader, "Procedural Shape (Interaction)");
                }

                if (interactionMaterial != null)
                {
                    ConfigureMaterial(interactionMaterial, matKey, GetPooledTexture(fillTexKey), GetPooledTexture(outlineTexKey),
                        GetPooledTexture(innerShadowTexKey), GetPooledTexture(outerShadowTexKey));
                }

                return;
            }

            ReleaseInteractionMaterial();

            if (!matKey.Equals(currentMatKey) || pooledMaterial == null || !matPool.ContainsKey(matKey))
            {
                ReleasePooledMaterial(currentMatKey);
                currentMatKey = matKey;
                pooledMaterial = AcquirePooledMaterial(matKey,
                    GetPooledTexture(fillTexKey),
                    GetPooledTexture(outlineTexKey),
                    GetPooledTexture(innerShadowTexKey),
                    GetPooledTexture(outerShadowTexKey)
                );
            }
        }

        /// <summary>
        /// Releases every pooled resource currently retained by this instance.
        /// </summary>
        void ReleasePooledResources()
        {
            ReleaseBatchedStyle();
            ReleaseLegacyResources();
        }

        void ReleaseLegacyResources()
        {
            ReleasePooledMaterial(currentMatKey);
            ReleaseInteractionMaterial();
            ReleasePooledTexture(fillTexKey);
            ReleasePooledTexture(outlineTexKey);
            ReleasePooledTexture(innerShadowTexKey);
            ReleasePooledTexture(outerShadowTexKey);

            currentMatKey = default;
            pooledMaterial = null;
            fillTexKey = outlineTexKey = innerShadowTexKey = outerShadowTexKey = 0;
        }

        // Shader modes: 0 = base, 1 = custom; gradients use 2 + type * 2 + space
        static bool IsGradientMode(ColorMode mode) => mode == ColorMode.Gradient || mode == ColorMode.StylerGradient;

        static int GetMaterialColorMode(ColorMode mode, GradientSpace space, GradientType type) => IsGradientMode(mode)
            ? (int)ColorMode.Gradient + (int)type * 2 + (int)space
            : mode == ColorMode.Styler ? (int)ColorMode.Custom : (int)mode;

        void ReleaseInteractionMaterial()
        {
            if (interactionMaterial == null)
                return;

            SafeDestroy(interactionMaterial);
            interactionMaterial = null;
        }

        void UpdatePooledTexture(ref ulong currentKey, ulong newKey, Gradient gradient, Color32[] samples, bool isLinear)
        {
            if (currentKey == newKey && (newKey == 0 || texPool.ContainsKey(newKey)))
                return;

            ReleasePooledTexture(currentKey);
            currentKey = newKey;
            AcquirePooledTexture(newKey, gradient, samples, isLinear);
        }

        static Texture2D GetPooledTexture(ulong key) => key != 0 && texPool.TryGetValue(key, out var entry) ? entry.tex : null;

        void AcquirePooledTexture(ulong key, Gradient g, Color32[] samples, bool isLinear)
        {
            if (key == 0 || g == null)
                return;

            if (texPool.TryGetValue(key, out var entry))
            {
                entry.refCount++;
                return;
            }

            Texture2D tex = new(GradientRes, 1, TextureFormat.RGBA32, mipChain: false, linear: true)
            {
                name = "ProceduralShape_SharedGradient",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            for (int i = 0; i < GradientRes; i++)
            {
                Color32 c = samples != null ? samples[i] : g.Evaluate((float)i / (GradientRes - 1));
                gradientPixels32[i] = SyncColorPrecision(c, isLinear);
            }

            tex.SetPixels32(gradientPixels32);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            texPool[key] = new TexEntry { tex = tex, refCount = 1 };
        }

        void ReleasePooledTexture(ulong key)
        {
            if (key == 0)
                return;

            if (texPool.TryGetValue(key, out var entry))
            {
                entry.refCount--;
                if (entry.refCount <= 0)
                {
                    SafeDestroy(entry.tex);
                    texPool.Remove(key);
                }
            }
        }

        /// <summary>
        /// Acquires or creates a shared material for an immutable material key.
        /// </summary>
        Material AcquirePooledMaterial(MaterialKey key, Texture2D fillTex, Texture2D outlineTex, Texture2D innerShadowTex, Texture2D outerShadowTex)
        {
            if (matPool.TryGetValue(key, out var entry) && entry.mat)
            {
                entry.refCount++;
                return entry.mat;
            }

            matPool.Remove(key);
            Shader shader = key.shader ? key.shader : GetDefaultShader();
            if (!shader)
                return null;

            Material mat = CreateMaterial(shader, "Procedural Shape (Shared)");
            ConfigureMaterial(mat, key, fillTex, outlineTex, innerShadowTex, outerShadowTex);
            matPool[key] = new MatEntry { mat = mat, refCount = 1 };
            return mat;
        }

        static Material CreateMaterial(Shader shader, string materialName)
        {
            if (!shader)
                return null;

            return new Material(shader)
            {
                name = materialName,
                hideFlags = HideFlags.HideAndDontSave,
            };
        }

        static void ConfigureMaterial(Material mat, MaterialKey key, Texture2D fillTex, Texture2D outlineTex,
            Texture2D innerShadowTex, Texture2D outerShadowTex)
        {
            mat.SetVector(ID_OutlineDash, key.outlineDash);
            SetColorProperties(mat, key.fill, fillTex, ID_FillColorMode, ID_FillColor, ID_FillGradientTex,
                ID_FillGradientAngle, ID_FillGradientDirection, ID_FillGradientZoom, ID_FillGradientReverse, key.isLinear);
            SetColorProperties(mat, key.outline, outlineTex, ID_OutlineColorMode, ID_OutlineColor,
                ID_OutlineGradientTex, ID_OutlineGradientAngle, ID_OutlineGradientDirection,
                ID_OutlineGradientZoom, ID_OutlineGradientReverse, key.isLinear);
            SetColorProperties(mat, key.innerShadow, innerShadowTex, ID_InnerShadowColorMode, ID_InnerShadowColor,
                ID_InnerShadowGradientTex, ID_InnerShadowGradientAngle, ID_InnerShadowGradientDirection,
                ID_InnerShadowGradientZoom, ID_InnerShadowGradientReverse, key.isLinear);
            SetColorProperties(mat, key.outerShadow, outerShadowTex, ID_OuterShadowColorMode, ID_OuterShadowColor,
                ID_OuterShadowGradientTex, ID_OuterShadowGradientAngle, ID_OuterShadowGradientDirection,
                ID_OuterShadowGradientZoom, ID_OuterShadowGradientReverse, key.isLinear);
        }

        static void SetColorProperties(Material mat, ColorKey key, Texture2D gradientTex, int colorModeID, int colorID,
            int gradientTexID, int gradientAngleID, int gradientDirID, int gradientZoomID, int gradientReverseID, bool isLinear)
        {
            mat.SetFloat(colorModeID, key.mode);
            mat.SetVector(colorID, SyncColorPrecision(key.color, isLinear));
            mat.SetFloat(gradientAngleID, key.angle);
            mat.SetVector(gradientDirID, GetGradientDirection(key.angle));
            mat.SetFloat(gradientZoomID, key.zoom);
            mat.SetFloat(gradientReverseID, key.reverse ? 1f : 0f);

            if (gradientTex)
                mat.SetTexture(gradientTexID, gradientTex);
        }

        static Vector4 GetGradientDirection(float angle)
        {
            float radians = angle * Mathf.Deg2Rad;
            return new Vector4(Mathf.Cos(radians), Mathf.Sin(radians), 0f, 0f);
        }

        void ReleasePooledMaterial(MaterialKey key)
        {
            if (!key.IsValid)
                return;

            if (matPool.TryGetValue(key, out var entry))
            {
                entry.refCount--;
                if (entry.refCount <= 0)
                {
                    SafeDestroy(entry.mat);
                    matPool.Remove(key);
                }
            }
        }

        static void SafeDestroy(Object obj)
        {
            if (!obj)
                return;

            if (Application.isPlaying) { Destroy(obj); }
            else { DestroyImmediate(obj); }
        }
        #endregion

        #region Batched Style Data
        const int StyleIndexStride = 128;
        // Pages draw with the light shader program until one of their shapes needs the full one
        internal const string LiteShaderKeyword = "EVO_UI_LITE";
        static readonly int ID_ShapeData = Shader.PropertyToID("_ShapeData");
        static readonly int ID_ShapeGradients = Shader.PropertyToID("_ShapeGradientAtlas");
        static readonly int ID_ShapePatterns = Shader.PropertyToID("_ShapePatterns");
        static readonly List<StylePage> stylePages = new();
        static UnityEngine.UI.CanvasUpdateRegistry styleUploadRegistry;
        static bool styleUploadRegistered;
        static bool? styleDataSupported;
        StylePage stylePage;
        int styleSlot = -1;
        int meshStyleIndex;
        // Plain Gradient ramps are sampled again after a change that can alter them (a setter, UpdateRect, a call to
        // SetMaterialDirty, Inspector edits, animation, disabling), not on parent or canvas changes (see gradientRefreshHold)
        bool gradientRampsDirty = true;
        int gradientRefreshHold;

        int StyleIndex => stylePage != null ? styleSlot + 1 : 0;

        public override void SetMaterialDirty()
        {
            if (gradientRefreshHold == 0)
                gradientRampsDirty = true;

            base.SetMaterialDirty();
        }

        bool TryResolveBatchedStyle(Shader shader)
        {
            // Unity validates this enum through reflection, which allocates on every call
            if (!styleDataSupported.HasValue)
                styleDataSupported = SystemInfo.SupportsTextureFormat(TextureFormat.RGBAFloat) && SystemInfo.maxTextureSize >= GradientRes * 5;

            // Explicit materials and older/custom shaders retain their existing behavior
            if (m_Material != null || shader == null || shader.FindPropertyIndex("_ShapeData") < 0 || !styleDataSupported.Value)
            {
                ReleaseBatchedStyle();
                return false;
            }

            if (stylePage == null || !stylePage.material || stylePage.material.shader != shader || !KeepsStylePage())
            {
                ReleaseBatchedStyle();

                for (int i = 0; i < stylePages.Count; i++)
                {
                    StylePage page = stylePages[i];
                    if (page.material && page.material.shader == shader && page.canvas == canvas && page.HasRoom)
                    {
                        stylePage = page;
                        break;
                    }
                }

                if (stylePage == null)
                {
                    stylePage = new StylePage(shader, canvas);
                    stylePages.Add(stylePage);
                }

                styleSlot = stylePage.Acquire(this);
                EnsureStyleUploadCallback();
            }

            // Retain gamma source colors so the upload can follow Canvas color-space
            // changes without rebuilding meshes or replacing shared materials.
            WriteBatchedStyle(gradientRampsDirty);
            gradientRampsDirty = false;
            return true;
        }

        // A page stays while its canvas is disabled above the shape. Disabling a nested canvas is the usual way to hide
        // a panel; moving every shape below it to the parent's page and back would cost far more than the toggle.
        // A canvas inside the disabled one that still draws the shape takes it over.
        bool KeepsStylePage() => stylePage.canvas == canvas || (stylePage.canvas != null
            && !stylePage.canvas.isActiveAndEnabled && transform.IsChildOf(stylePage.canvas.transform)
            && (canvas == null || !canvas.transform.IsChildOf(stylePage.canvas.transform)));

        int GetMeshStyleIndex()
        {
            if (m_Material == null && stylePage == null)
                ResolveMaterialAndTextures();

            meshStyleIndex = m_Material == null ? StyleIndex : 0;
            return meshStyleIndex;
        }

        // Color tweens update the small data texture without rebuilding geometry,
        // changing material identity, or regenerating gradient ramps.
        void SetStylerColorsDirty(int layer = -1)
        {
            if (m_Material == null && stylePage != null && stylePage.material)
                WriteBatchedStyle(false, layer);
            else
                SetMaterialDirty();
        }

        // The material only depends on whether a layer is active; the value itself is vertex and row data
        void SetLayerValueDirty(bool wasActive, bool active)
        {
            SetVerticesDirty();
            if (wasActive != active)
                SetMaterialDirty();
        }

        void WriteBatchedStyle(bool updateGradients, int layer = -1)
        {
            if (layer < 0)
            {
                // WriteBatchedPattern also updates the shader program
                WriteBatchedPattern(updateGradients);
                stylePage.WriteDash(styleSlot, GetOutlineDashData());
                stylePage.WriteShadows(styleSlot, OuterShadowData, InnerShadowData);
            }

            if (layer < 0 || layer == 0)
                stylePage.WriteLayer(styleSlot, 0, fillCenter ? fillColorMode : ColorMode.Base,
                    GetLayerColor(fillColorMode, fillColor, fillStyler, stylerFillColor), fillGradient, fillGradientSpace,
                    fillGradientType, fillGradientAngle, fillGradientZoom, fillGradientReverse, updateGradients,
                    GetStylerGradientPixels(0, fillColorMode));
           
            if (layer < 0 || layer == 1)
                stylePage.WriteLayer(styleSlot, 1, outlineWidth > 0.001f ? outlineColorMode : ColorMode.Base,
                    GetLayerColor(outlineColorMode, outlineColor, outlineStyler, stylerOutlineColor), outlineGradient,
                    outlineGradientSpace, outlineGradientType, outlineGradientAngle, outlineGradientZoom, outlineGradientReverse,
                    updateGradients, GetStylerGradientPixels(1, outlineColorMode));
        
            if (layer < 0 || layer == 2)
                stylePage.WriteLayer(styleSlot, 2, InnerShadowActive ? innerShadowColorMode : ColorMode.Base,
                    GetLayerColor(innerShadowColorMode, innerShadowColor, innerShadowStyler, stylerInnerShadowColor), innerShadowGradient,
                    innerShadowGradientSpace, innerShadowGradientType, innerShadowGradientAngle, innerShadowGradientZoom,
                    innerShadowGradientReverse, updateGradients, GetStylerGradientPixels(2, innerShadowColorMode));
          
            if (layer < 0 || layer == 3)
                stylePage.WriteLayer(styleSlot, 3, OuterShadowActive ? outerShadowColorMode : ColorMode.Base,
                    GetLayerColor(outerShadowColorMode, outerShadowColor, outerShadowStyler, stylerOuterShadowColor), outerShadowGradient,
                    outerShadowGradientSpace, outerShadowGradientType, outerShadowGradientAngle, outerShadowGradientZoom,
                    outerShadowGradientReverse, updateGradients, GetStylerGradientPixels(3, outerShadowColorMode));
        }

        static void UploadStylePages()
        {
            RefreshShapeOperations();
            for (int i = 0; i < stylePages.Count; i++)
                stylePages[i].Upload();
        }

        static void EnsureStyleUploadCallback()
        {
            if (styleUploadRegistered)
                return;

            // Register from OnEnable, before the first mesh rebuild.
            // A callback first added during willRenderCanvases would miss that invocation.
            Canvas.preWillRenderCanvases += CheckStyleUploadOrder;
            SubscribeStyleUpload();
            styleUploadRegistered = true;
        }

        static void SubscribeStyleUpload()
        {
            // The upload must follow the registry's graphic rebuild. uGUI 2.7 recreates the
            // registry when entering play mode without a domain reload, so line up behind the new one.
            styleUploadRegistry = UnityEngine.UI.CanvasUpdateRegistry.instance;
            Canvas.willRenderCanvases -= UploadStylePages;
            Canvas.willRenderCanvases += UploadStylePages;
        }

        static void CheckStyleUploadOrder()
        {
            if (!ReferenceEquals(styleUploadRegistry, UnityEngine.UI.CanvasUpdateRegistry.instance))
                SubscribeStyleUpload();
        }

        // Unity 6.6 clears the Canvas render events when entering play mode without a domain reload
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RestoreStyleUploadCallback()
        {
            if (!styleUploadRegistered)
                return;

            Canvas.preWillRenderCanvases -= CheckStyleUploadOrder;
            Canvas.preWillRenderCanvases += CheckStyleUploadOrder;
            SubscribeStyleUpload();
        }

        /// <summary>
        /// Shapes the light program can't draw. It covers rectangles, ellipses and polygons with every color mode,
        /// outlines, outer shadows, sprites, backdrop blur, dithering, fill clipping and solid rectangle or ellipse patterns.
        /// </summary>
        bool RequiresFullShader => activeOperationCount > 0 || meshHasEffectRow || effectRequests > 0 || InnerShadowActive
            || shape == ShapeType.Star || shape == ShapeType.Heart || shape == ShapeType.Chevron
            || (outlineWidth > 0.001f && outlineDashed && outlineDashGap > 0f)
            || (PatternActive && (patternSource == PatternSource.Sprite || IsGradientMode(patternColorMode)
                || (patternShape != ShapeType.Rectangle && patternShape != ShapeType.Ellipse)
                || (clipMethod != ClipMethod.None && (clipAmount < 1f || UsesRoundedClipEdges))));

        // Disabled shapes keep their style row in play mode, but not their say in the page's program
        void UpdateShaderProgram()
        {
            if (stylePage == null)
                return;

            bool full = isActiveAndEnabled && !operandOnly && RequiresFullShader;
            stylePage.SetFullShader(styleSlot, full);
            // Frosted shapes draw with their own copy, which takes the light program unless one of its own shapes needs more
            blurSource?.SetFullProgram(full);
        }

        /// <summary>
        /// Switches a Procedural Shape material between the light and the full shader program.
        /// </summary>
        internal static void SetLightProgram(Material material, bool lite, Shader shader = null)
        {
            if (material == null || (shader != null && material.shader != shader) || material.IsKeywordEnabled(LiteShaderKeyword) == lite)
                return;

            if (lite) { material.EnableKeyword(LiteShaderKeyword); }
            else { material.DisableKeyword(LiteShaderKeyword); }
        }

        // The material whose program this shape's Soft Mask copies follow: its backdrop copy when that picks its own, else
        // its page. A Soft Mask's source can be a stencil copy that uGUI made before the program last switched.
        internal Material ProgramMaterial => m_Material != null ? null
            : blurSource != null && blurSource.ProgramMaterial != null ? blurSource.ProgramMaterial : stylePage?.material;

        /// <summary>
        /// Gives a Soft Mask copy of a Procedural Shape material the shader program of its source.
        /// </summary>
        internal static void SyncShaderProgram(Material source, Material copy)
        {
            if (source != null && copy != null && source.shader == copy.shader)
                SetLightProgram(copy, source.IsKeywordEnabled(LiteShaderKeyword));
        }

        /// <summary>
        /// Compiles shader programs now instead of when a canvas first draws with them. Each program is a one-time compile
        /// that can freeze WebGL for seconds and mobile for a fraction of one. Nothing calls this automatically: call it from
        /// a loading screen, once the scene or prefab with your shapes has loaded, when the programs would otherwise first
        /// compile mid-game.
        /// </summary>
        /// <param name="fullProgram">The program a canvas switches to while one of its shapes uses an inner shadow, a dashed
        /// outline, a star, heart or chevron, shape operations, Interactive effects, or a sprite or gradient pattern.</param>
        /// <param name="lightProgram">The program a canvas uses otherwise, and the one backdrop blur shapes draw with unless
        /// they need more, even on a canvas that uses the full program. It usually compiles with the first shape shown; pass
        /// true when a frosted panel first appears mid-game on a screen whose canvases all use the full program.</param>
        /// <param name="softMask">Also compiles the chosen programs for shapes inside a Soft Mask: two more per program, one
        /// for a single sprite, rectangle or ellipse mask and one for the rest (nested or other masks), each slower to compile
        /// than the program itself.</param>
        /// <returns>False when the shader isn't loaded yet.</returns>
        public static bool WarmUpShaders(bool fullProgram = true, bool lightProgram = false, bool softMask = false)
        {
            Shader shader = GetDefaultShader();
            if (shader == null)
                return false;

            // One throwaway pixel per program makes the browser or driver build it now. The mesh carries every
            // canvas vertex channel, so the GPU program matches the one the canvas binds later.
            var mesh = new Mesh { name = "Procedural Shape Warm Up", hideFlags = HideFlags.HideAndDontSave };
            using (var vertices = new UnityEngine.UI.VertexHelper())
            {
                UIVertex vertex = UIVertex.simpleVert;
                vertex.position = new Vector3(-1f, -1f);
                vertices.AddVert(vertex);
                vertex.position = new Vector3(-1f, 3f);
                vertices.AddVert(vertex);
                vertex.position = new Vector3(3f, -1f);
                vertices.AddVert(vertex);
                vertices.AddTriangle(0, 1, 2);
                vertices.FillMesh(mesh);
            }

            var lite = new UnityEngine.Rendering.LocalKeyword(shader, LiteShaderKeyword);
            RenderTexture target = RenderTexture.GetTemporary(1, 1, 24);
            var commands = new UnityEngine.Rendering.CommandBuffer { name = "Procedural Shape Warm Up" };
            commands.SetRenderTarget(target);
            commands.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
            var materials = new List<Material>(6);

            for (int program = 0; program < 2; program++)
            {
                bool full = program == 0;
                if (full ? !fullProgram : !lightProgram || !lite.isValid)
                    continue;

                for (int mask = 0; mask < (softMask ? 3 : 1); mask++)
                {
                    Material material = CreateMaterial(shader, "Procedural Shape (Warm Up)");
                    if (lite.isValid)
                        material.SetKeyword(lite, !full);
                    if (mask > 0)
                        material.EnableKeyword(mask == 1 ? SoftMask.BasicSoftMaskKeyword : SoftMask.SoftMaskKeyword);

                    commands.DrawMesh(mesh, Matrix4x4.identity, material);
                    materials.Add(material);
                }
            }

            Graphics.ExecuteCommandBuffer(commands);
            commands.Release();
            RenderTexture.ReleaseTemporary(target);
            SafeDestroy(mesh);

            for (int i = 0; i < materials.Count; i++)
                SafeDestroy(materials[i]);

            return true;
        }

        void ReleaseBatchedStyle()
        {
            if (stylePage == null)
                return;

            StylePage page = stylePage;
            stylePage = null;
            page.Release(styleSlot);
            styleSlot = -1;

            if (page.OwnerCount == 0)
            {
                stylePages.Remove(page);
                page.Dispose();
            }

            if (stylePages.Count == 0)
            {
                Canvas.preWillRenderCanvases -= CheckStyleUploadOrder;
                Canvas.willRenderCanvases -= UploadStylePages;
                styleUploadRegistered = false;
            }
        }

        // Canvas batching multiplies vertex tangents and normals by each graphic's matrix relative to the
        // root canvas. Style rows keep shadow offsets and sizes in local units at any scale or rotation.
        Vector4 OuterShadowData => new(outerShadowOffset.x, outerShadowOffset.y, Mathf.Max(0f, outerShadowSize), Mathf.Max(0f, outerShadowSoftness));
        Vector4 InnerShadowData => new(innerShadowOffset.x, innerShadowOffset.y, Mathf.Max(0f, innerShadowSize), 0f);

        Vector4 GetOutlineDashData()
        {
            return outlineDashed && outlineDashGap > 0f
                ? new Vector4(Mathf.Max(0.1f, outlineDashLength), Mathf.Max(0f, outlineDashGap),
                    Mathf.Repeat(outlineDashOffset, 1f), outlineDashRounded ? 1f : 0f)
                : Vector4.zero;
        }

        void SetOutlineDashDirty(bool updateVertices = false)
        {
            if (updateVertices)
                SetVerticesDirty();

            if (m_Material == null && stylePage != null && stylePage.material)
                stylePage.WriteDash(styleSlot, GetOutlineDashData());
            else
                SetMaterialDirty();
        }

        // Sprite patterns share MainTex with the fill. Never create a per-shape material
        // or silently bind a second texture; incompatible sprites stay disabled.
        public bool PatternSpriteCompatible => patternSprite != null && patternSprite.texture != null
            && (!patternSprite.packed || (patternSprite.packingMode == SpritePackingMode.Rectangle
            && patternSprite.packingRotation == SpritePackingRotation.None))
            && (sprite == null || (sprite.texture == patternSprite.texture
            && sprite.associatedAlphaSplitTexture == patternSprite.associatedAlphaSplitTexture));

        bool PatternActive => fillCenter && fillPattern && m_Material == null && (patternSource == PatternSource.Shape || PatternSpriteCompatible);

        static Vector2 ClampPatternOrigin(Vector2 origin) => new(Mathf.Clamp01(origin.x), Mathf.Clamp01(origin.y));

        // Offset, angle and color animation change data only, without a Canvas mesh rebuild
        void SetPatternDirty(bool updateGradient = false)
        {
            if (!PatternActive)
                return;

            if (stylePage != null && stylePage.material) { WriteBatchedPattern(updateGradient); }
            else { SetMaterialDirty(); }
        }

        void WriteBatchedPattern(bool updateGradient)
        {
            // Tile shapes and colors can decide the program without a mesh rebuild
            UpdateShaderProgram();
            if (!PatternActive)
                return;

            Vector2 size = Vector2.Max(patternSize, Vector2.one * 0.1f);
            Vector2 period = size + Vector2.Max(patternGap, Vector2.zero);
            float radians = Mathf.Repeat(patternAngle, 360f) * Mathf.Deg2Rad;
            float radius = Mathf.Clamp(patternRadius, 0f, Mathf.Min(size.x, size.y) * 0.5f);
            Vector2 origin = ClampPatternOrigin(patternFalloffOrigin);
            float start = Mathf.Max(0f, patternFalloffStart);
            float end = Mathf.Max(start + 0.001f, patternFalloffEnd);
            bool useSprite = patternSource == PatternSource.Sprite;
            int flags = (sprite == null ? 2 : 0) + (patternFalloff ? 4 : 0);
            Vector4 uv = Vector4.zero;

            if (useSprite)
            {
                // Preserve the repeat interval while fitting the sprite inside its tile bounds
                size = CalculateFitRect(new Rect(Vector2.zero, size), patternSprite).size;
                uv = UnityEngine.Sprites.DataUtility.GetOuterUV(patternSprite);
                // Clamp to texel centers so filtering cannot bleed into neighboring sprites
                Vector2 inset = new(0.5f / patternSprite.texture.width, 0.5f / patternSprite.texture.height);
                uv = new Vector4(uv.x + inset.x, uv.y + inset.y, uv.z - inset.x, uv.w - inset.y);
                if (patternSprite.associatedAlphaSplitTexture != null)
                    flags |= 1;
            }

            stylePage.WritePattern(styleSlot,
                new Vector4(1f / period.x, 1f / period.y, size.x * 0.5f, size.y * 0.5f),
                new Vector4(Mathf.Cos(radians), Mathf.Sin(radians), Mathf.Repeat(patternOffset.x, period.x), Mathf.Repeat(patternOffset.y, period.y)),
                new Vector4(useSprite ? -1f : GetShaderShapeID(patternShape, patternPolygonSides), radius, flags, 0f),
                uv, new Vector4(origin.x, origin.y, start, 1f / (end - start)));
            stylePage.WriteLayer(styleSlot, 4, patternColorMode,
                GetLayerColor(patternColorMode, patternColor, patternStyler, stylerPatternColor), patternGradient,
                patternGradientSpace, patternGradientType, patternGradientAngle, patternGradientZoom, patternGradientReverse,
                updateGradient, GetStylerGradientPixels(4, patternColorMode));
        }

        sealed class StylePage
        {
            const int PatternDataWidth = 7;
            const int OperationDataWidth = PatternDataWidth + 1 + MaxShapeOperations * 5;
            const int GradientLayers = 5;
            public readonly Canvas canvas;
            public readonly Material material;
            public readonly bool optimizedData;
            readonly UnityEngine.Rendering.LocalKeyword liteKeyword;
            // The plain Gradient each slot's ramps were last sampled from, per layer
            Gradient[] writtenGradients;
            // Backdrop copies pick their program themselves (BackdropBlurSource.SetFullProgram)
            readonly UnityEngine.Rendering.LocalKeyword backdropKeyword;
            ProceduralShape[] owners;
            bool[] fullSlots;
            int fullCount;
            readonly Texture2D dataTexture, gradientTexture, patternTexture;
            NativeArray<Color> dataStorage, patternStorage;
            NativeArray<Color32> gradientStorage;
            Color[] patternPixels;
            Color[] dataPixels;
            Color32[] gradientPixels;
            int patternWidth = PatternDataWidth;
            int[] freeSlots, gradientRows, freeGradientRows, patternRows, freePatternRows;
            readonly int maxCapacity, dataWidth;
            int freeCount, capacity, gradientCapacity, nextSlot, freeGradientCount, nextGradientRow;
            int patternCapacity, freePatternCount, nextPatternRow;
            bool patternsDirty, dataDirty, gradientsDirty, linearColors, rebuildData, rebuildGradients, rebuildPatterns;

            public int OwnerCount { get; private set; }
            public bool HasRoom => freeCount > 0 || nextSlot < maxCapacity;
            int PatternRows => patternRows != null ? patternCapacity : capacity;

            public StylePage(Shader shader, Canvas ownerCanvas)
            {
                canvas = ownerCanvas;
                // Older custom shaders keep their gamma atlas contract.
                int version = shader.FindPropertyIndex("_ShapeDataVersion");
                optimizedData = version >= 0;
                linearColors = UsesLinearColors();
                // Five ramps share one row. Reserve row zero in the style data texture.
                maxCapacity = Mathf.Max(1, SystemInfo.maxTextureSize - 1);
                capacity = Mathf.Min(32, maxCapacity);
                freeSlots = new int[capacity];
                owners = new ProceduralShape[capacity];
                fullSlots = new bool[capacity];
                writtenGradients = new Gradient[capacity * GradientLayers];

                // Version 2 shaders read gradient and pattern rows from the style row, so only styles
                // that use them take rows. Older shaders address both by style row.
                if (optimizedData && shader.GetPropertyType(version) == UnityEngine.Rendering.ShaderPropertyType.Float
                    && shader.GetPropertyDefaultFloatValue(version) >= 2f)
                {
                    gradientRows = new int[capacity];
                    freeGradientRows = new int[capacity];
                    patternRows = new int[capacity];
                    freePatternRows = new int[capacity];
                }

                // Version 2 rows add the pattern row texel
                dataWidth = patternRows != null ? 12 : 11;
                dataPixels = new Color[dataWidth * (capacity + 1)];
                dataTexture = new Texture2D(dataWidth, capacity + 1, TextureFormat.RGBAFloat, false, true)
                {
                    name = "ProceduralShape_StyleData",
                    hideFlags = HideFlags.HideAndDontSave,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Point
                };
                gradientTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false, true)
                {
                    name = "ProceduralShape_GradientAtlas",
                    hideFlags = HideFlags.HideAndDontSave,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                // Bind a tiny placeholder before any stencil/soft-mask material is copied.
                // Allocate rows on first use while retaining the same Texture2D identity.
                patternTexture = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true)
                {
                    name = "ProceduralShape_PatternData",
                    hideFlags = HideFlags.HideAndDontSave,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Point
                };
                patternTexture.SetPixel(0, 0, Color.clear);
                patternTexture.Apply(false, false);
                material = CreateMaterial(shader, "Procedural Shape");
                // Custom shaders without the light program keep drawing with their only one
                liteKeyword = new UnityEngine.Rendering.LocalKeyword(shader, LiteShaderKeyword);
                backdropKeyword = new UnityEngine.Rendering.LocalKeyword(shader, BackdropBlurSource.ShapeKeyword);
                if (liteKeyword.isValid)
                    material.SetKeyword(liteKeyword, true);
                material.SetTexture(ID_ShapeData, dataTexture);
                material.SetTexture(ID_ShapeGradients, gradientTexture);
                material.SetTexture(ID_ShapePatterns, patternTexture);
                gradientTexture.SetPixel(0, 0, Color.white);
                gradientTexture.Apply(false, false);
                dataStorage = dataTexture.GetPixelData<Color>(0);
                patternStorage = patternTexture.GetPixelData<Color>(0);
                gradientStorage = gradientTexture.GetPixelData<Color32>(0);
                UpdateHeader();
            }

            public int Acquire(ProceduralShape owner)
            {
                int slot;
                if (freeCount > 0) { slot = freeSlots[--freeCount]; }
                else
                {
                    if (nextSlot == capacity)
                        Grow();

                    slot = nextSlot++;
                }

                owners[slot] = owner;
                OwnerCount++;
                return slot;
            }

            public void Release(int slot)
            {
                if (gradientRows != null && gradientRows[slot] != 0)
                    ReleaseGradientRow(slot);

                SetFullShader(slot, false);
                owners[slot] = null;
                ForgetGradients(slot);
                ReleasePatternRow(slot);
                freeSlots[freeCount++] = slot;
                OwnerCount--;
            }

            // One material per page keeps batching as it was. The program switches only when the first shape that
            // needs the full one arrives or the last one leaves; the keyword also goes to the stencil and Soft Mask
            // copies the page's shapes render with, so no material is rebuilt. Backdrop copies pick their own.
            public void SetFullShader(int slot, bool full)
            {
                if (fullSlots[slot] == full || !liteKeyword.isValid)
                    return;

                fullSlots[slot] = full;
                fullCount += full ? 1 : -1;
                if (fullCount != (full ? 1 : 0))
                    return;

                material.SetKeyword(liteKeyword, !full);

                for (int i = 0; i < nextSlot; i++)
                {
                    if (owners[i] != null)
                        ApplyProgram(owners[i].canvasRenderer);
                }
            }

            // Most shapes render the page material itself; only their stencil and Soft Mask copies need the keyword.
            // Also run after each material update, since uGUI reuses a stencil copy made before the last switch.
            public void ApplyProgram(CanvasRenderer renderer)
            {
                if (!liteKeyword.isValid)
                    return;

                bool lite = fullCount == 0;
                int count = renderer.materialCount;

                for (int m = 0; m < count; m++)
                    SetCopyKeyword(renderer.GetMaterial(m), lite);

                int pops = renderer.popMaterialCount;

                for (int m = 0; m < pops; m++)
                    SetCopyKeyword(renderer.GetPopMaterial(m), lite);
            }

            // Copies of this page share its data texture. A shape moving to another page still renders with that page's
            // material until its update finishes, and backdrop copies pick their own program.
            void SetCopyKeyword(Material copy, bool lite)
            {
                if (ReferenceEquals(copy, material) || copy == null || copy.shader != material.shader
                    || copy.GetTexture(ID_ShapeData) != dataTexture || (backdropKeyword.isValid && copy.IsKeywordEnabled(backdropKeyword))
                    || copy.IsKeywordEnabled(liteKeyword) == lite)
                    return;

                copy.SetKeyword(liteKeyword, lite);
            }

            void Grow()
            {
                capacity = Mathf.Min(capacity * 2, maxCapacity);
                System.Array.Resize(ref dataPixels, dataWidth * (capacity + 1));
                System.Array.Resize(ref freeSlots, capacity);
                System.Array.Resize(ref owners, capacity);
                System.Array.Resize(ref fullSlots, capacity);
                System.Array.Resize(ref writtenGradients, capacity * GradientLayers);

                if (gradientRows != null)
                {
                    System.Array.Resize(ref gradientRows, capacity);
                    System.Array.Resize(ref freeGradientRows, capacity);
                    System.Array.Resize(ref patternRows, capacity);
                    System.Array.Resize(ref freePatternRows, capacity);
                }
                else if (patternPixels != null) { ResizePatternTexture(); }

                // Keep the Texture2D objects: stencil/soft-mask material copies still reference them.
                // The header supplies dimensions to every copy.
                dataTexture.Reinitialize(dataWidth, capacity + 1);
                dataStorage = dataTexture.GetPixelData<Color>(0);
                UpdateHeader();
            }

            // Gradient rows are stored one-based so resized arrays start without a row
            int GetGradientRow(int slot)
            {
                if (gradientRows == null)
                    return slot;

                int row = gradientRows[slot] - 1;
                if (row >= 0)
                    return row;

                row = freeGradientCount > 0 ? freeGradientRows[--freeGradientCount] : nextGradientRow++;
                gradientRows[slot] = row + 1;
                int offset = (slot + 1) * dataWidth + 10;
                Color value = dataPixels[offset];
                value.a = row;
                WriteDataValue(offset, value);
                return row;
            }

            void ReleaseGradientRow(int slot)
            {
                freeGradientRows[freeGradientCount++] = gradientRows[slot] - 1;
                gradientRows[slot] = 0;
                ForgetGradients(slot);
            }

            void ForgetGradients(int slot) => System.Array.Clear(writtenGradients, slot * GradientLayers, GradientLayers);

            bool UsesGradients(int slot)
            {
                int offset = (slot + 1) * dataWidth + 1;
                for (int layer = 0; layer < 4; layer++)
                {
                    if (dataPixels[offset + layer * 2].b >= (int)ColorMode.Gradient)
                        return true;
                }

                int row = patternRows[slot] - 1;
                return row >= 0 && patternPixels != null && patternPixels[(row + 1) * patternWidth + 1].b >= (int)ColorMode.Gradient;
            }

            // Pattern rows follow the gradient row scheme; row zero of the texture stays the header
            int GetPatternRow(int slot)
            {
                if (patternRows == null)
                    return slot;

                int row = patternRows[slot] - 1;
                if (row >= 0)
                    return row;

                row = freePatternCount > 0 ? freePatternRows[--freePatternCount] : nextPatternRow++;
                patternRows[slot] = row + 1;
                WriteDataValue((slot + 1) * dataWidth + 11, new Vector4(row, 0f, 0f, 0f));
                if (row >= patternCapacity)
                {
                    patternCapacity = Mathf.Min(Mathf.NextPowerOfTwo(row + 1), maxCapacity);
                    if (patternPixels != null)
                        ResizePatternTexture();
                }

                return row;
            }

            public void ReleasePatternRow(int slot)
            {
                if (patternRows == null || patternRows[slot] == 0)
                    return;

                freePatternRows[freePatternCount++] = patternRows[slot] - 1;
                patternRows[slot] = 0;
            }

            void EnsureGradientCapacity(int row)
            {
                if (row < gradientCapacity)
                    return;

                gradientCapacity = Mathf.Min(Mathf.NextPowerOfTwo(row + 1), maxCapacity);
                System.Array.Resize(ref gradientPixels, GradientRes * gradientCapacity * GradientLayers);
                gradientTexture.Reinitialize(GradientRes * GradientLayers, gradientCapacity, GradientFormat, false);
                gradientStorage = gradientTexture.GetPixelData<Color32>(0);
                UpdateHeader();
                gradientsDirty = true;
                rebuildGradients = true;
            }

            void UpdateHeader()
            {
                // UV (0,0) always addresses this texel even after growth
                dataPixels[0] = new Color(1f / (capacity + 1), 1f / Mathf.Max(1, gradientCapacity), GradientLayers, 0f);
                dataDirty = true;
                rebuildData = true;
            }

            public void WriteDash(int slot, Vector4 dash) => WriteDataValue((slot + 1) * dataWidth + 8, dash);

            public void WriteShadows(int slot, Vector4 outer, Vector4 inner)
            {
                int offset = (slot + 1) * dataWidth + 9;
                WriteDataValue(offset, outer);
                // The inner shadow's W holds the gradient row
                inner.w = dataPixels[offset + 1].a;
                WriteDataValue(offset + 1, inner);
            }

            void WriteDataValue(int offset, Vector4 value)
            {
                Color color = new(value.x, value.y, value.z, value.w);
                if (dataPixels[offset].Equals(color))
                    return;

                dataPixels[offset] = color;
                var pixels = dataStorage;
                pixels[offset] = color;
                dataDirty = true;
            }

            public void WriteLayer(int slot, int layer, ColorMode colorMode, Color color, Gradient gradient, GradientSpace space,
                GradientType type, float angle, float zoom, bool reverse, bool updateGradient, Color32[] samples = null)
            {
                int mode = GetMaterialColorMode(colorMode, space, type);
                bool pattern = layer == 4;
                int offset = pattern ? (GetPatternRow(slot) + 1) * patternWidth : (slot + 1) * dataWidth + layer * 2;
                Color[] pixels = pattern ? patternPixels : dataPixels;
                Color resolvedColor = mode == (int)ColorMode.Custom ? (Color)(Color32)color : default;
                Color config = new(0f, 0f, mode, 0f);

                if (IsGradientMode(colorMode))
                {
                    float radians = Mathf.Repeat(angle, 360f) * Mathf.Deg2Rad;
                    config.r = Mathf.Cos(radians);
                    config.g = Mathf.Sin(radians);
                    config.a = (reverse ? -1f : 1f) * Mathf.Max(zoom, 0.01f);
                }

                if (!pixels[offset].Equals(resolvedColor) || !pixels[offset + 1].Equals(config))
                {
                    pixels[offset] = resolvedColor;
                    pixels[offset + 1] = config;
                    var uploaded = pattern ? patternStorage : dataStorage;
                    uploaded[offset] = linearColors ? (Color)LinearColor32(resolvedColor) : resolvedColor;
                    uploaded[offset + 1] = config;
                    if (pattern) { patternsDirty = true; }
                    else { dataDirty = true; }
                }

                if (!IsGradientMode(colorMode))
                {
                    if (gradientRows != null && gradientRows[slot] != 0 && !UsesGradients(slot))
                        ReleaseGradientRow(slot);

                    return;
                }

                // A newly assigned row may still hold another style's ramp. Styler ramps arrive as samples, which are
                // cheap to compare; a plain Gradient is sampled again when it may have changed, or for another Gradient.
                // A null Gradient (drawn white) can't be told apart from a forgotten entry, so it is always written
                if (updateGradient || samples != null || gradient == null || (gradientRows != null && gradientRows[slot] == 0)
                    || !ReferenceEquals(writtenGradients[slot * GradientLayers + layer], gradient))
                    WriteGradient(slot, layer, gradient, samples);
            }

            public void WriteGradient(int slot, int layer, Gradient gradient, Color32[] samples)
            {
                writtenGradients[slot * GradientLayers + layer] = samples == null ? gradient : null;
                int row = GetGradientRow(slot);
                EnsureGradientCapacity(row);
                int start = (row * GradientLayers + layer) * GradientRes;
                var pixels = gradientStorage;
                for (int i = 0; i < GradientRes; i++)
                {
                    Color32 sample = samples != null ? samples[i] : gradient != null ? gradient.Evaluate((float)i / (GradientRes - 1)) : Color.white;
                    if (PackColor(gradientPixels[start + i]) == PackColor(sample))
                        continue;

                    gradientPixels[start + i] = sample;
                    pixels[start + i] = sample;
                    gradientsDirty = true;
                }
            }

            // Pattern storage is created only when a pattern is used. Keep texture identity
            // until the page is disposed, because stencil/soft-mask copies reference it too.
            void EnsurePatternTexture(bool operations = false)
            {
                int width = operations ? OperationDataWidth : patternWidth;
                if (patternPixels != null && patternWidth == width)
                    return;

                Color[] pixels = new Color[width * (PatternRows + 1)];
                if (patternPixels != null)
                {
                    for (int row = 1; row <= PatternRows; row++)
                        System.Array.Copy(patternPixels, row * patternWidth, pixels, row * width, patternWidth);
                }

                patternPixels = pixels;
                patternWidth = width;
                patternTexture.Reinitialize(patternWidth, PatternRows + 1);
                patternStorage = patternTexture.GetPixelData<Color>(0);
                UpdatePatternHeader();
            }

            void ResizePatternTexture()
            {
                System.Array.Resize(ref patternPixels, patternWidth * (PatternRows + 1));
                patternTexture.Reinitialize(patternWidth, PatternRows + 1);
                patternStorage = patternTexture.GetPixelData<Color>(0);
                UpdatePatternHeader();
            }

            void UpdatePatternHeader()
            {
                patternPixels[0] = new Color(1f / (PatternRows + 1), 1f / patternWidth, 0f, 0f);
                patternsDirty = true;
                rebuildPatterns = true;
            }

            public void WritePattern(int slot, Vector4 size, Vector4 transform, Vector4 source, Vector4 uv, Vector4 falloff)
            {
                int row = GetPatternRow(slot);
                EnsurePatternTexture();
                int offset = (row + 1) * patternWidth + 2;
                WritePatternValue(offset, size);
                WritePatternValue(offset + 1, transform);
                WritePatternValue(offset + 2, source);
                WritePatternValue(offset + 3, uv);
                WritePatternValue(offset + 4, falloff);
            }

            public void WriteOperation(int slot, int index, OperationData data)
            {
                int row = GetPatternRow(slot);
                EnsurePatternTexture(true);
                int offset = (row + 1) * patternWidth + PatternDataWidth + 1 + index * 5;
                WritePatternValue(offset, data.rowX);
                WritePatternValue(offset + 1, data.rowY);
                WritePatternValue(offset + 2, data.shape);
                WritePatternValue(offset + 3, data.radii);
                WritePatternValue(offset + 4, data.clip);
            }

            public void WriteOperationCount(int slot, int count)
            {
                WritePatternValue((GetPatternRow(slot) + 1) * patternWidth + PatternDataWidth, new Vector4(count, 0f, 0f, 0f));
            }

            void WritePatternValue(int offset, Vector4 value)
            {
                Color color = new(value.x, value.y, value.z, value.w);
                if (patternPixels[offset].Equals(color))
                    return;

                patternPixels[offset] = color;
                var pixels = patternStorage;
                pixels[offset] = color;
                patternsDirty = true;
            }

            bool UsesLinearColors() => optimizedData && QualitySettings.activeColorSpace == ColorSpace.Linear
                && (canvas == null || !canvas.vertexColorAlwaysGammaSpace);

            // Linear pages keep gamma ramps in an sRGB atlas: sampling decodes them exactly,
            // so dark ramps avoid 8-bit linear banding. Solid colors match Canvas quantization.
            GraphicsFormat GradientFormat => linearColors ? GraphicsFormat.R8G8B8A8_SRGB : GraphicsFormat.R8G8B8A8_UNorm;

            public void Upload()
            {
                bool linear = UsesLinearColors();
                if (linearColors != linear)
                {
                    linearColors = linear;
                    dataDirty = true;
                    patternsDirty = patternPixels != null;
                    gradientsDirty = gradientPixels != null;
                    rebuildData = rebuildPatterns = rebuildGradients = true;

                    // Keep the Texture2D object: stencil/soft-mask material copies still reference it.
                    if (gradientsDirty)
                    {
                        gradientTexture.Reinitialize(GradientRes * GradientLayers, gradientCapacity, GradientFormat, false);
                        gradientStorage = gradientTexture.GetPixelData<Color32>(0);
                    }
                }

                // Changed entries already reside in Texture2D's CPU storage. Restore the
                // complete source only after reallocation or a Canvas color-space change.
                if (patternsDirty)
                {
                    if (rebuildPatterns)
                    {
                        var pixels = patternStorage;
                        pixels.CopyFrom(patternPixels);
                        if (linearColors)
                        {
                            int rows = patternRows != null ? nextPatternRow : nextSlot;
                            for (int row = 0; row < rows; row++)
                            {
                                int offset = (row + 1) * patternWidth;
                                pixels[offset] = LinearColor32(patternPixels[offset]);
                            }
                        }
                    }

                    patternTexture.Apply(false, false);
                    patternStorage = patternTexture.GetPixelData<Color>(0);
                    patternsDirty = false;
                    rebuildPatterns = false;
                }

                if (dataDirty)
                {
                    if (rebuildData)
                    {
                        var pixels = dataStorage;
                        pixels.CopyFrom(dataPixels);
                        if (linearColors)
                        {
                            for (int slot = 0; slot < nextSlot; slot++)
                            {
                                int offset = (slot + 1) * dataWidth;
                                for (int layer = 0; layer < 4; layer++)
                                    pixels[offset + layer * 2] = LinearColor32(dataPixels[offset + layer * 2]);
                            }
                        }
                    }

                    dataTexture.Apply(false, false);
                    dataStorage = dataTexture.GetPixelData<Color>(0);
                    dataDirty = false;
                    rebuildData = false;
                }

                if (gradientsDirty)
                {
                    if (rebuildGradients)
                    {
                        var pixels = gradientStorage;
                        pixels.CopyFrom(gradientPixels);
                    }

                    gradientTexture.Apply(false, false);
                    gradientStorage = gradientTexture.GetPixelData<Color32>(0);
                    gradientsDirty = false;
                    rebuildGradients = false;
                }
            }

            public void Dispose()
            {
                SafeDestroy(material);
                SafeDestroy(dataTexture);
                SafeDestroy(gradientTexture);
                SafeDestroy(patternTexture);
            }
        }
        #endregion

        #region Pool Entry and Key Types
        /// <summary>
        /// Stores a shared material and the number of active owners.
        /// </summary>
        class MatEntry
        {
            public Material mat;
            public int refCount;
        }

        /// <summary>
        /// Stores a shared gradient texture and the number of active owners.
        /// </summary>
        class TexEntry
        {
            public Texture2D tex;
            public int refCount;
        }

        /// <summary>
        /// Identifies the material state for one color-capable visual layer.
        /// </summary>
        readonly struct ColorKey : System.IEquatable<ColorKey>
        {
            public readonly int mode;
            public readonly ulong texKey;
            public readonly float angle, zoom;
            public readonly bool reverse;
            public readonly Color32 color;

            public bool UsesCustomColor => mode == (int)ColorMode.Custom;

            public ColorKey(int colorMode, ulong gradientTexKey, float gradientAngle, float gradientZoom, bool gradientReverse, Color customColor)
            {
                mode = Mathf.Clamp(colorMode, (int)ColorMode.Base, (int)ColorMode.Gradient + (int)GradientType.Angular * 2 + (int)GradientSpace.ScreenSpace);
                bool isGradient = mode >= (int)ColorMode.Gradient;
                texKey = isGradient ? gradientTexKey : 0;
                angle = isGradient ? Mathf.Repeat(gradientAngle, 360f) : 0f;
                zoom = isGradient ? Mathf.Max(gradientZoom, 0.01f) : 0f;
                reverse = isGradient && gradientReverse;
                color = mode == (int)ColorMode.Custom ? (Color32)customColor : default;
            }

            public bool Equals(ColorKey o)
            {
                return mode == o.mode && texKey == o.texKey && angle == o.angle && zoom == o.zoom && reverse == o.reverse && PackColor(color) == PackColor(o.color);
            }

            public override int GetHashCode() => System.HashCode.Combine(mode, texKey, angle, zoom, reverse, PackColor(color));
        }

        /// <summary>
        /// Identifies a pooled material across all four visual layers and color-space state.
        /// </summary>
        readonly struct MaterialKey : System.IEquatable<MaterialKey>
        {
            public readonly ColorKey fill, outline, innerShadow, outerShadow;
            public readonly bool isLinear;
            public readonly Shader shader;
            public readonly Vector4 outlineDash;

            public bool IsValid => shader != null;

            public MaterialKey(ColorKey fillKey, ColorKey outlineKey, ColorKey innerShadowKey, ColorKey outerShadowKey, bool lin, Shader sh, Vector4 dash)
            {
                fill = fillKey;
                outline = outlineKey;
                innerShadow = innerShadowKey;
                outerShadow = outerShadowKey;
                isLinear = lin && (fill.UsesCustomColor || outline.UsesCustomColor || innerShadow.UsesCustomColor || outerShadow.UsesCustomColor);
                shader = sh;
                outlineDash = dash;
            }

            public bool Equals(MaterialKey o)
            {
                return fill.Equals(o.fill) && outline.Equals(o.outline) && innerShadow.Equals(o.innerShadow) && outerShadow.Equals(o.outerShadow)
                    && isLinear == o.isLinear && shader == o.shader && outlineDash.Equals(o.outlineDash);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + fill.GetHashCode();
                    hash = hash * 31 + outline.GetHashCode();
                    hash = hash * 31 + innerShadow.GetHashCode();
                    hash = hash * 31 + outerShadow.GetHashCode();
                    hash = hash * 31 + isLinear.GetHashCode();
                    hash = hash * 31 + (shader ? shader.GetHashCode() : 0);
                    hash = hash * 31 + outlineDash.GetHashCode();
                    return hash;
                }
            }
        }
        #endregion
    }
}