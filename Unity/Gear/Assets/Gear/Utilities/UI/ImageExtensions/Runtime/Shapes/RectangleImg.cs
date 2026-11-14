using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Gear.Common;
namespace Gear.Utilities.UI.ImageExtensions
{
    /// <summary>
    /// 矩形
    /// </summary>
    [Serializable]
    public class RectangleImg : IUIImageComponent
    {
        [SerializeField] private Vector4 cornerRadius;
        [SerializeField] private CornerRadiusMode radiusMode = CornerRadiusMode.Pixels;
#if UNITY_EDITOR
        [SerializeField] private bool uniformCornerRadius;
#endif
        private static readonly int rectangleCornerRadius_Sp = Shader.PropertyToID("_RectangleCornerRadius");

        public Material sharedMat { get; set; }
        public bool shouldModifySharedMat { get; set; }
        public RectTransform rectTransform { get; set; }

        public event EventHandler onComponentSettingsChanged;

        /// <summary>
        /// 四个角的半径。从左下角逆时针
        /// x=>左下，y=>右下
        /// z=>右上角，w=>左上角
        /// <para>含义取决于 <see cref="RadiusMode"/>：Pixels 为像素值，Percentage 为 0–100（100 = 短边的一半）。</para>
        /// </summary>
        public Vector4 CornerRadius
        {
            get => cornerRadius;
            set
            {
                cornerRadius = Vector4.Max(value, Vector4.zero);
                if (shouldModifySharedMat)
                {
                    sharedMat.SetVector(rectangleCornerRadius_Sp, ResolveCornerRadius());
                }
                onComponentSettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 圆角半径的单位。默认 Pixels，保持旧资源行为不变。
        /// </summary>
        public CornerRadiusMode RadiusMode
        {
            get => radiusMode;
            set
            {
                if (radiusMode == value) return;
                radiusMode = value;
                if (shouldModifySharedMat)
                {
                    sharedMat.SetVector(rectangleCornerRadius_Sp, ResolveCornerRadius());
                }
                onComponentSettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="sharedMat"></param>
        /// <param name="renderMat"></param>
        /// <param name="rectTransform"></param>
        public void Init(Material sharedMat, Material renderMat, RectTransform rectTransform)
        {
            this.sharedMat = sharedMat;
            shouldModifySharedMat = sharedMat == renderMat;
            this.rectTransform = rectTransform;
        }

        public void OnValidate()
        {
            CornerRadius = cornerRadius;
        }

        /// <summary>
        /// 材质的初始化值
        /// <para>材质里只存像素半径，不存单位；Shared 模式回读时保留组件当前的 <see cref="RadiusMode"/>。</para>
        /// </summary>
        /// <param name="material"></param>
        public void InitValuesFromMaterial(ref Material material)
        {
            cornerRadius = material.GetVector(rectangleCornerRadius_Sp);
        }

        /// <summary>
        /// 修改材质
        /// </summary>
        /// <param name="material"></param>
        /// <param name="otherProperties"></param>
        public void ModifyMaterial(ref Material material, params object[] otherProperties)
        {
            material.SetVector(rectangleCornerRadius_Sp, ResolveCornerRadius());
        }

        /// <summary>
        /// 把组件上的圆角设置换算成着色器需要的像素半径，并做相邻角不重叠的修正。
        /// </summary>
        public Vector4 ResolveCornerRadius()
        {
            return ResolveCornerRadius(rectTransform != null ? rectTransform.rect.size : Vector2.zero);
        }

        /// <summary>
        /// 用指定尺寸换算圆角（命中检测等无 RectTransform 的路径使用）。
        /// </summary>
        public Vector4 ResolveCornerRadius(Vector2 rectSize)
        {
            Vector4 radius = cornerRadius;
            if (radiusMode == CornerRadiusMode.Percentage)
            {
                float halfShortEdge = Mathf.Max(0f, Mathf.Min(rectSize.x, rectSize.y)) * 0.5f;
                radius = new Vector4(
                    Mathf.Clamp(radius.x, 0f, 100f) * 0.01f * halfShortEdge,
                    Mathf.Clamp(radius.y, 0f, 100f) * 0.01f * halfShortEdge,
                    Mathf.Clamp(radius.z, 0f, 100f) * 0.01f * halfShortEdge,
                    Mathf.Clamp(radius.w, 0f, 100f) * 0.01f * halfShortEdge);
            }

            return FixRadius(radius, rectSize);
        }

        /// <summary>
        /// 修正半径
        /// </summary>
        /// <param name="radius"></param>
        /// <param name="rectSize">用于计算相邻角约束的尺寸；零尺寸时只做非负修正。</param>
        /// <returns></returns>
        private Vector4 FixRadius(Vector4 radius, Vector2 rectSize)
        {
            radius = Vector4.Max(radius, Vector4.zero);
            if (rectSize.x <= 0f || rectSize.y <= 0f)
                return radius;

            radius = Vector4.Min(radius, Vector4.one * Mathf.Min(rectSize.x, rectSize.y));
            float scaleFactor =
                Mathf.Min(
                    Mathf.Min(
                        Mathf.Min(
                            Mathf.Min(
                                rectSize.x / (radius.x + radius.y),
                                rectSize.x / (radius.z + radius.w)),
                            rectSize.y / (radius.x + radius.w)),
                        rectSize.y / (radius.z + radius.y)),
                    1f);
            return radius * scaleFactor;
        }
    }
}