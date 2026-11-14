using NUnit.Framework;
using UnityEngine;
using Gear.Utilities.UI.ImageExtensions;

namespace Gear.Tests
{
    /// <summary>
    /// MaterialMode.Pooled：同设置共享材质、改设置自动换新、软遮罩回退。
    /// </summary>
    public class ImageExMaterialPoolTests
    {
        private const string ShaderName = "ReunionMovement/UI/ImageEx";

        private GameObject go1;
        private GameObject go2;

        [SetUp]
        public void SetUp()
        {
            go1 = CreateImage("PoolA");
            go2 = CreateImage("PoolB");
        }

        [TearDown]
        public void TearDown()
        {
            if (go1 != null) Object.DestroyImmediate(go1);
            if (go2 != null) Object.DestroyImmediate(go2);
        }

        private static GameObject CreateImage(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx));
            ((RectTransform)go.transform).sizeDelta = new Vector2(200f, 100f);
            return go;
        }

        private static Material NewBaseMaterial()
        {
            return new Material(Shader.Find(ShaderName));
        }

        private static void ConfigureSame(ImageEx image)
        {
            image.MaterialMode = MaterialMode.Pooled;
            image.DrawShape = DrawShape.Rectangle;
            image.Rectangle = new RectangleImg { CornerRadius = new Vector4(12f, 12f, 12f, 12f) };
            image.OutlineWidth = 2f;
            image.OutlineColor = new Color(1f, 0.5f, 0f, 1f);
        }

        private static void AssertColor(Color expected, Color actual)
        {
            Assert.AreEqual(expected.r, actual.r, 1e-4f);
            Assert.AreEqual(expected.g, actual.g, 1e-4f);
            Assert.AreEqual(expected.b, actual.b, 1e-4f);
            Assert.AreEqual(expected.a, actual.a, 1e-4f);
        }

        [Test]
        public void SameSettings_ShareMaterial()
        {
            ImageEx a = go1.GetComponent<ImageEx>();
            ImageEx b = go2.GetComponent<ImageEx>();
            ConfigureSame(a);
            ConfigureSame(b);

            Material baseA = NewBaseMaterial();
            Material baseB = NewBaseMaterial();
            try
            {
                Material matA = a.GetModifiedMaterial(baseA);
                Material matB = b.GetModifiedMaterial(baseB);

                Assert.AreSame(matA, matB, "设置相同的 Pooled 实例应共享同一份材质");
                Assert.AreEqual("ImageEx Pooled", matA.name);
                AssertColor(new Color(1f, 0.5f, 0f, 1f), matA.GetColor("_OutlineColor"));
                Assert.AreEqual((int)DrawShape.Rectangle, (int)matA.GetInt("_DrawShape"));
            }
            finally
            {
                Object.DestroyImmediate(baseA);
                Object.DestroyImmediate(baseB);
            }
        }

        [Test]
        public void DifferentSettings_DoNotShare()
        {
            ImageEx a = go1.GetComponent<ImageEx>();
            ImageEx b = go2.GetComponent<ImageEx>();
            ConfigureSame(a);
            ConfigureSame(b);
            b.OutlineColor = new Color(0f, 1f, 1f, 1f);

            Material baseA = NewBaseMaterial();
            Material baseB = NewBaseMaterial();
            try
            {
                Material matA = a.GetModifiedMaterial(baseA);
                Material matB = b.GetModifiedMaterial(baseB);
                Assert.AreNotSame(matA, matB, "描边色不同的实例不能共享材质");
            }
            finally
            {
                Object.DestroyImmediate(baseA);
                Object.DestroyImmediate(baseB);
            }
        }

        [Test]
        public void ChangedSettings_GetNewMaterialWithoutTouchingOld()
        {
            ImageEx a = go1.GetComponent<ImageEx>();
            ConfigureSame(a);
            a.OutlineColor = new Color(1f, 0f, 0f, 1f);

            Material baseA = NewBaseMaterial();
            try
            {
                Material red = a.GetModifiedMaterial(baseA);
                AssertColor(new Color(1f, 0f, 0f, 1f), red.GetColor("_OutlineColor"));

                a.OutlineColor = new Color(0f, 0f, 1f, 1f);
                Material blue = a.GetModifiedMaterial(baseA);

                Assert.AreNotSame(red, blue);
                AssertColor(new Color(1f, 0f, 0f, 1f), red.GetColor("_OutlineColor"));
                AssertColor(new Color(0f, 0f, 1f, 1f), blue.GetColor("_OutlineColor"));
            }
            finally
            {
                Object.DestroyImmediate(baseA);
            }
        }

        [Test]
        public void SoftMask_FallsBackToInstanceMaterial()
        {
            var maskRoot = new GameObject("MaskRoot", typeof(RectTransform), typeof(GearSoftMask));
            var child = new GameObject("Masked", typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx));
            Material baseMat = NewBaseMaterial();
            try
            {
                child.transform.SetParent(maskRoot.transform, false);
                ImageEx image = child.GetComponent<ImageEx>();
                image.MaterialMode = MaterialMode.Pooled;
                image.DrawShape = DrawShape.Rectangle;

                Material mat = image.GetModifiedMaterial(baseMat);

                Assert.AreNotEqual("ImageEx Pooled", mat.name);
                Assert.AreEqual(1, (int)mat.GetInt("_GearMaskEnabled"));
            }
            finally
            {
                Object.DestroyImmediate(baseMat);
                Object.DestroyImmediate(child);
                Object.DestroyImmediate(maskRoot);
            }
        }

        [Test]
        public void DynamicMode_StillWritesInstanceMaterial()
        {
            ImageEx image = go1.GetComponent<ImageEx>();
            image.OutlineWidth = 3f;
            image.OutlineColor = new Color(0.2f, 0.4f, 0.6f, 1f);

            Material baseMat = NewBaseMaterial();
            try
            {
                Material mat = image.GetModifiedMaterial(baseMat);
                AssertColor(new Color(0.2f, 0.4f, 0.6f, 1f), mat.GetColor("_OutlineColor"));
                Assert.AreEqual(3f, mat.GetFloat("_OutlineWidth"), 0.01f);
            }
            finally
            {
                Object.DestroyImmediate(baseMat);
            }
        }
    }
}
