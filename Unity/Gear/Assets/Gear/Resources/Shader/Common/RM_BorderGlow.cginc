#ifndef RM_BORDER_GLOW
#define RM_BORDER_GLOW

// ============================================================
// ReunionMovement 边缘辉光模块 (RM_BorderGlow)
//
// 语义参考 Evo UI 的 Border Glow：
//   沿图形边缘发一圈光，可整体常亮（Ambient），也可只跟随指针附近（Follow）。
//   形状路径按 SDF 边缘计算；纯贴图路径按矩形边缘 + 贴图 alpha 计算。
//
// 只做加法叠色，不改变 alpha，因此不会破坏描边/裁剪/过渡的 alpha 结构。
// ============================================================

uniform int _EnableBorderGlow;
uniform half4 _BorderGlowColor;
uniform float _BorderGlowWidth;      // 发光宽度（像素）
uniform float _BorderGlowSoftness;   // 外沿羽化（像素）
uniform float _BorderGlowAmbient;    // 环境光比例 0..1
uniform float2 _BorderGlowPosition;  // 指针位置（0..1，局部）
uniform float _BorderGlowRadius;     // 跟随半径（像素）
uniform float _BorderGlowFollow;     // 0=全程常亮，1=完全跟随指针
uniform float _BorderGlowIntensity;  // 总强度 0..1
uniform float _BorderGlowHover;      // 悬停淡入 0..1（由组件动画）

void RM_ApplyBorderGlow(inout half4 color, float4 shapeData, float2 uv, float sdfData, float falloffDistance)
{
    if (_EnableBorderGlow == 0) return;
    if (_BorderGlowIntensity <= 0.0 || _BorderGlowColor.a <= 0.0) return;
    if (_BorderGlowWidth <= 0.0) return;

    float2 size = max(shapeData.zw, float2(1, 1));
    float width = max(_BorderGlowWidth, 0.5);
    float softness = max(_BorderGlowSoftness, 0.5);

    float edge = 0.0;
    if (_DrawShape != 0)
    {
        // sdfData 在内部为负：depth 是离边缘向内的像素距离
        float depth = max(-sdfData, 0.0);
        edge = 1.0 - smoothstep(width, width + softness, depth);
    }
    else
    {
        float2 edgeDistance = min(uv * size, (1.0 - uv) * size);
        float depth = max(min(edgeDistance.x, edgeDistance.y), 0.0);
        edge = 1.0 - smoothstep(width, width + softness, depth);
    }

    // 指针跟随：距离指针越近越亮
    float2 pointerPx = _BorderGlowPosition * size;
    float distanceToPointer = length(shapeData.xy - pointerPx);
    float radius = max(_BorderGlowRadius, 1.0);
    float follow = 1.0 - smoothstep(radius * (1.0 - saturate(_BorderGlowFollow)), radius, distanceToPointer);
    float radial = lerp(1.0, follow, saturate(_BorderGlowFollow));

    float glow = edge * max(saturate(_BorderGlowAmbient), radial) * saturate(_BorderGlowIntensity) * saturate(_BorderGlowHover);
    color.rgb += _BorderGlowColor.rgb * glow * _BorderGlowColor.a;
}

#endif // RM_BORDER_GLOW
