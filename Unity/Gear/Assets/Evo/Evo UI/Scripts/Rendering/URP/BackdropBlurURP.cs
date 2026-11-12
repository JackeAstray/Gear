#if EVO_UI_URP
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Evo.UI.Rendering
{
    /// <summary>
    /// Enqueues directly on the camera renderer; no Renderer Feature asset or renderer mutation.
    /// </summary>
    [NoAutoStaticsCleanup]
    static class BackdropBlurURP
    {
        static readonly Dictionary<BackdropBlurCapture, BlurPass> passes = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.beginCameraRendering += BeginCamera;
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        static void InitializeEditor() => Initialize();
#endif

        static void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset asset
                || camera.stereoEnabled || BackdropBlurCapture.Active.Count == 0)
                return;

            ScriptableRenderer renderer = camera.TryGetComponent(out UniversalAdditionalCameraData cameraData)
                ? cameraData.scriptableRenderer 
                : asset.scriptableRenderer;
            
            if (renderer == null)
                return;

            IReadOnlyList<BackdropBlurCapture> captures = BackdropBlurCapture.Active;
            for (int i = 0; i < captures.Count; i++)
            {
                BackdropBlurCapture capture = captures[i];
                if (capture.Camera != camera)
                    continue;

                if (!passes.TryGetValue(capture, out BlurPass pass))
                {
                    pass = new(capture);
                    passes.Add(capture, pass);
                }

                renderer.EnqueuePass(pass);
            }
        }

        sealed class BlurPass : ScriptableRenderPass
        {
            readonly BackdropBlurCapture capture;
            RTHandle[] handles;

            sealed class PassData
            {
                public TextureHandle Source;
                public BackdropBlurCapture Capture;
                public int Width, Height;
            }

            public BlurPass(BackdropBlurCapture capture)
            {
                this.capture = capture;
                renderPassEvent = capture.Configuration.BeforeTransparents
                    ? RenderPassEvent.BeforeRenderingTransparents 
                    : RenderPassEvent.AfterRenderingPostProcessing;
                requiresIntermediateTexture = true;
                capture.Released += Release;
                capture.TexturesReleasing += ReleaseHandles;
            }

            void Release()
            {
                ReleaseHandles();
                capture.Released -= Release;
                capture.TexturesReleasing -= ReleaseHandles;
                passes.Remove(capture);
            }

            void ReleaseHandles()
            {
                if (handles != null)
                {
                    foreach (RTHandle handle in handles)
                        handle.Release();
                   
                    handles = null;
                }
            }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                UniversalCameraData camera = frameData.Get<UniversalCameraData>();
                if (resources.isActiveTargetBackBuffer)
                    return;

                RenderTextureDescriptor descriptor = camera.cameraTargetDescriptor;
                if (!capture.Prepare(descriptor.width, descriptor.height, descriptor.graphicsFormat))
                    return;

                if (handles == null)
                {
                    handles = new RTHandle[capture.Levels.Length];
                    for (int i = 0; i < handles.Length; i++)
                        handles[i] = RTHandles.Alloc(capture.Levels[i]);
                }

                // One unsafe pass shares the exact filter with BIRP/HDRP. Every persistent texture is declared,
                // including the output consumed by Canvas outside the graph. No hidden graph resources are used.
                using IUnsafeRenderGraphBuilder builder = graph.AddUnsafePass<PassData>(BackdropBlurCapture.ProfilerName, out PassData data);
                data.Source = resources.activeColorTexture;
                data.Capture = capture;
                data.Width = descriptor.width;
                data.Height = descriptor.height;
                builder.UseTexture(data.Source, AccessFlags.Read);
               
                foreach (RTHandle handle in handles)
                    builder.UseTexture(graph.ImportTexture(handle), AccessFlags.ReadWrite);
              
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc(static (PassData data, UnsafeGraphContext context) =>
                {
                    RTHandle source = data.Source;
                    RenderTexture texture = source.rt;
                    int width = texture != null ? texture.width : data.Width;
                    int height = texture != null ? texture.height : data.Height;
                    CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                    data.Capture.Record(cmd, source.nameID, width, height, new Vector4((float)data.Width / width, (float)data.Height / height, 0f, 0f));
                });
            }

#if !UNITY_6000_4_OR_NEWER
            // Compatibility Mode (Render Graph disabled) only; Unity 6.4 removed it along with this method
#pragma warning disable CS0672, CS0618
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
                if (!capture.Prepare(descriptor.width, descriptor.height, descriptor.graphicsFormat))
                    return;

                RTHandle source = renderingData.cameraData.renderer.cameraColorTargetHandle;
                int width = source.rt != null ? source.rt.width : descriptor.width;
                int height = source.rt != null ? source.rt.height : descriptor.height;
                CommandBuffer cmd = CommandBufferPool.Get(BackdropBlurCapture.ProfilerName);

                try
                {
                    capture.Record(cmd, source.nameID, width, height, new Vector4((float)descriptor.width / width, (float)descriptor.height / height, 0f, 0f));
                    CoreUtils.SetRenderTarget(cmd, source, renderingData.cameraData.renderer.cameraDepthTargetHandle);
                    context.ExecuteCommandBuffer(cmd);
                }
                finally
                {
                    CommandBufferPool.Release(cmd);
                }
            }
#pragma warning restore CS0672, CS0618
#endif
        }
    }
}
#endif