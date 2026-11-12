// Shared coverage for the canvas pass and the InkNoise coverage bake.
#ifndef EVO_UI_INK_PANEL_CGINC
#define EVO_UI_INK_PANEL_CGINC

float _EvoInkTime;

float3 EvoInkHashCell(float2 p)
{
    float3 q = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    q += dot(q, q.yxz + 33.33);
    return frac((q.xxy + q.yzz) * q.zyx);
}

// Bounded disks cannot cross a cell boundary: no nine-neighbour Voronoi search.
float EvoInkDropDistance(float2 p, float density, float t, float4 motion)
{
    float2 cell = floor(p);
    float3 h = EvoInkHashCell(cell + motion.z * 3.19);
    float2 center = 0.28 + h.xy * 0.44;
    center += sin(t + h.xy * 6.2831853) * (0.035 * motion.y);
    float radius = (0.045 + h.z * h.z * 0.17) * smoothstep(0.0, 0.18, density);
    radius *= step(h.z, density);
    // Subtracting a negative radius suppresses absent cells without branches.
    return length(frac(p) - center) - radius + (1.0 - step(h.z, density)) * 2.0;
}

float EvoInkCoverage(float2 p, sampler2D noiseTexture, float4 ink, float4 motion, float4 geometry, float2 origin)
{
    // ink: reveal, roughness, droplets, roundness.
    if (ink.x <= 0.0)
        return 0.0;

    float2 size = max(geometry.xy, 0.001);
    float unit = min(size.x, size.y);
    float2 aspectPosition = p * size / unit;
    float2 physical = p * size * motion.w;
    float t = _EvoInkTime * motion.x;
    float2 offset = float2(motion.z * 0.173, motion.z * 0.317);

    // Four filtered samples, with independent channels for domain warp and edge scales.
    float2 drift = float2(t * 0.013, -t * 0.009) * motion.y;
    float4 broad = tex2D(noiseTexture, aspectPosition * 0.43 + offset + drift);
    float2 warp = (broad.rg - 0.5) * (0.2 + ink.y * 0.34);
    float4 curl = tex2D(noiseTexture, physical * 0.71 + warp + offset - drift * 1.7);
    float4 grain = tex2D(noiseTexture, physical * 2.9 + (curl.rg - 0.5) * 0.22 + offset + drift * 0.6);
    float4 fine = tex2D(noiseTexture, physical * 10.7 + (grain.rg - 0.5) * 0.075 + offset - drift * 0.8);

    // Distance is measured in uniform canvas units, so stretching the layout adds panel length
    // without stretching droplets, edge lobes, corner radii, or the radial contact front.
    // Square panels grow toward their rect and rein in large holes, making Roundness a strong
    // silhouette control. The soft guard band still leaves room for detached edge droplets.
    float cornerFill = 1.0 - saturate(ink.w);
    float squareFill = cornerFill * cornerFill * (3.0 - 2.0 * cornerFill);
    float extent = lerp(0.705, 0.89, squareFill);
    float2 halfSize = size / unit * extent;
    float radius = extent * saturate(ink.w);
    warp *= lerp(1.0, 0.3, squareFill);
    float2 q = abs(aspectPosition + warp) - halfSize + radius;
    float body = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
    float edge = (curl.b - 0.5) * 0.19 + (grain.b - 0.5) * 0.083 + (fine.b - 0.5) * 0.038;
    body += edge * (0.3 + ink.y * 1.7) * lerp(1.0, 0.5, squareFill);

    // A contact point expands in all directions, then merges into the resting silhouette.
    // At a fixed time this field is monotonic in Reveal, so scrubbing cannot pop holes open.
    float growth = 1.0 - (1.0 - ink.x) * (1.0 - ink.x);
    float contact = length((p - origin) * size / unit + warp * 0.6) + edge * 1.55 + 0.08;
    // Interpolate arrival distance toward the final boundary, so the reveal uses its whole
    // range and finishes continuously at 1 even with an off-center contact point.
    float front = lerp(contact, body - 0.012, growth);
    float inkDistance = max(body, front);
    float aa = max(fwidth(inkDistance), 0.0001);
    float coverage = saturate(0.5 - inkDistance / aa);

    // A band of detached satellites follows the advancing front as well as the outer edge.
    float envelope = inkDistance;
    float band = smoothstep(-0.035, 0.025, envelope) * (1.0 - smoothstep(0.045, 0.17, envelope));
    float cluster = saturate((curl.a - 0.29) * 2.7) * ink.z;
    float2 dropUV = physical * (23.0 / max(geometry.z, 0.25)) + warp * 3.0;
    float2 microUV = physical * (59.0 / max(geometry.z, 0.25)) + curl.rg * 0.25;
    float drop = EvoInkDropDistance(dropUV, cluster, t * 0.31, motion);
    float micro = EvoInkDropDistance(microUV, cluster * 0.82, t * 0.24, motion);
    // Differentiate continuous coordinates, never the discontinuous cell hash: the latter
    // creates faint grid lines at cell boundaries, especially when the UI is magnified.
    float dropAA = max(length(fwidth(dropUV)), 0.0001);
    float microAA = max(length(fwidth(microUV)), 0.0001);
    // Fade subpixel specks by area rather than allowing temporal sparkle.
    float satellites = max(saturate(0.5 - drop / dropAA), saturate(0.5 - micro / microAA) * saturate(0.36 / microAA));
    coverage = max(coverage, satellites * band);

    // Keep an empty guard band: no hard rectangular clipping of animated splatters.
    float bounds = max(abs(p.x), abs(p.y));
    coverage *= 1.0 - smoothstep(0.94, 0.99, bounds);
    coverage *= smoothstep(0.0, 0.018, ink.x);

    return coverage;
}

#endif