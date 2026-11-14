using System.Text;
using UnityEngine;
using UnityEngine.UI;

using Gear.Common;
namespace Gear.Utilities.UI.ImageExtensions
{
    /// <summary>
    /// 图像辅助类（Public API）：生成精灵网格、阴影四边形、径向填充等顶点数据。
    /// </summary>
    public static partial class ImageHelper
    {
        #region Public API

        /// <summary>
        /// 生成一个简单的精灵网格（不带阴影），并填充到指定的 <see cref="VertexHelper"/> 中。
        /// </summary>
        /// <param name="vh">目标 VertexHelper，用于接收顶点与三角形。</param>
        /// <param name="preserveAspect">是否保持精灵纵横比。</param>
        /// <param name="canvas">用于像素调整的 Canvas（可为 null）。</param>
        /// <param name="rectTransform">目标 RectTransform，用于确定绘制区域。</param>
        /// <param name="activeSprite">用于绘制的精灵（可为 null，表示使用默认 UV）。</param>
        /// <param name="color">顶点颜色（Color32）。</param>
        /// <param name="falloffDistance">（保留参数）衰减距离（当前实现未直接使用此参数，但保留签名以兼容性）。
        /// </param>
        public static void GenerateSimpleSprite(VertexHelper vh, bool preserveAspect, Canvas canvas,
            RectTransform rectTransform, Sprite activeSprite, Color32 color, float falloffDistance)
        {
            GenerateSimpleSprite(vh, preserveAspect, canvas, rectTransform, activeSprite, color, falloffDistance, false, Vector2.zero, 1f);
        }

        /// <summary>
        /// Cover 填充的 UV 裁切：从精灵的 outer UV 中裁出与目标矩形同宽高比的子区域。
        /// Stretch / Fit 原样返回。
        /// </summary>
        public static Vector4 ApplySpriteFill(Vector4 outerUv, Vector2 spriteSize, Vector2 rectSize, SpriteFillMode mode)
        {
            if (mode != SpriteFillMode.Cover) return outerUv;
            if (spriteSize.x <= 0f || spriteSize.y <= 0f || rectSize.x <= 0f || rectSize.y <= 0f) return outerUv;

            float spriteAspect = spriteSize.x / spriteSize.y;
            float rectAspect = rectSize.x / rectSize.y;
            if (Mathf.Approximately(spriteAspect, rectAspect)) return outerUv;

            float uCenter = (outerUv.x + outerUv.z) * 0.5f;
            float vCenter = (outerUv.y + outerUv.w) * 0.5f;
            float uHalf = (outerUv.z - outerUv.x) * 0.5f;
            float vHalf = (outerUv.w - outerUv.y) * 0.5f;

            if (rectAspect > spriteAspect)
                vHalf *= spriteAspect / rectAspect;
            else
                uHalf *= rectAspect / spriteAspect;

            return new Vector4(uCenter - uHalf, vCenter - vHalf, uCenter + uHalf, vCenter + vHalf);
        }

        /// <summary>
        /// 生成一个简单的精灵网格，并可选地在下方附加一个阴影四边形。
        /// 阴影四边形会以黑色（但保留原始透明度）添加在原始四边形之前，以便在后续 shader 中进行形状／SDF 效果处理。
        /// </summary>
        /// <param name="vh">目标 VertexHelper。</param>
        /// <param name="preserveAspect">是否保持精灵纵横比。</param>
        /// <param name="canvas">用于像素调整的 Canvas（可为 null）。</param>
        /// <param name="rectTransform">目标 RectTransform。</param>
        /// <param name="activeSprite">精灵（可为 null）。</param>
        /// <param name="color">顶点颜色。</param>
        /// <param name="falloffDistance">衰减距离（保留用）。</param>
        /// <param name="appendShadow">是否附加阴影四边形。</param>
        /// <param name="shadowOffsetLocal">阴影的本地偏移量（以像素为单位，局部空间）。</param>
        /// <summary>复用静态顶点缓冲：网格生成在主线程，AddQuad 调用期间即消费、不持有引用，复用安全且零分配</summary>
        private static readonly Vector3[] sxyBuffer = new Vector3[4];
        private static readonly Vector3[] suvBuffer = new Vector3[4];

