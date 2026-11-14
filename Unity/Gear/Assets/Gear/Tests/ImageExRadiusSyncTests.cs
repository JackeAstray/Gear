using NUnit.Framework;
using UnityEngine;
using Gear.Utilities.UI.ImageExtensions;

namespace Gear.Tests
{
    /// <summary>
    /// RectangleImg 圆角同步（None / MatchParent / MatchTarget / MatchAncestor）的纯几何测试（EditMode）。
    /// </summary>
    public class ImageExRadiusSyncTests
    {
        private GameObject root;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("RadiusSyncRoot", typeof(RectTransform));
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null)
                Object.DestroyImmediate(root);
        }

        private static ImageEx CreateRectangle(Transform parent, string name, Vector2 size, float radius,
            CornerRadiusMode mode = CornerRadiusMode.Pixels)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx));
            go.transform.SetParent(parent, false);
            ((RectTransform)go.transform).sizeDelta = size;
            var image = go.GetComponent<ImageEx>();
            image.DrawShape = DrawShape.Rectangle;
            image.Rectangle = new RectangleImg
            {
                CornerRadius = new Vector4(radius, radius, radius, radius),
                RadiusMode = mode,
            };
            return image;
        }

        [Test]
        public void MatchParent_SubtractsInset()
        {
            ImageEx parent = CreateRectangle(root.transform, "Parent", new Vector2(200f, 200f), 16f);
            ImageEx child = CreateRectangle(parent.transform, "Child", new Vector2(100f, 60f), 0f);
            child.Rectangle.SyncMode = CornerRadiusSyncMode.MatchParent;
            child.Rectangle.SyncInset = 4f;

            Vector4 radius = child.Rectangle.ResolveCornerRadius(new Vector2(100f, 60f));

            Assert.AreEqual(12f, radius.x, 0.01f);
            Assert.AreEqual(12f, radius.y, 0.01f);
            Assert.AreEqual(12f, radius.z, 0.01f);
            Assert.AreEqual(12f, radius.w, 0.01f);
        }

        [Test]
        public void MatchAncestor_SkipsNonRectangleParent()
        {
            ImageEx ancestor = CreateRectangle(root.transform, "Ancestor", new Vector2(200f, 200f), 20f);
            var middle = new GameObject("Middle", typeof(RectTransform));
            middle.transform.SetParent(ancestor.transform, false);
            ImageEx child = CreateRectangle(middle.transform, "Child", new Vector2(100f, 60f), 0f);
            child.Rectangle.SyncMode = CornerRadiusSyncMode.MatchAncestor;
            child.Rectangle.SyncInset = 5f;

            Vector4 radius = child.Rectangle.ResolveCornerRadius(new Vector2(100f, 60f));

            Assert.AreEqual(15f, radius.x, 0.01f);
        }

        [Test]
        public void MatchTarget_UsesTargetRadius()
        {
            ImageEx target = CreateRectangle(root.transform, "Target", new Vector2(200f, 200f), 30f);
            ImageEx child = CreateRectangle(root.transform, "Child", new Vector2(100f, 60f), 0f);
            child.Rectangle.SyncMode = CornerRadiusSyncMode.MatchTarget;
            child.Rectangle.SyncTarget = target.rectTransform;
            child.Rectangle.SyncInset = 10f;

            Vector4 radius = child.Rectangle.ResolveCornerRadius(new Vector2(100f, 60f));

            Assert.AreEqual(20f, radius.x, 0.01f);
        }

        [Test]
        public void InsetLargerThanSource_ClampsToZero()
        {
            ImageEx parent = CreateRectangle(root.transform, "Parent", new Vector2(200f, 200f), 10f);
            ImageEx child = CreateRectangle(parent.transform, "Child", new Vector2(100f, 60f), 0f);
            child.Rectangle.SyncMode = CornerRadiusSyncMode.MatchParent;
            child.Rectangle.SyncInset = 50f;

            Vector4 radius = child.Rectangle.ResolveCornerRadius(new Vector2(100f, 60f));

            Assert.AreEqual(0f, radius.x, 0.001f);
        }

        [Test]
        public void PercentageSource_ResolvedBySourceSize()
        {
            // 父级 200×100、100% => 50px；若误用子级尺寸（200×200）会得到 100px
            ImageEx parent = CreateRectangle(root.transform, "Parent", new Vector2(200f, 100f), 100f,
                CornerRadiusMode.Percentage);
            ImageEx child = CreateRectangle(parent.transform, "Child", new Vector2(200f, 200f), 0f);
            child.Rectangle.SyncMode = CornerRadiusSyncMode.MatchParent;
            child.Rectangle.SyncInset = 10f;

            Vector4 radius = child.Rectangle.ResolveCornerRadius(new Vector2(200f, 200f));

            Assert.AreEqual(40f, radius.x, 0.01f);
        }

        [Test]
        public void None_KeepsOwnRadius()
        {
            ImageEx parent = CreateRectangle(root.transform, "Parent", new Vector2(200f, 200f), 16f);
            ImageEx child = CreateRectangle(parent.transform, "Child", new Vector2(100f, 60f), 8f);

            Vector4 radius = child.Rectangle.ResolveCornerRadius(new Vector2(100f, 60f));

            Assert.AreEqual(8f, radius.x, 0.01f);
        }

        [Test]
        public void SourceRadiusChange_DirtiesSyncedChild()
        {
            ImageEx parent = CreateRectangle(root.transform, "Parent", new Vector2(200f, 200f), 16f);
            ImageEx child = CreateRectangle(parent.transform, "Child", new Vector2(100f, 60f), 0f);
            child.Rectangle.SyncMode = CornerRadiusSyncMode.MatchParent;

            // 父级改圆角后，子级应重新解析（此处直接验证解析结果随来源变化）
            parent.Rectangle.CornerRadius = new Vector4(24f, 24f, 24f, 24f);
            Vector4 radius = child.Rectangle.ResolveCornerRadius(new Vector2(100f, 60f));

            Assert.AreEqual(24f, radius.x, 0.01f);
        }
    }
}
