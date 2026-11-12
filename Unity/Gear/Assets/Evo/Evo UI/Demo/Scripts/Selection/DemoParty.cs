using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Evo.Tween;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Dropdown (Input) tab: a four-seat party. Picking a name seats that player and clears the field,
    /// and invited names leave the list until their seat is clicked. The field locks while the party is full.
    /// Each seat is an Interactive that shows its remove mark while hovered; it's only interactable while taken.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoParty : MonoBehaviour
    {
        [Serializable]
        class Seat
        {
            public Interactive button;
            public GameObject empty;
            public GameObject member;
            public RectTransform avatar;
            public TMP_Text initial;
            public TMP_Text memberName;
        }

        [SerializeField] private Dropdown dropdown;
        [SerializeField] private DemoDropdown list;
        [SerializeField] private TMP_InputField field;
        [SerializeField] private TMP_Text placeholder;
        [SerializeField] private CanvasGroup fieldGroup;
        [SerializeField] private Seat[] seats;
        [SerializeField] private TMP_Text count;
        [SerializeField] private CanvasGroup startReady;
        [SerializeField] private string fullText = "Party is full";
        [Tooltip("Players seated when the tab first opens, by their place in the list.")]
        [SerializeField] private int[] invited = { 2 };

        const float PopScale = 0.8f;
        const float PopDuration = 0.35f;
        const float ReadyDuration = 0.2f;

        readonly List<Dropdown.Item> party = new();
        Dropdown.Item[] authored;
        string emptyText;
        bool clearPending;
        readonly char[] initial = new char[1];

        void Awake()
        {
            authored = dropdown.items.ToArray();
            emptyText = placeholder.text;
            dropdown.onItemSelected.AddListener(OnItemSelected);
        }

        void Start()
        {
            foreach (int index in invited)
            {
                party.Add(authored[index]);
                list.SetHidden(authored[index], true);
            }

            Draw(null, false);
        }

        // The Dropdown (Input) writes the picked name into the field after this, so the field clears a little later
        void LateUpdate()
        {
            if (!clearPending)
                return;

            clearPending = false;
            field.SetTextWithoutNotify(string.Empty);
            dropdown.SetFilter(string.Empty);
            dropdown.ClearSelection();
        }

        void OnItemSelected(int index)
        {
            clearPending = true;
            Dropdown.Item item = dropdown.items[index];
            if (party.Count >= seats.Length || party.Contains(item))
                return;

            party.Add(item);
            list.SetHidden(item, true);
            Draw(item, true);
        }

        // Called by each seat's Interactive
        public void Remove(int seat)
        {
            if (seat >= party.Count)
                return;

            Dropdown.Item item = party[seat];
            party.RemoveAt(seat);
            list.SetHidden(item, false);
            Draw(null, true);
        }

        void Draw(Dropdown.Item added, bool animate)
        {
            for (int i = 0; i < seats.Length; i++)
            {
                Seat seat = seats[i];
                bool taken = i < party.Count;
                seat.empty.SetActive(!taken);
                seat.member.SetActive(taken);
                seat.button.SetInteractable(taken);
                if (!taken)
                    continue;

                string label = party[i].label;
                initial[0] = label[0];
                seat.initial.SetCharArray(initial, 0, 1);
                seat.memberName.SetText(label);
                if (party[i] == added)
                {
                    EvoTween.Create(Vector3.one * PopScale, Vector3.one, PopDuration)
                        .WithEase(Ease.OutBack)
                        .WithTimeMode(TimeMode.Unscaled)
                        .BindToScale(seat.avatar);
                }
            }

            count.SetText("{0} / {1}", (float)(party.Count + 1), (float)(seats.Length + 1));
            DemoUtils.Fade(startReady, party.Count > 0 ? 1f : 0f, animate ? ReadyDuration : 0f);

            bool full = party.Count >= seats.Length;
            DemoUtils.SetLocked(fieldGroup, full, animate);
            field.interactable = !full;
            placeholder.text = full ? fullText : emptyText;
            if (full)
                dropdown.Close();
        }
    }
}
