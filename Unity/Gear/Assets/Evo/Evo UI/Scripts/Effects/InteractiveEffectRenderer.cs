using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Evo.UI
{
    /// <summary>
    /// Runtime mesh payload. Targets share a float atlas, clock and material cache.
    /// </summary>
    [DisallowMultipleComponent, NoAutoStaticsCleanup, AddComponentMenu("")]
    public sealed class InteractiveEffectRenderer : BaseMeshEffect, IMaterialModifier
    {
        // Constants
        const int Limit = Ripple.MaximumRipples;
        const int PointerColumn = 1 + Limit * 3;
        const int BorderColumn = PointerColumn + 2;
        const int Width = BorderColumn + 4;
        const int ShapeStride = 512; // Procedural Shape owns the low nine bits of UV2.w

        // Shader IDs
        static readonly int SupportId = Shader.PropertyToID("_EvoEffectsSupport");
        static readonly int DataId = Shader.PropertyToID("_EvoEffectsData");
        static readonly int SizeId = Shader.PropertyToID("_EvoEffectsDataSize");
        static readonly int TimeId = Shader.PropertyToID("_EvoEffectsTime");
        static readonly int ExternalAlphaId = Shader.PropertyToID("_EnableExternalAlpha");

        // State
        static readonly List<InteractiveEffectRenderer> residents = new();
        static readonly List<InteractiveEffectRenderer> active = new();
        static readonly Stack<int> freeRows = new();
        static readonly Dictionary<Material, MaterialEntry> materials = new();
        static readonly Canvas.WillRenderCanvases tickCallback = Tick;
        static readonly Canvas.WillRenderCanvases uploadCallback = Upload;

        static Texture2D atlas;
        static CanvasUpdateRegistry uploadRegistry;
        static int nextRow = 1;
        static bool dirty;
        static double epoch;

        readonly Wave[] waves = new Wave[Limit];
        readonly List<PointerLight> lights = new(2);
        readonly Stack<PointerLight> spareLights = new(2);
        int count;
        int row;
        int activeIndex = -1;
        Shader effectShader;
        Material sourceMaterial;
        MaterialEntry materialEntry;
        ProceduralShape shape;
        Rect meshRect;
        Vector4 spriteUV = new(0, 0, 1, 1);
        float lastTick = -1f;
        float groupAlpha = 1f;
        bool keepMesh, restoreCulling;

        // Properties
        public int ActiveRippleCount => count;
        Rect DrawingRect => shape != null ? shape.GetDrawingRect() : meshRect.width > 0f ? meshRect : graphic.GetPixelAdjustedRect();

        protected override void Awake()
        {
            base.Awake();
            TryGetComponent(out shape);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            Prepare();
        }

        protected override void OnDisable()
        {
            Release();
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            Release();
            base.OnDestroy();
        }

        protected override void OnCanvasHierarchyChanged()
        {
            base.OnCanvasHierarchyChanged();
            EnsureChannels();
        }

        // Unity sends both synchronously with the updated inherited alpha
        protected override void OnCanvasGroupChanged()
        {
            base.OnCanvasGroupChanged();
            RefreshGroupAlpha();
        }

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            RefreshGroupAlpha();
        }

        internal void Initialize(Shader shader)
        {
            if (effectShader != shader)
            {
                ReleaseMaterial();
                effectShader = shader;

                if (graphic != null)
                    graphic.SetMaterialDirty();
            }

            Prepare();
        }

        bool Prepare()
        {
            if (row != 0)
                return true;

            if (effectShader == null || !IsActive() || graphic == null || !graphic.isActiveAndEnabled
                || !SystemInfo.SupportsTextureFormat(TextureFormat.RGBAFloat) || !AllocateRow())
                return false;

            residents.Add(this);
            EnsureChannels();
            WriteRow();
            graphic.SetVerticesDirty();
            graphic.SetMaterialDirty();
            return true;
        }

        void Release()
        {
            Clear();

            if (row != 0)
            {
                freeRows.Push(row);
                row = 0;
                residents.Remove(this);
            }

            ReleaseMaterial();

            if (residents.Count == 0)
                ReleaseAtlas();
        }

        #region Mesh
        void EnsureChannels()
        {
            if (graphic != null && graphic.canvas != null && shape == null)
                graphic.canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || row == 0)
                return;

            // Actual mesh bounds account for Image.preserveAspect and sliced/filled geometry.
            // Filled images use the unfilled rect for stable pointer coordinates.
            if (shape == null && vh.currentVertCount > 0)
            {
                UIVertex point = default;
                Vector2 minimum = new(float.MaxValue, float.MaxValue);
                Vector2 maximum = new(float.MinValue, float.MinValue);

                for (int i = 0; i < vh.currentVertCount; i++)
                {
                    vh.PopulateUIVertex(ref point, i);
                    minimum = Vector2.Min(minimum, point.position);
                    maximum = Vector2.Max(maximum, point.position);
                }

                meshRect = Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);

                if (graphic is Image image)
                {
                    Sprite sprite = image.overrideSprite;
                    spriteUV = sprite != null ? UnityEngine.Sprites.DataUtility.GetOuterUV(sprite) : new Vector4(0, 0, 1, 1);

                    if (image.type == Image.Type.Filled)
                        meshRect = graphic.GetPixelAdjustedRect();
                }
                else if (graphic is RawImage raw)
                {
                    Rect uv = raw.uvRect;
                    spriteUV = new Vector4(Mathf.Min(uv.xMin, uv.xMax), Mathf.Min(uv.yMin, uv.yMax),
                        Mathf.Max(uv.xMin, uv.xMax), Mathf.Max(uv.yMin, uv.yMax));
                }
            }

            Rect rect = DrawingRect;
            if (rect.width <= 0.001f || rect.height <= 0.001f)
                return;

            UIVertex vertex = default;
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);

                if (shape != null)
                {
                    vertex.uv2.w = Mathf.Repeat(vertex.uv2.w, ShapeStride) + row * ShapeStride;
                    shape.meshHasEffectRow = true;
                }
                else
                {
                    vertex.uv1 = new Vector4((vertex.position.x - rect.center.x) / rect.width,
                        (vertex.position.y - rect.center.y) / rect.height, row, 0f);
                }

                vh.SetUIVertex(vertex, i);
            }

            // Resizing reuses normalized origins and recalculates coverage radii
            WriteRow();
        }
        #endregion

        #region Animation
        internal void Emit(Ripple.Settings settings, PointerEventData pointer)
        {
            if (!IsActive() || graphic == null || !graphic.isActiveAndEnabled || settings.ResolvedColor.a <= 0f
                || graphic.canvas == null || !graphic.canvas.isActiveAndEnabled || !Prepare())
                return;

            Rect rect = DrawingRect;
            if (rect.width <= 0.001f || rect.height <= 0.001f)
                return;

            Vector2 position = rect.center;
            if (!settings.centered && pointer != null)
            {
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(graphic.rectTransform,
                    pointer.position, pointer.pressEventCamera, out position))
                    return;

                if (shape != null)
                    position = shape.GetShapeTransform(rect.center, true).MultiplyPoint3x4(position);
            }

            float now = Activate();
            Expire(now);

            if (count == Limit)
            {
                for (int i = 1; i < count; i++)
                    waves[i - 1] = waves[i];

                count--;
            }

            float duration = Mathf.Clamp(settings.duration, 0.01f, 1f);
            waves[count++] = new Wave
            {
                owner = settings,
                center = new Vector2((position.x - rect.center.x) / rect.width, (position.y - rect.center.y) / rect.height),
                color = ToWorkingColor(settings.ResolvedColor),
                start = now,
                duration = duration,
                coverage = Mathf.Clamp(settings.coverage, 0.1f, 2f),
                softness = Mathf.Clamp01(settings.softness),
                fadeOutDuration = Mathf.Clamp(settings.fadeOutDuration, 0f, 3f),
                ignoreAlpha = settings.ignoreGraphicAlpha
            };

            WriteRow();
        }

        float Activate()
        {
            if (active.Count == 0)
                epoch = Time.unscaledTimeAsDouble;

            if (activeIndex < 0)
            {
                activeIndex = active.Count;
                active.Add(this);
                lastTick = -1f;
                groupAlpha = graphic.canvasRenderer.GetInheritedAlpha();
            }

            return (float)(Time.unscaledTimeAsDouble - epoch);
        }

        void RefreshGroupAlpha()
        {
            // Idle rows read it again when an effect starts
            if (activeIndex < 0 || graphic == null)
                return;

            float alpha = graphic.canvasRenderer.GetInheritedAlpha();
            if (alpha == groupAlpha)
                return;

            groupAlpha = alpha;
            WriteRow();
        }

        internal void Show(InteractiveEffectSettings settings, PointerEventData pointer)
        {
            if (!IsActive() || graphic == null || !graphic.isActiveAndEnabled || !Prepare())
                return;

            float now = Activate();
            PointerLight light = FindLight(settings);
            if (light == null)
            {
                // Reuse faded-out lights so showing one again doesn't allocate
                light = spareLights.Count > 0 ? spareLights.Pop() : new PointerLight();
                light.settings = settings;
                light.pointer = pointer;
                light.position = default;
                light.opacity = 0f;
                light.shown = false;
                light.position = PointerPosition(light);
                lights.Add(light);
            }
            else
            {
                lights.Remove(light);
                lights.Add(light);
            }

            light.Refresh();
            light.pointer = pointer;
            if (!light.shown)
            {
                light.shown = true;
                light.from = light.opacity;
                light.start = now;
            }

            WriteRow();
        }

        internal void Move(InteractiveEffectSettings settings, PointerEventData pointer)
        {
            PointerLight light = FindLight(settings);
            if (light != null)
                light.pointer = pointer;
        }

        internal void Hide(InteractiveEffectSettings settings, float fadeOutDuration)
        {
            PointerLight light = FindLight(settings);
            if (light == null || !light.shown)
                return;

            if (fadeOutDuration <= 0f || light.opacity <= 0f)
            {
                Remove(settings);
                return;
            }

            light.shown = false;
            light.from = light.opacity;
            light.start = (float)(Time.unscaledTimeAsDouble - epoch);
        }

        PointerLight FindLight(InteractiveEffectSettings settings)
        {
            for (int i = 0; i < lights.Count; i++)
            {
                if (ReferenceEquals(lights[i].settings, settings))
                    return lights[i];
            }
            return null;
        }

        void RemoveLight(int index)
        {
            PointerLight light = lights[index];
            lights.RemoveAt(index);
            light.settings = null;
            light.pointer = null;
            spareLights.Push(light);
        }

        Vector2 PointerPosition(PointerLight light)
        {
            Canvas canvas = graphic.canvas;
            Camera cam = light.pointer != null
                ? light.pointer.enterEventCamera
                : canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

            Vector2 screen = light.pointer != null ? light.pointer.position : Utilities.GetPointerPosition();
            Rect rect = DrawingRect;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(graphic.rectTransform, screen, cam, out Vector2 position))
                return light.position;
            if (shape != null)
                position = shape.GetShapeTransform(rect.center, true).MultiplyPoint3x4(position);

            return position - rect.center;
        }

        bool AnimateLights(float now, float delta)
        {
            bool changed = false;

            for (int i = lights.Count - 1; i >= 0; i--)
            {
                PointerLight light = lights[i];
                InteractiveEffectSettings settings = light.settings;
                light.Refresh();
                if (!settings.IsValidFor(graphic) || light.color.a <= 0f)
                {
                    RemoveLight(i);
                    changed = true;
                    continue;
                }

                float duration = light.shown ? light.fadeInDuration : light.fadeOutDuration;
                float t = duration > 0f ? Mathf.Clamp01((now - light.start) / duration) : 1f;
                float opacity = Mathf.Lerp(light.from, light.shown ? 1f : 0f, t * t * (3f - 2f * t));

                if (!light.shown && t >= 1f)
                {
                    RemoveLight(i);
                    changed = true;
                    continue;
                }

                // Preserve the last pointer location during fade-out, including touch release
                Vector2 position = light.position;
                if (light.shown && !graphic.canvasRenderer.cull && graphic.canvasRenderer.GetInheritedAlpha() > 0f)
                {
                    Vector2 destination = PointerPosition(light);
                    float blend = light.followDuration <= 0f ? 1f : 1f - Mathf.Exp(-delta * 4f / light.followDuration);
                    position = Vector2.Lerp(position, destination, blend);
                    if ((destination - position).sqrMagnitude < 0.0001f)
                        position = destination;
                }

                changed |= light.opacity != opacity || light.position != position;
                light.opacity = opacity;
                light.position = position;
            }

            return changed;
        }

        internal void Remove(InteractiveEffectSettings owner)
        {
            int kept = 0;

            for (int i = 0; i < count; i++)
            {
                if (!ReferenceEquals(waves[i].owner, owner))
                    waves[kept++] = waves[i];
            }

            System.Array.Clear(waves, kept, count - kept);
            count = kept;

            for (int i = lights.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(lights[i].settings, owner))
                    RemoveLight(i);
            }

            WriteRow();

            if (count == 0 && lights.Count == 0)
                Deactivate();
        }

        bool Expire(float now)
        {
            int previous = count;
            int kept = 0;

            for (int i = 0; i < count; i++)
            {
                if (waves[i].owner.IsValidFor(graphic)
                    && now - waves[i].start < waves[i].duration + waves[i].fadeOutDuration)
                    waves[kept++] = waves[i];
            }

            System.Array.Clear(waves, kept, count - kept);
            count = kept;
            return previous != count;
        }

        public void Clear()
        {
            if (count != 0 || lights.Count != 0)
            {
                System.Array.Clear(waves, 0, count);
                count = 0;
                for (int i = lights.Count - 1; i >= 0; i--)
                    RemoveLight(i);
                WriteRow();
            }

            Deactivate();
        }

        void Deactivate()
        {
            if (activeIndex < 0)
                return;

            int last = active.Count - 1;
            InteractiveEffectRenderer moved = active[last];
            active[activeIndex] = moved;
            moved.activeIndex = activeIndex;
            active.RemoveAt(last);
            activeIndex = -1;

            if (active.Count == 0)
                Shader.SetGlobalFloat(TimeId, -1f);
        }

        static void Tick()
        {
            if (!ReferenceEquals(uploadRegistry, CanvasUpdateRegistry.instance))
                SubscribeUpload();

            if (active.Count == 0)
                return;

            // Bound float-clock error even if something keeps emitting for hours
            float now = (float)(Time.unscaledTimeAsDouble - epoch);
            if (now >= 4096f)
            {
                epoch += now;
                for (int i = 0; i < active.Count; i++)
                {
                    InteractiveEffectRenderer item = active[i];

                    for (int j = 0; j < item.count; j++)
                        item.waves[j].start -= now;
                    for (int j = 0; j < item.lights.Count; j++)
                        item.lights[j].start -= now;

                    item.lastTick -= now;
                    item.WriteRow();
                }
                now = 0f;
            }

            Shader.SetGlobalFloat(TimeId, now);

            for (int i = active.Count - 1; i >= 0; i--)
            {
                InteractiveEffectRenderer item = active[i];
                if (item.graphic == null || !item.graphic.isActiveAndEnabled || item.graphic.canvas == null
                    || !item.graphic.canvas.isActiveAndEnabled)
                {
                    item.Clear();
                    continue;
                }

                float delta = item.lastTick < 0f ? Time.unscaledDeltaTime : Mathf.Max(0f, now - item.lastTick);
                item.lastTick = now;
                bool changed = item.Expire(now);
                changed |= item.AnimateLights(now, delta);

                if (changed || item.lights.Count > 0)
                    item.WriteRow(); // Expire already updates count; always flush the zero header as well

                if (item.count == 0 && item.lights.Count == 0)
                    item.Deactivate();
            }
        }
        #endregion

        #region Materials
        public static bool IsSupported(Graphic target)
        {
            if (target == null || !(target is Image || target is RawImage || target is ProceduralShape))
                return false;

            Material material = target.material;
            if (material == null || material.shader == null)
                return false;
            if (material.HasProperty(SupportId) && material.GetFloat(SupportId) > 0.5f)
                return true;

            string name = material.shader.name;
            return target is not ProceduralShape && (name == "UI/Default" || name == "UI/DefaultETC1");
        }

        public Material GetModifiedMaterial(Material baseMaterial)
        {
            if (!IsActive() || row == 0 || baseMaterial == null || effectShader == null)
                return baseMaterial;

            if (baseMaterial.HasProperty(SupportId) && baseMaterial.GetFloat(SupportId) > 0.5f)
            {
                ReleaseMaterial();
                return baseMaterial;
            }

            string shaderName = baseMaterial.shader.name;
            if (shape != null || (shaderName != "UI/Default" && shaderName != "UI/DefaultETC1"))
            {
                ReleaseMaterial();
                return baseMaterial;
            }

            if (sourceMaterial != baseMaterial || materialEntry == null)
            {
                ReleaseMaterial();
                sourceMaterial = baseMaterial;
                if (!materials.TryGetValue(baseMaterial, out materialEntry))
                {
                    materialEntry = new MaterialEntry
                    {
                        material = new Material(effectShader)
                        {
                            name = baseMaterial.name + "_EvoEffects",
                            hideFlags = HideFlags.HideAndDontSave
                        }
                    };
                    materials.Add(baseMaterial, materialEntry);
                }
                materialEntry.references++;
            }

            materialEntry.material.CopyMatchingPropertiesFromMaterial(baseMaterial);

            if (shaderName == "UI/DefaultETC1")
            {
                materialEntry.material.EnableKeyword("ETC1_EXTERNAL_ALPHA");
                materialEntry.material.SetFloat(ExternalAlphaId, 1f);
            }
            else
                materialEntry.material.DisableKeyword("ETC1_EXTERNAL_ALPHA");

            return materialEntry.material;
        }

        void ReleaseMaterial()
        {
            if (materialEntry == null)
                return;

            if (--materialEntry.references == 0)
            {
                materials.Remove(sourceMaterial);
                DisposeObject(materialEntry.material);
            }

            materialEntry = null;
            sourceMaterial = null;
        }
        #endregion

        #region Atlas
        bool AllocateRow()
        {
            if (freeRows.Count > 0)
            {
                row = freeRows.Pop();
                return true;
            }

            // Keeps packed float vertex IDs exact on large canvases
            int maximum = Mathf.Min(4096, SystemInfo.maxTextureSize);
            if (nextRow >= maximum)
                return false;

            if (atlas == null || nextRow >= atlas.height)
            {
                int height = Mathf.Min(maximum, atlas == null ? 16 : atlas.height * 2);
                if (atlas != null)
                {
                    if (!atlas.Reinitialize(Width, height, TextureFormat.RGBAFloat, false))
                        return false;
                }
                else
                {
                    atlas = new Texture2D(Width, height, TextureFormat.RGBAFloat, false, true)
                    {
                        name = "EvoEffectsData",
                        hideFlags = HideFlags.HideAndDontSave,
                        filterMode = FilterMode.Point,
                        wrapMode = TextureWrapMode.Clamp
                    };

                    // Stay subscribed while the atlas exists. Adding and removing handlers
                    // on every dirty frame allocates once other handlers share the events.
                    Canvas.preWillRenderCanvases -= tickCallback;
                    Canvas.preWillRenderCanvases += tickCallback;
                    SubscribeUpload();
                }

                // Reinitialize discards data; initialize all headers, including dormant rows
                var pixels = atlas.GetPixelData<Color>(0);
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = Color.clear;
                for (int i = 0; i < residents.Count; i++)
                    residents[i].WriteRow();

                // Reserve collection capacity during preparation, never during a click
                if (active.Capacity < height)
                    active.Capacity = height;

                Shader.SetGlobalTexture(DataId, atlas);
                Shader.SetGlobalVector(SizeId, new Vector4(1f / Width, 1f / height, 0f, 0f));
                dirty = true;
            }

            row = nextRow++;
            return true;
        }

        void WriteRow()
        {
            if (row == 0 || atlas == null)
                return;

            Rect rect = graphic != null ? DrawingRect : default;
            var pixels = atlas.GetPixelData<Color>(0);
            int offset = row * Width;
            PointerLight spotlight = null, border = null;

            // Multiple hosts may share a graphic. The newest visible light of each kind wins;
            // removing it reveals the previous owner and never clears another effect kind.
            for (int i = 0; i < lights.Count; i++)
            {
                if (lights[i].opacity <= 0f)
                    continue;

                if (lights[i].settings is BorderGlow.Settings) { border = lights[i]; }
                else { spotlight = lights[i]; }
            }

            // Negative radii mark layers that ignore the graphic's alpha
            bool ignoring = false;

            for (int i = 0; i < count; i++)
            {
                Wave wave = waves[i];
                int index = offset + 1 + i * 3;
                Vector2 farthest = new((Mathf.Abs(wave.center.x) + 0.5f) * rect.width, (Mathf.Abs(wave.center.y) + 0.5f) * rect.height);
                float radius = farthest.magnitude * wave.coverage;

                Put(pixels, index, new Color(wave.center.x, wave.center.y, wave.ignoreAlpha ? -radius : radius, wave.softness));
                Put(pixels, index + 1, wave.color);
                Put(pixels, index + 2, new Color(wave.start, wave.duration, wave.fadeOutDuration, 0f));
                ignoring |= wave.ignoreAlpha;
            }

            ignoring |= WriteLight(pixels, offset + PointerColumn, spotlight, rect);
            ignoring |= WriteLight(pixels, offset + BorderColumn, border, rect);

            // The ripple count's fraction carries Canvas Group alpha for those layers
            Put(pixels, offset, new Color(count + (ignoring ? groupAlpha * 0.5f : 0f),
                (spotlight != null ? 1f : 0f) + (border != null ? 2f : 0f), rect.width, rect.height));
            KeepMesh(ignoring && groupAlpha > 0f);

            if (border != null)
            {
                var settings = (BorderGlow.Settings)border.settings;
                Put(pixels, offset + BorderColumn + 2, new Color(Mathf.Max(0.1f, settings.width), 0f,
                    Mathf.Clamp01(settings.ambient), graphic.mainTexture != Texture2D.whiteTexture ? 1f : 0f));
            }

            Put(pixels, offset + BorderColumn + 3, spriteUV);
        }

        // uGUI skips meshes whose graphic alpha is zero, which would also hide layers that ignore it
        void KeepMesh(bool keep)
        {
            if (keep == keepMesh || graphic == null)
                return;

            keepMesh = keep;
            CanvasRenderer canvasRenderer = graphic.canvasRenderer;

            if (keep)
            {
                restoreCulling = canvasRenderer.cullTransparentMesh;
                if (restoreCulling)
                    canvasRenderer.cullTransparentMesh = false;
            }
            else if (restoreCulling)
                canvasRenderer.cullTransparentMesh = true;
        }

        static Color ToWorkingColor(Color color) => QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color;

        static bool WriteLight(Unity.Collections.NativeArray<Color> pixels, int index, PointerLight light, Rect rect)
        {
            if (light == null)
                return false;

            bool ignoreAlpha = light.settings.ignoreGraphicAlpha;
            float radius = Mathf.Max(1f, light.radius);
            Put(pixels, index, new Color(light.position.x / Mathf.Max(rect.width, 0.001f), light.position.y
                / Mathf.Max(rect.height, 0.001f), ignoreAlpha ? -radius : radius, Mathf.Clamp01(light.softness)));

            Color color = ToWorkingColor(light.color);
            color.a *= light.opacity;
            Put(pixels, index + 1, color);
            return ignoreAlpha;
        }

        static void Put(Unity.Collections.NativeArray<Color> pixels, int index, Color value)
        {
            if (pixels[index].Equals(value))
                return;

            pixels[index] = value;
            dirty = true;
        }

        static void SubscribeUpload()
        {
            // The upload must follow the registry's graphic rebuild. uGUI 2.7 recreates the
            // registry when entering play mode without a domain reload, so line up behind the new one.
            uploadRegistry = CanvasUpdateRegistry.instance;
            Canvas.willRenderCanvases -= uploadCallback;
            Canvas.willRenderCanvases += uploadCallback;
        }

        static void Upload()
        {
            if (!dirty || atlas == null)
                return;

            atlas.Apply(false, false);
            dirty = false;
        }

        static void ReleaseAtlas()
        {
            Canvas.preWillRenderCanvases -= tickCallback;
            Canvas.willRenderCanvases -= uploadCallback;
            uploadRegistry = null;
            Shader.SetGlobalFloat(TimeId, -1f);
            Shader.SetGlobalTexture(DataId, null);
            Shader.SetGlobalVector(SizeId, Vector4.zero);

            DisposeObject(atlas);
            atlas = null;
            freeRows.Clear();
            nextRow = 1;
            dirty = false;
        }

        static void DisposeObject(Object value)
        {
            if (value == null)
                return;

            if (Application.isPlaying) { Destroy(value); }
            else { DestroyImmediate(value); }
        }
        #endregion

        #region Static Cleanup
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetResources()
        {
            // Includes idle renderers and supports disabled domain/scene reload
            for (int i = residents.Count - 1; i >= 0; i--)
            {
                InteractiveEffectRenderer item = residents[i];
                if (item != null)
                {
                    item.Release();
                    if (item.graphic != null)
                    {
                        item.graphic.SetVerticesDirty();
                        item.graphic.SetMaterialDirty();
                    }
                }
            }

            residents.Clear();
            active.Clear();

            foreach (MaterialEntry entry in materials.Values)
                DisposeObject(entry.material);

            materials.Clear();
            ReleaseAtlas();
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        static void RegisterEditorCleanup()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ResetResources;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeChanged;
            UnityEditor.EditorApplication.quitting += ResetResources;
        }

        static void OnPlayModeChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
                ResetResources();
        }
