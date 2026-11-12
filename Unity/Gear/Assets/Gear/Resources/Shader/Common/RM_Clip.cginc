#ifndef RM_CLIP
#define RM_CLIP

// ============================================================
// ReunionMovement 通用进度裁剪模块 (RM_Clip)
// 用途：进度条 / 扇形填充 / 逐格显现；只返回可见度，不改变形状 SDF。
// 坐标约定：uvCoord 为 0..1 归一化坐标，sizePx 为对应像素尺寸（用于把柔和度换算成像素）。
// ============================================================

uniform int _ClipMode;
uniform float _ClipAmount;
uniform int _ClipReverse;
uniform float _ClipSoftness;

// 返回 0..1 的可见度：amount 之前可见，边缘按 softnessPx 平滑过渡。
// _ClipMode == 0 或 amount >= 1 时直接返回 1（无分支外的额外成本）。
float RM_GetClipMask(float2 uvCoord, float2 sizePx, float aspect)
{
    if (_ClipMode == 0) return 1.0;

    float amount = saturate(_ClipAmount);
    if (amount >= 0.9999) return 1.0;
    if (amount <= 0.0001) return 0.0;

    float2 size = max(sizePx, 1.0);
    float softPx = max(_ClipSoftness, 0.5);

    float t;
    float soft;
    if (_ClipMode == 1) // Horizontal
    {
        t = uvCoord.x;
        soft = softPx / size.x;
    }
    else if (_ClipMode == 2) // Vertical
    {
        t = uvCoord.y;
        soft = softPx / size.y;
    }
    else // Radial：从中心出发，0 点从下方开始，顺时针填充
    {
        float2 p = uvCoord - 0.5;
        p.x *= max(aspect, 1e-4);
        float angle = atan2(p.y, p.x) * 0.15915494 + 0.5; // 1/(2π) = 0.15915494
        t = frac(angle - 0.25);
        // 角度占 0..1 对应整圈 2π；半径约 size.y/2 时，像素 → 角度的换算为 1/π
        soft = softPx / max(size.y, 1.0) * 0.31830989;
    }

    if (_ClipReverse > 0) t = 1.0 - t;

    float edge = saturate((amount - t) / max(soft, 1e-4) + 0.5);
    return smoothstep(0.0, 1.0, edge);
}

#endif // RM_CLIP
