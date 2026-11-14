using UnityEngine;

namespace Gear.Utilities.UI.Motion
{
    /// <summary>
    /// 旋转器：按固定角速度旋转自身（加载圈、指针、装饰件）。
    /// <para>纯 C#，提供 <see cref="Step"/> 便于测试与外部驱动。</para>
    /// </summary>
    [AddComponentMenu("UI/Gear/Spinner")]
    [DisallowMultipleComponent]
    public sealed class GearSpinner : MonoBehaviour
    {
        [SerializeField] private float degreesPerSecond = 240f;
        [SerializeField] private bool clockwise = true;
        [SerializeField] private bool useUnscaledTime = true;

        /// <summary>旋转速度（度/秒）。</summary>
        public float DegreesPerSecond
        {
            get => degreesPerSecond;
            set => degreesPerSecond = value;
        }

        /// <summary>是否顺时针。</summary>
        public bool Clockwise
        {
            get => clockwise;
            set => clockwise = value;
        }

        private void Update()
        {
            Step(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);
        }

        /// <summary>手动推进（测试 / 外部驱动用）。</summary>
        public void Step(float delta)
        {
            if (delta <= 0f) return;
            float direction = clockwise ? -1f : 1f;
            transform.Rotate(0f, 0f, direction * degreesPerSecond * delta, Space.Self);
        }
    }
}
