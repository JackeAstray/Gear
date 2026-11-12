Shader "Hidden/Evo/UI/Soft Mask TMP Bitmap"
{
    Properties
    {
        _MainTex           ("Font Atlas", 2D) = "white" {}
        _FaceTex           ("Font Texture", 2D) = "white" {}
        _FaceColor         ("Text Color", Color) = (1,1,1,1)
        _Color             ("Tint", Color) = (1,1,1,1)
        _DiffusePower      ("Diffuse Power", Range(1.0,4.0)) = 1.0

        _VertexOffsetX     ("Vertex OffsetX", Float) = 0
        _VertexOffsetY     ("Vertex OffsetY", Float) = 0
        _MaskSoftnessX     ("Mask SoftnessX", Float) = 0
        _MaskSoftnessY     ("Mask SoftnessY", Float) = 0

        _ClipRect          ("Clip Rect", Vector) = (-32767, -32767, 32767, 32767)

        _StencilComp       ("Stencil Comparison", Float) = 8
        _Stencil           ("Stencil ID", Float) = 0
        _StencilOp         ("Stencil Operation", Float) = 0
        _StencilWriteMask  ("Stencil Write Mask", Float) = 255
        _StencilReadMask   ("Stencil Read Mask", Float) = 255

        _CullMode          ("Cull Mode", Float) = 0
        _ColorMask         ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0

        // Soft Mask Parameters
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
            "Queue"           = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType"      = "Transparent"
            "PreviewType"     = "Plane"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Lighting Off
        Cull [_CullMode]
        ZTest [unity_GUIZTestMode]
        ZWrite Off
        Fog { Mode Off }
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
        CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile __ UNITY_UI_CLIP_RECT
            #pragma multi_compile __ UNITY_UI_ALPHACLIP
            #pragma multi_compile_local _ EVO_TMP_BITMAP_MOBILE EVO_TMP_SPRITE

            // Replacement shaders only draw masked graphics
            #pragma multi_compile_local_fragment EVO_UI_SOFT_MASK_BASIC EVO_UI_SOFT_MASK
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #include "SoftMask.cginc"

            struct appdata_t
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float4 vertex    : POSITION;
                fixed4 color     : COLOR;
                float4 texcoord0 : TEXCOORD0;
                float2 texcoord1 : TEXCOORD1;
            };

            struct v2f
            {
                UNITY_VERTEX_OUTPUT_STEREO
                float4 vertex    : SV_POSITION;
                fixed4 color     : COLOR;
                float2 texcoord0 : TEXCOORD0;
                float2 texcoord1 : TEXCOORD1;
                float4 mask      : TEXCOORD2;
                float4 canvasPos : TEXCOORD3;
            };

            sampler2D _MainTex;
            sampler2D _FaceTex;
            float4 _MainTex_ST;
            float4 _FaceTex_ST;
            fixed4 _FaceColor;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float _DiffusePower;
            float _VertexOffsetX;
            float _VertexOffsetY;
            float4 _ClipRect;
            float _MaskSoftnessX;
            float _MaskSoftnessY;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;
            int _UIVertexColorAlwaysGammaSpace;

            v2f vert(appdata_t input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float4 vert = input.vertex;
                vert.x += _VertexOffsetX;
                vert.y += _VertexOffsetY;
                output.canvasPos = vert;

                #if defined(EVO_TMP_SPRITE)
                float4 vPosition = UnityObjectToClipPos(vert);
                output.texcoord0 = TRANSFORM_TEX(input.texcoord0.xy, _MainTex);
                #else
                vert.xy += (vert.w * 0.5) / _ScreenParams.xy;
                float4 vPosition = UnityPixelSnap(UnityObjectToClipPos(vert));
                output.texcoord0 = input.texcoord0.xy;
                #endif

                if (_UIVertexColorAlwaysGammaSpace && !IsGammaSpace())
                    input.color.rgb = UIGammaToLinear(input.color.rgb);

                #if defined(EVO_TMP_BITMAP_MOBILE) || defined(EVO_TMP_SPRITE)
                output.color = input.color * _Color;
                #if defined(EVO_TMP_BITMAP_MOBILE)
                output.color.rgb *= _DiffusePower;
                #endif
                #else
                output.color = input.color * _FaceColor;
                #endif

                output.vertex = vPosition;
                output.texcoord1 = TRANSFORM_TEX(input.texcoord1, _FaceTex);

                float2 pixelSize = vPosition.w;
                pixelSize /= abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));

                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                const half2 maskSoftness = half2(max(_UIMaskSoftnessX, _MaskSoftnessX), max(_UIMaskSoftnessY, _MaskSoftnessY));
                output.mask = half4(vert.xy * 2 - clampedRect.xy - clampedRect.zw, 0.25 / (0.25 * maskSoftness + abs(pixelSize.xy)));

                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                #if defined(EVO_TMP_SPRITE)
                fixed4 color = (tex2D(_MainTex, input.texcoord0) + _TextureSampleAdd) * input.color;
                #elif defined(EVO_TMP_BITMAP_MOBILE)
                fixed4 color = fixed4(input.color.rgb, input.color.a * tex2D(_MainTex, input.texcoord0).a);
                #else
                fixed4 atlas = tex2D(_MainTex, input.texcoord0);
                fixed4 color = fixed4(tex2D(_FaceTex, input.texcoord1).rgb * input.color.rgb, input.color.a * atlas.a);
                #endif

                #if UNITY_UI_CLIP_RECT
                half2 mask = saturate((_ClipRect.zw - _ClipRect.xy - abs(input.mask.xy)) * input.mask.zw);
                color *= mask.x * mask.y;
                #endif

                SoftMask_Apply(color, input.canvasPos);

                #if UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
        ENDCG
        }
    }
}