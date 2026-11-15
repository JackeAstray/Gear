using UnityEngine;
using UnityEngine.UI;
using Gear.Utilities.UI.ImageExtensions;

namespace Gear.Utilities.UI.Backdrop
{
    /// <summary>
    /// UI 背景模糊：抓取"面板背后的场景"，做多级 4-tap 模糊，再把结果贴到当前 UI 图形上。
    /// <para>
    /// 工作方式：
    /// 1. 复制源相机的参数生成一个只渲染到 RenderTexture 的隐藏相机（排除 UI 层）；
    /// 2. 在 <see cref="Canvas.willRenderCanvases"/>（相机渲染之后、Canvas 绘制之前）做降采样 + 多次模糊；
    /// 3. 把结果写入面板材质的 <c>_BackdropTex</c>，面板着色器按屏幕 UV 采样。
    /// </para>
    /// <para>
    /// 限制与建议：
    /// - 需要 URP/Built-in 下"相机照常渲染"的能力：抓屏相机是真实相机（enabled），不是手动 Render；
    /// - 每帧会多渲染一次场景，请配合降采样（默认 2）使用，并在面板关闭时禁用组件；
    /// - 可挂在 Image / RawImage 上；也可挂在 ImageEx 上（保留 SDF 形状/圆角/描边，只把填充换成模糊画面）；
    /// - 纯 Overlay Canvas 可用；Screen Space - Camera / World Space 下屏幕 UV 仍然按最终屏幕坐标采样。
    /// </para>
    /// </summary>
    [AddComponentMenu("UI/Gear/Backdrop Blur")]
    [DisallowMultipleComponent]
    public sealed class UIBackdropBlur : MonoBehaviour
    {
        [SerializeField, Tooltip("抓取哪个相机的画面；为空时自动取 Canvas 的 worldCamera 或 Camera.main")]
        private Camera sourceCamera;

        [SerializeField, Range(0, 4), Tooltip("降采样倍率：0=全分辨率，2=宽高各 1/4 像素量")]
        private int downsample = 2;

        [SerializeField, Range(0, 4), Tooltip("模糊迭代次数：越多越糊，也越贵")]
        private int iterations = 2;

        [SerializeField, Range(0.25f, 8f), Tooltip("每次迭代的采样半径（源图像素）")]
        private float blurRadius = 2f;

        [SerializeField, Range(1, 4), Tooltip("每 N 帧抓屏一次：1=每帧，2/3/4 可显著省 GPU（模糊结果复用上一帧）")]
        private int captureInterval = 1;

        [SerializeField, Tooltip("面板完全透明时暂停抓屏（由 CanvasRenderer alpha 判断）")]
        private bool skipWhenInvisible = true;

        [SerializeField, Tooltip("额外排除的层（UI 层会自动排除）")]
        private LayerMask excludeLayers;

        [SerializeField, Tooltip("同源相机/分辨率共享一台隐藏抓屏相机")]
        private bool useSharedCapture = true;

        [Header("玻璃参数")]
        [SerializeField, Tooltip("玻璃染色")]
        private Color glassTint = Color.white;

        [SerializeField, Range(0f, 1f), Tooltip("染色比例")]
        private float tintAmount = 0.6f;

        [SerializeField, Range(0f, 2f), Tooltip("饱和度：0 灰度，1 原色")]
        private float saturation = 1f;

        [SerializeField, Range(0f, 2f), Tooltip("亮度")]
        private float brightness = 1f;

        [SerializeField, Range(0f, 0.2f), Tooltip("噪点强度")]
        private float noise = 0.1f;

        [SerializeField, Range(0f, 1f), Tooltip("背景模糊对填充的替换强度：1 = 完全玻璃，0 = 保留原填充")]
        private float backdropStrength = 1f;

        private static readonly int backdropTexId = Shader.PropertyToID("_BackdropTex");
        private static readonly int enableBackdropBlurId = Shader.PropertyToID("_EnableBackdropBlur");
        private static readonly int backdropStrengthId = Shader.PropertyToID("_BackdropStrength");
        private static readonly int offsetId = Shader.PropertyToID("_Offset");
        private static readonly int glassTintId = Shader.PropertyToID("_GlassTint");
        private static readonly int tintAmountId = Shader.PropertyToID("_TintAmount");
        private static readonly int saturationId = Shader.PropertyToID("_Saturation");
        private static readonly int brightnessId = Shader.PropertyToID("_Brightness");
        private static readonly int noiseId = Shader.PropertyToID("_Noise");

        private const string panelShaderName = "Gear/UI/BackdropBlurPanel";
        private const string panelShaderPath = "Shader/UI/BackdropBlur/BackdropBlurPanel";
        private const string blurShaderName = "Hidden/Gear/UI/BackdropBlur";
        private const string blurShaderPath = "Shader/UI/BackdropBlur/BackdropBlur";

