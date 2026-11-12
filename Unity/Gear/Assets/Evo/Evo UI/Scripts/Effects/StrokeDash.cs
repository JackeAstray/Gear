using System;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_6000_2_OR_NEWER
using ObjectId = UnityEngine.EntityId;
#else
using ObjectId = System.Int32;
#endif

namespace Evo.UI
{
    [AddComponentMenu("Evo/UI/Effects/Stroke Dash"), HelpURL(Constants.HelpUrl)]
    [ExecuteAlways, RequireComponent(typeof(CanvasRenderer))]
    public class StrokeDash : MaskableGraphic
    {
        [Tooltip("Thickness of the stroke line.")]
        [Min(0.5f)] public float thickness = 4f;

        [Tooltip("Outer Radius of the corners. Set to 0 for sharp corners.")]
        [Min(0f)] public float borderRadius = 20f;

        [Tooltip("Length of the visible dash.")]
        [Min(0.5f)] public float dashLength = 20f;

        [Tooltip("Length of the empty gap.")]
        [Min(0f)] public float gapLength = 5f;

        [Tooltip("Animation speed. Positive for clockwise.")]
        public float speed = 30f;

        // Cache
        Material cachedMaterialInstance;
        Vector2 lastRectSize;
        Vector4 lastSettings;
        float lastSpeed;
        StrokeKey? currentKey;

        // Shader IDs
        [SerializeField, HideInInspector] Shader embeddedShader;
        const string ShaderName = "Evo/UI/Stroke Dash";
        static readonly int PropsRectSize = Shader.PropertyToID("_RectSize");
        static readonly int PropsRadius = Shader.PropertyToID("_Radius");
        static readonly int PropsPathRadius = Shader.PropertyToID("_PathRadius");
        static readonly int PropsThickness = Shader.PropertyToID("_Thickness");
        static readonly int PropsDashSettings = Shader.PropertyToID("_DashSettings");

        // Stencil IDs
        static readonly int PropsStencil = Shader.PropertyToID("_Stencil");
        static readonly int PropsStencilComp = Shader.PropertyToID("_StencilComp");
        static readonly int PropsStencilOp = Shader.PropertyToID("_StencilOp");
        static readonly int PropsStencilReadMask = Shader.PropertyToID("_StencilReadMask");
        static readonly int PropsStencilWriteMask = Shader.PropertyToID("_StencilWriteMask");
        static readonly int PropsColorMask = Shader.PropertyToID("_ColorMask");
        static readonly int PropsUseUIAlphaClip = Shader.PropertyToID("_UseUIAlphaClip");

        protected override void OnEnable()
        {
            ClampSettings();

            if (embeddedShader == null)
                embeddedShader = Shader.Find(ShaderName);

            lastRectSize = GetPixelAdjustedRect().size;
            lastSettings = new Vector4(thickness, borderRadius, dashLength, gapLength);
            lastSpeed = speed;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update += OnEditorUpdate;
#endif

            base.OnEnable();
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            if (currentKey.HasValue)
            {
                MaterialCache.Release(currentKey.Value);
                currentKey = null;
            }

            cachedMaterialInstance = null;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= OnEditorUpdate;
#endif
        }

        void Update()
        {
            Vector2 currentSize = GetPixelAdjustedRect().size;
            Vector4 settings = new(thickness, borderRadius, dashLength, gapLength);
            if (!currentSize.Equals(lastRectSize) || !settings.Equals(lastSettings) || !speed.Equals(lastSpeed))
            {
                lastRectSize = currentSize;
                lastSettings = settings;
                lastSpeed = speed;
                SetMaterialDirty();
            }
            else if (Application.IsPlaying(gameObject) && speed != 0)
                SetMaterialDirty();
        }

#if UNITY_EDITOR
        protected override void Reset()
        {
            embeddedShader = Shader.Find(ShaderName);
            base.Reset();
        }

        void OnEditorUpdate()
        {
            if (this == null || !isActiveAndEnabled || Application.IsPlaying(gameObject))
                return;

            if (speed != 0)
            {
                SetMaterialDirty();
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            }
        }
        protected override void OnValidate()
        {
            ClampSettings();

            if (embeddedShader == null)
                embeddedShader = Shader.Find(ShaderName);

            base.OnValidate();
        }
#endif

        void ClampSettings()
        {
            thickness = ClampMinimum(thickness, 0.5f);
            borderRadius = ClampMinimum(borderRadius, 0f);
            dashLength = ClampMinimum(dashLength, 0.5f);
            gapLength = ClampMinimum(gapLength, 0f);
            if (float.IsNaN(speed) || float.IsInfinity(speed)) { speed = 0f; }
        }

