#ifndef RM_OUTLINE
#define RM_OUTLINE

// ============================================================
// ReunionMovement 通用描边模块 (RM_Outline)
// 适用场景：UI / 2D Sprite / 3D
// 依赖：RM_SDFShapes.cginc（形状参数）, Base/Common.cginc（旧 UV 虚线）
//
// 虚线语义对齐 Evo UI：
//   - 矩形按"外轮廓周长"连续排布，跨角不中断，不会在四边接缝处错位；
//   - 圆形按周长排布；
//   - _DashRounded 打开后，每段虚线两端是半圆帽（不是方头）；
//   - _DashOffset 按像素平移，配合 _CustomTime 做流动。
// ============================================================

#include "../Base/Common.cginc"

uniform half _OutlineWidth;
uniform half4 _OutlineColor;
uniform int _EnableDashedOutline;
uniform float _CustomTime;
uniform half _StrokeWidth;
uniform half _StrokeFill;
uniform half _FalloffDistance;

// 虚线参数（像素）：_DashLength <= 0 时回退到旧的 UV 比例虚线，保证旧资源观感不变
uniform float _DashLength;
uniform float _DashGap;
uniform float _DashSoftness;
uniform float _DashRounded;
uniform float _DashOffset;

// 注：_DrawShape 已在 RM_SDFShapes.cginc（更早包含）中声明

// ------------------------------------------------------------
// 旧 UV 比例虚线（保留兼容）
// ------------------------------------------------------------
float RM_LegacyDashPattern(float s)
{
    float wavelength = 0.2;
    float dashRatio = 0.5;
    return generateDashedPattern(s, wavelength, dashRatio);
}

// ------------------------------------------------------------
// 单段虚线：s 沿轮廓的弧长（像素），lateral 为离外边缘向内的距离（像素）
// 返回 0..1 覆盖率；圆头帽通过胶囊 SDF 实现。
// ------------------------------------------------------------
float RM_DashMask(float s, float lateral, float width)
{
    float dash = max(_DashLength, 0.1);
    float gap = max(_DashGap, 0.0);
    float period = max(dash + gap, 1.0);

    // _CustomTime 以秒计；*60 保持旧资源的流动速度量级
    float t = fmod(s + _DashOffset + _CustomTime * 60.0, period);
    if (t < 0.0) t += period;

    float halfWidth = max(width * 0.5, 0.5);
    float cap = _DashRounded > 0.5 ? min(halfWidth, dash * 0.5) : 0.0;
    float body = max(dash - cap * 2.0, 0.01);

    float2 q = float2(t - cap - body * 0.5, lateral - halfWidth);
    float2 halfSize = float2(body * 0.5, max(halfWidth - cap, 0.01));
    float2 d = abs(q) - halfSize;
    float sd = length(max(d, 0.0)) + min(max(d.x, d.y), 0.0) - cap;

    float aa = max(_DashSoftness * 2.0, 0.5);
    return 1.0 - smoothstep(-aa, aa, sd);
}

