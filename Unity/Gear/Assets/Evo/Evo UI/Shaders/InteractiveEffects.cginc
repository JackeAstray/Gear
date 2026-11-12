#ifndef EVO_UI_EFFECTS_INCLUDED
#define EVO_UI_EFFECTS_INCLUDED

// Global data is shared by every graphic and by stencil/Soft Mask material copies.
// Row zero is dormant: no atlas samples or animation work on that path.
sampler2D _EvoEffectsData;
float4 _EvoEffectsDataSize;
float _EvoEffectsTime;

// Keep in sync with Ripple.MaximumRipples
#define EVO_RIPPLE_MAXIMUM 8
#define EVO_SPOTLIGHT_COLUMN (1 + EVO_RIPPLE_MAXIMUM * 3)
#define EVO_BORDER_COLUMN (EVO_SPOTLIGHT_COLUMN + 2)
#if defined(UNITY_COMPILER_HLSL)
#define EVO_EFFECTS_BRANCH [branch]
#else
#define EVO_EFFECTS_BRANCH
#endif

inline float4 EvoEffectsRead(float row, float column)
{
    return tex2Dlod(_EvoEffectsData, float4((column + 0.5) * _EvoEffectsDataSize.x,
    (row + 0.5) * _EvoEffectsDataSize.y, 0, 0));
}

// Resolve activity per vertex. Dormant graphics skip all fragment atlas/lighting work,
// even while another graphic is animating. A fully idle canvas skips the vertex sample too.
inline float EvoEffectsActiveRow(float row)
{
    float result = 0.0;
    EVO_EFFECTS_BRANCH
    if (row >= 0.5 && _EvoEffectsTime >= 0.0)
    {
        float2 active = EvoEffectsRead(row, 0).xy;
        result = active.x + active.y > 0.0 ? row : 0.0;
    }
    return result;
}

// Stacks a premultiplied layer onto its group. A negative radius marks layers that ignore the graphic's alpha
inline void EvoEffectsLayer(inout half4 follow, inout half4 ignore, half4 color, float coverage, float radius)
{
    color.a = saturate(color.a * coverage);
    color.rgb *= color.a;
    if (radius < 0.0) { ignore = color + ignore * (1.0 - color.a); }
    else { follow = color + follow * (1.0 - color.a); }
}

inline float EvoEffectsRadial(float4 light, float2 position, float2 size, float aa)
{
    float distance = length((position - light.xy) * size);
    float radius = abs(light.z);
    return 1.0 - smoothstep(max(0.0, radius * (1.0 - light.w)), radius + aa, distance);
}

#if defined(EVO_UI_EFFECTS_PASS)
float EvoSpriteBorder(float row, float2 uv, float2 position, float2 size, float2 uvPerUnitX, float2 uvPerUnitY, float width, float alphaEdge);
#endif