        static float ClampMinimum(float value, float minimum)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? minimum : Mathf.Max(minimum, value);
        }

        public override Material GetModifiedMaterial(Material baseMaterial)
        {
            if (!IsActive())
                return baseMaterial;

            ClampSettings();
            baseMaterial = base.GetModifiedMaterial(baseMaterial);

            if (embeddedShader == null)
                embeddedShader = Shader.Find(ShaderName);
            if (embeddedShader == null)
                return baseMaterial;

            Rect rect = GetPixelAdjustedRect();
            float width = Mathf.Max(0f, rect.width);
            float height = Mathf.Max(0f, rect.height);

            // Calculations
            float maxR = Mathf.Min(width, height) * 0.5f;
            float strokeThickness = Mathf.Min(thickness, maxR);
            float outerRadius = Mathf.Clamp(borderRadius, 0f, maxR);
            float pathRadius = Mathf.Clamp(Mathf.Max(outerRadius, strokeThickness), 0f, maxR);
            float perimeter = 2f * (width + height) + pathRadius * (6.283185f - 8f);
            float finalDash = dashLength;
            float finalGap = gapLength;
            double cycle = (double)finalDash + finalGap;

            if (perimeter > 0.001f && finalGap > 0f)
            {
                double targetCount = Math.Max(1.0, Math.Round(perimeter / cycle));
                float perfectCycle = (float)(perimeter / targetCount);
                if (perfectCycle < finalDash)
                {
                    double scale = perfectCycle / cycle;
                    finalDash = (float)(finalDash * scale);
                    finalGap = (float)(finalGap * scale);
                }
                else { finalGap = perfectCycle - finalDash; }
            }

            // Global Time Calculation
            double time = 0.0;
            bool playing = Application.IsPlaying(gameObject);
            if (playing) { time = Time.timeAsDouble; }
            else
            {
#if UNITY_EDITOR
                time = UnityEditor.EditorApplication.timeSinceStartup;
#endif
            }

            double finalCycle = finalDash + finalGap;
            float currentPhase = 0;
            if (finalCycle > 0.001)
            {
                double totalMovement = time * speed;
                double wrapped = totalMovement % finalCycle;
                if (wrapped < 0) { wrapped += finalCycle; }
                currentPhase = (float)wrapped;
            }

            // Stencil Support
            float stencilId = GetFloat(baseMaterial, PropsStencil, 0);
            float stencilComp = GetFloat(baseMaterial, PropsStencilComp, 8);
            float stencilOp = GetFloat(baseMaterial, PropsStencilOp, 0);
            float stencilRead = GetFloat(baseMaterial, PropsStencilReadMask, 255);
            float stencilWrite = GetFloat(baseMaterial, PropsStencilWriteMask, 255);
            float colorMask = GetFloat(baseMaterial, PropsColorMask, 15);
            bool alphaClip = baseMaterial != null && baseMaterial.IsKeywordEnabled("UNITY_UI_ALPHACLIP");
#if UNITY_6000_2_OR_NEWER
            ObjectId shaderId = embeddedShader.GetEntityId();
#else
            ObjectId shaderId = embeddedShader.GetInstanceID();
#endif
            StrokeKey newKey = new(width, height, outerRadius, strokeThickness, finalDash, finalGap, speed, stencilId,
                stencilComp, stencilOp, stencilRead, stencilWrite, colorMask, shaderId, playing, alphaClip);

            bool materialChanged = cachedMaterialInstance == null || !currentKey.HasValue || !newKey.Equals(currentKey.Value);
            if (materialChanged)
            {
                cachedMaterialInstance = MaterialCache.Get(newKey, embeddedShader, currentKey);
                currentKey = newKey;
            }

            if (cachedMaterialInstance == null)
                return baseMaterial;

            if (materialChanged)
            {
                cachedMaterialInstance.SetVector(PropsRectSize, new Vector4(width, height, 0, 0));
                cachedMaterialInstance.SetFloat(PropsRadius, outerRadius);
                cachedMaterialInstance.SetFloat(PropsPathRadius, pathRadius);
                cachedMaterialInstance.SetFloat(PropsThickness, strokeThickness);
                cachedMaterialInstance.SetFloat(PropsStencil, stencilId);
                cachedMaterialInstance.SetFloat(PropsStencilComp, stencilComp);
                cachedMaterialInstance.SetFloat(PropsStencilOp, stencilOp);
                cachedMaterialInstance.SetFloat(PropsStencilReadMask, stencilRead);
                cachedMaterialInstance.SetFloat(PropsStencilWriteMask, stencilWrite);
                cachedMaterialInstance.SetFloat(PropsColorMask, colorMask);
                cachedMaterialInstance.SetFloat(PropsUseUIAlphaClip, alphaClip ? 1f : 0f);
              
                if (alphaClip) { cachedMaterialInstance.EnableKeyword("UNITY_UI_ALPHACLIP"); }
                else { cachedMaterialInstance.DisableKeyword("UNITY_UI_ALPHACLIP"); }
            }

            cachedMaterialInstance.SetVector(PropsDashSettings, new Vector3(finalDash, finalGap, currentPhase));
            return cachedMaterialInstance;
        }

