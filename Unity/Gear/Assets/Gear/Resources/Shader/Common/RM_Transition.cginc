#ifndef RM_TRANSITION
#define RM_TRANSITION

// ============================================================
// ReunionMovement 通用过渡模块 (RM_Transition)
// 适用场景：UI / 2D Sprite / 3D
// 需要在包含此文件的 Shader 中声明对应的 _Transition* Properties
// 依赖：Common.cginc（rgb_to_hsv, hsv_to_rgb, inv_lerp, rotateUV）
// ============================================================

#include "../Base/Common.cginc"

uniform int _TransitionMode;
uniform sampler2D _TransitionTex; uniform float4 _TransitionTex_ST;
uniform float4 _TransitionTex_TexelSize;
uniform half _TransitionTexRotation;
uniform half _TransitionRate;
uniform half4 _TransitionColor;
uniform half _TransitionWidth;
uniform half _TransitionSoftness;
uniform int _TransitionReverse;
uniform half2 _TransitionTex_Speed;
uniform int _TransitionPatternReverse;
uniform half _TransitionAutoPlaySpeed;
uniform int _TransitionColorFilter;
uniform int _TransitionColorGlow;
uniform sampler2D _TransitionGradientTex;
uniform half2 _TransitionRange;
uniform half _TransitionClamp;
uniform half _TransitionTexClampPadding;
uniform half _TransitionUseUv0;
uniform half _TransitionKeepAspectRatio;
uniform half _TransitionScreenSpace;
uniform int _PatternArea;

// ---- 过渡模式判定（对齐 C# 的 TransitionMode 枚举）----
// 0=None 1=Fade 2=Cutoff 3=Dissolve 4=Shiny 5=Mask 6=Melt 7=Burn 8=Pattern 9=Blaze
//
// 【为什么用运行时分支而不是 shader 关键字】
// 这些模式由 C# 在运行时用 Material.EnableKeyword 打开，而 #pragma shader_feature
// 的变体只有在「构建期存在启用该关键字的材质」时才会被编进包，否则整组被裁剪，
// Player 里 EnableKeyword 静默失效（编辑器正常、真机没效果）。
// 改为读 uniform 后不产生任何额外变体，且 Android/WebGL 无条件生效。
#define RM_TMODE_WRAP(m) ((m) == 4 || (m) == 5 || (m) == 6 || (m) == 7)  // 需要 UV 平铺钳制的模式
#define RM_TMODE_BAND(m) ((m) >= 3 && (m) <= 7)                          // 走带状过渡的模式
#define RM_TMODE_MOVE(m) ((m) == 6 || (m) == 7)                          // Melt/Burn 需要偏移采样 UV

half4 RM_ApplyColorFilter(int mode, half4 inColor, half4 factor, float intensity, float glow)
{
    half4 color = inColor;
    if (mode == 1) // Color.Multiply
    {
        color.rgb = color.rgb * factor.rgb;
        color *= factor.a;
    }
    else if (mode == 2) // Color.Additive
    {
        color.rgb = color.rgb + factor.rgb * color.a * factor.a;
    }
    else if (mode == 3) // Color.Subtractive
    {
        color.rgb = color.rgb - factor.rgb * color.a * color.a;
    }
    else if (mode == 4) // Color.Replace
    {
        color.rgb = factor.rgb * color.a;
        color *= factor.a;
    }
    else if (mode == 5) // Color.MultiplyLuminance
    {
        color.rgb = (1 + Luminance(color.rgb)) * factor.rgb * factor.a / 2 * color.a;
    }
    else if (mode == 6) // Color.MultiplyAdditive
    {
        color.rgb = color.rgb * (1 + factor.rgb * factor.a);
    }
    else if (mode == 7) // Color.HsvModifier
    {
        const float3 hsv = rgb_to_hsv(color.rgb);
        color.rgb = hsv_to_rgb(hsv + factor.rgb) * color.a * color.a;
        color.a = inColor.a * factor.a;
    }
    else if (mode == 8) // Color.Contrast
    {
        color.rgb = ((color.rgb - 0.5) * (factor.r + 1) + 0.5 + factor.g * 1.5) * color.a * factor.a;
        color.a = color.a * factor.a;
    }

    if (0 < mode)
    {
        color = lerp(inColor, color, intensity);
        // 发光效果：提升 RGB 亮度而非降低 Alpha（原 UIEffect 公式 color.a *= 1-glow*intensity 配合 Bloom 使用，
        // 但无 Bloom 时会直接变透明，改为真正的亮度提升）
        color.rgb = lerp(color.rgb, color.rgb * 1.5, glow * intensity);
    }

    return color;
}