        private Graphic graphic;
        private Material panelMaterial;
        private Material blurMaterial;
        private Camera captureCamera;
        private RenderTexture captureTexture;
        private RenderTexture pingTexture;
        private RenderTexture pongTexture;
        private bool usingImageEx;
        private bool sharedCapture;

        public bool UseSharedCapture
        {
            get => useSharedCapture;
            set => useSharedCapture = value;
        }

        private int lastWidth;
        private int lastHeight;
        private bool hasResult;
        private bool warnedSourceCamera;

        private void OnEnable()
        {
            usingImageEx = false;
            graphic = GetComponent<Graphic>();
            if (graphic == null)
            {
                Debug.LogError("[UIBackdropBlur] 需要挂在带 Graphic（Image / RawImage）的物体上。", this);
                enabled = false;
                return;
            }

            if (graphic is ImageEx)
            {
                // ImageEx 保留自己的 SDF 材质：把模糊纹理写进它的材质，由 RM_Backdrop 采样，
                // 这样圆角/描边/图案等 ImageEx 能力仍然生效。
                usingImageEx = true;
            }

            if (!CreateMaterials())
            {
                enabled = false;
                return;
            }

            if (!usingImageEx)
            {
                graphic.material = panelMaterial;
            }
            Canvas.willRenderCanvases += OnWillRenderCanvases;
        }

        private void OnDisable()
        {
            Canvas.willRenderCanvases -= OnWillRenderCanvases;

            if (graphic != null)
            {
                if (usingImageEx)
                {
                    Material material = graphic.material;
                    if (material != null) material.SetFloat(enableBackdropBlurId, 0f);
                }
                else
                {
                    graphic.material = null;
                }
            }

            if (captureCamera != null)
            {
                if (sharedCapture)
                {
                    UIBackdropSharedCapture.Release(captureCamera);
                    captureTexture = null;
                }
                else
                {
                    Destroy(captureCamera.gameObject);
                }

                captureCamera = null;
                sharedCapture = false;
            }

            ReleaseTextures();

            if (panelMaterial != null)
            {
                Destroy(panelMaterial);
                panelMaterial = null;
            }

            if (blurMaterial != null)
            {
                Destroy(blurMaterial);
                blurMaterial = null;
            }

            warnedSourceCamera = false;
        }

        private bool CreateMaterials()
        {
            Shader panelShader = Shader.Find(panelShaderName) ?? Resources.Load<Shader>(panelShaderPath);
            Shader blurShader = Shader.Find(blurShaderName) ?? Resources.Load<Shader>(blurShaderPath);

            if (panelShader == null || blurShader == null)
            {
                Debug.LogError($"[UIBackdropBlur] 找不到着色器：{panelShaderName} / {blurShaderName}。", this);
                return false;
            }

            panelMaterial = new Material(panelShader) { name = "UI Backdrop Blur (Panel)" };
            blurMaterial = new Material(blurShader) { name = "UI Backdrop Blur (Blur)" };
            return true;
        }

        private void OnWillRenderCanvases()
        {
            if (!EnsureCapture()) return;

            ApplyGlassSettings();

            // 面板不可见时直接停抓屏（CanvasRenderer alpha 为 0，或物体被禁用）
            if (skipWhenInvisible && (!graphic.isActiveAndEnabled || graphic.canvasRenderer.GetAlpha() <= 0.001f))
            {
                if (captureCamera != null) captureCamera.enabled = false;
                return;
            }

            int interval = Mathf.Clamp(captureInterval, 1, 4);
            bool shouldCapture = !hasResult || interval <= 1 || (Time.frameCount % interval) == 0;
            if (captureCamera != null)
            {
                if (sharedCapture)
                {
                    if (shouldCapture) UIBackdropSharedCapture.RequestFrame(captureCamera, true);
                }
                else
                {
                    captureCamera.enabled = shouldCapture;
                }
            }

            // 非抓屏帧复用上一轮结果，避免每帧重算
            if (!shouldCapture)
            {
                ApplyBackdropTexture();
                return;
            }

            // Graphics.Blit 会把 active render target 留在目标 RT 上，
            // 不还原会让后续 Canvas 画进我们的 RT（黑屏/花屏的常见原因）。
            RenderTexture previous = RenderTexture.active;
            try
            {
                Graphics.Blit(captureTexture, pingTexture);

                float radius = Mathf.Max(0.25f, blurRadius);
                for (int i = 0; i < iterations; i++)
                {
                    blurMaterial.SetVector(offsetId, new Vector4(radius * (i + 1), radius * (i + 1), 0f, 0f));
                    Graphics.Blit(pingTexture, pongTexture, blurMaterial);

                    RenderTexture swap = pingTexture;
                    pingTexture = pongTexture;
                    pongTexture = swap;
                }

                ApplyBackdropTexture();
                hasResult = true;
            }
            finally
            {
                RenderTexture.active = previous;
            }
        }

