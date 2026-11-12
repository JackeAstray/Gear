using UnityEngine;
using UnityEngine.UI;
using Evo.Tween;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Soft Mask tab: a scrolling list masked by a Raw Image. Soft Mask reads the alpha of an Image or Raw Image texture,
    /// so the shape and softness options redraw a small fade texture. Invert is the mask's own option.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoSoftMask : MonoBehaviour
    {
        [SerializeField] private SoftMask mask;
        [SerializeField] private RawImage maskImage;
        [Tooltip("Holds the rows twice, so the scroll can wrap after one set.")]
        [SerializeField] private RectTransform list;
        [SerializeField] private VerticalLayoutGroup listLayout;
        [SerializeField] private float scrollSpeed = 40f;

        [Header("Options")]
        [SerializeField] private RadioButtonGroup shape;
        [SerializeField] private Slider softness;
        [SerializeField] private Switch invert;
        [SerializeField] private Switch scroll;

        const int Size = 128;
        const int ScrollKey = 1;
        Texture2D fade;
        Color32[] pixels;
        bool radial;
        float softnessValue = 0.5f;
        bool scrolling = true;
        float startY;
        float period;
        TweenHandle scrollTween;

        void Awake()
        {
            startY = list.anchoredPosition.y;
            fade = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "Soft Mask Fade", wrapMode = TextureWrapMode.Clamp };
            pixels = new Color32[Size * Size];
            maskImage.texture = fade;
            Redraw();

            DemoUtils.Show(shape, radial ? 1 : 0);
            DemoUtils.Show(softness, softnessValue * 100f);
            DemoUtils.Show(invert, mask.InvertMask);
            DemoUtils.Show(scroll, scrolling);
        }

        void OnDestroy() => Destroy(fade);

        void OnEnable()
        {
            if (scrolling)
                Scroll();
        }

        void OnDisable() => scrollTween.Kill();

        // A continuous loop: the list moves up and wraps after one set of rows, from wherever it stopped
        void Scroll()
        {
            period = (list.rect.height + listLayout.spacing) * 0.5f;
            float from = list.anchoredPosition.y - startY;
            scrollTween = EvoTween.Create(from, from + period, period / scrollSpeed)
                .WithLoops(0)
                .WithTimeMode(TimeMode.Unscaled)
                .Bind(this, static (demo, distance) => demo.SetScrolled(distance), ScrollKey);
        }

        void SetScrolled(float distance) => list.anchoredPosition = new Vector2(list.anchoredPosition.x, startY + Mathf.Repeat(distance, period));

        // Edges fade at the top and bottom, Radial is a soft circle
        void Redraw()
        {
            // The editor's sliders can report their value before Awake; Awake draws with it
            if (fade == null)
                return;

            float edge = 0.02f + softnessValue * 0.5f;
            for (int y = 0; y < Size; y++)
            {
                float v = (y + 0.5f) / Size - 0.5f;
                for (int x = 0; x < Size; x++)
                {
                    float alpha;
                    if (radial)
                    {
                        float u = (x + 0.5f) / Size - 0.5f;
                        float distance = Mathf.Sqrt(u * u + v * v) * 2f;
                        alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f - edge * 2f, 1f, distance));
                    }
                    else { alpha = Mathf.SmoothStep(0f, 1f, (0.5f - Mathf.Abs(v)) / edge); }
                    pixels[y * Size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }
            fade.SetPixels32(pixels);
            fade.Apply(false);
        }

        public void SetShape(int index)
        {
            radial = index == 1;
            Redraw();
        }

        // Percent bar
        public void SetSoftness(float value)
        {
            softnessValue = value / 100f;
            Redraw();
        }

        public void SetScroll(bool value)
        {
            scrolling = value;
            if (!isActiveAndEnabled)
                return;

            if (value) { Scroll(); }
            else { scrollTween.Kill(); }
        }
    }
}