using UnityEngine;

namespace Gear.Utilities.UI.ImageExtensions
{
    /// <summary>
    /// ImageEx partial part：命中检测（Standard / Shape / None）。
    /// <para>
    /// Shape 模式不读回 GPU，而是按 <c>RM_SDFShapes.cginc</c> 的同一套参数在 C# 里做形状包含测试，
    /// 因此圆角、圆形、星形、四边形等透明区域不会吃掉点击。
    /// </para>
    /// </summary>
    public partial class ImageEx
    {
        /// <summary>
        /// 命中检测模式。只影响点击/悬停，不影响渲染，也不脏化材质。
        /// </summary>
        public RaycastDetectionMode RaycastMode
        {
            get => raycastMode;
            set
            {
                if (raycastMode == value) return;
                raycastMode = value;
            }
        }

        /// <summary>
        /// 重写 Graphic 命中：Shape 模式按当前 SDF 形状判定，None 模式直接不参与。
        /// </summary>
        public override bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (!raycastTarget) return false;

            switch (raycastMode)
            {
                case RaycastDetectionMode.None:
                    return false;
                case RaycastDetectionMode.Shape:
                    break;
                default:
                    return base.IsRaycastLocationValid(screenPoint, eventCamera);
            }

            // 没有画 SDF 形状时（DrawShape=None）仍按基类矩形/Alpha 命中，避免 Shape 模式把普通 ImageEx 吞掉
            if (DrawShape == DrawShape.None)
                return base.IsRaycastLocationValid(screenPoint, eventCamera);