        public static void GenerateSimpleSprite(VertexHelper vh, bool preserveAspect, Canvas canvas,
            RectTransform rectTransform, Sprite activeSprite, Color32 color, float falloffDistance, bool appendShadow, Vector2 shadowOffsetLocal, float shadowScale = 1f,
            SpriteFillMode spriteFillMode = SpriteFillMode.Stretch)
        {
            vh.Clear();

            Vector4 v = GetDrawingDimensions(preserveAspect, activeSprite, canvas, rectTransform);
            Vector4 uv = (activeSprite != null)
                ? UnityEngine.Sprites.DataUtility.GetOuterUV(activeSprite)
                : new Vector4(0, 0, 1, 1);

            Vector2 size = new Vector2(v.z - v.x, v.w - v.y);
            if (activeSprite != null)
            {
                uv = ApplySpriteFill(uv, activeSprite.rect.size, size, spriteFillMode);
            }
            Vector4 fullBounds = v;

            Vector3[] sxy = sxyBuffer;
            sxy[0] = new Vector2(v.x, v.y);
            sxy[1] = new Vector2(v.x, v.w);
            sxy[2] = new Vector2(v.z, v.w);
            sxy[3] = new Vector2(v.z, v.y);

            Vector3[] suv = suvBuffer;
            suv[0] = new Vector2(uv.x, uv.y);
            suv[1] = new Vector2(uv.x, uv.w);
            suv[2] = new Vector2(uv.z, uv.w);
            suv[3] = new Vector2(uv.z, uv.y);

            AddQuad(vh, sxy, color, suv, size, fullBounds, appendShadow, shadowOffsetLocal, shadowScale);
        }

        /// <summary>
        /// 仅添加阴影四边形到 VertexHelper（不清除现有数据）。
        /// 用于多次调用以生成 Shadow3（迭代投影）或 Outline8（八方向轮廓）。
        /// </summary>
        public static void AddShadowQuad(VertexHelper vh, Vector4 drawingDims, Vector4 uv,
            Color32 color, Vector2 shadowOffsetLocal, float shadowScale = 1f, float shadowFade = 1f)
        {
            Vector2 size = new Vector2(drawingDims.z - drawingDims.x, drawingDims.w - drawingDims.y);

            Vector3[] sxy = sxyBuffer;
            sxy[0] = new Vector2(drawingDims.x, drawingDims.y);
            sxy[1] = new Vector2(drawingDims.x, drawingDims.w);
            sxy[2] = new Vector2(drawingDims.z, drawingDims.w);
            sxy[3] = new Vector2(drawingDims.z, drawingDims.y);

            Vector3[] suv = suvBuffer;
            suv[0] = new Vector2(uv.x, uv.y);
            suv[1] = new Vector2(uv.x, uv.w);
            suv[2] = new Vector2(uv.z, uv.w);
            suv[3] = new Vector2(uv.z, uv.y);

            AddQuad(vh, sxy, color, suv, size, drawingDims, true, shadowOffsetLocal, shadowScale, shadowFade, false);
        }

        /// <summary>
        /// 便捷重载：自动从 RectTransform/Sprite 计算绘制区域和 UV，然后添加阴影四边形。
        /// </summary>
        public static void AddShadowQuad(VertexHelper vh, bool preserveAspect, Canvas canvas,
            RectTransform rectTransform, Sprite activeSprite, Color32 color,
            Vector2 shadowOffsetLocal, float shadowScale = 1f, float shadowFade = 1f,
            SpriteFillMode spriteFillMode = SpriteFillMode.Stretch)
        {
            Vector4 v = GetDrawingDimensions(preserveAspect, activeSprite, canvas, rectTransform);
            Vector4 uv = (activeSprite != null)
                ? UnityEngine.Sprites.DataUtility.GetOuterUV(activeSprite)
                : new Vector4(0, 0, 1, 1);

            if (activeSprite != null)
            {
                Vector2 size = new Vector2(v.z - v.x, v.w - v.y);
                uv = ApplySpriteFill(uv, activeSprite.rect.size, size, spriteFillMode);
            }
            AddShadowQuad(vh, v, uv, color, shadowOffsetLocal, shadowScale, shadowFade);
        }

