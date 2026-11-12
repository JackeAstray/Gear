using UnityEngine;

namespace Evo.UI
{
    /// <summary>
    /// Type declarations and CPU shape definitions used by <see cref="ProceduralShape"/>.
    /// </summary>
    public partial class ProceduralShape
    {
        #region Enums
        /// <summary>
        /// Determines where a layer obtains its color.
        /// </summary>
        public enum ColorMode { Base = 0, Custom = 1, Gradient = 2, Styler = 3, StylerGradient = 4 }

        /// <summary>
        /// Determines how gradient colors are distributed.
        /// </summary>
        public enum GradientType { Linear = 0, Radial = 1, Angular = 2 }

        /// <summary>
        /// Determines which coordinates a gradient uses.
        /// </summary>
        public enum GradientSpace { Local = 0, ScreenSpace = 1 }

        /// <summary>
        /// Determines whether pattern tiles use an SDF primitive or an atlas sprite.
        /// </summary>
        public enum PatternSource { Shape = 0, Sprite = 1 }

        /// <summary>
        /// Determines how the rendered shape is progressively clipped.
        /// </summary>
        public enum ClipMethod { None, Horizontal, Vertical, Radial360 }

        /// <summary>
        /// Determines how corner-radius values are interpreted.
        /// </summary>
        public enum RadiusMode { Pixels = 0, Percentage = 1 }

        /// <summary>
        /// Determines how the shape and optional sprite fit the drawing rect.
        /// </summary>
        public enum ScaleMode { Stretch = 0, Cover = 1, Fit = 2 }

        /// <summary>
        /// Determines whether raycasts use the rect or the procedural shape.
        /// </summary>
        public enum RaycastMode { None = 0, Standard = 1, Shape = 2 }

        /// <summary>
        /// Determines whether rectangle radii are resolved from another shape.
        /// </summary>
        public enum RadiusSyncMode { None = 0, MatchParent = 1, MatchTarget = 2, MatchAncestor = 3 }

        /// <summary>
        /// Identifies the SDF primitive rendered by this graphic.
        /// </summary>
        public enum ShapeType
        {
            Rectangle = 0,
            Ellipse = 1,
            Polygon = 2,
            Star = 3,
            Heart = 4,
            Chevron = 5
        }
        #endregion

        #region Shape Definitions
        public const int MinPolygonSides = 3;
        public const int MaxPolygonSides = 8;

        // The 4-bit shader ID reserves 10..15 for fixed polygon variants.
        // Other primitives use their enum value; 6..9 are available for new shapes.
        // NOTE TO MYSELF: Keep these IDs in sync with ProceduralShape.cginc.
        const int ShaderPolygonBaseID = 10;

        internal int ShaderShapeID => GetShaderShapeID(shape, polygonSides);

        static int GetShaderShapeID(ShapeType type, int sides) => type == ShapeType.Polygon
            ? ShaderPolygonBaseID + Mathf.Clamp(sides, MinPolygonSides, MaxPolygonSides) - MinPolygonSides
            : (int)type;

        static readonly Vector2[] starVertices =
        {
            new(0f, 1f),
            new(-0.236068f, 0.236068f),
            new(-1f, 0.236068f),
            new(-0.381966f, -0.236068f),
            new(-0.618034f, -1f),
            new(0f, -0.527864f),
            new(0.618034f, -1f),
            new(0.381966f, -0.236068f),
            new(1f, 0.236068f),
            new(0.236068f, 0.236068f)
        };

        static readonly Vector2[] polygon3Vertices =
        {
            new(-1f, -1f),
            new(1f, -1f),
            new(0f, 1f)
        };

        static readonly Vector2[] polygon4Vertices =
        {
            new(0f, -1f),
            new(1f, 0f),
            new(0f, 1f),
            new(-1f, 0f)
        };

        static readonly Vector2[] polygon5Vertices =
        {
            new(-0.618034f, -1f),
            new(0.618034f, -1f),
            new(1f, 0.236068f),
            new(0f, 1f),
            new(-1f, 0.236068f)
        };

        static readonly Vector2[] polygon6Vertices =
        {
            new(-0.5f, -1f),
            new(0.5f, -1f),
            new(1f, 0f),
            new(0.5f, 1f),
            new(-0.5f, 1f),
            new(-1f, 0f)
        };

        static readonly Vector2[] polygon7Vertices =
        {
            new(-0.445042f, -1f),
            new(0.445042f, -1f),
            new(1f, -0.286208f),
            new(0.801938f, 0.603875f),
            new(0f, 1f),
            new(-0.801938f, 0.603875f),
            new(-1f, -0.286208f)
        };

        static readonly Vector2[] polygon8Vertices =
        {
            new(-0.414214f, -1f),
            new(0.414214f, -1f),
            new(1f, -0.414214f),
            new(1f, 0.414214f),
            new(0.414214f, 1f),
            new(-0.414214f, 1f),
            new(-1f, 0.414214f),
            new(-1f, -0.414214f)
        };

