using Gear.Utilities.UI.ImageExtensions;
using NUnit.Framework;
using UnityEngine;

namespace Gear.Tests
{
    public sealed class ImageExShapeOperandTests
    {
        private GameObject root;
        private ImageEx main;
        private ImageEx operand;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Operand", typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx));
            main = root.GetComponent<ImageEx>();
            main.rectTransform.sizeDelta = new Vector2(200f, 100f);
            main.DrawShape = DrawShape.Rectangle;

            var child = new GameObject("OperandShape", typeof(RectTransform), typeof(CanvasRenderer), typeof(ImageEx));
            child.transform.SetParent(root.transform, false);
            operand = child.GetComponent<ImageEx>();
            operand.rectTransform.sizeDelta = new Vector2(60f, 30f);
            operand.rectTransform.anchoredPosition = new Vector2(20f, 0f);
            operand.DrawShape = DrawShape.Circle;
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        [Test]
        public void Subtract_WritesOperandUniforms()
        {
            main.SetOperand(operand, ShapeOperandOperation.Subtract, 0.02f);

            Material baseMat = new Material(Shader.Find("ReunionMovement/UI/ImageEx"));
            try
            {
                Material modified = main.GetModifiedMaterial(baseMat);
                Assert.AreEqual(1f, modified.GetFloat("_GearOperandEnabled"), 0.001f);
                Assert.AreEqual(1f, modified.GetFloat("_GearOperandOp"), 0.001f);
                Assert.AreEqual(1f, modified.GetFloat("_GearOperandShape"), 0.001f);
                Assert.AreEqual(0.3f, modified.GetVector("_GearOperandCenterSize").z, 0.01f);
            }
            finally
            {
                Object.DestroyImmediate(baseMat);
            }
        }

        [Test]
        public void Union_WritesUnionOperation()
        {
            main.SetOperand(operand, ShapeOperandOperation.Union, 0.01f);

            Material baseMat = new Material(Shader.Find("ReunionMovement/UI/ImageEx"));
            try
            {
                Material modified = main.GetModifiedMaterial(baseMat);
                Assert.AreEqual(0f, modified.GetFloat("_GearOperandOp"), 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(baseMat);
            }
        }
    }
}
