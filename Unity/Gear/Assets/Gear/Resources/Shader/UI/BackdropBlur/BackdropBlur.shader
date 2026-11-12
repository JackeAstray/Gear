Shader "Hidden/Gear/UI/BackdropBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Offset ("Offset", Vector) = (1, 1, 0, 0)
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _Offset;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            // 4-tap 对角采样（Kawase 风格的一档），迭代次数由 C# 控制
            fixed4 frag(v2f i) : SV_Target
            {
                float2 texel = _MainTex_TexelSize.xy * _Offset.xy;

                half4 color = tex2D(_MainTex, i.uv + float2(texel.x, texel.y)) * 0.25;
                color += tex2D(_MainTex, i.uv + float2(texel.x, -texel.y)) * 0.25;
                color += tex2D(_MainTex, i.uv + float2(-texel.x, texel.y)) * 0.25;
                color += tex2D(_MainTex, i.uv + float2(-texel.x, -texel.y)) * 0.25;
                return color;
            }
            ENDCG
        }
    }
}