// Stacks premultiplied ripples onto their groups. Position is normalized about the drawing rect's center
inline void EvoRippleEvaluate(float row, float2 position, float2 rectSize, float4 header, float2 pixelDx, float2 pixelDy,
    inout half4 follow, inout half4 ignore)
{
    // Match Unity's HLSL hints without requiring an engine header for standalone parsing
#if defined(UNITY_COMPILER_HLSL)
    [branch]
#endif
    if (row >= 0.5 && _EvoEffectsTime >= 0.0)
    {
        int count = (int)clamp(header.x, 0.0, (float)EVO_RIPPLE_MAXIMUM);
#if defined(UNITY_COMPILER_HLSL)
        [loop]
#endif
        for (int index = 0; index < count; index++)
        {
            float column = 1.0 + index * 3.0;
            float4 geometry = EvoEffectsRead(row, column);
            float4 timing = EvoEffectsRead(row, column + 2.0);

            // timing = start time, scale duration, fade-out seconds, unused
            float elapsed = max(0.0, _EvoEffectsTime - timing.x);
            float scaleT = elapsed / max(timing.y, 0.00001);
            float approach = min(scaleT, 1.0);

            // Reach Coverage at scaleT=1 with nonzero velocity, then continue at that
            // same velocity. The handoff to fading neither stops nor jumps in speed.
            float growth = approach * (1.5 - 0.5 * approach) + 0.5 * max(scaleT - 1.0, 0.0);

            // Coverage radius is calculated once on the CPU when the row changes
            float radius = max(abs(geometry.z) * growth, 0.0001);
            float2 delta = (position - geometry.xy) * rectSize;
            float distanceToCenter = length(delta);
            float2 normal = delta / max(distanceToCenter, 0.0001);
            float aa = max(abs(dot(normal, pixelDx)) + abs(dot(normal, pixelDy)), 0.001);
            float feather = max(radius * geometry.w, aa);
            float radial = 1.0 - smoothstep(max(0.0, radius - feather), radius + aa, distanceToCenter);
            float fadeOut = timing.z > 0.0
            ? 1.0 - smoothstep(0.0, 1.0, (elapsed - timing.y) / max(timing.z, 0.00001))
            : 1.0 - step(timing.y, elapsed);
            EvoEffectsLayer(follow, ignore, EvoEffectsRead(row, column + 1.0), radial * fadeOut, geometry.z);
        }
    }
}

// Composite Spotlight, Ripple and Border Glow over a premultiplied graphic in one pass. Layers that follow the
// graphic's alpha recolor it in place. Layers that ignore it go on top, clipped by coverage (sprite or shape
// silhouette) and Canvas Group alpha. Under is what the graphic covers, such as Procedural Shape's outer shadow.
inline half4 EvoEffectsEvaluate(float row, float2 position, float2 rectSize, float shapeDistance, float2 uv,
    half4 color, half4 under, half coverage)
{
    float2 positionDx = ddx(position);
    float2 positionDy = ddy(position);
    float edgeAA = max(fwidth(shapeDistance), 0.001);
#if defined(EVO_UI_EFFECTS_PASS)
    float2 uvDx = ddx(uv);
    float2 uvDy = ddy(uv);
#endif
    half4 result = color + under;
    EVO_EFFECTS_BRANCH
    if (row >= 0.5 && _EvoEffectsTime >= 0.0)
    {
        float4 header = EvoEffectsRead(row, 0);
        if (rectSize.x <= 0.0) rectSize = header.zw;
        float2 pixelDx = positionDx * rectSize;
        float2 pixelDy = positionDy * rectSize;
        float aa = max(length(pixelDx) + length(pixelDy), 0.001);
        half4 follow = 0;
        half4 ignore = 0;
        EVO_EFFECTS_BRANCH
        if (fmod(header.y, 2.0) > 0.5)
        {
            float4 light = EvoEffectsRead(row, EVO_SPOTLIGHT_COLUMN);
            EvoEffectsLayer(follow, ignore, EvoEffectsRead(row, EVO_SPOTLIGHT_COLUMN + 1),
                EvoEffectsRadial(light, position, rectSize, aa), light.z);
        }

        EvoRippleEvaluate(row, position, rectSize, header, pixelDx, pixelDy, follow, ignore);

        EVO_EFFECTS_BRANCH
        if (header.y >= 2.0)
        {
            float4 light = EvoEffectsRead(row, EVO_BORDER_COLUMN);
            float4 border = EvoEffectsRead(row, EVO_BORDER_COLUMN + 2);
            float radial = max(border.z, EvoEffectsRadial(light, position, rectSize, aa));
            EVO_EFFECTS_BRANCH
            if (radial > 0.0)
            {
                float edge = 0.0;
#if defined(EVO_UI_EFFECTS_PASS)
                // Invert the local-position Jacobian. This follows sliced/tiled/rotated UVs
                // without assuming that one sprite texel equals one UI unit.
                float determinant = pixelDx.x * pixelDy.y - pixelDx.y * pixelDy.x;
                float inverse = abs(determinant) > 0.0000001 ? 1.0 / determinant : 0.0;
                float2 uvPerUnitX = (uvDx * pixelDy.y - uvDy * pixelDx.y) * inverse;
                float2 uvPerUnitY = (uvDy * pixelDx.x - uvDx * pixelDy.x) * inverse;
                edge = EvoSpriteBorder(row, uv, position, rectSize, uvPerUnitX, uvPerUnitY, border.x, border.w);
#else
                edge = 1.0 - smoothstep(border.x, border.x + edgeAA, max(0.0, -shapeDistance));
#endif
                EvoEffectsLayer(follow, ignore, EvoEffectsRead(row, EVO_BORDER_COLUMN + 1), edge * radial, light.z);
            }
        }

        color.rgb = color.rgb * (1.0 - follow.a) + follow.rgb * color.a;
        // The ripple count's fraction carries Canvas Group alpha
        result = ignore * (coverage * frac(header.x) * 2.0) + color * (1.0 - ignore.a) + under * (1.0 - ignore.a * coverage);
    }
    return result;
}

