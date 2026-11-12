Shader "Evo/UI/Dot Matrix Loader"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
        _GridSize ("Grid Size", Vector) = (7, 7, 0.1666667, 0.02040816)
        _DotParams ("Size, Inactive Alpha, Inactive Scale, Softness", Vector) = (0.65, 0.12, 0.65, 1)
        _AnimationParams ("Speed, Phase, Trail Length", Vector) = (0.8, 0, 0.4, 0)
        [Enum(PatternType)] _Pattern ("Pattern", Float) = 0
        [Enum(Circle,0,Square,1,Diamond,2)] _DotShape ("Dot Shape", Float) = 0

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
                float4 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float2 gridPosition : TEXCOORD2;
                float phase : TEXCOORD3;
                #ifdef UNITY_UI_CLIP_RECT
                float4 mask : TEXCOORD4;
                #endif
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            sampler2D _AlphaTex;
            float _EnableExternalAlpha;
            float4 _MainTex_ST;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;
            int _UIVertexColorAlwaysGammaSpace;

            float4 _GridSize;
            float4 _DotParams;
            float4 _AnimationParams;
            float _Pattern;
            float _DotShape;

            static const float TWO_PI = 6.28318530;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                // Canvases that keep vertex colors in gamma space leave the linear conversion to the shader
                if (_UIVertexColorAlwaysGammaSpace && !IsGammaSpace())
                    v.color.rgb = UIGammaToLinear(v.color.rgb);
                OUT.color = v.color;

                // Grid coordinates survive Canvas batching and remain independent of sprite UVs
                OUT.gridPosition = v.texcoord.zw;
                OUT.phase = frac(_Time.y * _AnimationParams.x + _AnimationParams.y);

                #ifdef UNITY_UI_CLIP_RECT
                float2 pixelSize = OUT.vertex.w;
                pixelSize /= abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                OUT.mask = float4(v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw,
                    0.25 / (0.25 * float2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize)));
                #endif

                return OUT;
            }

            float GetIntensity(float2 cell, float phase)
            {
                float intensity = 0.0;

                // Uniform branches evaluate only the selected pattern without shader variants
                UNITY_BRANCH
                if (_Pattern > 4.5 && _Pattern < 5.5)
                {
                    float height = 0.5 + 0.45 * sin(phase * TWO_PI - cell.x * 0.8);
                    float y = (cell.y + 0.5) / _GridSize.y;
                    float edge = 0.5 / _GridSize.y;
                    intensity = smoothstep(y - edge, y + edge, height);
                }
                else
                {
                    float2 p = (cell + 0.5 - _GridSize.xy * 0.5) * (_GridSize.z * 2.0);
                    float offset = 0.0;

                    UNITY_BRANCH
                    if (_Pattern < 0.5)
                        offset = (p.x + p.y) * 0.25;
                    else if (_Pattern < 1.5)
                        offset = length(p) * 0.6;
                    else if (_Pattern < 3.5)
                    {
                        // A tiny offset gives the central dot a defined angle on every GPU
                        offset = atan2(p.x, p.y + 0.00001) / TWO_PI;
                        if (_Pattern > 2.5)
                            offset += length(p) * 0.6;
                    }
                    else if (_Pattern < 4.5)
                    {
                        float row = _GridSize.y - 1.0 - cell.y;
                        float column = lerp(cell.x, _GridSize.x - 1.0 - cell.x, frac(row * 0.5) * 2.0);
                        offset = (row * _GridSize.x + column) * _GridSize.w;
                    }
                    else if (_Pattern < 6.5)
                        offset = frac((cell.x + cell.y) * 0.5);

                    float distance = abs(frac(phase - offset + 0.5) - 0.5) * 2.0;
                    intensity = 1.0 - smoothstep(0.0, max(_AnimationParams.z, 0.0001), distance);
                }

                return intensity;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                half4 texColor = tex2D(_MainTex, IN.texcoord);
                if (_EnableExternalAlpha > 0.5)
                    texColor.a = tex2D(_AlphaTex, IN.texcoord).r;
                fixed4 color = (texColor + _TextureSampleAdd) * IN.color;

                float2 cell = floor(IN.gridPosition);
                float intensity = GetIntensity(cell, IN.phase);
                float2 p = abs(frac(IN.gridPosition) - 0.5);
                float distance = _DotShape < 0.5 ? length(p) : (_DotShape < 1.5 ? max(p.x, p.y) : p.x + p.y);
                float radius = _DotParams.x * 0.5 * lerp(_DotParams.z, 1.0, intensity);

                // Derivatives use continuous coordinates so cell boundaries do not widen the dots
                float2 pixelSize = fwidth(IN.gridPosition);
                float edge = max(max(pixelSize.x, pixelSize.y) * _DotParams.w * 0.5, 0.00001);
                float coverage = (1.0 - smoothstep(radius - edge, radius + edge, distance)) * step(0.00001, radius);
                float2 bounds = step(0.0, IN.gridPosition) * (1.0 - step(_GridSize.xy, IN.gridPosition));
                color.a *= coverage * bounds.x * bounds.y * lerp(_DotParams.y, 1.0, intensity);

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