// ------------------------------------------------------------
// 矩形周长参数化：返回沿轮廓的弧长 s（像素），lateral 为向内距离。
// 角度与遍历方向：从左上圆角起点(0, h-r.w) 开始，顺时针一周。
// ------------------------------------------------------------
float RM_RectPerimeter(float2 p, float2 size, float4 r, out float lateral)
{
    float maxRadius = min(size.x, size.y) * 0.5;
    r = min(max(r, 0.0), maxRadius);

    float halfPi = 1.57079632679;
    float lt = r.w * halfPi;
    float tt = r.z * halfPi;
    float tr = r.y * halfPi;
    float tb = r.x * halfPi;

    float topLen = max(size.x - r.w - r.z, 0.0);
    float rightLen = max(size.y - r.z - r.y, 0.0);
    float bottomLen = max(size.x - r.y - r.x, 0.0);
    float leftLen = max(size.y - r.x - r.w, 0.0);

    float s;
    float deg;

    // 四个圆角象限（先判定，避免角落被当成直边）
    if (p.x < r.w && p.y > size.y - r.w)
    {
        float2 c = float2(r.w, size.y - r.w);
        float2 d = p - c;
        deg = degrees(atan2(d.y, d.x));
        s = (180.0 - clamp(deg, 90.0, 180.0)) / 90.0 * lt;
        lateral = r.w - length(d);
    }
    else if (p.x > size.x - r.z && p.y > size.y - r.z)
    {
        float2 c = float2(size.x - r.z, size.y - r.z);
        float2 d = p - c;
        deg = degrees(atan2(d.y, d.x));
        s = lt + topLen + (90.0 - clamp(deg, 0.0, 90.0)) / 90.0 * tt;
        lateral = r.z - length(d);
    }
    else if (p.x > size.x - r.y && p.y < r.y)
    {
        float2 c = float2(size.x - r.y, r.y);
        float2 d = p - c;
        deg = degrees(atan2(d.y, d.x));
        if (deg < 0.0) deg += 360.0;
        s = lt + topLen + tt + rightLen + (360.0 - clamp(deg, 270.0, 360.0)) / 90.0 * tr;
        lateral = r.y - length(d);
    }
    else if (p.x < r.x && p.y < r.x)
    {
        float2 c = float2(r.x, r.x);
        float2 d = p - c;
        deg = degrees(atan2(d.y, d.x));
        if (deg < 0.0) deg += 360.0;
        s = lt + topLen + tt + rightLen + tr + bottomLen + (270.0 - clamp(deg, 180.0, 270.0)) / 90.0 * tb;
        lateral = r.x - length(d);
    }
    else
    {
        float dTop = size.y - p.y;
        float dBottom = p.y;
        float dLeft = p.x;
        float dRight = size.x - p.x;
        float m = min(min(dTop, dBottom), min(dLeft, dRight));

        if (m == dTop)
        {
            s = lt + clamp(p.x - r.w, 0.0, topLen);
            lateral = dTop;
        }
        else if (m == dRight)
        {
            s = lt + topLen + tt + clamp(size.y - r.z - p.y, 0.0, rightLen);
            lateral = dRight;
        }
        else if (m == dBottom)
        {
            s = lt + topLen + tt + rightLen + tr + clamp(size.x - r.y - p.x, 0.0, bottomLen);
            lateral = dBottom;
        }
        else
        {
            s = lt + topLen + tt + rightLen + tr + bottomLen + tb + clamp(p.y - r.x, 0.0, leftLen);
            lateral = dLeft;
        }
    }

    return s;
}

// 圆形周长参数化
float RM_CirclePerimeter(float2 p, float2 size, out float lateral)
{
    float radius = lerp(_CircleRadius, min(size.x, size.y) * 0.5, _CircleFitRadius);
    radius = max(radius, 1.0);
    float2 center = size * 0.5;
    float2 d = p - center;
    float distance = length(d);
    float angle = atan2(d.y, d.x) * 0.15915494 + 0.5;
    lateral = radius - distance;
    return frac(angle) * 6.2831853 * radius;
}

