Shader "Evo/UI/Pixel Curtain"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _GridSize ("Grid Size", Vector) = (32, 18, 0, 0)
        _Progress ("Progress", Range(0, 1)) = 0
        _Direction ("Direction", Vector) = (1, 0, 0, 0)
        _Randomness ("Randomness", Range(0, 2)) = 0.5
        _FadeWidth ("Fade Width", Range(0.001, 1)) = 0.25
        
        // Z property acts as a multiplier indicating the active max directional distance
        [HideInInspector] _DirectionParams ("Direction Params", Vector) = (0, 1, 1, 0)

        // Standard Unity UI Properties
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
                float2 rectUV   : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float2 rectUV : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            int _UIVertexColorAlwaysGammaSpace;

            float2 _GridSize;
            float _Progress;
            float2 _Direction;
            float _Randomness;
            float _FadeWidth;
            float4 _DirectionParams;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);

                OUT.texcoord = v.texcoord;
                OUT.rectUV = v.rectUV;
                // Canvases that keep vertex colors in gamma space leave the linear conversion to the shader
                if (_UIVertexColorAlwaysGammaSpace && !IsGammaSpace())
                    v.color.rgb = UIGammaToLinear(v.color.rgb);
                OUT.color = v.color * _Color; 
                return OUT;
            }

            // Pseudo-random generator based on UV coordinates
            float rand(float2 co) {
                return frac(sin(dot(co.xy ,float2(12.9898,78.233))) * 43758.5453);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                // Pixel Curtains Logic
                float2 uv = saturate(IN.rectUV);
                float2 gridUV = uv * _GridSize;
                float2 cellID = floor(gridUV);

                float randomVal = rand(cellID);

                // Cell center in normalized 0-1 space
                float2 cellCenter = (cellID + 0.5) / max(_GridSize, float2(0.0001, 0.0001));

                // Use pre-calculated direction and projection bounds from the CPU
                float proj = dot(cellCenter, _Direction.xy);
                float normalizedDistance = saturate((proj - _DirectionParams.x) * _DirectionParams.y);

                // Threshold logic combining wave direction and randomness
                float threshold = normalizedDistance + randomVal * _Randomness;
                
                // Z param dictates mapping limit depending on whether direction is non-zero
                float fadeWidth = max(_FadeWidth, 0.001);
                float maxThreshold = _DirectionParams.z + _Randomness + fadeWidth;
                float currentProg = _Progress * maxThreshold;

                // Smoothly fade alpha from 0 to 1 instead of instantly popping
                float pixelAlpha = _Progress >= 1.0 ? 1.0 : saturate((currentProg - threshold) / fadeWidth);

                // Apply the calculated alpha to the final pixel
                color.a *= pixelAlpha;
                SoftMask_Apply(color, IN.worldPosition);

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                return color;
            }
        ENDCG
        }
    }
}