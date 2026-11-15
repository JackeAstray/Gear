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
    /// HSV 色轮：角度 = Hue，半径 = Saturation，亮度由独立滑条控制。
    /// 贴图程序化生成，指针取色只改 HSV，不依赖第三方拾色器。
    /// </summary>
    [AddComponentMenu("UI/Gear/Color Wheel")]
    [DisallowMultipleComponent]
    public sealed class GearColorWheel : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        [SerializeField] private RectTransform area;
        [SerializeField] private RectTransform handle;
        [SerializeField] private Slider brightnessSlider;
        [SerializeField] private Graphic preview;
        [SerializeField] private TMP_Text hexLabel;
        [SerializeField, Range(64, 1024)] private int textureSize = 256;
        [SerializeField] private Color value = Color.red;
        [SerializeField] private UnityEvent<Color> onColorChanged = new UnityEvent<Color>();

        private float hue;
        private float saturation = 1f;
        private float brightness = 1f;
        private bool refreshing;
        private Texture2D wheelTexture;
        private Sprite wheelSprite;
        private float textureBrightness = -1f;

        public UnityEvent<Color> OnColorChanged => onColorChanged;
        public event Action<Color> ColorChanged;

        public Color Value => value;
        public float Hue => hue;
        public float Saturation => saturation;
        public float Brightness => brightness;
        public string Hex => GearColorPicker.ColorToHex(value);

        public RectTransform Area { get => area; set { area = value; Refresh(); } }
        public RectTransform Handle { get => handle; set { handle = value; Refresh(); } }
        public Graphic Preview { get => preview; set { preview = value; Refresh(); } }

        public Slider BrightnessSlider
        {
            get => brightnessSlider;
            set
            {
                Unbind();
                brightnessSlider = value;
                Bind();
                Refresh();
            }
        }

        public int TextureSize
        {
            get => textureSize;
            set
            {
                textureSize = Mathf.Clamp(value, 64, 1024);
                textureBrightness = -1f;
                Refresh();
            }
        }

        private void OnEnable()
        {
            Bind();
            ApplyHsv(value, false);
            Refresh();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void OnDestroy()
        {
            ReleaseTexture();
        }

        private void Bind()
        {
            if (brightnessSlider == null) return;
            brightnessSlider.onValueChanged.RemoveListener(OnBrightnessChanged);
            brightnessSlider.onValueChanged.AddListener(OnBrightnessChanged);
        }

        private void Unbind()
        {
            if (brightnessSlider != null) brightnessSlider.onValueChanged.RemoveListener(OnBrightnessChanged);
        }

        public void SetColor(Color color, bool notify = true)
        {
            color.a = Mathf.Clamp01(color.a);
            ApplyHsv(color, notify);
            Refresh();
        }

        public void SetHSV(float hueDegrees, float s, float v, bool notify = true)
        {
            float alpha = value.a;
            float oldBrightness = brightness;
            hue = Mathf.Repeat(hueDegrees, 360f);
            saturation = Mathf.Clamp01(s);
            brightness = Mathf.Clamp01(v);
            value = Color.HSVToRGB(hue / 360f, saturation, brightness);
            value.a = alpha;

            if (!Mathf.Approximately(oldBrightness, brightness)) textureBrightness = -1f;
            Refresh();
            if (notify)
            {
                onColorChanged?.Invoke(value);
                ColorChanged?.Invoke(value);
            }
        }

        public void OnPointerDown(PointerEventData eventData) => ApplyPointer(eventData);
        public void OnDrag(PointerEventData eventData) => ApplyPointer(eventData);

        private void ApplyPointer(PointerEventData eventData)
        {
            RectTransform target = area != null ? area : (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    target, eventData.position, eventData.pressEventCamera, out Vector2 local))
            {
                return;
            }

            Rect rect = target.rect;
            if (rect.width <= 0.0001f || rect.height <= 0.0001f) return;

            float u = local.x / rect.width * 2f;
            float v = local.y / rect.height * 2f;
            float radius = Mathf.Min(1f, Mathf.Sqrt(u * u + v * v));
            float angle = Mathf.Atan2(v, u) * Mathf.Rad2Deg;
            SetHSV(angle, radius, brightness);
        }

        private void OnBrightnessChanged(float sliderValue)
        {
            if (refreshing) return;
            SetHSV(hue, saturation, Mathf.Clamp01(sliderValue));
        }

        private void ApplyHsv(Color color, bool notify)
        {
            Color.RGBToHSV(color, out float h, out float s, out float v);
            hue = h * 360f;
            saturation = s;
            brightness = v;
            value = color;
            textureBrightness = -1f;
            if (notify)
            {
                onColorChanged?.Invoke(value);
                ColorChanged?.Invoke(value);
            }
        }

        private void Refresh()
        {
            if (refreshing) return;
            refreshing = true;
            try
            {
                if (preview != null) preview.color = value;
                UpdateTexture();
                if (handle != null && area != null)
                {
                    Rect rect = area.rect;
                    float angle = hue * Mathf.Deg2Rad;
                    handle.anchoredPosition = new Vector2(
                        Mathf.Cos(angle) * saturation * rect.width * 0.5f,
                        Mathf.Sin(angle) * saturation * rect.height * 0.5f);
                }

                if (brightnessSlider != null) brightnessSlider.SetValueWithoutNotify(brightness);
                if (hexLabel != null) hexLabel.text = Hex;
            }
            finally
            {
                refreshing = false;
            }
        }

        private void UpdateTexture()
        {
            if (area == null) return;
            var image = area.GetComponent<ImageEx>();
            if (image == null) return;
            if (wheelSprite != null && Mathf.Approximately(textureBrightness, brightness))
            {
                image.sprite = wheelSprite;
                image.color = Color.white;
                return;
            }

            ReleaseTexture();
            wheelTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            Color32[] pixels = new Color32[textureSize * textureSize];
            for (int y = 0; y < textureSize; y++)
            {
                float v = (y / (float)(textureSize - 1)) * 2f - 1f;
                for (int x = 0; x < textureSize; x++)
                {
                    float u = (x / (float)(textureSize - 1)) * 2f - 1f;
                    Color color = EvaluateWheelColor(u, v, brightness);
                    pixels[y * textureSize + x] = color;
                }
            }

            wheelTexture.SetPixels32(pixels);
            wheelTexture.Apply(false, false);
            wheelSprite = Sprite.Create(wheelTexture,
                new Rect(0f, 0f, textureSize, textureSize), new Vector2(0.5f, 0.5f));
            wheelSprite.hideFlags = HideFlags.HideAndDontSave;
            textureBrightness = brightness;

            image.sprite = wheelSprite;
            image.color = Color.white;
            var gradient = image.GradientEffect;
            if (gradient.Enabled)
            {
                gradient.Enabled = false;
                image.GradientEffect = gradient;
            }
        }

        /// <summary>归一化坐标 [-1,1]；半径外返回透明。</summary>
        public static Color EvaluateWheelColor(float u, float v, float brightness)
        {
            float radius = Mathf.Sqrt(u * u + v * v);
            if (radius > 1f) return new Color(0f, 0f, 0f, 0f);
            float angle = Mathf.Atan2(v, u) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;
            Color color = Color.HSVToRGB(angle / 360f, Mathf.Clamp01(radius), Mathf.Clamp01(brightness));
            color.a = 1f;
            return color;
        }

        private void ReleaseTexture()
        {
            if (wheelSprite != null)
            {
                if (Application.isPlaying) Destroy(wheelSprite);
                else DestroyImmediate(wheelSprite);
                wheelSprite = null;
            }

            if (wheelTexture != null)
            {
                if (Application.isPlaying) Destroy(wheelTexture);
                else DestroyImmediate(wheelTexture);
                wheelTexture = null;
            }
        }
    }
}
