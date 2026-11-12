using System.Collections.Generic;
using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Scroll Rect tab: a picker, as a vertical wheel and a horizontal strip over the same items; Direction shows one of them.
    /// Every option applies to both. On Item Focused lights the item that settles in the band.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoScrollRect : MonoBehaviour
    {
        [SerializeField] private ScrollRect[] views;
        [Tooltip("One per view, shown by Direction: the view and its scrollbar.")]
        [SerializeField] private GameObject[] directions;
        [Tooltip("One per view, in the same order.")]
        [SerializeField] private Scrollbar[] scrollbars;

        [Header("Options")]
        [SerializeField] private CanvasGroup snapTimeOption;

        int[] focused;
        readonly List<DemoScrollItem> items = new();

        void Awake()
        {
            focused = new int[views.Length];
            for (int i = 0; i < views.Length; i++)
            {
                int view = i;
                views[i].onItemFocused.AddListener(index =>
                {
                    focused[view] = index;
                    Highlight(views[view], index);
                });
            }
        }

        // Infinite scrolling adds copies of the items, and each copy keeps its item's index
        void Highlight(ScrollRect view, int index)
        {
            view.content.GetComponentsInChildren(true, items);
            foreach (DemoScrollItem item in items)
                item.SetFocused(item.Index == index);
        }

        public void SetDirection(int index)
        {
            for (int i = 0; i < directions.Length; i++)
                directions[i].SetActive(i == index);
        }

        public void SetSmoothWheel(bool value)
        {
            foreach (ScrollRect view in views)
                view.EnableSmoothScroll = value;
        }

        public void SetInfinite(bool value)
        {
            foreach (ScrollRect view in views)
                view.EnableInfiniteScroll = value;
        }

        public void SetSnapping(bool value)
        {
            // Turning snapping on settles the view on the item it already focuses
            for (int i = 0; i < views.Length; i++)
            {
                views[i].enableSnapping = value;
                if (value && views[i].isActiveAndEnabled)
                    views[i].SnapToElement(focused[i]);
            }

            DemoUtils.SetLocked(snapTimeOption, !value);
        }

        // A Scroll Rect turns a Permanent scrollbar back on, so a hidden one is detached from its view
        public void SetScrollbar(bool value)
        {
            for (int i = 0; i < views.Length; i++)
            {
                if (views[i].horizontal) { views[i].horizontalScrollbar = value ? scrollbars[i] : null; }
                else { views[i].verticalScrollbar = value ? scrollbars[i] : null; }

                scrollbars[i].gameObject.SetActive(value);
            }
        }

        public void SetSnapTime(float value)
        {
            foreach (ScrollRect view in views)
                view.snapDuration = value;
        }

        public void SetMinScale(float percent)
        {
            foreach (ScrollRect view in views)
                view.minScale = percent / 100f;
        }

        public void SetMinOpacity(float percent)
        {
            foreach (ScrollRect view in views)
                view.minAlpha = percent / 100f;
        }
    }
}
