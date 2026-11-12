using System;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

namespace Evo.UI
{
    /// <summary>
    /// Shared capture lifetime and dual Kawase filter. Pipeline adapters only supply the camera color buffer.
    /// </summary>
    [NoAutoStaticsCleanup]
    public sealed class BackdropBlurCapture
    {
        // Constants
        public const string ProfilerName = "Backdrop Blur";
        const string ArrayKeyword = "EVO_BLUR_ARRAY";

        // State
        public Camera Camera { get; }
        public Settings Configuration { get; private set; }
        public RenderTexture Texture => Levels.Length > 0 ? Levels[0] : null;
        public RenderTexture[] Levels { get; private set; } = Array.Empty<RenderTexture>();
        public bool Ready { get; private set; }
        public event Action Changed;
        public event Action Released;
        public event Action TexturesReleasing;
        public static event Action AllReleased; // Allows optional pipeline adapters to release registrations immediately

        static readonly List<BackdropBlurCapture> captures = new();
        static readonly Dictionary<Camera, BackdropBlurCamera> cameraEffects = new();

        // Shader IDs
        static readonly int SourceId = Shader.PropertyToID("_EvoBlurSource");
        static readonly int SourceArrayId = Shader.PropertyToID("_EvoBlurSourceArray");
        static readonly int TexelId = Shader.PropertyToID("_EvoBlurTexelSize");
        static readonly int ScaleBiasId = Shader.PropertyToID("_EvoBlurScaleBias");
        static readonly int OffsetId = Shader.PropertyToID("_EvoBlurOffset");

        static Material filter;
        static Material arrayFilter;
        static CommandBuffer imageCommands;
        static RenderPipelineAsset lastPipeline;
        CommandBuffer commands;
        RenderTexture resolvedSource;
        CameraEvent cameraEvent;
        int references;
        int countdown;
        readonly Shader shader;
        bool disposed;

        public static IReadOnlyList<BackdropBlurCapture> Active => captures;

        public readonly struct Settings : IEquatable<Settings>
        {
            public readonly int Iterations, Downsample, Interval;
            public readonly float Radius;
            public readonly bool BeforeTransparents;

            public Settings(int iterations, float radius, int downsample, int interval, bool beforeTransparents)
            {
                Iterations = Mathf.Clamp(iterations, 1, 5);
                Radius = ClampFinite(radius, 0.5f, 6f, 1f);
                Downsample = downsample <= 1 ? 1 : downsample <= 2 ? 2 : downsample <= 4 ? 4 : 8;
                Interval = Mathf.Max(1, interval);
                BeforeTransparents = beforeTransparents;
            }

            public bool Equals(Settings other)
            {
                return Iterations == other.Iterations
                    && Radius == other.Radius
                    && Downsample == other.Downsample
                    && Interval == other.Interval
                    && BeforeTransparents == other.BeforeTransparents;
            }

            public override bool Equals(object obj) => obj is Settings other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(Iterations, Radius, Downsample, Interval, BeforeTransparents);
        }

        BackdropBlurCapture(Camera camera, Settings settings, Shader shader)
        {
            Camera = camera;
            Configuration = settings;
            this.shader = shader;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Initialize()
        {
            Cleanup();
            Application.quitting -= Cleanup;
            Application.quitting += Cleanup;
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        static void InitializeEditor()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= Cleanup;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
            UnityEditor.EditorApplication.quitting -= Cleanup;
            UnityEditor.EditorApplication.quitting += Cleanup;
        }
#endif

        static void Cleanup()
        {
            while (captures.Count > 0)
            {
                BackdropBlurCapture capture = captures[^1];
                capture.references = 1;
                capture.Release();
            }
        }

        internal bool Matches(Camera camera, Settings settings) => !disposed && Camera == camera && Configuration.Equals(settings);

        internal static float ClampFinite(float value, float min, float max, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);
        }

        internal bool TryReconfigure(Camera camera, Settings settings)
        {
            if (disposed || references != 1 || Camera != camera
                || Configuration.BeforeTransparents != settings.BeforeTransparents
                || Configuration.Downsample != settings.Downsample || Configuration.Iterations != settings.Iterations)
                return false;

            for (int i = 0; i < captures.Count; i++)
            {
                if (captures[i] != this && captures[i].Matches(camera, settings))
                    return false;
            }

            Configuration = settings;
            RequestRefresh();
            return true;
        }

