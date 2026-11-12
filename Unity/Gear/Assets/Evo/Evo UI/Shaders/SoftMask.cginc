#ifndef EVO_UI_SOFTMASK_CGINC
#define EVO_UI_SOFTMASK_CGINC

#include "ProceduralShape.cginc"

#define EVO_UI_SOFTMASK_MAX 4

sampler2D _SoftMaskTex0;
sampler2D _SoftMaskTex1;
sampler2D _SoftMaskTex2;
sampler2D _SoftMaskTex3;

float _SoftMask_Count;
float4 _SoftMask_CanvasToLocalX[EVO_UI_SOFTMASK_MAX];
float4 _SoftMask_CanvasToLocalY[EVO_UI_SOFTMASK_MAX];
float4 _SoftMask_Rect[EVO_UI_SOFTMASK_MAX];
float4 _SoftMask_Data[EVO_UI_SOFTMASK_MAX];
float4 _SoftMask_Invert;
float4 _SoftMask_PRRect[EVO_UI_SOFTMASK_MAX];
float4 _SoftMask_PRRadii[EVO_UI_SOFTMASK_MAX];
float4 _SoftMask_PRFillData[EVO_UI_SOFTMASK_MAX];
float4 _SoftMask_BorderData[EVO_UI_SOFTMASK_MAX];
float4 _SoftMask_UVOuter[EVO_UI_SOFTMASK_MAX];
float4 _SoftMask_UVInner[EVO_UI_SOFTMASK_MAX];

#define EVO_UI_PI     3.14159265
#define EVO_UI_TWO_PI 6.28318530

float Evo_UI_GetClipDistance(float2 p, float2 halfSize, float fillAmount, float fillPacked)
{
    float packed = round(fillPacked);
    float method = fmod(packed, 8.0);
    float origin = fmod(floor(packed * 0.125), 8.0);
    float cw = fmod(floor(packed * 0.015625), 2.0);

    if (method < 1.5)
    {
        float fromMin = (origin < 0.5) ? cw : 1.0 - cw;
        float boundary = (fromMin > 0.5) ? lerp(-halfSize.x, halfSize.x, fillAmount) : lerp(halfSize.x, -halfSize.x, fillAmount);
        return (fromMin > 0.5) ? p.x - boundary : boundary - p.x;
    }

    if (method < 2.5)
    {
        float fromMin = (origin < 0.5) ? cw : 1.0 - cw;
        float boundary = (fromMin > 0.5) ? lerp(-halfSize.y, halfSize.y, fillAmount) : lerp(halfSize.y, -halfSize.y, fillAmount);
        return (fromMin > 0.5) ? p.y - boundary : boundary - p.y;
    }

    float startAngle = (origin < 0.5) ? -EVO_UI_PI * 0.5 :
                       (origin < 1.5) ? 0.0 :
                       (origin < 2.5) ? EVO_UI_PI * 0.5 : EVO_UI_PI;

    float angle = atan2(p.y, p.x) - startAngle;
    angle = (cw > 0.5) ? -angle : angle;
    float radial = frac(angle / EVO_UI_TWO_PI + 1.0);
    float signedFraction = (radial <= fillAmount) ? -min(radial, fillAmount - radial) : min(radial - fillAmount, 1.0 - radial);
    float angleDistance = min(abs(signedFraction) * EVO_UI_TWO_PI, EVO_UI_PI * 0.5);
    return ((signedFraction < 0.0) ? -1.0 : 1.0) * sin(angleDistance) * length(p);
}

// Recenter the visible rectangle without changing sprite/gradient coordinates.
// The caller reuses this geometry for the fill, outline, and both shadows.
inline void Evo_UI_ApplyRoundedLinearClip(inout float2 p, inout float2 halfSize, inout float4 radii,
    float amount, float packed)
{
    packed = round(packed);
    float method = fmod(packed, 8.0);
    float origin = fmod(floor(packed * 0.125), 8.0);
    float cw = fmod(floor(packed * 0.015625), 2.0);
    float fromMin = (origin < 0.5) ? cw : 1.0 - cw;
    float2 scale = (method < 1.5) ? float2(saturate(amount), 1.0) : float2(1.0, saturate(amount));
    float2 removedHalfSize = halfSize * (1.0 - scale);
    p -= ((fromMin > 0.5) ? -1.0 : 1.0) * removedHalfSize;
    halfSize *= scale;
    radii = min(radii, min(halfSize.x, halfSize.y));
}

