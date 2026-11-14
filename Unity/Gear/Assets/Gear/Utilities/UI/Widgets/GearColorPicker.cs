using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Gear.Utilities.UI.ImageExtensions;

namespace Gear.Utilities.UI.Widgets
{
    /// <summary>
    /// 简化取色器：SV 方块 + Hue 滑条 + Hex 输入，不做吸管/预设库。
    /// <para>
    /// 颜色换算（Hex / HSV）是静态纯函数，EditMode 可直接断言；
    /// 指针拖动与滑条只在赋值后才生效，方便测试与复用。
    /// </para>
    /// </summary>
    [AddComponentMenu("UI/Gear/Color Picker")]
    [DisallowMultipleComponent]
    public sealed class GearColorPicker : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        [SerializeField] private RectTransform svArea;
        [SerializeField] private RectTransform svHandle;
        [SerializeField] private Slider hueSlider;
        [SerializeField] private Graphic preview;
        [SerializeField] private TMP_InputField hexInput;
        [SerializeField] private Color value = Color.white;
        [SerializeField] private UnityEvent<Color> onColorChanged = new UnityEvent<Color>();
        [SerializeField] private UnityEvent<string> onHexChanged = new UnityEvent<string>();

        private const int SvTextureSize = 64;

        private float hue;
        private float saturation = 1f;
        private float brightness = 1f;
        private bool refreshing;
        private Texture2D svTexture;
        private Sprite svSprite;
        private float svTextureHue = -999f;

        public UnityEvent<Color> OnColorChanged => onColorChanged;
        public UnityEvent<string> OnHexChanged => onHexChanged;
        public event Action<Color> ColorChanged;

        public Color Value => value;
        public string Hex => ColorToHex(value);
        public float Hue => hue;
        public float Saturation => saturation;
        public float Brightness => brightness;

        public RectTransform SvArea { get => svArea; set { svArea = value; RefreshVisuals(); } }
        public RectTransform SvHandle { get => svHandle; set { svHandle = value; RefreshVisuals(); } }

        public Slider HueSlider
        {
            get => hueSlider;
            set
            {
                Unbind();
                hueSlider = value;
                Bind();
                RefreshVisuals();
            }
        }

        public Graphic Preview { get => preview; set { preview = value; RefreshVisuals(); } }

        public TMP_InputField HexInput
        {
            get => hexInput;
            set
            {
                if (hexInput != null) hexInput.onSubmit.RemoveListener(OnHexSubmitted);
                hexInput = value;
                if (hexInput != null) hexInput.onSubmit.AddListener(OnHexSubmitted);
                RefreshVisuals();
            }
        }

        private void OnEnable()
        {
            Bind();
            ApplyHsv(value, false);
            RefreshVisuals();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void Bind()
        {
            if (hueSlider == null) return;
            hueSlider.onValueChanged.RemoveListener(OnHueSliderChanged);
            hueSlider.onValueChanged.AddListener(OnHueSliderChanged);
        }

        private void Unbind()
        {
            if (hueSlider != null) hueSlider.onValueChanged.RemoveListener(OnHueSliderChanged);
        }

        /// <summary>设置颜色（会同步 HSV/预览/Hex）。</summary>
        public void SetColor(Color color, bool notify = true)
        {
            color.a = Mathf.Clamp01(color.a);
            ApplyHsv(color, notify);
            RefreshVisuals();
        }

        /// <summary>设置 HSV（色相 0-360，自动取模；S/V 0-1 钳制）。</summary>
        public void SetHSV(float hueDegrees, float s, float v, bool notify = true)
        {
            float alpha = value.a;
            hue = Mathf.Repeat(hueDegrees, 360f);
            saturation = Mathf.Clamp01(s);
            brightness = Mathf.Clamp01(v);
            value = Color.HSVToRGB(hue / 360f, saturation, brightness);
            value.a = alpha;

            RefreshVisuals();
            if (notify) Notify();
        }

        /// <summary>Hex（#RGB / #RRGGBB / #RRGGBBAA，可省略 #）；解析失败返回 false 并保持原色。</summary>
        public bool SetHex(string hex, bool notify = true)
        {
            if (!TryParseHex(hex, out Color parsed)) return false;

            parsed.a = value.a;
            ApplyHsv(parsed, notify);
            RefreshVisuals();
            return true;
        }

        public void SetHexWithoutNotify(string hex) => SetHex(hex, false);

        /// <summary>点击/拖拽 SV 方块取色。</summary>
        public void OnPointerDown(PointerEventData eventData) => ApplyPointer(eventData);

        public void OnDrag(PointerEventData eventData) => ApplyPointer(eventData);

        private void ApplyPointer(PointerEventData eventData)
        {
            RectTransform area = svArea != null ? svArea : (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    area, eventData.position, eventData.pressEventCamera, out Vector2 local))
            {
                return;
            }

            Rect rect = area.rect;
            float s = rect.width > 0f ? Mathf.Clamp01(local.x / rect.width + 0.5f) : 0f;
            float v = rect.height > 0f ? Mathf.Clamp01(local.y / rect.height + 0.5f) : 0f;
            SetHSV(hue, s, v);
        }

