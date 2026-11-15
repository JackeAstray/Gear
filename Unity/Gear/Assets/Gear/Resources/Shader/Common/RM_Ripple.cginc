#ifndef GEAR_RM_RIPPLE
#define GEAR_RM_RIPPLE

// 8 波环形缓冲：xy=UV 中心，z=开始时间，w=强度
half4 _RippleData[8];
half _RippleCount;
half _RippleSpeed;
half _RippleWidth;
half _RippleSoftness;
half _RippleStrength;
half4 _RippleColor;

// 返回叠加环强度，并给 UV 加径向扰动；涟漪坐标使用 texcoord(0..1)
half RM_ApplyRippleUV(inout float2 uv)
{
    half ring = 0;
    float2 offset = float2(0, 0);
    int count = (int)clamp(_RippleCount, 0, 8);
    [unroll]
    for (int i = 0; i < 8; i++)
    {
        if (i >= count) break;
        half4 data = _RippleData[i];
        float age = _Time.y - data.z;
        if (age < 0) continue;

        float2 delta = uv - data.xy;
        float dist = length(delta);
        float radius = age * max(_RippleSpeed, 0.01);
        float width = max(_RippleWidth, 0.001) * (1 + age * 0.15);
        float band = saturate(1.0 - abs(dist - radius) / width);
        band = band * band;
        half decay = saturate(1.0 - age * 0.35);
        half wave = band * data.w * decay * saturate(_RippleStrength);

        offset += normalize(delta + 1e-5) * wave * 0.012;
        ring += wave;
    }

    uv += offset;
    return saturate(ring);
}

// 只在已过 SDF/裁剪的 alpha 内加色，避免波纹溢出形状
void RM_ApplyRippleColor(inout half4 color, half ring)
{
    if (ring <= 0) return;
    color.rgb += _RippleColor.rgb * ring * _RippleColor.a * color.a;
}

#endif
