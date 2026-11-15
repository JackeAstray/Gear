using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gear.Utilities.UI.Widgets
{
    /// <summary>
    /// 可重排列表：拖动子项时实时换 sibling index，支持事件、CanvasGroup 拖拽透明和可选手动排布。
    /// </summary>
    [AddComponentMenu("UI/Gear/Reorderable List")]
    [DisallowMultipleComponent]
    public sealed class GearReorderableList : MonoBehaviour
    {
        [SerializeField] private RectTransform content;
        [SerializeField] private List<RectTransform> items = new List<RectTransform>();
        [SerializeField] private bool vertical = true;
        [SerializeField] private bool manualLayout = true;
        [SerializeField, Min(0f)] private float gap = 8f;
        [SerializeField, Range(0f, 1f)] private float dragAlpha = 0.75f;
        [SerializeField] private UnityEvent<int, int> onReordered = new UnityEvent<int, int>();

        private RectTransform dragging;
        private int dragIndex;
        private CanvasGroup dragGroup;

        public IReadOnlyList<RectTransform> Items => items;
        public UnityEvent<int, int> OnReordered => onReordered;
        public int Count => items.Count;

        public void Configure(RectTransform contentRect, IList<RectTransform> source = null)
        {
            content = contentRect != null ? contentRect : content;
            items.Clear();
            if (source != null)
            {
                for (int i = 0; i < source.Count; i++)
                    if (source[i] != null) items.Add(source[i]);
            }
            else if (content != null)
            {
                for (int i = 0; i < content.childCount; i++)
                {
                    if (content.GetChild(i) is RectTransform rect) items.Add(rect);
                }
            }

            ApplyPositions();
        }

        /// <summary>把 from 移到 to；返回是否真的发生换位。</summary>
        public bool MoveItem(int from, int to)
        {
            if (items.Count < 2) return false;
            from = Mathf.Clamp(from, 0, items.Count - 1);
            to = Mathf.Clamp(to, 0, items.Count - 1);
            if (from == to) return false;

            RectTransform item = items[from];
            items.RemoveAt(from);
            items.Insert(to, item);
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null) items[i].SetSiblingIndex(i);
            }

            ApplyPositions();
            onReordered?.Invoke(from, to);
            return true;
        }

        public void BeginDrag(RectTransform item)
        {
            if (item == null || !items.Contains(item)) return;
            dragging = item;
            dragIndex = items.IndexOf(item);
            dragGroup = item.GetComponent<CanvasGroup>();
            if (dragGroup == null) dragGroup = item.gameObject.AddComponent<CanvasGroup>();
            dragGroup.alpha = dragAlpha;
            dragGroup.blocksRaycasts = false;
        }

        public void DragTo(PointerEventData eventData)
        {
            if (dragging == null || eventData == null || content == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    content, eventData.position, eventData.pressEventCamera, out Vector2 local))
            {
                return;
            }

            int target = FindClosestIndex(local);
            if (target == dragIndex) return;
            if (MoveItem(dragIndex, target)) dragIndex = target;
        }

        public void EndDrag()
        {
            if (dragGroup != null)
            {
                dragGroup.alpha = 1f;
                dragGroup.blocksRaycasts = true;
            }

            dragging = null;
            dragGroup = null;
        }

        private int FindClosestIndex(Vector2 local)
        {
            int best = dragIndex;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null) continue;
                Vector2 center = items[i].anchoredPosition;
                if (vertical) center.x = 0f;
                else center.y = 0f;
                Vector2 probe = local;
                if (vertical) probe.x = 0f;
                else probe.y = 0f;
                float distance = (center - probe).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            return best;
        }

        private void ApplyPositions()
        {
            if (!manualLayout || content == null) return;
            float cursor = 0f;
            for (int i = 0; i < items.Count; i++)
            {
                RectTransform item = items[i];
                if (item == null) continue;
                float size = vertical ? item.rect.height : item.rect.width;
                item.anchorMin = new Vector2(0f, 1f);
                item.anchorMax = new Vector2(0f, 1f);
                item.pivot = new Vector2(0f, 1f);
                item.anchoredPosition = vertical ? new Vector2(0f, -cursor) : new Vector2(cursor, 0f);
                cursor += size + gap;
            }

            LayoutRebuilder.MarkLayoutForRebuild(content);
        }
    }
}
