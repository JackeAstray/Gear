#if EVO_UI_HDRP
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Evo.UI.Rendering
{
    /// <summary>
    /// Registers transient global Custom Passes without creating scene objects or volume assets.
    /// </summary>
    [NoAutoStaticsCleanup]
    static class BackdropBlurHDRP
    {
        static BlurPass early, late;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            Cleanup();
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            BackdropBlurCapture.AllReleased -= Cleanup;
            BackdropBlurCapture.AllReleased += Cleanup;
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        static void InitializeEditor()
        {
            Initialize();
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= Cleanup;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
        }
#endif

        static void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (GraphicsSettings.currentRenderPipeline is not HDRenderPipelineAsset || BackdropBlurCapture.Active.Count == 0)
            {
                Cleanup();
                return;
            }

            if (early != null)
                return;

            early = new(true);
            late = new(false);
            CustomPassVolume.RegisterGlobalCustomPass(CustomPassInjectionPoint.BeforeTransparent, early);
            CustomPassVolume.RegisterGlobalCustomPass(CustomPassInjectionPoint.AfterPostProcess, late);
        }

        static void Cleanup()
        {
            if (early != null)
                CustomPassVolume.UnregisterGlobalCustomPass(early);
          
            if (late != null)
                CustomPassVolume.UnregisterGlobalCustomPass(late);
          
            early = late = null;
        }

        sealed class BlurPass : CustomPass
        {
            readonly bool beforeTransparents;

            public BlurPass(bool beforeTransparents)
            {
                this.beforeTransparents = beforeTransparents;
                name = BackdropBlurCapture.ProfilerName;
                targetColorBuffer = TargetBuffer.None;
                targetDepthBuffer = TargetBuffer.None;
            }

            protected override void Execute(CustomPassContext context)
            {
                Camera camera = context.hdCamera.camera;
                if (camera.stereoEnabled)
                    return;

                IReadOnlyList<BackdropBlurCapture> captures = BackdropBlurCapture.Active;
                for (int i = 0; i < captures.Count; i++)
                {
                    BackdropBlurCapture capture = captures[i];
                    if (capture.Camera != camera || capture.Configuration.BeforeTransparents != beforeTransparents)
                        continue;

                    RTHandle source = context.cameraColorBuffer;
                    RenderTexture texture = source.rt;
                    if (texture == null)
                        continue;
                  
                    int width = context.hdCamera.actualWidth;
                    int height = context.hdCamera.actualHeight;
                    if (!capture.Prepare(width, height, texture.graphicsFormat))
                        continue;

                    capture.Record(context.cmd, texture, new Vector4((float)width / texture.width, (float)height / texture.height, 0f, 0f));
                }
            }
        }
    }
}
#endif