using UnityEngine;
using TMPro;

namespace Evo.UI.Demo
{
    /// <summary>
    /// The sample card game menu. Cards, the draw pile, the ante chips and the chip ring run on Tween Animators;
    /// this brings a hovered card to the front of the hand and names the picked ante.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoCardGame : DemoScreenContent
    {
        [Tooltip("Card buttons from left to right. Each rests over the one on its left; a hovered card comes to the front.")]
        [SerializeField] private Interactive[] cards;

        [Header("Ante")]
        [SerializeField] private RadioButtonGroup ante;
        [SerializeField] private TMP_Text anteName;
        [SerializeField] private TMP_Text anteRule;
        [SerializeField] private string[] names = { "Knave", "Knight", "Lord", "King" };
        [SerializeField] private string[] rules = { "The standard run.", "Elites appear on every floor.", "Shops cost 25% more.", "One life. No rerolls." };

        int raised = -1;

        void Awake()
        {
            foreach (Interactive card in cards)
                card.OnStateChanged += RaiseHovered;

            ante.onSelectionChanged.AddListener(ShowAnte);
        }

        // A card raised when the screen went away rests again; its state resets without an event
        void OnEnable() => RaiseHovered(InteractionState.Normal);

        void RaiseHovered(InteractionState state)
        {
            int hovered = -1;

            for (int i = 0; i < cards.Length; i++)
            {
                InteractionState cardState = cards[i].InteractionState;
                if (cardState == InteractionState.Highlighted || cardState == InteractionState.Pressed)
                    hovered = i;
            }

            if (hovered == raised)
                return;

            raised = hovered;

            for (int i = 0; i < cards.Length; i++)
                cards[i].transform.SetAsLastSibling();

            if (hovered >= 0)
                cards[hovered].transform.SetAsLastSibling();
        }

        void ShowAnte(int index)
        {
            anteName.SetText(names[index]);
            anteRule.SetText(rules[index]);
        }
    }
}
