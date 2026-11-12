#ifndef RM_INNER_SHADOW
#define RM_INNER_SHADOW

// ============================================================
// ReunionMovement 通用内阴影模块 (RM_InnerShadow)
// 依赖：RM_SDFShapes.cginc（RM_ComputeSdfData）
//
// 语义对齐 Evo UI 的 Inner Shadow：
//   Offset    阴影相对形状的偏移（像素，正值向右/上）
//   Width     Size：阴影向形状内部延伸的深度（像素）
//   Softness  边缘羽化宽度（像素）
//
// 做法：把采样点反向平移 Offset 后重新求 SDF，
//   mask = smoothstep(-aa, aa, sdf + Width)
// 其中 sdf 在形状内部为负：贴边处 sdf≈0 → mask≈0.5；向内超过 Width → mask=0。
// 这正是 Evo 的 "边缘压暗、向内部快速衰减" 观感，而不是旧实现的线性条带。
// ============================================================

uniform int _EnableInnerShadow;
uniform half4 _InnerShadowColor;
uniform float _InnerShadowWidth;
uniform float _InnerShadowSoftness;
uniform float2 _InnerShadowOffset;

// 在描边内侧按 SDF 深度叠色。色值可为 HDR。
void RM_ApplyInnerShadow(inout half4 color, float4 shapeData, float falloffDistance)
{
    if (_EnableInnerShadow == 0) return;
    if (_InnerShadowWidth <= 0.0001) return;
    if (_InnerShadowColor.a <= 0.0) return;
    if (_DrawShape == 0) return;

    float2 shadedPoint = shapeData.xy - _InnerShadowOffset;
    float sdf = 0;
    float pixelScale = 1;
    RM_ComputeSdfData(float4(shadedPoint, shapeData.zw), max(falloffDistance, 0.0001), sdf, pixelScale);

    // 0..1 映射到约 0..12px：与 Evo 的 Softness 观感对齐，同时不改变旧资源的 0-1 序列化范围
    float aa = max(_InnerShadowSoftness * 12.0, 0.5);
    float mask = smoothstep(-aa, aa, sdf + _InnerShadowWidth);

    // 只叠色、不改 alpha：alpha 已由形状/描边/裁剪决定，改 alpha 会在边缘出现缺口
    float shade = mask * saturate(_InnerShadowColor.a);
    color.rgb = lerp(color.rgb, _InnerShadowColor.rgb, shade);
}

#endif // RM_INNER_SHADOW