        private void OnHueSliderChanged(float sliderValue)
        {
            if (refreshing) return;
            SetHSV(Mathf.Clamp01(sliderValue) * 360f, saturation, brightness);
        }

        private void OnHexSubmitted(string text)
        {
            SetHex(text);
        }

        private void ApplyHsv(Color color, bool notify)
        {
            Color.RGBToHSV(color, out float h, out float s, out float v);
            hue = h * 360f;
            saturation = s;
            brightness = v;
            value = color;

            if (notify) Notify();
        }

        private void Notify()
        {
            onColorChanged?.Invoke(value);
            onHexChanged?.Invoke(Hex);
            ColorChanged?.Invoke(value);
        }

        private void RefreshVisuals()
        {
            if (refreshing) return;
            refreshing = true;
            try
            {
                if (preview != null) preview.color = value;
                UpdateSvTexture();

                if (svHandle != null && svArea != null)
                {
                    Rect rect = svArea.rect;
                    svHandle.anchoredPosition = new Vector2(
                        (saturation - 0.5f) * rect.width,
                        (brightness - 0.5f) * rect.height);
                }

                if (hueSlider != null) hueSlider.SetValueWithoutNotify(hue / 360f);
                if (hexInput != null && !hexInput.isFocused) hexInput.SetTextWithoutNotify(Hex);
            }
            finally
            {
                refreshing = false;
            }
        }

        /// <summary>把 SV 方块刷成当前色相的 S/V 贴图（X=饱和度，Y=明度）。</summary>
        private void UpdateSvTexture()
        {
            if (svArea == null) return;
            var image = svArea.GetComponent<ImageEx>();
            if (image == null) return;

            if (svSprite == null || Mathf.Abs(svTextureHue - hue) > 0.01f)
            {
                if (svTexture == null)
                {
                    svTexture = new Texture2D(SvTextureSize, SvTextureSize, TextureFormat.RGBA32, false)
                    {
                        hideFlags = HideFlags.HideAndDontSave,
                        wrapMode = TextureWrapMode.Clamp,
                    };
                }

                for (int y = 0; y < SvTextureSize; y++)
                {
                    float v = y / (float)(SvTextureSize - 1);
                    for (int x = 0; x < SvTextureSize; x++)
                    {
                        float s = x / (float)(SvTextureSize - 1);
                        svTexture.SetPixel(x, y, Color.HSVToRGB(hue / 360f, s, v));
                    }
                }

                svTexture.Apply(false, false);
                if (svSprite != null) DestroySprite();
                svSprite = Sprite.Create(svTexture, new Rect(0f, 0f, SvTextureSize, SvTextureSize), new Vector2(0.5f, 0.5f));
                svSprite.hideFlags = HideFlags.HideAndDontSave;
                svTextureHue = hue;
            }

            image.sprite = svSprite;
            image.color = Color.white;
            var gradient = image.GradientEffect;
            if (gradient.Enabled)
            {
                gradient.Enabled = false;
                image.GradientEffect = gradient;
            }
        }

        private void OnDestroy()
        {
            DestroySprite();
            if (svTexture != null)
            {
                if (Application.isPlaying) Destroy(svTexture);
                else DestroyImmediate(svTexture);
                svTexture = null;
            }
        }

        private void DestroySprite()
        {
            if (svSprite == null) return;
            if (Application.isPlaying) Destroy(svSprite);
            else DestroyImmediate(svSprite);
            svSprite = null;
        }

        // ---------------- 静态换算 ----------------

        /// <summary>颜色 → #RRGGBB。</summary>
        public static string ColorToHex(Color color)
        {
            Color32 c = color;
            return "#" + c.r.ToString("X2") + c.g.ToString("X2") + c.b.ToString("X2");
        }

        /// <summary>解析 #RGB / #RRGGBB / #RRGGBBAA（# 可省略）。</summary>
        public static bool TryParseHex(string hex, out Color color)
        {
            color = Color.white;
            if (string.IsNullOrWhiteSpace(hex)) return false;

            string text = hex.Trim();
            if (text.StartsWith("#")) text = text.Substring(1);
            if (text.Length == 3)
            {
                text = new string(new[]
                {
                    text[0], text[0], text[1], text[1], text[2], text[2],
                });
            }

            if (text.Length != 6 && text.Length != 8) return false;

            if (!TryParseByte(text, 0, out int r)
                || !TryParseByte(text, 2, out int g)
                || !TryParseByte(text, 4, out int b))
            {
                return false;
            }

            int a = 255;
            if (text.Length == 8 && !TryParseByte(text, 6, out a)) return false;

            color = new Color32((byte)r, (byte)g, (byte)b, (byte)a);
            return true;
        }

        private static bool TryParseByte(string text, int index, out int value)
        {
            value = 0;
            for (int i = 0; i < 2; i++)
            {
                int digit = HexDigit(text[index + i]);
                if (digit < 0) return false;
                value = value * 16 + digit;
            }

            return true;
        }

        private static int HexDigit(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return -1;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ApplyHsv(value, false);
        }
#endif
    }
}
