using UnityEngine;
using UnityEngine.EventSystems;
using Evo.Tween;
using ShapeType = Evo.UI.ProceduralShape.ShapeType;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Pattern tab: a desktop wallpaper whose fill pattern switches between tile setups, colors, size, gap and angle,
    /// with a falloff and a scroll. Pressing or dragging on the wallpaper moves the ring, and the falloff origin with it.
    /// The options start from the wallpaper's own pattern.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoShapePattern : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        [System.Serializable]
        struct Tiles
        {
            public ShapeType shape;
            public int sides;
            public float radius;
            public Vector2 size;
            public Vector2 gap;
            public float angle;
        }

        [SerializeField] private ProceduralShape wallpaper;
        [SerializeField] private RectTransform origin;
        [Tooltip("One setup per Shape option, in the same order. Size and Gap edit X; Y keeps the setup's proportions.")]
        [SerializeField] private Tiles[] tiles;
        [Tooltip("Seconds the tiles take to grow in after a shape change.")]
        [SerializeField] private float growDuration = 0.45f;
        [Tooltip("Scroll speed in pixels per second.")]
        [SerializeField] private float scrollSpeed = 16f;
        [Tooltip("Screen direction the tiles scroll toward; (1, -1) is the bottom right.")]
        [SerializeField] private Vector2 scrollDirection = new(1f, -1f);

        [Header("Options")]
        [SerializeField] private RadioButtonGroup shape;
        [SerializeField] private RadioButtonGroup color;
        [SerializeField] private Slider size;
        [SerializeField] private Slider gap;
        [SerializeField] private Slider angle;
        [SerializeField] private Switch falloff;
        [SerializeField] private Switch scroll;
        [SerializeField] private Slider falloffStart;
        [SerializeField] private Slider falloffEnd;
        [Tooltip("Locked while the falloff is off.")]
        [SerializeField] private CanvasGroup falloffOptions;

        const int GrowKey = 1;
        const int ScrollKey = 2;
        const float ClockPeriod = 60f;

        int current;
        Vector2 tileSize;
        Vector2 tileGap;
        float growth = 1f;
        float clock;
        TweenHandle scrollClock;

        void Awake()
        {
            current = FindTiles();
            tileSize = wallpaper.PatternSize;
            tileGap = wallpaper.PatternGap;

            DemoUtils.Show(shape, current);
            DemoUtils.Show(color, wallpaper.patternColorMode == ProceduralShape.ColorMode.StylerGradient ? 2
                : wallpaper.patternStyler.color.stylerID == "Primary" ? 1 : 0);
            DemoUtils.Show(size, tileSize.x);
            DemoUtils.Show(gap, tileGap.x);
            DemoUtils.Show(angle, wallpaper.PatternAngle);
            DemoUtils.Show(falloff, wallpaper.PatternFalloff);
            DemoUtils.Show(falloffStart, wallpaper.PatternFalloffStart);
            DemoUtils.Show(falloffEnd, wallpaper.PatternFalloffEnd);

            origin.anchoredPosition = Rect.NormalizedToPoint(wallpaper.rectTransform.rect, wallpaper.PatternFalloffOrigin);
            origin.gameObject.SetActive(wallpaper.PatternFalloff);
            DemoUtils.SetLocked(falloffOptions, !wallpaper.PatternFalloff, false);

            if (scroll != null)
                scroll.onValueChanged.AddListener(SetScrolling);
        }

        void OnEnable() => SetScrolling(scroll != null && scroll.IsOn);

        void OnDisable() => scrollClock.Kill();

        // The scroll follows the live angle, size and gap on every frame, so a looping Evo Tween is its clock
        void SetScrolling(bool value)
        {
            if (!value || !isActiveAndEnabled)
            {
                scrollClock.Kill();
                return;
            }

            clock = 0f;
            scrollClock = EvoTween.Create(0f, ClockPeriod, ClockPeriod)
                .WithLoops(0)
                .WithTimeMode(TimeMode.Unscaled)
                .Bind(this, static (demo, seconds) => demo.Scroll(seconds), ScrollKey);
        }

        // Seconds within the minute; each tick moves the tiles by the time since the last one
        void Scroll(float seconds)
        {
            float dt = Mathf.Repeat(seconds - clock, ClockPeriod);
            clock = seconds;

            // The offset lives in the rotated pattern space, so the screen move is rotated into it.
            // Each axis repeats every tile plus gap, which keeps the wrap seamless.
            float radians = wallpaper.PatternAngle * Mathf.Deg2Rad, cos = Mathf.Cos(radians), sin = Mathf.Sin(radians);
            Vector2 move = scrollSpeed * dt * scrollDirection.normalized;
            Vector2 cell = tileSize + tileGap;
            Vector2 offset = wallpaper.PatternOffset + new Vector2(move.x * cos + move.y * sin, move.y * cos - move.x * sin);
            wallpaper.PatternOffset = new Vector2(Mathf.Repeat(offset.x, cell.x), Mathf.Repeat(offset.y, cell.y));
        }

        public void OnPointerDown(PointerEventData eventData) => MoveOrigin(eventData);
        public void OnDrag(PointerEventData eventData) => MoveOrigin(eventData);

        public void SetPatternShape(int index)
        {
            // Each setup brings its own size, gap and angle, and its tiles grow in from nothing
            current = index;
            Tiles setup = tiles[index];
            tileSize = setup.size;
            tileGap = setup.gap;
            wallpaper.PatternShape = setup.shape;
            wallpaper.PatternPolygonSides = setup.sides;
            wallpaper.PatternRadius = setup.radius;
            wallpaper.PatternAngle = setup.angle;
            DemoUtils.Show(size, setup.size.x);
            DemoUtils.Show(gap, setup.gap.x);
            DemoUtils.Show(angle, setup.angle);

            // Binding applies the start at once
            EvoTween.Create(0f, 1f, growDuration)
                .WithTimeMode(TimeMode.Unscaled)
                .Bind(this, static (demo, value) => demo.Grow(value), GrowKey);
        }

        void Grow(float value)
        {
            growth = value;
            ApplyTiles();
        }

        public void SetPatternColor(int index)
        {
            // 0: Accent, 1: translucent Primary, 2: the Red Blue gradient.
            // The color mode and Styler mapping are plain fields, so the shape needs a refresh to show them
            wallpaper.patternColorMode = index == 2 ? ProceduralShape.ColorMode.StylerGradient : ProceduralShape.ColorMode.Styler;
            wallpaper.patternStyler.color.stylerID = index == 1 ? "Primary" : "Accent";
            wallpaper.patternStyler.color.overrideAlpha = index == 1;
            wallpaper.UpdateStyler();
        }

        public void SetPatternSize(float value)
        {
            tileSize = KeepProportions(value, tiles[current].size);
            ApplyTiles();
        }

        public void SetPatternGap(float value)
        {
            tileGap = KeepProportions(value, tiles[current].gap);
            ApplyTiles();
        }

        public void SetFalloff(bool value)
        {
            wallpaper.PatternFalloff = value;
            origin.gameObject.SetActive(value);
            DemoUtils.SetLocked(falloffOptions, !value);
        }

        int FindTiles()
        {
            for (int i = 0; i < tiles.Length; i++)
            {
                if (tiles[i].shape == wallpaper.PatternShape && (tiles[i].shape != ShapeType.Polygon || tiles[i].sides == wallpaper.PatternPolygonSides))
                    return i;
            }

            return 0;
        }

        static Vector2 KeepProportions(float x, Vector2 setup) => new(x, setup.x > 0f ? x * setup.y / setup.x : x);

        // The tiles grow in place: the period stays while their size eases in with a slight overshoot
        void ApplyTiles()
        {
            float t = growth - 1f;
            Vector2 grown = tileSize * (1f + 2.4f * t * t * t + 1.4f * t * t);
            wallpaper.PatternSize = Vector2.Max(grown, Vector2.one * 0.1f);
            wallpaper.PatternGap = Vector2.Max(tileSize + tileGap - grown, Vector2.zero);
        }

        void MoveOrigin(PointerEventData eventData)
        {
            RectTransform area = wallpaper.rectTransform;
            if (!wallpaper.PatternFalloff || !RectTransformUtility.ScreenPointToLocalPointInRectangle(area, eventData.position, eventData.pressEventCamera, out Vector2 local))
                return;

            // The whole ring stays on the wallpaper, and the falloff starts at its center
            Rect bounds = area.rect;
            Vector2 half = origin.rect.size * 0.5f;
            local.x = Mathf.Clamp(local.x, bounds.xMin + half.x, bounds.xMax - half.x);
            local.y = Mathf.Clamp(local.y, bounds.yMin + half.y, bounds.yMax - half.y);
            origin.anchoredPosition = local;
            wallpaper.PatternFalloffOrigin = Rect.PointToNormalized(bounds, local);
        }
    }
}
