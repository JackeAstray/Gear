using System.Collections.Generic;
using UnityEngine;

namespace Evo.UI
{
    /// <summary>
    /// Ordered SDF operations sharing the owner's quad, style, and material.
    /// </summary>
    public partial class ProceduralShape
    {
        public const int MaxShapeOperations = 4;

        public enum OperationType { Union = 0, Subtract = 1, Intersect = 2, Exclude = 3 }

        [System.Serializable]
        public struct ShapeOperation
        {
            [Tooltip("Geometry source on the same root Canvas. Its own operations and visual layers are not sampled.")]
            public ProceduralShape target;
            public OperationType operation;
            [Range(0f, 1f), Tooltip("Blend radius relative to the smaller shape. Zero gives a hard boolean edge.")]
            public float smoothing;
        }

        [SerializeField, Tooltip("Supply geometry to operations without drawing or receiving raycasts.")]
        private bool operandOnly = false;
        [SerializeField, Tooltip("Applied in order. At most four operands; each samples the target's primitive and clipping.")]
        private ShapeOperation[] shapeOperations = System.Array.Empty<ShapeOperation>();

        static readonly List<ProceduralShape> operationOwners = new();
        OperationData[] operationData;
        bool operationClipValid, operationsTracked;
        int activeOperationCount, meshOperationCount;
        float operationDistanceStretch = 1f;
        float meshOperationStretch = 1f;
        Rect operationBounds, meshOperationBounds, operationClipRect;

        struct OperationData
        {
            public ProceduralShape target;
            public Vector4 rowX, rowY, shape, radii, clip;
        }

        public bool OperandOnly
        {
            get => operandOnly;
            set
            {
                if (operandOnly == value)
                    return;

                operandOnly = value;
                base.raycastTarget = !operandOnly && raycastMode != RaycastMode.None;
                UpdateOperationTracking();
                SetVerticesDirty();
            }
        }

        public int ShapeOperationCount => shapeOperations != null ? Mathf.Min(shapeOperations.Length, MaxShapeOperations) : 0;

        public ShapeOperation GetShapeOperation(int index) => shapeOperations[index];

        public void SetShapeOperation(int index, ShapeOperation value)
        {
            value.smoothing = Mathf.Clamp01(value.smoothing);
            shapeOperations[index] = value;
            SetVerticesDirty();
        }

        /// <summary>
        /// Retains the supplied array. Configure once, then animate entries without allocations.
        /// </summary>
        public void SetShapeOperations(ShapeOperation[] operations)
        {
            if (operations != null && operations.Length > MaxShapeOperations)
                throw new System.ArgumentException("At most four shape operations are supported.", nameof(operations));

            shapeOperations = operations;
            UpdateOperationTracking();
            SetVerticesDirty();
        }

        void UpdateOperationTracking()
        {
            bool track = isActiveAndEnabled && !operandOnly && ShapeOperationCount > 0;
            if (track == operationsTracked)
                return;

            operationsTracked = track;
            if (!track) { operationOwners.Remove(this); }
            else
            {
                operationData ??= new OperationData[MaxShapeOperations];
                operationOwners.Add(this);
            }
        }

        void UntrackOperations()
        {
            if (!operationsTracked)
                return;

            operationOwners.Remove(this);
            operationsTracked = false;
        }

        // Runs after Canvas layout/graphic rebuilds, immediately before the shared atlas upload.
        // Only owners with configured operations are visited. Changed bounds rebuild one quad.
        static void RefreshShapeOperations()
        {
            for (int i = 0; i < operationOwners.Count; i++)
            {
                ProceduralShape owner = operationOwners[i];
                owner.WriteShapeOperations(owner.GetDrawingRect());
                if (owner.meshOperationCount != owner.activeOperationCount || !owner.meshOperationBounds.Equals(owner.operationBounds)
                    || owner.meshOperationStretch != owner.operationDistanceStretch)
                {
                    owner.UpdateGeometry();
                    if (owner.canvasRenderer.hasRectClipping)
                        owner.Cull(owner.operationClipRect, owner.operationClipValid);
                }
            }
        }

