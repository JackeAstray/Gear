using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Gear.Utilities.UI.ImageExtensions;
using Gear.Utilities.UI.Interaction;

namespace Gear.Tests
{
    /// <summary>
    /// ImageExStateColors 五态状态色的 EditMode 测试：直接调用 <c>ApplyState</c> 与真实指针事件两条路径。
    /// </summary>
    public class ImageExStateColorsTests
    {
        private GameObject go;
        private ImageEx image;
        private GearInteractive interactive;
        private ImageExStateColors colors;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("StateColorsTest",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx),
                typeof(GearInteractive), typeof(ImageExStateColors));
            image = go.GetComponent<ImageEx>();
            interactive = go.GetComponent<GearInteractive>();
            colors = go.GetComponent<ImageExStateColors>();
            colors.TransitionDuration = 0f;
            // 编辑模式下 OnEnable 不会执行，显式建立绑定（运行时由 OnEnable 自动调用）
            colors.Bind();
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null)
                Object.DestroyImmediate(go);
        }

        private static ImageExStateColors.StateStyle Style(Color fill, Color outline, Color glow, float intensity)
        {
            return new ImageExStateColors.StateStyle
            {
                Fill = fill,
                Outline = outline,
                Glow = glow,
                GlowIntensity = intensity,
            };
        }

        [Test]
        public void ApplyState_Instant_WritesFillOutlineGlow()
        {
            Color fill = new Color(0.2f, 0.4f, 0.6f, 1f);
            Color outline = new Color(1f, 0.5f, 0f, 1f);
            Color glow = new Color(1f, 0.9f, 0.2f, 1f);
            colors.SetStyle(GearInteractionState.Highlighted, Style(fill, outline, glow, 0.7f));

            colors.ApplyState(GearInteractionState.Highlighted, true);

            Assert.AreEqual(fill, image.color);
            Assert.AreEqual(outline, image.OutlineColor);
            Assert.AreEqual(glow, image.BorderGlowColor);
            Assert.AreEqual(0.7f, image.BorderGlowIntensity, 0.001f);
            Assert.IsTrue(image.EnableBorderGlow);
        }

        [Test]
        public void PointerEnter_AppliesHighlightedStyle()
        {
            Color fill = new Color(0.3f, 0.3f, 0.3f, 1f);
            colors.SetStyle(GearInteractionState.Highlighted, Style(fill, Color.white, Color.white, 0.4f));

            var data = new PointerEventData(null);
            ExecuteEvents.Execute(go, data, ExecuteEvents.pointerEnterHandler);

            Assert.AreEqual(GearInteractionState.Highlighted, interactive.State);
            Assert.AreEqual(fill, image.color);
            Assert.AreEqual(0.4f, image.BorderGlowIntensity, 0.001f);
        }

        [Test]
        public void SetStyle_ReappliesWhenCurrentState()
        {
            var data = new PointerEventData(null);
            interactive.OnPointerDown(data);
            Assert.AreEqual(GearInteractionState.Pressed, interactive.State);

            Color fill = new Color(0.1f, 0.8f, 0.2f, 1f);
            colors.SetStyle(GearInteractionState.Pressed, Style(fill, Color.black, Color.white, 0f));

            Assert.AreEqual(fill, image.color);
        }

        [Test]
        public void Disabled_WhenSelectableNotInteractable()
        {
            Button button = go.AddComponent<Button>();
            button.interactable = false;
            interactive.Target = button;
            interactive.Refresh();

            Assert.AreEqual(GearInteractionState.Disabled, interactive.State);
            Assert.AreEqual(colors.GetStyle(GearInteractionState.Disabled).Fill, image.color);
        }

        [Test]
        public void NormalState_DisablesGlow()
        {
            colors.SetStyle(GearInteractionState.Highlighted, Style(Color.white, Color.white, Color.white, 0.8f));
            colors.ApplyState(GearInteractionState.Highlighted, true);
            Assert.IsTrue(image.EnableBorderGlow);

            colors.ApplyState(GearInteractionState.Normal, true);
            Assert.IsFalse(image.EnableBorderGlow);
        }
    }
}
