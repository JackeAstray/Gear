using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Gear.Utilities.UI.ImageExtensions;
using Gear.Utilities.UI.Widgets;

namespace Gear.UI.Showcase
{
    /// <summary>
    /// 展厅窗口的运行时 UI 构建工具。
    /// <para>
    /// 所有布局都在代码里生成，窗口预制体只保留 Canvas + UIWindowAsset + Controller 根，
    /// 这样展厅可以随窗口代码一起迭代，不需要每次改预制体。
    /// </para>
    /// </summary>
    internal static class ShowcaseUIFactory
    {
        public const int UILayer = 5;

        private static readonly Color PanelColor = new Color(0.075f, 0.086f, 0.11f, 0.98f);
        private static readonly Color PanelSoftColor = new Color(0.11f, 0.125f, 0.16f, 0.96f);
        private static readonly Color TextPrimary = new Color(0.94f, 0.95f, 0.98f, 1f);
        private static readonly Color TextMuted = new Color(0.62f, 0.66f, 0.75f, 1f);
        private static readonly Color Accent = new Color(0.35f, 0.62f, 1f, 1f);

        public static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = UILayer;
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.anchoredPosition3D = Vector3.zero;
            return rect;
        }

        public static void Stretch(RectTransform rect, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        public static void TopLeft(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        public static void TopRight(RectTransform rect, float right, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-right, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        public static ImageEx CreatePanel(string name, Transform parent, Color color, float radius = 0f, bool raycast = false)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx));
            go.layer = UILayer;
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;

            ImageEx image = go.GetComponent<ImageEx>();
            image.raycastTarget = raycast;
            image.DrawShape = DrawShape.Rectangle;
            image.Rectangle = new RectangleImg
            {
                CornerRadius = new Vector4(radius, radius, radius, radius),
            };
            image.color = color;
            return image;
        }

        public static TextMeshProUGUI CreateText(
            string name,
            Transform parent,
            string text,
            float fontSize,
            Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft,
            FontStyles fontStyle = FontStyles.Normal)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.layer = UILayer;
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;

            TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.fontStyle = fontStyle;
            label.raycastTarget = false;
            label.margin = Vector4.zero;
            return label;
        }

        public static Button CreateButton(
            string name,
            Transform parent,
            string labelText,
            Color background,
            float radius,
            UnityAction onClick,
            float fontSize = 22f)
        {
            ImageEx image = CreatePanel(name, parent, background, radius, true);
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.selectedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            TextMeshProUGUI label = CreateText("Label", image.transform, labelText, fontSize, PrimaryText,
                TextAlignmentOptions.Center);
            Stretch(label.rectTransform, 8f, 8f, 4f, 4f);

            if (onClick != null) button.onClick.AddListener(onClick);
            return button;
        }

        public static TMP_InputField CreateInputField(
            string name,
            Transform parent,
            string placeholderText,
            UnityAction<string> onChanged)
        {
            ImageEx background = CreatePanel(name, parent, PanelSoftColor, 10f, true);
            background.gameObject.SetActive(false);
            TMP_InputField input = background.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = background;
            input.transition = Selectable.Transition.ColorTint;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = 64;

            RectTransform viewport = CreateRect("Text Area", background.transform);
            Stretch(viewport, 14f, 14f, 8f, 8f);
            viewport.gameObject.AddComponent<RectMask2D>();

            TextMeshProUGUI text = CreateText("Text", viewport, "", 20f, PrimaryText);
            Stretch(text.rectTransform);
            text.richText = false;

            TextMeshProUGUI placeholder = CreateText("Placeholder", viewport, placeholderText, 20f, MutedText);
            Stretch(placeholder.rectTransform);
            placeholder.richText = false;

            input.textViewport = viewport;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.pointSize = 20f;
            input.caretColor = TextPrimary;
            input.selectionColor = new Color(Accent.r, Accent.g, Accent.b, 0.35f);
            if (onChanged != null) input.onValueChanged.AddListener(onChanged);
            background.gameObject.SetActive(true);
            return input;
        }

        public static void ApplyCardLayer(GameObject instance)
        {
            if (instance == null) return;
            Transform[] transforms = instance.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                transforms[i].gameObject.layer = UILayer;
            }
        }

        public static Slider CreateSlider(
            string name,
            Transform parent,
            float min,
            float max,
            float value,
            Color fillColor,
            Color trackColor)
        {
            ImageEx track = CreatePanel(name, parent, trackColor, 8f, true);
            Slider slider = track.gameObject.AddComponent<Slider>();
            slider.targetGraphic = track;
            slider.transition = Selectable.Transition.None;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;

            RectTransform fillArea = CreateRect("Fill Area", track.transform);
            Stretch(fillArea, 6f, 6f, 6f, 6f);
            ImageEx fill = CreatePanel("Fill", fillArea, fillColor, 6f);
            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            slider.fillRect = fillRect;

            RectTransform handleArea = CreateRect("Handle Slide Area", track.transform);
            Stretch(handleArea, 6f, 6f, 0f, 0f);
            ImageEx handle = CreatePanel("Handle", handleArea, Color.white, 10f);
            RectTransform handleRect = handle.rectTransform;
            handleRect.sizeDelta = new Vector2(20f, 20f);
            slider.handleRect = handleRect;

            slider.value = Mathf.Clamp(value, min, max);
            return slider;
        }

        public static GearSwitch CreateSwitch(string name, Transform parent, bool isOn)
        {
            ImageEx background = CreatePanel(name, parent, new Color(0.2f, 0.22f, 0.28f, 1f), 15f, true);
            background.rectTransform.sizeDelta = new Vector2(56f, 30f);

            ImageEx handle = CreatePanel("Handle", background.transform, Color.white, 12f);
            handle.rectTransform.sizeDelta = new Vector2(24f, 24f);

            GearSwitch toggle = background.gameObject.AddComponent<GearSwitch>();
            toggle.Background = background;
            toggle.Handle = handle.rectTransform;
            toggle.OffPosition = new Vector2(-14f, 0f);
            toggle.OnPosition = new Vector2(14f, 0f);
            toggle.OffColor = new Color(0.2f, 0.22f, 0.28f, 1f);
            toggle.OnColor = Accent;
            toggle.SetIsOn(isOn, false);
            return toggle;
        }

        public static GearProgressBar CreateMeter(string name, Transform parent, Color fillColor, float value)
        {
            ImageEx background = CreatePanel(name, parent, new Color(0.12f, 0.14f, 0.18f, 1f), 5f);
            ImageEx fill = CreatePanel("Fill", background.transform, fillColor, 5f);
            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            GearProgressBar bar = background.gameObject.AddComponent<GearProgressBar>();
            bar.Bind(fillRect, null);
            bar.SetValueImmediate(Mathf.Clamp01(value));
            return bar;
        }

        public static GearRadioGroup CreateRadioGroup(
            string name,
            Transform parent,
            string[] labels,
            int selected,
            float width,
            float height,
            float gap,
            bool vertical = false)
        {
            RectTransform root = CreateRect(name, parent);
            List<GearRadioGroup.Option> options = new List<GearRadioGroup.Option>(labels != null ? labels.Length : 0);
            if (labels != null)
            {
                for (int i = 0; i < labels.Length; i++)
                {
                    Button button = CreateButton("Option " + i, root, labels[i], Color.white, 8f, null, 16f);
                    button.transition = Selectable.Transition.None;
                    RectTransform rect = button.GetComponent<RectTransform>();
                    rect.anchorMin = new Vector2(0f, 1f);
                    rect.anchorMax = new Vector2(0f, 1f);
                    rect.pivot = new Vector2(0f, 1f);
                    rect.anchoredPosition = vertical
                        ? new Vector2(0f, -i * (height + gap))
                        : new Vector2(i * (width + gap), 0f);
                    rect.sizeDelta = new Vector2(width, height);
                    options.Add(new GearRadioGroup.Option
                    {
                        button = button,
                        visual = button.targetGraphic as ImageEx,
                    });
                }
            }

            GearRadioGroup group = root.gameObject.AddComponent<GearRadioGroup>();
            group.Configure(options, selected, false);
            return group;
        }

        public static GearQuantitySelector CreateQuantity(
            string name,
            Transform parent,
            int min,
            int max,
            int value)
        {
            ImageEx background = CreatePanel(name, parent, SoftPanelColor, 10f, true);
            background.rectTransform.sizeDelta = new Vector2(180f, 48f);

            Button minus = CreateButton("Minus", background.transform, "−", new Color(0.16f, 0.18f, 0.23f, 1f),
                8f, null, 22f);
            RectTransform minusRect = minus.GetComponent<RectTransform>();
            minusRect.anchorMin = new Vector2(0f, 0.5f);
            minusRect.anchorMax = new Vector2(0f, 0.5f);
            minusRect.pivot = new Vector2(0f, 0.5f);
            minusRect.anchoredPosition = new Vector2(8f, 0f);
            minusRect.sizeDelta = new Vector2(40f, 36f);

            Button plus = CreateButton("Plus", background.transform, "+", new Color(0.16f, 0.18f, 0.23f, 1f),
                8f, null, 22f);
            RectTransform plusRect = plus.GetComponent<RectTransform>();
            plusRect.anchorMin = new Vector2(1f, 0.5f);
            plusRect.anchorMax = new Vector2(1f, 0.5f);
            plusRect.pivot = new Vector2(1f, 0.5f);
            plusRect.anchoredPosition = new Vector2(-8f, 0f);
            plusRect.sizeDelta = new Vector2(40f, 36f);

            TextMeshProUGUI label = CreateText("Value", background.transform, "0", 20f, TextPrimary,
                TextAlignmentOptions.Center);
            Stretch(label.rectTransform, 56f, 56f, 4f, 4f);

            GearQuantitySelector quantity = background.gameObject.AddComponent<GearQuantitySelector>();
            quantity.Label = label;
            quantity.MinusButton = minus;
            quantity.PlusButton = plus;
            quantity.Configure(min, max, value);
            return quantity;
        }

        public static ImageEx CreateCircle(string name, Transform parent, Color color, float size)
        {
            ImageEx circle = CreatePanel(name, parent, color, 0f);
            circle.DrawShape = DrawShape.Circle;
            circle.rectTransform.sizeDelta = new Vector2(size, size);
            return circle;
        }

        public static ImageEx CreateDivider(string name, Transform parent, Color color, float height = 1f)
        {
            ImageEx divider = CreatePanel(name, parent, color, 0f);
            divider.rectTransform.sizeDelta = new Vector2(0f, height);
            return divider;
        }

        public static Color SoftPanelColor => Resolve("Surface", PanelSoftColor);

        public static Color PageBackground => Resolve("Background", PanelColor);

        public static Color SoftPanel => Resolve("Surface", PanelSoftColor);

        public static Color PrimaryText => Resolve("Text", TextPrimary);

        public static Color MutedText => Resolve("TextSecondary", TextMuted);

        public static Color AccentColor => Resolve("Accent", Accent);

        private static Color Resolve(string tokenId, Color fallback)
        {
            ImageExThemeAsset theme = GearThemeRegistry.Current;
            return theme != null && theme.TryGetColor(tokenId, out Color color) ? color : fallback;
        }
    }
}
