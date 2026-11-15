using System.Collections.Generic;
using Gear.Utilities.UI.Widgets;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Gear.Tests
{
    public sealed class GearFlexibleGridLayoutTests
    {
        private GameObject root;
        private RectTransform rect;
        private GearFlexibleGridLayout layout;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("FlexibleGrid", typeof(RectTransform), typeof(GearFlexibleGridLayout));
            rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(420f, 400f);
            layout = root.GetComponent<GearFlexibleGridLayout>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.Spacing = new Vector2(10f, 10f);
            layout.AspectRatio = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        private RectTransform AddChild(string name, float preferredHeight = 0f)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(root.transform, false);
            var r = (RectTransform)go.transform;
            if (preferredHeight > 0f)
            {
                var element = go.AddComponent<LayoutElement>();
                element.preferredHeight = preferredHeight;
            }

            return r;
        }

        private void Apply()
        {
            layout.CalculateLayoutInputHorizontal();
            layout.CalculateLayoutInputVertical();
            layout.SetLayoutHorizontal();
        }

        [Test]
        public void FixedColumns_PlacesCellsAndAspectRatio()
        {
            layout.Fit = GearFlexibleGridLayout.FitType.FixedColumns;
            layout.Columns = 2;
            RectTransform a = AddChild("A");
            RectTransform b = AddChild("B");
            RectTransform c = AddChild("C");
            Apply();

            Assert.AreEqual(205f, a.sizeDelta.x, 0.01f);
            Assert.AreEqual(205f, a.sizeDelta.y, 0.01f);
            Assert.AreEqual(0f, a.anchoredPosition.x, 0.01f);
            Assert.AreEqual(215f, b.anchoredPosition.x, 0.01f);
            Assert.AreEqual(-215f, c.anchoredPosition.y, 0.01f);
        }

        [Test]
        public void Responsive_UsesMinCellSizeForColumns()
        {
            layout.Fit = GearFlexibleGridLayout.FitType.Responsive;
            layout.MinCellSize = 90f;
            for (int i = 0; i < 5; i++) AddChild("C" + i);
            Apply();

            Assert.AreEqual(97.5f, root.transform.GetChild(0).GetComponent<RectTransform>().sizeDelta.x, 0.01f);
            Assert.AreEqual(0f, root.transform.GetChild(4).GetComponent<RectTransform>().anchoredPosition.x, 0.01f);
        }

        [Test]
        public void Masonry_UsesTallestShortColumn()
        {
            layout.Masonry = true;
            layout.Columns = 2;
            AddChild("A", 50f);
            AddChild("B", 50f);
            RectTransform c = AddChild("C", 50f);
            RectTransform d = AddChild("D", 50f);
            Apply();

            Assert.AreEqual(0f, c.anchoredPosition.x, 0.01f);
            Assert.AreEqual(-60f, c.anchoredPosition.y, 0.01f);
            Assert.AreEqual(215f, d.anchoredPosition.x, 0.01f);
        }
    }
}
