using TMPro;
using UnityEngine;

namespace Gear.Utilities.UI.Widgets
{
    /// <summary>
    /// 跑马灯：文字宽度超过 viewport 时横向循环滚动；不依赖协程，Tick 可手动推进。
    /// </summary>
    [AddComponentMenu("UI/Gear/Marquee")]
    [DisallowMultipleComponent]
    public sealed class GearMarquee : MonoBehaviour
    {
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform content;
        [SerializeField] private TMP_Text text;
        [SerializeField] private float speed = 40f;
        [SerializeField] private float spacing = 40f;
        [SerializeField] private bool autoPlay = true;
        [SerializeField] private bool useUnscaledTime = true;

        private float startX;
        private bool playing;

        public RectTransform Viewport { get => viewport; set { viewport = value; ResetPosition(); } }
        public RectTransform Content { get => content; set { content = value; ResetPosition(); } }
        public float Speed { get => speed; set => speed = Mathf.Max(0f, value); }
        public float Spacing { get => spacing; set => spacing = Mathf.Max(0f, value); }
        public bool IsPlaying => playing;

        private void Awake()
        {
            if (viewport == null) viewport = transform.parent as RectTransform;
            if (content == null && text != null) content = text.rectTransform;
            if (content == null) content = (RectTransform)transform;
            startX = content.anchoredPosition.x;
        }

        private void OnEnable()
        {
            if (autoPlay) Play();
            else ResetPosition();
        }

        public void Play() => playing = true;
        public void Pause() => playing = false;

        public void ResetPosition()
        {
            if (content == null) return;
            content.anchoredPosition = new Vector2(startX, content.anchoredPosition.y);
        }

        public void Tick(float delta)
        {
            if (!playing || content == null || delta <= 0f) return;
            float distance = Mathf.Max(0f, delta) * speed;
            Vector2 position = content.anchoredPosition;
            position.x -= distance;

            float contentWidth = GetContentWidth();
            float viewportWidth = GetViewportWidth();
            float cycle = contentWidth + spacing;
            if (cycle <= 0.0001f) return;

            if (contentWidth <= viewportWidth)
            {
                content.anchoredPosition = new Vector2(startX, position.y);
                return;
            }

            if (position.x <= startX - cycle) position.x += cycle;
            content.anchoredPosition = position;
        }

        private void Update()
        {
            Tick(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);
        }

        private float GetContentWidth()
        {
            float width = content != null ? content.rect.width : 0f;
            if (text != null) width = Mathf.Max(width, text.preferredWidth);
            return width;
        }

        private float GetViewportWidth()
        {
            return viewport != null ? viewport.rect.width : GetComponent<RectTransform>().rect.width;
        }
    }
}
