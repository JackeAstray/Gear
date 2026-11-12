using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Evo.UI
{
    /// <summary>
    /// Applies cylindrical curvature to descendant uGUI and TextMesh Pro geometry.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Canvas), typeof(RectTransform))]
    [AddComponentMenu("Evo/UI/Effects/Curved Canvas"), HelpURL(Constants.HelpUrl + "effects/curved-canvas")]
    public class CurvedCanvas : MonoBehaviour, ICanvasElement
    {
        // Curve
        [Tooltip("Total arc angle in degrees. Zero leaves the original geometry unchanged.")]
        [Range(0, 180)] public float curveAngle = 25;
        [Tooltip("Subdivisions for rectangular quads with linearly varying attributes.")]
        [Range(2, 64)] public int segments = 16;
        [Tooltip("Inward = concave visor. Outward = convex dome.")]
        public CurveDirection curveDirection = CurveDirection.Inward;

        // Settings
        [Tooltip("Fallback discovery interval for new Graphics and reparented subtrees." +
                 "\n\nUse RefreshManagedGraphics() for immediate discovery.")]
        [Range(1, 120)] public int rescanIntervalFrames = 30;

        // State
        RectTransform canvasRect;
        readonly List<CurvedCanvasGraphic> managed = new();
        readonly List<Graphic> graphicBuffer = new();
        readonly List<UIVertex> streamBuffer = new(512);
        Canvas.WillRenderCanvases renderCallback;
        bool refreshPending;
        bool forceDirty;
        bool wasEffective;
        bool refreshing;
        int lastScanFrame;
        float previousAngle;
        int previousSegments;
        CurveDirection previousDirection;
        Rect previousRect;
        internal bool IsEffective
        {
            get
            {
                if (!isActiveAndEnabled)
                    return false;
#if UNITY_EDITOR
                return gameObject.scene.IsValid() && !UnityEditor.EditorUtility.IsPersistent(this)
                    && (Application.IsPlaying(gameObject) || previewInEditor);
#else
                return true;
#endif
            }
        }

        void OnEnable()
        {
            canvasRect = (RectTransform)transform;
            forceDirty = refreshPending = true;
            renderCallback ??= BeforeCanvasUpdate;
            Canvas.preWillRenderCanvases += renderCallback;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.hierarchyChanged += OnEditorHierarchyChanged;
#endif
        }

        void OnDisable()
        {
            Canvas.preWillRenderCanvases -= renderCallback;
            CanvasUpdateRegistry.UnRegisterCanvasElementForRebuild(this);
            wasEffective = false;
            NotifyOwnerStateChanged();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.hierarchyChanged -= OnEditorHierarchyChanged;
            QueuePreviewCleanup();
#endif
        }

        void OnDestroy()
        {
            // An outer owner can take over after this component has actually been removed
            CurvedCanvas ancestor = null;
            if (transform.parent != null)
            {
                ancestor = transform.parent.GetComponentInParent<CurvedCanvas>(true);
                if (ancestor != null)
                    ancestor.refreshPending = true;
            }

            while (managed.Count > 0)
            {
                int last = managed.Count - 1;
                CurvedCanvasGraphic modifier = managed[last];
                managed.RemoveAt(last);
                if (modifier != null)
                {
                    modifier.Initialize(null);
                    // Keep the adapter for an ancestor's takeover. Deferred Destroy cannot be
                    // cancelled and could otherwise remove an adapter the ancestor just reused.
                    if (ancestor == null && Application.IsPlaying(modifier.gameObject)
                        && (modifier.hideFlags & HideFlags.DontSaveInBuild) != 0)
                        Destroy(modifier);
                }
            }

            graphicBuffer.Clear();
        }

        void OnTransformChildrenChanged() => refreshPending = true;

        #region Types
        /// <summary>
        /// Direction of the cylindrical bend relative to the original canvas plane.
        /// </summary>
        public enum CurveDirection
        {
            /// <summary>
            /// Bends the edges toward negative canvas-local Z to form a concave surface.
            /// </summary>
            Inward,

            /// <summary>
            /// Bends the edges toward positive canvas-local Z to form a convex surface.
            /// </summary>
            Outward
        }

        /// <summary>
        /// Immutable curve geometry and coordinate transforms used by <see cref="CurveVertex"/>.
        /// </summary>
        public readonly struct CurveParams
        {
            /// <summary>
            /// Half of the sanitized total arc angle, in radians.
            /// </summary>
            public readonly float HalfAngleRad;

            /// <summary>
            /// Cylinder radius that preserves the canvas chord width, or zero when curvature is unavailable.
            /// </summary>
            public readonly float Radius;

            /// <summary>
            /// Half of the positive, finite canvas width; otherwise zero.
            /// </summary>
            public readonly float HalfWidth;

            /// <summary>
            /// Canvas-local X coordinate of the center of curvature.
            /// </summary>
            public readonly float CenterX;

            /// <summary>
            /// Sign used for the depth offset: positive one for inward, negative one for outward.
            /// </summary>
            public readonly float ZSign;

            /// <summary>
            /// Transform from the source vertex's local space to canvas-local space.
            /// </summary>
            public readonly Matrix4x4 ToCanvas;

            /// <summary>
            /// Transform from canvas-local space back to the source vertex's local space.
            /// </summary>
            public readonly Matrix4x4 ToLocal;

            /// <summary>
            /// Creates curve parameters from a canvas width, total arc angle and source-space transforms.
            /// </summary>
            /// <param name="curveAngle">Total arc angle in degrees. NaN becomes zero; other values are clamped to 0–180.</param>
            /// <param name="canvasWidth">Canvas width in canvas-local units. Non-positive or non-finite values become zero.</param>
            /// <param name="dir">Direction of the bend.</param>
            /// <param name="toCanvas">Transform from source-local space to canvas-local space.</param>
            /// <param name="toLocal">Inverse of <paramref name="toCanvas"/>, mapping back to source-local space.</param>
            /// <param name="centerX">Canvas-local X coordinate of the canvas rectangle's center.</param>
            public CurveParams(float curveAngle, float canvasWidth, CurveDirection dir,
                Matrix4x4 toCanvas, Matrix4x4 toLocal, float centerX = 0)
            {
                HalfAngleRad = SanitizeAngle(curveAngle) * 0.5f * Mathf.Deg2Rad;
                HalfWidth = IsFinite(canvasWidth) && canvasWidth > 0 ? canvasWidth * 0.5f : 0;
                float radius = HalfAngleRad >= 0.005f * Mathf.Deg2Rad ? HalfWidth / Mathf.Sin(HalfAngleRad) : 0;
                Radius = IsFinite(radius) ? radius : 0;
                CenterX = centerX;
                ZSign = dir == CurveDirection.Inward ? 1 : -1;
                ToCanvas = toCanvas;
                ToLocal = toLocal;
            }
        }
        #endregion

        #region Canvas Updates
        void BeforeCanvasUpdate()
        {
            if (CanvasUpdateRegistry.IsRebuildingLayout() || CanvasUpdateRegistry.IsRebuildingGraphics())
                return;

            bool effective = IsEffective;
            if (wasEffective != effective)
            {
                wasEffective = effective;
                forceDirty = refreshPending = true;
                NotifyOwnerStateChanged();
#if UNITY_EDITOR
                if (!effective)
                    QueuePreviewCleanup();
#endif
            }

            if (!effective)
                return;

            // A render callback can run several times per frame (e.g. ForceUpdateCanvases).
            // Only the fallback timer is frame-limited; layout/transform checks must run every time.
            // Unsigned subtraction also handles frame-counter resets with domain/scene reload disabled.
            bool scanDue = unchecked((uint)(Time.frameCount - lastScanFrame)) >= (uint)Mathf.Clamp(rescanIntervalFrames, 1, 120);
#if UNITY_EDITOR
            if (!Application.IsPlaying(gameObject))
                scanDue = false; // Edit-mode discovery is driven by hierarchyChanged instead.
#endif
            if (refreshPending || scanDue)
                RefreshManagedGraphics();

            CanvasUpdateRegistry.TryRegisterCanvasElementForLayoutRebuild(this);
        }

        /// <summary>
        /// Checks curve settings and relative Graphic transforms after all layout phases,
        /// invalidating changed geometry before Graphic rebuilds begin.
        /// </summary>
        public void LayoutComplete()
        {
            if (this == null || !IsEffective || canvasRect == null)
                return;

            Rect rect = canvasRect.rect;
            float angle = SanitizeAngle(curveAngle);
            int subdivisions = Mathf.Clamp(segments, 2, 64);
            bool changed = forceDirty || previousAngle != angle || previousSegments != subdivisions
                || previousDirection != curveDirection || previousRect != rect;
            forceDirty = false;
            previousAngle = angle;
            previousSegments = subdivisions;
            previousDirection = curveDirection;
            previousRect = rect;

            Matrix4x4 worldToCanvas = canvasRect.worldToLocalMatrix;
            for (int i = managed.Count - 1; i >= 0; i--)
            {
                CurvedCanvasGraphic modifier = managed[i];
                if (modifier == null)
                    managed.RemoveAt(i);
                else
                    modifier.CheckForChanges(in worldToCanvas, changed);
            }
        }

        /// <summary>
        /// Implements the canvas rebuild callback. Work is deferred to <see cref="LayoutComplete"/>.
        /// </summary>
        public void Rebuild(CanvasUpdate executing) { }
      
        /// <summary>
        /// Completes the canvas element's graphic update. No additional work is required.
        /// </summary>
        public void GraphicUpdateComplete() { }
     
        /// <summary>
        /// Reports whether Unity has destroyed this component's native object.
        /// </summary>
        public bool IsDestroyed() => this == null;
        #endregion

        #region Graphic Management
        internal void Register(CurvedCanvasGraphic modifier)
        {
            if (!managed.Contains(modifier))
                managed.Add(modifier);
        }

        internal void Unregister(CurvedCanvasGraphic modifier) => managed.Remove(modifier);

        void NotifyOwnerStateChanged()
        {
            for (int i = managed.Count - 1; i >= 0; i--)
            {
                if (managed[i] != null)
                    managed[i].OwnerStateChanged();
            }
        }
        #endregion

        #region Curve Parameters
        static float SanitizeAngle(float angle) => float.IsNaN(angle) ? 0 : Mathf.Clamp(angle, 0, 180);
      
        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        bool TryGetCurveParams(RectTransform source, out CurveParams parameters)
        {
            parameters = default;

            if (source == null || !IsEffective || SanitizeAngle(curveAngle) < 0.01f)
                return false;
            if (canvasRect == null)
                canvasRect = (RectTransform)transform;

            Rect rect = canvasRect.rect;
            if (!(rect.width > 0) || !IsFinite(rect.width) || !IsFinite(rect.center.x))
                return false;

            Matrix4x4 toCanvas = canvasRect.worldToLocalMatrix * source.localToWorldMatrix;
            float determinant = toCanvas.determinant;
            if (determinant == 0 || !IsFinite(determinant))
                return false;
          
            Matrix4x4 toLocal = toCanvas.inverse;
            for (int i = 0; i < 16; i++)
            {
                if (!IsFinite(toCanvas[i]) || !IsFinite(toLocal[i]))
                    return false;
            }

            parameters = new CurveParams(curveAngle, rect.width, curveDirection, toCanvas, toLocal, rect.center.x);
            return parameters.Radius > 0;
        }
        #endregion

        #region Mesh Deformation
        // Only called on fresh TMP geometry, before TMP uploads/sorts it. Includes fallback
        // materials, sprites, underline, strikethrough and highlight vertices exactly once.
        internal void CurveTMPTextInfo(TMP_Text text, TMP_TextInfo info)
        {
            if (text == null || info == null || info.meshInfo == null || !TryGetCurveParams(text.rectTransform, out CurveParams cp))
                return;

            int materials = Mathf.Min(info.materialCount, info.meshInfo.Length);
            for (int m = 0; m < materials; m++)
            {
                Vector3[] vertices = info.meshInfo[m].vertices;
                if (vertices == null)
                    continue;
                int count = Mathf.Min(info.meshInfo[m].vertexCount, vertices.Length);
                for (int i = 0; i < count; i++)
                    CurveVertex(ref vertices[i], in cp);
            }
        }

        internal void CurveMesh(VertexHelper vh, Graphic source)
        {
            if (vh == null || vh.currentVertCount == 0 || source == null || source is TMP_Text
                || source is TMP_SubMeshUI || !TryGetCurveParams(source.rectTransform, out CurveParams cp))
                return;

            int indices = vh.currentIndexCount;
            int subdivisions = 0;
            if (indices > 0 && indices % 6 == 0)
            {
                // VertexHelper.FillMesh requires strictly fewer than 65k verts
                subdivisions = Mathf.Min(Mathf.Clamp(segments, 2, 64), 64999 / (indices / 6 * 2) - 1);
                if (subdivisions >= 2)
                {
                    streamBuffer.Clear();
                    vh.GetUIVertexStream(streamBuffer);
                    for (int i = 0; i < streamBuffer.Count; i += 6)
                    {
                        if (!IsRectangularQuad(i))
                        {
                            subdivisions = 0;
                            break;
                        }
                    }
                }
            }

            if (subdivisions < 2)
            {
                // Preserve topology and every vertex channel for arbitrary/custom meshes
                UIVertex vertex = default;
                for (int i = 0; i < vh.currentVertCount; i++)
                {
                    vh.PopulateUIVertex(ref vertex, i);
                    CurveVertex(ref vertex.position, in cp);
                    vh.SetUIVertex(vertex, i);
                }
                return;
            }

            vh.Clear();

            for (int i = 0; i < streamBuffer.Count; i += 6)
            {
                UIVertex a = streamBuffer[i], b = streamBuffer[i + 1], c = streamBuffer[i + 2], d = streamBuffer[i + 4];
                float across = Mathf.Abs(cp.ToCanvas.MultiplyVector(d.position - a.position).x);
                float along = Mathf.Abs(cp.ToCanvas.MultiplyVector(b.position - a.position).x);
               
                if (along > across)
                    EmitQuad(vh, b, c, d, a, subdivisions, in cp);
                else
                    EmitQuad(vh, a, b, c, d, subdivisions, in cp);
            }
        }

        bool IsRectangularQuad(int i)
        {
            UIVertex a = streamBuffer[i], b = streamBuffer[i + 1], c = streamBuffer[i + 2], d = streamBuffer[i + 4];
            bool rectangle = (a.position.x == b.position.x && c.position.x == d.position.x
                && a.position.y == d.position.y && b.position.y == c.position.y)
                || (a.position.y == b.position.y && c.position.y == d.position.y
                && a.position.x == d.position.x && b.position.x == c.position.x);
            return rectangle && SameVertex(a, streamBuffer[i + 5]) && SameVertex(c, streamBuffer[i + 3])
                && a.position.z == b.position.z && a.position.z == c.position.z && a.position.z == d.position.z
                && a.uv0 + c.uv0 == b.uv0 + d.uv0 && a.uv1 + c.uv1 == b.uv1 + d.uv1
                && a.uv2 + c.uv2 == b.uv2 + d.uv2 && a.uv3 + c.uv3 == b.uv3 + d.uv3
                && a.normal + c.normal == b.normal + d.normal && a.tangent + c.tangent == b.tangent + d.tangent
                && a.color.r + c.color.r == b.color.r + d.color.r && a.color.g + c.color.g == b.color.g + d.color.g
                && a.color.b + c.color.b == b.color.b + d.color.b && a.color.a + c.color.a == b.color.a + d.color.a;
        }

        static bool SameVertex(UIVertex a, UIVertex b)
        {
            return a.position.Equals(b.position) && a.normal.Equals(b.normal) && a.tangent.Equals(b.tangent)
                && a.color.r == b.color.r && a.color.g == b.color.g && a.color.b == b.color.b && a.color.a == b.color.a
                && a.uv0.Equals(b.uv0) && a.uv1.Equals(b.uv1) && a.uv2.Equals(b.uv2) && a.uv3.Equals(b.uv3);
        }

        static void EmitQuad(VertexHelper vh, UIVertex a, UIVertex b, UIVertex c, UIVertex d, int subdivisions, in CurveParams cp)
        {
            int start = vh.currentVertCount;
            for (int col = 0; col <= subdivisions; col++)
            {
                float t = (float)col / subdivisions;
                UIVertex bottom = LerpVertex(a, d, t), top = LerpVertex(b, c, t);
                CurveVertex(ref bottom.position, in cp);
                CurveVertex(ref top.position, in cp);
                vh.AddVert(bottom);
                vh.AddVert(top);

                if (col == 0)
                    continue;

                int index = start + (col - 1) * 2;
                vh.AddTriangle(index, index + 1, index + 3);
                vh.AddTriangle(index + 3, index + 2, index);
            }
        }

        /// <summary>
        /// Deforms one source-local vertex in place using the supplied cylindrical curve parameters.
        /// </summary>
        /// <param name="p">Original source-local position, replaced with the curved source-local position.</param>
        /// <param name="cp">Curve geometry and transforms between source-local and canvas-local space.</param>
        public static void CurveVertex(ref Vector3 p, in CurveParams cp)
        {
            if (cp.Radius == 0 || cp.HalfWidth <= 0)
                return;

            Vector3 point = cp.ToCanvas.MultiplyPoint3x4(p);
            float angle = (point.x - cp.CenterX) / cp.HalfWidth * cp.HalfAngleRad;
            point.x = cp.CenterX + Mathf.Sin(angle) * cp.Radius;
            float halfSine = Mathf.Sin(angle * 0.5f);
            point.z -= 2 * halfSine * halfSine * cp.Radius * cp.ZSign;
            Vector3 curved = cp.ToLocal.MultiplyPoint3x4(point);
           
            if (IsFinite(curved.x) && IsFinite(curved.y) && IsFinite(curved.z))
                p = curved;
        }

        static UIVertex LerpVertex(UIVertex a, UIVertex b, float t)
        {
            return new UIVertex
            {
                position = Vector3.LerpUnclamped(a.position, b.position, t),
                normal = Vector3.LerpUnclamped(a.normal, b.normal, t),
                tangent = Vector4.LerpUnclamped(a.tangent, b.tangent, t),
                color = Color32.Lerp(a.color, b.color, t),
                uv0 = Vector4.LerpUnclamped(a.uv0, b.uv0, t),
                uv1 = Vector4.LerpUnclamped(a.uv1, b.uv1, t),
                uv2 = Vector4.LerpUnclamped(a.uv2, b.uv2, t),
                uv3 = Vector4.LerpUnclamped(a.uv3, b.uv3, t)
            };
        }
        #endregion

        #region Public API
        /// <summary>
        /// Discovers new Graphics while the effect is active, including inactive descendants and nested Canvases.
        /// Call after spawning/reparenting a subtree to avoid the fallback interval.
        /// </summary>
        public void RefreshManagedGraphics()
        {
            refreshPending = true;

            if (!IsEffective || refreshing || CanvasUpdateRegistry.IsRebuildingLayout() || CanvasUpdateRegistry.IsRebuildingGraphics())
                return;

            refreshing = true;
            refreshPending = false;
            lastScanFrame = Time.frameCount;

            try
            {
                for (int i = managed.Count - 1; i >= 0; i--)
                {
                    if (managed[i] == null)
                        managed.RemoveAt(i);
                    else
                        managed[i].ResolveOwner();
                }

                GetComponentsInChildren(true, graphicBuffer);
                for (int i = 0; i < graphicBuffer.Count; i++)
                {
                    Graphic source = graphicBuffer[i];
                    if (source == null || source is TMP_SubMeshUI || source.GetComponentInParent<CurvedCanvas>(true) != this)
                        continue;

                    if (!source.TryGetComponent(out CurvedCanvasGraphic modifier))
                    {
                        modifier = source.gameObject.AddComponent<CurvedCanvasGraphic>();
                        modifier.hideFlags = HideFlags.HideInInspector | HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
                    }
                    modifier.Initialize(this);
                }
            }
            finally
            {
                graphicBuffer.Clear(); // Do not retain references to another owner's subtree
                refreshing = false;
            }
        }

        /// <summary>
        /// Invalidate all managed meshes for the next canvas update.
        /// </summary>
        public void SetAllMeshesDirty() => forceDirty = true;
        #endregion

#if UNITY_EDITOR
        [Tooltip("Preview in Scene/Game views and Prefab Mode.")]
        public bool previewInEditor;

        void OnValidate()
        {
            // OnValidate may run while loading, including on a worker thread. No scene changes here.
            curveAngle = SanitizeAngle(curveAngle);
            segments = Mathf.Clamp(segments, 2, 64);
            rescanIntervalFrames = Mathf.Clamp(rescanIntervalFrames, 1, 120);
            forceDirty = refreshPending = true;
        }

        void OnEditorHierarchyChanged()
        {
            if (Application.IsPlaying(gameObject))
                return;

            refreshPending = true;

            if (IsEffective)
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
        }

        void QueuePreviewCleanup()
        {
            if (Application.IsPlaying(gameObject))
                return;
            for (int i = managed.Count - 1; i >= 0; i--)
                if (managed[i] != null)
                    managed[i].QueueEditorCleanup();
        }
#endif
    }
}