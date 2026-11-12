using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gear.Utilities.UI.ImageExtensions.Overlays
{
    /// <summary>
    /// UI 覆盖层公共设施：运行时 Canvas、默认配色、圆角面板与文本创建。
    /// <para>
    /// Canvas 会优先挂到 <c>TipsUIRoot</c> 下（若 UISystem 已初始化），否则自建一个
    /// DontDestroyOnLoad 的 Overlay Canvas；排序值高于普通窗口，保证 Toast/Tooltip/菜单在最上层。
    /// </para>
    /// </summary>
    internal static class GearOverlayLayer
    {
        private const int SortingOrder = 30000;
        private static Canvas s_canvas;
        private static RectTransform s_root;

        internal static RectTransform Root
        {
            get
            {
                EnsureCanvas();
                return s_root;
            }
        }

        private static void EnsureCanvas()
        {
            if (s_canvas != null && s_root != null) return;

            GameObject canvasObject = new GameObject("GearOverlayCanvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.layer = LayerMask.NameToLayer("UI");

            // 尽量复用 UISystem 的分层根；找不到时自建一个常驻 Canvas
            GameObject tipsRoot = GameObject.Find("TipsUIRoot");
            if (tipsRoot != null)
            {
                canvasObject.transform.SetParent(tipsRoot.transform, false);
            }
            else
            {
                Object.DontDestroyOnLoad(canvasObject);
            }

            s_canvas = canvasObject.GetComponent<Canvas>();
            s_canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            s_canvas.sortingOrder = SortingOrder;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GameObject rootObject = new GameObject("OverlayRoot", typeof(RectTransform));
            rootObject.transform.SetParent(canvasObject.transform, false);
            s_root = (RectTransform)rootObject.transform;
            s_root.anchorMin = Vector2.zero;
            s_root.anchorMax = Vector2.one;
            s_root.offsetMin = Vector2.zero;
            s_root.offsetMax = Vector2.zero;
        }

        internal static ImageEx CreateRoundedPanel(Transform parent, string name, Color color, float radius, bool raycast = false, bool shadow = false)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx));
            ApplyUILayer(go);
            go.transform.SetParent(parent, false);

            ImageEx image = go.GetComponent<ImageEx>();
            image.DrawShape = DrawShape.Rectangle;
            image.Rectangle = new RectangleImg
            {
                CornerRadius = new Vector4(radius, radius, radius, radius),
            };
            image.color = color;
            image.raycastTarget = raycast;

            if (shadow)
            {
                image.AppendShadow = true;
                image.Shadow = ImageEx.ShadowMode.Shadow;
                image.ShadowOffsetLocal = new Vector2(0f, -8f);
                image.ShadowBlurIntensity = 8f;
                image.ShadowColor = new Color(0f, 0f, 0f, 0.45f);
            }

            return image;
        }

        internal static TextMeshProUGUI CreateText(
            Transform parent, string name, float fontSize, Color color, bool bold, TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            ApplyUILayer(go);
            go.transform.SetParent(parent, false);

            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.color = color;
            text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            text.overflowMode = TextOverflowModes.Overflow;

            TMP_FontAsset font = TMP_Settings.defaultFontAsset;
            if (font == null)
            {
                font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            }
            if (font != null) text.font = font;

            return text;
        }

        internal static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        internal static void ApplyUILayer(GameObject go)
        {
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0) go.layer = uiLayer;
        }
    }

    /// <summary>
    /// 覆盖层默认配色。若 <see cref="ImageExThemeService"/> 当前主题声明了同名令牌，优先使用主题值。
    /// </summary>
    internal static class GearOverlayTheme
    {
        internal static readonly Color Surface = new Color(0.078f, 0.086f, 0.114f, 0.97f);
        internal static readonly Color SurfaceRaised = new Color(0.118f, 0.129f, 0.169f, 0.99f);
        internal static readonly Color TextPrimary = new Color(0.95f, 0.96f, 0.98f, 1f);
        internal static readonly Color TextSecondary = new Color(0.66f, 0.69f, 0.77f, 1f);
        internal static readonly Color Accent = new Color(0.49f, 0.36f, 0.99f, 1f);
        internal static readonly Color Success = new Color(0.25f, 0.78f, 0.52f, 1f);
        internal static readonly Color Warning = new Color(1f, 0.69f, 0.25f, 1f);
        internal static readonly Color Danger = new Color(1f, 0.36f, 0.42f, 1f);

        internal static Color Get(string tokenId, Color fallback)
        {
            ImageExThemeAsset theme = ImageExThemeService.Current;
            if (theme != null && theme.TryGetColor(tokenId, out Color color)) return color;
            return fallback;
        }
    }
}
