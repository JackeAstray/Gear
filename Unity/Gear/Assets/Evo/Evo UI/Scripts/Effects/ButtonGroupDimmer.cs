using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Evo.UI
{
    /// <summary>
    /// Manages a group of buttons and smoothly dims the non-hovered ones.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Evo/UI/Effects/Button Group Dimmer"), HelpURL(Constants.HelpUrl)]
    public class ButtonGroupDimmer : MonoBehaviour
    {
        // Cache
        Button currentButton;
        readonly List<Element> elements = new();

        sealed class Element
        {
            public Button button;
            public UnityAction pointerEnter;
            public UnityAction pointerExit;
            public bool dimmed;
        }

        void Start() => RebuildCache();

        void OnDisable() => RestoreButtons();

        void OnDestroy() => ClearCache();

        void ClearCache()
        {
            for (int i = 0; i < elements.Count; i++)
            {
                Element element = elements[i];
                if (element.button == null)
                    continue;

                element.button.onPointerEnter.RemoveListener(element.pointerEnter);
                element.button.onPointerExit.RemoveListener(element.pointerExit);
            }

            elements.Clear();
        }

        void SetToDim(Button sourceButton)
        {
            if (!isActiveAndEnabled || sourceButton == null || !sourceButton.isActiveAndEnabled || !sourceButton.IsInteractable())
                return;

            currentButton = sourceButton;

            for (int i = 0; i < elements.Count; i++)
            {
                Element element = elements[i];
                Button button = element.button;
                if (button == null)
                    continue;

                if (button == sourceButton || button.InteractionState == InteractionState.Selected)
                {
                    if (element.dimmed)
                    {
                        element.dimmed = false;
                        button.interactable = true;
                    }
                    continue;
                }

                // Only restore buttons that this component actually disabled
                if (!button.isActiveAndEnabled || !button.IsInteractable())
                    continue;

                element.dimmed = true;
                button.SetInteractable(false);
            }
        }

        void SetToNormal(Button sourceButton)
        {
            if (!isActiveAndEnabled || currentButton == null || currentButton != sourceButton)
                return;

            // Restore immediately so the next button can receive pointer enter
            RestoreButtons();
        }

        void RestoreButtons()
        {
            currentButton = null;

            for (int i = 0; i < elements.Count; i++)
            {
                Element element = elements[i];
                if (!element.dimmed)
                    continue;

                element.dimmed = false;
                Button button = element.button;
                if (button == null)
                    continue;

                button.interactable = true;

                // Preserve selection, hover, and CanvasGroup restrictions
                if (button.InteractionState == InteractionState.Disabled && button.IsInteractable())
                    button.SetState(InteractionState.Normal);
            }
        }

        /// <summary>
        /// Restores dimmed buttons and rebuilds the cache after adding or removing buttons at runtime.
        /// Allocates new listener registrations; call only when the group changes.
        /// </summary>
        public void RebuildCache()
        {
            RestoreButtons();
            ClearCache();

            Transform groupTransform = transform;
            int childCount = groupTransform.childCount;
            if (elements.Capacity < childCount)
                elements.Capacity = childCount;

            for (int i = 0; i < childCount; i++)
            {
                if (!groupTransform.GetChild(i).TryGetComponent<Button>(out var btn))
                    continue;

                var element = new Element
                {
                    button = btn,
                    pointerEnter = () => SetToDim(btn),
                    pointerExit = () => SetToNormal(btn)
                };
                elements.Add(element);

                btn.onPointerEnter.AddListener(element.pointerEnter);
                btn.onPointerExit.AddListener(element.pointerExit);
            }
        }
    }
}