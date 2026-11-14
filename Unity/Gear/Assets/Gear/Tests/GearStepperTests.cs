using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Gear.Utilities.UI.Navigation;

namespace Gear.Tests
{
    /// <summary>
    /// GearStepper 的 EditMode 测试：前进/后退、校验门与完成事件。
    /// </summary>
    public class GearStepperTests
    {
        private GameObject root;
        private GearStepper stepper;
        private GameObject page0;
        private GameObject page1;
        private GameObject page2;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("StepperRoot", typeof(RectTransform), typeof(GearStepper));
            stepper = root.GetComponent<GearStepper>();

            page0 = new GameObject("Step0", typeof(RectTransform));
            page0.transform.SetParent(root.transform, false);
            page1 = new GameObject("Step1", typeof(RectTransform));
            page1.transform.SetParent(root.transform, false);
            page2 = new GameObject("Step2", typeof(RectTransform));
            page2.transform.SetParent(root.transform, false);

            stepper.Configure(new List<GearStepper.Step>
            {
                new GearStepper.Step { id = "0", page = page0 },
                new GearStepper.Step { id = "1", page = page1 },
                new GearStepper.Step { id = "2", page = page2 },
            });
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null)
                Object.DestroyImmediate(root);
        }

        [Test]
        public void Configure_ShowsOnlyFirstPage()
        {
            Assert.AreEqual(0, stepper.CurrentIndex);
            Assert.IsTrue(page0.activeSelf);
            Assert.IsFalse(page1.activeSelf);
            Assert.IsFalse(page2.activeSelf);
        }

        [Test]
        public void NextAndBack_MoveBetweenPages()
        {
            stepper.Next();
            Assert.AreEqual(1, stepper.CurrentIndex);
            Assert.IsFalse(page0.activeSelf);
            Assert.IsTrue(page1.activeSelf);

            stepper.Back();
            Assert.AreEqual(0, stepper.CurrentIndex);
            Assert.IsTrue(page0.activeSelf);
            Assert.IsFalse(page1.activeSelf);
        }

        [Test]
        public void CanContinueFalse_BlocksNext()
        {
            stepper.CanContinue = false;
            stepper.Next();
            Assert.AreEqual(0, stepper.CurrentIndex);
        }

        [Test]
        public void LastNext_RaisesCompleted()
        {
            int completed = 0;
            stepper.Completed += () => completed++;
            stepper.GoTo(2);
            stepper.Next();
            Assert.AreEqual(1, completed);
        }

        [Test]
        public void GoTo_UnknownId_KeepsCurrent()
        {
            stepper.GoTo("missing");
            Assert.AreEqual(0, stepper.CurrentIndex);
        }
    }
}
