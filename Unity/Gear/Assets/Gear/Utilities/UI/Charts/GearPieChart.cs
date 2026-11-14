using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gear.Utilities.UI.Charts
{
    /// <summary>轻量饼图/环形图：自绘扇区，支持 hover 高亮与内半径（环形）。</summary>
    [AddComponentMenu("UI/Gear/Charts/Pie Chart")]
    public sealed class GearPieChart : MaskableGraphic, IPointerMoveHandler, IPointerExitHandler, IPointerClickHandler
    {
        [SerializeField] private float[] values = { 0.5f, 0.3f, 0.2f };
        [SerializeField] private Color[] colors =
        {
            new Color(0.49f, 0.36f, 0.99f, 1f),
            new Color(0.14f, 0.75f, 0.95f, 1f),
            new Color(0.25f, 0.78f, 0.52f, 1f),
        };
        [SerializeField] private float startAngle = 90f;
        [SerializeField, Range(0f, 0.95f)] private float innerRadius = 0.45f;
        [SerializeField, Min(2)] private int segmentsPerSlice = 24;
        [SerializeField, Min(0f)] private float gapDegrees = 1.5f;

        private readonly List<float> sliceStarts = new List<float>();
        private readonly List<float> sliceSweeps = new List<float>();
        private readonly List<Color32> sliceColors = new List<Color32>();

        public int SliceCount => sliceStarts.Count;
        public IReadOnlyList<float> SliceStarts => sliceStarts;
        public IReadOnlyList<float> SliceSweeps => sliceSweeps;
        public int HoveredIndex { get; private set; } = -1;
        public event Action<int> HoveredChanged;
        public event Action<int> SliceClicked;

        public void SetData(IList<float> data, IList<Color> palette = null)
        {
            if (data == null)
            {
                values = null;
            }
            else
            {
                values = new float[data.Count];
                for (int i = 0; i < data.Count; i++) values[i] = data[i];
            }

            if (palette != null)
            {
                colors = new Color[palette.Count];
                for (int i = 0; i < palette.Count; i++) colors[i] = palette[i];
            }

            RebuildSlices();
            SetVerticesDirty();
        }

        public void SetInnerRadius(float value)
        {
            innerRadius = Mathf.Clamp(value, 0f, 0.95f);
            SetVerticesDirty();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            RebuildSlices();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (sliceStarts.Count == 0) return;

            Rect rect = GetPixelAdjustedRect();
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            if (radius <= 0f) return;

            Vector2 center = rect.center;
            float inner = radius * innerRadius;

            for (int i = 0; i < sliceStarts.Count; i++)
            {
                float start = sliceStarts[i];
                float sweep = sliceSweeps[i];
                if (sweep <= 0f) continue;

                int segments = Mathf.Max(2, Mathf.CeilToInt(segmentsPerSlice * (sweep / 360f)));
                float sliceRadius = i == HoveredIndex ? radius * 1.04f : radius;
                Color32 color = sliceColors[i];
                float step = sweep / segments;

                for (int s = 0; s < segments; s++)
                {
                    float a0 = start + s * step;
                    float a1 = start + (s + 1) * step;
                    Vector2 outer0 = center + Direction(a0) * sliceRadius;
                    Vector2 outer1 = center + Direction(a1) * sliceRadius;

                    if (inner <= 0.001f)
                    {
                        AddTriangle(vh, center, outer0, outer1, color);
                    }
                    else
                    {
                        Vector2 inner0 = center + Direction(a0) * inner;
                        Vector2 inner1 = center + Direction(a1) * inner;
                        AddQuad(vh, inner0, inner1, outer1, outer0, color);
                    }
                }
            }
        }

        private void RebuildSlices()
        {
            sliceStarts.Clear();
            sliceSweeps.Clear();
            sliceColors.Clear();
            if (values == null || values.Length == 0) return;

            float total = 0f;
            for (int i = 0; i < values.Length; i++) total += Mathf.Max(0f, values[i]);
            if (total <= 0f) return;

            float angle = startAngle;
            for (int i = 0; i < values.Length; i++)
            {
                float sweep = Mathf.Max(0f, values[i]) / total * 360f;
                float drawSweep = Mathf.Max(0f, sweep - gapDegrees);
                sliceStarts.Add(angle);
                sliceSweeps.Add(drawSweep);

                Color color = colors != null && colors.Length > 0
                    ? colors[i % colors.Length]
                    : new Color(0.49f, 0.36f, 0.99f, 1f);
                if (i == HoveredIndex) color = Color.Lerp(color, Color.white, 0.25f);
                sliceColors.Add(color);

                angle += sweep;
            }
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (TryGetSliceAtScreenPosition(eventData, out int hovered))
                SetHovered(hovered);
        }

        public void OnPointerExit(PointerEventData eventData) => SetHovered(-1);

        public void OnPointerClick(PointerEventData eventData)
        {
            if (TryGetSliceAtScreenPosition(eventData, out int index))
                SliceClicked?.Invoke(index);
        }

        /// <summary>按本地坐标返回扇区索引；不在扇区内返回 -1。便于测试与外部 hit-test。</summary>
        public int GetSliceIndexAtLocalPoint(Vector2 local)
        {
            if (sliceStarts.Count == 0) return -1;

            Rect rect = GetPixelAdjustedRect();
            Vector2 delta = local - rect.center;
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            float distance = delta.magnitude;
            if (radius <= 0f || distance > radius || distance < radius * innerRadius)
                return -1;

            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;

            for (int i = 0; i < sliceStarts.Count; i++)
            {
                float start = Normalize(sliceStarts[i]);
                float localAngle = Normalize(angle - start);
                if (localAngle <= sliceSweeps[i])
                    return i;
            }

            return -1;
        }

        private bool TryGetSliceAtScreenPosition(PointerEventData eventData, out int index)
        {
            index = -1;
            if (sliceStarts.Count == 0 || eventData == null) return false;

            RectTransform rectTransform = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 local))
                return false;

            index = GetSliceIndexAtLocalPoint(local);
            return index >= 0;
        }

        private void SetHovered(int index)
        {
            if (HoveredIndex == index) return;
            HoveredIndex = index;
            RebuildSlices();
            SetVerticesDirty();
            HoveredChanged?.Invoke(index);
        }

        private static float Normalize(float angle)
        {
            angle %= 360f;
            return angle < 0f ? angle + 360f : angle;
        }

        private static Vector2 Direction(float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }

        private static void AddTriangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color32 color)
        {
            int start = vh.currentVertCount;
            vh.AddVert(a, color, Vector2.zero);
            vh.AddVert(b, color, Vector2.up);
            vh.AddVert(c, color, Vector2.one);
            vh.AddTriangle(start, start + 1, start + 2);
        }

        private static void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 color)
        {
            int start = vh.currentVertCount;
            vh.AddVert(a, color, Vector2.zero);
            vh.AddVert(b, color, Vector2.up);
            vh.AddVert(c, color, Vector2.one);
            vh.AddVert(d, color, Vector2.right);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
