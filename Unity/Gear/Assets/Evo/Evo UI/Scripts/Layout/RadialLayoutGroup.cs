using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI
{
    /// <summary>
    /// Arranges child centers on a circle fitted to the padded RectTransform.
    /// Angles increase from local right in the selected direction, with StartAngle <= EndAngle.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Evo/UI/Layout/Radial Layout Group"), HelpURL(Constants.HelpUrl + "layout/radial-layout-group")]
    public class RadialLayoutGroup : LayoutGroup
    {
        // Angle Range
        [SerializeField, Range(0f, 360f), Tooltip("The first child's angle, measured in the selected direction.")] 
        private float startAngle = 0f;
        [SerializeField, Range(0f, 360f), Tooltip("0 to 360 is a full circle without a duplicate endpoint. Equal angles overlap.")]
        private float endAngle = 360f;
        [SerializeField, Tooltip("Angles increase from the right in this direction. 0 to 90 is always a quarter circle.")] 
        private bool clockwise = true;

        // Distribution
        [SerializeField] private bool evenDistribution = true;
        [SerializeField, Tooltip("Angular spacing in degrees. Negative values reverse the selected direction.")]
        private float customSpacing = 10f;

        // Child Control
        [SerializeField, Tooltip("Points each child's local up axis toward the center. Otherwise, rotation is reset.")]
        private bool faceCenter = false;
        [SerializeField] private bool controlChildSize = true;
        [SerializeField] private Vector2 childSize = new(50f, 50f);


        public float Radius
        {
            get
            {
                Rect rect = rectTransform.rect;
                float availableWidth = Mathf.Max(0f, rect.width - padding.horizontal);
                float availableHeight = Mathf.Max(0f, rect.height - padding.vertical);
                return Mathf.Min(availableWidth, availableHeight) * 0.5f;
            }
        }

        public float StartAngle
        {
            get => startAngle;
            set => SetValue(ref startAngle, Mathf.Clamp(FiniteOrDefault(value), 0f, endAngle));
        }

        public float EndAngle
        {
            get => endAngle;
            set => SetValue(ref endAngle, Mathf.Clamp(FiniteOrDefault(value, 360f), startAngle, 360f));
        }

        public bool Clockwise
        {
            get => clockwise;
            set => SetValue(ref clockwise, value);
        }

        public bool EvenDistribution
        {
            get => evenDistribution;
            set => SetValue(ref evenDistribution, value);
        }

        public float CustomSpacing
        {
            get => customSpacing;
            set => SetValue(ref customSpacing, FiniteOrDefault(value));
        }

        public bool FaceCenter
        {
            get => faceCenter;
            set => SetValue(ref faceCenter, value);
        }

        public bool ControlChildSize
        {
            get => controlChildSize;
            set => SetValue(ref controlChildSize, value);
        }

        public Vector2 ChildSize
        {
            get => childSize;
            set => SetValue(ref childSize, Vector2.Max(Vector2.zero, SanitizeVector(value)));
        }

        // Anchored positions are relative to the parent's center, regardless of its pivot
        Vector2 Center => new((padding.left - padding.right) * 0.5f, (padding.bottom - padding.top) * 0.5f);

        protected override void OnDisable()
        {
            base.OnDisable();
            rectChildren.Clear();
        }

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
            CalculateLayoutInput(0);
        }

        public override void CalculateLayoutInputVertical() => CalculateLayoutInput(1);

        void CalculateLayoutInput(int axis)
        {
            float paddingSize = axis == 0 ? padding.horizontal : padding.vertical;
            bool hasChildren = rectChildren.Count > 0;
           
            // Keep the original nominal sizes independent of the current parent size.
            // Deriving preferred size from Radius would create a ContentSizeFitter feedback loop.
            SetLayoutInputForAxis(paddingSize + (hasChildren ? 100f : 0f),
#if UNITY_6000_6_OR_NEWER
                float.PositiveInfinity,
#endif
                paddingSize + (hasChildren ? 200f : 0f), 1f, axis);
        }

        public override void SetLayoutHorizontal()
        {
            DrivenTransformProperties drivenProperties = DrivenTransformProperties.Anchors |
                DrivenTransformProperties.Pivot | DrivenTransformProperties.AnchoredPosition |
                DrivenTransformProperties.Rotation;

            if (controlChildSize)
                drivenProperties |= DrivenTransformProperties.SizeDelta;

            Vector2 centerAnchor = new(0.5f, 0.5f);
            for (int i = 0; i < rectChildren.Count; i++)
            {
                RectTransform child = rectChildren[i];
                m_Tracker.Add(this, child, drivenProperties);

                // Capture the actual size before removing stretch anchors
                Vector2 size = controlChildSize ? childSize : child.rect.size;

                if (child.anchorMin != centerAnchor)
                    child.anchorMin = centerAnchor;
                if (child.anchorMax != centerAnchor)
                    child.anchorMax = centerAnchor;
                if (child.pivot != centerAnchor)
                    child.pivot = centerAnchor;
                if (child.sizeDelta != size)
                    child.sizeDelta = size;
            }
        }

        public override void SetLayoutVertical()
        {
            int childCount = rectChildren.Count;
            if (childCount == 0)
                return;

            // Like GridLayoutGroup, set sizes in the horizontal pass and positions in the vertical pass.
            // Both parent dimensions are now final, so calculate the circle once.
            Vector2 center = Center;
            float radius = Radius;
            float firstAngle = clockwise ? -startAngle : startAngle;
            float angleStep = GetAngleStep(childCount);

            for (int i = 0; i < childCount; i++)
            {
                RectTransform child = rectChildren[i];
                float angle = Mathf.Repeat(firstAngle + angleStep * i, 360f);
                Vector2 position = center + GetDirection(angle) * radius;
                Quaternion rotation = faceCenter ? Quaternion.Euler(0f, 0f, angle + 90f) : Quaternion.identity;

                if (child.anchoredPosition != position)
                    child.anchoredPosition = position;
                if (child.localRotation != rotation)
                    child.localRotation = rotation;
            }
        }

        float GetAngleStep(int childCount)
        {
            if (childCount <= 1)
                return 0f;

            if (!evenDistribution)
                return (customSpacing % 360f) * (clockwise ? -1f : 1f);

            float sweep = GetSweep();
            int intervals = Mathf.Abs(sweep) >= 360f ? childCount : childCount - 1;
            return sweep / intervals;
        }

        float GetSweep() => (endAngle - startAngle) * (clockwise ? -1f : 1f);

        static Vector2 GetDirection(float angle)
        {
            float radians = angle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        void SetValue(ref float currentValue, float value)
        {
            if (currentValue.Equals(value))
                return;

            currentValue = value;
            SetDirty();
        }

        void SetValue(ref bool currentValue, bool value)
        {
            if (currentValue == value)
                return;

            currentValue = value;
            SetDirty();
        }

        void SetValue(ref Vector2 currentValue, Vector2 value)
        {
            if (currentValue.Equals(value))
                return;

            currentValue = value;
            SetDirty();
        }

        static float FiniteOrDefault(float value, float fallback = 0f) => float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;

        static Vector2 SanitizeVector(Vector2 value) => new(FiniteOrDefault(value.x), FiniteOrDefault(value.y));

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            endAngle = Mathf.Clamp(FiniteOrDefault(endAngle, 360f), 0f, 360f);
            startAngle = Mathf.Clamp(FiniteOrDefault(startAngle), 0f, endAngle);
            customSpacing = FiniteOrDefault(customSpacing);
            childSize = Vector2.Max(Vector2.zero, SanitizeVector(childSize));
            base.OnValidate();
        }

        void OnDrawGizmosSelected()
        {
            if (!isActiveAndEnabled)
                return;

            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;
            Gizmos.matrix = rectTransform.localToWorldMatrix;

            try
            {
                Vector3 center = rectTransform.rect.center + Center;
                float radius = Radius;
                Vector3 previousPoint = center + Vector3.right * radius;

                Gizmos.color = Color.yellow;
                const int segments = 64;
                for (int i = 1; i <= segments; i++)
                {
                    Vector3 point = center + (Vector3)GetDirection(360f * i / segments) * radius;
                    Gizmos.DrawLine(previousPoint, point);
                    previousPoint = point;
                }

                float sweep = evenDistribution ? GetSweep() : GetAngleStep(rectChildren.Count) * Mathf.Max(0, rectChildren.Count - 1);
                float firstAngle = clockwise ? -startAngle : startAngle;
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(center, center + (Vector3)GetDirection(firstAngle) * radius);
                Gizmos.DrawLine(center, center + (Vector3)GetDirection(firstAngle + sweep) * radius);
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(center, 2f);
            }
            finally
            {
                Gizmos.matrix = previousMatrix;
                Gizmos.color = previousColor;
            }
        }
#endif
    }
}