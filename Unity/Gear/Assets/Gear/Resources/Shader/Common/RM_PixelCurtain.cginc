#ifndef RM_PIXEL_CURTAIN
#define RM_PIXEL_CURTAIN

// ============================================================
// ReunionMovement 像素幕帘模块 (RM_PixelCurtain)
//
// 语义参考 Evo 的 PixelCurtain：
//   把图形切成格子，按方向 + 逐格随机延迟依次揭示/隐藏，格子边缘可羽化。
//   只乘 alpha，不改颜色；方向 / 格子尺寸 / 随机度 / 羽化都可调。
// ============================================================

uniform int _EnablePixelCurtain;
uniform float _CurtainProgress;   // 0=完全隐藏 1=完全显示
uniform float _CurtainCell;       // 格子尺寸（像素）
uniform float _CurtainRandomness; // 逐格随机延迟 0..1
uniform float _CurtainSoftness;   // 格子边缘羽化 0..1
uniform int _CurtainDirection;    // 0=左到右 1=右到左 2=下到上 3=上到下

float RM_CurtainHash(float2 cell)
{
    return frac(sin(dot(cell, float2(127.1, 311.7))) * 43758.5453);
}

void RM_ApplyPixelCurtain(inout half4 color, float4 shapeData, float2 uv, float2 localSize)
{
    if (_EnablePixelCurtain == 0) return;

    float2 size = max(localSize, float2(1, 1));
    float2 pixel = _DrawShape != 0 ? float2(shapeData.x, shapeData.y) : uv * size;
    float cell = max(_CurtainCell, 2.0);
    float2 cellIndex = floor(pixel / cell);

    float position;
    if (_CurtainDirection == 1) position = 1.0 - pixel.x / size.x;
    else if (_CurtainDirection == 2) position = pixel.y / size.y;
    else if (_CurtainDirection == 3) position = 1.0 - pixel.y / size.y;
    else position = pixel.x / size.x;

    float delay = RM_CurtainHash(cellIndex) * saturate(_CurtainRandomness);
    float progress = saturate(_CurtainProgress * (1.0 + saturate(_CurtainRandomness)) - delay);

    float soft = max(_CurtainSoftness, 0.001);
    float mask = smoothstep(position - soft, position + soft, progress);

    color.a *= mask;
}

#endif // RM_PIXEL_CURTAIN