// Plain shapes use plainRadii; rounded clipping uses radii. Both paths share one shape dispatch,
// which keeps the shape library compiled once per caller instead of twice.
float Evo_UI_GetShapeDistance(float2 p, float2 halfSize, float4 radii, float4 plainRadii, float isSquircle,
    float shapeType, bool roundClip, float width, float amount, float packed)
{
    if (roundClip && amount <= 0.0)
        return 100000.0;

    bool linearClip = roundClip && fmod(round(packed), 8.0) < 2.5;

    // Preserve the ellipse's precomputed, trigonometry-free arc path.
    if (roundClip && !linearClip && abs(shapeType - EVO_UI_SHAPE_ELLIPSE) < 0.5)
        return Evo_SdRoundArc(p, halfSize, width, amount, radii);

    float4 shapeRadii = plainRadii;
    if (linearClip)
    {
        // A linear rounded clip is always a rounded box
        Evo_UI_ApplyRoundedLinearClip(p, halfSize, radii, amount, packed);
        shapeRadii = radii;
        shapeType = EVO_UI_SHAPE_RECTANGLE;
    }
    else if (roundClip)
        shapeRadii = min(radii, min(halfSize.x, halfSize.y));

    float distance = Evo_SdProceduralShape(p, halfSize, shapeRadii, isSquircle, shapeType);
    if (!roundClip || linearClip)
        return distance;

    float halfWidth = width * 0.5;
    float centerDistance = distance + halfWidth;
    if (amount >= 1.0)
        return abs(centerDistance) - halfWidth;

    // Round the clipped stroke in distance space without additional shape samples.
    float clipDistance = max(Evo_UI_GetClipDistance(p, halfSize, amount, packed), 0.0);
    return length(float2(centerDistance, clipDistance)) - halfWidth;
}

float Evo_UI_ComputeFillMask(float2 p, float2 halfSize, float fillAmount, float fillPacked)
{
    float method = fmod(round(fillPacked), 8.0);
    if (method < 0.5 || fillAmount >= 1.0)
        return 1.0;
    if (fillAmount <= 0.0)
        return 0.0;

    float clipDist = Evo_UI_GetClipDistance(p, halfSize, fillAmount, fillPacked);
    float clipAA = max(fwidth(clipDist) * 0.5, 0.00001);
    return 1.0 - smoothstep(-clipAA, clipAA, clipDist);
}

float Evo_UI_Map1D(float x, float left, float right, float extent, float uvMin, float uvLeft, float uvRight, float uvMax)
{
    if (x < left)
    {
        return lerp(uvMin, uvLeft, (left > 0.001) ? (x / left) : 0.0);
    }
    else if (x > right)
    {
        return lerp(uvRight, uvMax, (extent > right + 0.001) ? ((x - right) / (extent - right)) : 0.0);
    }

    return lerp(uvLeft, uvRight, (right > left + 0.001) ? ((x - left) / (right - left)) : 0.0);
}

float Evo_UI_MapTiled1D(float x, float left, float right, float extent, float tileExtent, float uvMin, float uvLeft, float uvRight, float uvMax)
{
    if (x < left)
    {
        return lerp(uvMin, uvLeft, (left > 0.001) ? (x / left) : 0.0);
    }
    else if (x >= right)
    {
        return lerp(uvRight, uvMax, (extent > right + 0.001) ? ((x - right) / (extent - right)) : 0.0);
    }

    float repeat = frac((x - left) / max(tileExtent, 0.001));
    return lerp(uvLeft, uvRight, repeat);
}

