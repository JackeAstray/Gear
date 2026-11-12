#ifndef EVO_KAWASE_BLUR_INCLUDED
#define EVO_KAWASE_BLUR_INCLUDED

// Shared texture bindings and viewport-aware sampling for both kernels.
// The default source is 2D; EVO_BLUR_ARRAY selects slice zero of a texture array.
// Scale/bias selects the valid viewport within a potentially larger allocation.
#if defined(EVO_BLUR_ARRAY)
Texture2DArray _EvoBlurSourceArray;
SamplerState sampler_EvoBlurSourceArray;
#else
sampler2D _EvoBlurSource;
#endif
float4 _EvoBlurTexelSize; // reciprocal width/height, width/height
float4 _EvoBlurScaleBias; // UV scale xy, UV offset zw

inline half4 EvoKawaseSample(float2 uv)
{
    float2 low = _EvoBlurScaleBias.zw + _EvoBlurTexelSize.xy * 0.5;
    float2 high = _EvoBlurScaleBias.zw + _EvoBlurScaleBias.xy - _EvoBlurTexelSize.xy * 0.5;
    uv = clamp(uv, low, max(low, high));
#if defined(EVO_BLUR_ARRAY)
    return _EvoBlurSourceArray.Sample(sampler_EvoBlurSourceArray, float3(uv, 0.0));
#else
    return tex2D(_EvoBlurSource, uv);
#endif
}

// Offset is in source UV units (source texel size multiplied by the radius)
inline half4 EvoKawaseDown(float2 uv, float2 offset)
{
    half4 sum = EvoKawaseSample(uv) * 4.0;
    sum += EvoKawaseSample(uv + offset);
    sum += EvoKawaseSample(uv - offset);
    sum += EvoKawaseSample(uv + float2(offset.x, -offset.y));
    sum += EvoKawaseSample(uv + float2(-offset.x, offset.y));
    return sum * 0.125;
}

inline half4 EvoKawaseUp(float2 uv, float2 offset)
{
    float2 d = offset * 0.5;
    half4 sum = EvoKawaseSample(uv + float2(-2.0 * d.x, 0.0));
    sum += EvoKawaseSample(uv + float2(-d.x, d.y)) * 2.0;
    sum += EvoKawaseSample(uv + float2(0.0, 2.0 * d.y));
    sum += EvoKawaseSample(uv + d) * 2.0;
    sum += EvoKawaseSample(uv + float2(2.0 * d.x, 0.0));
    sum += EvoKawaseSample(uv + float2(d.x, -d.y)) * 2.0;
    sum += EvoKawaseSample(uv + float2(0.0, -2.0 * d.y));
    sum += EvoKawaseSample(uv - d) * 2.0;
    return sum / 12.0;
}

#endif