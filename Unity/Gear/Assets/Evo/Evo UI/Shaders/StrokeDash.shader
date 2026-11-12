Shader "Evo/UI/Stroke Dash"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
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
        
        // These are driven by the component
        _RectSize ("Rect Size", Vector) = (100, 100, 0, 0)
        _Radius ("Outer Radius", Float) = 10
        _PathRadius ("Path Radius", Float) = 10 
        _Thickness ("Thickness", Float) = 5
        _DashSettings ("Dash(X) Gap(Y) Phase(Z)", Vector) = (10, 5, 0, 0)
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
                float4 canvasPos : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _ClipRect;
            int _UIVertexColorAlwaysGammaSpace;
            
            float2 _RectSize;
            float _Radius;      // Visual Outer Radius
            float _PathRadius;  // Dash Flow Radius
            float _Thickness;
            float3 _DashSettings;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                
                OUT.worldPosition = v.vertex;
                OUT.canvasPos = v.vertex;
                
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = v.texcoord;
                // Canvases that keep vertex colors in gamma space leave the linear conversion to the shader
                if (_UIVertexColorAlwaysGammaSpace && !IsGammaSpace())
                    v.color.rgb = UIGammaToLinear(v.color.rgb);
                OUT.color = v.color * _Color;
                return OUT;
            }

            // Standard SDF for a Rounded Box
            float sdRoundedBox(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b + r;
                return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r;
            }

            // Calculate distance along the perimeter
            float getPerimeterPosition(float2 p, float2 halfSize, float r)
            {
                float safeR = max(r, 0.001);
                float2 cornerCenter = halfSize - safeR;
                float topLen = cornerCenter.x;
                float arcLen = 1.5707963 * safeR;
                float q1_dist = topLen + arcLen + cornerCenter.y;
                float q2_dist = q1_dist + cornerCenter.y + arcLen + topLen;
                float q3_dist = q2_dist + cornerCenter.x + arcLen + cornerCenter.y;
                float perimeterPos = 0.0;

                // Top-Right (Q1)
                if (p.x >= 0 && p.y >= 0)
                {
                    // Corner Zone
                    if (p.x > cornerCenter.x && p.y > cornerCenter.y)
                    {
                        float2 v = p - cornerCenter;
                        float ang = atan2(v.x, v.y); // 0 at Up
                        perimeterPos = topLen + ang * safeR;
                    }
                    // Right Edge Logic
                    else if (p.x > cornerCenter.x)
                    {
                        perimeterPos = topLen + arcLen + (cornerCenter.y - p.y);
                    }
                    // Top Edge Logic
                    else
                    {
                        perimeterPos = p.x;
                    }
                }
                // Bottom-Right (Q2)
                else if (p.x >= 0 && p.y < 0)
                {
                    float py = -p.y;
                    // Corner Zone
                    if (p.x > cornerCenter.x && py > cornerCenter.y)
                    {
                        float2 v = float2(p.x, py) - cornerCenter;
                        float ang = atan2(v.y, v.x); // 0 at Right
                        perimeterPos = q1_dist + cornerCenter.y + ang * safeR;
                    }
                    else if (p.x > cornerCenter.x)
                    {
                        perimeterPos = q1_dist + py;
                    }
                    else
                    {
                        perimeterPos = q1_dist + cornerCenter.y + arcLen + (cornerCenter.x - p.x);
                    }
                }
                // Bottom-Left (Q3)
                else if (p.x < 0 && p.y < 0)
                {
                    float px = -p.x;
                    float py = -p.y;
                    // Corner Zone
                    if (px > cornerCenter.x && py > cornerCenter.y)
                    {
                        float2 v = float2(px, py) - cornerCenter;
                        float ang = atan2(v.x, v.y); // 0 at Down
                        perimeterPos = q2_dist + cornerCenter.x + ang * safeR;
                    }
                    else if (px > cornerCenter.x)
                    {
                        perimeterPos = q2_dist + cornerCenter.x + arcLen + (cornerCenter.y - py);
                    }
                    else
                    {
                        perimeterPos = q2_dist + px;
                    }
                }
                // Top-Left (Q4)
                else if (p.x < 0 && p.y >= 0)
                {
                    float px = -p.x;
                    // Corner Zone
                    if (px > cornerCenter.x && p.y > cornerCenter.y)
                    {
                        float2 v = float2(px, p.y) - cornerCenter;
                        float ang = atan2(v.y, v.x); // 0 at Left
                        perimeterPos = q3_dist + cornerCenter.y + ang * safeR;
                    }
                    else if (px > cornerCenter.x)
                    {
                        perimeterPos = q3_dist + p.y;
                    }
                    else
                    {
                        perimeterPos = q3_dist + cornerCenter.y + arcLen + (cornerCenter.x - px);
                    }
                }

                return perimeterPos;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 pos = IN.texcoord * _RectSize; 
                float2 center = _RectSize * 0.5;
                float2 p = pos - center;
                float2 halfSize = center;

                // Visual Shape (CSG: Outer - Inner)
                // Outer
                float d_outer = sdRoundedBox(p, halfSize, _Radius);
                
                // Inner (Radius is outer radius minus thickness, clamped to 0)
                float innerR = max(0, _Radius - _Thickness);
                float d_inner = sdRoundedBox(p, halfSize - _Thickness, innerR);
                
                // Subtract Inner from Outer
                float d = max(d_outer, -d_inner);
                
                float aa = fwidth(d);
                float soft = max(aa, 0.001);
                
                // Base Alpha
                float alpha = 1.0 - smoothstep(-soft, 0.0, d);
                if (alpha <= 0.001) discard;

                // Dash Logic
                float dashLen = _DashSettings.x;
                float gapLen = _DashSettings.y;
                float phase = _DashSettings.z;
                
                if (gapLen > 0.001)
                {
                    float cycle = dashLen + gapLen;
                    
                    float pathDist = getPerimeterPosition(p, halfSize, _PathRadius);
                    float currentPos = pathDist - phase;
                    
                    float t = (currentPos + cycle * 100.0) / cycle;
                    float cyclePos = (t - floor(t)) * cycle;

                    float dashSoft = soft * 1.5;
                    float dashMask = 1.0 - smoothstep(dashLen - dashSoft, dashLen, cyclePos);
                    
                    alpha *= dashMask;
                }

                half4 color = IN.color;
                color.a *= alpha;
                SoftMask_Apply(color, IN.canvasPos);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}