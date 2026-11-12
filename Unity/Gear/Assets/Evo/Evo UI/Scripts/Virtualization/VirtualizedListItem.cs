using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Evo.UI
{
    /// <summary>
    /// Routes navigation through data indices and protects pressed/selected views from recycling.
    /// Created automatically on each view and its interactive descendants.
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public class VirtualizedListItem : MonoBehaviour, IMoveHandler, IPointerDownHandler, IPointerUpHandler, 
        ISelectHandler, IDeselectHandler
    {
        VirtualizedList owner;
        Action onInteractionChanged;
        VirtualizedListItem root;
        List<PointerEventData> presses;
        VirtualizedListItem[] interactions;
        Selectable[] selectables;
        ScrollRect.ItemData itemState;
        Selectable selectable;
        Navigation originalNavigation;
        bool navigationCached;
        bool navigationEnabled;
        int index = -1;

        internal static GameObject SelectedObject => EventSystem.current != null 
            ? EventSystem.current.currentSelectedGameObject 
            : null;

        internal bool IsPressed => presses != null && presses.Count > 0;

        internal bool IsInUse
        {
            get
            {
                GameObject selected = SelectedObject;
                return IsPressed || (selected != null && selected.transform.IsChildOf(transform));
            }
        }

        void OnDisable() => CancelPress();
      
        void OnDestroy() => InvalidateVisible();
      
        public void OnSelect(BaseEventData eventData) => InvalidateVisible();
       
        public void OnDeselect(BaseEventData eventData) => InvalidateVisible();

        void InvalidateVisible()
        {
            owner?.InvalidateVisible();
            onInteractionChanged?.Invoke();
        }

        public void OnMove(AxisEventData eventData)
        {
            if (eventData.used || !navigationEnabled || index < 0 || owner == null || owner.IsDisposed)
                return;

            int step = owner.IsVertical
                ? eventData.moveDir == MoveDirection.Down ? 1 : eventData.moveDir == MoveDirection.Up ? -1 : 0
                : eventData.moveDir == MoveDirection.Right ? 1 : eventData.moveDir == MoveDirection.Left ? -1 : 0;
           
            if (step == 0)
            {
                if (selectable != null && navigationCached)
                {
                    selectable.navigation = originalNavigation;
                   
                    try { selectable.OnMove(eventData); }
                    finally
                    {
                        if (selectable != null && navigationEnabled)
                            selectable.navigation = new Navigation { mode = Navigation.Mode.Explicit };
                    }
                }
                return;
            }

            // Selectable's own OnMove is suppressed: physical sibling order changes during recycling
            VirtualizedList list = owner;
            int next = index;
            for (int i = 0; i < list.ItemCount; i++)
            {
                next += step;
             
                if (list.LoopNavigation)
                    next = next < 0 ? list.ItemCount - 1 : next >= list.ItemCount ? 0 : next;
                else if (next < 0 || next >= list.ItemCount)
                    return;

                list.EnsureVisible(next, true);
              
                VirtualizedListItem candidate = list.GetInteraction(next);
                if (candidate == null)
                    continue;
             
                Selectable target = candidate.itemState.GetNavigationTarget(candidate.selectables);
                if (target == null)
                    continue;

                if (list.ScrollRect.enableSnapping && !list.ScrollRect.EnableSmoothScroll)
                    list.ScrollRect.SnapToElementInstant(next);
               
                if (list.IsDisposed || target == null || !target.isActiveAndEnabled)
                    return;
              
                eventData.selectedObject = target.gameObject;
                eventData.Use();
                return;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            VirtualizedListItem target = root != null ? root : this;
            target.presses ??= new List<PointerEventData>(1);
            if (!target.presses.Contains(eventData))
                target.presses.Add(eventData);
          
            InvalidateVisible();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            VirtualizedListItem target = root != null ? root : this;
            target.presses?.Remove(eventData);
            InvalidateVisible();
        }

        internal void Initialize(VirtualizedList list, int dataIndex, bool enableNavigation, ScrollRect.ItemData state,
            Action onInteractionChanged = null)
        {
            itemState = state;

            if (interactions == null)
            {
                // Pointer events stop at the first handler in the hierarchy. Cover custom click targets too.
                foreach (MonoBehaviour handler in GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if ((handler is Selectable || handler is IPointerDownHandler || handler is IPointerClickHandler)
                        && !handler.TryGetComponent<VirtualizedListItem>(out _))
                        handler.gameObject.AddComponent<VirtualizedListItem>();
                }

                selectables = GetComponentsInChildren<Selectable>(true);
                interactions = GetComponentsInChildren<VirtualizedListItem>(true);
            }

            foreach (VirtualizedListItem interaction in interactions)
            {
                if (interaction == null)
                    continue;
              
                interaction.owner = list;
                interaction.onInteractionChanged = onInteractionChanged;
                interaction.index = dataIndex;
                interaction.root = this;
                interaction.navigationEnabled = enableNavigation;
               
                if (interaction.selectable == null && interaction.TryGetComponent(out interaction.selectable))
                {
                    interaction.originalNavigation = interaction.selectable.navigation;
                    interaction.navigationCached = true;
                }

                if (interaction.selectable != null && interaction.navigationCached)
                {
                    interaction.selectable.navigation = enableNavigation
                        ? new Navigation { mode = Navigation.Mode.Explicit } 
                        : interaction.originalNavigation;
                }
            }
        }

        internal void Clear()
        {
            CancelPress();

            GameObject selected = SelectedObject;
            if (selected != null && selected.transform.IsChildOf(transform))
                EventSystem.current.SetSelectedGameObject(null);

            if (interactions == null)
                return;
         
            foreach (VirtualizedListItem interaction in interactions)
            {
                if (interaction == null)
                    continue;
               
                interaction.owner = null;
                interaction.onInteractionChanged = null;
                interaction.index = -1;
                interaction.navigationEnabled = false;
                
                if (interaction.navigationCached && interaction.selectable != null)
                    interaction.selectable.navigation = interaction.originalNavigation;
            }
        }

        internal void CancelPress()
        {
            VirtualizedListItem target = root != null ? root : this;
            if (target.presses != null)
            {
                foreach (PointerEventData press in target.presses)
                    press.eligibleForClick = false;

                target.presses.Clear();
            }

            InvalidateVisible();
        }
    }
}