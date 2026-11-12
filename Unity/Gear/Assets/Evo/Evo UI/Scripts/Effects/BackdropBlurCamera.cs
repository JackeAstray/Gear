using UnityEngine;

namespace Evo.UI
{
    /// <summary>
    /// Built-in's resolved camera source. Created and owned by BackdropBlurCapture.
    /// </summary>
    [ExecuteAlways, AddComponentMenu(""), RequireComponent(typeof(Camera))]
    public sealed class BackdropBlurCamera : MonoBehaviour
    {
        Camera sourceCamera;

        void OnEnable() => sourceCamera = GetComponent<Camera>();

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            // Always preserve the camera image, even when capture recording fails
            try { BackdropBlurCapture.RenderBuiltIn(sourceCamera, source); }
            finally { Graphics.Blit(source, destination); }
        }
    }
}