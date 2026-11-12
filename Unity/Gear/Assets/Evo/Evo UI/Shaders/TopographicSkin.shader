Shader "Evo/UI/Topographic Skin"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0

        _Pattern ("Frequency / Levels / Speed", Vector) = (0.003,15,0.02,0)
        _Lines ("Width / Major Width / Inverse Softness / Inverse Major Interval", Vector) = (2,4,1,0)
        _Seed ("Seed Offset", Vector) = (0.37,0.71,0.43,0)
        _FalloffOrigin ("Falloff Scaled Origin / Inverse Aspect / One", Vector) = (0,0,1,1)
        _FalloffRange ("Falloff Offset / Inverse Width / Depth", Vector) = (0,0.002,1,0)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
        [HideInInspector] _SoftMaskSupport ("Soft Mask Support", Float) = 1
        [HideInInspector] _SoftMask_Count ("Soft Mask Count", Float) = 0
        [HideInInspector] _SoftMaskTex0 ("Soft Mask 0", 2D) = "white" {}
        [HideInInspector] _SoftMaskTex1 ("Soft Mask 1", 2D) = "white" {}
        [HideInInspector] _SoftMaskTex2 ("Soft Mask 2", 2D) = "white" {}
        [HideInInspector] _SoftMaskTex3 ("Soft Mask 3", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "Queue"             = "Transparent"
            "IgnoreProjector"   = "True"
            "RenderType"        = "Transparent"
            "PreviewType"       = "Plane"
            "CanUseSpriteAtlas" = "True"
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
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #include "SoftMask.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            // Not shader_feature: the material is created at runtime, so builds must keep both variants
            #pragma multi_compile_local _ EVO_TOPOGRAPHIC_FALLOFF
            #pragma multi_compile_local_fragment _ EVO_UI_SOFT_MASK_BASIC EVO_UI_SOFT_MASK

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float4 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float4 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                #ifdef UNITY_UI_CLIP_RECT
                float4 mask : TEXCOORD2;
                #endif
                // Every other channel is taken: SoftMask_Apply reads all of worldPosition
                #ifdef EVO_TOPOGRAPHIC_FALLOFF
                float2 falloff : TEXCOORD3;
                #endif
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            sampler2D _AlphaTex;
            float4 _MainTex_ST;
            fixed4 _TextureSampleAdd;
            float _EnableExternalAlpha;
            float4 _ClipRect;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;
            int _UIVertexColorAlwaysGammaSpace;

            float4 _Pattern;
            float4 _Lines;
            float4 _Seed;
            float _EvoTopographicTime;
            #ifdef EVO_TOPOGRAPHIC_FALLOFF
            float4 _FalloffOrigin;
            float4 _FalloffRange;
            #endif

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                // UV0.zw holds rect-local canvas units. A 15 degree turn keeps lattice rows off the screen axes.
                float2 local = v.texcoord.zw;
                float2 position = float2(local.x * 0.965925826 - local.y * 0.258819045, local.x * 0.258819045 + local.y * 0.965925826);
                OUT.texcoord = float4(TRANSFORM_TEX(v.texcoord.xy, _MainTex), position * _Pattern.x + _Seed.xy);
                #ifdef EVO_TOPOGRAPHIC_FALLOFF
                // Offset from the origin with horizontal distances divided by the aspect: (local - origin) * (1 / aspect, 1)
                OUT.falloff = local * _FalloffOrigin.zw - _FalloffOrigin.xy;
                #endif
                // Canvases that keep vertex colors in gamma space leave the linear conversion to the shader
                if (_UIVertexColorAlwaysGammaSpace && !IsGammaSpace())
                    v.color.rgb = UIGammaToLinear(v.color.rgb);
                OUT.color = v.color;

                #ifdef UNITY_UI_CLIP_RECT
                float2 pixelSize = OUT.vertex.w;
                pixelSize /= abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                OUT.mask = float4(v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw,
                    0.25 / (0.25 * float2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize)));
                #endif

                return OUT;
            }

            float3 Hash33(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return frac((p.xxy + p.yxx) * p.zyx) * 2.0 - 1.0;
            }

            // 3D gradient noise with quintic fades: smooth enough for crisp contours at any zoom
            float GradientNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = p - i;
                float3 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
                return lerp(lerp(lerp(dot(Hash33(i), f), dot(Hash33(i + float3(1, 0, 0)), f - float3(1, 0, 0)), u.x),
                                 lerp(dot(Hash33(i + float3(0, 1, 0)), f - float3(0, 1, 0)), dot(Hash33(i + float3(1, 1, 0)), f - float3(1, 1, 0)), u.x), u.y),
                            lerp(lerp(dot(Hash33(i + float3(0, 0, 1)), f - float3(0, 0, 1)), dot(Hash33(i + float3(1, 0, 1)), f - float3(1, 0, 1)), u.x),
                                 lerp(dot(Hash33(i + float3(0, 1, 1)), f - float3(0, 1, 1)), dot(Hash33(i + 1.0), f - 1.0), u.x), u.y), u.z);
            }

            // Line coverage at a pattern position (rotated, scaled and seeded in the vertex stage)
            float TopographicLines(float2 position)
            {
                float height = GradientNoise(float3(position, _EvoTopographicTime * _Pattern.z + _Seed.z)) * _Pattern.y;

                // Distance to the nearest contour in screen pixels, from the height's screen gradient.
                // Contours sit between whole levels, so flat ground around zero never fills in.
                float2 gradient = float2(ddx(height), ddy(height));
                float gradientSq = max(dot(gradient, gradient), 1e-12);
                float inverseSlope = rsqrt(gradientSq);
                float slope = gradientSq * inverseSlope;
                float index = floor(height);
                float distance = abs(height - index - 0.5) * inverseSlope;

                // Pixels per canvas unit. The determinant keeps rotated and scaled graphics correct.
                float2 dx = ddx(position);
                float2 dy = ddy(position);
                float pixelsPerUnit = _Pattern.x * rsqrt(max(abs(dx.x * dy.y - dx.y * dy.x), 1e-24));

                // Every Nth contour is a major line. _Lines.w holds 1 / N, or 0 when major lines are off.
                float major = step(frac((index + 0.5) * _Lines.w), _Lines.w - 0.0001);
                float width = lerp(_Lines.x, _Lines.y, major) * pixelsPerUnit;

                // Sub-pixel lines fade with their coverage instead of breaking up
                float coverage = saturate((max(width, 1.0) * 0.5 - distance) * _Lines.z + 0.5) * min(width, 1.0);

                // Contours closer than a few pixels settle to their average coverage instead of moire
                return lerp(coverage, saturate(width * slope), saturate(slope * 3.0 - 0.75));
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 texColor = tex2D(_MainTex, IN.texcoord.xy);
                if (_EnableExternalAlpha > 0.5)
                    texColor.a = tex2D(_AlphaTex, IN.texcoord.xy).r;
                fixed4 color = (texColor + _TextureSampleAdd) * IN.color;

                color.a *= TopographicLines(IN.texcoord.zw);

                #ifdef EVO_TOPOGRAPHIC_FALLOFF
                // lerp(outside, 1, 1 - smoothstep(t)), with t going 0 to 1 between the start and end distances
                float fade = saturate(length(IN.falloff) * _FalloffRange.y + _FalloffRange.x);
                color.a *= 1.0 - fade * fade * (3.0 - 2.0 * fade) * _FalloffRange.z;
                #endif

                SoftMask_Apply(color, IN.worldPosition);

                #ifdef UNITY_UI_CLIP_RECT
                float2 mask = saturate((_ClipRect.zw - _ClipRect.xy - abs(IN.mask.xy)) * IN.mask.zw);
                color.a *= mask.x * mask.y;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