        /// <summary>
        /// 生成"单四边形 SDF 外阴影 + 主图"的合并网格（对应 <c>ShadowMode.SdfOutline</c>）。
        /// <para>
        /// 与 <see cref="AddShadowQuad"/> 的 8 方向偏移四边形不同：这里只生成一个"外扩"四边形，
        /// 顶点位置向外扩 <paramref name="grow"/> 像素，但 <c>uv1</c> 仍按原始绘制区域做
        /// <see cref="Mathf.InverseLerp"/>（可超出 0..1），于是着色器里的形状坐标会延伸到形状外侧，
        /// 片元再按 SDF 距离做等距膨胀，得到 1 个 quad 的轮廓阴影（顶点 16→8，overdraw 16→2）。
        /// </para>
        /// <para>
        /// 顶点顺序：先阴影（tangent.w=1）、后主图，保证主图盖在阴影之上。
        /// </para>
        /// </summary>
        public static void GenerateSdfShadowSprite(VertexHelper vh, bool preserveAspect, Canvas canvas,
            RectTransform rectTransform, Sprite activeSprite, Color32 color, float grow, float falloffDistance,
            SpriteFillMode spriteFillMode = SpriteFillMode.Stretch)
        {
            vh.Clear();

            Vector4 v = GetDrawingDimensions(preserveAspect, activeSprite, canvas, rectTransform);
            Vector4 uv = (activeSprite != null)
                ? UnityEngine.Sprites.DataUtility.GetOuterUV(activeSprite)
                : new Vector4(0, 0, 1, 1);

            Vector2 size = new Vector2(v.z - v.x, v.w - v.y);
            if (activeSprite != null)
            {
                uv = ApplySpriteFill(uv, activeSprite.rect.size, size, spriteFillMode);
            }

            // 外扩量：grow + 一个衰减余量；限制不超过短边一半，避免极小图形生成退化四边形
            float margin = Mathf.Max(falloffDistance, 0f) + 2f;
            float expand = Mathf.Max(0f, grow + margin);
            float maxExpand = Mathf.Min(size.x, size.y) * 0.5f;
            expand = Mathf.Min(expand, maxExpand);

            float x0 = v.x - expand;
            float y0 = v.y - expand;
            float x1 = v.z + expand;
            float y1 = v.w + expand;
            Vector4 expandedBounds = new Vector4(x0, y0, x1, y1);

            // ---- 阴影四边形（外扩，uv1 用原始区域做逆插值，允许 <0 / >1） ----
            int shadowStart = vh.currentVertCount;
            AddSdfShadowVertex(vh, new Vector2(x0, y0), color, uv, v, expandedBounds, size);
            AddSdfShadowVertex(vh, new Vector2(x0, y1), color, uv, v, expandedBounds, size);
            AddSdfShadowVertex(vh, new Vector2(x1, y1), color, uv, v, expandedBounds, size);
            AddSdfShadowVertex(vh, new Vector2(x1, y0), color, uv, v, expandedBounds, size);

            vh.AddTriangle(shadowStart, shadowStart + 1, shadowStart + 2);
            vh.AddTriangle(shadowStart + 2, shadowStart + 3, shadowStart);

            // ---- 主图四边形（与 GenerateSimpleSprite 一致：uv1 归一化 + epsilon 内缩） ----
            const float epsilon = 0.001f;
            int mainStart = vh.currentVertCount;
            Vector3[] corners =
            {
                new Vector2(v.x, v.y),
                new Vector2(v.x, v.w),
                new Vector2(v.z, v.w),
                new Vector2(v.z, v.y),
            };
            Vector3[] uvs =
            {
                new Vector2(uv.x, uv.y),
                new Vector2(uv.x, uv.w),
                new Vector2(uv.z, uv.w),
                new Vector2(uv.z, uv.y),
            };

            for (int i = 0; i < 4; i++)
            {
                float u = Mathf.InverseLerp(v.x, v.z, corners[i].x);
                float w = Mathf.InverseLerp(v.y, v.w, corners[i].y);
                u = Mathf.Lerp(epsilon, 1f - epsilon, u);
                w = Mathf.Lerp(epsilon, 1f - epsilon, w);
                Vector2 uv1 = new Vector2(u, w);

                vh.AddVert(corners[i], color, uvs[i], uv1, size, Vector2.zero,
                    Vector3.zero, Vector4.zero);
            }

            vh.AddTriangle(mainStart, mainStart + 1, mainStart + 2);
            vh.AddTriangle(mainStart + 2, mainStart + 3, mainStart);
        }