        void WriteShapeOperations(Rect rect)
        {
            activeOperationCount = 0;
            operationBounds = rect;
            operationDistanceStretch = 1f;

            if (!operationsTracked || m_Material != null || stylePage == null || canvas == null)
                return;

            Matrix4x4 ownerToWorld = transform.localToWorldMatrix * GetShapeTransform(rect.center) * Matrix4x4.Translate(rect.center);
            for (int i = 0; i < ShapeOperationCount; i++)
            {
                ShapeOperation operation = shapeOperations[i];
                ProceduralShape target = operation.target;
                if (target == null || target == this || !target.isActiveAndEnabled || target.canvas == null
                    || target.canvas.rootCanvas != canvas.rootCanvas)
                    continue;

                Rect targetRect = target.GetDrawingRect();
                if (targetRect.width <= 0.001f || targetRect.height <= 0.001f)
                    continue;

                Matrix4x4 mapping = Matrix4x4.Translate(-targetRect.center) * target.GetShapeTransform(targetRect.center, true)
                    * target.transform.worldToLocalMatrix * ownerToWorld;
                float determinant = mapping.m00 * mapping.m11 - mapping.m01 * mapping.m10;
                // The renderer combines coplanar UI geometry; singular transforms have no area
                if (Mathf.Abs(determinant) < 0.000001f || Mathf.Abs(mapping.m20) > 0.0001f
                    || Mathf.Abs(mapping.m21) > 0.0001f || Mathf.Abs(mapping.m23) > 0.01f)
                    continue;

                float a = mapping.m00 * mapping.m00 + mapping.m10 * mapping.m10;
                float b = mapping.m01 * mapping.m01 + mapping.m11 * mapping.m11;
                float c = mapping.m00 * mapping.m01 + mapping.m10 * mapping.m11;
                float scale = 1f / Mathf.Sqrt(0.5f * (a + b + Mathf.Sqrt((a - b) * (a - b) + 4f * c * c)));
                float stretch = 1f / (Mathf.Abs(determinant) * scale * scale);
                if (target.UsesRoundedClipEdges && target.shape == ShapeType.Ellipse)
                    stretch *= Mathf.Max(targetRect.width, targetRect.height) / Mathf.Min(targetRect.width, targetRect.height);
               
                operationDistanceStretch = Mathf.Max(operationDistanceStretch, stretch);
                float smoothing = Mathf.Clamp01(operation.smoothing)
                    * Mathf.Min(Mathf.Min(rect.width, rect.height), Mathf.Min(targetRect.width, targetRect.height) * scale) * 0.5f;
                OperationData data = new()
                {
                    target = target,
                    rowX = new(mapping.m00, mapping.m01, mapping.m03, scale),
                    rowY = new(mapping.m10, mapping.m11, mapping.m13, smoothing),
                    shape = new(targetRect.width * 0.5f, targetRect.height * 0.5f, target.ShaderShapeID, target.UsesSquircleCorners ? 1f : 0f),
                    radii = target.GetShaderRadii(targetRect),
                    clip = new(Mathf.Clamp01(target.clipAmount), target.GetPackedClipConfig(), Mathf.Max(0f, target.outlineWidth), (int)operation.operation)
                };
                operationData[activeOperationCount] = data;
                stylePage.WriteOperation(styleSlot, activeOperationCount++, data);

                if (operation.operation == OperationType.Union || operation.operation == OperationType.Exclude)
                {
                    Rect bounds = TransformOperationRect(new Rect(-targetRect.size * 0.5f, targetRect.size), mapping.inverse);
                    bounds.position += rect.center;
                    operationBounds = Rect.MinMaxRect(Mathf.Min(operationBounds.xMin, bounds.xMin), Mathf.Min(operationBounds.yMin, bounds.yMin),
                        Mathf.Max(operationBounds.xMax, bounds.xMax), Mathf.Max(operationBounds.yMax, bounds.yMax));
                    // A polynomial smooth minimum can expand by at most k/4 per operation
                    operationBounds.min -= Vector2.one * (smoothing * 0.25f * operationDistanceStretch);
                    operationBounds.max += Vector2.one * (smoothing * 0.25f * operationDistanceStretch);
                }
            }

            if (activeOperationCount > 0)
                stylePage.WriteOperationCount(styleSlot, activeOperationCount);
        }

