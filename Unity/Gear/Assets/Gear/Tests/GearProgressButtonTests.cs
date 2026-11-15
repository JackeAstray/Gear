using Gear.Utilities.UI.Widgets;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gear.Tests
{
    public sealed class GearProgressButtonTests
    {
        private GameObject go;
        private GearProgressButton button;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("ProgressButton",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(GearProgressButton));
            button = go.GetComponent<GearProgressButton>();
            button.targetGraphic = go.GetComponent<Image>();
            button.HoldDuration = 1f;
            button.ResetDuration = 0.2f;
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
        }

        private static PointerEventData Pointer()
        {
            return new PointerEventData(null) { button = PointerEventData.InputButton.Left };
        }

        [Test]
        public void HoldToComplete_ProgressAndEvent()
        {
            int started = 0, completed = 0;
            button.OnProgressStart.AddListener(() => started++);
            button.OnProgressComplete.AddListener(() => completed++);

            button.OnPointerDown(Pointer());
            button.Step(0.5f);

            Assert.IsTrue(button.IsHolding);
            Assert.AreEqual(0.5f, button.Progress, 0.01f);
            Assert.AreEqual(1, started);

            button.Step(0.6f);
            Assert.IsFalse(button.IsHolding);
            Assert.IsTrue(button.IsComplete);
            Assert.AreEqual(1f, button.Progress, 0.001f);
            Assert.AreEqual(1, completed);
        }

        [Test]
        public void ReleaseBeforeComplete_Cancels()
        {
            int cancelled = 0;
            button.OnProgressCancel.AddListener(() => cancelled++);

            button.OnPointerDown(Pointer());
            button.Step(0.4f);
            button.OnPointerUp(Pointer());

            Assert.IsFalse(button.IsHolding);
            Assert.IsFalse(button.IsComplete);
            Assert.AreEqual(1, cancelled);
        }

        [Test]
        public void DisabledButton_DoesNotStart()
        {
            button.interactable = false;
            button.OnPointerDown(Pointer());

            Assert.IsFalse(button.IsHolding);
        }

        [Test]
        public void ResetProgress_ClearsCompletedState()
        {
            button.OnPointerDown(Pointer());
            button.Step(1.1f);
            Assert.IsTrue(button.IsComplete);

            button.ResetProgress();

            Assert.IsFalse(button.IsComplete);
            Assert.AreEqual(0f, button.Progress, 0.001f);
        }
    }
}
