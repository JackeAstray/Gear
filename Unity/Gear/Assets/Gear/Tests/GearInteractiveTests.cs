using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Gear.Utilities.UI.Interaction;

namespace Gear.Tests
{
    /// <summary>GearInteractive 五态与 Tint 的 EditMode 测试。</summary>
    public class GearInteractiveTests
    {
        private GameObject root;
        private Button button;
        private GearInteractive interactive;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("InteractiveRoot", typeof(RectTransform), typeof(Image), typeof(Button), typeof(GearInteractive));
            button = root.GetComponent<Button>();
            interactive = root.GetComponent<GearInteractive>();
            interactive.Target = button;
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null)
                Object.DestroyImmediate(root);
        }

        [Test]
        public void PointerAndSelection_ResolveFiveStates()
        {
            Assert.AreEqual(GearInteractionState.Normal, interactive.State);

            PointerEventData pointer = new PointerEventData(null) { button = PointerEventData.InputButton.Left };
            BaseEventData selection = new BaseEventData(null);

            interactive.OnPointerEnter(pointer);
            Assert.AreEqual(GearInteractionState.Highlighted, interactive.State);

            interactive.OnPointerDown(pointer);
            Assert.AreEqual(GearInteractionState.Pressed, interactive.State);

            interactive.OnPointerUp(pointer);
            Assert.AreEqual(GearInteractionState.Highlighted, interactive.State);

            interactive.OnSelect(selection);
            Assert.AreEqual(GearInteractionState.Selected, interactive.State);

            interactive.OnDeselect(selection);
            Assert.AreEqual(GearInteractionState.Highlighted, interactive.State);

            interactive.OnPointerExit(pointer);
            Assert.AreEqual(GearInteractionState.Normal, interactive.State);
        }

        [Test]
        public void DisabledTarget_BecomesDisabledAndDeduplicatesEvents()
        {
            int changes = 0;
            interactive.StateChanged += _ => changes++;

            button.interactable = false;
            interactive.Refresh();
            Assert.AreEqual(GearInteractionState.Disabled, interactive.State);
            int afterDisable = changes;

            interactive.Refresh();
            Assert.AreEqual(afterDisable, changes);
        }

        [Test]
        public void Tint_FollowsStateColors()
        {
            root.AddComponent<GearInteractiveTint>();
            GearInteractiveTint tint = root.GetComponent<GearInteractiveTint>();
            Image image = root.GetComponent<Image>();
            Color highlighted = new Color(0.2f, 0.4f, 0.6f, 1f);
            tint.SetColors(Color.white, highlighted, Color.red, Color.green, Color.gray);

            PointerEventData pointer = new PointerEventData(null) { button = PointerEventData.InputButton.Left };
            interactive.OnPointerEnter(pointer);
            Assert.AreEqual(highlighted, image.color);
        }
    }
}