        /// <summary>
        /// 写入一个"外扩阴影"顶点：uv1 用原始绘制区域做逆插值（可超出 0..1），
        /// 让着色器里的形状坐标覆盖到形状外侧；tangent.w=1 标记阴影顶点。
        /// 精灵 UV 按外扩四边形线性映射（形状阴影通常不依赖贴图）。
        /// </summary>
        private static void AddSdfShadowVertex(VertexHelper vh, Vector2 position, Color32 color,
            Vector4 spriteUv, Vector4 shapeBounds, Vector4 expandedBounds, Vector2 size)
        {
            float shapeU = Mathf.InverseLerp(shapeBounds.x, shapeBounds.z, position.x);
            float shapeV = Mathf.InverseLerp(shapeBounds.y, shapeBounds.w, position.y);

            float texU = Mathf.InverseLerp(expandedBounds.x, expandedBounds.z, position.x);
            float texV = Mathf.InverseLerp(expandedBounds.y, expandedBounds.w, position.y);
            Vector2 texcoord = new Vector2(
                Mathf.Lerp(spriteUv.x, spriteUv.z, texU),
                Mathf.Lerp(spriteUv.y, spriteUv.w, texV));

            vh.AddVert(position, color, texcoord, new Vector2(shapeU, shapeV), size, Vector2.zero,
                Vector3.zero, new Vector4(0f, 0f, 0f, 1f));
        }