float RM_TransitionRate()
{
    if (abs(_TransitionAutoPlaySpeed) > 0.001)
        return frac(_TransitionAutoPlaySpeed * _Time.y + _TransitionRate);
    return _TransitionRate;
}

// 在像素空间旋转过渡 UV：归一化 UV 空间下 U/V 像素长度不等，
// 非正方形矩形直接旋转会把图案剪切变形，故先按四边形宽高比展开再转回来
float2 RM_RotateTransitionUV(float2 uv, float aspect)
{
    if (_TransitionTexRotation == 0)
        return uv;

    float2 s = float2(max(aspect, 1e-4), 1);
    return rotateUV(uv * s, radians(_TransitionTexRotation), s * 0.5) / s;
}

// NDC → 屏幕像素，按平台的 SV_POSITION 方向换算（D3D/Metal/Vulkan 原点在左上，GL 在左下）。
// 屏幕空间采样要用它算面板中心/轴向，否则片元里的 IN.vertex.xy 与这里算出的像素坐标上下相反。
float2 RM_TransitionNdcToPixel(float2 ndc)
{
#if UNITY_UV_STARTS_AT_TOP
    return float2((ndc.x * 0.5 + 0.5) * _ScreenParams.x, (0.5 - ndc.y * 0.5) * _ScreenParams.y);
#else
    return float2((ndc.x * 0.5 + 0.5) * _ScreenParams.x, (ndc.y * 0.5 + 0.5) * _ScreenParams.y);
#endif
}

// 过渡 UV 的像素宽高比：由屏幕空间导数得出。
// 用屏幕空间（而不是网格自带的 quadAspect）后，图案的圆点按“当前视角的像素”保持正方 ——
// 相机从任何角度看、过渡纹理转到任何角度，点阵都不会被拉成椭圆。
// 退化（UV 在一轴上没有梯度）时回退到网格自带的 quadAspect。
float RM_TransitionUvAspect(float2 uv, float fallbackAspect)
{
    const float2 dx = ddx(uv);
    const float2 dy = ddy(uv);
    const float gradU = length(float2(dx.x, dy.x));
    const float gradV = length(float2(dx.y, dy.y));
    return (gradU < 1e-8 || gradV < 1e-8) ? fallbackAspect : gradV / gradU;
}

// 「保持纵横比」：纹素在屏幕上保持正方形所需的 V 向收缩系数。
// 过渡旋转已经搬到像素空间，这里再把「纹理自身宽高比 / 四边形宽高比」补齐 ——
// 于是任意旋转角度、任意矩形比例下，图案都不会被拉成长短轴不等的椭圆。
// Pattern 模式恒定启用；其余过渡模式由 _TransitionKeepAspectRatio 开关决定。
float RM_TransitionTexelFix(float aspect)
{
    const float texAspect = _TransitionTex_TexelSize.z / max(_TransitionTex_TexelSize.w, 1e-6);
    return texAspect / max(aspect, 1e-4);
}

// 返回过渡纹理采样用的 (scale.xy, offset.zw)，按需并入各向同性修正
float4 RM_TransitionScaleOffset(float aspect, float keepAspect)
{
    float4 st = _TransitionTex_ST;

    if (keepAspect > 0.5)
    {
        const float fix = RM_TransitionTexelFix(aspect);
        st.y *= fix;
        st.w += (1 - fix) * 0.5;
    }

    return st;
}

float RM_TransitionAlpha(float2 uvLocal, float aspect)
{
    float2 uv = RM_RotateTransitionUV(uvLocal, aspect);

    const float4 st = RM_TransitionScaleOffset(aspect, _TransitionKeepAspectRatio);
    uv = uv * st.xy + st.zw;

    if (RM_TMODE_WRAP(_TransitionMode))
    {
        uv = saturate(uv);
    }
    else
    {
    #if TRANSITION_CLAMP_STATIC
        uv = saturate(uv);
    #else
        if (_TransitionClamp > 0.5)
        {
            uv = saturate(uv);
        }
    #endif
    }

    float2 uvSample = uv + _Time.y * _TransitionTex_Speed;

    if (RM_TMODE_WRAP(_TransitionMode))
    {
        float2 pad = _TransitionTex_TexelSize.xy * max(_TransitionTexClampPadding, 0);
        float2 tileUv = frac(uvSample);
        tileUv = clamp(tileUv, pad, 1.0 - pad);
        uvSample = floor(uvSample) + tileUv;
    }

    float alpha = tex2Dlod(_TransitionTex, float4(uvSample, 0, 0)).a;
    alpha = _TransitionReverse ? 1 - alpha : alpha;

    if (RM_TMODE_WRAP(_TransitionMode))
    {
        alpha = clamp(alpha, 1e-4, 1.0 - 1e-4);
    }

    return alpha;
}

