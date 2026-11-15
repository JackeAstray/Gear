using Gear.Utilities.UI.ImageExtensions.Overlays;
using Gear.Utilities.UI.Widgets;
using TMPro;
using UnityEngine;

namespace Gear.UI.Showcase
{
    /// <summary>
    /// M2.1 展卡：长按进度按钮 + 文案反馈 + 完成 Toast。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShowcaseProgressDemo : MonoBehaviour
    {
        [SerializeField] private GearProgressButton button;
        [SerializeField] private TMP_Text label;

        public void Configure(GearProgressButton target, TMP_Text text)
        {
            button = target;
            label = text;
            Hook();
        }

        private void OnEnable() => Hook();
        private void OnDisable() => Unhook();

        private void Hook()
        {
            if (button == null) return;
            button.OnProgressStart.RemoveListener(HandleStart);
            button.OnProgressStart.AddListener(HandleStart);
            button.OnProgressUpdate.RemoveListener(HandleUpdate);
            button.OnProgressUpdate.AddListener(HandleUpdate);
            button.OnProgressComplete.RemoveListener(HandleComplete);
            button.OnProgressComplete.AddListener(HandleComplete);
            button.OnProgressCancel.RemoveListener(HandleCancel);
            button.OnProgressCancel.AddListener(HandleCancel);
        }

        private void Unhook()
        {
            if (button == null) return;
            button.OnProgressStart.RemoveListener(HandleStart);
            button.OnProgressUpdate.RemoveListener(HandleUpdate);
            button.OnProgressComplete.RemoveListener(HandleComplete);
            button.OnProgressCancel.RemoveListener(HandleCancel);
        }

        private void HandleStart()
        {
            if (label != null) label.text = "Holding...";
        }

        private void HandleUpdate(float progress)
        {
            if (label != null) label.text = Mathf.RoundToInt(progress * 100f) + "%";
        }

        private void HandleComplete()
        {
            if (label != null) label.text = "Complete";
            GearToast.Show("ProgressButton 完成", "M2.1", GearToastKind.Success);
        }

        private void HandleCancel()
        {
            if (label != null) label.text = "Hold 1.2s";
        }
    }
}
