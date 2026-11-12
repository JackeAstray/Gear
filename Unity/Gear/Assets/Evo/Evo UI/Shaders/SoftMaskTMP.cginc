#ifndef EVO_UI_SOFTMASK_TMP_INCLUDED
#define EVO_UI_SOFTMASK_TMP_INCLUDED

sampler2D _FaceTex;
float _FaceUVSpeedX;
float _FaceUVSpeedY;
half4 _FaceColor;
float _FaceDilate;
float _OutlineSoftness;

sampler2D _OutlineTex;
float _OutlineUVSpeedX;
float _OutlineUVSpeedY;
half4 _OutlineColor;
float _OutlineWidth;

float _Bevel;
float _BevelOffset;
float _BevelWidth;
float _BevelClamp;
float _BevelRoundness;

sampler2D _BumpMap;
float _BumpOutline;
float _BumpFace;

samplerCUBE _Cube;
half4 _ReflectFaceColor;
half4 _ReflectOutlineColor;
float3 _EnvMatrixRotation;
float4x4 _EnvMatrix;

half4 _SpecularColor;
float _LightAngle;
float _SpecularPower;
float _Reflectivity;
float _Diffuse;
float _Ambient;

half4 _UnderlayColor;
float _UnderlayOffsetX;
float _UnderlayOffsetY;
float _UnderlayDilate;
float _UnderlaySoftness;

half4 _GlowColor;
float _GlowOffset;
float _GlowOuter;
float _GlowInner;
float _GlowPower;

float _ShaderFlags;
float _WeightNormal;
float _WeightBold;

float _ScaleRatioA;
float _ScaleRatioB;
float _ScaleRatioC;

float _VertexOffsetX;
float _VertexOffsetY;

float4 _MaskCoord;
float4 _ClipRect;
float _MaskSoftnessX;
float _MaskSoftnessY;

sampler2D _MainTex;
float _TextureWidth;
float _TextureHeight;
float _GradientScale;
float _ScaleX;
float _ScaleY;
float _PerspectiveFilter;
float _Sharpness;

inline half4 TMP_GetColor(half distance, half4 faceColor, half4 outlineColor, half outline, half softness)
{
    half faceAlpha = 1.0 - saturate((distance - outline * 0.5 + softness * 0.5) / (1.0 + softness));
    half outlineAlpha = saturate(distance + outline * 0.5) * sqrt(min(1.0, outline));

    faceColor.rgb *= faceColor.a;
    outlineColor.rgb *= outlineColor.a;
    faceColor = lerp(faceColor, outlineColor, outlineAlpha);
    faceColor *= faceAlpha;
    return faceColor;
}

inline float3 TMP_GetSurfaceNormal(float4 height, float bias)
{
    bool raisedBevel = step(1.0, fmod(_ShaderFlags, 2.0));
    height += bias + _BevelOffset;

    float bevelWidth = max(0.01, _OutlineWidth + _BevelWidth);
    height -= 0.5;
    height /= bevelWidth;
    height = saturate(height + 0.5);

    if (raisedBevel) { height = 1.0 - abs(height * 2.0 - 1.0); }

    height = lerp(height, sin(height * 1.570796), _BevelRoundness);
    height = min(height, 1.0 - _BevelClamp);
    height *= _Bevel * bevelWidth * _GradientScale * -2.0;

    float3 va = normalize(float3(1.0, 0.0, height.y - height.x));
    float3 vb = normalize(float3(0.0, -1.0, height.w - height.z));
    return cross(va, vb);
}

inline float3 TMP_GetSurfaceNormal(float2 uv, float bias, float3 delta)
{
    float4 height = float4(
        tex2D(_MainTex, uv - delta.xz).a,
        tex2D(_MainTex, uv + delta.xz).a,
        tex2D(_MainTex, uv - delta.zy).a,
        tex2D(_MainTex, uv + delta.zy).a);

    return TMP_GetSurfaceNormal(height, bias);
}

inline float3 TMP_GetSpecular(float3 normal, float3 lightDirection)
{
    float specular = pow(max(0.0, dot(normal, lightDirection)), _Reflectivity);
    return _SpecularColor.rgb * specular * _SpecularPower;
}

inline float4 TMP_GetGlowColor(float distance, float scale)
{
    float glow = distance - (_GlowOffset * _ScaleRatioB) * 0.5 * scale;
    float thickness = lerp(_GlowInner, _GlowOuter * _ScaleRatioB, step(0.0, glow)) * 0.5 * scale;
    glow = saturate(abs(glow / (1.0 + thickness)));
    glow = 1.0 - pow(glow, _GlowPower);
    glow *= sqrt(min(1.0, thickness));
    return float4(_GlowColor.rgb, saturate(_GlowColor.a * glow * 2.0));
}

#endif