float2 RM_MoveTransitionFilter(float4 uvMask, float alpha)
{
    if (!RM_TMODE_MOVE(_TransitionMode))
    {
        return 0;
    }

    const float factor = alpha - RM_TransitionRate() * (1 + _TransitionWidth * 1.5) + _TransitionWidth;
    const float band = max(0, _TransitionWidth - factor);

    if (_TransitionMode == 6) // Melt
    {
        return float2(0, +band * band * (uvMask.w - uvMask.y) / max(0.01, _TransitionWidth));
    }
    // Burn(7)
    return float2(0, -band * band * (uvMask.w - uvMask.y) / max(0.01, _TransitionWidth));
}

half4 RM_ApplyTransitionFilter(half4 color, float alpha, float2 uvLocal, float edgeFactor)
{
    const int mode = _TransitionMode;

    if (mode == 1) // Fade
    {
        color *= saturate(alpha + 1 - RM_TransitionRate() * 2);
    }
    else if (mode == 2) // Cutoff
    {
        color *= step(0.001, alpha - RM_TransitionRate());
    }
    else if (mode == 8) // Pattern
    {
        const half4 patternColor = RM_ApplyColorFilter(_TransitionColorFilter, half4(color.rgb, 1), half4(_TransitionColor.rgb * color.a, 1), _TransitionColor.a, _TransitionColorGlow);

        float isPattern = min(inv_lerp(_TransitionRange.x, _TransitionRange.y, uvLocal.x), 0.995) < (_TransitionPatternReverse ? alpha : 1 - alpha);
        isPattern = _TransitionPatternReverse ? isPattern : 1 - isPattern;

        // Pattern Area: 0=All, 1=Inner, 2=Edge
        float patternFactor = 1;
        if (_PatternArea == 1) patternFactor = 1 - edgeFactor;
        else if (_PatternArea == 2) patternFactor = edgeFactor;

        color.rgb = lerp(color.rgb, patternColor.rgb, patternFactor * isPattern);
    }
    else if (RM_TMODE_BAND(mode)) // Dissolve(3) / Shiny(4) / Mask(5) / Melt(6) / Burn(7)
    {
        const float factor = alpha - RM_TransitionRate() * (1 + _TransitionWidth) + _TransitionWidth;
        const float softness = max(0.0001, _TransitionWidth * _TransitionSoftness);
        const half bandLerp = saturate((_TransitionWidth - factor) * 2 / softness);
        const half softLerp = saturate(factor * 2 / softness);

        half4 bandColor = RM_ApplyColorFilter(_TransitionColorFilter, half4(color.rgb, 1),
                                 half4(_TransitionColor.rgb, 1), _TransitionColor.a, _TransitionColorGlow);
        bandColor *= color.a;

        if (mode == 6) // Melt
        {
            color = lerp(color, bandColor, bandLerp);
            return color;
        }
        if (mode == 7) // Burn
        {
            color = lerp(color, bandColor, bandLerp * 1.25);
            color.a *= 1 - inv_lerp(0.85, 1.0, bandLerp * 1.25);
            color.rgb *= (1 - inv_lerp(0.85, 1.0, bandLerp * 1.3)) * color.a;
            return color;
        }

        half lerpFactor = bandLerp * softLerp;
        color = lerp(color, bandColor, lerpFactor);

        if (mode == 3) // Dissolve
        {
            color *= softLerp;
        }
        else if (mode == 5) // Mask
        {
            color *= bandLerp * softLerp;
        }
    }
    else if (mode == 9) // Blaze
    {
        const float maxValue = RM_TransitionRate();
        const float minValue = maxValue - _TransitionWidth / 2;
        const float rate = 1 - inv_lerp(minValue, maxValue, alpha * (1 - _TransitionWidth / 2));
        const float4 gradColor = tex2D(_TransitionGradientTex, float2(rate, 0.5));
        const float4 burntColor = gradColor * color;
        const float4 flameColor = float4(gradColor.rgb, gradColor.a * color.a);

        color = lerp(burntColor, flameColor, step(0.5, rate));
        color.rgb *= color.a;
    }

    return color;
}

#endif // RM_TRANSITION
