using System.Collections.Generic;
using Gear.Utilities.UI.Widgets;
using NUnit.Framework;
using UnityEngine;

namespace Gear.Tests
{
    public sealed class GearReorderableListTests
    {
        private GameObject root;
        private RectTransform content;
        private GearReorderableList list;
        private readonly List<RectTransform> items = new List<RectTransform>();

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Reorderable", typeof(RectTransform), typeof(GearReorderableList));
            list = root.GetComponent<GearReorderableList>();
            content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(root.transform, false);
            content.sizeDelta = new Vector2(200f, 400f);
            items.Clear();
            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject("Item" + i, typeof(RectTransform), typeof(GearReorderableItem));
                go.transform.SetParent(content, false);
                var rect = (RectTransform)go.transform;
                rect.sizeDelta = new Vector2(180f, 40f);
                items.Add(rect);
            }

            list.Configure(content, items);
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        [Test]
        public void MoveItem_ReordersChildren()
        {
            Assert.IsTrue(list.MoveItem(0, 2));

            Assert.AreEqual(items[2], content.GetChild(2));
            Assert.AreEqual("Item1", list.Items[0].name);
            Assert.AreEqual("Item0", list.Items[2].name);
        }

        [Test]
        public void MoveItem_RaisesFromTo()
        {
            int from = -1;
            int to = -1;
            list.OnReordered.AddListener((a, b) => { from = a; to = b; });

            list.MoveItem(1, 0);

            Assert.AreEqual(1, from);
            Assert.AreEqual(0, to);
            Assert.AreEqual(items[1], list.Items[0]);
        }
    }
}
