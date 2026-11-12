Shader "Hidden/Evo/UI/Kawase Blur"
{
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        CGINCLUDE
        #include "UnityCG.cginc"

        #include "KawaseBlur.cginc"
        float _EvoBlurOffset;

        struct v2f
        {
            float4 vertex : SV_POSITION;
            float2 uv : TEXCOORD0;
        };

        v2f vert(uint id : SV_VertexID)
        {
            v2f o;
            float2 uv = float2((id << 1) & 2, id & 2);
            o.vertex = float4(uv * 2.0 - 1.0, 0.0, 1.0);
            // Procedural draws use native texture UVs, independent of camera/blit matrices
            #if UNITY_UV_STARTS_AT_TOP
            uv.y = 1.0 - uv.y;
            #endif
            o.uv = uv * _EvoBlurScaleBias.xy + _EvoBlurScaleBias.zw;
            return o;
        }

        half4 down(v2f i) : SV_Target
        {
            return EvoKawaseDown(i.uv, _EvoBlurTexelSize.xy * _EvoBlurOffset);
        }

        half4 up(v2f i) : SV_Target
        {
            return EvoKawaseUp(i.uv, _EvoBlurTexelSize.xy * _EvoBlurOffset);
        }
        ENDCG

        Pass
        {
            CGPROGRAM
            #pragma target 3.5
            #pragma multi_compile_local _ EVO_BLUR_ARRAY
            #pragma vertex vert
            #pragma fragment down
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma target 3.5
            #pragma multi_compile_local _ EVO_BLUR_ARRAY
            #pragma vertex vert
            #pragma fragment up
            ENDCG
        }
    }
    Fallback Off
}