            RectTransform rt = rectTransform;
            if (rt == null) return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPoint, eventCamera, out Vector2 local))
                return false;

            // 与网格生成共用绘制区域：preserveAspect / Sprite padding 下仍然命中的是实际画出来的形状
            Vector4 dims = ImageHelper.GetDrawingDimensionsForHit(preserveAspect, ActiveSprite, canvas, rt);
            float width = dims.z - dims.x;
            float height = dims.w - dims.y;
            if (width <= 0f || height <= 0f) return false;

            Vector2 point = new Vector2(local.x - dims.x, local.y - dims.y);
            return ImageExShapeHitTest.Contains(this, point, new Vector2(width, height));
        }
    }

    /// <summary>
    /// ImageEx 的 SDF 形状包含测试（纯 C#、无分配、不依赖 GPU 读回）。
    /// <para>
    /// 几何语义对齐 <c>RM_SDFShapes.cginc</c>；圆角/尖角按同一组参数近似，
    /// 旋转与翻转按 shader 的 UV 变换顺序处理。命中检测是"是否在形状内"的布尔判定，
    /// 不需要像素级精确的 SDF 距离，因此少量圆角近似不影响点击。
    /// </para>
    /// </summary>
    public static class ImageExShapeHitTest
    {
        // 多边形临时缓冲（UI 主线程调用，不跨线程；最多 17 边 + 余量）
        private const int MaxPolygonPoints = 24;
        private static readonly Vector2[] PolygonBuffer = new Vector2[MaxPolygonPoints];

        /// <summary>
        /// 判断本地坐标点是否落在 ImageEx 当前形状内。
        /// </summary>
        /// <param name="image">目标 ImageEx。</param>
        /// <param name="point">相对绘制区域左下角的本地坐标（像素）。</param>
        /// <param name="size">绘制区域尺寸（像素）。</param>
        public static bool Contains(ImageEx image, Vector2 point, Vector2 size)
        {
            if (image == null || size.x <= 0f || size.y <= 0f) return false;
            if (point.x < 0f || point.y < 0f || point.x > size.x || point.y > size.y) return false;

            // 与 shader 的 UV 变换顺序一致：先翻转，再旋转
            if (image.FlipHorizontal) point.x = size.x - point.x;
            if (image.FlipVertical) point.y = size.y - point.y;

            float rotation = image.ShapeRotation;
            if (!Mathf.Approximately(rotation, 0f))
            {
                float rad = -rotation * Mathf.Deg2Rad;
                float cos = Mathf.Cos(rad);
                float sin = Mathf.Sin(rad);
                Vector2 center = size * 0.5f;
                Vector2 p = point - center;
                point = new Vector2(p.x * cos - p.y * sin, p.x * sin + p.y * cos) + center;
            }

            switch (image.DrawShape)
            {
                case DrawShape.None:
                    return false;
                case DrawShape.Circle:
                    return CircleContains(image, point, size);
                case DrawShape.Triangle:
                    return TriangleContains(point, size);
                case DrawShape.Rectangle:
                    return RectangleContains(image, point, size);
                case DrawShape.Pentagon:
                    return PentagonContains(image, point, size);
                case DrawShape.Hexagon:
                    return HexagonContains(image, point, size);
                case DrawShape.ChamferBox:
                    return ChamferBoxContains(image, point, size);
                case DrawShape.Quadrilateral:
                    return QuadrilateralContains(image, point, size);
                case DrawShape.NStarPolygon:
                    return NStarContains(image, point, size);
                case DrawShape.Heart:
                    return HeartContains(point, size);
                case DrawShape.BlobbyCross:
                    return BlobbyCrossContains(point, size);
                case DrawShape.Squircle:
                    return SquircleContains(image, point, size);
                case DrawShape.NTriangleRounded:
                    return NTriangleRoundedContains(image, point, size);
                default:
                    // 未来新增形状在补测试前先保持矩形命中，避免直接吞掉点击
                    return true;
            }
        }

        private static bool RectangleContains(ImageEx image, Vector2 point, Vector2 size)
        {
            Vector4 radius = image.Rectangle != null
                ? image.Rectangle.ResolveCornerRadius(size)
                : Vector4.zero;

            // 与 RM_RectangleScene 一致：半径不得超过短边一半
            radius = Vector4.Min(radius, Vector4.one * (Mathf.Min(size.x, size.y) * 0.5f));

            float x = point.x;
            float y = point.y;
            float w = size.x;
            float h = size.y;

            // 左下 x、右下 y、右上 z、左上 w（与 _RectangleCornerRadius 一致）
            if (radius.x > 0f && x < radius.x && y < radius.x)
                return (point - new Vector2(radius.x, radius.x)).sqrMagnitude <= radius.x * radius.x;
            if (radius.y > 0f && x > w - radius.y && y < radius.y)
                return (point - new Vector2(w - radius.y, radius.y)).sqrMagnitude <= radius.y * radius.y;
            if (radius.z > 0f && x > w - radius.z && y > h - radius.z)
                return (point - new Vector2(w - radius.z, h - radius.z)).sqrMagnitude <= radius.z * radius.z;
            if (radius.w > 0f && x < radius.w && y > h - radius.w)
                return (point - new Vector2(radius.w, h - radius.w)).sqrMagnitude <= radius.w * radius.w;

            return true;
        }

        private static bool CircleContains(ImageEx image, Vector2 point, Vector2 size)
        {
            CircleImg circle = image.Circle;
            if (circle == null) return true;

            float radius = circle.FitToRect ? Mathf.Min(size.x, size.y) * 0.5f : circle.Radius;
            if (radius <= 0f) return false;
            return (point - size * 0.5f).sqrMagnitude <= radius * radius;
        }

        private static bool TriangleContains(Vector2 point, Vector2 size)
        {
            int count = BuildTriangle(size);
            return PointInPolygon(point, count);
        }

        private static bool PentagonContains(ImageEx image, Vector2 point, Vector2 size)
        {
            PentagonImg pentagon = image.Pentagon;
            float tipSize = pentagon != null ? Mathf.Max(1f, pentagon.TipSize) : 1f;
            float shoulder = Mathf.Clamp(size.y - tipSize, 0f, size.y);

            PolygonBuffer[0] = new Vector2(0f, 0f);
            PolygonBuffer[1] = new Vector2(size.x, 0f);
            PolygonBuffer[2] = new Vector2(size.x, shoulder);
            PolygonBuffer[3] = new Vector2(size.x * 0.5f, size.y);
            PolygonBuffer[4] = new Vector2(0f, shoulder);
            return PointInPolygon(point, 5);
        }

        private static bool HexagonContains(ImageEx image, Vector2 point, Vector2 size)
        {
            HexagonImg hexagon = image.Hexagon;
            float tipX = hexagon != null ? Mathf.Clamp(hexagon.TipSize.x, 0f, size.x * 0.5f) : 0f;

            PolygonBuffer[0] = new Vector2(0f, size.y * 0.5f);
            PolygonBuffer[1] = new Vector2(tipX, 0f);
            PolygonBuffer[2] = new Vector2(size.x - tipX, 0f);
            PolygonBuffer[3] = new Vector2(size.x, size.y * 0.5f);
            PolygonBuffer[4] = new Vector2(size.x - tipX, size.y);
            PolygonBuffer[5] = new Vector2(tipX, size.y);
            return PointInPolygon(point, 6);
        }

        private static bool ChamferBoxContains(ImageEx image, Vector2 point, Vector2 size)
        {
            ChamferBoxImg chamferBox = image.ChamferBox;
            float chamfer = chamferBox != null
                ? Mathf.Clamp(chamferBox.ChamferBoxRadius.x, 0f, 0.5f) * Mathf.Min(size.x, size.y)
                : 0f;
            chamfer = Mathf.Min(chamfer, Mathf.Min(size.x, size.y) * 0.5f);

            PolygonBuffer[0] = new Vector2(chamfer, 0f);
            PolygonBuffer[1] = new Vector2(size.x - chamfer, 0f);
            PolygonBuffer[2] = new Vector2(size.x, chamfer);
            PolygonBuffer[3] = new Vector2(size.x, size.y - chamfer);
            PolygonBuffer[4] = new Vector2(size.x - chamfer, size.y);
            PolygonBuffer[5] = new Vector2(chamfer, size.y);
            PolygonBuffer[6] = new Vector2(0f, size.y - chamfer);
            PolygonBuffer[7] = new Vector2(0f, chamfer);
            return PointInPolygon(point, 8);
        }

        private static bool QuadrilateralContains(ImageEx image, Vector2 point, Vector2 size)
        {
            QuadrilateralImg quad = image.Quadrilateral;
            if (quad == null)
                return true;

            Vector2 oBl = ClampOffset(quad.BottomLeftOffset) * size;
            Vector2 oBr = ClampOffset(quad.BottomRightOffset) * size;
            Vector2 oTr = ClampOffset(quad.TopRightOffset) * size;
            Vector2 oTl = ClampOffset(quad.TopLeftOffset) * size;

            PolygonBuffer[0] = new Vector2(0f, 0f) + oBl;
            PolygonBuffer[1] = new Vector2(size.x, 0f) + oBr;
            PolygonBuffer[2] = new Vector2(size.x, size.y) + oTr;
            PolygonBuffer[3] = new Vector2(0f, size.y) + oTl;
            return PointInPolygon(point, 4);
        }

        private static Vector2 ClampOffset(Vector2 offset)
        {
            return new Vector2(
                Mathf.Clamp(offset.x, -0.5f, 0.5f),
                Mathf.Clamp(offset.y, -0.5f, 0.5f));
        }

        private static bool NStarContains(ImageEx image, Vector2 point, Vector2 size)
        {
            NStarPolygonImg star = image.NStarPolygon;
            if (star == null) return true;

            float sideCount = Mathf.Max(3f, star.SideCount);
            float inset = Mathf.Clamp(star.Inset, 2f, sideCount);
            float cornerRadius = Mathf.Clamp(star.CornerRadius, 0f, size.y * 0.5f);
            float radius = size.y * 0.5f - cornerRadius;
            if (radius <= 0f) return true;

            Vector2 p = new Vector2(
                point.x - size.x * 0.5f - star.Offset.x,
                point.y - size.y * 0.5f - star.Offset.y);
            return SdNStarPolygon(p, radius, sideCount, inset) - cornerRadius <= 0f;
        }

        private static bool HeartContains(Vector2 point, Vector2 size)
        {
            float radius = Mathf.Min(size.x, size.y) * 0.8f;
            Vector2 value = point - new Vector2(size.x * 0.5f, size.y * 0.1f);
            return SdHeart(value, radius) <= 0f;
        }

        private static bool BlobbyCrossContains(Vector2 point, Vector2 size)
        {
            // 十字近似：横/竖两条带（足够覆盖点击，不追求时间形变的毫米级轮廓）
            Vector2 center = size * 0.5f;
            float bar = Mathf.Min(size.x, size.y) * 0.5f;
            bool horizontal = Mathf.Abs(point.x - center.x) <= size.x * 0.5f
                              && Mathf.Abs(point.y - center.y) <= bar * 0.5f;
            bool vertical = Mathf.Abs(point.x - center.x) <= bar * 0.5f
                            && Mathf.Abs(point.y - center.y) <= size.y * 0.5f;
            return horizontal || vertical;
        }

        private static bool SquircleContains(ImageEx image, Vector2 point, Vector2 size)
        {
            float time = image.Squircle != null ? image.Squircle.SquircleTime : 0f;
            float n = 3f + 3f * Mathf.Sin(9.8f * time * 0.5f);
            Vector2 p = new Vector2(
                (2f * point.x - size.x) / Mathf.Min(size.x, size.y),
                (2f * point.y - size.y) / Mathf.Min(size.x, size.y));
            return SdSquircle(p, n) * 80f <= 0f;
        }

        private static bool NTriangleRoundedContains(ImageEx image, Vector2 point, Vector2 size)
        {
            NTriangleRoundedImg triangle = image.NTriangleRounded;
            float number = triangle != null ? triangle.NTriangleRoundedNumber : 3f;
            int sides = Mathf.Clamp(Mathf.FloorToInt(3f + Mathf.Repeat(number, 15f)), 3, MaxPolygonPoints - 1);
            float radius = Mathf.Min(size.x, size.y) * 0.5f;
            Vector2 center = size * 0.5f;

            for (int i = 0; i < sides; i++)
            {
                float angle = -Mathf.PI * 0.5f + Mathf.PI * 2f * i / sides;
                PolygonBuffer[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }

            return PointInPolygon(point, sides);
        }

        private static int BuildTriangle(Vector2 size)
        {
            PolygonBuffer[0] = new Vector2(0f, 0f);
            PolygonBuffer[1] = new Vector2(size.x, 0f);
            PolygonBuffer[2] = new Vector2(size.x * 0.5f, size.y);
            return 3;
        }

        private static bool PointInPolygon(Vector2 point, int count)
        {
            bool inside = false;
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                Vector2 a = PolygonBuffer[i];
                Vector2 b = PolygonBuffer[j];
                if ((a.y > point.y) != (b.y > point.y)
                    && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        // ---------- SDF（与 2D_SDF.cginc 同名函数对齐） ----------

        private static float SdNStarPolygon(Vector2 p, float r, float n, float m)
        {
            float an = Mathf.PI / n;
            float en = Mathf.PI / m;
            Vector2 acs = new Vector2(Mathf.Cos(an), Mathf.Sin(an));
            Vector2 ecs = new Vector2(Mathf.Cos(en), Mathf.Sin(en));

            float bn = Mathf.Abs(Mathf.Atan2(p.x, p.y)) % (2f * an) - an;
            p = p.magnitude * new Vector2(Mathf.Cos(bn), Mathf.Abs(Mathf.Sin(bn)));
            p -= r * acs;
            p += ecs * Mathf.Clamp(-Vector2.Dot(p, ecs), 0f, r * acs.y / ecs.y);
            return p.magnitude * Mathf.Sign(p.x);
        }

        private static float SdHeart(Vector2 p, float scale)
        {
            p.x = Mathf.Abs(p.x) / scale;
            p.y /= scale;

            if (p.y + p.x > 1f)
            {
                Vector2 d = p - new Vector2(0.25f, 0.75f);
                return d.magnitude - Mathf.Sqrt(2f) / 4f;
            }

            float d1 = (p - new Vector2(0f, 1f)).sqrMagnitude;
            float q = 0.5f * Mathf.Max(p.x + p.y, 0f);
            float d2 = (p - new Vector2(q, q)).sqrMagnitude;
            return Mathf.Sqrt(Mathf.Min(d1, d2)) * Mathf.Sign(p.x - p.y);
        }

        private static float SdSquircle(Vector2 p, float n)
        {
            p = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y));
            if (p.y > p.x)
                p = new Vector2(p.y, p.x);

            n = 2f / Mathf.Max(n, 0.001f);

            float xa = 0f;
            float xb = 6.283185f / 8f;
            for (int i = 0; i < 6; i++)
            {
                float x = 0.5f * (xa + xb);
                float c = Mathf.Cos(x);
                float s = Mathf.Sin(x);
                float cn = Mathf.Pow(c, n);
                float sn = Mathf.Pow(s, n);
                float y = (p.x - cn) * cn * s * s - (p.y - sn) * sn * c * c;

                if (y < 0f) xa = x;
                else xb = x;
            }

            Vector2 qa = new Vector2(Mathf.Pow(Mathf.Cos(xa), n), Mathf.Pow(Mathf.Sin(xa), n));
            Vector2 qb = new Vector2(Mathf.Pow(Mathf.Cos(xb), n), Mathf.Pow(Mathf.Sin(xb), n));
            Vector2 pa = p - qa;
            Vector2 ba = qb - qa;
            float h = Mathf.Clamp(Vector2.Dot(pa, ba) / Mathf.Max(Vector2.Dot(ba, ba), 1e-6f), 0f, 1f);
            Vector2 closest = pa - ba * h;
            return closest.magnitude * Mathf.Sign(pa.x * ba.y - pa.y * ba.x);
        }
    }
}