        static Rect TransformOperationRect(Rect rect, Matrix4x4 matrix)
        {
            Vector2 center = matrix.MultiplyPoint3x4(rect.center);
            Vector2 extent = rect.size * 0.5f;
            extent = new Vector2(Mathf.Abs(matrix.m00) * extent.x + Mathf.Abs(matrix.m01) * extent.y,
                Mathf.Abs(matrix.m10) * extent.x + Mathf.Abs(matrix.m11) * extent.y);
            return new Rect(center - extent, extent * 2f);
        }

        public override void Cull(Rect clipRect, bool validRect)
        {
            operationClipRect = clipRect;
            operationClipValid = validRect;
            if ((activeOperationCount == 0 && !HasShapeTransform) || canvas == null)
            {
                base.Cull(clipRect, validRect);
                return;
            }

            // RectMask2D normally tests only RectTransform bounds, which can miss a union or skew
            Rect bounds = activeOperationCount > 0 ? operationBounds : GetDrawingRect();
            float padding = (Mathf.Max(softness, outerShadowSize + outerShadowSoftness) + 2f) * operationDistanceStretch + outerShadowOffset.magnitude;
            bounds.min -= Vector2.one * padding;
            bounds.max += Vector2.one * padding;
            Matrix4x4 toCanvas = canvas.rootCanvas.transform.worldToLocalMatrix * transform.localToWorldMatrix * GetShapeTransform(GetDrawingRect().center);
            bool cull = !validRect || !clipRect.Overlaps(TransformOperationRect(bounds, toCanvas), true);
            if (canvasRenderer.cull == cull)
                return;

            canvasRenderer.cull = cull;
            onCullStateChanged.Invoke(cull);
            OnCullingChanged();
        }

        float ApplyShapeOperations(float distance, Vector2 p)
        {
            if (!operationsTracked || m_Material != null || stylePage == null)
                return distance;

            // Refresh for input events occurring before this frame's Canvas rendering
            WriteShapeOperations(GetDrawingRect());

            for (int i = 0; i < activeOperationCount; i++)
            {
                OperationData data = operationData[i];
                Vector2 q = new(data.rowX.x * p.x + data.rowX.y * p.y + data.rowX.z, data.rowY.x * p.x + data.rowY.y * p.y + data.rowY.z);
                float operand = data.target.GetPrimitiveDistance(q, data.target.GetDrawingRect()) * data.rowX.w;
                float k = data.rowY.w;
                OperationType operation = (OperationType)data.clip.w;
                if (operation == OperationType.Subtract) { distance = -SmoothOperationMin(-distance, operand, k); }
                else if (operation == OperationType.Intersect) { distance = -SmoothOperationMin(-distance, -operand, k); }
                else if (operation == OperationType.Exclude)
                {
                    float union = SmoothOperationMin(distance, operand, k);
                    float intersection = -SmoothOperationMin(-distance, -operand, k);
                    distance = -SmoothOperationMin(-union, intersection, k);
                }
                else { distance = SmoothOperationMin(distance, operand, k); }
            }

            return distance;
        }

        static float SmoothOperationMin(float a, float b, float k)
        {
            if (k <= 0f)
                return Mathf.Min(a, b);

            float h = Mathf.Max(k - Mathf.Abs(a - b), 0f) / k;
            return Mathf.Min(a, b) - h * h * k * 0.25f;
        }

        float GetPrimitiveDistance(Vector2 p, Rect rect, bool applyClip = true)
        {
            Vector2 halfSize = rect.size * 0.5f;
            if (clipMethod != ClipMethod.None && clipAmount <= 0f)
                return 1e10f;
            if (UsesRoundedClipEdges)
                return GetRoundedClipSDF(p, halfSize, GetShaderRadii(rect));

            float distance = GetShapeSDF(p, halfSize, GetRadiiPixels(rect), UsesSquircleCorners);
            if (applyClip && clipMethod != ClipMethod.None && clipAmount < 1f)
                distance = Mathf.Max(distance, GetClipSDF(p, halfSize, Mathf.Clamp01(clipAmount), Mathf.Clamp(clipOrigin, 0, MaxOriginForMethod())));
           
            return distance;
        }
    }
}