using System;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI
{
    /// <summary>
    /// Owner-scoped capture binding. Matching captures and display materials are reference counted.
    /// </summary>
    [NoAutoStaticsCleanup]
    internal sealed class BackdropBlurSource : IDisposable
    {
        // Shader IDs
        internal const string ShapeKeyword = "EVO_UI_BACKDROP";
        static readonly int TextureId = Shader.PropertyToID("_BackgroundTex");
        static readonly int ViewportId = Shader.PropertyToID("_BgViewportRect");
        static readonly int AdjustmentsId = Shader.PropertyToID("_BlurAdjustments");
        static readonly Dictionary<MaterialKey, MaterialEntry> materials = new();

        // State
        readonly Graphic owner;
        BackdropBlurCapture capture;
        MaterialEntry entry;
        MaterialKey materialKey;
        Vector3 adjustments;
        Material renderedMaterial;
        List<Material> popMaterials;
        int boundVersion = -1;
        bool fullProgram;

        // Properties
        public bool IsReady => capture != null && capture.Ready;
        public RenderTexture Texture => IsReady ? capture.Texture : null;
        // The copy whose shader program the owner's stencil and Soft Mask copies follow, when it picks its own
        internal Material ProgramMaterial => entry != null && entry.ownProgram ? entry.material : null;
        public static bool BeforeTransparents(Graphic owner) => owner.canvas != null
            && owner.canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay;

        public BackdropBlurSource(Graphic owner) => this.owner = owner;

        public void Refresh(BackdropBlur.Settings settings, bool automaticMaterial)
        {
            Camera camera = settings.ResolveCamera(owner);
            bool visible = owner.canvas != null && owner.canvas.isActiveAndEnabled && owner.color.a > 0f
                && owner.canvasRenderer.GetInheritedAlpha() > 0f && owner.canvasRenderer.GetAlpha() > 0f
                && !owner.canvasRenderer.cull;

            if (!settings.enabled || !automaticMaterial || !visible || camera == null
                || camera.cameraType != CameraType.Game || !camera.isActiveAndEnabled || camera.stereoEnabled)
            {
                ReleaseCapture();
                return;
            }

            BackdropBlurCapture.Settings configuration = new(settings.iterations, settings.radius,
                (int)settings.resolution, settings.updateInterval, BeforeTransparents(owner));
           
            if (capture == null || !capture.Matches(camera, configuration))
            {
                if (capture == null || !capture.TryReconfigure(camera, configuration))
                {
                    Shader shader = settings.FilterShader;
                    if (shader == null || !shader.isSupported)
                    {
                        ReleaseCapture();
                        return;
                    }

                    BackdropBlurCapture next = BackdropBlurCapture.Acquire(camera, configuration, shader);
                    ReleaseCapture();
                    capture = next;
                    capture.Changed += Bind;
                    owner.SetMaterialDirty();
                }
            }

            Vector3 nextAdjustments = settings.Adjustments;
            if (adjustments != nextAdjustments)
            {
                adjustments = nextAdjustments;
                ReleaseMaterial();
                owner.SetMaterialDirty();
            }

            Bind();
        }

        /// <param name="ownProgram">The copy of a Procedural Shape page material picks its own shader program from the
        /// shapes that draw with it (see SetFullProgram), instead of following the page.</param>
        public Material GetMaterial(Material original, Shader shader = null, bool ownProgram = false)
        {
            if (capture == null)
                return original;

            if (original != null)
                shader = original.shader;
            if (shader == null || shader.FindPropertyIndex("_BackgroundTex") < 0)
                return original;

            MaterialKey key = new(original, shader, capture, adjustments);
            if (entry != null && materialKey.Equals(key))
                return entry.material;

            ReleaseMaterial();
            materialKey = key;
            if (!materials.TryGetValue(key, out entry))
            {
                Material material = original != null ? new Material(original) : new Material(shader);
                material.name = "Backdrop Blur (Shared)";
                material.hideFlags = HideFlags.HideAndDontSave;
                if (original != null)
                    material.EnableKeyword(ShapeKeyword);
               
                entry = new MaterialEntry { material = material, ownProgram = ownProgram && original != null, lite = -1 };
                materials.Add(key, entry);
            }

            entry.users.Add(this);
            if (fullProgram)
                entry.fullVotes++;
            ApplyProgram(entry);
            Bind();
            return entry.material;
        }

        /// <summary>
        /// The owner's vote for the full shader program. A frosted panel is its own batch, so on a canvas that needs the
        /// full program for other shapes it can still draw with the light one, which is several times cheaper per pixel.
        /// </summary>
        internal void SetFullProgram(bool full)
        {
            if (fullProgram == full)
                return;

            fullProgram = full;
            if (entry == null)
                return;

            entry.fullVotes += full ? 1 : -1;
            ApplyProgram(entry);
        }

        // The copy, and the stencil copies its users render with, switch together. Soft Mask copies follow the copy
        // on their own (SoftMask.SyncMaterial, through ProgramMaterial).
        static void ApplyProgram(MaterialEntry target)
        {
            if (!target.ownProgram)
                return;

            int lite = target.fullVotes == 0 ? 1 : 0;
            if (target.lite == lite)
                return;

            target.lite = lite;
            ProceduralShape.SetLightProgram(target.material, lite == 1);

            for (int i = 0; i < target.users.Count; i++)
                target.users[i].ApplyProgramToRendered();
        }

        void ApplyProgramToRendered()
        {
            if (entry == null || !entry.ownProgram)
                return;

            bool lite = entry.lite == 1;
            if (renderedMaterial != entry.material)
                ProceduralShape.SetLightProgram(renderedMaterial, lite, entry.material.shader);
            if (popMaterials != null)
            {
                for (int i = 0; i < popMaterials.Count; i++)
                    ProceduralShape.SetLightProgram(popMaterials[i], lite, entry.material.shader);
            }
        }

        // Refresh existing stencil/soft-mask copies directly: no canvas rebuild per camera frame
        public void Bind()
        {
            if (entry == null || capture == null)
                return;

            Rect rect = capture.Configuration.BeforeTransparents ? new Rect(0f, 0f, 1f, 1f) : capture.Camera.rect;
            Vector4 viewport = new(rect.x, rect.y, rect.width, rect.height);
            Vector4 values = new(adjustments.x, adjustments.y, adjustments.z, capture.Ready ? 1f : 0f);
            Texture texture = capture.Texture != null ? capture.Texture : Texture2D.blackTexture;
            if (entry.texture != texture || entry.viewport != viewport || entry.adjustments != values)
            {
                entry.texture = texture;
                entry.viewport = viewport;
                entry.adjustments = values;
                Apply(entry.material);
                entry.version++;
            }

            if (boundVersion == entry.version)
                return;

            boundVersion = entry.version;
           
            if (renderedMaterial != entry.material)
                Apply(renderedMaterial);
          
            if (popMaterials != null)
            {
                for (int i = 0; i < popMaterials.Count; i++)
                    Apply(popMaterials[i]);
            }
        }

        public void BindRenderedMaterials()
        {
            boundVersion = -1;
            CanvasRenderer renderer = owner.canvasRenderer;
            renderedMaterial = renderer.materialCount > 0 ? renderer.GetMaterial(0) : null;
            popMaterials?.Clear();
          
            for (int i = 0; i < renderer.popMaterialCount; i++)
            {
                popMaterials ??= new List<Material>();
                popMaterials.Add(renderer.GetPopMaterial(i));
            }

            // Stencil copies are cached per source and can predate the copy's last program switch
            ApplyProgramToRendered();
            Bind();
        }

        void Apply(Material target)
        {
            if (target == null || target.shader != entry.material.shader)
                return;

            target.SetTexture(TextureId, entry.texture);
            target.SetVector(ViewportId, entry.viewport);
            target.SetVector(AdjustmentsId, entry.adjustments);
        }

        void ReleaseMaterial()
        {
            renderedMaterial = null;
            popMaterials?.Clear();
            boundVersion = -1;
       
            if (entry == null)
                return;

            entry.users.Remove(this);
            if (fullProgram)
                entry.fullVotes--;
            if (entry.users.Count == 0)
            {
                materials.Remove(materialKey);
                BackdropBlurCapture.DestroyResource(entry.material);
            }
            else
                ApplyProgram(entry);

            entry = null;
            materialKey = default;
        }

        void ReleaseCapture()
        {
            ReleaseMaterial();
            if (capture == null)
                return;

            capture.Changed -= Bind;
            capture.Release();
            capture = null;
            owner.SetMaterialDirty();
        }

        public void Dispose() => ReleaseCapture();

        sealed class MaterialEntry
        {
            public Material material;
            public int version;
            public Texture texture;
            public Vector4 viewport, adjustments;
            // The sources drawing with the copy; it is destroyed when the last one leaves
            public readonly List<BackdropBlurSource> users = new();
            // Page copies only: votes for the full program, and the program applied (1 light, 0 full, -1 not yet)
            public bool ownProgram;
            public int fullVotes, lite;
        }

        readonly struct MaterialKey : IEquatable<MaterialKey>
        {
            readonly Material original;
            readonly Shader shader;
            readonly BackdropBlurCapture capture;
            readonly Vector3 adjustments;

            public MaterialKey(Material original, Shader shader, BackdropBlurCapture capture, Vector3 adjustments)
            {
                this.original = original;
                this.shader = shader;
                this.capture = capture;
                this.adjustments = adjustments;
            }

            public bool Equals(MaterialKey other) => original == other.original && shader == other.shader
                && capture == other.capture && adjustments.Equals(other.adjustments);
            public override bool Equals(object obj) => obj is MaterialKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(original, shader, capture, adjustments);
        }
    }
}