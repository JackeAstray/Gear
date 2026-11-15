using System;
using UnityEngine;

namespace Gear.Utilities.UI.ImageExtensions
{
    public enum ShapeOperandOperation
    {
        Union = 0,
        Subtract = 1,
    }

    [Serializable]
    public struct ShapeOperand
    {
        public ImageEx target;
        public ShapeOperandOperation operation;
        [Range(0f, 0.25f)] public float smoothing;
    }

    /// <summary>ImageEx partial：单操作数 Combine（Union/Subtract）。</summary>
    public partial class ImageEx
    {
        private static readonly int operandEnabled_Sp = Shader.PropertyToID("_GearOperandEnabled");
        private static readonly int operandCenterSize_Sp = Shader.PropertyToID("_GearOperandCenterSize");
        private static readonly int operandShape_Sp = Shader.PropertyToID("_GearOperandShape");
        private static readonly int operandOp_Sp = Shader.PropertyToID("_GearOperandOp");
        private static readonly int operandSmoothing_Sp = Shader.PropertyToID("_GearOperandSmoothing");
        private static readonly int operandCorner_Sp = Shader.PropertyToID("_GearOperandCorner");

        [SerializeField] private ShapeOperand shapeOperand;

        public ShapeOperand Operand
        {
            get => shapeOperand;
            set { shapeOperand = value; SetMaterialDirty(); }
        }

        public void SetOperand(ImageEx target, ShapeOperandOperation operation = ShapeOperandOperation.Subtract,
            float smoothing = 0.02f)
        {
            shapeOperand.target = target;
            shapeOperand.operation = operation;
            shapeOperand.smoothing = Mathf.Clamp(smoothing, 0f, 0.25f);
            SetMaterialDirty();
        }

        public void ClearOperand()
        {
            shapeOperand.target = null;
            SetMaterialDirty();
        }

        private void WriteShapeOperand(Material mat)
        {
            ImageEx target = shapeOperand.target;
            if (target == null || target == this || rectTransform == null)
            {
                mat.SetFloat(operandEnabled_Sp, 0f);
                return;
            }

            Rect mainRect = rectTransform.rect;
            Rect targetRect = target.rectTransform.rect;
            if (mainRect.width <= 0.0001f || mainRect.height <= 0.0001f)
            {
                mat.SetFloat(operandEnabled_Sp, 0f);
                return;
            }

            Vector3 worldCenter = target.rectTransform.TransformPoint(targetRect.center);
            Vector3 localCenter = rectTransform.InverseTransformPoint(worldCenter);
            Vector2 center = new Vector2(
                localCenter.x / mainRect.width + 0.5f,
                localCenter.y / mainRect.height + 0.5f);
            Vector2 size = new Vector2(
                targetRect.width / mainRect.width,
                targetRect.height / mainRect.height);

            mat.SetFloat(operandEnabled_Sp, 1f);
            mat.SetVector(operandCenterSize_Sp, new Vector4(center.x, center.y, size.x, size.y));
            mat.SetFloat(operandShape_Sp, target.DrawShape == DrawShape.Circle ? 1f : 0f);
            mat.SetFloat(operandOp_Sp, shapeOperand.operation == ShapeOperandOperation.Subtract ? 1f : 0f);
            mat.SetFloat(operandSmoothing_Sp, Mathf.Max(0.0001f, shapeOperand.smoothing));
            mat.SetFloat(operandCorner_Sp, target.DrawShape == DrawShape.Circle ? 0.5f : 0.08f);
        }
    }
}
