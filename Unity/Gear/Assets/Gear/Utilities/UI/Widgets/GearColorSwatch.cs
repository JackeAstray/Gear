using Gear.Utilities.UI.ImageExtensions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gear.Utilities.UI.Widgets
{
    /// <summary>环状色板的一个色块；负责指针悬停/点击，选中描边由 Palette 统一管理。</summary>
    [AddComponentMenu("UI/Gear/Color Swatch")]
    [DisallowMultipleComponent]
    public sealed class GearColorSwatch : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private ImageEx image;
        [SerializeField] private Color color = Color.white;

        private GearColorPalette palette;
        private int index;
        private Button button;

        public ImageEx Image
        {
            get
            {
                if (image == null) image = GetComponent<ImageEx>();
                return image;
            }
        }

        public Color SwatchColor => color;
        public int Index => index;

        public void SetColor(Color swatchColor)
        {
            color = swatchColor;
            var graphic = Image;
            if (graphic != null) graphic.color = color;
        }

        public void Bind(GearColorPalette owner, int swatchIndex, Color swatchColor)
        {
            palette = owner;
            index = swatchIndex;
            color = swatchColor;
            var graphic = Image;
            if (graphic != null) graphic.color = color;

            button = GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveListener(OnButtonClick);
                button.onClick.AddListener(OnButtonClick);
            }
        }

        public void SetSelected(bool selected)
        {
            var graphic = Image;
            if (graphic == null) return;
            graphic.OutlineWidth = selected ? 2f : 0f;
            graphic.OutlineColor = selected ? Color.white : new Color(1f, 1f, 1f, 0f);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (palette != null) palette.SetHover(index, true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (palette != null) palette.SetHover(index, false);
        }

        private void OnButtonClick()
        {
            if (palette != null) palette.Select(index);
        }

        private void OnDisable()
        {
            if (button != null) button.onClick.RemoveListener(OnButtonClick);
        }
    }
}
