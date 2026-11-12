using TMPro;
using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Tooltip tab: every option applies to all the toolbar's tooltips.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoTooltip : MonoBehaviour
    {
        [SerializeField] private Tooltip[] tooltips;
        [SerializeField] private OffsetPosition[] positions = { OffsetPosition.BottomRight, OffsetPosition.Bottom, OffsetPosition.Top, OffsetPosition.Right };

        [Header("Options")]
        [SerializeField] private TMP_Text info;
        [SerializeField] private CanvasGroup smoothingOption;

        string[] descriptions;

        void Awake()
        {
            descriptions = new string[tooltips.Length];
            for (int i = 0; i < tooltips.Length; i++)
                descriptions[i] = tooltips[i].Description;

            DemoUtils.SetLocked(smoothingOption, !tooltips[0].followCursor, false);
        }

        void OnEnable() => UpdateInfo();

        public void SetPosition(int index)
        {
            foreach (Tooltip tooltip in tooltips)
                tooltip.offsetPosition = positions[index];
        }

        public void SetFollowCursor(bool value)
        {
            foreach (Tooltip tooltip in tooltips)
                tooltip.followCursor = value;

            DemoUtils.SetLocked(smoothingOption, !value);
            UpdateInfo();
        }

        public void SetDescription(bool value)
        {
            for (int i = 0; i < tooltips.Length; i++)
                tooltips[i].Description = value ? descriptions[i] : string.Empty;
        }

        public void SetDelay(float seconds)
        {
            foreach (Tooltip tooltip in tooltips)
                tooltip.showDelay = seconds;
        }

        // The kit's smoothing is a follow speed (0 = instant, higher = faster), so the percent runs the other way
        public void SetSmoothing(float percent)
        {
            float speed = percent <= 0 ? 0 : Mathf.Lerp(30, 3, percent / 100f);

            foreach (Tooltip tooltip in tooltips)
                tooltip.movementSmoothing = speed;
        }

        public void SetMaxWidth(float width)
        {
            foreach (Tooltip tooltip in tooltips)
                tooltip.maxWidth = width;
        }

        void UpdateInfo() => info.SetText(tooltips[0].followCursor ? "Hover the tools: the tooltip follows the cursor." : "Hover the tools: the tooltip pins to the hovered tool.");
    }
}
