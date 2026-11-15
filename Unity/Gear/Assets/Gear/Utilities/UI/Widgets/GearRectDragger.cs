using UnityEngine;
using UnityEngine.EventSystems;

namespace Gear.Utilities.UI.Widgets
{
    public enum GearDragAxis
    {
        Both = 0,
        Horizontal = 1,
        Vertical = 2,
        None = 3,
    }

    /// <summary>
    /// 矩形拖拽：轴约束、边界夹取、松手回弹；纯 anchoredPosition 位移，不依赖物理。
    /// </summary>
    [AddComponentMenu("UI/Gear/Rect Dragger")]
    [DisallowMultipleComponent]
    public sealed class GearRectDragger : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private RectTransform target;
        [SerializeField] private RectTransform bounds;
        [SerializeField] private GearDragAxis axis = GearDragAxis.Both;
        [SerializeField] private bool clampToBounds = true;
        [SerializeField] private bool springBack;
        [SerializeField, Min(0f)] private float springDuration = 0.18f;
        [SerializeField] private bool useUnscaledTime = true;

        private Vector2 dragStart;
        private Vector2 restPosition;
        private Vector2 returnFrom;
        private float returnElapsed;
        private bool returning;

        public RectTransform Target
        {
            get => target != null ? target : (RectTransform)transform;
            set => target = value;
        }

        public RectTransform Bounds { get => bounds; set => bounds = value; }
        public GearDragAxis Axis { get => axis; set => axis = value; }
        public bool ClampToBounds { get => clampToBounds; set => clampToBounds = value; }
        public bool SpringBack { get => springBack; set => springBack = value; }
        public bool IsDragging { get; private set; }

        private void Awake()
        {
            if (target == null) target = (RectTransform)transform;
            restPosition = target.anchoredPosition;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData == null) return;
            RectTransform rect = Target;
            if (rect == null) return;
            IsDragging = true;
            returning = false;
            dragStart = rect.anchoredPosition;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!IsDragging || eventData == null) return;
            RectTransform rect = Target;
            if (rect == null) return;

            Vector2 delta = eventData.delta;
            if (axis == GearDragAxis.Horizontal) delta.y = 0f;
            else if (axis == GearDragAxis.Vertical) delta.x = 0f;
            else if (axis == GearDragAxis.None) delta = Vector2.zero;

            Vector2 position = rect.anchoredPosition + delta;
            rect.anchoredPosition = clampToBounds ? Clamp(position) : position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!IsDragging) return;
            IsDragging = false;
            if (!springBack) return;
            RectTransform rect = Target;
            if (rect == null) return;
            returnFrom = rect.anchoredPosition;
            returnElapsed = 0f;
            returning = true;
        }

        /// <summary>手动推进回弹，测试/外部时钟可用。</summary>
        public void Tick(float delta)
        {
            if (!returning) return;
            RectTransform rect = Target;
            if (rect == null)
            {
                returning = false;
                return;
            }

            returnElapsed += Mathf.Max(0f, delta);
            float t = springDuration <= 0f ? 1f : Mathf.Clamp01(returnElapsed / springDuration);
            rect.anchoredPosition = Vector2.Lerp(returnFrom, restPosition, Mathf.SmoothStep(0f, 1f, t));
            if (t >= 1f) returning = false;
        }

        private void Update()
        {
            Tick(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);
        }

        private Vector2 Clamp(Vector2 position)
        {
            if (bounds == null) return position;
            RectTransform rect = Target;
            if (rect == null) return position;

            Rect b = bounds.rect;
            Rect t = rect.rect;
            float halfW = t.width * 0.5f;
            float halfH = t.height * 0.5f;
            position.x = Mathf.Clamp(position.x, b.xMin + halfW, b.xMax - halfW);
            position.y = Mathf.Clamp(position.y, b.yMin + halfH, b.yMax - halfH);
            return position;
        }
    }
}
