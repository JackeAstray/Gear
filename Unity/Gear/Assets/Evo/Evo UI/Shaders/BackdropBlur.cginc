#ifndef EVO_UI_BACKDROP_BLUR_INCLUDED
#define EVO_UI_BACKDROP_BLUR_INCLUDED

sampler2D _BackgroundTex;
float4 _BgViewportRect;
float4 _BlurAdjustments; // brightness, saturation, noise, capture ready

// Straight-alpha result, composed before the shared UI tint/effects/masking pass
inline half4 EvoBackdropEvaluate(half4 sprite, float2 screenUV, float2 screenSize)
{
    half4 result = sprite;
#if defined(UNITY_COMPILER_HLSL)
    [branch]
#endif
    if (_BlurAdjustments.w > 0.0)
    {
        float2 uv = (screenUV - _BgViewportRect.xy) / max(_BgViewportRect.zw, 0.00001);
        half inside = step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0);
        half3 rgb = tex2D(_BackgroundTex, saturate(uv)).rgb * _BlurAdjustments.x;
        half luminance = dot(rgb, half3(0.2126, 0.7152, 0.0722));
        rgb = lerp(luminance.xxx, rgb, _BlurAdjustments.y);
        float grain = frac(52.9829189 * frac(dot(floor(screenUV * screenSize), float2(0.06711056, 0.00583715))));
        rgb += (grain - 0.5) * _BlurAdjustments.z;
        result = half4(rgb, sprite.a * inside);
    }
    return result;
}

#endif