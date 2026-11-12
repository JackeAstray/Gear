using System;
using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI
{
    public class CalendarDay : MonoBehaviour
    {
        [Header("References")]
        public Button button;
        [SerializeField] private GameObject disabledState;
        [SerializeField] private GameObject normalState;
        [SerializeField] private GameObject currentState;
        [SerializeField] private GameObject selectedState;
        [SerializeField, TextComponent] private Graphic[] labels;

        // Source is serialized so saved children can be reused after a domain reload or in a build
        [SerializeField, HideInInspector] private GameObject sourcePrefab;

        // Runtime binding
        Calendar owner;
        Button boundButton;

        internal GameObject SourcePrefab
        {
            get => sourcePrefab;
            set
            {
                if (sourcePrefab == value)
                    return;

                sourcePrefab = value;
#if UNITY_EDITOR
                Calendar.SaveEditorChanges(this);
#endif
            }
        }

        /// <summary>
        /// Stores the date this button represents.
        /// </summary>
        public DateTime Date { get; set; }

        public void SetLabel(string text)
        {
            if (button != null && button.Text != text)
                button.Text = text;

            if (labels == null)
                return;

            foreach (Graphic lbl in labels)
                TextProvider.UpdateText(lbl, text);
        }

        public void SetState(int index)
        {
            SetStateActive(disabledState, index == 0);
            SetStateActive(normalState, index == 1);
            SetStateActive(currentState, index == 2);
            SetStateActive(selectedState, index == 3);
        }

        void SetStateActive(GameObject state, bool value)
        {
            // State visuals must not disable the day root or objects outside this day
            if (state == null || state == gameObject || !state.transform.IsChildOf(transform) || state.activeSelf == value)
                return;

            state.SetActive(value);
        }

        internal bool IsBoundTo(Calendar calendar) => owner == calendar && boundButton == button;

        internal void Bind(Calendar calendar)
        {
            if (IsBoundTo(calendar))
                return;
            if (boundButton != null)
                boundButton.onClick.RemoveListener(OnClick);

            owner = calendar;
            boundButton = button;

            if (boundButton != null)
                boundButton.onClick.AddListener(OnClick);
        }

        internal void Unbind(Calendar calendar)
        {
            if (owner != calendar)
                return;
            if (boundButton != null)
                boundButton.onClick.RemoveListener(OnClick);

            boundButton = null;
            owner = null;
        }

        void OnClick()
        {
            if (owner != null)
                owner.OnDayClicked(this);
        }

        void OnDestroy() => Unbind(owner);
    }
}