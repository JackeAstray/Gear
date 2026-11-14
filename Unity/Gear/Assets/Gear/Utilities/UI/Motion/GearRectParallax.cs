using UnityEngine;
using UnityEngine.EventSystems;

namespace Gear.Utilities.UI.Motion
{
    /// <summary>
    /// 视差：指针在容器内移动时，目标按归一化位置平移（常用于卡片内的前景/背景分层）。
    /// <para>
    /// 与 <see cref="GearRectSway"/> 互补：Sway 改旋转，Parallax 改位置；都只响应指针、不接管布局。
    /// </para>
    /// </summary>
    [AddComponentMenu("UI/Gear/Rect Parallax")]
    [DisallowMultipleComponent]
    public sealed class GearRectParallax : MonoBehaviour, IPointerMoveHandler, IPointerExitHandler
    {
        [SerializeField] private RectTransform target;
        [SerializeField] private Vector2 maxOffset = new Vector2(12f, 8f);
        [SerializeField, Min(0.01f)] private float smoothness = 10f;
        [SerializeField] private bool invert;
        [SerializeField] private bool useUnscaledTime = true;

        private RectTransform rect;
        private Vector2 basePosition;
        private bool baseCaptured;
        private Vector2 pointer = new Vector2(0.5f, 0.5f);
        private Vector2 current;
        private Vector2 velocity;
        private bool inside;

        /// <summary>被平移的目标，默认自身。</summary>
        public RectTransform Target
        {
            get
            {
                if (target != null) return target;
                if (rect == null) rect = (RectTransform)transform;
                return rect;
            }
            set
            {
                target = value;
                baseCaptured = false;
                current = Vector2.zero;
                velocity = Vector2.zero;
            }
        }

        /// <summary>最大位移（像素）。</summary>
        public Vector2 MaxOffset
        {
            get => maxOffset;
            set => maxOffset = value;
        }

        private void Awake()
        {
            CaptureBase();
        }

        private void Update()
        {
            Step(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);
        }

        /// <summary>指针归一化位置（0-1）。</summary>
        public void SetPointerNormalized(Vector2 normalized)
        {
            CaptureBase();
            pointer = new Vector2(Mathf.Clamp01(normalized.x), Mathf.Clamp01(normalized.y));
            inside = true;
        }

        /// <summary>指针移出：平滑回到原位。</summary>
        public void ClearPointer()
        {
            inside = false;
        }

        /// <summary>立即回原位。</summary>
        public void ResetParallax()
        {
            CaptureBase();
            current = Vector2.zero;
            velocity = Vector2.zero;
            RectTransform t = Target;
            if (t != null) t.anchoredPosition = basePosition;
        }

        /// <summary>手动推进平滑（测试 / 外部驱动用）。</summary>
        public void Step(float delta)
        {
            CaptureBase();
            RectTransform t = Target;
            if (t == null) return;

            Vector2 targetOffset = Vector2.zero;
            if (inside)
            {
                float sign = invert ? -1f : 1f;
                targetOffset = new Vector2(
                    (pointer.x - 0.5f) * 2f * maxOffset.x * sign,
                    (pointer.y - 0.5f) * 2f * maxOffset.y * sign);
            }

            current.x = Mathf.SmoothDamp(current.x, targetOffset.x, ref velocity.x, 1f / smoothness, Mathf.Infinity, delta);
            current.y = Mathf.SmoothDamp(current.y, targetOffset.y, ref velocity.y, 1f / smoothness, Mathf.Infinity, delta);
            t.anchoredPosition = basePosition + current;
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (rect == null) rect = (RectTransform)transform;
            if (rect == null || eventData == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out Vector2 local))
                return;

            Rect localRect = rect.rect;
            if (localRect.width <= 0.0001f || localRect.height <= 0.0001f) return;

            SetPointerNormalized(new Vector2(
                local.x / localRect.width + rect.pivot.x,
                local.y / localRect.height + rect.pivot.y));
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ClearPointer();
        }

        private void CaptureBase()
        {
            if (baseCaptured) return;
            RectTransform t = Target;
            if (t == null) return;
            basePosition = t.anchoredPosition;
            baseCaptured = true;
        }
    }
}
