#ifndef RM_INK_PANEL
#define RM_INK_PANEL

// ============================================================
// ReunionMovement 墨团揭示模块 (RM_InkPanel)
//
// 语义参考 Evo 的 InkPanel：
//   从指定原点按圆形墨团向外扩散，边缘用 value noise 扰动出"墨水毛边"。
//   只乘 alpha，不改颜色；进度 / 原点 / 羽化 / 粗糙度可调。
//   与 Evo 的差异：暂不做“把覆盖度烘焙成 RT 供 SoftMask 使用”的那一步。
// ============================================================

uniform int _EnableInkPanel;
uniform float _InkProgress;
uniform float2 _InkOrigin;
uniform float _InkSoftness;
uniform float _InkRoughness;

float RM_InkHash(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}

float RM_InkNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    return lerp(
        lerp(RM_InkHash(i), RM_InkHash(i + float2(1.0, 0.0)), u.x),
        lerp(RM_InkHash(i + float2(0.0, 1.0)), RM_InkHash(i + float2(1.0, 1.0)), u.x),
        u.y);
}

void RM_ApplyInkPanel(inout half4 color, float4 shapeData, float2 uv, float2 localSize)
{
    if (_EnableInkPanel == 0) return;

    float2 size = max(localSize, float2(1, 1));
    float2 pixel = _DrawShape != 0 ? float2(shapeData.x, shapeData.y) : uv * size;
    float2 normalized = pixel / size;

    float distanceToOrigin = length(normalized - _InkOrigin);
    float edgeNoise = (RM_InkNoise(normalized * 7.0) - 0.5) * max(_InkRoughness, 0.0) * 0.5;
    distanceToOrigin = max(distanceToOrigin + edgeNoise, 0.0);

    float threshold = saturate(_InkProgress);
    float softness = max(_InkSoftness, 0.001);

    // 墨团内部（距离 < 阈值）可见，边缘按 softness 羽化
    float mask = 1.0 - smoothstep(threshold - softness, threshold + softness, distanceToOrigin);

    color.a *= mask;
}

#endif // RM_INK_PANEL