        // Walked counterclockwise from the lower left by the shader's dash path
        static readonly Vector2[] chevronVertices =
        {
            new(-1f, -1f),
            new(0f, -1f),
            new(1f, 0f),
            new(0f, 1f),
            new(-1f, 1f),
            new(0f, 0f)
        };

        Vector2[] GetPolygonVertices()
        {
            return Mathf.Clamp(polygonSides, MinPolygonSides, MaxPolygonSides) switch
            {
                3 => polygon3Vertices,
                4 => polygon4Vertices,
                5 => polygon5Vertices,
                6 => polygon6Vertices,
                7 => polygon7Vertices,
                _ => polygon8Vertices,
            };
        }

        // ProceduralShape.cginc reads the corners from shader globals: WebGPU copies static const shader arrays
        // into every use per pixel. Polygons go back to back by side count, then the chevron.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void SetShaderVertices()
        {
            Vector2[][] tables = { polygon3Vertices, polygon4Vertices, polygon5Vertices, polygon6Vertices, polygon7Vertices, polygon8Vertices, chevronVertices };
            var polygonCorners = new Vector4[39];
            int count = 0;

            foreach (Vector2[] table in tables)
            {
                foreach (Vector2 corner in table)
                    polygonCorners[count++] = corner;
            }

            var starCorners = new Vector4[starVertices.Length];
            for (int i = 0; i < starVertices.Length; i++)
                starCorners[i] = starVertices[i];

            Shader.SetGlobalVectorArray("_EvoPolygonVertices", polygonCorners);
            Shader.SetGlobalVectorArray("_EvoStarVertices", starCorners);
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        static void SetShaderVerticesInEditor() => SetShaderVertices();
#endif

        internal bool HasShapeTransform => shapeAngle != 0f || shapeSkew.x != 0f || shapeSkew.y != 0f;

        static Vector2 ClampShapeSkew(Vector2 value) => new(Mathf.Clamp(value.x, -60f, 60f), Mathf.Clamp(value.y, -60f, 60f));

        // Shared by mesh generation, raycasts, masks, operations, and pointer effects.
        // Sequential shears stay invertible when both axes are used. The shader is unchanged.
        internal Matrix4x4 GetShapeTransform(Vector2 center, bool inverse = false)
        {
            Matrix4x4 matrix = Matrix4x4.identity;
            if (!HasShapeTransform)
                return matrix;

            float radians = shapeAngle * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            float skewX = shapeSkew.x != 0f ? Mathf.Tan(shapeSkew.x * Mathf.Deg2Rad) : 0f;
            float skewY = shapeSkew.y != 0f ? Mathf.Tan(shapeSkew.y * Mathf.Deg2Rad) : 0f;
            matrix.m00 = cos - sin * skewY;
            matrix.m01 = matrix.m00 * skewX - sin;
            matrix.m10 = sin + cos * skewY;
            matrix.m11 = matrix.m10 * skewX + cos;

            if (inverse)
            {
                // Rotation and each shear have determinant one
                float m00 = matrix.m00;
                matrix.m00 = matrix.m11;
                matrix.m01 = -matrix.m01;
                matrix.m10 = -matrix.m10;
                matrix.m11 = m00;
            }

            matrix.m03 = center.x - matrix.m00 * center.x - matrix.m01 * center.y;
            matrix.m13 = center.y - matrix.m10 * center.x - matrix.m11 * center.y;
            return matrix;
        }
        #endregion

        #region CPU Shape Distance Functions
        /// <summary>
        /// Evaluates the CPU counterpart of the shader SDF for precise raycast filtering.
        /// </summary>
        float GetShapeSDF(Vector2 p, Vector2 b, Vector4 r, bool squircle)
        {
            if (shape == ShapeType.Rectangle)
                return GetRoundedRectSDF(p, b, r, squircle);
            if (shape == ShapeType.Ellipse)
                return GetEllipseSDF(p, b);

            float radius = Mathf.Clamp(r.x, 0f, Mathf.Min(b.x, b.y));
            Vector2 innerSize = new(Mathf.Max(b.x - radius, 0.0001f), Mathf.Max(b.y - radius, 0.0001f));
            float distance;

            switch (shape)
            {
                case ShapeType.Polygon: distance = GetPolygonSDF(p, innerSize, GetPolygonVertices()); break;
                case ShapeType.Star: distance = GetPolygonSDF(p, innerSize, starVertices); break;
                case ShapeType.Heart: distance = GetHeartSDF(p, innerSize); break;
                case ShapeType.Chevron: distance = GetChevronSDF(p, innerSize); break;
                default: return GetRoundedRectSDF(p, b, r, squircle);
            }

            return distance - radius;
        }

        static float GetRoundedRectSDF(Vector2 p, Vector2 b, Vector4 r, bool squircle)
        {
            float radius;

            if (p.x > 0 && p.y > 0) { radius = r.x; } // TopRight
            else if (p.x > 0 && p.y <= 0) { radius = r.y; } // BottomRight
            else if (p.x <= 0 && p.y <= 0) { radius = r.w; } // BottomLeft
            else { radius = r.z; } // TopLeft

            Vector2 q = new(Mathf.Abs(p.x) - b.x + radius, Mathf.Abs(p.y) - b.y + radius);

            float maxQx = Mathf.Max(q.x, 0f);
            float maxQy = Mathf.Max(q.y, 0f);
            float length;

            if (!squircle) { length = Mathf.Sqrt(maxQx * maxQx + maxQy * maxQy); }
            else
            {
                float sqX = maxQx * maxQx;
                float sqY = maxQy * maxQy;
                length = Mathf.Sqrt(Mathf.Sqrt(sqX * sqX + sqY * sqY));
            }

            return Mathf.Min(Mathf.Max(q.x, q.y), 0.0f) + length - radius;
        }

        static float GetEllipseSDF(Vector2 p, Vector2 b)
        {
            b.x = Mathf.Max(b.x, 0.0001f);
            b.y = Mathf.Max(b.y, 0.0001f);

            float px = p.x / b.x;
            float py = p.y / b.y;
            float k0 = Mathf.Sqrt(px * px + py * py);

            float bx2 = b.x * b.x;
            float by2 = b.y * b.y;
            float qx = p.x / bx2;
            float qy = p.y / by2;
            float k1 = Mathf.Sqrt(qx * qx + qy * qy);

            if (k1 < 0.0001f)
                return -Mathf.Min(b.x, b.y);

            return k0 * (k0 - 1f) / k1;
        }

        static float GetHeartSDF(Vector2 p, Vector2 b)
        {
            const float lobeRadius = 0.353553391f;
            const float halfWidth = 0.603553391f;
            const float height = 1.103553391f;

            b.x = Mathf.Max(b.x, 0.0001f);
            b.y = Mathf.Max(b.y, 0.0001f);

            Vector2 toNormalized = new(halfWidth / b.x, height * 0.5f / b.y);
            Vector2 q = new(Mathf.Abs(p.x) * toNormalized.x, (p.y + b.y) * toNormalized.y);
            float distance;

            if (q.x + q.y > 1f)
            {
                Vector2 toLobe = q - new Vector2(0.25f, 0.75f);
                distance = toLobe.magnitude - lobeRadius;
            }
            else
            {
                Vector2 toNotch = q - new Vector2(0f, 1f);
                float diagonal = 0.5f * Mathf.Max(q.x + q.y, 0f);
                Vector2 toSide = q - new Vector2(diagonal, diagonal);
                distance = Mathf.Sqrt(Mathf.Min(toNotch.sqrMagnitude, toSide.sqrMagnitude));
                distance *= q.x >= q.y ? 1f : -1f;
            }

            // Convert the normalized SDF back to a conservative pixel-space distance.
            return distance / Mathf.Max(toNormalized.x, toNormalized.y);
        }

        static float GetChevronSDF(Vector2 p, Vector2 b)
        {
            // Reflect onto the upper half: only two diagonals and the end can be nearest.
            p.y = Mathf.Abs(p.y);
            Vector2 edge = new(-b.x, b.y);
            Vector2 outer = p - new Vector2(b.x, 0f);
            Vector2 inner = p - edge * Mathf.Clamp01(Vector2.Dot(p, edge) / edge.sqrMagnitude);
            outer -= edge * Mathf.Clamp01(Vector2.Dot(outer, edge) / edge.sqrMagnitude);
            Vector2 end = new(p.x - Mathf.Clamp(p.x, -b.x, 0f), p.y - b.y);
            float distance = Mathf.Sqrt(Mathf.Min(Mathf.Min(inner.sqrMagnitude, outer.sqrMagnitude), end.sqrMagnitude));
            float side = p.x * b.y + p.y * b.x;
            return distance * (p.y <= b.y && side >= 0f && side <= b.x * b.y ? -1f : 1f);
        }

        static float GetPolygonSDF(Vector2 p, Vector2 b, Vector2[] vertices)
        {
            float minDistanceSq = float.MaxValue;
            bool inside = false;

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector2 current = Vector2.Scale(vertices[i], b);
                Vector2 next = Vector2.Scale(vertices[(i + 1) % vertices.Length], b);
                AddPolygonEdge(p, current, next, ref minDistanceSq, ref inside);
            }

            return Mathf.Sqrt(Mathf.Max(minDistanceSq, 0f)) * (inside ? -1f : 1f);
        }

        static void AddPolygonEdge(Vector2 p, Vector2 a, Vector2 b, ref float minDistanceSq, ref bool inside)
        {
            Vector2 edge = b - a;
            Vector2 toPoint = p - a;
            float edgeLengthSq = edge.sqrMagnitude;
            Vector2 nearest = toPoint - edge * Mathf.Clamp01(Vector2.Dot(toPoint, edge) / edgeLengthSq);

            minDistanceSq = Mathf.Min(minDistanceSq, nearest.sqrMagnitude);

            if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                inside = !inside;
        }
        #endregion
    }
}