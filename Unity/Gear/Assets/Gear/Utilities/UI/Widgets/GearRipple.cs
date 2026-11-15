using UnityEngine;
using UnityEngine.EventSystems;
using Gear.Utilities.UI.ImageExtensions;

namespace Gear.Utilities.UI.Widgets
{
    /// <summary>
    /// ImageEx 8 波涟漪：点击/手动 Emit 写入环形缓冲，shader 内按时间扩散。
    /// 不需要为每次点击创建 GameObject 或材质。
    /// </summary>
    [AddComponentMenu("UI/Gear/Ripple")]
    [RequireComponent(typeof(ImageEx))]
    [DisallowMultipleComponent]
    public sealed class GearRipple : MonoBehaviour, IPointerDownHandler
    {
        private const int MaxWaves = 8;

        [SerializeField] private bool emitOnClick = true;
        [SerializeField, Range(0.05f, 4f)] private float speed = 0.9f;
        [SerializeField, Range(0.002f, 0.2f)] private float width = 0.035f;
        [SerializeField, Range(0f, 2f)] private float strength = 1f;
        [SerializeField] private Color color = new Color(0.486f, 0.361f, 0.988f, 0.85f);

        private readonly Vector4[] waves = new Vector4[MaxWaves];
        private int waveCount;
        private ImageEx image;
        private bool transientRegistered;

        public int WaveCount => waveCount;
        public float Speed { get => speed; set => speed = Mathf.Clamp(value, 0.05f, 4f); }
        public float Width { get => width; set => width = Mathf.Clamp(value, 0.002f, 0.2f); }
        public float Strength { get => strength; set => strength = Mathf.Clamp(value, 0f, 2f); }
        public Color Color { get => color; set => color = value; }

        private void OnEnable()
        {
            image = GetComponent<ImageEx>();
            if (image != null && !transientRegistered)
            {
                image.AddTransientEffect();
                transientRegistered = true;
            }
            Push();
        }

        private void OnDisable()
        {
            waveCount = 0;
            Push();
            if (transientRegistered && image != null)
            {
                image.RemoveTransientEffect();
                transientRegistered = false;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!emitOnClick || eventData == null) return;
            var rect = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rect, eventData.position, eventData.pressEventCamera, out Vector2 local))
            {
                return;
            }

            Rect r = rect.rect;
            if (r.width <= 0.0001f || r.height <= 0.0001f) return;
            Emit(new Vector2(local.x / r.width + 0.5f, local.y / r.height + 0.5f));
        }

        /// <summary>发射一波；center 为 0..1 的本地 UV 坐标。</summary>
        public void Emit(Vector2 center, float waveStrength = 1f)
        {
            for (int i = MaxWaves - 1; i > 0; i--) waves[i] = waves[i - 1];
            waves[0] = new Vector4(
                Mathf.Clamp01(center.x), Mathf.Clamp01(center.y),
                Time.time, Mathf.Max(0f, waveStrength));
            waveCount = Mathf.Min(MaxWaves, waveCount + 1);
            Push();
        }

        public void Clear()
        {
            waveCount = 0;
            Push();
        }

        private void Push()
        {
            if (image == null) image = GetComponent<ImageEx>();
            if (image == null) return;
            Material material = image.material;
            if (material == null) return;
            material.SetVectorArray("_RippleData", waves);
            material.SetFloat("_RippleCount", waveCount);
            material.SetFloat("_RippleSpeed", speed);
            material.SetFloat("_RippleWidth", width);
            material.SetFloat("_RippleStrength", strength);
            material.SetColor("_RippleColor", color);
        }
    }
}
