using TMPro;
using UnityEngine;

namespace Gear.Utilities.UI.Widgets
{
    /// <summary>
    /// 出屏指示器：目标离开屏幕时，把箭头贴到安全区边缘并指向目标，同时显示距离。
    /// <para>
    /// 目标可见时按 <see cref="HideWhenVisible"/> 隐藏箭头。边缘数学是纯函数
    /// （<see cref="ClampToEdge"/> / <see cref="AngleFor"/>），EditMode 可直接验证。
    /// </para>
    /// </summary>
    [AddComponentMenu("UI/Gear/Off Screen Indicator")]
    [DisallowMultipleComponent]
    public sealed class GearOffScreenIndicator : MonoBehaviour
    {
        [SerializeField] private RectTransform indicator;
        [SerializeField] private Transform worldTarget;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private RectTransform area;
        [SerializeField] private Vector2 edgePadding = new Vector2(32f, 32f);
        [SerializeField] private bool hideWhenVisible = true;
        [SerializeField] private bool rotateToTarget = true;
        [SerializeField] private TMP_Text distanceLabel;
        [SerializeField] private string distanceFormat = "{0:0} m";

        public RectTransform Indicator
        {
            get => indicator;
            set => indicator = value;
        }

        public Transform WorldTarget
        {
            get => worldTarget;
            set => worldTarget = value;
        }

        public Camera WorldCamera
        {
            get => worldCamera;
            set => worldCamera = value;
        }

        public RectTransform Area
        {
            get => area;
            set => area = value;
        }

        public Vector2 EdgePadding
        {
            get => edgePadding;
            set => edgePadding = new Vector2(Mathf.Max(0f, value.x), Mathf.Max(0f, value.y));
        }

        public bool HideWhenVisible
        {
            get => hideWhenVisible;
            set => hideWhenVisible = value;
        }

        public TMP_Text DistanceLabel
        {
            get => distanceLabel;
            set { distanceLabel = value; }
        }

        public string DistanceFormat
        {
            get => distanceFormat;
            set => distanceFormat = string.IsNullOrEmpty(value) ? "{0:0} m" : value;
        }

        private void LateUpdate()
        {
            Refresh();
        }

        /// <summary>更新箭头位置/朝向与距离；测试可手动调用。</summary>
        public void Refresh()
        {
            if (indicator == null || worldTarget == null) return;

            Camera camera = ResolveCamera();
            if (camera == null) return;

            Vector3 screen = camera.WorldToScreenPoint(worldTarget.position);
            bool behind = screen.z < 0f;

            RectTransform rect = area != null ? area : (RectTransform)transform;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rect, new Vector2(screen.x, screen.y), camera, out local))
            {
                local = Vector2.zero;
            }

            if (behind) local = -local;

            Vector2 halfSize = rect.rect.size * 0.5f;
            Vector2 padding = new Vector2(Mathf.Max(0f, edgePadding.x), Mathf.Max(0f, edgePadding.y));
            bool outside;
            Vector2 edge = ClampToEdge(local, halfSize, padding, out outside);

            bool visible = outside || behind;
            if (indicator.gameObject.activeSelf != visible) indicator.gameObject.SetActive(visible);
            if (!visible)
            {
                UpdateDistance(camera);
                return;
            }

            indicator.anchoredPosition = edge;
            if (rotateToTarget) indicator.localEulerAngles = new Vector3(0f, 0f, AngleFor(local));
            UpdateDistance(camera);
        }

        /// <summary>
        /// 把矩形（以原点为中心）内的点贴到内缩 padding 的边界上。
        /// 未越界时原样返回；越界时沿中心→点方向投影到边界。
        /// </summary>
        public static Vector2 ClampToEdge(Vector2 point, Vector2 halfSize, Vector2 padding, out bool outside)
        {
            Vector2 safePadding = new Vector2(Mathf.Max(0f, padding.x), Mathf.Max(0f, padding.y));
            Vector2 inner = new Vector2(
                Mathf.Max(1f, halfSize.x - safePadding.x),
                Mathf.Max(1f, halfSize.y - safePadding.y));

            outside = Mathf.Abs(point.x) > inner.x || Mathf.Abs(point.y) > inner.y;
            if (!outside) return point;

            float scaleX = Mathf.Abs(point.x) > 0.0001f ? inner.x / Mathf.Abs(point.x) : float.PositiveInfinity;
            float scaleY = Mathf.Abs(point.y) > 0.0001f ? inner.y / Mathf.Abs(point.y) : float.PositiveInfinity;
            float scale = Mathf.Min(scaleX, scaleY);
            return point * Mathf.Clamp01(scale);
        }

        /// <summary>箭头朝向（Z 轴角度，0 = 朝上）。</summary>
        public static float AngleFor(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.0001f) return 0f;
            return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        }

        private Camera ResolveCamera()
        {
            if (worldCamera != null) return worldCamera;
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay && canvas.worldCamera != null)
            {
                worldCamera = canvas.worldCamera;
                return worldCamera;
            }

            worldCamera = Camera.main;
            return worldCamera;
        }

        private void UpdateDistance(Camera camera)
        {
            if (distanceLabel == null || camera == null) return;
            float distance = Vector3.Distance(camera.transform.position, worldTarget.position);
            distanceLabel.text = string.Format(distanceFormat, distance);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            edgePadding = new Vector2(Mathf.Max(0f, edgePadding.x), Mathf.Max(0f, edgePadding.y));
            if (string.IsNullOrEmpty(distanceFormat)) distanceFormat = "{0:0} m";
        }
#endif
    }
}
