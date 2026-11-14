Shader "Gear/UI/InkCoverage"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Progress ("Progress", Range(0,1)) = 1
        _Origin ("Origin", Vector) = (0.5,0.5,0,0)
        _Softness ("Softness", Range(0.001,0.5)) = 0.08
        _Roughness ("Roughness", Range(0,1)) = 0.35
        _Aspect ("Aspect", Float) = 1
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
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

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            fixed4 _Color;
            float _Progress;
            float4 _Origin;
            float _Softness;
            float _Roughness;
            float _Aspect;

            float GearInkHash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float GearInkNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(GearInkHash(i), GearInkHash(i + float2(1.0, 0.0)), u.x),
                    lerp(GearInkHash(i + float2(0.0, 1.0)), GearInkHash(i + float2(1.0, 1.0)), u.x),
                    u.y);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float aspect = max(_Aspect, 0.001);
                float2 p = i.uv;
                p.x *= aspect;
                float2 origin = _Origin.xy;
                origin.x *= aspect;

                float distanceToOrigin = distance(p, origin);
                float edgeNoise = (GearInkNoise(i.uv * 7.0) - 0.5) * _Roughness * 0.5;
                distanceToOrigin = max(distanceToOrigin + edgeNoise, 0.0);

                float threshold = saturate(_Progress) * 1.45;
                float softness = max(_Softness, 0.001);
                float alpha = 1.0 - smoothstep(threshold - softness, threshold + softness, distanceToOrigin);
                return fixed4(1.0, 1.0, 1.0, saturate(alpha) * i.color.a);
            }
            ENDCG
        }
    }
}
