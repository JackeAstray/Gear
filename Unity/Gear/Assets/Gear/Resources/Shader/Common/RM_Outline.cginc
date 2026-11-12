#ifndef RM_OUTLINE
#define RM_OUTLINE

// ============================================================
// ReunionMovement 通用描边模块 (RM_Outline)
// 适用场景：UI / 2D Sprite / 3D
// 依赖：RM_SDFShapes.cginc（ComputeSdfData）, Base/Common.cginc（generateDashedPattern, getEdge）
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

// 像素级虚线：s 为沿边/沿圆周的像素坐标，time 为动画偏移（像素）
float RM_DashPattern(float s, float time)
{
    float period = max(_DashLength + _DashGap, 1.0);
    float t = fmod(s + time, period);
    if (t < 0.0) t += period;

    float soft = max(_DashSoftness, 0.0) * 0.5;
    float start = smoothstep(0.0, soft + 0.5, t);
    float end = 1.0 - smoothstep(max(_DashLength - soft, 0.0), _DashLength + 0.5, t);
    return saturate(start * end);
}
// 注：_DrawShape 已在 RM_SDFShapes.cginc（更早包含）中声明

// 生成虚线效果（支持圆形和矩形）
float RM_GenerateDashedEffect(float4 shapeData, float time, float aspectRatio, int shapeType)
{
    float wavelength = 0.2;
    float dashRatio = 0.5;
    float dashedEffect = 0.0;

    if (shapeType == 1) // CIRCLE
    {
        float2 center = float2(shapeData.z * 0.5, shapeData.w * 0.5);
        float angle = atan2(shapeData.y - center.y, shapeData.x - center.x); 
        float normalizedAngle = (angle + 3.1415926) / (2.0 * 3.1415926);

        float distance = length(float2(shapeData.x - center.x, shapeData.y - center.y));
        float width = shapeData.z;
        float height = shapeData.w;
        float radius = lerp(_CircleRadius, min(width, height) / 2.0, _CircleFitRadius);

        float outerRadius = radius;
        float innerRadius = radius - _OutlineWidth;
        
        float edgeMask = smoothstep(innerRadius, innerRadius + 0.01, distance) * 
                         (1.0 - smoothstep(outerRadius - 0.01, outerRadius, distance));

        float dashPattern;
        if (_DashLength > 0.0)
        {
            // 圆周弧长（像素）驱动，长度/间隔与矩形一致
            float circumference = 6.2831853 * radius;
            dashPattern = RM_DashPattern(normalizedAngle * circumference, time * 60.0);
        }
        else
        {
            dashPattern = step(0.5, frac(normalizedAngle * 15.0 + _CustomTime));
        }

        dashedEffect = dashPattern * edgeMask;
    }
    else if (shapeType == 3) // RECTANGLE
    {
        float2 uv = shapeData.xy / float2(shapeData.z, shapeData.w);
        float edgeWidth = _OutlineWidth / shapeData.w;
        float edgeTop = getEdge(1.0 - uv.y, edgeWidth, 1.0);
        float edgeBottom = getEdge(uv.y, edgeWidth, 1.0);
        float edgeLeft = getEdge(uv.x, edgeWidth, aspectRatio);
        float edgeRight = getEdge(1.0 - uv.x, edgeWidth, aspectRatio);

        if (_DashLength > 0.0)
        {
            // 像素级虚线：横边按 x、竖边按 y，长度/间隔一致，且随 _CustomTime 平移
            float offset = time * 60.0;
            float dashTop = RM_DashPattern(shapeData.x, offset);
            float dashBottom = RM_DashPattern(shapeData.x, offset);
            float dashLeft = RM_DashPattern(shapeData.y, offset);
            float dashRight = RM_DashPattern(shapeData.y, offset);

            return saturate(edgeTop * dashTop + edgeBottom * dashBottom +
                            edgeLeft * dashLeft + edgeRight * dashRight);
        }

        float oldWavelength = wavelength;
        float oldDashRatio = dashRatio;
        time = fmod(time, oldWavelength / 2);

        float oldTop = generateDashedPattern(uv.x + time, oldWavelength, oldDashRatio);
        float oldBottom = generateDashedPattern(uv.x - time, oldWavelength, oldDashRatio);
        float oldLeft = generateDashedPattern(uv.y + time, oldWavelength, oldDashRatio);
        float oldRight = generateDashedPattern(uv.y - time, oldWavelength, oldDashRatio);

        return saturate(edgeTop * oldTop + edgeBottom * oldBottom +
                        edgeLeft * oldLeft + edgeRight * oldRight);
    }
    return saturate(dashedEffect);
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

        #if DASHED_OUTLINE_STATIC
            float dashedEffect = RM_GenerateDashedEffect(shapeData, _CustomTime, shapeData.z / shapeData.w, _DrawShape);
            if (_DrawShape == 1 || _DrawShape == 3)
            {
                color = half4(lerp(color.rgb, _OutlineColor.rgb, dashedEffect), lerp(_OutlineColor.a, color.a, dashedEffect));
            }
            else
            {
                color = half4(lerp(_OutlineColor.rgb, color.rgb, lerpFac), lerp(_OutlineColor.a, color.a, lerpFac));
            }
        #else
            if (_EnableDashedOutline == 1)
            {
                float dashedEffect = RM_GenerateDashedEffect(shapeData, _CustomTime, shapeData.z / shapeData.w, _DrawShape);

                if (_DrawShape == 1)
                {
                    color = half4(lerp(color.rgb, _OutlineColor.rgb, dashedEffect), lerp(_OutlineColor.a, color.a, dashedEffect));
                }
                else if (_DrawShape == 3)
                {
                    color = half4(lerp(color.rgb, _OutlineColor.rgb, dashedEffect), lerp(_OutlineColor.a, color.a, dashedEffect));
                }
                else
                {
                    color = half4(lerp(_OutlineColor.rgb, color.rgb, lerpFac), lerp(_OutlineColor.a, color.a, lerpFac));
                }
            }
            else
            {
                color = half4(lerp(_OutlineColor.rgb, color.rgb, lerpFac), lerp(_OutlineColor.a, color.a, lerpFac));
            }
        #endif
        color.a *= alpha;
    }
}

#endif // RM_OUTLINE