        internal static BackdropBlurCapture Acquire(Camera camera, Settings settings, Shader shader)
        {
            for (int i = 0; i < captures.Count; i++)
            {
                if (!captures[i].Matches(camera, settings))
                    continue;

                captures[i].references++;
                return captures[i];
            }

            BackdropBlurCapture result = new(camera, settings, shader) { references = 1 };
            captures.Add(result);
            
            if (GraphicsSettings.currentRenderPipeline == null)
                EnsureBuiltInTarget(camera);
           
            if (captures.Count == 1)
            {
                Camera.onPreCull += PrepareBuiltIn;
                RenderPipelineManager.beginCameraRendering += CheckPipeline;
                lastPipeline = GraphicsSettings.currentRenderPipeline;
            }

            return result;
        }

        internal void Release()
        {
            if (disposed || --references > 0)
                return;

            disposed = true;
            DetachCommands();
            Released?.Invoke();
            ReleaseTextures();
            captures.Remove(this);
            Changed = Released = TexturesReleasing = null;
            bool cameraInUse = false;
           
            foreach (BackdropBlurCapture capture in captures)
                cameraInUse |= capture.Camera == Camera;
          
            if (!cameraInUse && cameraEffects.Remove(Camera, out BackdropBlurCamera effect))
            {
                if (effect != null)
                    effect.enabled = false;

                DestroyResource(effect);
            }

            if (captures.Count == 0)
            {
                Camera.onPreCull -= PrepareBuiltIn;
                RenderPipelineManager.beginCameraRendering -= CheckPipeline;
                DestroyResource(filter);
                DestroyResource(arrayFilter);
                filter = null;
                arrayFilter = null;
                imageCommands?.Release();
                imageCommands = null;
                AllReleased?.Invoke();
            }
        }

        static void CheckPipeline(ScriptableRenderContext context, Camera camera)
        {
            if (lastPipeline == GraphicsSettings.currentRenderPipeline)
                return;

            lastPipeline = GraphicsSettings.currentRenderPipeline;
        
            foreach (BackdropBlurCamera effect in cameraEffects.Values)
            {
                if (effect != null)
                    effect.enabled = false;

                DestroyResource(effect);
            }

            cameraEffects.Clear();
        
            foreach (BackdropBlurCapture capture in captures)
            {
                capture.DetachCommands();
                capture.ReleaseTextures();
                capture.countdown = 0;
                capture.Changed?.Invoke();
            }
        }

        static void PrepareBuiltIn(Camera camera)
        {
            CheckPipeline(default, camera);
         
            if (GraphicsSettings.currentRenderPipeline != null)
                return;

            foreach (BackdropBlurCapture capture in captures)
            {
                if (capture.Camera != camera)
                    continue;

                EnsureBuiltInTarget(camera);

                if (!capture.Configuration.BeforeTransparents)
                    continue;

                if (capture.commands == null)
                {
                    capture.cameraEvent = CameraEvent.BeforeForwardAlpha;
                    capture.commands = new() { name = ProfilerName };
                    camera.AddCommandBuffer(capture.cameraEvent, capture.commands);
                }

                capture.commands.Clear();
                GraphicsFormat format = SystemInfo.GetGraphicsFormat(camera.allowHDR ? DefaultFormat.HDR : DefaultFormat.LDR);
                if (!capture.Prepare(camera.pixelWidth, camera.pixelHeight, format))
                    continue;

                int width = camera.targetTexture != null 
                    ? camera.targetTexture.width 
                    : Mathf.RoundToInt(camera.pixelWidth / camera.rect.width);
                int height = camera.targetTexture != null 
                    ? camera.targetTexture.height 
                    : Mathf.RoundToInt(camera.pixelHeight / camera.rect.height);
              
                Rect rect = camera.rect;
                capture.Record(capture.commands, BuiltinRenderTextureType.CurrentActive, width, height,
                    new Vector4(rect.width, rect.height, rect.x, rect.y), copySource: true);
                capture.commands.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
                capture.commands.SetViewport(camera.pixelRect);
            }
        }

