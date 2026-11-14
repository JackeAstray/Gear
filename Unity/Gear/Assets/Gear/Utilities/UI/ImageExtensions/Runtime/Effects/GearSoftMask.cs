using UnityEngine;

namespace Gear.Utilities.UI.ImageExtensions
{
    /// <summary>
    /// 挂在容器上的矩形软遮罩：只影响其子树里的 <see cref="ImageEx"/>，通过材质 uniform 在片元阶段做圆角/羽化。
    /// <para>
    /// 这是"每 Canvas/每容器一个矩形软遮罩"的轻量版：支持圆角、羽化、反相；
    /// 不读取 RenderTexture，也不需要给 Image / TMP 准备额外 shader 变体。嵌套时取最近的父级遮罩。
    /// </para>
    /// </summary>
    [AddComponentMenu("UI/Gear/Soft Mask")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class GearSoftMask : MonoBehaviour
    {
        [SerializeField][Min(0f)] private float softness = 8f;
        [SerializeField][Min(0f)] private float radius;
        [SerializeField] private bool invert;

        private static readonly int enabledSp = Shader.PropertyToID("_GearMaskEnabled");
        private static readonly int rectSp = Shader.PropertyToID("_GearMaskRect");
        private static readonly int radiusSp = Shader.PropertyToID("_GearMaskRadius");
        private static readonly int softnessSp = Shader.PropertyToID("_GearMaskSoftness");
        private static readonly int invertSp = Shader.PropertyToID("_GearMaskInvert");

        private readonly Vector3[] worldCorners = new Vector3[4];
        private RectTransform rectTransform;

        /// <summary>遮罩边缘羽化宽度（像素）。0 为硬边。</summary>
        public float Softness
        {
            get => softness;
            set
            {
                float clamped = Mathf.Max(0f, value);
                if (Mathf.Approximately(softness, clamped)) return;
                softness = clamped;
                DirtyChildren();
            }
        }

        /// <summary>遮罩圆角半径（像素）。</summary>
        public float Radius
        {
            get => radius;
            set
            {
                float clamped = Mathf.Max(0f, value);
                if (Mathf.Approximately(radius, clamped)) return;
                radius = clamped;
                DirtyChildren();
            }
        }

        /// <summary>反相：形状内部隐藏、外部显示。</summary>
        public bool Invert
        {
            get => invert;
            set
            {
                if (invert == value) return;
                invert = value;
                DirtyChildren();
            }
        }

        private RectTransform RectTransform
        {
            get
            {
                if (rectTransform == null) rectTransform = (RectTransform)transform;
                return rectTransform;
            }
        }

        private void OnEnable() => DirtyChildren();
        private void OnDisable() => DirtyChildren();
        private void OnDestroy() => DirtyChildren();
        private void OnRectTransformDimensionsChange() => DirtyChildren();
        private void OnTransformChildrenChanged() => DirtyChildren();

        private void LateUpdate()
        {
            // RectTransform 移动/缩放不一定触发 DimensionsChange；用 hasChanged 兜底
            if (!transform.hasChanged) return;
            transform.hasChanged = false;
            DirtyChildren();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            softness = Mathf.Max(0f, softness);
            radius = Mathf.Max(0f, radius);
            DirtyChildren();
        }
#endif

        /// <summary>
        /// 把最近父级软遮罩的参数写进 ImageEx 材质；没有遮罩时关闭。
        /// </summary>
        internal static void ApplyToMaterial(Material material, ImageEx image)
        {
            GearSoftMask mask = image != null ? image.GetComponentInParent<GearSoftMask>(true) : null;
            if (mask == null || !mask.isActiveAndEnabled)
            {
                material.SetInt(enabledSp, 0);
                return;
            }

            mask.WriteToMaterial(material, image);
        }

        private void WriteToMaterial(Material material, ImageEx image)
        {
            RectTransform target = image != null ? image.rectTransform : null;
            if (target == null)
            {
                material.SetInt(enabledSp, 0);
                return;
            }

            RectTransform.GetWorldCorners(worldCorners);
            Matrix4x4 worldToLocal = target.worldToLocalMatrix;
            Rect targetRect = target.rect;

            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            for (int i = 0; i < worldCorners.Length; i++)
            {
                Vector3 local = worldToLocal.MultiplyPoint3x4(worldCorners[i]);
                float x = local.x - targetRect.xMin;
                float y = local.y - targetRect.yMin;
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }

            material.SetInt(enabledSp, 1);
            material.SetVector(rectSp, new Vector4(minX, minY, maxX, maxY));
            material.SetFloat(radiusSp, radius);
            material.SetFloat(softnessSp, softness);
            material.SetInt(invertSp, invert ? 1 : 0);
        }

        /// <summary>
        /// 标记子树内所有 ImageEx 材质脏化；移动/改参数后由组件自动调用。
        /// </summary>
        public void DirtyChildren()
        {
            ImageEx[] images = GetComponentsInChildren<ImageEx>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null)
                    images[i].SetMaterialDirty();
            }
        }
    }
}
