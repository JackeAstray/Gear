#ifndef RM_INNER_SHADOW
#define RM_INNER_SHADOW

// ============================================================
// ReunionMovement 通用内阴影模块 (RM_InnerShadow)
// 依赖：2D_SDF.cginc（sdfData 约定：形状内部为负、外部为正）
// 说明：只在形状路径（_DrawShape != 0）调用；宽度单位与 sdfData 一致（像素）。
// ============================================================

uniform int _EnableInnerShadow;
uniform half4 _InnerShadowColor;
uniform float _InnerShadowWidth;
uniform float _InnerShadowSoftness;

// 在描边内侧按 SDF 深度叠色。色值可为 HDR（配合 _InnerShadowColor 的 HDR 属性）。
void RM_ApplyInnerShadow(inout half4 color, float sdfData)
{
    if (_EnableInnerShadow == 0) return;
    if (_InnerShadowWidth <= 0.0001) return;
    if (_InnerShadowColor.a <= 0.0) return;

    // sdfData 在形状内部为负值；depth 为"离边缘的深度"（像素）
    float depth = max(0.0, -sdfData);
    float edgeDistance = max(_InnerShadowWidth, 0.0001);
    float band = saturate(1.0 - depth / edgeDistance);

    // 柔和度只改变衰减曲线，不改变宽度：softness=0 线性，1 更靠边
    float softness = saturate(_InnerShadowSoftness);
    float shade = pow(band, 1.0 + softness * 3.0) * saturate(_InnerShadowColor.a);

    color.rgb = lerp(color.rgb, _InnerShadowColor.rgb, shade);
}

#endif // RM_INNER_SHADOW