        static void EnsureBuiltInTarget(Camera camera)
        {
            if (cameraEffects.TryGetValue(camera, out BackdropBlurCamera existing) && existing != null)
                return;
           
            BackdropBlurCamera effect = camera.gameObject.AddComponent<BackdropBlurCamera>();
            effect.hideFlags = HideFlags.HideInInspector | HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            cameraEffects[camera] = effect;
        }

        internal static void RenderBuiltIn(Camera camera, RenderTexture source)
        {
            if (GraphicsSettings.currentRenderPipeline != null)
                return;

            CommandBuffer cmd = null;

            foreach (BackdropBlurCapture capture in captures)
            {
                if (capture.Camera != camera || capture.Configuration.BeforeTransparents
                    || !capture.Prepare(camera.pixelWidth, camera.pixelHeight, source.graphicsFormat))
                    continue;

                if (cmd == null)
                {
                    imageCommands ??= new CommandBuffer { name = ProfilerName };
                    cmd = imageCommands;
                    cmd.Clear();
                }

                Rect rect = source.width == camera.pixelWidth && source.height == camera.pixelHeight
                    ? new Rect(0f, 0f, 1f, 1f) : camera.rect;
                capture.Record(cmd, source, source.width, source.height,
                    new Vector4(rect.width, rect.height, rect.x, rect.y), copySource: true);
            }

            if (cmd != null)
                Graphics.ExecuteCommandBuffer(cmd);
        }

