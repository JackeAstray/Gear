using UnityEngine;
using UnityEngine.UI;

namespace Gear.Utilities.UI.Interaction
{
    /// <summary>
    /// 把 <see cref="GearInteractive"/> 的五态映射到目标 Graphic 的颜色。
    /// <para>
    /// 适合 ImageEx / Image / TMP 这类只需要“变色反馈”的控件；复杂缩放/音效仍交给 ButtonAni。
    /// </para>
    /// </summary>
    [AddComponentMenu("UI/Gear/Interactive Tint")]
    [DisallowMultipleComponent]
    public sealed class GearInteractiveTint : MonoBehaviour
    {
        [SerializeField] private GearInteractive interactive;
        [SerializeField] private Graphic graphic;

        [Header("Colors")]
        [SerializeField] private Color normal = Color.white;
        [SerializeField] private Color highlighted = new Color(0.92f, 0.96f, 1f, 1f);
        [SerializeField] private Color pressed = new Color(0.78f, 0.86f, 1f, 1f);
        [SerializeField] private Color selected = new Color(0.86f, 0.93f, 1f, 1f);
        [SerializeField] private Color disabled = new Color(1f, 1f, 1f, 0.4f);
        [SerializeField] private bool colorsInitialized;

        private void OnEnable()
        {
            EnsureBound();
            if (interactive != null) Apply(interactive.State);
        }

        private void OnDisable()
        {
            if (interactive != null)
                interactive.StateChanged -= Apply;
        }

        /// <summary>运行时绑定依赖并立即应用。</summary>
        public void Bind(GearInteractive stateSource, Graphic targetGraphic)
        {
            if (interactive != null)
                interactive.StateChanged -= Apply;

            interactive = stateSource;
            graphic = targetGraphic;

            EnsureBound();
            if (interactive != null) Apply(interactive.State);
        }

        /// <summary>设置五态颜色；可在编辑器/演示卡构建时调用。</summary>
        public void SetColors(Color normalColor, Color highlightedColor, Color pressedColor,
            Color selectedColor, Color disabledColor)
        {
            normal = normalColor;
            highlighted = highlightedColor;
            pressed = pressedColor;
            selected = selectedColor;
            disabled = disabledColor;
            colorsInitialized = true;

            // EditMode 下 AddComponent 的 OnEnable 时机不稳定，这里补一次绑定
            EnsureBound();
            if (interactive != null) Apply(interactive.State);
        }

        private void EnsureBound()
        {
            if (interactive == null) interactive = GetComponent<GearInteractive>();
            if (graphic == null) graphic = GetComponent<Graphic>();
            if (interactive == null || graphic == null) return;

            if (!colorsInitialized)
            {
                // 旧卡片/运行时 AddComponent 常常只挂 Tint 没配颜色；
                // 默认白色会把原本的按钮底色覆盖成白块。首次绑定时捕获 Graphic 当前色。
                CaptureFromGraphic(graphic.color);
            }

            interactive.StateChanged -= Apply;
            interactive.StateChanged += Apply;
        }

        private void CaptureFromGraphic(Color baseColor)
        {
            normal = baseColor;
            highlighted = Color.Lerp(baseColor, Color.white, 0.12f);
            pressed = Color.Lerp(baseColor, Color.black, 0.18f);
            selected = baseColor;
            disabled = new Color(baseColor.r, baseColor.g, baseColor.b, 0.45f);
            colorsInitialized = true;
        }

        private void Apply(GearInteractionState state)
        {
            if (graphic == null) graphic = GetComponent<Graphic>();
            if (graphic == null) return;
            graphic.color = state switch
            {
                GearInteractionState.Highlighted => highlighted,
                GearInteractionState.Pressed => pressed,
                GearInteractionState.Selected => selected,
                GearInteractionState.Disabled => disabled,
                _ => normal,
            };
        }
    }
}
