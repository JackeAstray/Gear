using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gear.Utilities.UI.Motion
{
    /// <summary>
    /// 分布条：按权重把子项宽度铺满一行（分段进度、容量分布、资源占比）。
    /// <para>
    /// 只改子项 `sizeDelta.x` 与 `anchoredPosition.x`，不建图元、不参与合批排序；
    /// 子项可以是 ImageEx / Image / 任意 RectTransform。
    /// </para>
    /// </summary>
    [AddComponentMenu("UI/Gear/Distribution Bar")]
    [DisallowMultipleComponent]
    public sealed class GearDistributionBar : MonoBehaviour
    {
        [SerializeField] private float spacing;
        [SerializeField, Min(0f)] private float minSegmentWidth;
        [SerializeField] private bool useRectWidth = true;
        [SerializeField] private float totalWidth = 400f;

        private readonly List<RectTransform> segments = new List<RectTransform>();
        private float[] weights = Array.Empty<float>();

        /// <summary>当前参与分布的子项。</summary>
        public IReadOnlyList<RectTransform> Segments => segments;

        /// <summary>设置权重并立即重排。</summary>
        public void SetWeights(params float[] values)
        {
            weights = values ?? Array.Empty<float>();
            Refresh();
        }

        /// <summary>设置权重并立即重排。</summary>
        public void SetWeights(IList<float> values)
        {
            if (values == null)
            {
                weights = Array.Empty<float>();
            }
            else
            {
                weights = new float[values.Count];
                for (int i = 0; i < values.Count; i++) weights[i] = Mathf.Max(0f, values[i]);
            }
            Refresh();
        }

        /// <summary>按当前宽度与权重重新分配。</summary>
        public void Refresh()
        {
            Collect();
            int count = segments.Count;
            if (count == 0) return;

            float available = (useRectWidth ? ((RectTransform)transform).rect.width : totalWidth)
                - spacing * Mathf.Max(0, count - 1);

            float minTotal = minSegmentWidth * count;
            if (available < minTotal)
                available = minTotal;

            float sum = 0f;
            for (int i = 0; i < count; i++) sum += WeightAt(i);
            if (sum <= 0.0001f) sum = count;

            float cursor = -available * 0.5f;
            for (int i = 0; i < count; i++)
            {
                RectTransform segment = segments[i];
                if (segment == null) continue;

                float share = WeightAt(i) / sum;
                float width = Mathf.Max(minSegmentWidth, available * share);

                segment.sizeDelta = new Vector2(width, segment.sizeDelta.y);
                segment.anchoredPosition = new Vector2(cursor + width * 0.5f, segment.anchoredPosition.y);
                cursor += width + spacing;
            }
        }

        private float WeightAt(int index)
        {
            if (index >= weights.Length) return 1f;
            return Mathf.Max(0f, weights[index]);
        }

        private void Collect()
        {
            segments.Clear();
            for (int i = 0; i < transform.childCount; i++)
            {
                RectTransform child = transform.GetChild(i) as RectTransform;
                if (child == null || !child.gameObject.activeInHierarchy) continue;
                segments.Add(child);
            }
        }
    }
}
