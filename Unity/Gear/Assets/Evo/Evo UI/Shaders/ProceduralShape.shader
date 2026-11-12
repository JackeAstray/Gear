Shader "Evo/UI/Procedural Shape"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)

        // Shared automatic-material data. Explicit materials keep using the properties below.
        [HideInInspector] _ShapeData ("Shape Data", 2D) = "black" {}
        [HideInInspector] _ShapeDataVersion ("Shape Data Version", Float) = 2
        [HideInInspector] _ShapeGradientAtlas ("Shape Gradients", 2D) = "white" {}
        [HideInInspector] _ShapePatterns ("Shape Patterns", 2D) = "black" {}
        [HideInInspector] _EvoEffectsSupport ("Interactive Effects Support", Float) = 1

        [HideInInspector] _BackgroundTex ("Camera Capture", 2D) = "black" {}
        [HideInInspector] _BgViewportRect ("Camera Viewport", Vector) = (0,0,1,1)
        [HideInInspector] _BlurAdjustments ("Brightness, Saturation, Noise, Ready", Vector) = (1,1,0,0)

        // Fill
        _FillColor           ("Fill Color",           Vector) = (1,1,1,1)
        _FillColorMode       ("Fill Color Mode",      Float) = 0
        _FillGradientTex     ("Fill Gradient Tex",    2D)    = "white" {}
        _FillGradientAngle   ("Fill Gradient Angle",  Float) = 0
        [HideInInspector] _FillGradientDirection ("Fill Gradient Direction", Vector) = (0,0,0,0)
        _FillGradientZoom    ("Fill Gradient Zoom",   Float) = 1
        _FillGradientReverse ("Fill Gradient Reverse",Float) = 0

        // Outline
        _OutlineDash            ("Dash Length, Gap, Offset, Rounded", Vector) = (0,0,0,0)
        _OutlineColor           ("Outline Color",          Vector) = (0,0,0,1)
        _OutlineColorMode       ("Outline Color Mode",     Float) = 0
        _OutlineGradientTex     ("Outline Gradient Tex",   2D)    = "white" {}
        _OutlineGradientAngle   ("Outline Gradient Angle", Float) = 0
        [HideInInspector] _OutlineGradientDirection ("Outline Gradient Direction", Vector) = (0,0,0,0)
        _OutlineGradientZoom    ("Outline Gradient Zoom",  Float) = 1
        _OutlineGradientReverse ("Outline Gradient Rev",   Float) = 0

        // Inner Shadow
        _InnerShadowColor           ("Inner Shadow Color",          Vector) = (0,0,0,0.4)
        _InnerShadowColorMode       ("Inner Shadow Color Mode",     Float) = 0
        _InnerShadowGradientTex     ("Inner Shadow Gradient Tex",   2D)    = "white" {}
        _InnerShadowGradientAngle   ("Inner Shadow Gradient Angle", Float) = 0
        [HideInInspector] _InnerShadowGradientDirection ("Inner Shadow Gradient Direction", Vector) = (0,0,0,0)
        _InnerShadowGradientZoom    ("Inner Shadow Gradient Zoom",  Float) = 1
        _InnerShadowGradientReverse ("Inner Shadow Gradient Rev",   Float) = 0

        // Outer Shadow
        _OuterShadowColor           ("Outer Shadow Color",          Vector) = (0,0,0,0.4)
        _OuterShadowColorMode       ("Outer Shadow Color Mode",     Float) = 0
        _OuterShadowGradientTex     ("Outer Shadow Gradient Tex",   2D)    = "white" {}
        _OuterShadowGradientAngle   ("Outer Shadow Gradient Angle", Float) = 0
        [HideInInspector] _OuterShadowGradientDirection ("Outer Shadow Gradient Direction", Vector) = (0,0,0,0)
        _OuterShadowGradientZoom    ("Outer Shadow Gradient Zoom",  Float) = 1
        _OuterShadowGradientReverse ("Outer Shadow Gradient Rev",   Float) = 0

        // Mask/stencil
        _StencilComp      ("Stencil Comparison",  Float) = 8
        _Stencil          ("Stencil ID",          Float) = 0
        _StencilOp        ("Stencil Operation",   Float) = 0
        _StencilWriteMask ("Stencil Write Mask",  Float) = 255
        _StencilReadMask  ("Stencil Read Mask",   Float) = 255
        _ColorMask        ("Color Mask",          Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
        
        // Soft Mask Parameters
        [HideInInspector] _SoftMaskSupport ("Soft Mask Support", Float) = 1
        [HideInInspector] _SoftMask_Count ("Soft Mask Count", Float) = 0
        [HideInInspector] _SoftMaskTex0 ("Soft Mask 0", 2D) = "white" {}
        [HideInInspector] _SoftMaskTex1 ("Soft Mask 1", 2D) = "white" {}
        [HideInInspector] _SoftMaskTex2 ("Soft Mask 2", 2D) = "white" {}
        [HideInInspector] _SoftMaskTex3 ("Soft Mask 3", 2D) = "white" {}
    }

    CGINCLUDE

    #include "UnityCG.cginc"
    #include "UnityUI.cginc"
    #include "ProceduralShape.cginc"
    #include "SoftMask.cginc"
    #include "InteractiveEffects.cginc"
    #include "BackdropBlur.cginc"

    // Full precision preserves atlas addressing and Color32 conversion on mobile
    sampler2D_float _ShapeData;
    // Matches StylePage.dataWidth for version 2; only the row count changes when an atlas grows
    #define EVO_UI_SHAPE_DATA_WIDTH 12.0
    // Effect rows stay below InteractiveEffectRenderer's 4096-row limit; dithered shapes add this to theirs
    #define EVO_UI_DITHER_ROW 4096.0
    // Feature bits of plain shapes, carried by a negative dash length (see the end of vert)
    #define EVO_UI_PLAIN_FILL         1u
    #define EVO_UI_PLAIN_OUTLINE      2u
    #define EVO_UI_PLAIN_SHADOW       4u
    #define EVO_UI_PLAIN_OUTSIDE_ONLY 8u
    #define EVO_UI_PLAIN_DITHER       16u
    #define EVO_UI_PLAIN_ELLIPSE      32u
    #define EVO_UI_PLAIN_SQUIRCLE     64u
    #define EVO_UI_PLAIN_ALPHA_TEX    128u
    #define EVO_UI_PLAIN_CLIP         256u
    #define EVO_UI_PLAIN_PATTERN      512u
    #define EVO_UI_PLAIN_PATTERN_ELLIPSE 1024u
    #define EVO_UI_PLAIN_PATTERN_BASE 2048u
    #define EVO_UI_PLAIN_PATTERN_FALLOFF 4096u
    // Polygons: shader shape ID minus 9 from this bit up (rectangles and ellipses have none; see EVO_UI_PLAIN_ELLIPSE)
    #define EVO_UI_PLAIN_SHAPE_SHIFT  13u
    sampler2D_float _ShapeGradientAtlas;
    sampler2D_float _ShapePatterns;
    sampler2D _MainTex;
    sampler2D _AlphaTex;
    half4     _Color;
    float4    _ClipRect;

    half      _UIVertexColorAlwaysGammaSpace;

    half4     _FillColor;
    half      _FillColorMode;
    sampler2D _FillGradientTex;
    half      _FillGradientAngle;
    half4     _FillGradientDirection;
    half      _FillGradientZoom;
    half      _FillGradientReverse;

    half4     _OutlineColor;
    float4    _OutlineDash;
    half      _OutlineColorMode;
    sampler2D _OutlineGradientTex;
    half      _OutlineGradientAngle;
    half4     _OutlineGradientDirection;
    half      _OutlineGradientZoom;
    half      _OutlineGradientReverse;

    half4     _InnerShadowColor;
    half      _InnerShadowColorMode;
    sampler2D _InnerShadowGradientTex;
    half      _InnerShadowGradientAngle;
    half4     _InnerShadowGradientDirection;
    half      _InnerShadowGradientZoom;
    half      _InnerShadowGradientReverse;

    half4     _OuterShadowColor;
    half      _OuterShadowColorMode;
    sampler2D _OuterShadowGradientTex;
    half      _OuterShadowGradientAngle;
    half4     _OuterShadowGradientDirection;
    half      _OuterShadowGradientZoom;
    half      _OuterShadowGradientReverse;

    struct appdata
    {
        float4 vertex  : POSITION;
        half4  color   : COLOR;
        float4 uv0     : TEXCOORD0;
        float4 uv1     : TEXCOORD1;
        float4 uv2     : TEXCOORD2;
        float4 uv3     : TEXCOORD3;
        float4 tangent : TANGENT; 
        float3 normal  : NORMAL;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    #ifdef UNITY_PLATFORM_WEBGL
        #define EVO_UI_NOINTERPOLATION
    #else
        #define EVO_UI_NOINTERPOLATION nointerpolation
    #endif

    struct v2f
    {
        float4 vertex   : SV_POSITION;
        half4  color    : COLOR;
        half4  fillColor : TEXCOORD10; // Negative alpha selects a per-fragment gradient
        float2 sdfCoord : TEXCOORD0;
        EVO_UI_NOINTERPOLATION float4 rectData : TEXCOORD1;
    #if defined(EVO_UI_LITE)
        // Light pages carry only the shadow and outline colors here: half precision, as fillColor
        EVO_UI_NOINTERPOLATION half4  fxData   : TEXCOORD2;
    #else
        EVO_UI_NOINTERPOLATION float4 fxData   : TEXCOORD2;
    #endif
        EVO_UI_NOINTERPOLATION float4 radii    : TEXCOORD3;
        EVO_UI_NOINTERPOLATION float4 outerShd : TEXCOORD4;
        float4 worldPos : TEXCOORD5;
    #if defined(EVO_UI_LITE)
        EVO_UI_NOINTERPOLATION half4  innerShd : TEXCOORD6;
    #else
        EVO_UI_NOINTERPOLATION float4 innerShd : TEXCOORD6; // W: pattern row V, negative enables patterns
    #endif
        float4 spriteUV : TEXCOORD7; // XY: sprite UV, Z: style row scale, W: gradient atlas V
        EVO_UI_NOINTERPOLATION float4 dash : TEXCOORD9;
        float4 screen : TEXCOORD8; // Backdrop blur only
        UNITY_VERTEX_OUTPUT_STEREO
    };

    #undef EVO_UI_NOINTERPOLATION

    // Engine Polynomial Sync
    inline half4 UI_ColorSpaceSync(half4 c)
    {
    #if !defined(UNITY_COLORSPACE_GAMMA)
        if (_UIVertexColorAlwaysGammaSpace > 0.5) {
            c.rgb = UIGammaToLinear(c.rgb);
        }
    #endif
        return c;
    }

    // StylePage uploads Canvas's quantized linear colors when required.
    // Gamma vertex colors still use Unity's polynomial after sampling, as before.
    inline half4 UI_StyleColorSpaceSync(float4 c)
    {
    #if !defined(UNITY_COLORSPACE_GAMMA)
        if (_UIVertexColorAlwaysGammaSpace > 0.5)
            c.rgb = UIGammaToLinear(c.rgb);

    #endif
        return c;
    }

    // Packed vertex fields are never negative, so frac() can stand in for fmod() without its sign handling
    inline float Evo_UI_Mod(float x, float y)
    {
        return frac(x / y) * y;
    }

    // Premultiplied Alpha Blending Helpers
    inline half4 Premultiply(half4 col)
    {
        col.rgb *= col.a;
        return col;
    }

    inline half4 AlphaBlend(half4 top, half4 bottom)
    {
        return top + bottom * (1.0 - top.a);
    }

    // Dithering
    // Static interleaved gradient noise of about one 8-bit level breaks banding in slow gradients and soft shadows.
    // The shape blends over content it can't read, so the noise rides on both blend terms: the premultiplied color
    // carries a + b * color and the destination factor carries b * destination, which together follow the final color.
    inline half4 Evo_UI_Dither(half4 col, float2 pixel)
    {
        float noise = frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715)))) * 2.0 - 1.0;
    #if defined(UNITY_COLORSPACE_GAMMA)
        // Gamma targets step evenly
        const float2 level = float2(0.5, 0.5) / 255.0;
    #else
        // An sRGB level spans about 0.0089 * L^0.58 in linear units; a + b * L stays within 0.56-1.7 of it
        const float2 level = float2(0.0005, 0.008);
    #endif
        float4 c = col;
        // RGB: noise * (a + b * color). Alpha: -noise * b * (1 - alpha), which scales the destination by 1 + noise * b
        float4 offset = noise * (c * level.y + float4(level.xxx, -level.y));
        // Symmetric limits keep the noise unbiased where the shape barely covers what's below
        float4 limit = min(c, 1.0 - c);
        return c + clamp(offset, -limit, limit);
    }

    // Gradient helpers
    inline float2 GetGradientCoordinates(float mode, float2 sdfCoord, float2 halfSize, float2 screenPosition)
    {
        float2 coord;
        // Odd gradient modes use screen coordinates; even modes use local coordinates
        UNITY_BRANCH
        if (frac(mode * 0.5) > 0.25)
        {
            // SV_POSITION is already in pixels: no extra vertex data or interpolator
            coord = screenPosition / _ScreenParams.xy * 2.0 - 1.0;
            #if UNITY_UV_STARTS_AT_TOP
            coord.y = -coord.y;
            #endif
            coord.y *= _ProjectionParams.x;
        }
        else
            coord = sdfCoord / max(halfSize, 0.0001);
        return coord;
    }

    float2 ResolveGradientDirection(float angleDeg, float2 direction)
    {
        if (dot(direction, direction) > 0.5)
            return direction;

        float angleRad = angleDeg * (3.14159265 / 180.0);
        return float2(cos(angleRad), sin(angleRad));
    }

    inline float GetGradientTime(float2 coord, float2 direction, float zoom, float mode)
    {
        zoom = max(zoom, 0.01);
        float t = 0.0;
        UNITY_BRANCH
        if (mode < 3.5)
            t = dot(coord, direction) * 0.5 / zoom + 0.5;
        else
        {
            UNITY_BRANCH
            if (mode < 5.5)
                t = length(coord) / zoom;
            else
            {
                // Rotate the seam using the existing CPU direction. Avoid atan2(0, 0) at the center.
                float2 rotated = float2(dot(coord, direction), dot(coord, float2(-direction.y, direction.x)));
                rotated.x += dot(coord, coord) == 0.0 ? 1.0 : 0.0;
                t = frac(atan2(rotated.y, rotated.x) * (1.0 / 6.28318531) + 1.0) / zoom;
            }
        }
        return saturate(t);
    }

    inline half4 SampleMaterialLayer(half mode, half4 color, sampler2D gradient,
        float angle, float2 direction, float zoom, float reverse,
        float2 coord, float2 halfSize, half4 vertexColor, float2 screenPosition)
    {
        if (mode < 0.5) return half4(vertexColor.rgb, 1);
        if (mode < 1.5) return UI_ColorSpaceSync(color);
        float2 normUV = GetGradientCoordinates(mode, coord, halfSize, screenPosition);
        float t = GetGradientTime(normUV, ResolveGradientDirection(angle, direction), zoom, mode);
        if (reverse > 0.5) t = 1.0 - t;
        // Ramp textures hold GradientRes samples at i / 255, like the atlas rows
        return UI_ColorSpaceSync(tex2D(gradient, float2(t * (255.0 / 256.0) + 0.5 / 256.0, 0.5)));
    }

    inline half4 SampleShapeGradient(float t, float row, float2 mapping)
    {
        // One bilinear tap. Linear pages store sRGB texels, which the GPU decodes before filtering, so dark ramps and
        // hard stops interpolate in linear space at full texel precision. Texel i holds the ramp at i / 255: t maps to
        // texel centers, so stops land where they were placed.
        return UI_StyleColorSpaceSync(tex2Dlod(_ShapeGradientAtlas, float4((t * (255.0 / 256.0) + 0.5 / 256.0) * mapping.x + mapping.y, row, 0, 0)));
    }

    inline half4 SampleConfiguredGradient(float4 config, float gradientRow,
        float layer, float2 sdfCoord, float2 halfSize, float2 screenPosition)
    {
        float2 normUV = GetGradientCoordinates(config.z, sdfCoord, halfSize, screenPosition);
        // Preserve the original uniform precision and order of operations
        half2 direction = config.xy;
        half zoom = abs(config.w);
        float t = GetGradientTime(normUV, (float2)direction, (float)zoom, config.z);
        if (config.w < 0.0) t = 1.0 - t;
        return SampleShapeGradient(t, gradientRow, float2(0.2, layer * 0.2));
    }

    // Style rows are shared across materials, including Unity's stencil copies.
    // Read dimensions from a fixed header texel so atlas growth needs no mesh or
    // material-copy updates. Row zero in vertex data selects the legacy path.
    inline half4 SampleShapeLayer(float styleIndex, float2 dimensions, float layer, float2 sdfCoord, float2 halfSize, half4 vertexColor, float2 screenPosition)
    {
        float row = (styleIndex + 0.5) * dimensions.x;
        float4 config = tex2Dlod(_ShapeData, float4((layer * 2.0 + 1.5) / EVO_UI_SHAPE_DATA_WIDTH, row, 0, 0));
        if (config.z < 0.5)
            return half4(vertexColor.rgb, 1.0);
        if (config.z < 1.5)
            return UI_StyleColorSpaceSync(tex2Dlod(_ShapeData, float4((layer * 2.0 + 0.5) / EVO_UI_SHAPE_DATA_WIDTH, row, 0, 0)));

        return SampleConfiguredGradient(config, dimensions.y, layer, sdfCoord, halfSize, screenPosition);
    }

    inline float4 ReadPattern(float2 row, float column)
    {
        return tex2Dlod(_ShapePatterns, float4((column + 0.5) * row.y, row.x, 0, 0));
    }

    half4 SamplePattern(v2f i, float2 halfSize, out bool hasFillSprite)
    {
        float2 row = float2(abs(i.innerShd.w), tex2Dlod(_ShapePatterns, float4(0, 0, 0, 0)).y);
        float4 size = ReadPattern(row, 2.0);
        float4 transform = ReadPattern(row, 3.0);
        float4 source = ReadPattern(row, 4.0);
        hasFillSprite = fmod(floor(source.z / 2.0), 2.0) < 0.5;

        float2 coord = float2(dot(i.sdfCoord, transform.xy), dot(i.sdfCoord, float2(-transform.y, transform.x)));
        coord -= transform.zw;
        // Differentiate before wrapping, including across negative offsets and cell seams
        float patternAA = max(0.5 * length(fwidth(coord)), 0.00001);
        float2 tile = (frac(coord * size.xy + 0.5) - 0.5) / size.xy;
        half4 sample = 1.0;
        float distance;
        UNITY_BRANCH
        if (source.x < -0.5)
        {
            float4 uv = ReadPattern(row, 5.0);
            float2 spriteUV = lerp(uv.xy, uv.zw, saturate(tile / (size.zw * 2.0) + 0.5));
            sample = tex2Dlod(_MainTex, float4(spriteUV, 0, 0));
            UNITY_BRANCH
            if (fmod(source.z, 2.0) > 0.5)
                sample.a *= tex2Dlod(_AlphaTex, float4(spriteUV, 0, 0)).r;
            distance = Evo_SdRoundedBox(tile, size.zw, 0.0, 0.0);
        }
        else
            distance = Evo_SdProceduralShape(tile, size.zw, source.yyyy, 0.0, source.x);

        float coverage = 1.0 - smoothstep(-patternAA, patternAA, distance);
        UNITY_BRANCH
        if (source.z > 3.5)
        {
            float4 falloff = ReadPattern(row, 6.0);
            float fade = saturate((length(i.sdfCoord - (falloff.xy * 2.0 - 1.0) * halfSize) - falloff.z) * falloff.w);
            fade = fade * fade * (3.0 - 2.0 * fade);
            coverage *= 1.0 - fade;
        }

        float4 config = ReadPattern(row, 1.0);
        half4 color;
        if (config.z < 0.5)
            color = half4(i.color.rgb, 1.0);
        else if (config.z < 1.5)
            color = UI_StyleColorSpaceSync(ReadPattern(row, 0.0));
        else
            color = SampleConfiguredGradient(config, i.spriteUV.w, 4.0, i.sdfCoord, halfSize, i.vertex.xy);
        color *= sample;
        color.a *= coverage;
        return Premultiply(color);
    }

    float SmoothOperationMin(float a, float b, float k)
    {
        float h = max(k - abs(a - b), 0.0) / max(k, 0.00001);
        return min(a, b) - h * h * k * 0.25;
    }

    // Evo_UI_GetShapeDistance without the linear rounded clip, which the evaluator below applies first.
    // Radial rounded clips take clipDistance = Evo_UI_GetClipDistance(p, halfSize, amount, packed) when 0 < amount < 1,
    // so one evaluation can also serve the operand's own clipping.
    float GetOperandDistance(float2 p, float2 halfSize, float4 radii, float4 plainRadii, float isSquircle,
        float shapeType, bool roundClip, float width, float amount, float clipDistance)
    {
        if (roundClip && amount <= 0.0)
            return 100000.0;

        // Preserve the ellipse's precomputed, trigonometry-free arc path.
        if (roundClip && abs(shapeType - EVO_UI_SHAPE_ELLIPSE) < 0.5)
            return Evo_SdRoundArc(p, halfSize, width, amount, radii);

        float4 shapeRadii = plainRadii;
        if (roundClip)
            shapeRadii = min(radii, min(halfSize.x, halfSize.y));

        float distance = Evo_SdProceduralShape(p, halfSize, shapeRadii, isSquircle, shapeType);
        if (!roundClip)
            return distance;

        float halfWidth = width * 0.5;
        float centerDistance = distance + halfWidth;
        if (amount >= 1.0)
            return abs(centerDistance) - halfWidth;

        // Round the clipped stroke in distance space without additional shape samples.
        return length(float2(centerDistance, max(clipDistance, 0.0))) - halfWidth;
    }

    // One evaluator serves the fill and both shadows, with optional boolean operands.
    // Dynamic loops keep the compiler from duplicating the SDF for each operand/layer.
    float3 GetShapeDistances(v2f i, float innerRadius, float outerRadius)
    {
        float2 row = 0.0;
        int count = 0;
        UNITY_BRANCH
        if (i.rectData.x < 0.0)
        {
            row = float2(abs(i.innerShd.w), tex2Dlod(_ShapePatterns, float4(0, 0, 0, 0)).y);
            count = min((int)ReadPattern(row, 7.0).x, 4);
        }
        bool innerSample = innerRadius > 0.0 || any(i.innerShd.xy != 0.0);
        bool outerSample = outerRadius > 0.0 || any(i.outerShd.xy != 0.0);
        float flags = floor(i.rectData.w + 0.5);
        float3 distances = 0.0;
        int samples = outerSample ? 3 : innerSample ? 2 : 1;
        [loop]
        for (int n = 0; n <= count; n++)
        {
            float4 rowX = float4(1, 0, 0, 1);
            float4 rowY = float4(0, 1, 0, 0);
            float4 shape = float4(abs(i.rectData.xy) * 0.5, Evo_UI_Mod(floor(flags / 4.0), 16.0), Evo_UI_Mod(floor(flags / 2.0), 2.0));
            float4 radii = i.radii;
            float4 clipping = float4(i.fxData.z, Evo_UI_Mod(floor(i.fxData.w + 0.5), 512.0), i.fxData.x, 0);
            if (n > 0)
            {
                float column = 8.0 + (n - 1) * 5.0;
                rowX = ReadPattern(row, column);
                rowY = ReadPattern(row, column + 1.0);
                shape = ReadPattern(row, column + 2.0);
                radii = ReadPattern(row, column + 3.0);
                clipping = ReadPattern(row, column + 4.0);
            }
            [loop]
            for (int sampleIndex = 0; sampleIndex < samples; sampleIndex++)
            {
                if (sampleIndex == 1 && !innerSample)
                    continue;
                float2 p = i.sdfCoord - (sampleIndex == 1 ? i.innerShd.xy : sampleIndex == 2 ? i.outerShd.xy : float2(0, 0));
                float2 q = n == 0 ? p : float2(dot(rowX.xy, p) + rowX.z, dot(rowY.xy, p) + rowY.z);
                float2 halfSize = shape.xy;
                float4 sampleRadii = min(radii, min(halfSize.x, halfSize.y));
                // Packed configs stay below 512; the rounded-clip flag is 256
                bool roundRadial = frac(clipping.y * (1.0 / 512.0)) >= 0.5;
                // Branches keep rarely used clip math off every other shape's path
                UNITY_BRANCH
                if (roundRadial)
                {
                    if (Evo_UI_Mod(round(clipping.y), 8.0) < 2.5)
                    {
                        Evo_UI_ApplyRoundedLinearClip(q, halfSize, sampleRadii, clipping.x, clipping.y);
                        roundRadial = false;
                    }
                }
                if (n == 0 && sampleIndex > 0)
                    sampleRadii = max(sampleRadii, sampleIndex == 1 ? innerRadius : outerRadius);
                // Radial rounded clips leave halfSize at shape.xy
                // One clip evaluation serves rounded radial strokes (except the ellipse's arc) and clipped operands
                float clipDistance = 0.0;
                bool operandClip = false;
                UNITY_BRANCH
                if (roundRadial || n > 0)
                {
                    operandClip = n > 0 && !roundRadial && clipping.y < 256.0 && Evo_UI_Mod(clipping.y, 8.0) > 0.5 && clipping.x < 1.0;
                    if (operandClip || (roundRadial && clipping.x > 0.0 && clipping.x < 1.0 && abs(shape.z - EVO_UI_SHAPE_ELLIPSE) >= 0.5))
                        clipDistance = Evo_UI_GetClipDistance(q, shape.xy, clipping.x, clipping.y);
                }
                float operand = GetOperandDistance(q, halfSize, radii, sampleRadii, shape.w, shape.z,
                    roundRadial, clipping.z, clipping.x, clipDistance);
                if (operandClip)
                    operand = max(operand, clipDistance);
                if (Evo_UI_Mod(clipping.y, 8.0) > 0.5 && clipping.x <= 0.0)
                    operand = 1e10;
                operand *= rowX.w;
                float distance = sampleIndex == 0 ? distances.x : sampleIndex == 1 ? distances.y : distances.z;
                float k = rowY.w;
                if (n == 0) { distance = operand; }
                else if (clipping.w < 0.5) { distance = SmoothOperationMin(distance, operand, k); }
                else if (clipping.w < 1.5) { distance = -SmoothOperationMin(-distance, operand, k); }
                else if (clipping.w < 2.5) { distance = -SmoothOperationMin(-distance, -operand, k); }
                else
                {
                    float unionDistance = SmoothOperationMin(distance, operand, k);
                    float intersection = -SmoothOperationMin(-distance, -operand, k);
                    distance = -SmoothOperationMin(-unionDistance, intersection, k);
                }
                if (sampleIndex == 0) { distances.x = distance; }
                else if (sampleIndex == 1) { distances.y = distance; }
                else { distances.z = distance; }
            }
        }
        distances.y = innerSample ? distances.y : distances.x;
        distances.z = outerSample ? distances.z : distances.x;
        return distances;
    }

    // Vertex
    v2f vert(appdata v)
    {
        v2f o = (v2f)0;
        UNITY_SETUP_INSTANCE_ID(v);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
        o.vertex    = UnityObjectToClipPos(v.vertex);
        o.worldPos  = v.vertex;
        o.screen = ComputeScreenPos(o.vertex);
        
        half4 vcolor = UI_ColorSpaceSync(v.color);
        o.color     = vcolor * _Color; 
        
        o.sdfCoord  = v.uv0.xy;
        o.spriteUV  = float4(v.uv0.zw, 0.0, 0.0);
        
        o.rectData  = v.uv1;
        o.rectData.z = v.uv1.z < 0.0 ? -v.uv1.z - 1.0 : v.uv1.z;
        // fxData and innerShd are built here at full precision; the light program passes only colors on (see the end)
        float4 fxData = v.uv2;
        fxData.x = abs(v.uv2.x);
        // Negative inner softness enables dithering, passed on as an effect-row offset (see the fragment's effects branch)
        fxData.y = abs(v.uv2.y);
        fxData.w = fmod(v.uv2.w, 512.0) + (EvoEffectsActiveRow(floor(v.uv2.w / 512.0)) + (v.uv2.y < 0.0 ? EVO_UI_DITHER_ROW : 0.0)) * 512.0;
        o.radii     = v.uv3;
        // Explicit materials keep the vertex payload
        o.outerShd  = v.tangent; 
        float4 innerShd = float4(v.normal, 4.0);
        float styleIndex = floor(v.uv1.w / 128.0);
        o.dash = _OutlineDash;
        o.fillColor = half4(0, 0, 0, -1);
        float row = 0.0;
        if (styleIndex > 0.5)
        {
            // Read the shared header once per vertex, not for every shaded layer
            float3 header = tex2Dlod(_ShapeData, float4(0, 0, 0, 0)).xyz;
            row = (styleIndex + 0.5) * header.x;
            innerShd.w = header.z;
            // Patterns and operations read a compact pattern row, located once per vertex
            if (v.uv1.z < 0.0 || v.uv1.x < 0.0)
                innerShd.w = (tex2Dlod(_ShapeData, float4(11.5 / EVO_UI_SHAPE_DATA_WIDTH, row, 0, 0)).x + 1.5) * tex2Dlod(_ShapePatterns, float4(0, 0, 0, 0)).x;
            if (v.uv1.z < 0.0)
                innerShd.w = -innerShd.w;
            // Canvas batching transforms tangents and normals by the graphic's matrix.
            // Style rows keep shadow offsets and sizes in local units at any scale or rotation.
            o.outerShd = tex2Dlod(_ShapeData, float4(9.5 / EVO_UI_SHAPE_DATA_WIDTH, row, 0, 0));
            float4 innerShadow = tex2Dlod(_ShapeData, float4(10.5 / EVO_UI_SHAPE_DATA_WIDTH, row, 0, 0));
            innerShd.xyz = innerShadow.xyz;
    #if !defined(EVO_UI_LITE)
            // W holds the gradient atlas row; only styles with a gradient have one (the light fragment reads it itself)
            o.spriteUV.zw = float2(header.x, (innerShadow.w + 0.5) * header.y);
    #endif
            // Solid fills are constant over the quad: fetch/convert four times,
            // instead of twice for every covered pixel. Gradients remain per pixel.
            if (fmod(v.uv1.w, 2.0) > 0.5)
            {
                float mode = tex2Dlod(_ShapeData, float4(1.5 / EVO_UI_SHAPE_DATA_WIDTH, row, 0, 0)).z;
                if (mode < 0.5)
                    o.fillColor = half4(o.color.rgb, 1);
                else if (mode < 1.5)
                    o.fillColor = UI_StyleColorSpaceSync(tex2Dlod(_ShapeData, float4(0.5 / EVO_UI_SHAPE_DATA_WIDTH, row, 0, 0)));
            }
            o.dash = 0.0;
            if (v.uv2.x < 0.0)
            {
                o.dash = tex2Dlod(_ShapeData, float4(8.5 / EVO_UI_SHAPE_DATA_WIDTH, row, 0, 0));
            }
        }
        if (v.uv1.x < 0.0)
            o.dash = 0.0;
    #if !defined(EVO_UI_LITE)
        if (o.dash.x > 0.0)
        {
            float2 b = abs(v.uv1.xy) * 0.5;
            float4 radii = min(v.uv3, min(b.x, b.y));
            float shape = fmod(floor(v.uv1.w / 4.0), 16.0);
            float roundedClip = fmod(floor(v.uv2.w / 256.0), 2.0);
            if (roundedClip > 0.5)
            {
                if (fmod(round(v.uv2.w), 8.0) < 2.5)
                {
                    float2 unused = 0.0;
                    Evo_UI_ApplyRoundedLinearClip(unused, b, radii, v.uv2.z, v.uv2.w);
                }
                else if (shape == EVO_UI_SHAPE_ELLIPSE)
                    b = min(b.x, b.y);
            }
            o.dash = Evo_UI_DashPattern(b, radii, fmod(floor(v.uv1.w / 2.0), 2.0), shape, fxData.x, o.dash);
        }
    #endif

        // Plain rectangles and ellipses take the fragment's fast path: solid fill, outline and outer shadow, patterns of
        // rectangle and ellipse tiles in a solid color, sprite, edge softness and dithering. Operations, gradients, dashes,
        // partial clipping, other patterns, active effects, inner shadows and backdrop blur keep the full path. Only plain
        // shapes get the repacked data below, so the full path always reads the original vertex data.
        // The light program has no full path: its page only holds shapes that the fast path draws with its extensions
        // (gradients, polygons, fill clipping and backdrop blur), so every shape is plain there.
        bool pattern = v.uv1.z < 0.0;
        float2 patternRow = 0.0;
        float4 patternSource = -1.0;
        float patternMode = 2.0;
        if (pattern && styleIndex > 0.5)
        {
            // Pattern rows are located once per vertex; the fast path takes shape tiles in a solid or base color
            patternRow = float2(abs(innerShd.w), tex2Dlod(_ShapePatterns, float4(0, 0, 0, 0)).y);
            patternSource = ReadPattern(patternRow, 4.0);
            patternMode = ReadPattern(patternRow, 1.0).z;
        }
        float clipPacked = fmod(fxData.w, 512.0);
        float effectRow = floor(fxData.w / 512.0);
        float shapeID = fmod(floor(v.uv1.w / 4.0), 16.0);
        bool fill = fmod(v.uv1.w, 2.0) > 0.5;
        bool outline = fxData.x > 0.001;
        bool shadow = o.outerShd.z > 0.001 || o.outerShd.w > 0.001 || dot(o.outerShd.xy, o.outerShd.xy) > 0.001;
        bool plainPattern = patternMode < 1.5 && patternSource.x > -0.5 && patternSource.x < 1.5;
        bool plainShape = !EVO_UI_BACKDROP && shapeID < 1.5 && styleIndex > 0.5 && v.uv1.x >= 0.0 && (!pattern || plainPattern)
            && (!fill || o.fillColor.a >= 0.0) && o.dash.x <= 0.0
            && (effectRow < 0.5 || effectRow == EVO_UI_DITHER_ROW) && clipPacked < 256.0
            && (fmod(round(clipPacked), 8.0) < 0.5 || fxData.z >= 1.0)
            && innerShd.z <= 0.001 && fxData.y <= 0.001 && dot(innerShd.xy, innerShd.xy) <= 0.001;
        float plainBits = 0.0;
    #if defined(EVO_UI_LITE)
        plainShape = true;
        // Clip amount and flags move to a slot the fast path doesn't otherwise read, ahead of the shadow color
        if (clipPacked >= 256.0 || (fmod(round(clipPacked), 8.0) > 0.5 && fxData.z < 1.0))
        {
            o.dash.zw = float2(fxData.z, clipPacked);
            plainBits = 256.0;
        }
        // Base fills resolved above; any other unresolved fill is a gradient
        if (fill && o.fillColor.a < 0.0)
            o.fillColor.a = -2.0;
    #endif
        if (plainShape && (outline || shadow))
        {
            float outlineMode = outline ? tex2Dlod(_ShapeData, float4(3.5 / EVO_UI_SHAPE_DATA_WIDTH, row, 0, 0)).z : 0.0;
            float shadowMode = shadow ? tex2Dlod(_ShapeData, float4(7.5 / EVO_UI_SHAPE_DATA_WIDTH, row, 0, 0)).z : 0.0;
    #if !defined(EVO_UI_LITE)
            plainShape = outlineMode < 1.5 && shadowMode < 1.5;
    #endif
            // Colors go into flat slots the fast path doesn't otherwise read: the outline color into the inner shadow,
            // the shadow color into fxData (its outline width moves to dash.y). Alpha -1 selects the vertex color, -2 a gradient.
            if (plainShape && outline)
                innerShd = outlineMode < 0.5 ? float4(0, 0, 0, -1) : UI_StyleColorSpaceSync(tex2Dlod(_ShapeData, float4(2.5 / EVO_UI_SHAPE_DATA_WIDTH, row, 0, 0)));
            if (plainShape && shadow)
                fxData = shadowMode < 0.5 ? float4(0, 0, 0, -1) : UI_StyleColorSpaceSync(tex2Dlod(_ShapeData, float4(6.5 / EVO_UI_SHAPE_DATA_WIDTH, row, 0, 0)));
    #if defined(EVO_UI_LITE)
            if (outline && outlineMode > 1.5)
                innerShd.w = -2.0;
            if (shadow && shadowMode > 1.5)
                fxData.w = -2.0;
    #endif
        }
        // A negative dash length marks plain shapes and carries their feature bits; real dash lengths are positive
        plainBits += (fill ? 1.0 : 0.0) + (outline ? 2.0 : 0.0) + (shadow ? 4.0 : 0.0)
            + (fmod(floor(clipPacked / 128.0), 2.0) > 0.5 ? 8.0 : 0.0) + (effectRow > 0.5 ? 16.0 : 0.0) + (abs(shapeID - 1.0) < 0.5 ? 32.0 : 0.0)
            + (fmod(floor(v.uv1.w / 2.0), 2.0) > 0.5 ? 64.0 : 0.0) + (fmod(floor(v.uv1.w / 64.0), 2.0) > 0.5 ? 128.0 : 0.0);
        if (plainShape && pattern)
        {
            // The pattern row and its texel width go into a slot the fast path doesn't otherwise read.
            // The row also says whether the fill has a sprite (the pattern's own shape needs no texture).
            plainBits += 512.0 + (patternSource.x > 0.5 && patternSource.x < 1.5 ? 1024.0 : 0.0) + (patternMode < 0.5 ? 2048.0 : 0.0)
                + (patternSource.z > 3.5 ? 4096.0 : 0.0);
            o.dash.zw = patternRow;
            o.rectData.y = fmod(floor(patternSource.z / 2.0), 2.0) < 0.5 ? abs(o.rectData.y) : -abs(o.rectData.y);
        }
        if (plainShape)
            o.dash.y = abs(v.uv2.x);
    #if defined(EVO_UI_LITE)
        // Polygons carry their shape ID above the feature bits, so the fast path never decodes the shape flags.
        // That slot holds the style row instead, which gradients read. The ID is stored as 1-6 to keep the value small:
        // WebGL and WebGPU interpolate this slot, so a large value could round to a neighbor on tilted canvases.
        if (shapeID > 9.5)
            plainBits += (shapeID - 9.0) * 8192.0;
        o.rectData.w = row;
    #endif
        o.dash.x = plainShape ? -1.0 - plainBits : max(o.dash.x, 0.0);
    #if defined(EVO_UI_LITE)
        // Only colors remain in these slots on light pages; anything else would overflow half precision
        o.innerShd = outline ? innerShd : 0.0;
        o.fxData = shadow ? fxData : 0.0;
    #else
        o.innerShd = innerShd;
        o.fxData = fxData;
    #endif
        return o;
    }

    // Full evaluator: every shape, feature, operation and effect
    half4 ShadeFullShape(v2f i)
    {
        float2 rectSize       = abs(i.rectData.xy);
        float  softness       = i.rectData.z;
        
        // Unpack properties (Floor ensures floating point consistency across rendering pipeline limits)
        float  shapeAndFlags  = floor(i.rectData.w + 0.5);
        float  styleIndex     = floor(shapeAndFlags / 128.0);
        float  doFill         = Evo_UI_Mod(shapeAndFlags, 2.0);
        float  isSquircle     = Evo_UI_Mod(floor(shapeAndFlags / 2.0), 2.0);
        float  shapeType      = Evo_UI_Mod(floor(shapeAndFlags / 4.0), 16.0);
        float  useAlphaTex    = Evo_UI_Mod(floor(shapeAndFlags / 64.0), 2.0);
        
        float  outlineWidth   = i.fxData.x;
        float  innerSoftness  = i.fxData.y;
        float  fillAmount     = i.fxData.z;
        // Rounded: WebGL interpolates every slot, which can leave a packed value an ulp off
        float  fxPacked       = floor(i.fxData.w + 0.5);
        float  effectRow      = floor(fxPacked / 512.0);
        float  fillPacked     = Evo_UI_Mod(fxPacked, 512.0);
        float  outerOutsideOnly = Evo_UI_Mod(floor(fillPacked / 128.0), 2.0);
        float  roundClip       = Evo_UI_Mod(floor(fillPacked / 256.0), 2.0);

        float2 outerOffset    = i.outerShd.xy;
        float  outerSize      = i.outerShd.z;
        float  outerSoftness  = i.outerShd.w;

        float2 innerOffset    = i.innerShd.xy;
        float  innerSize      = i.innerShd.z;

        float2 halfSize = rectSize * 0.5;
        float4 r        = min(i.radii, min(halfSize.x, halfSize.y));

        // Keep original coordinates for color mapping;
        // prepare clipped geometry once and reuse it for every distance sample, including shadows.
        float2 shapeCoord = i.sdfCoord;
        float2 shapeHalfSize = halfSize;
        float roundRadial = roundClip;
        UNITY_BRANCH
        if (roundClip > 0.5 && Evo_UI_Mod(round(fillPacked), 8.0) < 2.5)
        {
            Evo_UI_ApplyRoundedLinearClip(shapeCoord, shapeHalfSize, r, fillAmount, fillPacked);
            roundRadial = 0.0;
        }
        
        bool operations = i.rectData.x < 0.0;
        // Preserve the rectangle shadow crease correction only for individual primitives.
        bool smoothShadow = !operations && shapeType == EVO_UI_SHAPE_RECTANGLE && roundRadial < 0.5;
        float innerRadius = 0.0;
        float outerRadius = 0.0;
        UNITY_BRANCH
        if (smoothShadow && (innerSoftness > 0.001 || outerSoftness > 0.001))
        {
            innerRadius = min(max(innerSoftness + innerSize, 0.0), min(shapeHalfSize.x, shapeHalfSize.y));
            outerRadius = min(max(outerSoftness - outerSize, 0.0), min(shapeHalfSize.x, shapeHalfSize.y));
            innerRadius = innerSoftness > 0.001 && any(r < innerRadius) ? innerRadius : 0.0;
            outerRadius = outerSoftness > 0.001 && any(r < outerRadius) ? outerRadius : 0.0;
        }
        float3 distances = GetShapeDistances(i, innerRadius, outerRadius);
        float dist = distances.x;
        // One clip evaluation serves the fill mask and the effects' border
        bool fillClip = roundClip < 0.5 && Evo_UI_Mod(round(fillPacked), 8.0) > 0.5 && fillAmount < 1.0;
        float fillClipDistance = 0.0;
        UNITY_BRANCH
        if (fillClip)
            fillClipDistance = Evo_UI_GetClipDistance(i.sdfCoord, halfSize, fillAmount, fillPacked);
        float clipMask = 1.0;
        if (roundClip > 0.5)
            clipMask = (fillAmount > 0.0) ? 1.0 : 0.0;
        else if (fillClip)
        {
            float clipAA = max(fwidth(fillClipDistance) * 0.5, 0.00001);
            clipMask = (fillAmount > 0.0) ? 1.0 - smoothstep(-clipAA, clipAA, fillClipDistance) : 0.0;
        }
        float aa = max(fwidth(dist) * 0.5 + softness, 0.00001);
        float shapeMask = 1.0 - smoothstep(-aa, aa, dist);

        // Fill Base color mapping
        half4 fillCol = half4(0.0, 0.0, 0.0, 0.0);
        UNITY_BRANCH
        if (doFill > 0.5)
        {
            if (i.fillColor.a >= 0.0) {
                fillCol = i.fillColor;
            } else if (styleIndex > 0.5) {
                fillCol = SampleShapeLayer(styleIndex, i.spriteUV.zw, 0.0, i.sdfCoord, halfSize, i.color, i.vertex.xy);
            } else {
                fillCol = SampleMaterialLayer(_FillColorMode, _FillColor, _FillGradientTex, _FillGradientAngle, 
                    _FillGradientDirection.xy, _FillGradientZoom, _FillGradientReverse, i.sdfCoord, halfSize, i.color, i.vertex.xy);
            }

            half4 patternCol = 0.0;
            bool hasFillSprite = i.rectData.y > 0.0;
            UNITY_BRANCH
            if (i.innerShd.w < 0.0)
                patternCol = SamplePattern(i, halfSize, hasFillSprite);

            // A sprite used only by the pattern must not also become the background image
            half4 spriteCol = 1.0;
            UNITY_BRANCH
            if (hasFillSprite)
                spriteCol = tex2D(_MainTex, i.spriteUV.xy);
            UNITY_BRANCH
            if (useAlphaTex > 0.5) { spriteCol.a *= tex2D(_AlphaTex, i.spriteUV.xy).r; }
            UNITY_BRANCH
            if (EVO_UI_BACKDROP)
                spriteCol = EvoBackdropEvaluate(spriteCol, i.screen.xy / i.screen.w, _ScreenParams.xy);
            fillCol *= spriteCol;
            fillCol = Premultiply(fillCol);
            // Preserve holes in a fill sprite while keeping pattern opacity independent
            UNITY_BRANCH
            if (i.innerShd.w < 0.0)
                fillCol = AlphaBlend(patternCol * spriteCol.a, fillCol);
        }

        // Inner Shadow
        UNITY_BRANCH
        if (innerSize > 0.001 || innerSoftness > 0.001 || dot(innerOffset, innerOffset) > 0.001)
        {
            half4 inShdCol = half4(1.0, 1.0, 1.0, 1.0);
            float inDist = distances.y;
            float inAA = max(max(fwidth(inDist) * 0.5, innerSoftness), 0.00001);
            float innerShadowMask = smoothstep(-inAA, inAA, inDist + innerSize);

            if (styleIndex > 0.5) {
                inShdCol = SampleShapeLayer(styleIndex, i.spriteUV.zw, 2.0, i.sdfCoord, halfSize, i.color, i.vertex.xy);
            } else {
                inShdCol = SampleMaterialLayer(_InnerShadowColorMode, _InnerShadowColor, _InnerShadowGradientTex,
                    _InnerShadowGradientAngle, _InnerShadowGradientDirection.xy, _InnerShadowGradientZoom, _InnerShadowGradientReverse,
                    i.sdfCoord, halfSize, i.color, i.vertex.xy);
            }
            
            inShdCol.a *= innerShadowMask;
            inShdCol = Premultiply(inShdCol);
            fillCol = AlphaBlend(inShdCol, fillCol);
        }

        // Outline
        half4 shapeCol = fillCol;
        UNITY_BRANCH
        if (outlineWidth > 0.001)
        {
            half4 outCol = half4(1.0, 1.0, 1.0, 1.0);

            if (styleIndex > 0.5) {
                outCol = SampleShapeLayer(styleIndex, i.spriteUV.zw, 1.0, i.sdfCoord, halfSize, i.color, i.vertex.xy);
            } else {
                outCol = SampleMaterialLayer(_OutlineColorMode, _OutlineColor, _OutlineGradientTex,
                    _OutlineGradientAngle, _OutlineGradientDirection.xy, _OutlineGradientZoom, _OutlineGradientReverse,
                    i.sdfCoord, halfSize, i.color, i.vertex.xy);
            }
            
            float innerMask = (roundRadial > 0.5 && !operations) ? 1.0 : smoothstep(-aa, aa, dist + outlineWidth);
            UNITY_BRANCH
            if (i.dash.x > 0.0)
            {
                float2 dashCoord = shapeCoord;
                float2 dashHalfSize = shapeHalfSize;
                if (roundRadial > 0.5 && shapeType == EVO_UI_SHAPE_ELLIPSE)
                {
                    float radius = min(halfSize.x, halfSize.y);
                    dashCoord *= radius / max(halfSize, 0.0001);
                    dashHalfSize = radius;
                }
                // Use screen-space pixel size, never derivatives of a wrapped dash phase
                float dashAA = max(0.5 * length(fwidth(dashCoord)) + softness, 0.00001);
                UNITY_BRANCH
                if (innerMask > 0.0 && shapeMask > 0.0 && clipMask > 0.0)
                {
                    float dashDistance = Evo_UI_DashedOutline(dashCoord, dashHalfSize, r, isSquircle,
                        shapeType, outlineWidth, dashAA, i.dash);
                    innerMask *= 1.0 - smoothstep(-dashAA, dashAA, dashDistance);
                }
            }
            outCol.a *= innerMask;
            outCol = Premultiply(outCol);

            shapeCol = AlphaBlend(outCol, shapeCol);
        }

        shapeCol *= shapeMask * clipMask;

        // Outer Shadow
        half4 outShdCol = half4(0.0, 0.0, 0.0, 0.0);

        UNITY_BRANCH
        if (outerSize > 0.001 || outerSoftness > 0.001 || dot(outerOffset, outerOffset) > 0.001)
        {
            float shdDist = distances.z - outerSize;
            float shdAA = max(max(fwidth(shdDist) * 0.5, outerSoftness), 0.00001);
            float outerShadowMask = 1.0 - smoothstep(-shdAA, shdAA, shdDist);

            if (outerOutsideOnly > 0.5)
                outerShadowMask *= 1.0 - shapeMask;

            if (styleIndex > 0.5) {
                outShdCol = SampleShapeLayer(styleIndex, i.spriteUV.zw, 3.0, i.sdfCoord, halfSize, i.color, i.vertex.xy);
            } else {
                outShdCol = SampleMaterialLayer(_OuterShadowColorMode, _OuterShadowColor, _OuterShadowGradientTex,
                    _OuterShadowGradientAngle, _OuterShadowGradientDirection.xy, _OuterShadowGradientZoom,
                    _OuterShadowGradientReverse, i.sdfCoord, halfSize, i.color, i.vertex.xy);
            }
            
            outShdCol.a *= outerShadowMask * clipMask;
            outShdCol = Premultiply(outShdCol);
        }

        // Final Composite
        half4 col = AlphaBlend(shapeCol, outShdCol);

        // Apply Master Tint Alpha
        col *= i.color.a;
        
        // Tint only visible shape coverage, preserving sprite holes, outline-only shapes,
        // rounded clipping and antialiasing. Effects that ignore graphic alpha fill the shape's
        // geometry instead and cover the outer shadow beneath it.
        // Dithered shapes share this branch through a row offset, so plain shapes test nothing new
        UNITY_BRANCH
        if (effectRow > 0.5)
        {
            float dither = step(EVO_UI_DITHER_ROW, effectRow);
            effectRow -= dither * EVO_UI_DITHER_ROW;
            UNITY_BRANCH
            if (effectRow > 0.5)
            {
                float borderDistance = dist;
                if (fillClip)
                    borderDistance = max(borderDistance, fillClipDistance);
                half4 shape = shapeCol * i.color.a;
                half4 under = outShdCol * ((1.0 - shapeCol.a) * i.color.a);
                col = EvoEffectsEvaluate(effectRow, i.sdfCoord / max(rectSize, 0.0001), rectSize, borderDistance, i.spriteUV.xy,
                    shape, under, shapeMask * clipMask);
            }

            UNITY_BRANCH
            if (dither > 0.5)
                col = Evo_UI_Dither(col, i.vertex.xy);
        }

        return col;
    }

    // SamplePattern for plain shapes: rectangle and ellipse tiles in a solid or base color, with the pattern row
    // and options resolved per vertex
    half4 SamplePlainPattern(v2f i, uint flags, float2 halfSize)
    {
        float4 size = ReadPattern(i.dash.zw, 2.0);
        float4 transform = ReadPattern(i.dash.zw, 3.0);
        float2 coord = float2(dot(i.sdfCoord, transform.xy), dot(i.sdfCoord, float2(-transform.y, transform.x)));
        coord -= transform.zw;
        float patternAA = max(0.5 * length(fwidth(coord)), 0.00001);
        float2 tile = (frac(coord * size.xy + 0.5) - 0.5) / size.xy;
        float distance;
        UNITY_BRANCH
        if ((flags & EVO_UI_PLAIN_PATTERN_ELLIPSE) != 0u) { distance = Evo_SdEllipse(tile, size.zw); }
        else { distance = Evo_SdRoundedBox(tile, size.zw, ReadPattern(i.dash.zw, 4.0).yyyy, 0.0); }
        float coverage = 1.0 - smoothstep(-patternAA, patternAA, distance);
        UNITY_BRANCH
        if ((flags & EVO_UI_PLAIN_PATTERN_FALLOFF) != 0u)
        {
            float4 falloff = ReadPattern(i.dash.zw, 6.0);
            float fade = saturate((length(i.sdfCoord - (falloff.xy * 2.0 - 1.0) * halfSize) - falloff.z) * falloff.w);
            fade = fade * fade * (3.0 - 2.0 * fade);
            coverage *= 1.0 - fade;
        }

        half4 color;
        UNITY_BRANCH
        if ((flags & EVO_UI_PLAIN_PATTERN_BASE) != 0u)
            color = half4(i.color.rgb, 1.0);
        else
            color = UI_StyleColorSpaceSync(ReadPattern(i.dash.zw, 0.0));
        color.a *= coverage;
        return Premultiply(color);
    }

    // Layer colors of plain shapes: alpha >= 0 is the color, -1 the vertex color, -2 a gradient (light program only)
    // Gradients are only sampled where the layer shows: the result is multiplied by its coverage, which is exactly zero
    // in the padding, inside an outline and under an opaque fill.
    half4 PlainLayerColor(v2f i, half4 packed, float layer, float2 halfSize, bool extended, float coverage)
    {
        half4 color = packed;
        UNITY_BRANCH
        if (extended && packed.a < -1.5)
        {
            color = 0.0;
            UNITY_BRANCH
            if (coverage > 0.0)
            {
                // The gradient row comes from the data texture: on mobile GPUs a fetch is cheaper than another
                // interpolated value. The vertex stage put the style row in rectData.w and marked the layer as a gradient.
                float gradientRow = (tex2Dlod(_ShapeData, float4(10.5 / EVO_UI_SHAPE_DATA_WIDTH, i.rectData.w, 0, 0)).w + 0.5)
                    * tex2Dlod(_ShapeData, float4(0, 0, 0, 0)).y;
                float4 config = tex2Dlod(_ShapeData, float4((layer * 2.0 + 1.5) / EVO_UI_SHAPE_DATA_WIDTH, i.rectData.w, 0, 0));
                color = SampleConfiguredGradient(config, gradientRow, layer, i.sdfCoord, halfSize, i.vertex.xy);
            }
        }
        else if (packed.a < 0.0)
            color = half4(i.color.rgb, 1.0);
        return color;
    }

    // Distance of plain shapes. The full program's fast path draws rectangles and ellipses; the light program's
    // (extended) adds polygons and draws rounded radial clips as rounded strokes, as the full path does.
    float PlainDistance(v2f i, float2 p, float2 halfSize, float4 radii, float isSquircle, bool ellipse, float shapeType,
        bool roundRadial, bool extended)
    {
        float dist = 0.0;
        if (extended)
        {
            UNITY_BRANCH
            if (roundRadial && ellipse) { dist = Evo_SdRoundArc(p, halfSize, i.dash.y, i.dash.z, i.radii); }
            else if (ellipse) { dist = Evo_SdEllipse(p, halfSize); }
            else if (shapeType < EVO_UI_POLYGON_ID(3) - 0.5) { dist = Evo_SdRoundedBox(p, halfSize, radii, isSquircle); }
            else
            {
                // Clamped: a shape the page wasn't switched for yet must never run an unbounded loop
                float radius = clamp(radii.x, 0.0, min(halfSize.x, halfSize.y));
                dist = Evo_SdPolygon(p, max(halfSize - radius, 0.0001), (uint)min(shapeType, EVO_UI_POLYGON_ID(8)) - 7u) - radius;
            }
            UNITY_BRANCH
            if (roundRadial && !ellipse)
            {
                float halfWidth = i.dash.y * 0.5;
                float centerDistance = dist + halfWidth;
                dist = i.dash.z >= 1.0 ? abs(centerDistance) - halfWidth
                    : length(float2(centerDistance, max(Evo_UI_GetClipDistance(p, halfSize, i.dash.z, i.dash.w), 0.0))) - halfWidth;
            }
        }
        else
        {
            UNITY_BRANCH
            if (ellipse) { dist = Evo_SdEllipse(p, halfSize); }
            else { dist = Evo_SdRoundedBox(p, halfSize, radii, isSquircle); }
        }
        return dist;
    }

    // Fast path for shapes the vertex stage marked plain. Same math and order as ShadeFullShape for them, without the
    // evaluator that needs more registers than mobile GPUs have (it spills to memory on every pixel). The full program
    // compiles one copy without patterns and one with them; the light program compiles one extended copy.
    half4 ShadeFastShape(v2f i, bool patternPath, bool extended)
    {
        uint flags = (uint)(-0.5 - i.dash.x);
        bool ellipse = (flags & EVO_UI_PLAIN_ELLIPSE) != 0u;
        float isSquircle = (flags & EVO_UI_PLAIN_SQUIRCLE) != 0u ? 1.0 : 0.0;
        float2 halfSize = abs(i.rectData.xy) * 0.5;
        float4 r = min(i.radii, min(halfSize.x, halfSize.y));

        // Fill clipping: rounded bars shrink the rectangle, rounded radial clips draw rounded strokes, other cuts mask
        // the result. Colors keep the original rect.
        float2 p = i.sdfCoord;
        float2 shapeHalfSize = halfSize;
        float shapeType = ellipse ? EVO_UI_SHAPE_ELLIPSE : EVO_UI_SHAPE_RECTANGLE;
        bool roundRadial = false;
        float clipMask = 1.0;
        if (extended)
        {
            uint shapeID = flags >> EVO_UI_PLAIN_SHAPE_SHIFT;
            if (shapeID > 0u)
                shapeType = (float)(shapeID + 9u);
            UNITY_BRANCH
            if ((flags & EVO_UI_PLAIN_CLIP) != 0u)
            {
                UNITY_BRANCH
                if (i.dash.w >= 256.0)
                {
                    roundRadial = Evo_UI_Mod(round(i.dash.w), 8.0) > 2.5;
                    if (!roundRadial)
                        Evo_UI_ApplyRoundedLinearClip(p, shapeHalfSize, r, i.dash.z, i.dash.w);
                }
                else
                {
                    float clipDistance = Evo_UI_GetClipDistance(i.sdfCoord, halfSize, i.dash.z, i.dash.w);
                    float clipAA = max(fwidth(clipDistance) * 0.5, 0.00001);
                    clipMask = i.dash.z > 0.0 ? 1.0 - smoothstep(-clipAA, clipAA, clipDistance) : 0.0;
                }
            }
        }

        // The pattern first, while few other values are live
        half4 patternCol = 0.0;
        if (patternPath)
        {
            UNITY_BRANCH
            if ((flags & EVO_UI_PLAIN_PATTERN) != 0u)
                patternCol = SamplePlainPattern(i, flags, halfSize);
        }

        half4 shapeCol = half4(0.0, 0.0, 0.0, 0.0);
        float shapeMask = 0.0;
        UNITY_BRANCH
        if ((flags & (EVO_UI_PLAIN_FILL | EVO_UI_PLAIN_OUTLINE | EVO_UI_PLAIN_OUTSIDE_ONLY)) != 0u)
        {
            float dist = PlainDistance(i, p, shapeHalfSize, r, isSquircle, ellipse, shapeType, roundRadial, extended);
            float aa = max(fwidth(dist) * 0.5 + i.rectData.z, 0.00001);
            shapeMask = 1.0 - smoothstep(-aa, aa, dist);

            UNITY_BRANCH
            if ((flags & EVO_UI_PLAIN_FILL) != 0u)
            {
                half4 spriteCol = 1.0;
                UNITY_BRANCH
                if (i.rectData.y > 0.0)
                    spriteCol = tex2D(_MainTex, i.spriteUV.xy);
                UNITY_BRANCH
                if ((flags & EVO_UI_PLAIN_ALPHA_TEX) != 0u) { spriteCol.a *= tex2D(_AlphaTex, i.spriteUV.xy).r; }
                if (extended)
                {
                    UNITY_BRANCH
                    if (EVO_UI_BACKDROP)
                        spriteCol = EvoBackdropEvaluate(spriteCol, i.screen.xy / i.screen.w, _ScreenParams.xy);
                    shapeCol = Premultiply(PlainLayerColor(i, i.fillColor, 0.0, halfSize, true, shapeMask * clipMask) * spriteCol);
                }
                else
                    shapeCol = Premultiply(i.fillColor * spriteCol);
                // Preserve holes in a fill sprite while keeping pattern opacity independent
                if (patternPath)
                {
                    UNITY_BRANCH
                    if ((flags & EVO_UI_PLAIN_PATTERN) != 0u)
                        shapeCol = AlphaBlend(patternCol * spriteCol.a, shapeCol);
                }
            }

            UNITY_BRANCH
            if ((flags & EVO_UI_PLAIN_OUTLINE) != 0u)
            {
                // A rounded radial clip draws the stroke itself
                float outlineMask = roundRadial ? 1.0 : smoothstep(-aa, aa, dist + i.dash.y);
                half4 outCol = PlainLayerColor(i, i.innerShd, 1.0, halfSize, extended, outlineMask * shapeMask * clipMask);
                outCol.a *= outlineMask;
                shapeCol = AlphaBlend(Premultiply(outCol), shapeCol);
            }

            shapeCol *= shapeMask;
            if (extended)
                shapeCol *= clipMask;
        }

        half4 outShdCol = half4(0.0, 0.0, 0.0, 0.0);
        UNITY_BRANCH
        if ((flags & EVO_UI_PLAIN_SHADOW) != 0u)
        {
            float outerSize = i.outerShd.z;
            float outerSoftness = i.outerShd.w;
            // Rectangle shadows round their corners up to the blur radius, as in the full path
            float4 shadowRadii = r;
            float outerRadius = min(max(outerSoftness - outerSize, 0.0), min(shapeHalfSize.x, shapeHalfSize.y));
            if (shapeType == EVO_UI_SHAPE_RECTANGLE && !roundRadial && outerSoftness > 0.001 && any(r < outerRadius))
                shadowRadii = max(r, outerRadius);

            float shdDist = PlainDistance(i, p - i.outerShd.xy, shapeHalfSize, shadowRadii, isSquircle, ellipse, shapeType, roundRadial, extended);
            shdDist -= outerSize;
            float shdAA = max(max(fwidth(shdDist) * 0.5, outerSoftness), 0.00001);
            float outerShadowMask = 1.0 - smoothstep(-shdAA, shdAA, shdDist);
            if ((flags & EVO_UI_PLAIN_OUTSIDE_ONLY) != 0u)
                outerShadowMask *= 1.0 - shapeMask;

            outShdCol = PlainLayerColor(i, i.fxData, 3.0, halfSize, extended, outerShadowMask * clipMask * (1.0 - shapeCol.a));
            outShdCol.a *= outerShadowMask;
            if (extended)
                outShdCol.a *= clipMask;
            outShdCol = Premultiply(outShdCol);
        }

        half4 col = AlphaBlend(shapeCol, outShdCol);
        col *= i.color.a;
        UNITY_BRANCH
        if ((flags & EVO_UI_PLAIN_DITHER) != 0u)
            col = Evo_UI_Dither(col, i.vertex.xy);
        return col;
    }

    half4 frag(v2f i) : SV_Target
    {
        half4 col;
    #if defined(EVO_UI_LITE)
        col = ShadeFastShape(i, true, true);
    #else
        // Feature bits from 512 up belong to patterns (see the end of vert). Patterns get their own copy of the fast
        // path, which keeps the code and the register allocation on mobile GPUs of shapes without one as it was.
        UNITY_BRANCH
        if (i.dash.x < 0.0 && i.dash.x > -512.5)
            col = ShadeFastShape(i, false, false);
        else if (i.dash.x < 0.0)
            col = ShadeFastShape(i, true, false);
        else
            col = ShadeFullShape(i);
    #endif

        // Apply Soft Mask Alpha
        SoftMask_ApplyPremultiplied(col, i.worldPos);

        // A real branch: as a select, every pixel would load worldPos without a RectMask2D
        UNITY_BRANCH
        if (UNITY_UI_CLIP_RECT)
            col *= UnityGet2DClipping(i.worldPos.xy, _ClipRect);

        if (UNITY_UI_ALPHACLIP)
            clip(col.a - 0.001);

        return col;
    }

    ENDCG

    SubShader
    {
        Tags
        {
            "Queue"             = "Transparent"
            "RenderType"        = "Transparent"
            "IgnoreProjector"   = "True"
            "PreviewType"       = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref       [_Stencil]
            Comp      [_StencilComp]
            Pass      [_StencilOp]
            ReadMask  [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            // Runtime branches, not variants: each Procedural Shape variant is a multi-second
            // first-use compile on WebGL, while these paths are only a few instructions
            #pragma dynamic_branch_local EVO_UI_BACKDROP
            #pragma dynamic_branch_local UNITY_UI_CLIP_RECT
            #pragma multi_compile_local_fragment _ EVO_UI_SOFT_MASK_BASIC EVO_UI_SOFT_MASK
            // Light program for pages whose shapes all take the fast path (see ProceduralShape.RequiresFullShader)
            #pragma multi_compile_local _ EVO_UI_LITE
            #pragma dynamic_branch_local UNITY_UI_ALPHACLIP
            ENDCG
        }
    }
}