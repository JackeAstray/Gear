using UnityEngine;
using UnityEngine.EventSystems;

namespace Gear.Utilities.UI.ImageExtensions
{
    /// <summary>
    /// 指针聚光：把指针位置写成 <see cref="ImageEx"/> 的 Spotlight 参数，在图形内部叠一团软光。
    /// <para>
    /// 与 <see cref="ImageExPointerGlow"/> 互补：辉光在边缘，聚光在内部；两者可同时挂，
    /// 分别写不同的 uniform，互不覆盖。只在悬停时点亮，移出后淡出。
    /// </para>
    /// </summary>
    [RequireComponent(typeof(ImageEx))]
    [AddComponentMenu("UI/Gear/ImageEx 指针聚光")]
    [DisallowMultipleComponent]
    public sealed class ImageExSpotlight : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        [SerializeField] private Color color = new Color(1f, 0.97f, 0.9f, 1f);
        [SerializeField, Min(1f)] private float radius = 140f;
        [SerializeField, Range(0f, 1f)] private float softness = 0.6f;
        [SerializeField, Range(0f, 1f)] private float intensity = 0.35f;
        [SerializeField] private bool hoverOnly = true;
        [SerializeField, Min(0.01f)] private float fadeSpeed = 8f;

        private ImageEx image;
        private RectTransform rectTransform;
        private float targetHover;
        private float currentHover;

        public Color Color
        {
            get => color;
            set { color = value; ApplySettings(); }
        }

        public float Radius
        {
            get => radius;
            set { radius = Mathf.Max(1f, value); ApplySettings(); }
        }

        public float Softness
        {
            get => softness;
            set { softness = Mathf.Clamp01(value); ApplySettings(); }
        }

        public float Intensity
        {
            get => intensity;
            set { intensity = Mathf.Clamp01(value); ApplySettings(); }
        }

        public bool HoverOnly
        {
            get => hoverOnly;
            set
            {
                hoverOnly = value;
                targetHover = hoverOnly ? 0f : 1f;
            }
        }

        private void Awake()
        {
            Resolve();
            targetHover = hoverOnly ? 0f : 1f;
            currentHover = targetHover;
            ApplySettings();
            if (image != null) image.SpotlightHover = currentHover;
        }

        private void OnEnable()
        {
            Resolve();
            targetHover = hoverOnly ? 0f : 1f;
            ApplySettings();
            if (image != null) image.SpotlightHover = currentHover;
        }

        private void Update()
        {
            if (image == null || Mathf.Approximately(currentHover, targetHover)) return;
            currentHover = Mathf.MoveTowards(currentHover, targetHover, Time.unscaledDeltaTime * fadeSpeed);
            image.SpotlightHover = currentHover;
        }

        /// <summary>
        /// 把组件配置写给 ImageEx。运行时由 Awake/OnEnable 调用；
        /// 编辑器预览与 EditMode 测试可手动调用（编辑模式下生命周期不执行）。
        /// </summary>
        public void ApplySettings()
        {
            Resolve();
            if (image == null) return;
            image.EnableSpotlight = true;
            image.SpotlightColor = color;
            image.SpotlightRadius = radius;
            image.SpotlightSoftness = softness;
            image.SpotlightIntensity = intensity;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            targetHover = 1f;
            UpdatePointer(eventData);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            targetHover = 0f;
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            UpdatePointer(eventData);
        }

        private void Resolve()
        {
            if (image == null) image = GetComponent<ImageEx>();
            if (rectTransform == null) rectTransform = (RectTransform)transform;
        }

        private void UpdatePointer(PointerEventData eventData)
        {
            Resolve();
            if (image == null || rectTransform == null || eventData == null) return;
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
            image.SpotlightPosition = new Vector2(Mathf.Clamp01(normalized.x), Mathf.Clamp01(normalized.y));
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            radius = Mathf.Max(1f, radius);
            softness = Mathf.Clamp01(softness);
            intensity = Mathf.Clamp01(intensity);
            fadeSpeed = Mathf.Max(0.01f, fadeSpeed);
            if (image != null) ApplySettings();
        }
#endif
    }
}
