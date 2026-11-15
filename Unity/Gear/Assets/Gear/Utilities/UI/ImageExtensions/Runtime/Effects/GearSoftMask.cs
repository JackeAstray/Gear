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
        public enum SoftMaskShape
        {
            RoundedRect = 0,
            Circle = 1,
        }

        [SerializeField][Min(0f)] private float softness = 8f;
        [SerializeField][Min(0f)] private float radius;
        [SerializeField] private bool invert;
        [SerializeField] private SoftMaskShape shape = SoftMaskShape.RoundedRect;

        private static readonly int enabledSp = Shader.PropertyToID("_GearMaskEnabled");
        private static readonly int rectSp = Shader.PropertyToID("_GearMaskRect");
        private static readonly int radiusSp = Shader.PropertyToID("_GearMaskRadius");
        private static readonly int softnessSp = Shader.PropertyToID("_GearMaskSoftness");
        private static readonly int invertSp = Shader.PropertyToID("_GearMaskInvert");
        private static readonly int shapeSp = Shader.PropertyToID("_GearMaskShape");

        private static readonly int enabled2Sp = Shader.PropertyToID("_GearMask2Enabled");
        private static readonly int rect2Sp = Shader.PropertyToID("_GearMask2Rect");
        private static readonly int radius2Sp = Shader.PropertyToID("_GearMask2Radius");
        private static readonly int softness2Sp = Shader.PropertyToID("_GearMask2Softness");
        private static readonly int invert2Sp = Shader.PropertyToID("_GearMask2Invert");
        private static readonly int shape2Sp = Shader.PropertyToID("_GearMask2Shape");

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

        public SoftMaskShape Shape
        {
            get => shape;
            set
            {
                if (shape == value) return;
                shape = value;
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
            if (material == null) return;

            GearSoftMask[] masks = image != null
                ? image.GetComponentsInParent<GearSoftMask>(true)
                : null;
            int layer = 0;
            if (masks != null)
            {
                for (int i = 0; i < masks.Length && layer < 2; i++)
                {
                    GearSoftMask mask = masks[i];
                    if (mask == null || !mask.isActiveAndEnabled) continue;
                    mask.WriteToMaterial(material, image, layer);
                    layer++;
                }
            }

            if (layer == 0)
            {
                material.SetInt(enabledSp, 0);
                material.SetInt(enabled2Sp, 0);
            }
            else if (layer == 1)
            {
                material.SetInt(enabled2Sp, 0);
            }
        }

        private void WriteToMaterial(Material material, ImageEx image, int layer)
        {
            RectTransform target = image != null ? image.rectTransform : null;
            if (target == null)
            {
                material.SetInt(layer == 0 ? enabledSp : enabled2Sp, 0);
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

            bool first = layer == 0;
            material.SetInt(first ? enabledSp : enabled2Sp, 1);
            material.SetVector(first ? rectSp : rect2Sp, new Vector4(minX, minY, maxX, maxY));
            material.SetFloat(first ? radiusSp : radius2Sp, radius);
            material.SetFloat(first ? softnessSp : softness2Sp, softness);
            material.SetInt(first ? invertSp : invert2Sp, invert ? 1 : 0);
            material.SetFloat(first ? shapeSp : shape2Sp, (int)shape);
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
