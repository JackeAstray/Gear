using UnityEngine;
using UnityEngine.EventSystems;

namespace Gear.Utilities.UI.ImageExtensions
{
    /// <summary>
    /// 把指针位置写入同一物体上的 <see cref="ImageEx"/>，驱动 Evo 风格的 Border Glow。
    /// <para>
    /// 挂在带 ImageEx 的按钮/卡片上即可：悬停时边缘按指针位置发亮，移出后恢复环境光。
    /// 只改 ImageEx 的公开属性，不接管渲染，也不依赖任何补间库。
    /// </para>
    /// </summary>
    [RequireComponent(typeof(ImageEx))]
    [AddComponentMenu("UI/Gear/ImageEx 指针辉光")]
    [DisallowMultipleComponent]
    public sealed class ImageExPointerGlow : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField, Tooltip("悬停时才点亮；关闭则常亮（由 ImageEx 的环境光比例决定）")]
        private bool hoverOnly = true;

        /// <summary>是否只在悬停时点亮；false 时配合 Ambient 常亮。</summary>
        public bool HoverOnly
        {
            get => hoverOnly;
            set => hoverOnly = value;
        }

        [SerializeField, Range(0f, 1f), Tooltip("悬停时的辉光强度")]
        private float hoverIntensity = 1f;

        [SerializeField, Tooltip("按下时额外增强（0 = 不增强）")]
        private float pressBoost = 0.15f;

        private ImageEx image;
        private RectTransform rectTransform;
        private float targetHover;
        private float currentHover;
        private bool transientRegistered;

        private void Awake()
        {
            image = GetComponent<ImageEx>();
            rectTransform = (RectTransform)transform;
            image.EnableBorderGlow = true;
            targetHover = hoverOnly ? 0f : hoverIntensity;
            currentHover = targetHover;
            image.BorderGlowHover = currentHover;
        }

        private void OnEnable()
        {
            if (image == null)
            {
                image = GetComponent<ImageEx>();
                rectTransform = (RectTransform)transform;
            }

            image.EnableBorderGlow = true;
            if (hoverOnly) targetHover = 0f;
            image.BorderGlowHover = currentHover;
            if (!transientRegistered)
            {
                image.AddTransientEffect();
                transientRegistered = true;
            }
        }

        private void OnDisable()
        {
            if (transientRegistered && image != null)
            {
                image.RemoveTransientEffect();
                transientRegistered = false;
            }
        }

        private void Update()
        {
            if (Mathf.Approximately(currentHover, targetHover)) return;
            currentHover = Mathf.MoveTowards(currentHover, targetHover, Time.unscaledDeltaTime * 8f);
            if (image != null) image.BorderGlowHover = currentHover;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            targetHover = hoverIntensity;
            UpdatePointer(eventData);
        }

        /// <summary>供交互效果总线驱动，不依赖指针事件。</summary>
        public void SetHover(bool hovered)
        {
            targetHover = hovered || !hoverOnly ? hoverIntensity : 0f;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            targetHover = hoverOnly ? 0f : hoverIntensity;
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            UpdatePointer(eventData);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            UpdatePointer(eventData);
            if (pressBoost > 0f) targetHover = Mathf.Clamp01(hoverIntensity + pressBoost);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            targetHover = hoverIntensity;
        }

        private void UpdatePointer(PointerEventData eventData)
        {
            if (image == null || rectTransform == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 local))
            {
                return;
            }

            Rect rect = rectTransform.rect;
            if (rect.width <= 0.0001f || rect.height <= 0.0001f) return;

            Vector2 normalized = new Vector2(
                local.x / rect.width + rectTransform.pivot.x,
                local.y / rect.height + rectTransform.pivot.y);
            image.BorderGlowPosition = new Vector2(Mathf.Clamp01(normalized.x), Mathf.Clamp01(normalized.y));
        }
    }
}