        internal static void DestroyResource(UnityEngine.Object resource)
        {
            if (resource == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(resource);
            else
                UnityEngine.Object.DestroyImmediate(resource);
        }

        void Draw(CommandBuffer cmd, RenderTargetIdentifier source, RenderTexture destination,
            int width, int height, Vector4 scaleBias, int pass, bool sourceArray = false)
        {
            cmd.SetGlobalTexture(sourceArray ? SourceArrayId : SourceId, source);
            cmd.SetGlobalVector(TexelId, new Vector4(1f / width, 1f / height, width, height));
            cmd.SetGlobalVector(ScaleBiasId, scaleBias);
            cmd.SetRenderTarget(destination, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            cmd.SetViewport(new Rect(0f, 0f, destination.width, destination.height));
            cmd.DrawProcedural(Matrix4x4.identity, sourceArray ? arrayFilter : filter, pass, MeshTopology.Triangles, 3);
        }

        void DetachCommands()
        {
            if (commands == null)
                return;
           
            if (Camera != null)
                Camera.RemoveCommandBuffer(cameraEvent, commands);
          
            commands.Release();
            commands = null;
        }

        void ReleaseTextures()
        {
            TexturesReleasing?.Invoke();
            DestroyResource(resolvedSource);
            resolvedSource = null;
           
            foreach (RenderTexture texture in Levels)
                DestroyResource(texture);
           
            Levels = Array.Empty<RenderTexture>();
            Ready = false;
        }

        public void RequestRefresh() => countdown = 0;

        /// <summary>
        /// Allocate only on size/format changes. Call once per camera render, including skipped updates.
        /// </summary>
        public bool Prepare(int width, int height, GraphicsFormat format)
        {
            if (disposed || width <= 0 || height <= 0 || shader == null || !shader.isSupported)
                return false;

            width = Mathf.Max(1, width / Configuration.Downsample);
            height = Mathf.Max(1, height / Configuration.Downsample);

            int count = 1;
            for (int w = width, h = height; count < Configuration.Iterations + 1 && (w > 1 || h > 1); count++)
            {
                w = Mathf.Max(1, w / 2);
                h = Mathf.Max(1, h / 2);
            }

            if (Texture == null || !Texture.IsCreated() || Texture.width != width
                || Texture.height != height || Texture.graphicsFormat != format)
            {
                ReleaseTextures();
                Levels = new RenderTexture[count];

                for (int i = 0; i < count; i++)
                {
                    RenderTextureDescriptor descriptor = new(width, height)
                    {
                        graphicsFormat = format,
                        depthBufferBits = 0,
                        msaaSamples = 1,
                        useMipMap = false,
                        autoGenerateMips = false,
                        useDynamicScale = false
                    };
                    Levels[i] = new(descriptor)
                    {
                        name = $"BackdropBlur_{Camera.name}[{i}]",
                        hideFlags = HideFlags.HideAndDontSave,
                        filterMode = FilterMode.Bilinear,
                        wrapMode = TextureWrapMode.Clamp
                    };
                    if (!Levels[i].Create())
                    {
                        ReleaseTextures();
                        Changed?.Invoke();
                        return false;
                    }
                    width = Mathf.Max(1, width / 2);
                    height = Mathf.Max(1, height / 2);
                }

                Ready = false;
                countdown = 0;
            }

            if (countdown-- > 0)
                return false;

            countdown = Configuration.Interval - 1;

            if (filter == null && shader != null)
            {
                filter = new Material(shader)
                {
                    name = "Backdrop Blur Filter",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
            return filter != null;
        }

        /// <summary>
        /// Source scale/bias selects the valid RTHandle viewport (or Built-in camera rect).
        /// Built-in uses Unity's resolved blit source; SRP sources already use render-texture orientation.
        /// </summary>
        public void Record(CommandBuffer cmd, RenderTargetIdentifier source, int width, int height,
            Vector4 scaleBias, bool sourceArray = false, bool copySource = false)
        {
            if (disposed || filter == null || Texture == null)
                return;

            if (sourceArray && arrayFilter == null)
            {
                arrayFilter = new(filter) { hideFlags = HideFlags.HideAndDontSave };
                arrayFilter.EnableKeyword(ArrayKeyword);
            }

            cmd.BeginSample(ProfilerName);
            cmd.SetGlobalFloat(OffsetId, Configuration.Radius);

            if (copySource)
                cmd.Blit(source, Levels[0], new Vector2(scaleBias.x, scaleBias.y), new Vector2(scaleBias.z, scaleBias.w));
            else
                Draw(cmd, source, Levels[0], width, height, scaleBias, 0, sourceArray);

            for (int i = 1; i < Levels.Length; i++)
            {
                Draw(cmd, Levels[i - 1], Levels[i], Levels[i - 1].width, Levels[i - 1].height,
                    new Vector4(1f, 1f, 0f, 0f), 0, false);
            }

            for (int i = Levels.Length - 2; i >= 0; i--)
            {
                Draw(cmd, Levels[i + 1], Levels[i], Levels[i + 1].width, Levels[i + 1].height,
                    new Vector4(1f, 1f, 0f, 0f), 1, false);
            }

            cmd.EndSample(ProfilerName);

            if (!Ready)
            {
                Ready = true;
                Changed?.Invoke();
            }
        }

        /// <summary>
        /// HDRP can expose an explicitly bound MSAA surface before transparents.
        /// </summary>
        public void Record(CommandBuffer cmd, RenderTexture source, Vector4 scaleBias)
        {
            if (disposed || source == null)
                return;

            if (source.antiAliasing > 1)
            {
                if (resolvedSource == null || !resolvedSource.IsCreated() || resolvedSource.width != source.width
                    || resolvedSource.height != source.height || resolvedSource.graphicsFormat != source.graphicsFormat
                    || resolvedSource.dimension != source.dimension || resolvedSource.volumeDepth != source.volumeDepth)
                {
                    DestroyResource(resolvedSource);
                    RenderTextureDescriptor descriptor = source.descriptor;
                    descriptor.msaaSamples = 1;
                    descriptor.bindMS = false;
                    descriptor.depthBufferBits = 0;
                    descriptor.useDynamicScale = false;
                    resolvedSource = new(descriptor)
                    {
                        name = "BackdropBlurMSAAResolve",
                        hideFlags = HideFlags.HideAndDontSave
                    };
                    if (!resolvedSource.Create())
                        return;
                }

                cmd.ResolveAntiAliasedSurface(source, resolvedSource);
                source = resolvedSource;
            }

            Record(cmd, source, source.width, source.height, scaleBias, source.dimension == TextureDimension.Tex2DArray);
        }
    }
}