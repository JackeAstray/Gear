using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Reorderable List tab: a squad and a bench in the same transfer group. Drag a player to reorder,
    /// or across to the other list while Transfer is on. Each list holds as many rows as its panel fits;
    /// a full one leaves the group while the other list drags, so a drop there goes back.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoReorderableList : MonoBehaviour
    {
        [SerializeField] private ReorderableList[] lists;
        [Tooltip("One row of capacity dots per list, in the same order.")]
        [SerializeField] private DemoDotRow[] capacityDots;
        [SerializeField] private int capacity = 5;

        [Header("Options")]
        [SerializeField] private CanvasGroup durationOption;

        string group;
        string fullGroup;
        bool dragging;

        void Awake()
        {
            group = lists[0].transferGroup;
            fullGroup = group + " (Full)";
            foreach (ReorderableList list in lists)
                list.onListChanged.AddListener((item, from, to) => UpdateCounts());

            UpdateCounts();
        }

        void LateUpdate()
        {
            bool active = false;
            foreach (ReorderableList list in lists)
                active |= list.IsDraggingActive;

            if (active == dragging)
                return;

            dragging = active;
            foreach (ReorderableList list in lists)
                list.transferGroup = dragging && !list.IsDraggingActive && Count(list) >= capacity ? fullGroup : group;
        }

        // A finished drag's placeholder is still a child for this frame, but already inactive
        static int Count(ReorderableList list)
        {
            int count = 0;
            Transform container = list.listContainer;
            for (int i = 0; i < container.childCount; i++)
            {
                if (container.GetChild(i).gameObject.activeSelf)
                    count++;
            }

            return count;
        }

        void UpdateCounts()
        {
            for (int i = 0; i < lists.Length; i++)
                DemoUtils.SetDots(capacityDots[i], Count(lists[i]));
        }

        public void SetDragScale(float value)
        {
            foreach (ReorderableList list in lists)
                list.dragScale = value;
        }

        public void SetDragOpacity(float percent)
        {
            foreach (ReorderableList list in lists)
                list.dragAlpha = percent / 100f;
        }

        public void SetDuration(float value)
        {
            foreach (ReorderableList list in lists)
                list.animationDuration = value;
        }

        public void SetInstantSnap(bool value)
        {
            foreach (ReorderableList list in lists)
                list.instantSnap = value;

            DemoUtils.SetLocked(durationOption, value);
        }

        public void SetTransfer(bool value)
        {
            foreach (ReorderableList list in lists)
                list.allowListTransfer = value;
        }
    }
}
