Shader "Gear/UI/BackdropBlurPanel"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _BackdropTex ("Backdrop", 2D) = "black" {}
        _BlurStrength ("Blur Strength", Range(0, 1)) = 1

        [Header(Glass)]
        _GlassTint ("Glass Tint", Color) = (1, 1, 1, 1)
        _TintAmount ("Tint Amount", Range(0, 1)) = 0.6
        _Saturation ("Saturation", Range(0, 2)) = 1
        _Brightness ("Brightness", Range(0, 2)) = 1
        _Noise ("Noise", Range(0, 0.2)) = 0.1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float4 screenPos : TEXCOORD1;
            };

            sampler2D _BackdropTex;
            fixed4 _Color;
            float _BlurStrength;

            fixed4 _GlassTint;
            float _TintAmount;
            float _Saturation;
            float _Brightness;
            float _Noise;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.screenPos.xy / max(i.screenPos.w, 1e-5);
                half3 backdrop = tex2D(_BackdropTex, uv).rgb;

                // 饱和度（按亮度插值）→ 亮度 → 玻璃染色 → 噪点
                half luminance = dot(backdrop, half3(0.299, 0.587, 0.114));
                backdrop = lerp(luminance.xxx, backdrop, max(_Saturation, 0.0));
                backdrop *= max(_Brightness, 0.0);
                backdrop = lerp(backdrop, _GlassTint.rgb, saturate(_TintAmount));

                half noiseSeed = frac(sin(dot(uv * _ScreenParams.xy, half2(12.9898, 78.233))) * 43758.5453);
                backdrop += (noiseSeed - 0.5) * _Noise;

                half alpha = i.color.a * saturate(_BlurStrength);
                return half4(backdrop * i.color.rgb, alpha);
            }
            ENDCG
        }
    }
}
