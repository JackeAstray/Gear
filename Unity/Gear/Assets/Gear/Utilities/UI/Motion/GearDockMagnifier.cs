using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Gear.Utilities.UI.Motion
{
    /// <summary>
    /// Dock 放大：指针靠近哪个子项，哪个子项就平滑放大（高斯权重 + SmoothDamp）。
    /// <para>
    /// 语义参考 Evo 的 DockMagnifier：改的是子项 sizeDelta（不是 scale），所以旁边的项会被布局推着让位；
    /// 常配 HorizontalLayoutGroup 使用。纯 C#，提供 Step/SetPointerLocal 便于测试与外部驱动。
    /// </para>
    /// </summary>
    [AddComponentMenu("UI/Gear/Dock Magnifier")]
    [DisallowMultipleComponent]
    public sealed class GearDockMagnifier : MonoBehaviour, IPointerMoveHandler, IPointerExitHandler
    {
        public enum DockOrientation
        {
            Free = 0,
            Horizontal = 1,
            Vertical = 2,
        }

        [SerializeField] private RectTransform content;
        [SerializeField] private DockOrientation orientation = DockOrientation.Horizontal;
        [SerializeField] private Vector2 baseSize = new Vector2(96f, 96f);
        [SerializeField] private Vector2 maxSize = new Vector2(132f, 132f);
        [SerializeField, Min(1f)] private float influenceRadius = 160f;
        [SerializeField, Min(0.001f)] private float smoothTime = 0.07f;
        [SerializeField] private bool useUnscaledTime = true;

        private sealed class Entry
        {
            public RectTransform Rect;
            public Vector2 Velocity;
        }

        private readonly List<Entry> entries = new List<Entry>();
        private int cachedChildCount = -1;
        private Vector2 pointerLocal;
        private bool hasPointer;

        private RectTransform Content
        {
            get
            {
                if (content != null) return content;
                return (RectTransform)transform;
            }
        }

        /// <summary>未放大时的子项尺寸。</summary>
        public Vector2 BaseSize
        {
            get => baseSize;
            set => baseSize = value;
        }

        /// <summary>放大后的子项尺寸。</summary>
        public Vector2 MaxSize
        {
            get => maxSize;
            set => maxSize = value;
        }

        /// <summary>影响半径（像素），越大越“一锅端”。</summary>
        public float InfluenceRadius
        {
            get => influenceRadius;
            set => influenceRadius = Mathf.Max(1f, value);
        }

        private void Update()
        {
            if (!hasPointer) return;
            Step(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);
        }

        /// <summary>指针在 content 本地空间的位置（像素）。</summary>
        public void SetPointerLocal(Vector2 local)
        {
            pointerLocal = local;
            hasPointer = true;
        }

        /// <summary>指针移出：所有项平滑回到底尺寸。</summary>
        public void ClearPointer()
        {
            hasPointer = false;
        }

        /// <summary>手动推进平滑（测试 / 外部驱动用）。</summary>
        public void Step(float delta)
        {
            Cache();
            RectTransform root = Content;
            if (root == null) return;

            float sigma = Mathf.Max(0.001f, influenceRadius * 0.5f);
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (entry.Rect == null) continue;

                Vector2 target = baseSize;
                if (hasPointer)
                {
                    Vector2 center = root.InverseTransformPoint(entry.Rect.TransformPoint(entry.Rect.rect.center));
                    float weight = EvaluateWeight(Vector2.Distance(pointerLocal, center), sigma);
                    if (orientation == DockOrientation.Horizontal)
                        target = new Vector2(Mathf.Lerp(baseSize.x, maxSize.x, weight), baseSize.y);
                    else if (orientation == DockOrientation.Vertical)
                        target = new Vector2(baseSize.x, Mathf.Lerp(baseSize.y, maxSize.y, weight));
                    else
                        target = new Vector2(
                            Mathf.Lerp(baseSize.x, maxSize.x, weight),
                            Mathf.Lerp(baseSize.y, maxSize.y, weight));
                }

                Vector2 current = entry.Rect.sizeDelta;
                float x = Mathf.SmoothDamp(current.x, target.x, ref entry.Velocity.x, smoothTime, Mathf.Infinity, delta);
                float y = Mathf.SmoothDamp(current.y, target.y, ref entry.Velocity.y, smoothTime, Mathf.Infinity, delta);
                entry.Rect.sizeDelta = new Vector2(x, y);
            }
        }

        /// <summary>高斯权重：0 距离=1，越远越接近 0。</summary>
        public float EvaluateWeight(float distance, float sigma)
        {
            sigma = Mathf.Max(0.001f, sigma);
            return Mathf.Exp(-(distance * distance) / (2f * sigma * sigma));
        }

        /// <summary>所有项立即回到底尺寸。</summary>
        public void ResetToBase()
        {
            Cache();
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (entry.Rect == null) continue;
                entry.Rect.sizeDelta = baseSize;
                entry.Velocity = Vector2.zero;
            }
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            RectTransform root = Content;
            if (root == null || eventData == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, eventData.position, eventData.pressEventCamera, out Vector2 local))
                return;

            SetPointerLocal(local);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ClearPointer();
        }

        private void Cache()
        {
            RectTransform root = Content;
            if (root == null) return;

            int childCount = root.childCount;
            if (cachedChildCount == childCount) return;

            entries.Clear();
            for (int i = 0; i < childCount; i++)
            {
                RectTransform child = root.GetChild(i) as RectTransform;
                if (child == null) continue;
                entries.Add(new Entry { Rect = child });
            }

            cachedChildCount = childCount;
        }
    }
}
