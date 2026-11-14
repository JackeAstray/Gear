using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Gear.Utilities.UI.Motion
{
    /// <summary>
    /// 径向布局：把子项中心排布在一个圆上，可设半径/起止角度/顺逆时针/是否朝向圆心。
    /// <para>
    /// 参考 Evo 的 RadialLayoutGroup，但只做 UI 常用部分：不接管子项尺寸，只改 anchoredPosition 与可选旋转。
    /// </para>
    /// </summary>
    [AddComponentMenu("UI/Gear/Radial Layout")]
    public sealed class GearRadialLayout : LayoutGroup
    {
        public enum RadiusSource
        {
            Fixed = 0,
            Fit = 1,
        }

        [SerializeField] private RadiusSource radiusSource = RadiusSource.Fixed;
        [SerializeField, Min(0f)] private float radius = 100f;
        [SerializeField] private float startAngle;
        [SerializeField] private float endAngle = 360f;
        [SerializeField] private bool clockwise = true;
        [SerializeField] private bool fillFullCircle;
        [SerializeField] private bool rotateToCenter;

        private readonly List<RectTransform> children = new List<RectTransform>();

        public float Radius
        {
            get => radius;
            set => radius = Mathf.Max(0f, value);
        }

        public float StartAngle
        {
            get => startAngle;
            set => startAngle = value;
        }

        public float EndAngle
        {
            get => endAngle;
            set => endAngle = value;
        }

        public bool Clockwise
        {
            get => clockwise;
            set => clockwise = value;
        }

        public bool FillFullCircle
        {
            get => fillFullCircle;
            set => fillFullCircle = value;
        }

        public bool RotateToCenter
        {
            get => rotateToCenter;
            set => rotateToCenter = value;
        }

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
            CollectChildren();
        }

        public override void CalculateLayoutInputVertical()
        {
        }

        public override void SetLayoutHorizontal()
        {
            Apply();
        }

        public override void SetLayoutVertical()
        {
            Apply();
        }

        private void CollectChildren()
        {
            children.Clear();
            for (int i = 0; i < rectTransform.childCount; i++)
            {
                RectTransform child = rectTransform.GetChild(i) as RectTransform;
                if (child == null || !child.gameObject.activeInHierarchy) continue;
                children.Add(child);
            }
        }

        private void Apply()
        {
            CollectChildren();
            int count = children.Count;
            if (count == 0) return;

            Rect rect = rectTransform.rect;
            float effectiveRadius = radiusSource == RadiusSource.Fit
                ? Mathf.Max(0f, Mathf.Min(rect.width, rect.height) * 0.5f)
                : radius;

            for (int i = 0; i < count; i++)
            {
                RectTransform child = children[i];
                float t = count == 1 ? 0f : (fillFullCircle ? (float)i / count : (float)i / (count - 1));
                float span = fillFullCircle ? 360f : endAngle - startAngle;
                float angle = startAngle + span * t;
                if (!clockwise) angle = -angle;

                float radians = angle * Mathf.Deg2Rad;
                Vector2 offset = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * effectiveRadius;

                child.anchorMin = new Vector2(0.5f, 0.5f);
                child.anchorMax = new Vector2(0.5f, 0.5f);
                child.anchoredPosition = offset;

                if (rotateToCenter)
                {
                    float face = angle - 90f;
                    child.localRotation = Quaternion.Euler(0f, 0f, face);
                }
            }
        }
    }
}
