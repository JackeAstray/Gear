using System.Collections.Generic;
using Gear.Utilities.UI.ImageExtensions;
using UnityEngine;
using UnityEngine.Events;

namespace Gear.Utilities.UI.Widgets
{
    /// <summary>
    /// 环状色板：子色块按环布局，悬停外推、选中描边，按钮可键盘/手柄导航。
    /// </summary>
    [AddComponentMenu("UI/Gear/Color Palette")]
    [DisallowMultipleComponent]
    public sealed class GearColorPalette : MonoBehaviour
    {
        [SerializeField] private List<GearColorSwatch> swatches = new List<GearColorSwatch>();
        [SerializeField] private ImageEx selectedPreview;
        [SerializeField] private int selectedIndex;
        [SerializeField, Min(1f)] private float hoverScale = 1.18f;
        [SerializeField, Min(1f)] private float selectedScale = 1.1f;
        [SerializeField] private UnityEvent<Color> onColorChanged = new UnityEvent<Color>();

        public IReadOnlyList<GearColorSwatch> Swatches => swatches;
        public UnityEvent<Color> OnColorChanged => onColorChanged;
        public int SelectedIndex => selectedIndex;
        public Color SelectedColor => swatches.Count == 0
            ? Color.white
            : swatches[Mathf.Clamp(selectedIndex, 0, swatches.Count - 1)].SwatchColor;

        private void OnEnable()
        {
            Bind();
            Select(selectedIndex, false);
        }

        /// <summary>重新绑定子色块；编辑器生成 prefab 或运行时注入都可调用。</summary>
        public void Bind()
        {
            if (swatches.Count == 0)
            {
                swatches.AddRange(GetComponentsInChildren<GearColorSwatch>(true));
            }

            for (int i = 0; i < swatches.Count; i++)
            {
                if (swatches[i] != null) swatches[i].Bind(this, i, swatches[i].SwatchColor);
            }
        }

        public void SetHover(int index, bool hovered)
        {
            if (index < 0 || index >= swatches.Count || swatches[index] == null) return;
            float scale = hovered ? hoverScale : index == selectedIndex ? selectedScale : 1f;
            swatches[index].transform.localScale = Vector3.one * scale;
        }

        public void Select(int index, bool notify = true)
        {
            if (swatches.Count == 0) return;
            selectedIndex = Mathf.Clamp(index, 0, swatches.Count - 1);
            for (int i = 0; i < swatches.Count; i++)
            {
                GearColorSwatch swatch = swatches[i];
                if (swatch == null) continue;
                bool selected = i == selectedIndex;
                swatch.SetSelected(selected);
                swatch.transform.localScale = Vector3.one * (selected ? selectedScale : 1f);
            }

            if (selectedPreview != null) selectedPreview.color = SelectedColor;
            if (notify) onColorChanged?.Invoke(SelectedColor);
        }

        /// <summary>按 RGB 距离选最近的色块。</summary>
        public void SetColor(Color color, bool notify = true)
        {
            if (swatches.Count == 0) return;
            int best = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < swatches.Count; i++)
            {
                Color candidate = swatches[i].SwatchColor;
                float distance = (candidate.r - color.r) * (candidate.r - color.r)
                    + (candidate.g - color.g) * (candidate.g - color.g)
                    + (candidate.b - color.b) * (candidate.b - color.b);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            Select(best, notify);
        }

        /// <summary>编辑器用：写入色块列表和选中预览。</summary>
        public void Configure(List<GearColorSwatch> items, ImageEx preview)
        {
            swatches = items ?? new List<GearColorSwatch>();
            selectedPreview = preview;
            Bind();
        }
    }
}