        private void ApplyBackdropTexture()
        {
            if (pingTexture == null) return;
            Material target = usingImageEx ? graphic?.material : panelMaterial;
            if (target != null) target.SetTexture(backdropTexId, pingTexture);
        }

        private void ApplyGlassSettings()
        {
            Material target = usingImageEx ? graphic?.material : panelMaterial;
            if (target == null) return;
            target.SetColor(glassTintId, glassTint);
            target.SetFloat(tintAmountId, tintAmount);
            target.SetFloat(saturationId, saturation);
            target.SetFloat(brightnessId, brightness);
            target.SetFloat(noiseId, noise);
            target.SetFloat(backdropStrengthId, backdropStrength);
            if (usingImageEx) target.SetFloat(enableBackdropBlurId, 1f);
        }

        private bool EnsureCapture()
        {
            Camera source = ResolveSourceCamera();
            if (source == null)
            {
                if (!warnedSourceCamera)
                {
                    warnedSourceCamera = true;
                    Debug.LogWarning("[UIBackdropBlur] 找不到源相机，背景模糊暂停。", this);
                }
                return false;
            }

            int scale = Mathf.Clamp(downsample, 0, 4);
            int width = Mathf.Max(16, Screen.width >> scale);
            int height = Mathf.Max(16, Screen.height >> scale);

            if (useSharedCapture)
                return EnsureSharedCapture(source, width, height);

            if (captureCamera == null)
            {
                GameObject cameraObject = new GameObject("~UIBackdropBlurCapture")
                {
                    hideFlags = HideFlags.HideAndDontSave,
                };
                captureCamera = cameraObject.AddComponent<Camera>();
                captureCamera.CopyFrom(source);

                int canvasLayer = GetCanvasLayer();
                int mask = source.cullingMask & ~(1 << canvasLayer) & ~excludeLayers.value;
                if (mask == 0) mask = source.cullingMask & ~(1 << canvasLayer);

                captureCamera.cullingMask = mask;
                captureCamera.depth = source.depth - 0.5f;
                captureCamera.targetTexture = null;
                captureCamera.allowHDR = false;
                captureCamera.allowMSAA = false;
                captureCamera.enabled = true;

                lastWidth = 0;
                lastHeight = 0;
            }

            if (width != lastWidth || height != lastHeight || captureTexture == null)
            {
                ReleaseTextures();
                lastWidth = width;
                lastHeight = height;

                captureTexture = CreateTexture(width, height, "UI Backdrop Capture");
                pingTexture = CreateTexture(width, height, "UI Backdrop Ping");
                pongTexture = CreateTexture(width, height, "UI Backdrop Pong");
                captureCamera.targetTexture = captureTexture;
            }

            return true;
        }

        private bool EnsureSharedCapture(Camera source, int width, int height)
        {
            if (captureCamera == null || width != lastWidth || height != lastHeight || captureTexture == null)
            {
                if (captureCamera != null)
                {
                    UIBackdropSharedCapture.Release(captureCamera);
                    captureCamera = null;
                    captureTexture = null;
                }

                ReleaseTexture(ref pingTexture);
                ReleaseTexture(ref pongTexture);

                int mask = source.cullingMask & ~(1 << GetCanvasLayer()) & ~excludeLayers.value;
                if (mask == 0) mask = source.cullingMask & ~(1 << GetCanvasLayer());

                captureCamera = UIBackdropSharedCapture.Acquire(
                    source, Mathf.Clamp(downsample, 0, 4), mask, width, height, out captureTexture);
                if (captureCamera == null) return false;

                sharedCapture = true;
                lastWidth = width;
                lastHeight = height;
                pingTexture = CreateTexture(width, height, "UI Backdrop Ping");
                pongTexture = CreateTexture(width, height, "UI Backdrop Pong");
            }

            return true;
        }

        private Camera ResolveSourceCamera()
        {
            if (sourceCamera != null) return sourceCamera;

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null && canvas.worldCamera != null)
            {
                sourceCamera = canvas.worldCamera;
                return sourceCamera;
            }

            sourceCamera = Camera.main;
            return sourceCamera;
        }

        private int GetCanvasLayer()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            return canvas != null ? canvas.gameObject.layer : gameObject.layer;
        }

        private static RenderTexture CreateTexture(int width, int height, string name)
        {
            RenderTexture texture = new RenderTexture(width, height, 16, RenderTextureFormat.Default)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.Create();
            return texture;
        }

        private void ReleaseTextures()
        {
            if (!sharedCapture) ReleaseTexture(ref captureTexture);
            ReleaseTexture(ref pingTexture);
            ReleaseTexture(ref pongTexture);
            lastWidth = 0;
            lastHeight = 0;
            hasResult = false;
        }

        private static void ReleaseTexture(ref RenderTexture texture)
        {
            if (texture == null) return;
            texture.Release();
            Destroy(texture);
            texture = null;
        }
    }
}
