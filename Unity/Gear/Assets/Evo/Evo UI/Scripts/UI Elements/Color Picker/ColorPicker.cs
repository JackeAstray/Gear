using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace Evo.UI
{
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("Evo/UI/UI Elements/Color Picker"), HelpURL(Constants.HelpUrl + "ui-elements/color-picker")]
    public class ColorPicker : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        // Settings
        public bool resetOnRightClick = true;
        public Color startColor = Color.white;
        [SerializeField] private ColorPickerType pickerType = ColorPickerType.Radial;
        [SerializeField] private TextureSize wheelSize = TextureSize.Size512;

        // References
        [Tooltip("Procedural Shape or Image that displays the generated color wheel.")]
        public Graphic colorWheel;
        public ProceduralShape colorPreview;
        public RectTransform colorSelector;
        [TextInput] public MonoBehaviour hexInput;
        public Slider hueSlider;
        [SerializeField, GradientShape] private MonoBehaviour hueGradient;
        public Slider saturationSlider;
        [SerializeField, GradientShape] private MonoBehaviour saturationGradient;
        public Slider brightnessSlider;
        [SerializeField, GradientShape] private MonoBehaviour brightnessGradient;
        public Slider opacitySlider;
        [SerializeField, GradientShape] private MonoBehaviour opacityGradient;

        // Constants
        static readonly Vector2 HalfVector = Vector2.one * 0.5f;
        const string HexDigits = "0123456789ABCDEF";
        static readonly GradientColorKey[] HueColorKeys =
        {
            new(Color.red, 0f),
            new(new Color(1f, 1f, 0f), 0.167f), // Color.yellow isn't pure yellow
            new(Color.green, 0.333f),
            new(Color.cyan, 0.5f),
            new(Color.blue, 0.667f),
            new(Color.magenta, 0.833f),
            new(Color.red, 1f)
        };
        static readonly GradientAlphaKey[] OpaqueAlphaKeys = { new(1f, 0f), new(1f, 1f) };

        // Events
        public UnityEvent<Color> onColorChanged = new();

        // Enums
        public enum ColorPickerType { None, Radial, Square }
        public enum TextureSize { Size64 = 64, Size128 = 128, Size256 = 256, Size512 = 512, Size1024 = 1024 }

        // Helpers
        Color currentColor = Color.white;
        float currentHue = 0;
        float currentSaturation = 1;
        float currentBrightness = 1;
        float currentAlpha = 1;
        bool isDraggingWheel = false;
        int activePointerId;

        // Cache
        RectTransform wheelRect;
        Camera eventCamera;
        Texture2D wheelTexture;
        Sprite wheelSprite;
        Sprite originalWheelSprite;
        Graphic generatedWheelGraphic;
        ColorPickerType generatedPickerType;
        TextureSize generatedWheelSize;
        Vector3 lastGradientHSV = new(-1f, -1f, -1f);
        Color lastGradientColor = Color.clear;
        TextInputListener hexInputListener;
        readonly char[] hexBuffer = new char[9];
        readonly GradientColorKey[] twoColorKeys = new GradientColorKey[2];
        readonly GradientAlphaKey[] twoAlphaKeys = new GradientAlphaKey[2];

        void Start()
        {
            if (!Application.IsPlaying(gameObject))
                return;

            GenerateColorWheel();
            SetupSliders();
            SetupHexInput();
            SetColor(startColor, updateHSV: true, notify: false);
            onColorChanged?.Invoke(currentColor);
        }

        void OnEnable()
        {
#if UNITY_EDITOR
            if (!Application.IsPlaying(gameObject))
                OnValidate();
#endif
        }

        void OnDisable()
        {
            isDraggingWheel = false;
            eventCamera = null;

#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= DelayedUpdate;

            if (!Application.IsPlaying(gameObject))
                ReleaseColorWheel();
#endif
        }

        void OnDestroy()
        {
            if (hueSlider != null)
                hueSlider.onValueChanged.RemoveListener(OnHueChanged);
            if (saturationSlider != null)
                saturationSlider.onValueChanged.RemoveListener(OnSaturationChanged);
            if (brightnessSlider != null)
                brightnessSlider.onValueChanged.RemoveListener(OnBrightnessChanged);
            if (opacitySlider != null)
                opacitySlider.onValueChanged.RemoveListener(OnOpacityChanged);
           
            hexInputListener?.Unbind();
            ReleaseColorWheel();
        }

        void OnRectTransformDimensionsChange() => UpdateSelectorPositionFromHSV();

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!isActiveAndEnabled || isDraggingWheel)
                return;

            if (resetOnRightClick && eventData.button == PointerEventData.InputButton.Right)
            {
                SetColor(startColor);
                return;
            }

            if (eventData.button != PointerEventData.InputButton.Left || colorWheel == null || pickerType == ColorPickerType.None)
                return;

            GameObject hitObject = eventData.pointerPressRaycast.gameObject;
            if (hitObject == null)
                hitObject = eventData.pointerEnter;
            if (hitObject == null || (!hitObject.transform.IsChildOf(colorWheel.transform)
                && (colorSelector == null || !hitObject.transform.IsChildOf(colorSelector))))
                return;

            wheelRect = colorWheel.rectTransform;
            eventCamera = eventData.pressEventCamera;
            activePointerId = eventData.pointerId;
            isDraggingWheel = true;
            HandleColorWheelInput(eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (isActiveAndEnabled && isDraggingWheel && eventData.pointerId == activePointerId
                && eventData.button == PointerEventData.InputButton.Left && colorWheel != null && pickerType != ColorPickerType.None)
                HandleColorWheelInput(eventData.position);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != activePointerId || eventData.button != PointerEventData.InputButton.Left)
                return;

            isDraggingWheel = false;
            eventCamera = null;
        }

        void GenerateColorWheel()
        {
            if (colorWheel == null || pickerType == ColorPickerType.None)
            {
                ReleaseColorWheel();
                wheelRect = null;
                return;
            }

            wheelRect = colorWheel.rectTransform;

            if (wheelSprite != null && generatedWheelGraphic == colorWheel && GetWheelSprite(colorWheel) == wheelSprite
                && generatedPickerType == pickerType && generatedWheelSize == wheelSize)
                return;

            ReleaseColorWheel();

            // Only Procedural Shapes and Images can display the sprite
            if (colorWheel is not (ProceduralShape or Image))
                return;

            int size = (int)wheelSize;
            Color32[] pixels = new Color32[size * size];

            if (pickerType == ColorPickerType.Radial) { CreateRadialColorWheel(pixels, size, colorWheel is ProceduralShape); }
            else { CreateSquareColorPicker(pixels, size); }

            wheelTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "ColorPickerTexture",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            wheelTexture.SetPixels32(pixels);
            wheelTexture.Apply(false, true);

            wheelSprite = Sprite.Create(wheelTexture, new Rect(0, 0, size, size), HalfVector, 100f, 0, SpriteMeshType.FullRect);
            wheelSprite.name = "ColorPickerSprite";
            wheelSprite.hideFlags = HideFlags.HideAndDontSave;

            generatedWheelGraphic = colorWheel;
            originalWheelSprite = GetWheelSprite(colorWheel);
            generatedPickerType = pickerType;
            generatedWheelSize = wheelSize;
            SetWheelSprite(colorWheel, wheelSprite);
        }

        void ReleaseColorWheel()
        {
            if (generatedWheelGraphic != null && GetWheelSprite(generatedWheelGraphic) == wheelSprite)
                SetWheelSprite(generatedWheelGraphic, originalWheelSprite);

            DestroyWheelObject(wheelSprite);
            DestroyWheelObject(wheelTexture);
            wheelSprite = null;
            wheelTexture = null;
            originalWheelSprite = null;
            generatedWheelGraphic = null;
        }

        static Sprite GetWheelSprite(Graphic graphic)
        {
            if (graphic is ProceduralShape shape) { return shape.Sprite; }
            return graphic is Image image ? image.sprite : null;
        }

        static void SetWheelSprite(Graphic graphic, Sprite sprite)
        {
            if (graphic is ProceduralShape shape) { shape.Sprite = sprite; }
            else if (graphic is Image image) { image.sprite = sprite; }
        }

        void DestroyWheelObject(Object wheelObject)
        {
            if (wheelObject == null)
                return;

            if (Application.isPlaying) { Destroy(wheelObject); }
            else { DestroyImmediate(wheelObject); }
        }

        // A Procedural Shape draws its own edge, so its texture keeps full saturation out to the corners
        void CreateRadialColorWheel(Color32[] pixels, int size, bool fillCorners)
        {
            Vector2 center = new(size * 0.5f, size * 0.5f);
            float radius = size * 0.5f;
            float radiusSquared = radius * radius;
            float invRadius = 1f / radius;
            float radiusMinusOne = radius - 1f;

            for (int y = 0; y < size; y++)
            {
                int yOffset = y * size;
                float dy = y + 0.5f - center.y;
                float dySquared = dy * dy;

                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - center.x;
                    float distanceSquared = dx * dx + dySquared;
                    if (distanceSquared > radiusSquared && !fillCorners)
                    {
                        pixels[yOffset + x] = Color.clear;
                        continue;
                    }

                    float distance = Mathf.Sqrt(distanceSquared);

                    // Anti-aliasing
                    float alpha = 1f;
                    if (distance > radiusMinusOne && !fillCorners)
                        alpha = radius - distance;

                    float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    if (angle < 0)
                        angle += 360;

                    float hue = angle / 360f;
                    float saturation = Mathf.Min(distance * invRadius, 1f);

                    Color32 color = Color.HSVToRGB(hue, saturation, 1f);
                    color.a = (byte)(alpha * 255);
                    pixels[yOffset + x] = color;
                }
            }
        }

        void CreateSquareColorPicker(Color32[] pixels, int size)
        {
            float invSize = 1f / (size - 1);
            for (int y = 0; y < size; y++)
            {
                float saturation = y * invSize;
                int yOffset = y * size;
                for (int x = 0; x < size; x++)
                {
                    float hue = x * invSize;
                    pixels[yOffset + x] = Color.HSVToRGB(hue, saturation, 1f);
                }
            }
        }

        void SetupSliders()
        {
            SetupSlider(hueSlider, OnHueChanged);
            SetupSlider(saturationSlider, OnSaturationChanged);
            SetupSlider(brightnessSlider, OnBrightnessChanged);
            SetupSlider(opacitySlider, OnOpacityChanged);

            // Setup static hue gradient once
            if (hueGradient is ImageGradient legacyHueGradient)
            {
                var gradient = new Gradient();
                gradient.SetKeys(HueColorKeys, OpaqueAlphaKeys);
                legacyHueGradient.SetGradient(gradient);
            }
            else
            {
                SetShapeGradient(hueGradient as ProceduralShape, HueColorKeys, OpaqueAlphaKeys);
            }
        }

        void SetupSlider(Slider slider, UnityAction<float> callback)
        {
            if (slider == null)
                return;

            slider.wholeNumbers = false;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.onValueChanged.AddListener(callback);
        }

        void SetupHexInput()
        {
            if (!TextProvider.IsInput(hexInput))
                return;

            TextProvider.SetCharacterLimit(hexInput, 9); // # + 8 hex chars (RRGGBBAA)
            hexInputListener = new TextInputListener { onEndEdit = OnHexInputChanged };
            hexInputListener.Bind(hexInput);
        }

        // Local rect covered by the whole wheel texture
        Rect GetWheelRect()
        {
            if (colorWheel is ProceduralShape shape)
            {
                Rect drawingRect = shape.GetDrawingRect();
                if (shape.ImageScaleMode != ProceduralShape.ScaleMode.Cover)
                    return drawingRect;

                // Cover crops the square texture to the shape
                float coverSize = Mathf.Max(drawingRect.width, drawingRect.height);
                return new Rect(drawingRect.center - 0.5f * coverSize * Vector2.one, coverSize * Vector2.one);
            }

            Rect rect = colorWheel.GetPixelAdjustedRect();

            if (colorWheel is Image image && image.preserveAspect)
            {
                float size = Mathf.Min(rect.width, rect.height);
                Vector2 squareSize = new(size, size);
                rect.position += Vector2.Scale(rect.size - squareSize, wheelRect.pivot);
                rect.size = squareSize;
            }

            return rect;
        }

        // Rotated or skewed shapes move the texture with them
        Vector2 TransformWheelPoint(Vector2 point, bool inverse)
        {
            if (colorWheel is ProceduralShape shape && shape.HasShapeTransform)
                return shape.GetShapeTransform(shape.GetDrawingRect().center, inverse).MultiplyPoint3x4(point);

            return point;
        }

        void HandleColorWheelInput(Vector2 screenPos)
        {
            if (wheelRect == null)
                return;

            Rect rect = GetWheelRect();
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(wheelRect, screenPos, eventCamera, out Vector2 localPoint))
            {
                Vector2 normalizedPoint = (TransformWheelPoint(localPoint, inverse: true) - rect.min) / rect.size;

                if (pickerType == ColorPickerType.Radial)
                {
                    Vector2 dir = normalizedPoint - HalfVector;
                    float distance = dir.magnitude;

                    if (distance > 0f)
                        currentHue = Mathf.Repeat(Mathf.Atan2(dir.y, dir.x) / (2f * Mathf.PI), 1f);

                    currentSaturation = Mathf.Clamp01(distance * 2f);
                }
                else
                {
                    currentHue = Mathf.Clamp01(normalizedPoint.x);
                    currentSaturation = Mathf.Clamp01(normalizedPoint.y);
                }

                UpdateColorFromHSV();
            }
        }

        void UpdateColorFromHSV()
        {
            Color newColor = Color.HSVToRGB(currentHue, currentSaturation, currentBrightness);
            newColor.a = currentAlpha;
            SetColor(newColor, updateHSV: false);
        }

        void OnHueChanged(float value)
        {
            if (!isActiveAndEnabled)
                return;

            currentHue = Mathf.Clamp01(value);
            UpdateColorFromHSV();
        }

        void OnSaturationChanged(float value)
        {
            if (!isActiveAndEnabled)
                return;

            currentSaturation = Mathf.Clamp01(value);
            UpdateColorFromHSV();
        }

        void OnBrightnessChanged(float value)
        {
            if (!isActiveAndEnabled)
                return;

            currentBrightness = Mathf.Clamp01(value);
            UpdateColorFromHSV();
        }

        void OnOpacityChanged(float value)
        {
            if (isActiveAndEnabled)
                SetAlpha(value);
        }

        void OnHexInputChanged(string hexValue)
        {
            if (!isActiveAndEnabled)
                return;

            hexValue = hexValue?.Trim();
            if (!string.IsNullOrEmpty(hexValue) && hexValue[0] != '#')
                hexValue = "#" + hexValue;

            if (ColorUtility.TryParseHtmlString(hexValue, out Color newColor))
                SetColor(newColor);
            else
                UpdateHexInput();
        }

        void UpdateUI()
        {
            if (colorPreview != null && colorPreview.color != currentColor)
                colorPreview.color = currentColor;

            UpdateSlider(hueSlider, currentHue);
            UpdateSlider(saturationSlider, currentSaturation);
            UpdateSlider(brightnessSlider, currentBrightness);
            UpdateSlider(opacitySlider, currentAlpha);
            UpdateHexInput();
            UpdateAllGradients();
            UpdateSelectorPositionFromHSV();
        }

        void UpdateSlider(Slider slider, float value)
        {
            if (slider == null)
                return;

            slider.SetValueWithoutNotify(value);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (slider.valueText != null || slider.valueInput != null)
                {
                    string text = slider.GetFormattedText(slider.value);
                    TextProvider.SetText(slider.valueText, text);
                    UpdateInputText(slider.valueInput, text);
                }
                return;
            }
