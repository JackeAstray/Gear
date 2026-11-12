using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>Outlines one component of a focused screen and links to its page, on the component's own variant.</summary>
    [AddComponentMenu("")]
    public class DemoScreenTag : MonoBehaviour
    {
        public enum Placement { Above, Below, Inside, AboveRight, BelowRight, Right }

        [SerializeField] private RectTransform target;
        [SerializeField] private string page;
        [Tooltip("Tab ID of the variant to open inside the page. Empty leaves the page on the variant it shows.")]
        [SerializeField] private string variant;
        [SerializeField] private Placement placement;
        [SerializeField, Min(0f)] private float padding = 10;
        [SerializeField, Min(0f)] private float pillGap = 8;
        [SerializeField] private RectTransform pill;
        [SerializeField] private GameObject arrow;

        static readonly Vector3[] corners = new Vector3[4];
        DemoHome home;

        /// <summary>
        /// Fits the outline around the target and puts the pill beside it. The arrow shows when Home has the page.
        /// </summary>
        public void Place(RectTransform screen, DemoHome owner)
        {
            if (home != owner)
            {
                home = owner;
                if (arrow != null)
                    arrow.SetActive(home.HasPage(page));
            }

            target.GetWorldCorners(corners);
            Vector2 min = new(float.MaxValue, float.MaxValue);
            Vector2 max = new(float.MinValue, float.MinValue);

            for (int i = 0; i < 4; i++)
            {
                Vector2 point = screen.InverseTransformPoint(corners[i]);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            var rect = (RectTransform)transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = min - new Vector2(padding, padding) - screen.rect.center;
            rect.sizeDelta = max - min + new Vector2(padding * 2, padding * 2);

            switch (placement)
            {
                case Placement.Below:
                    pill.anchorMin = pill.anchorMax = new Vector2(0, 0);
                    pill.pivot = new Vector2(0, 1);
                    pill.anchoredPosition = new Vector2(0, -pillGap);
                    break;
                case Placement.AboveRight:
                    pill.anchorMin = pill.anchorMax = new Vector2(1, 1);
                    pill.pivot = new Vector2(1, 0);
                    pill.anchoredPosition = new Vector2(0, pillGap);
                    break;
                case Placement.BelowRight:
                    pill.anchorMin = pill.anchorMax = new Vector2(1, 0);
                    pill.pivot = new Vector2(1, 1);
                    pill.anchoredPosition = new Vector2(0, -pillGap);
                    break;
                case Placement.Right:
                    pill.anchorMin = pill.anchorMax = new Vector2(1, 0.5f);
                    pill.pivot = new Vector2(0, 0.5f);
                    pill.anchoredPosition = new Vector2(pillGap, 0);
                    break;
                case Placement.Inside:
                    pill.anchorMin = pill.anchorMax = new Vector2(1, 1);
                    pill.pivot = new Vector2(1, 1);
                    pill.anchoredPosition = new Vector2(-pillGap - 6, -pillGap - 6);
                    break;
                default:
                    pill.anchorMin = pill.anchorMax = new Vector2(0, 1);
                    pill.pivot = new Vector2(0, 0);
                    pill.anchoredPosition = new Vector2(0, pillGap);
                    break;
            }
        }

        public void Open()
        {
            if (home != null)
                home.OpenPage(page, variant);
        }
    }
}