float Evo_UI_ComputeImageFillMask(float2 p, float2 size, float fillAmount, float fillPacked)
{
    if (fillAmount >= 1.0)
        return 1.0;
    if (fillAmount <= 0.0)
        return 0.0;

    float packed = round(fillPacked);
    float method = fmod(packed, 8.0);
    float origin = fmod(floor(packed * 0.125), 8.0);
    float cw = fmod(floor(packed * 0.015625), 2.0);
    float clipDist;

    if (method < 0.5)
    {
        float boundary = (origin < 0.5) ? size.x * fillAmount : size.x * (1.0 - fillAmount);
        clipDist = (origin < 0.5) ? p.x - boundary : boundary - p.x;
        float clipAA = max(fwidth(clipDist) * 0.5, 0.00001);
        return 1.0 - smoothstep(-clipAA, clipAA, clipDist);
    }

    if (method < 1.5)
    {
        float boundary = (origin < 0.5) ? size.y * fillAmount : size.y * (1.0 - fillAmount);
        clipDist = (origin < 0.5) ? p.y - boundary : boundary - p.y;
        float clipAA = max(fwidth(clipDist) * 0.5, 0.00001);
        return 1.0 - smoothstep(-clipAA, clipAA, clipDist);
    }

    float2 normalized = p / size;
    float2 pivot;
    float startAngle;
    float progressScale;

    if (method < 2.5)
    {
        pivot = (origin < 0.5) ? float2(0.0, 0.0) :
                (origin < 1.5) ? float2(0.0, 1.0) :
                (origin < 2.5) ? float2(1.0, 1.0) : float2(1.0, 0.0);
        startAngle = ((cw > 0.5) ? EVO_UI_PI * 0.5 : 0.0) - origin * EVO_UI_PI * 0.5;
        progressScale = 4.0;
    }
    else if (method < 3.5)
    {
        pivot = (origin < 0.5) ? float2(0.5, 0.0) :
                (origin < 1.5) ? float2(0.0, 0.5) :
                (origin < 2.5) ? float2(0.5, 1.0) : float2(1.0, 0.5);
        startAngle = ((cw > 0.5) ? EVO_UI_PI : 0.0) - origin * EVO_UI_PI * 0.5;
        progressScale = 2.0;
    }
    else
    {
        pivot = float2(0.5, 0.5);
        startAngle = -EVO_UI_PI * 0.5 + origin * EVO_UI_PI * 0.5;
        progressScale = 1.0;
    }

    float2 radial = normalized - pivot;
    if (method > 2.5 && method < 3.5)
    {
        if (origin < 0.5 || (origin > 1.5 && origin < 2.5))
            radial.x *= 2.0;
        else
            radial.y *= 2.0;
    }

    if (dot(radial, radial) < 0.0000001)
        return 1.0;

    float angle = atan2(radial.y, radial.x);
    float progress = (cw > 0.5) ? startAngle - angle : angle - startAngle;
    progress = frac(progress / EVO_UI_TWO_PI + 1.0) * progressScale;

    float progressAA = max(fwidth(progress) * 0.5, 0.00001);
    return 1.0 - smoothstep(fillAmount - progressAA, fillAmount + progressAA, progress);
}

// Sampler arrays can't be indexed by a loop counter on GLES/WebGL, so each slot gets its own branch
inline float Evo_UI_SampleSoftMask(int index, float2 uv)
{
    float alpha;
    if (index == 0) alpha = tex2D(_SoftMaskTex0, uv).a;
    else if (index == 1) alpha = tex2D(_SoftMaskTex1, uv).a;
    else if (index == 2) alpha = tex2D(_SoftMaskTex2, uv).a;
    else alpha = tex2D(_SoftMaskTex3, uv).a;
    return alpha;
}

