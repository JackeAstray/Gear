Shader "Hidden/Evo/UI/Ink Noise"
{
    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always
        Blend Off

        // Pass 0: generated once for all active panels
        Pass
        {
            Name "Noise"

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 Hash(float2 p)
            {
                float4 q = frac(p.xyxy * float4(0.1031, 0.1030, 0.0973, 0.1099));
                q += dot(q, q.wzxy + 33.33);
                return frac((q.xxyz + q.yzzw) * q.zywx);
            }

            float4 Noise(float2 uv, float grid)
            {
                float2 p = uv * grid;
                float2 baseCell = floor(p);
                float2 f = frac(p);
                f = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
                float2 nextCell = fmod(baseCell + 1.0, grid);
                float4 a = Hash(baseCell);
                float4 b = Hash(float2(nextCell.x, baseCell.y));
                float4 c = Hash(float2(baseCell.x, nextCell.y));
                float4 d = Hash(nextCell);
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float4 a = Noise(i.uv, 6.0);
                float4 b = Noise(i.uv, 12.0);
                float4 c = Noise(i.uv, 24.0);
                float4 d = Noise(i.uv, 48.0);
                float4 e = Noise(i.uv, 96.0);
                float2 rg = a.rg * 0.64 + b.rg * 0.24 + c.rg * 0.085 + d.rg * 0.035;
                float2 ba = b.ba * 0.64 + c.ba * 0.24 + d.ba * 0.085 + e.ba * 0.035;
                return saturate((float4(rg, ba) - 0.5) * 1.35 + 0.5);
            }
            ENDCG
        }

        // Pass 1: alpha source for the existing SoftMask texture interface
        Pass
        {
            Name "Coverage"

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "InkPanel.cginc"

            sampler2D _InkNoise;
            float4 _Ink;
            float4 _Motion;
            float4 _Geometry;
            float4 _Origin;
            float4 _BakeRect;

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 p = (i.uv * 2.0 - 1.0) * _BakeRect.xy + _BakeRect.zw;
                return fixed4(1, 1, 1, EvoInkCoverage(p, _InkNoise, _Ink, _Motion, _Geometry, _Origin.xy));
            }
            ENDCG
        }
    }
}