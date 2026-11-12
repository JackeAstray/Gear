using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

namespace Evo.UI.Demo
{
    /// <summary>
    /// One row in the search results: a page name and a second line saying why it matched.
    /// Not a Selectable on purpose, so clicking it doesn't take focus away from the search field.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoSearchResult : MonoBehaviour, IPointerMoveHandler, IPointerClickHandler
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text detailText;

        DemoSearch search;
        Vector2 lastPointer;

        public int Index { get; private set; }
        public RectTransform Rect => (RectTransform)transform;

        // Copied out of the builders, so the search reuses them without making strings
        public void Bind(DemoSearch owner, int index, StringBuilder name, StringBuilder detail)
        {
            search = owner;
            Index = index;
            nameText.SetText(name);
            detailText.SetText(detail);
        }

        // Moves, not enters: a list scrolling under a still pointer shouldn't steal the keyboard's pick
        public void OnPointerMove(PointerEventData eventData)
        {
            if (eventData.position == lastPointer)
                return;

            lastPointer = eventData.position;
            search.Hover(Index);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                search.Pick(Index);
        }
    }
}