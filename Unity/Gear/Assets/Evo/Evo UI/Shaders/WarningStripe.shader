Shader "Evo/UI/Warning Stripe"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
        _LineWidth ("Line Width", Range(0.01, 0.5)) = 0.05
        _LineSpacing ("Line Spacing", Range(0.01, 1)) = 0.2
        _LineAngle ("Line Angle", Range(-180, 180)) = 25
        _EdgeSoftness ("Edge Softness", Range(0.1, 4)) = 2
        _ScrollSpeed ("Scroll Speed", Range(-5, 5)) = 0.25

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
            #pragma multi_compile_local_fragment _ EVO_UI_SOFT_MASK_BASIC EVO_UI_SOFT_MASK

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float stripePosition : TEXCOORD2;
                #ifdef UNITY_UI_CLIP_RECT
                float4 mask : TEXCOORD3;
                #endif
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            sampler2D _AlphaTex;
            float _EnableExternalAlpha;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;
            int _UIVertexColorAlwaysGammaSpace;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            float _LineWidth;
            float _LineSpacing;
            float _LineAngle;
            float _EdgeSoftness;
            float _ScrollSpeed;

            static const float DEG_TO_RAD = 0.01745329251;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);

                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                // Canvases that keep vertex colors in gamma space leave the linear conversion to the shader
                if (_UIVertexColorAlwaysGammaSpace && !IsGammaSpace())
                    v.color.rgb = UIGammaToLinear(v.color.rgb);
                OUT.color = v.color;

                // Stripes use local positions so sprite slicing and atlas UVs do not distort them
                float2 objectPos = v.vertex.xy * 0.01; // Scale to manageable numbers
                float angleRad = _LineAngle * DEG_TO_RAD;
                float s, c;
                sincos(angleRad, s, c);

                OUT.stripePosition = objectPos.x * c - objectPos.y * s;

                #ifdef UNITY_UI_CLIP_RECT
                float2 pixelSize = OUT.vertex.w;
                pixelSize /= abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                OUT.mask = float4(v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw,
                    0.25 / (0.25 * float2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize)));
                #endif

                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // Standard UI Color (Texture * Vertex Color)
                half4 texColor = tex2D(_MainTex, IN.texcoord);
                if (_EnableExternalAlpha > 0.5)
                    texColor.a = tex2D(_AlphaTex, IN.texcoord).r;
                texColor += _TextureSampleAdd;
                fixed4 color = texColor * IN.color;

                // Add scrolling to the pre-calculated rotated position
                float scrolledPos = IN.stripePosition + _Time.y * _ScrollSpeed;

                float repeatPeriod = max(_LineWidth + _LineSpacing, 0.00001);

                // Centering logic for the stripes
                float lineCenter = floor(scrolledPos / repeatPeriod) * repeatPeriod + repeatPeriod * 0.5;
                float distanceFromCenter = abs(scrolledPos - lineCenter);

                // Derivative-based softness keeps the transition consistent in screen pixels
                float edgeWidth = max(fwidth(scrolledPos) * max(_EdgeSoftness, 0.1), 0.00001);
                float halfWidth = _LineWidth * 0.5;

                // 1.0 = opaque line, 0.0 = transparent gap
                float lineMask = 1.0 - smoothstep(halfWidth, halfWidth + edgeWidth, distanceFromCenter);

                // Apply mask to alpha
                color.a *= lineMask;
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