#endif

            slider.SetText(slider.value);
        }

        void UpdateHexInput()
        {
            // Same digits as ColorUtility.ToHtmlStringRGBA, written without allocating
            Color32 color = new(ToByte(currentColor.r), ToByte(currentColor.g), ToByte(currentColor.b), ToByte(currentColor.a));
            hexBuffer[0] = '#';
            WriteHex(color.r, 1);
            WriteHex(color.g, 3);
            WriteHex(color.b, 5);
            WriteHex(color.a, 7);
            UpdateInputText(hexInput, hexBuffer);
        }

        void WriteHex(byte value, int index)
        {
            hexBuffer[index] = HexDigits[value >> 4];
            hexBuffer[index + 1] = HexDigits[value & 0xF];
        }

        static byte ToByte(float channel) => (byte)Mathf.Clamp(Mathf.RoundToInt(channel * byte.MaxValue), 0, byte.MaxValue);

        // Inputs require a string; allocate only when the displayed value changes
        static void UpdateInputText(MonoBehaviour input, System.ReadOnlySpan<char> text)
        {
            TextBackend backend = TextProvider.FindInput(input);
            if (backend != null && !backend.ValueEquals(input, text))
                backend.SetValue(input, text.ToString(), false);
        }

        void UpdateAllGradients()
        {
            bool hueChanged = lastGradientHSV.x != currentHue;
            bool saturationChanged = lastGradientHSV.y != currentSaturation;
            bool brightnessChanged = lastGradientHSV.z != currentBrightness;

            if (saturationGradient != null && (hueChanged || brightnessChanged))
            {
                Color grayscale = Color.HSVToRGB(0f, 0f, currentBrightness);
                Color fullySaturated = Color.HSVToRGB(currentHue, 1f, currentBrightness);
                SetGradientColors(saturationGradient, grayscale, fullySaturated);
            }

            if (brightnessGradient != null && (hueChanged || saturationChanged))
            {
                Color colorAtFullBrightness = Color.HSVToRGB(currentHue, currentSaturation, 1f);
                SetGradientColors(brightnessGradient, Color.black, colorAtFullBrightness);
            }

            Color opaqueColor = currentColor;
            opaqueColor.a = 1f;

            if (opacityGradient != null && !lastGradientColor.Equals(opaqueColor))
            {
                Color transparentColor = opaqueColor;
                transparentColor.a = 0f;
                SetGradientColors(opacityGradient, transparentColor, opaqueColor);
            }

            lastGradientHSV = new Vector3(currentHue, currentSaturation, currentBrightness);
            lastGradientColor = opaqueColor;
        }

        // Image Gradient targets come from objects made before Procedural Shape support
        void SetGradientColors(MonoBehaviour target, Color startColor, Color endColor)
        {
            if (target is ImageGradient legacyGradient)
            {
                legacyGradient.SetGradientColors(startColor, endColor);
                return;
            }

            twoColorKeys[0] = new GradientColorKey(startColor, 0f);
            twoColorKeys[1] = new GradientColorKey(endColor, 1f);
            twoAlphaKeys[0] = new GradientAlphaKey(startColor.a, 0f);
            twoAlphaKeys[1] = new GradientAlphaKey(endColor.a, 1f);
            SetShapeGradient(target as ProceduralShape, twoColorKeys, twoAlphaKeys);
        }

        // Edits the fill gradient in place. Only the shape's gradient ramp is rewritten, without a mesh rebuild.
        static void SetShapeGradient(ProceduralShape shape, GradientColorKey[] colorKeys, GradientAlphaKey[] alphaKeys)
        {
            if (shape == null)
                return;

            Gradient gradient = shape.FillGradient;
            if (gradient == null)
                shape.FillGradient = gradient = new Gradient();

            // Blend keeps the HSV ramps exact
            gradient.mode = GradientMode.Blend;
            gradient.SetKeys(colorKeys, alphaKeys);
            shape.SetMaterialDirty();
        }

        void UpdateSelectorPositionFromHSV()
        {
            if (colorSelector == null || colorWheel == null || pickerType == ColorPickerType.None)
                return;

            wheelRect = colorWheel.rectTransform;
            Vector2 normalizedPos;

            if (pickerType == ColorPickerType.Radial)
            {
                float angle = currentHue * 2f * Mathf.PI;
                float distance = currentSaturation * 0.5f;
                normalizedPos = HalfVector + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
            }
            else
            {
                normalizedPos = new Vector2(currentHue, currentSaturation);
            }

            Rect rect = GetWheelRect();
            Vector3 position = wheelRect.TransformPoint(TransformWheelPoint(rect.min + normalizedPos * rect.size, inverse: false));

            if (colorSelector.position != position)
                colorSelector.position = position;
        }

        void SetColor(Color color, bool updateHSV, bool notify = true)
        {
            color = new Color(Mathf.Clamp01(color.r), Mathf.Clamp01(color.g), Mathf.Clamp01(color.b), Mathf.Clamp01(color.a));
            bool colorChanged = !currentColor.Equals(color);
            currentColor = color;
            currentAlpha = color.a;

            if (updateHSV && (colorChanged || !notify))
            {
                Color.RGBToHSV(color, out float h, out float s, out float v);

                // Preserve the selected hue for grayscale and saturation for black
                if (s > 0f)
                    currentHue = h;
                if (v > 0f)
                    currentSaturation = s;

                currentBrightness = v;
            }

            UpdateUI();

            if (notify && colorChanged)
                onColorChanged?.Invoke(currentColor);
        }

        #region Public API
        public void SetColor(Color color) => SetColor(color, updateHSV: true);

        public Color GetCurrentColor(bool withoutBrightness = false)
        {
            if (!withoutBrightness) { return currentColor; }
            else
            {
                // Return color at full brightness
                Color colorWithoutBrightness = Color.HSVToRGB(currentHue, currentSaturation, 1f);
                colorWithoutBrightness.a = currentAlpha;
                return colorWithoutBrightness;
            }
        }

        public void SetRGB(float r, float g, float b)
        {
            Color newColor = new(r, g, b, currentAlpha);
            SetColor(newColor);
        }

        public void SetRed(float value)
        {
            Color newColor = currentColor;
            newColor.r = value;
            SetColor(newColor);
        }

        public void SetGreen(float value)
        {
            Color newColor = currentColor;
            newColor.g = value;
            SetColor(newColor);
        }

        public void SetBlue(float value)
        {
            Color newColor = currentColor;
            newColor.b = value;
            SetColor(newColor);
        }

        public void SetAlpha(float value)
        {
            Color newColor = currentColor;
            newColor.a = value;
            SetColor(newColor, updateHSV: false);
        }
        #endregion

#if UNITY_EDITOR
        void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall -= DelayedUpdate;
            UnityEditor.EditorApplication.delayCall += DelayedUpdate;
        }

        void DelayedUpdate()
        {
            if (this == null || !isActiveAndEnabled || Application.isPlaying || !gameObject.scene.IsValid())
                return;

            GenerateColorWheel();
            lastGradientHSV = new Vector3(-1f, -1f, -1f);
            lastGradientColor = Color.clear;
            SetColor(startColor, updateHSV: true, notify: false);
        }
#endif
    }

    /// <summary>
    /// Draws a MonoBehaviour field as a Procedural Shape whose fill gradient the Color Picker sets.
    /// Image Gradients assigned on older objects are still shown and used.
    /// </summary>
    public sealed class GradientShapeAttribute : PropertyAttribute { }
}