// 返回虚线覆盖率：矩形/圆形走周长参数化，其它形状回退旧 UV 虚线
float RM_GenerateDashedEffect(float4 shapeData, float time, float aspectRatio, int shapeType)
{
    float2 size = max(shapeData.zw, float2(1, 1));
    if (_DashLength > 0.0 && (shapeType == 1 || shapeType == 3))
    {
        float lateral = 0;
        float s;
        if (shapeType == 1)
        {
            s = RM_CirclePerimeter(shapeData.xy, size, lateral);
        }
        else
        {
            s = RM_RectPerimeter(shapeData.xy, size, _RectangleCornerRadius, lateral);
        }
        return RM_DashMask(s, lateral, _OutlineWidth);
    }

    // DashLength=0：保持旧的 UV 比例虚线观感（旧资源不受影响）
    if (shapeType == 1)
    {
        float2 center = float2(size.x * 0.5, size.y * 0.5);
        float angle = atan2(shapeData.y - center.y, shapeData.x - center.x);
        float normalizedAngle = (angle + 3.1415926) / (2.0 * 3.1415926);
        float distance = length(shapeData.xy - center);
        float radius = lerp(_CircleRadius, min(size.x, size.y) / 2.0, _CircleFitRadius);
        float outerRadius = max(radius, 1.0);
        float innerRadius = outerRadius - _OutlineWidth;
        float edgeMask = smoothstep(innerRadius, innerRadius + 0.01, distance) *
                         (1.0 - smoothstep(outerRadius - 0.01, outerRadius, distance));
        float dashPattern = step(0.5, frac(normalizedAngle * 15.0 + time));
        return dashPattern * edgeMask;
    }

    if (shapeType == 3)
    {
        float2 uv = shapeData.xy / size;
        float edgeWidth = _OutlineWidth / size.y;
        float edgeTop = getEdge(1.0 - uv.y, edgeWidth, 1.0);
        float edgeBottom = getEdge(uv.y, edgeWidth, 1.0);
        float edgeLeft = getEdge(uv.x, edgeWidth, aspectRatio);
        float edgeRight = getEdge(1.0 - uv.x, edgeWidth, aspectRatio);

        float oldWavelength = 0.2;
        float oldDashRatio = 0.5;
        float t = fmod(time, oldWavelength / 2);
        float oldTop = generateDashedPattern(uv.x + t, oldWavelength, oldDashRatio);
        float oldBottom = generateDashedPattern(uv.x - t, oldWavelength, oldDashRatio);
        float oldLeft = generateDashedPattern(uv.y + t, oldWavelength, oldDashRatio);
        float oldRight = generateDashedPattern(uv.y - t, oldWavelength, oldDashRatio);

        return saturate(edgeTop * oldTop + edgeBottom * oldBottom +
                        edgeLeft * oldLeft + edgeRight * oldRight);
    }

    float2 uv = shapeData.xy / size;
    return step(0.5, frac((uv.x + uv.y) * 0.5 * 15.0 + time));
}

// 应用 SDF 描边效果
void RM_ApplyOutlinedSdf(inout half4 color, float4 shapeData, float sdfData, float pixelScale)
{
    // 原为 OUTLINED 关键字：即「有轮廓宽且没有笔宽」（对齐 C# strokeState 判定）。
    // OUTLINED_STROKE（两者都有）由调用方单独处理，此处必须排除，否则会多叠一层描边。
    if (_OutlineWidth > 0 && _StrokeWidth <= 0)
    {
        float alpha = sampleSdf(sdfData, pixelScale);
        float lerpFac = sampleSdf(sdfData + _OutlineWidth, pixelScale);

        if (_EnableDashedOutline == 1 && _DashLength > 0)
        {
            float dashMask = RM_GenerateDashedEffect(shapeData, _CustomTime, shapeData.z / shapeData.w, _DrawShape);
            // 虚线：dashMask=1 处显示描边色（含描边 alpha），=0 处保留填充。
            // 旧实现把 alpha 写反了（虚线处用填充 alpha、空隙用描边 alpha），
            // 空心图形的虚线描边会整圈消失。
            color.rgb = lerp(color.rgb, _OutlineColor.rgb, dashMask);
            color.a = lerp(color.a, _OutlineColor.a, dashMask);
            color.a *= alpha;
            return;
        }

        if (_EnableDashedOutline == 1)
        {
            // 旧的 UV 比例虚线（DashLength=0）：同样修正 alpha 混合方向
            float dashMask = RM_GenerateDashedEffect(shapeData, _CustomTime, shapeData.z / shapeData.w, _DrawShape);
            if (_DrawShape == 1 || _DrawShape == 3)
            {
                color.rgb = lerp(color.rgb, _OutlineColor.rgb, dashMask);
                color.a = lerp(color.a, _OutlineColor.a, dashMask);
            }
            else
            {
                color = half4(lerp(_OutlineColor.rgb, color.rgb, lerpFac), lerp(_OutlineColor.a, color.a, lerpFac));
            }
            color.a *= alpha;
            return;
        }

        color = half4(lerp(_OutlineColor.rgb, color.rgb, lerpFac), lerp(_OutlineColor.a, color.a, lerpFac));
        color.a *= alpha;
    }
}

#endif // RM_OUTLINE
