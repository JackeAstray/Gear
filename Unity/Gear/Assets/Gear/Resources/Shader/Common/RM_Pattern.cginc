#ifndef RM_PATTERN
#define RM_PATTERN

// ============================================================
// ReunionMovement 图案填充模块 (RM_Pattern)
//
// 语义参考 Evo UI 的 Fill Pattern：
//   在形状/贴图内部铺重复图案，支持点阵、网格、条纹、棋盘、圆环，
//   带尺寸/间隔/旋转/偏移/流动/径向 falloff。
//
// 坐标使用形状像素坐标（shapeData.xy），因此图案会跟随 RectTransform 尺寸，
// 不会像纯 UV 图案那样在非正方形图形上被拉伸。
// ============================================================

uniform int _PatternMode;              // 0=None 1=Dots 2=Grid 3=Stripes 4=Checker 5=Rings
uniform float2 _PatternSize;           // 单元格尺寸（像素）
uniform float2 _PatternGap;            // 图案留白（像素）
uniform float _PatternAngle;           // 旋转（度）
uniform float2 _PatternOffset;         // 偏移（像素）
uniform half4 _PatternColor;           // 图案颜色（HDR）
uniform float _PatternSoftness;        // 边缘柔化（像素）
uniform float2 _PatternFalloff;        // falloff 起止（0..1，按图形半径归一化）
uniform float2 _PatternOrigin;         // falloff 原点（0..1）
uniform float2 _PatternSpeed;          // 流动速度（像素/秒）
uniform float _PatternOpacity;         // 总不透明度 0..1
uniform float _PatternSource;          // 0 Procedural 1 Sprite 2 Shape
uniform sampler2D _PatternTex;
uniform float _PatternShape;
uniform float _PatternColorMode;
uniform sampler2D _PatternGradient;

// 返回图案覆盖率（0..1）。baseAlpha 用于保留贴图自身的透明洞。
float RM_PatternCoverage(float2 shapePx, float2 sizePx, float baseAlpha)
{
    float2 size = max(sizePx, float2(1, 1));
    float2 cellSize = max(_PatternSize, float2(2, 2));

    // 以图形中心为旋转中心，避免旋转时图案整体漂移
    float2 centered = shapePx - size * 0.5;
    float rad = radians(_PatternAngle);
    float2 rotated = float2(
        centered.x * cos(rad) - centered.y * sin(rad),
        centered.x * sin(rad) + centered.y * cos(rad));
    float2 coord = rotated + _PatternOffset + _PatternSpeed * _Time.y;

    float2 id = floor(coord / cellSize);
    float2 local = (frac(coord / cellSize) - 0.5) * cellSize;

    float gap = max(_PatternGap.x, 0.0);
    float softness = max(_PatternSoftness, 0.75);
    float sdf = 1e5;
    float coverage = 0.0;

    if (_PatternSource > 0.5 && _PatternSource < 1.5)
    {
        // Sprite 图块：直接按格子采样，透明洞保留
        float2 tileUv = frac(coord / cellSize);
        coverage = tex2D(_PatternTex, tileUv).a * saturate(baseAlpha);
    }
    else
    {
        if (_PatternSource > 1.5)
        {
            // Shape 图块：圆 / 矩形 SDF，复用格子内的局部坐标
            if (_PatternShape == 1)
                sdf = length(local) - max(min(cellSize.x, cellSize.y) * 0.5 - gap * 0.5, 0.5);
            else
                sdf = max(abs(local.x), abs(local.y)) - max(min(cellSize.x, cellSize.y) * 0.5 - gap * 0.5, 0.5);
        }
        else if (_PatternMode == 1) // Dots
        {
            float radius = max(min(cellSize.x, cellSize.y) * 0.5 - gap * 0.5, 0.5);
            sdf = length(local) - radius;
        }
        else if (_PatternMode == 2) // Grid
        {
            float lineWidth = max(gap, 0.75);
            sdf = min(abs(local.x), abs(local.y)) - lineWidth * 0.5;
        }
        else if (_PatternMode == 3) // Stripes（纵向）
        {
            float halfWidth = max(cellSize.x * 0.5 - gap * 0.5, 0.5);
            sdf = abs(local.x) - halfWidth;
        }
        else if (_PatternMode == 4) // Checker
        {
            float parity = fmod(abs(id.x + id.y), 2.0);
            if (parity > 0.5)
            {
                float halfSize = max(min(cellSize.x, cellSize.y) * 0.5 - gap * 0.5, 0.5);
                sdf = max(abs(local.x), abs(local.y)) - halfSize;
            }
        }
        else if (_PatternMode == 5) // Rings
        {
            float radius = min(cellSize.x, cellSize.y) * 0.25;
            float thickness = max(gap, 0.75);
            sdf = abs(length(local) - radius) - thickness * 0.5;
        }

        coverage = 1.0 - smoothstep(-softness, softness, sdf);
        coverage *= saturate(baseAlpha);
    }

    // 径向 falloff：从 _PatternOrigin 向外淡出
    if (_PatternFalloff.y > _PatternFalloff.x)
    {
        float2 originPx = (_PatternOrigin - 0.5) * size;
        float maxDistance = max(length(size * 0.5), 1.0);
        float distanceToOrigin = length(centered - originPx);
        float fade = smoothstep(_PatternFalloff.x * maxDistance, _PatternFalloff.y * maxDistance, distanceToOrigin);
        coverage *= 1.0 - fade;
    }

    return saturate(coverage * _PatternOpacity);
}

void RM_ApplyPattern(inout half4 color, float4 shapeData, float2 uv, float aspect, float baseAlpha)
{
    if (_PatternMode == 0) return;
    if (_PatternOpacity <= 0.0 || _PatternColor.a <= 0.0) return;

    float coverage = RM_PatternCoverage(shapeData.xy, shapeData.zw, baseAlpha);
    if (coverage <= 0.0001) return;

    half4 patternColor = _PatternColor;
    if (_PatternColorMode > 0.5)
    {
        float2 gradientUv = frac(shapeData.xy / max(_PatternSize, float2(2, 2)));
        patternColor *= tex2D(_PatternGradient, gradientUv);
    }

    float blend = coverage * saturate(patternColor.a);
    color.rgb = lerp(color.rgb, patternColor.rgb, blend);
    color.a = max(color.a, blend);
}

#endif // RM_PATTERN