// Image and RawImage pass shared by InteractiveEffects, SoftMask and BackdropBlur.
// ProceduralShape includes only the evaluator and supplies its own SDF pass.
#if defined(EVO_UI_EFFECTS_PASS)
#include "UnityCG.cginc"
#include "UnityUI.cginc"
#include "SoftMask.cginc"
#if defined(EVO_UI_BACKDROP_PASS)
#include "BackdropBlur.cginc"
#endif

struct appdata_t
{
    float4 vertex   : POSITION;
    float4 color    : COLOR;
    float2 texcoord : TEXCOORD0;
    float4 effectData : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct v2f
{
    float4 vertex    : SV_POSITION;
    float4 color     : COLOR;
    float2 texcoord  : TEXCOORD0;
    float4 canvasPos : TEXCOORD1;
    float3 effectData : TEXCOORD2;
    float4 mask      : TEXCOORD3;
#if defined(EVO_UI_BACKDROP_PASS)
    float4 screen : TEXCOORD4;
#endif
    UNITY_VERTEX_OUTPUT_STEREO
};

sampler2D _MainTex;
float4 _MainTex_TexelSize;
sampler2D _AlphaTex;
float _EnableExternalAlpha;
float4 _Color;
float4 _TextureSampleAdd;
float4 _ClipRect;
float _UIMaskSoftnessX;
float _UIMaskSoftnessY;

// Engine Polynomial Sync
float _UIVertexColorAlwaysGammaSpace;

inline float3 UI_GammaToLinear(float3 color)
{
    float3 linearPath = color * 0.084971 - 0.000163;
    float3 gammaPath = color * (color * (color * 0.265885 + 0.736584) - 0.009802) + 0.003197;
    float3 comparison = step(0.072549, color);
    return lerp(linearPath, gammaPath, comparison);
}

inline float4 UI_ColorSpaceSync(float4 color)
{
#if !defined(UNITY_COLORSPACE_GAMMA)
    if (_UIVertexColorAlwaysGammaSpace > 0.5)
    color.rgb = UI_GammaToLinear(color.rgb);
#endif
    return color;
}

inline half4 SampleSpriteTexture(float2 uv)
{
    half4 color = tex2D(_MainTex, uv);

#if ETC1_EXTERNAL_ALPHA
    half4 alpha = tex2D(_AlphaTex, uv);
    color.a = lerp(color.a, alpha.r, _EnableExternalAlpha);
#endif

    return color;
}

inline float EvoSpriteAlpha(float2 uv, float4 bounds)
{
    // Atlas neighbors must never contribute to this sprite's border.
    float2 inset = min(abs(_MainTex_TexelSize.xy) * 0.5, max(0.0, bounds.zw - bounds.xy) * 0.5);
    float2 clamped = clamp(uv, bounds.xy + inset, bounds.zw - inset);
    // Explicit LOD is safe inside the varying effect branch and the erosion loop.
    float alpha = tex2Dlod(_MainTex, float4(clamped, 0, 0)).a;
#if ETC1_EXTERNAL_ALPHA
    alpha = lerp(alpha, tex2Dlod(_AlphaTex, float4(clamped, 0, 0)).r, _EnableExternalAlpha);
#endif
    return alpha;
}

float EvoSpriteBorder(float row, float2 uv, float2 position, float2 size, float2 uvPerUnitX, float2 uvPerUnitY, float width, float alphaEdge)
{
    // The rect edge handles fully opaque sprites/RawImages. Texture erosion handles
    // transparent silhouettes and holes. One ring samples the border width.
    float rectDistance = min((0.5 - abs(position.x)) * size.x, (0.5 - abs(position.y)) * size.y);
    float edge = 1.0 - smoothstep(width, width + 0.001, rectDistance);
    EVO_EFFECTS_BRANCH
    if (alphaEdge >= 0.5)
    {
        float4 bounds = EvoEffectsRead(row, EVO_BORDER_COLUMN + 3);
        float center = max(EvoSpriteAlpha(uv, bounds), 0.0001);
        float2 dx = uvPerUnitX * width;
        float2 dy = uvPerUnitY * width;
        float minimum = min(EvoSpriteAlpha(uv + dx, bounds), EvoSpriteAlpha(uv - dx, bounds));
        minimum = min(minimum, min(EvoSpriteAlpha(uv + dy, bounds), EvoSpriteAlpha(uv - dy, bounds)));
        minimum = min(minimum, min(EvoSpriteAlpha(uv + (dx + dy) * 0.707107, bounds), EvoSpriteAlpha(uv - (dx + dy) * 0.707107, bounds)));
        minimum = min(minimum, min(EvoSpriteAlpha(uv + (dx - dy) * 0.707107, bounds), EvoSpriteAlpha(uv - (dx - dy) * 0.707107, bounds)));
        edge = max(edge, saturate(1.0 - minimum / center));
    }
    return edge;
}

v2f vert(appdata_t vertex)
{
    v2f output = (v2f)0;
    UNITY_SETUP_INSTANCE_ID(vertex);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    output.canvasPos = vertex.vertex;
    output.vertex = UnityObjectToClipPos(vertex.vertex);
#if defined(EVO_UI_BACKDROP_PASS)
    // Independent of sprite UVs (including slicing, tiling and atlas rotation)
    output.screen = ComputeScreenPos(output.vertex);
#endif
    output.texcoord = vertex.texcoord;
    output.color = UI_ColorSpaceSync(vertex.color) * _Color;
    output.effectData = float3(vertex.effectData.xy, EvoEffectsActiveRow(vertex.effectData.z));
    float2 pixelSize = output.vertex.w;
    pixelSize /= abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
    float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
    output.mask = float4(vertex.vertex.xy * 2.0 - clampedRect.xy - clampedRect.zw,
    0.25 / (0.25 * float2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));
    return output;
}

float4 frag(v2f input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    half4 color = SampleSpriteTexture(input.texcoord) + _TextureSampleAdd;
#if defined(EVO_UI_BACKDROP_PASS)
    color = EvoBackdropEvaluate(color, input.screen.xy / input.screen.w, _ScreenParams.xy);
#endif
    // Sprite silhouette without the graphic's alpha
    half coverage = color.a;
    color *= input.color;

    // Masks apply before the effects, which then clip to the same visible area
    half mask = SoftMask_GetAlpha(input.canvasPos);
#ifdef UNITY_UI_CLIP_RECT
    half2 clipMask = saturate((_ClipRect.zw - _ClipRect.xy - abs(input.mask.xy)) * input.mask.zw);
    mask *= clipMask.x * clipMask.y;
#endif
    color.a *= mask;
    color.rgb *= color.a;

    color = EvoEffectsEvaluate(input.effectData.z, input.effectData.xy, float2(0, 0), 0.0, input.texcoord,
        color, 0, coverage * mask);

#ifdef UNITY_UI_ALPHACLIP
    clip(color.a - 0.001);
#endif

    return color;
}
#endif

#endif