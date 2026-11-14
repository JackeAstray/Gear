using NUnit.Framework;
using UnityEngine;
using Gear.Utilities.UI.Motion;
using Gear.Utilities.UI.Navigation;

namespace Gear.Tests
{
    /// <summary>
    /// EVODemo 复刻的手感组件：错峰出现 / 指针倾斜 / Dock 放大。
    /// </summary>
    public class GearMotionTests
    {
        private GameObject root;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("MotionRoot", typeof(RectTransform));
            ((RectTransform)root.transform).sizeDelta = new Vector2(400f, 120f);
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        private RectTransform CreateChild(string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(root.transform, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        // ---------------- AnimatedContainer ----------------

        [Test]
        public void AnimatedContainer_StaggerAndComplete()
        {
            RectTransform a = CreateChild("A", Vector2.zero, new Vector2(80f, 80f));
            CreateChild("B", Vector2.zero, new Vector2(80f, 80f));
            CreateChild("C", Vector2.zero, new Vector2(80f, 80f));
            GearAnimatedContainer container = root.AddComponent<GearAnimatedContainer>();

            container.Play();
            container.Sample(0f);

            CanvasGroup first = a.GetComponent<CanvasGroup>();
            Assert.IsNotNull(first, "淡入应自动补 CanvasGroup");
            Assert.AreEqual(0f, first.alpha, 0.001f);
            Assert.Less(a.localScale.x, 1f, "起始应处于缩放中");

            container.Sample(container.TotalDuration);

            Assert.AreEqual(1f, first.alpha, 0.001f);
            Assert.AreEqual(1f, a.localScale.x, 0.001f);
            Assert.AreEqual(1f, root.transform.GetChild(2).localScale.x, 0.001f);
        }

        [Test]
        public void AnimatedContainer_SecondItemWaitsForStagger()
        {
            CreateChild("A", Vector2.zero, new Vector2(80f, 80f));
            RectTransform b = CreateChild("B", Vector2.zero, new Vector2(80f, 80f));
            GearAnimatedContainer container = root.AddComponent<GearAnimatedContainer>();

            container.Play();
            container.Sample(0f);

            Assert.AreEqual(0f, b.GetComponent<CanvasGroup>().alpha, 0.001f, "第二项在延迟内应保持隐藏");
        }

        // ---------------- RectSway ----------------

        [Test]
        public void RectSway_LeansTowardsPointerAndResets()
        {
            RectTransform target = CreateChild("Card", Vector2.zero, new Vector2(200f, 100f));
            GearRectSway sway = target.gameObject.AddComponent<GearRectSway>();
            sway.SetPointerNormalized(new Vector2(1f, 1f));

            for (int i = 0; i < 40; i++) sway.Step(0.05f);

            Vector3 euler = target.localRotation.eulerAngles;
            float pitch = Mathf.DeltaAngle(0f, euler.x);
            float yaw = Mathf.DeltaAngle(0f, euler.y);
            Assert.Less(pitch, -4f, "指针在上方时应有俯仰");
            Assert.Greater(yaw, 4f, "指针在右侧时应有偏航");

            sway.ResetSway();
            Assert.AreEqual(0f, Mathf.DeltaAngle(0f, target.localRotation.eulerAngles.x), 0.01f);
            Assert.AreEqual(0f, Mathf.DeltaAngle(0f, target.localRotation.eulerAngles.y), 0.01f);
        }

        // ---------------- DockMagnifier ----------------

        [Test]
        public void DockMagnifier_WeightFallsOffWithDistance()
        {
            GearDockMagnifier dock = root.AddComponent<GearDockMagnifier>();

            Assert.AreEqual(1f, dock.EvaluateWeight(0f, 50f), 0.001f);
            Assert.Less(dock.EvaluateWeight(200f, 50f), 0.01f);
        }

        [Test]
        public void DockMagnifier_GrowsItemNearPointer()
        {
            RectTransform left = CreateChild("L", new Vector2(-120f, 0f), new Vector2(96f, 96f));
            RectTransform middle = CreateChild("M", new Vector2(0f, 0f), new Vector2(96f, 96f));
            RectTransform right = CreateChild("R", new Vector2(120f, 0f), new Vector2(96f, 96f));

            GearDockMagnifier dock = root.AddComponent<GearDockMagnifier>();
            Vector2 center = ((RectTransform)root.transform).InverseTransformPoint(
                middle.TransformPoint(middle.rect.center));
            dock.SetPointerLocal(center);

            for (int i = 0; i < 40; i++) dock.Step(0.05f);

            Assert.Greater(middle.sizeDelta.x, 110f, "指针下的项应放大");
            Assert.Less(left.sizeDelta.x, middle.sizeDelta.x, "远处的项应小于指针下的项");
            Assert.Less(right.sizeDelta.x, middle.sizeDelta.x, "远处的项应小于指针下的项");

            dock.ClearPointer();
            for (int i = 0; i < 120; i++) dock.Step(0.05f);
            Assert.AreEqual(96f, middle.sizeDelta.x, 1f, "指针移出后应回到底尺寸");
        }

        // ---------------- Spring ----------------
        [Test]
        public void Spring_ConvergesToTarget()
        {
            var spring = new GearSpring(180f, 26f, 0f);

            for (int i = 0; i < 240; i++) spring.Step(1f, 1f / 60f);

            Assert.AreEqual(1f, spring.Value, 0.01f);
            Assert.IsTrue(spring.Settled(0.05f));
        }

        [Test]
        public void Spring_LongFrameStaysFinite()
        {
            var spring = new GearSpring(180f, 26f, 0f);
            spring.Step(1f, 5f);

            Assert.IsFalse(float.IsNaN(spring.Value));
            Assert.IsFalse(float.IsInfinity(spring.Value));
            Assert.LessOrEqual(spring.Value, 2f);
        }

        // ---------------- RadialLayout ----------------

        [Test]
        public void RadialLayout_PlacesChildrenOnCircle()
        {
            var host = new GameObject("Radial", typeof(RectTransform), typeof(GearRadialLayout));
            RectTransform hostRect = (RectTransform)host.transform;
            hostRect.sizeDelta = new Vector2(400f, 400f);

            for (int i = 0; i < 4; i++)
            {
                var child = new GameObject("Node" + i, typeof(RectTransform));
                child.transform.SetParent(host.transform, false);
                ((RectTransform)child.transform).sizeDelta = new Vector2(40f, 40f);
            }

            var layout = host.GetComponent<GearRadialLayout>();
            layout.Radius = 100f;
            layout.StartAngle = 0f;
            layout.EndAngle = 270f;
            layout.Clockwise = true;
            layout.FillFullCircle = false;

            UnityEngine.UI.LayoutRebuilder.MarkLayoutForRebuild(hostRect);
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(hostRect);

            Assert.AreEqual(100f, hostRect.GetChild(0).GetComponent<RectTransform>().anchoredPosition.x, 0.5f);
            Assert.AreEqual(100f, hostRect.GetChild(1).GetComponent<RectTransform>().anchoredPosition.y, 0.5f);
            Assert.AreEqual(-100f, hostRect.GetChild(2).GetComponent<RectTransform>().anchoredPosition.x, 0.5f);
            Assert.AreEqual(-100f, hostRect.GetChild(3).GetComponent<RectTransform>().anchoredPosition.y, 0.5f);

            Object.DestroyImmediate(host);
        }

        // ---------------- Spinner ----------------

        [Test]
        public void Spinner_RotatesAtConfiguredSpeed()
        {
            var go = new GameObject("Spinner", typeof(RectTransform), typeof(GearSpinner));
            var spinner = go.GetComponent<GearSpinner>();
            spinner.DegreesPerSecond = 90f;
            spinner.Clockwise = true;

            spinner.Step(1f);
            float clockwise = Mathf.DeltaAngle(0f, go.transform.localRotation.eulerAngles.z);
            Assert.AreEqual(-90f, clockwise, 1f);

            spinner.Clockwise = false;
            spinner.Step(1f);
            Assert.AreEqual(0f, Mathf.DeltaAngle(0f, go.transform.localRotation.eulerAngles.z), 1f);

            Object.DestroyImmediate(go);
        }

        // ---------------- RectParallax ----------------

        [Test]
        public void RectParallax_MovesTowardsPointerAndResets()
        {
            var go = new GameObject("Parallax", typeof(RectTransform), typeof(GearRectParallax));
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(200f, 100f);
            var parallax = go.GetComponent<GearRectParallax>();

            parallax.SetPointerNormalized(new Vector2(1f, 1f));
            for (int i = 0; i < 80; i++) parallax.Step(0.05f);

            Assert.AreEqual(12f, rect.anchoredPosition.x, 0.6f);
            Assert.AreEqual(8f, rect.anchoredPosition.y, 0.6f);

            parallax.ResetParallax();
            Assert.AreEqual(0f, rect.anchoredPosition.x, 0.01f);
            Assert.AreEqual(0f, rect.anchoredPosition.y, 0.01f);

            Object.DestroyImmediate(go);
        }

        // ---------------- DistributionBar ----------------

        [Test]
        public void DistributionBar_SplitsWidthByWeights()
        {
            var host = new GameObject("Distribution", typeof(RectTransform), typeof(GearDistributionBar));
            var hostRect = (RectTransform)host.transform;
            hostRect.sizeDelta = new Vector2(400f, 40f);

            for (int i = 0; i < 3; i++)
            {
                var child = new GameObject("Seg" + i, typeof(RectTransform));
                child.transform.SetParent(host.transform, false);
                ((RectTransform)child.transform).sizeDelta = new Vector2(40f, 40f);
            }

            var bar = host.GetComponent<GearDistributionBar>();
            bar.SetWeights(2f, 1f, 1f);

            Assert.AreEqual(200f, hostRect.GetChild(0).GetComponent<RectTransform>().sizeDelta.x, 0.5f);
            Assert.AreEqual(100f, hostRect.GetChild(1).GetComponent<RectTransform>().sizeDelta.x, 0.5f);
            Assert.AreEqual(100f, hostRect.GetChild(2).GetComponent<RectTransform>().sizeDelta.x, 0.5f);

            Assert.AreEqual(-100f, hostRect.GetChild(0).GetComponent<RectTransform>().anchoredPosition.x, 0.5f);
            Assert.AreEqual(50f, hostRect.GetChild(1).GetComponent<RectTransform>().anchoredPosition.x, 0.5f);
            Assert.AreEqual(150f, hostRect.GetChild(2).GetComponent<RectTransform>().anchoredPosition.x, 0.5f);

            Object.DestroyImmediate(host);
        }

        // ---------------- MenuBar ----------------

        [Test]
        public void MenuBar_BuildsButtonsAndReportsIndex()
        {
            var go = new GameObject("MenuBar", typeof(RectTransform), typeof(GearMenuBar));
            var bar = go.GetComponent<GearMenuBar>();

            int clicked = -1;
            bar.Configure(new[] { "文件", "编辑", "视图" }, index => clicked = index);

            Assert.AreEqual(3, bar.Buttons.Count);
            bar.Buttons[1].onClick.Invoke();
            Assert.AreEqual(1, clicked);

            Object.DestroyImmediate(go);
        }
    }
}
