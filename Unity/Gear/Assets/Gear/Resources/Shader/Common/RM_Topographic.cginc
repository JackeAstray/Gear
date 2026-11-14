#ifndef RM_TOPOGRAPHIC
#define RM_TOPOGRAPHIC

// ============================================================
// ReunionMovement 地形等值线模块 (RM_Topographic)
//
// 语义参考 Evo 的 Topographic Skin：
//   用程序化 value noise（3 层 fbm）生成等高线，线宽 / 层级密度 / 主线间隔 /
//   滚动速度 / 中心衰减都可调；只加色不改 alpha，不需要任何贴图。
// ============================================================

uniform int _EnableTopographic;
uniform float _TopoScale;
uniform float _TopoDensity;
uniform float _TopoLineWidth;
uniform float _TopoMajorEvery;
uniform half4 _TopoColor;
uniform float _TopoIntensity;
uniform float _TopoSpeed;
uniform float _TopoFalloff;

float RM_TopoHash(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}

float RM_TopoNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);

    float a = RM_TopoHash(i);
    float b = RM_TopoHash(i + float2(1.0, 0.0));
    float c = RM_TopoHash(i + float2(0.0, 1.0));
    float d = RM_TopoHash(i + float2(1.0, 1.0));

    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

float RM_TopoFbm(float2 p)
{
    float value = 0.0;
    float amplitude = 0.5;
    for (int i = 0; i < 3; i++)
    {
        value += RM_TopoNoise(p) * amplitude;
        p *= 2.03;
        amplitude *= 0.5;
    }
    return value;
}

void RM_ApplyTopographic(inout half4 color, float4 shapeData, float2 uv, float2 localSize)
{
    if (_EnableTopographic == 0) return;
    if (_TopoIntensity <= 0.0 || _TopoColor.a <= 0.0) return;

    float2 size = max(localSize, float2(1, 1));
    float2 pixel = _DrawShape != 0 ? float2(shapeData.x, shapeData.y) : uv * size;
    float2 normalized = pixel / size;

    float scale = max(_TopoScale, 0.1);
    float2 p = normalized * scale;
    p.x += _Time.y * _TopoSpeed * 0.05;

    float noise = RM_TopoFbm(p);
    float level = noise * max(_TopoDensity, 1.0);

    // 到最近整数等高线的距离
    float distanceToLine = abs(frac(level + 0.5) - 0.5);
    float width = max(_TopoLineWidth, 0.001);
    float contour = 1.0 - smoothstep(0.0, width, distanceToLine);

    // 主线：每 MajorEvery 条加粗一点
    float majorEvery = max(_TopoMajorEvery, 1.0);
    float majorPhase = frac(level / majorEvery + 0.5) - 0.5;
    float majorContour = 1.0 - smoothstep(0.0, width * 2.0, abs(majorPhase) * majorEvery);

    float falloff = saturate(1.0 - length(normalized - 0.5) * 2.0);
    falloff = lerp(1.0, falloff, saturate(_TopoFalloff));

    float amount = saturate(contour + majorContour * 0.8) * falloff;
    float coverage = saturate(color.a);

    color.rgb += _TopoColor.rgb * amount * saturate(_TopoIntensity) * _TopoColor.a * coverage;
}

#endif // RM_TOPOGRAPHIC
