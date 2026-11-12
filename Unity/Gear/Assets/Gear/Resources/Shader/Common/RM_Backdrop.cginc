#ifndef RM_BACKDROP
#define RM_BACKDROP

// ============================================================
// 背景模糊采样模块 (RM_Backdrop)
//
// 与 UIBackdropBlur 配合：组件把抓屏 + 模糊后的 RenderTexture 写到 _BackdropTex，
// ImageEx 在片元里按屏幕 UV 采样，从而在保留 SDF 形状/圆角/描边的同时显示“玻璃”效果。
// 这是 Evo 的 Backdrop Blur 作为 ProceduralShape 填充的等价能力。
// ============================================================

uniform int _EnableBackdropBlur;
uniform sampler2D _BackdropTex;
uniform half4 _GlassTint;
uniform float _TintAmount;
uniform float _Saturation;
uniform float _Brightness;
uniform float _Noise;
uniform float _BackdropStrength;

void RM_ApplyBackdrop(inout half4 color, float2 screenUV, float baseAlpha)
{
    if (_EnableBackdropBlur == 0) return;

    half3 backdrop = tex2D(_BackdropTex, screenUV).rgb;

    // 饱和度（按亮度插值）→ 亮度 → 玻璃染色 → 噪点，与 BackdropBlurPanel 保持一致
    half luminance = dot(backdrop, half3(0.299, 0.587, 0.114));
    backdrop = lerp(luminance.xxx, backdrop, max(_Saturation, 0.0));
    backdrop *= max(_Brightness, 0.0);
    backdrop = lerp(backdrop, _GlassTint.rgb, saturate(_TintAmount));

    half noiseSeed = frac(sin(dot(screenUV * _ScreenParams.xy, half2(12.9898, 78.233))) * 43758.5453);
    backdrop += (noiseSeed - 0.5) * _Noise;

    // 贴图模式保留精灵透明洞；形状模式 baseAlpha 为 1
    float strength = saturate(_BackdropStrength) * saturate(baseAlpha);
    color.rgb = lerp(color.rgb, backdrop, strength);
    // 强度让原本透明的面板也能显示模糊画面；形状/描边仍由后续 SDF alpha 裁剪
    color.a = max(color.a, strength);
}

#endif // RM_BACKDROP
