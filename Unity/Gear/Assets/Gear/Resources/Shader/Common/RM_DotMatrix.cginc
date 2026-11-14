#ifndef RM_DOT_MATRIX
#define RM_DOT_MATRIX

// ============================================================
// ReunionMovement 点阵图案模块 (RM_DotMatrix)
//
// 语义参考 Evo 的 Dot Matrix Loader：
//   把图形按格子切分，每格画一个点；点的亮度由 8 种解析图案（波浪/同心圆/雷达/
//   螺旋/蛇形/均衡器/棋盘/脉冲）+ 时间算出，不需要任何贴图。
//   只加色不改 alpha，与 Spotlight / Pattern 可叠加。
// ============================================================

uniform int _EnableDotMatrix;
uniform int _DotPattern;
uniform int _DotShape;
uniform float _DotCell;
uniform float _DotRadius;
uniform half4 _DotColor;
uniform float _DotIntensity;
uniform float _DotSpeed;

float RM_DotMask(float2 cellUv, float radius, int shape)
{
    float2 p = cellUv - 0.5;
    if (shape == 1) return step(max(abs(p.x), abs(p.y)), radius);
    if (shape == 2) return step(abs(p.x) + abs(p.y), radius);
    return step(length(p), radius);
}

float RM_DotAmount(float2 cellIndex, float2 normalized, float time)
{
    float2 centered = normalized - 0.5;
    float distanceToCenter = length(centered);
    float angle = atan2(centered.y, centered.x);

    if (_DotPattern == 1)
        return 0.5 + 0.5 * sin(distanceToCenter * 26.0 - time * 2.0);
    if (_DotPattern == 2)
        return 0.5 + 0.5 * sin(angle + distanceToCenter * 12.0 - time * 2.5);
    if (_DotPattern == 3)
        return 0.5 + 0.5 * sin(angle * 3.0 + distanceToCenter * 30.0 - time * 3.0);
    if (_DotPattern == 4)
        return 0.5 + 0.5 * sin(cellIndex.x * 0.7 + sin(cellIndex.y * 0.6 + time) * 2.2);
    if (_DotPattern == 5)
        return 0.3 + 0.7 * abs(sin(cellIndex.x * 0.9 + time * 2.0));
    if (_DotPattern == 6)
        return fmod(abs(cellIndex.x + cellIndex.y), 2.0) < 1.0 ? 1.0 : 0.12;
    if (_DotPattern == 7)
        return 0.35 + 0.65 * abs(sin(distanceToCenter * 24.0 - time * 3.0));

    // 0 = 波浪
    return 0.5 + 0.5 * sin(cellIndex.y * 0.9 - time * 2.0);
}

void RM_ApplyDotMatrix(inout half4 color, float4 shapeData, float2 uv, float2 localSize)
{
    if (_EnableDotMatrix == 0) return;
    if (_DotIntensity <= 0.0 || _DotColor.a <= 0.0) return;

    float2 size = max(localSize, float2(1, 1));
    float2 pixel = _DrawShape != 0 ? float2(shapeData.x, shapeData.y) : uv * size;
    float cell = max(_DotCell, 4.0);

    float2 cellIndex = floor(pixel / cell);
    float2 cellUv = frac(pixel / cell);
    float2 normalized = (cellIndex + 0.5) * cell / size;

    float amount = saturate(RM_DotAmount(cellIndex, normalized, _Time.y * _DotSpeed));
    float mask = RM_DotMask(cellUv, _DotRadius, _DotShape);
    float coverage = saturate(color.a);

    color.rgb += _DotColor.rgb * mask * amount * _DotIntensity * _DotColor.a * coverage;
}

#endif // RM_DOT_MATRIX
