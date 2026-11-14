using NUnit.Framework;
using UnityEngine;
using Gear.Utilities.UI.ImageExtensions;
using Gear.Utilities.UI.Widgets;

namespace Gear.Tests
{
    /// <summary>
    /// 设置页高频控件：GearSwitch 与 GearRangeSlider 的 EditMode 测试。
    /// </summary>
    public class GearSettingsWidgetTests
    {
        private GameObject root;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("SettingsRoot", typeof(RectTransform));
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        private ImageEx CreateImage(Transform parent, string name, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform),
                typeof(CanvasRenderer), typeof(ImageEx));
            go.transform.SetParent(parent, false);
            ((RectTransform)go.transform).sizeDelta = size;
            return go.GetComponent<ImageEx>();
        }

        // ---------------- Switch ----------------

        [Test]
        public void Switch_ToggleWritesHandleAndNotifies()
        {
            GameObject go = new GameObject("Switch", typeof(RectTransform), typeof(GearSwitch));
            go.transform.SetParent(root.transform, false);

            ImageEx background = CreateImage(go.transform, "Background", new Vector2(64f, 32f));
            ImageEx handle = CreateImage(go.transform, "Handle", new Vector2(26f, 26f));

            GearSwitch toggle = go.GetComponent<GearSwitch>();
            toggle.Handle = (RectTransform)handle.transform;
            toggle.Background = background;
            toggle.OffPosition = new Vector2(-14f, 0f);
            toggle.OnPosition = new Vector2(14f, 0f);

            bool notified = false;
            toggle.OnValueChanged.AddListener(value => notified = value);

            toggle.Toggle();

            Assert.IsTrue(toggle.IsOn);
            Assert.IsTrue(notified);
            Assert.AreEqual(14f, ((RectTransform)handle.transform).anchoredPosition.x, 0.01f);
        }

        [Test]
        public void Switch_SetWithoutNotify_DoesNotInvoke()
        {
            GameObject go = new GameObject("Switch", typeof(RectTransform), typeof(GearSwitch));
            go.transform.SetParent(root.transform, false);
            ImageEx handle = CreateImage(go.transform, "Handle", new Vector2(26f, 26f));

            GearSwitch toggle = go.GetComponent<GearSwitch>();
            toggle.Handle = (RectTransform)handle.transform;

            int calls = 0;
            toggle.OnValueChanged.AddListener(_ => calls++);

            toggle.SetIsOn(true, false);

            Assert.IsTrue(toggle.IsOn);
            Assert.AreEqual(0, calls);
        }

        // ---------------- RangeSlider ----------------

        [Test]
        public void RangeSlider_PlacesHandlesAndFill()
        {
            GameObject trackGo = new GameObject("Track", typeof(RectTransform));
            trackGo.transform.SetParent(root.transform, false);
            RectTransform track = (RectTransform)trackGo.transform;
            track.sizeDelta = new Vector2(200f, 10f);

            ImageEx fill = CreateImage(track, "Fill", new Vector2(0f, 10f));
            ImageEx lowHandle = CreateImage(track, "Low", new Vector2(18f, 18f));
            ImageEx highHandle = CreateImage(track, "High", new Vector2(18f, 18f));

            GearRangeSlider slider = root.AddComponent<GearRangeSlider>();
            slider.Track = track;
            slider.Fill = (RectTransform)fill.transform;
            slider.LowHandle = (RectTransform)lowHandle.transform;
            slider.HighHandle = (RectTransform)highHandle.transform;

            slider.SetValues(20f, 80f);

            Assert.AreEqual(-60f, ((RectTransform)lowHandle.transform).anchoredPosition.x, 0.5f);
            Assert.AreEqual(60f, ((RectTransform)highHandle.transform).anchoredPosition.x, 0.5f);
            Assert.AreEqual(120f, ((RectTransform)fill.transform).sizeDelta.x, 0.5f);
            Assert.AreEqual(0f, ((RectTransform)fill.transform).anchoredPosition.x, 0.5f);
        }

        [Test]
        public void RangeSlider_EnforcesMinGap()
        {
            GameObject trackGo = new GameObject("Track", typeof(RectTransform));
            trackGo.transform.SetParent(root.transform, false);
            RectTransform track = (RectTransform)trackGo.transform;
            track.sizeDelta = new Vector2(200f, 10f);

            GearRangeSlider slider = root.AddComponent<GearRangeSlider>();
            slider.Track = track;
            slider.SetMinMax(0f, 100f);
            slider.MinGap = 10f;
            slider.SetValues(50f, 52f, false);

            // 间距不足时把 high 推到 low + minGap
            Assert.AreEqual(10f, slider.High - slider.Low, 0.01f);
        }

        [Test]
        public void RangeSlider_ClampsAndNotifies()
        {
            GameObject trackGo = new GameObject("Track", typeof(RectTransform));
            trackGo.transform.SetParent(root.transform, false);
            RectTransform track = (RectTransform)trackGo.transform;
            track.sizeDelta = new Vector2(200f, 10f);

            GearRangeSlider slider = root.AddComponent<GearRangeSlider>();
            slider.Track = track;
            slider.SetMinMax(0f, 100f);

            Vector2 notified = Vector2.zero;
            slider.OnValueChanged.AddListener(value => notified = value);

            slider.SetValues(-10f, 120f);

            Assert.AreEqual(0f, slider.Low, 0.01f);
            Assert.AreEqual(100f, slider.High, 0.01f);
            Assert.AreEqual(new Vector2(0f, 100f), notified);

            slider.ApplyPointer01(0.1f);
            Assert.AreEqual(10f, slider.Low, 0.5f);
            Assert.AreEqual(100f, slider.High, 0.5f);
        }
    }
}
