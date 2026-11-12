using UnityEngine;
using ShapeType = Evo.UI.ProceduralShape.ShapeType;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Shapes tab: copies of one shape nested from the outer layer down to the core, each turned a few degrees further
    /// than the one around it and stepping in color from Background to Accent. Inner layers trail the outer ones when
    /// an option changes, and the twist breathes slowly. The options start from the layers' own values.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoShapeGeometry : MonoBehaviour, IStylerHandler
    {
        [Tooltip("Outer layer first. Inactive layers are the ones above the starting layer count.")]
        [SerializeField] private ProceduralShape[] layers;
        [SerializeField] private ShapeType[] choices;

        [Header("Options")]
        [SerializeField] private RadioButtonGroup type;
        [SerializeField] private Slider sides;
        [SerializeField] private Slider radius;
        [SerializeField] private Slider angle;
        [SerializeField] private Slider skewX;
        [SerializeField] private Slider skewY;
        [SerializeField] private Slider twist;
        [SerializeField] private Slider layerCount;
        [Tooltip("Locked unless the shape is a polygon.")]
        [SerializeField] private CanvasGroup sidesOption;
        [Tooltip("Locked for an ellipse, which has no corners.")]
        [SerializeField] private CanvasGroup radiusOption;

        // Gap between a layer and the one around it, in px
        const float Padding = 10f;
        // 0 moves the stack as one piece, 1 makes every layer chase the one around it
        const float Trail = 0.5f;
        const float Stiffness = 300f;
        const float Damping = 0.75f;
        // Per layer, when the shape type or sides change
        const float Stagger = 0.045f;
        const float Pop = 3.2f;
        const float BreathAngle = 3.5f;
        const float BreathPeriod = 7f;
        // Above 1 keeps the outer layers near Background for longer
        const float ColorCurve = 1.5f;
        // Inner layers stop shrinking before the core gets smaller than this part of the outer layer
        const float MinCore = 0.08f;
        const float MaxStep = 1f / 240f;

        struct Layer
        {
            public DemoSpring angle, skewX, skewY, radius, size, visibility, pop;
            public float tone;
            public ShapeType shape;
            public int sides;
            public bool shown;
        }

        Layer[] stack;
        float outerSize;
        float shapeChangedAt;

        // Option values
        ShapeType shapeType;
        int polygonSides;
        float cornerRadius;
        float shapeAngle;
        Vector2 shapeSkew;
        float twistAngle;
        int count;

        // Styler colors, faded with the rest of the page when the preset changes
        StylerPreset preset;
        Color background, accent, fromBackground, fromAccent, toBackground, toAccent;
        float fadeStart, fadeDuration;
        bool hasColors;

        public StylerPreset Preset
        {
            get => preset;
            set
            {
                if (preset == value)
                    return;

                preset = value;
                UpdateStyler();
            }
        }

        void Awake()
        {
            ProceduralShape outer = layers[0];
            outerSize = outer.rectTransform.rect.width;
            shapeType = outer.Shape;
            polygonSides = outer.PolygonSides;
            cornerRadius = outer.UniformCornerRadius;
            shapeAngle = Mathf.DeltaAngle(0f, outer.ShapeAngle);
            shapeSkew = outer.ShapeSkew;
            twistAngle = Mathf.DeltaAngle(outer.ShapeAngle, layers[1].ShapeAngle);

            stack = new Layer[layers.Length];
            for (int i = 0; i < layers.Length; i++)
            {
                ProceduralShape layer = layers[i];
                bool active = layer.gameObject.activeSelf;
                if (active)
                    count = i + 1;

                stack[i] = new Layer
                {
                    angle = new DemoSpring { value = shapeAngle + i * twistAngle },
                    skewX = new DemoSpring { value = shapeSkew.x },
                    skewY = new DemoSpring { value = shapeSkew.y },
                    radius = new DemoSpring { value = cornerRadius / outerSize },
                    size = new DemoSpring { value = layer.rectTransform.rect.width },
                    visibility = new DemoSpring { value = active ? 1f : 0f },
                    pop = new DemoSpring { value = 1f },
                    shape = shapeType,
                    sides = polygonSides,
                    shown = active
                };
            }

            for (int i = 0; i < stack.Length; i++)
                stack[i].tone = Tone(i);

            DemoUtils.Show(type, System.Array.IndexOf(choices, shapeType));
            DemoUtils.Show(sides, polygonSides);
            DemoUtils.Show(radius, cornerRadius);
            DemoUtils.Show(angle, shapeAngle);
            DemoUtils.Show(skewX, shapeSkew.x);
            DemoUtils.Show(skewY, shapeSkew.y);
            DemoUtils.Show(twist, twistAngle);
            DemoUtils.Show(layerCount, count);

            Lock(false);
        }

        void OnEnable()
        {
            if (preset == null)
                preset = Styler.GetDefaultPreset(false);

            // Also brings in a preset picked while the tab was closed
            Styler.Register(this);

            if (!hasColors)
                UpdateStyler();
        }

        void OnDisable() => Styler.Unregister(this);

        void Update()
        {
            float deltaTime = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float now = Time.unscaledTime;

            float fade = fadeDuration > 0f ? Mathf.Clamp01((now - fadeStart) / fadeDuration) : 1f;
            background = Color.LerpUnclamped(fromBackground, toBackground, fade);
            accent = Color.LerpUnclamped(fromAccent, toAccent, fade);

            float liveTwist = twistAngle + BreathAngle * Mathf.Sin(now * 2f * Mathf.PI / BreathPeriod);
            float radiusRatio = cornerRadius / outerSize;
            float toneBlend = 1f - Mathf.Exp(-deltaTime * 8f);

            // Square corners of each layer touch the one around it at the set twist, less the padding on both sides
            float turn = Mathf.Min(Mathf.Abs(twistAngle), 45f) * Mathf.Deg2Rad;
            float fit = Mathf.Cos(turn) + Mathf.Sin(turn);
            float minRatio = Mathf.Pow(MinCore, 1f / Mathf.Max(1, count - 1));
            float targetSize = outerSize;

            for (int i = 0; i < stack.Length; i++)
            {
                ref Layer layer = ref stack[i];

                // Each layer blends its own target with chasing the layer around it
                if (i == 0)
                {
                    layer.angle.Step(shapeAngle, deltaTime, Stiffness, Damping, MaxStep);
                    layer.skewX.Step(shapeSkew.x, deltaTime, Stiffness, Damping, MaxStep);
                    layer.skewY.Step(shapeSkew.y, deltaTime, Stiffness, Damping, MaxStep);
                    layer.radius.Step(radiusRatio, deltaTime, Stiffness, Damping, MaxStep);
                }
                else
                {
                    ref Layer around = ref stack[i - 1];
                    layer.angle.Step(Mathf.Lerp(shapeAngle + i * liveTwist, around.angle.value + liveTwist, Trail), deltaTime, Stiffness, Damping, MaxStep);
                    layer.skewX.Step(Mathf.Lerp(shapeSkew.x, around.skewX.value, Trail), deltaTime, Stiffness, Damping, MaxStep);
                    layer.skewY.Step(Mathf.Lerp(shapeSkew.y, around.skewY.value, Trail), deltaTime, Stiffness, Damping, MaxStep);
                    layer.radius.Step(Mathf.Lerp(radiusRatio, around.radius.value, Trail), deltaTime, Stiffness, Damping, MaxStep);
                }

                layer.size.Step(targetSize, deltaTime, 140f, 0.85f, MaxStep);
                layer.visibility.Step(i < count ? 1f : 0f, deltaTime, 160f, 1f, MaxStep);
                layer.pop.Step(1f, deltaTime, 320f, 0.42f, MaxStep);
                layer.tone = Mathf.Lerp(layer.tone, Tone(i), toneBlend);

                // A new shape type or side count runs from the outer layer to the core
                bool changed = layer.shape != shapeType || (shapeType == ShapeType.Polygon && layer.sides != polygonSides);
                if (changed && now >= shapeChangedAt + i * Stagger)
                {
                    layer.shape = shapeType;
                    layer.sides = polygonSides;
                    layer.pop.velocity -= Pop;
                }

                Apply(layers[i], ref layer);

                targetSize = i < count - 1
                    ? Mathf.Clamp((targetSize - 2f * Padding) / fit, targetSize * minRatio, targetSize * 0.9f)
                    : targetSize * 0.9f;
            }
        }

        void Apply(ProceduralShape target, ref Layer layer)
        {
            float visibility = Mathf.Clamp01(layer.visibility.value);
            bool visible = visibility > 0.01f;

            // Only this script toggles the layers, so the last state stands in for an activeSelf read
            if (layer.shown != visible)
            {
                layer.shown = visible;
                target.gameObject.SetActive(visible);
            }

            if (!visible)
                return;

            float size = layer.size.value * layer.pop.value * (0.55f + 0.45f * visibility);
            target.rectTransform.sizeDelta = new Vector2(size, size);
            target.Shape = layer.shape;
            target.PolygonSides = layer.sides;
            target.ShapeAngle = layer.angle.value;
            target.ShapeSkew = new Vector2(layer.skewX.value, layer.skewY.value);
            target.UniformCornerRadius = layer.radius.value * size;

            Color color = Color.LerpUnclamped(background, accent, Mathf.Pow(Mathf.Clamp01(layer.tone), ColorCurve));
            color.a *= visibility;
            target.FillColor = color;
        }

        // Place in the Background to Accent ramp: 0 for the outer layer, 1 for the core
        float Tone(int index) => count > 1 ? Mathf.Min(index, count - 1) / (count - 1f) : 1f;

        public void SetShape(int index)
        {
            shapeType = choices[index];
            shapeChangedAt = Time.unscaledTime;
            Lock(true);
        }

        void Lock(bool animate)
        {
            DemoUtils.SetLocked(sidesOption, shapeType != ShapeType.Polygon, animate);
            DemoUtils.SetLocked(radiusOption, shapeType == ShapeType.Ellipse, animate);
        }

        public void SetSides(float value)
        {
            polygonSides = Mathf.RoundToInt(value);
            shapeChangedAt = Time.unscaledTime;
        }

        public void SetCornerRadius(float value) => cornerRadius = value;
        public void SetAngle(float value) => shapeAngle = value;
        public void SetSkewX(float value) => shapeSkew.x = value;
        public void SetSkewY(float value) => shapeSkew.y = value;
        public void SetTwist(float value) => twistAngle = value;
        public void SetLayers(float value) => count = Mathf.RoundToInt(value);

        public void UpdateStyler()
        {
            if (preset == null || !preset.TryGetColor("Background", out Color newBackground) 
                || !preset.TryGetColor("Accent", out Color newAccent))
                return;

            fromBackground = hasColors ? background : newBackground;
            fromAccent = hasColors ? accent : newAccent;
            toBackground = newBackground;
            toAccent = newAccent;
            fadeStart = Time.unscaledTime;
            fadeDuration = ColorTransition.Duration;
            hasColors = true;
        }
    }
}