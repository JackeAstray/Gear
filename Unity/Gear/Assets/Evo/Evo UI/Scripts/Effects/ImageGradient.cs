using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI
{
    /// <summary>
    /// Modifies UI graphics by overlaying a highly configurable gradient effect.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    [AddComponentMenu("Evo/UI/Effects/Image Gradient"), HelpURL(Constants.HelpUrl + "effects/image-gradient")]
    public class ImageGradient : BaseMeshEffect
    {
        [Header("Gradient")]
        public Gradient gradientEffect = new()
        {
            colorKeys = new GradientColorKey[]
            {
                new(Color.black, 0),
                new(Color.white, 1)
            }
        };

        [Header("Settings")]
        public Type gradientType = Type.Horizontal;
        public BlendMode blendMode = BlendMode.Multiply;
        [Range(-180f, 180f)] public float angle = 0f;
        [Range(-1f, 1f)] public float effectOffset = 0f;
        [Range(0.1f, 10f)] public float zoom = 1f;
        public bool reverse = false;

        // Cache
        Gradient cachedGradient;
        Gradient twoColorGradient;
        readonly GradientColorKey[] twoColorKeys = new GradientColorKey[2];
        readonly GradientAlphaKey[] twoAlphaKeys = new GradientAlphaKey[2];
        const int MaxVertexCount = 64999;

        // Gradient Key Getters
        GradientColorKey[] cachedColorKeys = Array.Empty<GradientColorKey>();
        GradientAlphaKey[] cachedAlphaKeys = Array.Empty<GradientAlphaKey>();

        // Reusable Buffers
        readonly List<float> stops = new(32);
        readonly List<float> rawKeys = new(18);
        readonly List<UIVertex> vertexList = new();
        readonly List<UIVertex> currentTriangles = new();
        readonly List<UIVertex> nextTriangles = new();
        readonly List<UIVertex> finalTriangles = new();

        // Enums
        public enum Type { Horizontal, Vertical }
        public enum BlendMode { Multiply = 0, Add = 1, Subtract = 2, Override = 3 }

        protected override void Awake()
        {
            base.Awake();
            CacheGradientKeys();
        }

        public override void ModifyMesh(VertexHelper helper)
        {
            if (!IsActive() || helper.currentVertCount == 0 || gradientEffect == null)
                return;

            // Replacing the public gradient reference must also refresh its split positions
            if (!ReferenceEquals(cachedGradient, gradientEffect))
                CacheGradientKeys();

            vertexList.Clear();
            helper.GetUIVertexStream(vertexList);

            // Calculate effective angle
            float effectiveAngle = angle;
            if (gradientType == Type.Vertical)
                effectiveAngle += 90f;

            // Rotate vertices to align with Horizontal axis for simpler math
            Rect bounds = GetBounds(vertexList);
            Vector2 center = bounds.center;
            RotateVertices(vertexList, center, -effectiveAngle);

            // Recalculate bounds in the rotated space
            Rect rotatedBounds = GetBounds(vertexList);
            float min = rotatedBounds.xMin;
            float w = rotatedBounds.width;
            float safeZoom = float.IsNaN(zoom) ? 1f : Mathf.Clamp(zoom, 0.1f, 10f);
            float width = w <= 0f ? 0f : 1f / w / safeZoom;
            float zoomOffset = (1 - (1 / safeZoom)) * 0.5f;
            float offset = (effectOffset * (1 - zoomOffset)) - zoomOffset;

            // Keep the result in our reusable lists until the final upload
            List<UIVertex> result = SplitTrianglesAtGradientStops(vertexList, rotatedBounds, min, width, offset);
            if (result == null)
            {
                // Preserve the indexed input if subdivision would exceed uGUI's vertex limit
                ColorOriginalMesh(helper, center, -effectiveAngle, min, width, offset);
                return;
            }

            RotateVertices(result, center, effectiveAngle);
            helper.Clear();
            helper.AddUIVertexTriangleStream(result);
        }

        void CacheGradientKeys()
        {
            cachedGradient = gradientEffect;
            cachedColorKeys = gradientEffect != null ? gradientEffect.colorKeys : Array.Empty<GradientColorKey>();
            cachedAlphaKeys = gradientEffect != null ? gradientEffect.alphaKeys : Array.Empty<GradientAlphaKey>();
        }

        void ColorOriginalMesh(VertexHelper helper, Vector2 center, float angleVal, float min, float width, float offset)
        {
            Quaternion rotation = Mathf.Abs(angleVal) < 0.001f ? Quaternion.identity : Quaternion.Euler(0, 0, angleVal);
            Vector3 origin = center;
            UIVertex vertex = default;
            for (int i = 0; i < helper.currentVertCount; i++)
            {
                helper.PopulateUIVertex(ref vertex, i);
                float x = (rotation * (vertex.position - origin) + origin).x;
                vertex.color = BlendColor(vertex.color, EvaluateColor(x, min, width, offset));
                helper.SetUIVertex(vertex, i);
            }
        }

        void RotateVertices(List<UIVertex> vertices, Vector2 center, float angleVal)
        {
            if (Mathf.Abs(angleVal) < 0.001f)
                return;

            Quaternion q = Quaternion.Euler(0, 0, angleVal);
            Vector3 c = center;
            int count = vertices.Count;
            for (int i = 0; i < count; i++)
            {
                UIVertex v = vertices[i];
                Vector3 pos = v.position;
                pos -= c;
                pos = q * pos;
                pos += c;
                v.position = pos;
                vertices[i] = v;
            }
        }

        Rect GetBounds(List<UIVertex> vertices)
        {
            if (vertices.Count == 0)
                return new Rect();

            float left = vertices[0].position.x;
            float right = left;
            float bottom = vertices[0].position.y;
            float top = bottom;
            int count = vertices.Count;
            for (int i = 1; i < count; i++)
            {
                float x = vertices[i].position.x;
                float y = vertices[i].position.y;

                if (x > right)
                    right = x;
                else if (x < left)
                    left = x;

                if (y > top)
                    top = y;
                else if (y < bottom)
                    bottom = y;
            }

            return new Rect(left, bottom, right - left, top - bottom);
        }

        List<UIVertex> SplitTrianglesAtGradientStops(List<UIVertex> inputVerts, Rect bounds, float min, float width, float offset)
        {
            if (inputVerts.Count > MaxVertexCount)
                return null;

            FindStops(bounds, min, width, offset);

            // If no stops are needed, color without allocating another triangle buffer
            if (stops.Count == 0)
            {
                for (int i = 0; i < inputVerts.Count; i += 3)
                {
                    UIVertex v1 = inputVerts[i];
                    UIVertex v2 = inputVerts[i + 1];
                    UIVertex v3 = inputVerts[i + 2];
                    ColorTriangle(ref v1, ref v2, ref v3, min, width, offset);
                    inputVerts[i] = v1;
                    inputVerts[i + 1] = v2;
                    inputVerts[i + 2] = v3;
                }
                return inputVerts;
            }

            // Reuse pooled lists
            currentTriangles.Clear();
            currentTriangles.AddRange(inputVerts);
            nextTriangles.Clear();
            finalTriangles.Clear();

            // Toggle logic for swapping lists
            List<UIVertex> activeList = currentTriangles;
            List<UIVertex> nextList = nextTriangles;

            int stopCount = stops.Count;
            for (int s = 0; s < stopCount; s++)
            {
                float stopValue = stops[s];
                nextList.Clear();

                int activeCount = activeList.Count;
                for (int i = 0; i < activeCount; i += 3)
                {
                    UIVertex v1 = activeList[i];
                    UIVertex v2 = activeList[i + 1];
                    UIVertex v3 = activeList[i + 2];

                    bool v1Left = v1.position.x <= stopValue;
                    bool v2Left = v2.position.x <= stopValue;
                    bool v3Left = v3.position.x <= stopValue;

                    if (v1Left && v2Left && v3Left)
                        AddTriangleAndColor(finalTriangles, v1, v2, v3, min, width, offset);
                    else if (!v1Left && !v2Left && !v3Left)
                    {
                        nextList.Add(v1);
                        nextList.Add(v2);
                        nextList.Add(v3);
                    }
                    else
                        PerformTriangleSplit(v1, v2, v3, v1Left, v2Left, v3Left, stopValue, finalTriangles, nextList, min, width, offset);

                    // Include the unprocessed triangles in the budget before continuing
                    if (finalTriangles.Count + nextList.Count + activeCount - i - 3 > MaxVertexCount)
                        return null;
                }

                // Swap lists (Allocation-free Tuple swap)
                (nextList, activeList) = (activeList, nextList);
            }

            // Remaining triangles are on the far right
            int remainingCount = activeList.Count;
            for (int i = 0; i < remainingCount; i += 3)
                AddTriangleAndColor(finalTriangles, activeList[i], activeList[i + 1], activeList[i + 2], min, width, offset);

            return finalTriangles;
        }

        void PerformTriangleSplit(UIVertex v1, UIVertex v2, UIVertex v3, bool v1Left, bool v2Left, bool v3Left,
            float stopValue, List<UIVertex> leftList, List<UIVertex> rightList, float min, float width, float offset)
        {
            UIVertex lone, other1, other2;
            bool loneIsLeft;

            if (v1Left == v2Left)
            {
                lone = v3;
                other1 = v1;
                other2 = v2;
                loneIsLeft = v3Left;
            }
            else if (v1Left == v3Left)
            {
                lone = v2;
                other1 = v3;
                other2 = v1;
                loneIsLeft = v2Left;
            }
            else
            {
                lone = v1;
                other1 = v2;
                other2 = v3;
                loneIsLeft = v1Left;
            }

            UIVertex split1 = CreateSplitVertex(lone, other1, stopValue);
            UIVertex split2 = CreateSplitVertex(lone, other2, stopValue);

            if (loneIsLeft)
            {
                AddTriangleAndColor(leftList, lone, split1, split2, min, width, offset);
                rightList.Add(split1); rightList.Add(other1); rightList.Add(other2);
                rightList.Add(split1); rightList.Add(other2); rightList.Add(split2);
            }
            else
            {
                AddTriangleAndColor(leftList, split1, other1, other2, min, width, offset);
                AddTriangleAndColor(leftList, split1, other2, split2, min, width, offset);
                rightList.Add(lone); rightList.Add(split1); rightList.Add(split2);
            }
        }

        void AddTriangleAndColor(List<UIVertex> list, UIVertex v1, UIVertex v2, UIVertex v3, float min, float width, float offset)
        {
            // A cut through an existing vertex/edge can produce a zero-area triangle
            if (Vector3.Cross(v2.position - v1.position, v3.position - v1.position).sqrMagnitude == 0f)
                return;

            ColorTriangle(ref v1, ref v2, ref v3, min, width, offset);
            list.Add(v1);
            list.Add(v2);
            list.Add(v3);
        }

        void ColorTriangle(ref UIVertex v1, ref UIVertex v2, ref UIVertex v3, float min, float width, float offset)
        {
            if (gradientEffect.mode == GradientMode.Fixed)
            {
                Color color = EvaluateColor((v1.position.x + v2.position.x + v3.position.x) / 3f, min, width, offset);
                v1.color = BlendColor(v1.color, color);
                v2.color = BlendColor(v2.color, color);
                v3.color = BlendColor(v3.color, color);
            }
            else
            {
                v1.color = BlendColor(v1.color, EvaluateColor(v1.position.x, min, width, offset));
                v2.color = BlendColor(v2.color, EvaluateColor(v2.position.x, min, width, offset));
                v3.color = BlendColor(v3.color, EvaluateColor(v3.position.x, min, width, offset));
            }
        }

        Color EvaluateColor(float x, float min, float width, float offset)
        {
            float t = (x - min) * width - offset;
            return gradientEffect.Evaluate(Mathf.Clamp01(reverse ? 1f - t : t));
        }

        void FindStops(Rect bounds, float min, float width, float offset)
        {
            stops.Clear();

            if (width <= 0f)
                return;

            rawKeys.Clear();
            rawKeys.Add(0f);
            rawKeys.Add(1f);
          
            foreach (var key in cachedColorKeys)
                rawKeys.Add(key.time);
            foreach (var key in cachedAlphaKeys)
                rawKeys.Add(key.time);
           
            SortStops(rawKeys);

            for (int i = 0; i < rawKeys.Count; i++)
            {
                float t1 = rawKeys[i];
                AddStop(t1, bounds, min, width, offset);
            
                if (gradientEffect.mode != GradientMode.PerceptualBlend || i + 1 == rawKeys.Count)
                    continue;

                float t2 = rawKeys[i + 1];
                float dist = t2 - t1;
                if (dist <= 0.05f)
                    continue;

                Color c1 = gradientEffect.Evaluate(t1);
                Color c2 = gradientEffect.Evaluate(t2);
                Color midColor = gradientEffect.Evaluate(t1 + dist * 0.5f);
                if (!IsNonLinear(midColor, Color.Lerp(c1, c2, 0.5f)))
                    continue;

                int steps = Mathf.FloorToInt(dist / 0.1f);
                for (int j = 1; j <= steps; j++)
                    AddStop(t1 + dist * ((float)j / (steps + 1)), bounds, min, width, offset);
            }

            SortStops(stops);
            for (int i = stops.Count - 1; i > 0; i--)
            {
                // Do not merge distinct, closely spaced gradient keys
                if (stops[i] == stops[i - 1])
                    stops.RemoveAt(i);
            }
        }

        void AddStop(float time, Rect bounds, float min, float width, float offset)
        {
            // Invert the exact same mapping used by EvaluateColor
            float x = min + ((reverse ? 1f - time : time) + offset) / width;
            if (x > bounds.xMin && x < bounds.xMax)
                stops.Add(x);
        }

        static void SortStops(List<float> values)
        {
            for (int i = 1; i < values.Count; i++)
            {
                float value = values[i];
                int j = i - 1;
                while (j >= 0 && values[j] > value)
                {
                    values[j + 1] = values[j];
                    j--;
                }
                values[j + 1] = value;
            }
        }

        bool IsNonLinear(Color a, Color b)
        {
            return (Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) + Mathf.Abs(a.a - b.a)) > 0.05f;
        }

        UIVertex CreateSplitVertex(UIVertex vertex1, UIVertex vertex2, float stop)
        {
            float sx = vertex1.position.x - stop;
            float dx = vertex1.position.x - vertex2.position.x;
            float ratio = dx == 0f ? 0f : Mathf.Clamp01(sx / dx);
            Vector3 position = Vector3.Lerp(vertex1.position, vertex2.position, ratio);
            position.x = stop;

            UIVertex splitVertex = new()
            {
                position = position,
                normal = Vector3.Lerp(vertex1.normal, vertex2.normal, ratio),
                tangent = Vector4.Lerp(vertex1.tangent, vertex2.tangent, ratio),
                uv0 = Vector4.Lerp(vertex1.uv0, vertex2.uv0, ratio),
                uv1 = Vector4.Lerp(vertex1.uv1, vertex2.uv1, ratio),
                uv2 = Vector4.Lerp(vertex1.uv2, vertex2.uv2, ratio),
                uv3 = Vector4.Lerp(vertex1.uv3, vertex2.uv3, ratio),
                color = Color.Lerp(vertex1.color, vertex2.color, ratio)
            };

            return splitVertex;
        }

        Color BlendColor(Color colorA, Color colorB)
        {
            return blendMode switch
            {
                BlendMode.Add => new Color(Mathf.Clamp01(colorA.r + colorB.r),
                                           Mathf.Clamp01(colorA.g + colorB.g),
                                           Mathf.Clamp01(colorA.b + colorB.b),
                                           Mathf.Clamp01(colorA.a + colorB.a)),

                BlendMode.Subtract => new Color(Mathf.Clamp01(colorA.r - colorB.r),
                                                Mathf.Clamp01(colorA.g - colorB.g),
                                                Mathf.Clamp01(colorA.b - colorB.b),
                                                colorA.a),

                BlendMode.Override => new Color(colorB.r, colorB.g, colorB.b, colorB.a * colorA.a),
                _ => colorA * colorB,
            };
        }

        void MarkVerticesDirty()
        {
            if (graphic != null)
                graphic.SetVerticesDirty();
        }

        /// <summary>
        /// Animates gradient keys using the current Styler transition scope.
        /// </summary>
        internal void TransitionTo(Gradient target)
        {
            if (ColorTransition.Duration <= 0f || gradientEffect == null || target == null)
            {
                ColorTransition.Cancel(this);
                SetGradient(target);
                return;
            }

            var fromColors = gradientEffect.colorKeys;
            var fromAlpha = gradientEffect.alphaKeys;
            var toColors = target.colorKeys;
            var toAlpha = target.alphaKeys;
            var colors = new GradientColorKey[Mathf.Max(fromColors.Length, toColors.Length)];
            var alpha = new GradientAlphaKey[Mathf.Max(fromAlpha.Length, toAlpha.Length)];
            var blended = new Gradient { mode = target.mode, colorSpace = target.colorSpace };
            
            ColorTransition.Animate(this, 0, t =>
            {
                if (t >= 1f)
                {
                    SetGradient(target);
                    return;
                }

                for (int i = 0; i < colors.Length; i++)
                {
                    var a = fromColors[Mathf.Min(i, fromColors.Length - 1)];
                    var b = toColors[Mathf.Min(i, toColors.Length - 1)];
                    colors[i] = new GradientColorKey(Color.LerpUnclamped(a.color, b.color, t), Mathf.Lerp(a.time, b.time, t));
                }

                for (int i = 0; i < alpha.Length; i++)
                {
                    var a = fromAlpha[Mathf.Min(i, fromAlpha.Length - 1)];
                    var b = toAlpha[Mathf.Min(i, toAlpha.Length - 1)];
                    alpha[i] = new GradientAlphaKey(Mathf.Lerp(a.alpha, b.alpha, t), Mathf.Lerp(a.time, b.time, t));
                }

                blended.SetKeys(colors, alpha);
                gradientEffect = blended;
                cachedGradient = blended;
                cachedColorKeys = colors;
                cachedAlphaKeys = alpha;
                MarkVerticesDirty();
            });
        }

        /// <summary>
        /// Refresh after changing gradient keys in place.
        /// Mesh rebuilds reuse buffers, allocating only when capacity grows;
        /// SetGradientColors reuses its gradient and arrays after first use.
        /// </summary>
        public void UpdateGradient()
        {
            CacheGradientKeys();
            MarkVerticesDirty();
        }

        /// <summary>
        /// Copies the supplied gradient into this component's serialized gradient.
        /// Later changes to the source do not affect this component. Null clears the effect.
        /// </summary>
        public void SetGradient(Gradient newGradient)
        {
            if (newGradient == null) { gradientEffect = null; }
            else
            {
                gradientEffect ??= new Gradient();
                gradientEffect.SetKeys(newGradient.colorKeys, newGradient.alphaKeys);
                gradientEffect.mode = newGradient.mode;
                gradientEffect.colorSpace = newGradient.colorSpace;
            }

            UpdateGradient();
        }

        /// <summary>
        /// Sets a two-color gradient, reusing its gradient and key arrays after first use.
        /// </summary>
        public void SetGradientColors(Color startColor, Color endColor)
        {
            // Keep our reusable gradient separate from gradients supplied by callers
            twoColorGradient ??= new Gradient();
            twoColorKeys[0] = new GradientColorKey(startColor, 0f);
            twoColorKeys[1] = new GradientColorKey(endColor, 1f);
            twoAlphaKeys[0] = new GradientAlphaKey(startColor.a, 0f);
            twoAlphaKeys[1] = new GradientAlphaKey(endColor.a, 1f);
            twoColorGradient.mode = GradientMode.Blend;
            twoColorGradient.SetKeys(twoColorKeys, twoAlphaKeys);
            gradientEffect = twoColorGradient;
            cachedGradient = twoColorGradient;
            cachedColorKeys = twoColorKeys;
            cachedAlphaKeys = twoAlphaKeys;
            MarkVerticesDirty();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            CacheGradientKeys();
            base.OnValidate();
        }
#endif
    }
}