        static float GetFloat(Material material, int property, float fallback)
        {
            return material != null && material.HasProperty(property) ? material.GetFloat(property) : fallback;
        }

        // Caching System
        readonly struct StrokeKey : IEquatable<StrokeKey>
        {
            public readonly float width;
            public readonly float height;
            public readonly float radius;
            public readonly float thickness;
            public readonly float dash;
            public readonly float gap;
            public readonly float speed;

            // Stencil keys
            public readonly float stencilId;
            public readonly float stencilComp;
            public readonly float stencilOp;
            public readonly float stencilRead;
            public readonly float stencilWrite;
            public readonly float colorMask;
            public readonly ObjectId shaderId;
            public readonly bool playing;
            public readonly bool alphaClip;

            public StrokeKey(float w, float h, float r, float t, float d, float g, float s, float sId, float sComp,
                float sOp, float sRead, float sWrite, float cMask, ObjectId shader, bool isPlaying, bool useAlphaClip)
            {
                width = w;
                height = h;
                radius = r;
                thickness = t;
                dash = d;
                gap = g;
                speed = s;

                stencilId = sId;
                stencilComp = sComp;
                stencilOp = sOp;
                stencilRead = sRead;
                stencilWrite = sWrite;
                colorMask = cMask;
                shaderId = shader;
                playing = isPlaying;
                alphaClip = useAlphaClip;
            }

            public bool Equals(StrokeKey other)
            {
                return width.Equals(other.width) && height.Equals(other.height) &&
                       radius.Equals(other.radius) && thickness.Equals(other.thickness) &&
                       dash.Equals(other.dash) && gap.Equals(other.gap) && speed.Equals(other.speed) &&
                       stencilId.Equals(other.stencilId) && stencilComp.Equals(other.stencilComp) &&
                       stencilOp.Equals(other.stencilOp) && stencilRead.Equals(other.stencilRead) &&
                       stencilWrite.Equals(other.stencilWrite) && colorMask.Equals(other.colorMask) &&
                       shaderId == other.shaderId && playing == other.playing && alphaClip == other.alphaClip;
            }

            public override int GetHashCode()
            {
                var hash1 = HashCode.Combine(width, height, radius, thickness, dash, gap, speed);
                var hash2 = HashCode.Combine(stencilId, stencilComp, stencilOp, stencilRead, stencilWrite, colorMask);
                return HashCode.Combine(hash1, hash2, shaderId, playing, alphaClip);
            }
        }

        [NoAutoStaticsCleanup]
        static class MaterialCache
        {
            struct Entry
            {
                public Material material;
                public int refCount;
            }

            static readonly Dictionary<StrokeKey, Entry> cache = new();

            public static Material Get(StrokeKey key, Shader shader, StrokeKey? previousKey)
            {
                if (cache.TryGetValue(key, out Entry entry) && entry.material != null)
                {
                    entry.refCount++;
                    cache[key] = entry;

                    if (previousKey.HasValue)
                        Release(previousKey.Value);

                    return entry.material;
                }

                // A sole owner can change settings without creating another material
                if (!cache.ContainsKey(key) && previousKey.HasValue && cache.TryGetValue(previousKey.Value, out entry)
                    && entry.refCount == 1 && entry.material != null && entry.material.shader == shader)
                {
                    cache.Remove(previousKey.Value);
                    cache[key] = entry;
                    return entry.material;
                }

                // Preserve references if Unity destroyed a cached native material
                cache.TryGetValue(key, out entry);
                entry.material = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                entry.refCount++;
                cache[key] = entry;
           
                if (previousKey.HasValue)
                    Release(previousKey.Value);

                return entry.material;
            }

            public static void Release(StrokeKey key)
            {
                if (!cache.TryGetValue(key, out Entry entry))
                    return;

                if (--entry.refCount > 0)
                {
                    cache[key] = entry;
                    return;
                }

                cache.Remove(key);

                if (entry.material == null)
                    return;

                if (Application.isPlaying) { Destroy(entry.material); }
                else { DestroyImmediate(entry.material); }
            }
        }
    }
}
