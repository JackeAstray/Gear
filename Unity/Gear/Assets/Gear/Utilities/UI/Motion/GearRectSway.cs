using UnityEngine;
using UnityEngine.EventSystems;

namespace Gear.Utilities.UI.Motion
{
    /// <summary>
    /// 指针倾斜：按指针在目标内的归一化位置让目标轻微旋转（类似卡片“被指针吸引”）。
    /// <para>
    /// 语义参考 Evo 的 RectSway / Magnetic：纯 C#，用 SmoothDampAngle 平滑，支持单轴开关；
    /// 提供 <see cref="SetPointerNormalized"/> 便于测试与手柄/摇杆驱动。
    /// </para>
    /// </summary>
    [AddComponentMenu("UI/Gear/Rect Sway")]
    [DisallowMultipleComponent]
    public sealed class GearRectSway : MonoBehaviour, IPointerMoveHandler, IPointerExitHandler
    {
        [SerializeField] private RectTransform target;
        [SerializeField, Min(0f)] private float maxAngle = 8f;
        [SerializeField, Min(0.01f)] private float smoothness = 14f;
        [SerializeField] private bool enableXAxis = true;
        [SerializeField] private bool enableYAxis = true;
        [SerializeField] private bool useUnscaledTime = true;

        private RectTransform rect;
        private Quaternion baseRotation;
        private bool baseRotationCaptured;
        private Vector2 pointer = new Vector2(0.5f, 0.5f);
        private float currentX;
        private float currentY;
        private float velocityX;
        private float velocityY;

        private RectTransform Target
        {
            get
            {
                if (target != null) return target;
                if (rect == null) rect = (RectTransform)transform;
                return rect;
            }
        }

        /// <summary>最大倾斜角度（度）。</summary>
        public float MaxAngle
        {
            get => maxAngle;
            set => maxAngle = Mathf.Max(0f, value);
        }

        private void Awake()
        {
            rect = (RectTransform)transform;
            CaptureBaseRotation();
        }

        private void OnEnable()
        {
            CaptureBaseRotation();
            if (!GearMotionSettings.Enabled) ResetSway();
        }

        private void OnDisable()
        {
            ResetSway();
        }

        private void Update()
        {
            if (!GearMotionSettings.Enabled)
            {
                if (baseRotationCaptured) ResetSway();
                return;
            }

            RectTransform t = Target;
            if (t == null) return;

            float delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float targetX = enableXAxis ? (0.5f - pointer.y) * 2f * maxAngle : 0f;
            float targetY = enableYAxis ? (pointer.x - 0.5f) * 2f * maxAngle : 0f;

            currentX = Mathf.SmoothDampAngle(currentX, targetX, ref velocityX, 1f / smoothness, Mathf.Infinity, delta);
            currentY = Mathf.SmoothDampAngle(currentY, targetY, ref velocityY, 1f / smoothness, Mathf.Infinity, delta);

            t.localRotation = baseRotation * Quaternion.Euler(currentX, currentY, 0f);
        }

        /// <summary>指针位置（0-1，左下原点）。</summary>
        public void SetPointerNormalized(Vector2 normalized)
        {
            pointer = new Vector2(Mathf.Clamp01(normalized.x), Mathf.Clamp01(normalized.y));
        }

        /// <summary>回到正位并清空速度。</summary>
        public void ResetSway()
        {
            CaptureBaseRotation();
            pointer = new Vector2(0.5f, 0.5f);
            currentX = 0f;
            currentY = 0f;
            velocityX = 0f;
            velocityY = 0f;

            RectTransform t = Target;
            if (t != null && baseRotationCaptured)
                t.localRotation = baseRotation;
        }

        /// <summary>手动推进平滑（测试 / 外部驱动用）。</summary>
        public void Step(float delta)
        {
            if (!GearMotionSettings.Enabled) return;
            RectTransform t = Target;
            if (t == null) return;
            CaptureBaseRotation();

            float targetX = enableXAxis ? (0.5f - pointer.y) * 2f * maxAngle : 0f;
            float targetY = enableYAxis ? (pointer.x - 0.5f) * 2f * maxAngle : 0f;
            currentX = Mathf.SmoothDampAngle(currentX, targetX, ref velocityX, 1f / smoothness, Mathf.Infinity, delta);
            currentY = Mathf.SmoothDampAngle(currentY, targetY, ref velocityY, 1f / smoothness, Mathf.Infinity, delta);
            t.localRotation = baseRotation * Quaternion.Euler(currentX, currentY, 0f);
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (!GearMotionSettings.Enabled) return;
            RectTransform t = Target;
            if (t == null || eventData == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(t, eventData.position, eventData.pressEventCamera, out Vector2 local))
                return;

            Rect rect = t.rect;
            if (rect.width <= 0.0001f || rect.height <= 0.0001f) return;

            SetPointerNormalized(new Vector2(
                local.x / rect.width + t.pivot.x,
                local.y / rect.height + t.pivot.y));
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pointer = new Vector2(0.5f, 0.5f);
        }

        private void CaptureBaseRotation()
        {
            if (baseRotationCaptured) return;
            RectTransform t = Target;
            if (t == null) return;
            baseRotation = t.localRotation;
            baseRotationCaptured = true;
        }
    }
}