#endif
        #endregion

        #region Types
        struct Wave
        {
            public Ripple.Settings owner;
            public Vector2 center;
            public Color color;
            public float start, duration, coverage, softness, fadeOutDuration;
            public bool ignoreAlpha;
        }

        sealed class PointerLight
        {
            public InteractiveEffectSettings settings;
            public PointerEventData pointer;
            public Vector2 position;
            public float start, from, opacity;
            public bool shown;

            // Read the owner's live settings once per tick
            public Color color;
            public float radius, softness, followDuration, fadeInDuration, fadeOutDuration;

            public void Refresh()
            {
                color = settings.ResolvedColor;

                if (settings is BorderGlow.Settings border)
                {
                    radius = border.radius;
                    softness = border.softness;
                    followDuration = border.followDuration;
                    fadeInDuration = border.fadeInDuration;
                    fadeOutDuration = border.fadeOutDuration;
                }
                else if (settings is Spotlight.Settings spotlight)
                {
                    radius = spotlight.radius;
                    softness = spotlight.softness;
                    followDuration = spotlight.followDuration;
                    fadeInDuration = spotlight.fadeInDuration;
                    fadeOutDuration = spotlight.fadeOutDuration;
                }
            }
        }

        sealed class MaterialEntry
        {
            public Material material;
            public int references;
        }
        #endregion
    }
}