        /// <summary>
        /// 生成带填充（包括水平、垂直、径向填充）的精灵网格。
        /// 支持 <see cref="Image.FillMethod"/> 的所有填充方式（Horizontal / Vertical / Radial90 / Radial180 / Radial360）。
        /// </summary>
        /// <param name="toFill">目标 VertexHelper。</param>
        /// <param name="preserveAspect">是否保持纵横比。</param>
        /// <param name="canvas">用于像素调整的 Canvas（可为 null）。</param>
        /// <param name="rectTransform">目标 RectTransform。</param>
        /// <param name="activeSprite">精灵（可为 null）。</param>
        /// <param name="color">顶点颜色。</param>
        /// <param name="fillMethod">填充方式。</param>
        /// <param name="fillAmount">填充量（0..1）。</param>
        /// <param name="fillOrigin">填充起点（取决于 fillMethod）。</param>
        /// <param name="fillClockwise">对于径向填充是否顺时针方向填充。</param>
        /// <param name="falloffDistance">衰减距离（保留参数）。</param>
        public static void GenerateFilledSprite(VertexHelper toFill,
            bool preserveAspect,
            Canvas canvas,
            RectTransform rectTransform,
            Sprite activeSprite,
            Color32 color,
            Image.FillMethod fillMethod,
            float fillAmount,
            int fillOrigin,
            bool fillClockwise,
            float falloffDistance)
        {
            toFill.Clear();

            if (fillAmount < 0.001f)
                return;

            Vector4 v = GetDrawingDimensions(preserveAspect, activeSprite, canvas, rectTransform);
            Vector2 size = new Vector2(v.z - v.x, v.w - v.y);
            Vector4 fullBounds = v;

            Vector4 outer = activeSprite != null
                ? UnityEngine.Sprites.DataUtility.GetOuterUV(activeSprite)
                : new Vector4(0, 0, 1, 1);

            float tx0 = outer.x;
            float ty0 = outer.y;
            float tx1 = outer.z;
            float ty1 = outer.w;

            // 水平和垂直填充的处理
            if (fillMethod == Image.FillMethod.Horizontal || fillMethod == Image.FillMethod.Vertical)
            {
                if (fillMethod == Image.FillMethod.Horizontal)
                {
                    float fill = (tx1 - tx0) * fillAmount;

                    if (fillOrigin == 1)
                    {
                        v.x = v.z - (v.z - v.x) * fillAmount;
                        tx0 = tx1 - fill;
                    }
                    else
                    {
                        v.z = v.x + (v.z - v.x) * fillAmount;
                        tx1 = tx0 + fill;
                    }
                }
                else if (fillMethod == Image.FillMethod.Vertical)
                {
                    float fill = (ty1 - ty0) * fillAmount;

                    if (fillOrigin == 1)
                    {
                        v.y = v.w - (v.w - v.y) * fillAmount;
                        ty0 = ty1 - fill;
                    }
                    else
                    {
                        v.w = v.y + (v.w - v.y) * fillAmount;
                        ty1 = ty0 + fill;
                    }
                }
            }

            Vector3[] sxy = sxyBuffer;
            sxy[0] = new Vector2(v.x, v.y);
            sxy[1] = new Vector2(v.x, v.w);
            sxy[2] = new Vector2(v.z, v.w);
            sxy[3] = new Vector2(v.z, v.y);

            Vector3[] suv = suvBuffer;
            suv[0] = new Vector2(tx0, ty0);
            suv[1] = new Vector2(tx0, ty1);
            suv[2] = new Vector2(tx1, ty1);
            suv[3] = new Vector2(tx1, ty0);


            if (fillAmount < 1f &&
                fillMethod != Image.FillMethod.Horizontal &&
                fillMethod != Image.FillMethod.Vertical)
            {
                if (fillMethod == Image.FillMethod.Radial90)
                {
                    if (RadialCut(sxy, suv, fillAmount, fillClockwise, fillOrigin))
                        AddQuad(toFill, sxy, color, suv, size, fullBounds, false, Vector2.zero);
                }
                else if (fillMethod == Image.FillMethod.Radial180)
                {
                    for (int side = 0; side < 2; ++side)
                    {
                        float fx0, fx1, fy0, fy1;
                        int even = fillOrigin > 1 ? 1 : 0;

                        if (fillOrigin == 0 || fillOrigin == 2)
                        {
                            fy0 = 0f;
                            fy1 = 1f;
                            if (side == even)
                            {
                                fx0 = 0f;
                                fx1 = 0.5f;
                            }
                            else
                            {
                                fx0 = 0.5f;
                                fx1 = 1f;
                            }
                        }
                        else
                        {
                            fx0 = 0f;
                            fx1 = 1f;
                            if (side == even)
                            {
                                fy0 = 0.5f;
                                fy1 = 1f;
                            }
                            else
                            {
                                fy0 = 0f;
                                fy1 = 0.5f;
                            }
                        }

                        sxy[0].x = Mathf.Lerp(v.x, v.z, fx0);
                        sxy[1].x = sxy[0].x;
                        sxy[2].x = Mathf.Lerp(v.x, v.z, fx1);
                        sxy[3].x = sxy[2].x;

                        sxy[0].y = Mathf.Lerp(v.y, v.w, fy0);
                        sxy[1].y = Mathf.Lerp(v.y, v.w, fy1);
                        sxy[2].y = sxy[1].y;
                        sxy[3].y = sxy[0].y;

                        suv[0].x = Mathf.Lerp(tx0, tx1, fx0);
                        suv[1].x = suv[0].x;
                        suv[2].x = Mathf.Lerp(tx0, tx1, fx1);
                        suv[3].x = suv[2].x;

                        suv[0].y = Mathf.Lerp(ty0, ty1, fy0);
                        suv[1].y = Mathf.Lerp(ty0, ty1, fy1);
                        suv[2].y = suv[1].y;
                        suv[3].y = suv[0].y;

                        float val = fillClockwise ? fillAmount * 2f - side : fillAmount * 2f - (1 - side);

                        if (RadialCut(sxy, suv, Mathf.Clamp01(val), fillClockwise,
                            ((side + fillOrigin + 3) % 4)))
                        {
                            AddQuad(toFill, sxy, color, suv, size, fullBounds, false, Vector2.zero);
                        }
                    }
                }
                else if (fillMethod == Image.FillMethod.Radial360)
                {
                    for (int corner = 0; corner < 4; ++corner)
                    {
                        float fx0, fx1, fy0, fy1;

                        if (corner < 2)
                        {
                            fx0 = 0f;
                            fx1 = 0.5f;
                        }
                        else
                        {
                            fx0 = 0.5f;
                            fx1 = 1f;
                        }

                        if (corner == 0 || corner == 3)
                        {
                            fy0 = 0f;
                            fy1 = 0.5f;
                        }
                        else
                        {
                            fy0 = 0.5f;
                            fy1 = 1f;
                        }

                        sxy[0].x = Mathf.Lerp(v.x, v.z, fx0);
                        sxy[1].x = sxy[0].x;
                        sxy[2].x = Mathf.Lerp(v.x, v.z, fx1);
                        sxy[3].x = sxy[2].x;

                        sxy[0].y = Mathf.Lerp(v.y, v.w, fy0);
                        sxy[1].y = Mathf.Lerp(v.y, v.w, fy1);
                        sxy[2].y = sxy[1].y;
                        sxy[3].y = sxy[0].y;

                        suv[0].x = Mathf.Lerp(tx0, tx1, fx0);
                        suv[1].x = suv[0].x;
                        suv[2].x = Mathf.Lerp(tx0, tx1, fx1);
                        suv[3].x = suv[2].x;

                        suv[0].y = Mathf.Lerp(ty0, ty1, fy0);
                        suv[1].y = Mathf.Lerp(ty0, ty1, fy1);
                        suv[2].y = suv[1].y;
                        suv[3].y = suv[0].y;

                        float val = fillClockwise
                            ? fillAmount * 4f - ((corner + fillOrigin) % 4)
                            : fillAmount * 4f - (3 - ((corner + fillOrigin) % 4));

                        if (RadialCut(sxy, suv, Mathf.Clamp01(val), fillClockwise, ((corner + 2) % 4)))
                            AddQuad(toFill, sxy, color, suv, size, fullBounds, false, Vector2.zero);
                    }
                }
            }
            else
            {
                AddQuad(toFill, sxy, color, suv, size, fullBounds, false, Vector2.zero);
            }
        }

        /// <summary>
        /// 保持精灵纵横比。会修改传入的 rect 以匹配精灵的纵横比，并根据 RectTransform 的 pivot 进行对齐。
        /// 该方法对外公开以便在其它 UI 布局逻辑中复用。
        /// </summary>
        /// <param name="rect">要调整的矩形（按引用传入）。</param>
        /// <param name="rectTransform">关联的 RectTransform（用于 pivot 对齐）。</param>
        /// <param name="spriteSize">精灵的像素尺寸。</param>
        public static void PreserveSpriteAspectRatio(ref Rect rect, RectTransform rectTransform, Vector2 spriteSize)
        {
            float spriteRatio = spriteSize.x / spriteSize.y;
            float rectRatio = rect.width / rect.height;

            if (spriteRatio > rectRatio)
            {
                float oldHeight = rect.height;
                rect.height = rect.width * (1.0f / spriteRatio);
                rect.y += (oldHeight - rect.height) * rectTransform.pivot.y;
            }
            else
            {
                float oldWidth = rect.width;
                rect.width = rect.height * spriteRatio;
                rect.x += (oldWidth - rect.width) * rectTransform.pivot.x;
            }
        }

        #endregion
    }
}
