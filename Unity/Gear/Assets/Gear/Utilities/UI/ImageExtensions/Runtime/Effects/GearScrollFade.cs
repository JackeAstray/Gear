using UnityEngine;
using UnityEngine.UI;

namespace Gear.Utilities.UI.ImageExtensions
{
    /// <summary>
    /// 让 <see cref="GearSoftMask"/> 跟随 ScrollRect 刷新：滚动时重新计算每行 ImageEx 的遮罩矩形，
    /// 避免内容移动后遮罩仍停在旧位置。
    /// </summary>
    [AddComponentMenu("UI/Gear/Scroll Fade")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ScrollRect))]
    public sealed class GearScrollFade : MonoBehaviour
    {
        [SerializeField] private GearSoftMask mask;

        private ScrollRect scroll;

        public GearSoftMask Mask
        {
            get => mask;
            set
            {
                mask = value;
                if (mask != null) mask.DirtyChildren();
            }
        }

        private void Awake()
        {
            scroll = GetComponent<ScrollRect>();
            if (mask == null)
                mask = GetComponentInChildren<GearSoftMask>(true);
        }

        private void OnEnable()
        {
            if (scroll == null) scroll = GetComponent<ScrollRect>();
            if (scroll != null) scroll.onValueChanged.AddListener(OnScroll);
            if (mask != null) mask.DirtyChildren();
        }

        private void OnDisable()
        {
            if (scroll != null) scroll.onValueChanged.RemoveListener(OnScroll);
        }

        private void OnScroll(Vector2 _)
        {
            if (mask != null) mask.DirtyChildren();
        }
    }
}
