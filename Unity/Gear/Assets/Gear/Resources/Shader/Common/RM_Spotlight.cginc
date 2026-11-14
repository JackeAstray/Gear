#ifndef RM_SPOTLIGHT
#define RM_SPOTLIGHT

// ============================================================
// ReunionMovement 指针聚光模块 (RM_Spotlight)
//
// 语义参考 Evo UI 的 Spotlight：
//   在图形内部叠一团跟随指针的软光，只做加法、不改 alpha；
//   形状路径按 SDF 像素坐标定位，纯贴图路径按本地 UV × 尺寸定位。
//   与边缘辉光（RM_BorderGlow）互补：辉光在边缘，聚光在内部。
// ============================================================

uniform int _EnableSpotlight;
uniform half4 _SpotlightColor;
uniform float _SpotlightRadius;      // 光圈半径（像素）
uniform float _SpotlightSoftness;    // 边缘羽化比例 0..1（相对半径）
uniform float _SpotlightIntensity;   // 总强度 0..1
uniform float2 _SpotlightPosition;   // 指针位置（局部 0..1）
uniform float _SpotlightHover;       // 悬停淡入 0..1（由组件动画）

void RM_ApplySpotlight(inout half4 color, float4 shapeData, float2 uv, float2 localSize)
{
    if (_EnableSpotlight == 0) return;
    if (_SpotlightIntensity <= 0.0 || _SpotlightColor.a <= 0.0) return;

    float2 size = max(localSize, float2(1, 1));
    float2 pixel = _DrawShape != 0 ? float2(shapeData.x, shapeData.y) : uv * size;
    float2 center = _SpotlightPosition * size;

    float radius = max(_SpotlightRadius, 1.0);
    float softness = saturate(_SpotlightSoftness) * radius;
    float inner = max(radius - softness, 0.0);
    float dist = length(pixel - center);
    float falloff = 1.0 - smoothstep(inner, radius, dist);

    // 半透明边缘按 alpha 衰减；完全透明的区域不加光
    float coverage = saturate(color.a);
    float intensity = saturate(_SpotlightIntensity) * saturate(_SpotlightHover);
    color.rgb += _SpotlightColor.rgb * falloff * intensity * _SpotlightColor.a * coverage;
}

#endif // RM_SPOTLIGHT