inline float Evo_UI_EvaluateSoftMask(float4 canvasPos, int index)
{
    float4 rect = _SoftMask_Rect[index];
    if (rect.z <= 0.0 || rect.w <= 0.0)
        return 0.0;

    float4 data = _SoftMask_Data[index];
    float4 borderData = _SoftMask_BorderData[index];
    float4 uvOuter = _SoftMask_UVOuter[index];
    float4 uvInner = _SoftMask_UVInner[index];
    float2 localPos = float2(dot(_SoftMask_CanvasToLocalX[index], canvasPos), dot(_SoftMask_CanvasToLocalY[index], canvasPos));
    float maskAlpha = 1.0;
    float inBounds = 1.0;
    float2 maskUV = 0.0;

    if (data.x > 3.5)
    {
        float2 p = localPos - rect.xy;
        float2 size = rect.zw;

        maskUV.x = Evo_UI_MapTiled1D(p.x, borderData.x, borderData.z, size.x, data.z, uvOuter.x, uvInner.x, uvInner.z, uvOuter.z);
        maskUV.y = Evo_UI_MapTiled1D(p.y, borderData.y, borderData.w, size.y, data.w, uvOuter.y, uvInner.y, uvInner.w, uvOuter.w);

        float2 center = step(borderData.xy, p) * step(p, borderData.zw);
        maskAlpha = lerp(1.0 - center.x * center.y, 1.0, data.y);

        float2 bounds = step(0.0, p) * step(p, size);
        inBounds = bounds.x * bounds.y;
    }
#if defined(EVO_UI_SOFT_MASK)
    else if (data.x > 2.5)
    {
        float2 p = localPos - rect.xy;
        float2 size = rect.zw;
        maskUV = lerp(uvOuter.xy, uvOuter.zw, p / size);
        float4 proceduralFillData = _SoftMask_PRFillData[index];
        maskAlpha = Evo_UI_ComputeImageFillMask(p, size, proceduralFillData.x, proceduralFillData.y);

        float2 bounds = step(0.0, p) * step(p, size);
        inBounds = bounds.x * bounds.y;
    }
#endif
    else if (data.x > 1.5)
    {
        float4 proceduralRect = _SoftMask_PRRect[index];
        float4 proceduralRadii = _SoftMask_PRRadii[index];
        float4 proceduralFillData = _SoftMask_PRFillData[index];
        float2 sdfCoord = localPos - proceduralRect.xy;
        float2 halfSize = proceduralRect.zw;
        float4 radii = min(proceduralRadii, min(halfSize.x, halfSize.y));
        float shapeType = round(proceduralFillData.z);
        float isSquircle = round(proceduralFillData.w);

#if defined(EVO_UI_SOFT_MASK)
        bool roundClip = fmod(floor(proceduralFillData.y / 256.0), 2.0) > 0.5;
        float maskDist = Evo_UI_GetShapeDistance(sdfCoord, halfSize, proceduralRadii, radii, isSquircle, shapeType,
            roundClip, data.z, proceduralFillData.x, proceduralFillData.y);
        float clipMask;
        if (roundClip)
            clipMask = (proceduralFillData.x > 0.0) ? 1.0 : 0.0;
        else
            clipMask = Evo_UI_ComputeFillMask(sdfCoord, halfSize, proceduralFillData.x, proceduralFillData.y);
#else
        // Basic shape masks are unclipped rectangles and ellipses: the same distances the full path reaches
        float maskDist;
        if (shapeType == EVO_UI_SHAPE_ELLIPSE)
            maskDist = Evo_SdEllipse(sdfCoord, halfSize);
        else
            maskDist = Evo_SdRoundedBox(sdfCoord, halfSize, radii, isSquircle);
        float clipMask = 1.0;
#endif
        float shapeAA = max(fwidth(maskDist) * 0.5 + data.y, 0.00001);
        float shapeMask = 1.0 - smoothstep(-shapeAA, shapeAA, maskDist);
        maskAlpha = shapeMask * clipMask;
    }
    else if (data.x > 0.5)
    {
        float2 p = localPos - rect.xy;
        float2 size = rect.zw;

        maskUV.x = Evo_UI_Map1D(p.x, borderData.x, borderData.z, size.x, uvOuter.x, uvInner.x, uvInner.z, uvOuter.z);
        maskUV.y = Evo_UI_Map1D(p.y, borderData.y, borderData.w, size.y, uvOuter.y, uvInner.y, uvInner.w, uvOuter.w);

        float2 center = step(borderData.xy, p) * step(p, borderData.zw);
        maskAlpha = lerp(1.0 - center.x * center.y, 1.0, data.y);

        float2 bounds = step(0.0, p) * step(p, size);
        inBounds = bounds.x * bounds.y;
    }
    else
    {
        float2 p = localPos - rect.xy;
        float2 size = rect.zw;
        maskUV = lerp(uvOuter.xy, uvOuter.zw, p / size);

        float2 bounds = step(0.0, p) * step(p, size);
        inBounds = bounds.x * bounds.y;
    }

    // One fetch serves every sprite mode; Procedural Shape masks have no texture
    if (data.x < 1.5 || data.x > 2.5)
        maskAlpha *= Evo_UI_SampleSoftMask(index, maskUV);

    return saturate(maskAlpha * inBounds);
}

// The evaluator is compiled only where masking can happen: shaders with built-in support declare
// "#pragma multi_compile_local_fragment _ EVO_UI_SOFT_MASK_BASIC EVO_UI_SOFT_MASK" and Soft Mask enables one
// on its material copies; replacement shaders declare the set without "_". BASIC covers a single sprite (simple,
// sliced, tiled) mask or unclipped rectangle/ellipse shape, a fraction of the full evaluator's compile time.
inline float SoftMask_GetAlpha(float4 canvasPos)
{
    float alpha = 1.0;

#if defined(EVO_UI_SOFT_MASK_BASIC)
    // One mask at a constant index: mobile GPUs keep its data in uniform registers. A loop index would load every
    // array element from memory for each pixel. Nested masks use the full evaluator (see SoftMask.cs).
    float maskAlpha = Evo_UI_EvaluateSoftMask(canvasPos, 0);
    alpha = _SoftMask_Count > 0.5 ? lerp(maskAlpha, 1.0 - maskAlpha, _SoftMask_Invert.x) : 1.0;
#elif defined(EVO_UI_SOFT_MASK)
    // A loop keeps one copy of the evaluator. Four inlined copies made every UI shader
    // slow to compile on first use, which stalls WebGL for seconds.
    [loop]
    for (int index = 0; index < EVO_UI_SOFTMASK_MAX; index++)
    {
        if (_SoftMask_Count <= index + 0.5)
            break;

        float maskAlpha = Evo_UI_EvaluateSoftMask(canvasPos, index);
        alpha *= lerp(maskAlpha, 1.0 - maskAlpha, _SoftMask_Invert[index]);
    }
#endif

    return alpha;
}

inline void SoftMask_Apply(inout half4 color, float4 canvasPos)
{
    color.a *= SoftMask_GetAlpha(canvasPos);
}

inline void SoftMask_ApplyPremultiplied(inout half4 color, float4 canvasPos)
{
    color *= SoftMask_GetAlpha(canvasPos);
}

#undef EVO_UI_PI
#undef EVO_UI_TWO_PI

#endif