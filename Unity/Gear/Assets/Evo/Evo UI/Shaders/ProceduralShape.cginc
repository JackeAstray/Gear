#ifndef EVO_UI_PROCEDURAL_SHAPE_CGINC
#define EVO_UI_PROCEDURAL_SHAPE_CGINC

// Primitive IDs match ProceduralShape.ShapeType, except Polygon:
// its public value is 2, while shader IDs 10..15 encode polygons with 3..8 sides.
// IDs 6..9 are free for new primitives without changing the vertex payload.
// NOTE TO MYSELF: Add each new SDF to the explicit dispatcher below and its CPU counterpart.
#define EVO_UI_SHAPE_RECTANGLE        0
#define EVO_UI_SHAPE_ELLIPSE          1
#define EVO_UI_SHAPE_STAR             3
#define EVO_UI_SHAPE_HEART            4
#define EVO_UI_SHAPE_CHEVRON          5
#define EVO_UI_POLYGON_ID(sides)      (10 + (sides) - 3)

inline float Evo_SdRoundArc(float2 p, float2 b, float width, float amount, float4 arc)
{
    if (amount <= 0.0)
        return 100000.0;

    float radius = min(b.x, b.y);
    float capRadius = min(width, radius) * 0.5;
    float centerRadius = radius - capRadius;
    p *= radius / max(b, 0.0001);
    float radialDistance = abs(length(p) - centerRadius);
    if (amount >= 1.0)
        return radialDistance - capRadius;

    float2 q = float2(abs(p.x * arc.y - p.y * arc.x), dot(p, arc.xy));
    float distance = (arc.w * q.x > arc.z * q.y)
        ? length(q - arc.zw * centerRadius) : radialDistance;
    return distance - capRadius;
}

inline float Evo_SdRoundedBox(float2 p, float2 b, float4 r, float isSquircle)
{
    r.xy = (p.x > 0.0) ? r.xy : r.zw;
    r.x = (p.y > 0.0) ? r.x : r.y;

    float2 q = abs(p) - b + r.x;
    float2 mq = max(q, 0.0);
    // One shared square root: the squircle's corner length is the fourth root of its fourth powers
    float2 sq = mq * mq;
    float edgeLength = sqrt(isSquircle > 0.5 ? sqrt(dot(sq, sq)) : dot(mq, mq));

    return min(max(q.x, q.y), 0.0) + edgeLength - r.x;
}

inline float Evo_SdEllipse(float2 p, float2 b)
{
    b = max(b, 0.0001);

    float k0 = length(p / b);
    float k1 = length(p / (b * b));

    if (k1 < 0.0001)
        return -min(b.x, b.y);

    return k0 * (k0 - 1.0) / k1;
}

inline float Evo_SegmentDistanceSq(float2 toPoint, float2 edge)
{
    float2 nearest = toPoint - edge * saturate(dot(toPoint, edge) / dot(edge, edge));
    return dot(nearest, nearest);
}

inline void Evo_AddPolygonEdge(float2 p, float2 a, float2 b, inout float minDistanceSq, inout float minSide)
{
    float2 edge = b - a;
    float2 toPoint = p - a;
    minDistanceSq = min(minDistanceSq, Evo_SegmentDistanceSq(toPoint, edge));
    minSide = min(minSide, edge.x * toPoint.y - edge.y * toPoint.x);
}

inline float Evo_FinishPolygon(float minDistanceSq, float minSide)
{
    return sqrt(max(minDistanceSq, 0.0)) * ((minSide >= 0.0) ? -1.0 : 1.0);
}

// Corners of the 3..8-sided polygons (back to back, from index ((sides * (sides - 1)) >> 1) - 3),
// then the chevron at 33, and the star. ProceduralShape fills them from its CPU tables.
// Not static const: WebGPU copies such arrays into every use per pixel, which made shapes ~5x slower there.
CBUFFER_START(EvoShapeVertices)
    float4 _EvoPolygonVertices[39];
    float4 _EvoStarVertices[10];
CBUFFER_END

inline float Evo_SdPolygon(float2 p, float2 b, uint sides)
{
    // One bounded loop avoids duplicating six polygon programs in each shape,
    // shadow and soft-mask distance evaluation.
    uint firstVertex = ((sides * (sides - 1u)) >> 1u) - 3u;
    // All definitions are symmetric about X = 0. Only the right boundary
    // (plus any horizontal top/bottom edges) can be nearest after reflection.
    p.x = abs(p.x);
    float minDistanceSq = 1e20;
    float minSide = 1e20;

    // Each corner is loaded once and carried to the next edge
    float2 a = _EvoPolygonVertices[firstVertex].xy * b;
    [loop]
    for (uint i = 0u; i < (sides == 4u ? 2u : (sides >> 1u) + 1u); i++)
    {
        float2 next = _EvoPolygonVertices[firstVertex + i + 1u].xy * b;
        Evo_AddPolygonEdge(p, a, next, minDistanceSq, minSide);
        a = next;
    }

    return Evo_FinishPolygon(minDistanceSq, minSide);
}

inline void Evo_AddSignedPolygonEdge(float2 p, float2 a, float2 b, inout float minDistanceSq, inout float signValue)
{
    float2 edge = b - a;
    float2 toPoint = p - a;
    minDistanceSq = min(minDistanceSq, Evo_SegmentDistanceSq(toPoint, edge));

    if ((a.y > p.y) != (b.y > p.y))
    {
        float safeEdgeY = (abs(edge.y) > 0.000001) ? edge.y : 0.000001;
        float intersectionX = edge.x * (p.y - a.y) / safeEdgeY + a.x;
        if (p.x < intersectionX)
            signValue = -signValue;
    }
}

inline float Evo_SdStar(float2 p, float2 b)
{
    float minDistanceSq = 1e20;
    float signValue = 1.0;

    // Reflect onto the right half: only these five edges can be nearest or
    // intersect the positive-X winding ray. Keep repeated shadow/mask code compact.
    p.x = abs(p.x);
    // Each corner is loaded once and carried to the next edge
    float2 a = _EvoStarVertices[5].xy * b;
    [loop]
    for (int i = 5; i < 10; i++)
    {
        float2 next = _EvoStarVertices[i == 9 ? 0 : i + 1].xy * b;
        Evo_AddSignedPolygonEdge(p, a, next, minDistanceSq, signValue);
        a = next;
    }

    return sqrt(max(minDistanceSq, 0.0)) * signValue;
}

inline float Evo_SdHeart(float2 p, float2 b)
{
    const float lobeRadius = 0.353553391;
    const float halfWidth = 0.603553391;
    const float height = 1.103553391;

    b = max(b, 0.0001);

    float2 toNormalized = float2(halfWidth / b.x, height * 0.5 / b.y);
    float2 q = float2(abs(p.x) * toNormalized.x, (p.y + b.y) * toNormalized.y);
    float distance = 0.0;

    if (q.x + q.y > 1.0)
    {
        distance = length(q - float2(0.25, 0.75)) - lobeRadius;
    }
    else
    {
        float2 toNotch = q - float2(0.0, 1.0);
        float diagonal = 0.5 * max(q.x + q.y, 0.0);
        float2 toSide = q - diagonal;
        distance = sqrt(min(dot(toNotch, toNotch), dot(toSide, toSide)));
        distance *= (q.x >= q.y) ? 1.0 : -1.0;
    }

    return distance / max(toNormalized.x, toNormalized.y);
}

inline float Evo_SdChevron(float2 p, float2 b)
{
    // Reflect onto the upper half: only two diagonals and the end can be nearest.
    p.y = abs(p.y);
    float2 edge = float2(-b.x, b.y);
    float distanceSq = min(Evo_SegmentDistanceSq(p, edge), Evo_SegmentDistanceSq(p - float2(b.x, 0.0), edge));
    float2 end = float2(p.x - clamp(p.x, -b.x, 0.0), p.y - b.y);
    distanceSq = min(distanceSq, dot(end, end));
    float side = p.x * b.y + p.y * b.x;
    return sqrt(distanceSq) * ((p.y <= b.y && side >= 0.0 && side <= b.x * b.y) ? -1.0 : 1.0);
}

inline float Evo_SdProceduralShape(float2 p, float2 b, float4 r, float isSquircle, float shapeType)
{
    if (shapeType == EVO_UI_SHAPE_RECTANGLE)
        return Evo_SdRoundedBox(p, b, r, isSquircle);

    if (shapeType == EVO_UI_SHAPE_ELLIPSE)
        return Evo_SdEllipse(p, b);

    float radius = clamp(r.x, 0.0, min(b.x, b.y));
    float2 innerSize = max(b - radius, 0.0001);

    float distance = 0.0;
    switch ((int)shapeType)
    {
        case EVO_UI_SHAPE_STAR: distance = Evo_SdStar(p, innerSize); break;
        case EVO_UI_SHAPE_HEART: distance = Evo_SdHeart(p, innerSize); break;
        case EVO_UI_SHAPE_CHEVRON: distance = Evo_SdChevron(p, innerSize); break;
        default:
            if (shapeType >= EVO_UI_POLYGON_ID(3) && shapeType <= EVO_UI_POLYGON_ID(8))
                distance = Evo_SdPolygon(p, innerSize, (uint)shapeType - 7u);
            else
                return Evo_SdRoundedBox(p, b, r, isSquircle);
            break;
    }

    return distance - radius;
}

// Dash primitives share one cumulative perimeter. Union their distances before
// antialiasing: a dash crossing an edge/arc join is never blended twice.
struct Evo_DashPath
{
    float length;
    float distance;
    float halfWidth;
    float seam;
    float4 pattern; // period, half body length, phase in pixels, rounded
};

inline float2 Evo_DashInterval(float s, float4 pattern)
{
    // The perimeter-only vertex pass uses a zero pattern; keep this expression
    // finite even when the shader compiler evaluates it across an inactive branch.
    float center = floor((s + pattern.z) / max(pattern.x, 0.0001) + 0.5) * pattern.x - pattern.z;
    return center + float2(-pattern.y, pattern.y);
}

inline void Evo_DashLine(float2 p, float2 a, float2 b, inout Evo_DashPath path)
{
    float size = length(b - a);
    if (path.pattern.x > 0.0 && size > 0.00001)
    {
        float2 tangent = (b - a) / size;
        float projection = clamp(dot(p - a, tangent), 0.0, size) + path.length;
        float2 nearest = p - a - tangent * (projection - path.length);
        float reach = path.halfWidth + path.seam;
        if (dot(nearest, nearest) > reach * reach)
        {
            path.length += size;
            return;
        }
        float2 nearestInterval = Evo_DashInterval(projection, path.pattern);
        [loop]
        for (int j = -1; j <= 1; j++)
        {
            float2 interval = nearestInterval + j * path.pattern.x;
            float lo = max(interval.x, path.length);
            float hi = min(interval.y, path.length + size);
            if (lo <= hi)
            {
                float t = clamp(dot(p - a, tangent) + path.length, lo, hi) - path.length;
                float d = length(p - a - tangent * t) - path.halfWidth;
                float along = dot(p - a, tangent) + path.length;
                // Internal joins overlap only their AA band, not an entire cap.
                d = max(d, max(lo - along - (lo > interval.x ? path.seam : 0.0),
                    along - hi - (hi < interval.y ? path.seam : 0.0)));
                if (path.pattern.w > 0.5)
                {
                    if (lo == interval.x) d = min(d, length(p - a - tangent * (lo - path.length)) - path.halfWidth);
                    if (hi == interval.y) d = min(d, length(p - a - tangent * (hi - path.length)) - path.halfWidth);
                }
                path.distance = min(path.distance, d);
            }
        }
    }
    path.length += size;
}

inline float2 Evo_DashCurve(float angle, float2 radius, float squircle, out float2 derivative)
{
    float sn, cs;
    sincos(angle, sn, cs);
    float2 unit = float2(cs, sn);
    float2 turn = float2(-sn, cs);
    if (squircle > 0.5)
    {
        float2 cube = unit * unit * unit;
        float norm = sqrt(sqrt(dot(cube, unit)));
        derivative = radius * (turn / norm - unit * dot(cube, turn) / (norm * norm * norm * norm * norm));
        return radius * unit / norm;
    }
    derivative = radius * turn;
    return radius * unit;
}

inline void Evo_DashArc(float2 p, float2 center, float2 radius, float start, float sweep,
    float squircle, inout Evo_DashPath path)
{
    radius = max(radius, 0.00001);
    // Ramanujan circumference; circular corners are exact. Parameterization is
    // shared at both ends, including on stretched ellipses and squircle corners.
    float meanRadius = 0.5 * (3.0 * (radius.x + radius.y)
        - sqrt((3.0 * radius.x + radius.y) * (radius.x + 3.0 * radius.y)));
    if (squircle > 0.5) meanRadius *= 1.116879;
    float size = abs(sweep) * meanRadius;
    if (path.pattern.x > 0.0 && size > 0.0001 && all(abs(p - center) <= radius + path.halfWidth + path.seam))
    {
        float2 q = p - center;
        float angle = atan2(q.y / radius.y, q.x / radius.x);
        float middle = start + sweep * 0.5;
        // Wrap to the arc's half-turn without a second atan2/sin/cos sequence.
        angle -= 6.283185307 * floor((angle - middle + 3.141592654) / 6.283185307);
        float t = saturate((angle - start) / sweep);
        // Project onto the local curve, not a ray from the shape's center.
        if (abs(radius.x - radius.y) > 0.001 || squircle > 0.5)
        {
            // Keep the iterative projection compact on mobile and WebGL compilers
            [loop]
            for (int k = 0; k < 3; k++)
            {
                float2 tangent;
                float2 curvePoint = Evo_DashCurve(start + sweep * t, radius, squircle, tangent);
                tangent *= sweep;
                t = saturate(t + dot(q - curvePoint, tangent) / max(dot(tangent, tangent), 0.00001));
            }
        }
        float projection = path.length + t * size;
        float2 nearestInterval = Evo_DashInterval(projection, path.pattern);
        [loop]
        for (int j = -1; j <= 1; j++)
        {
            float2 interval = nearestInterval + j * path.pattern.x;
            float lo = max(interval.x, path.length);
            float hi = min(interval.y, path.length + size);
            if (lo <= hi)
            {
                float closest = clamp(projection, lo, hi);
                float2 tangent;
                float2 curvePoint = Evo_DashCurve(start + sweep * (closest - path.length) / size, radius, squircle, tangent);
                float d = length(q - curvePoint) - path.halfWidth;
                float2 startPoint = Evo_DashCurve(start + sweep * (lo - path.length) / size, radius, squircle, tangent);
                d = max(d, -dot(q - startPoint, normalize(tangent) * sign(sweep)) - (lo > interval.x ? path.seam : 0.0));
                float2 endPoint = Evo_DashCurve(start + sweep * (hi - path.length) / size, radius, squircle, tangent);
                d = max(d, dot(q - endPoint, normalize(tangent) * sign(sweep)) - (hi < interval.y ? path.seam : 0.0));
                if (path.pattern.w > 0.5)
                {
                    if (lo == interval.x) d = min(d, length(q - startPoint) - path.halfWidth);
                    if (hi == interval.y) d = min(d, length(q - endPoint) - path.halfWidth);
                }
                path.distance = min(path.distance, d);
            }
        }
    }
    path.length += size;
}

inline void Evo_DashJoin(float2 p, float2 vertex, float2 before, float2 after, inout Evo_DashPath path)
{
    if (path.pattern.x <= 0.0) return;
    float2 interval = Evo_DashInterval(path.length, path.pattern);
    if (path.length < interval.x || path.length > interval.y) return;
    float turn = sign(before.x * after.y - before.y * after.x);
    float2 a = float2(before.y, -before.x) * path.halfWidth * turn;
    float2 b = float2(after.y, -after.x) * path.halfWidth * turn;
    float2 miter = (a + b) / max(1.0 + dot(before, after), 0.0001);
    p -= vertex;
    // Fill the miter between the two edge strips; the original SDF clips it.
    float d = 1e20, winding = 1.0;
    Evo_AddSignedPolygonEdge(p, a, miter, d, winding);
    Evo_AddSignedPolygonEdge(p, miter, b, d, winding);
    Evo_AddSignedPolygonEdge(p, b, 0.0, d, winding);
    Evo_AddSignedPolygonEdge(p, 0.0, a, d, winding);
    path.distance = min(path.distance, sqrt(d) * winding);
}

inline float2 Evo_DashPolygonVertex(uint index, uint firstVertex, float star)
{
    return star > 0.5 ? _EvoStarVertices[index].xy
        : _EvoPolygonVertices[firstVertex + index].xy;
}

inline void Evo_DashShape(float2 p, float2 b, float4 r, float squircle, float shape, inout Evo_DashPath path)
{
    const float pi = 3.141592654;
    float h = path.halfWidth;
    bool rectangle = shape == EVO_UI_SHAPE_RECTANGLE;
    bool ellipse = shape == EVO_UI_SHAPE_ELLIPSE;
    bool heart = shape == EVO_UI_SHAPE_HEART;
    uint segments;

    // Heart: built from two lines and up to four arcs
    float2 scale = 0.0;
    float offset = 0.0;
    float radius = 0.0;
    float angle = 0.0;
    float2 origin = 0.0;
    float2 rightBottom = 0.0;
    float2 rightSide = 0.0;

    // Polygon, star and chevron: walked edge by edge, each starting after its corner
    float star = 0.0;
    uint sides = 0u;
    uint firstVertex = 0u;
    float2 size = 0.0;
    float2 a = 0.0;
    float2 b0 = 0.0;
    float2 c = 0.0;
    float2 before = 0.0;
    float2 edge = 0.0;
    float2 after = 0.0;
    float2 start = 0.0;
    float2 end = 0.0;

    if (rectangle)
    {
        b = max(b - h, 0.0001);
        r = max(r - h, 0.0);
        // Counterclockwise: BR, TR, TL, BL. Each edge begins after its corner.
        segments = 8u;
    }
    else if (ellipse)
    {
        b = max(b - h, 0.0001);
        segments = 2u;
    }
    else if (heart)
    {
        float2 heartSize = max(b - r.x, 0.0001);
        scale = heartSize / float2(0.603553391, 0.5517766955);
        offset = (r.x - h) / min(scale.x, scale.y);
        radius = max(0.353553391 + offset, 0.0001);
        angle = offset >= 0.0 ? acos(clamp(-0.25 / radius, -1.0, 1.0)) : 0.75 * pi;
        origin = float2(0.0, -heartSize.y);
        rightBottom = float2(max(offset, 0.0) * 0.707106781, -max(offset, 0.0) * 0.707106781);
        if (offset < 0.0) rightBottom = float2(0.0, -offset * 1.414213562);
        rightSide = float2(0.25, 0.75) + radius * float2(0.707106781, -0.707106781);
        segments = 6u;
    }
    else
    {
        sides = shape == EVO_UI_SHAPE_CHEVRON ? 6u : shape == EVO_UI_SHAPE_STAR ? 10u : (uint)shape - 7u;
        size = max(b - r.x, 0.0001);
        offset = r.x - h;
        star = shape == EVO_UI_SHAPE_STAR ? 1.0 : 0.0;
        firstVertex = shape == EVO_UI_SHAPE_CHEVRON ? 33u : star > 0.5 ? 0u : ((sides * (sides - 1u)) >> 1u) - 3u;
        a = Evo_DashPolygonVertex(0u, firstVertex, star) * size;
        b0 = Evo_DashPolygonVertex(1u, firstVertex, star) * size;
        before = normalize(a - Evo_DashPolygonVertex(sides - 1u, firstVertex, star) * size);
        edge = normalize(b0 - a);
        segments = sides * 2u;
    }

    // One loop and one arc/line/join call site serve every shape. Inlined per shape and
    // corner, the nested dash searches made this shader slow to compile (seconds on WebGL).
    [loop]
    for (uint segment = 0u; segment < segments; segment++)
    {
        // 0 skips, 1 arc, 2 line, 3 sharp-corner join
        uint kind = 0u;
        float2 from = 0.0;
        float2 to = 0.0;
        float2 joinBefore = 0.0;
        float2 joinAfter = 0.0;
        float arcStart = 0.0;
        float arcSweep = 0.0;
        float arcSquircle = 0.0;
        bool corner = (segment & 1u) == 0u;

        if (rectangle)
        {
            int n = (int)(segment >> 1u);
            float2 signCorner = float2(n < 2 ? 1.0 : -1.0, n == 1 || n == 2 ? 1.0 : -1.0);
            float cornerRadius = n == 0 ? r.y : n == 1 ? r.x : n == 2 ? r.z : r.w;
            float2 incoming = n == 0 ? float2(1,0) : n == 1 ? float2(0,1) : n == 2 ? float2(-1,0) : float2(0,-1);
            float2 outgoing = float2(-incoming.y, incoming.x);
            float2 vertex = signCorner * b;
            if (corner)
            {
                if (cornerRadius > 0.0001)
                {
                    kind = 1u;
                    from = signCorner * (b - cornerRadius);
                    to = cornerRadius;
                    arcStart = (n - 1.0) * pi * 0.5;
                    arcSweep = pi * 0.5;
                    arcSquircle = squircle;
                }
                else
                {
                    kind = 3u;
                    from = vertex;
                    joinBefore = incoming;
                    joinAfter = outgoing;
                }
            }
            else
            {
                float nextRadius = n == 0 ? r.x : n == 1 ? r.z : n == 2 ? r.w : r.y;
                float2 nextVertex = vertex + outgoing * (2.0 * dot(abs(outgoing), b));
                kind = 2u;
                from = vertex + outgoing * cornerRadius;
                to = nextVertex - outgoing * nextRadius;
            }
        }
        else if (ellipse)
        {
            kind = 1u;
            to = b;
            arcStart = segment == 0u ? 0.0 : pi;
            arcSweep = pi;
        }
        else if (heart)
        {
            if (segment == 0u)
            {
                kind = 2u;
                from = origin + rightBottom * scale;
                to = origin + rightSide * scale;
            }
            else if (segment == 1u)
            {
                kind = 1u;
                from = origin + float2(0.25, 0.75) * scale;
                to = radius * scale;
                arcStart = -pi * 0.25;
                arcSweep = angle + pi * 0.25;
            }
            else if (segment == 2u)
            {
                kind = offset < -0.0001 ? 1u : 0u;
                from = origin + float2(0,1) * scale;
                to = -offset * scale;
                arcStart = -pi * 0.25;
                arcSweep = -pi * 0.5;
            }
            else if (segment == 3u)
            {
                kind = 1u;
                from = origin + float2(-0.25, 0.75) * scale;
                to = radius * scale;
                arcStart = pi - angle;
                arcSweep = angle + pi * 0.25;
            }
            else if (segment == 4u)
            {
                kind = 2u;
                from = origin + rightSide * float2(-1,1) * scale;
                to = origin + rightBottom * float2(-1,1) * scale;
            }
            else
            {
                kind = offset > 0.0001 ? 1u : 0u;
                from = origin;
                to = offset * scale;
                arcStart = pi * 1.25;
                arcSweep = pi * 0.5;
            }
        }
        else if (corner)
        {
            uint n = segment >> 1u;
            // These indices advance by one, so wrapping needs no integer division.
            uint following = n + 2u < sides ? n + 2u : n + 2u - sides;
            c = Evo_DashPolygonVertex(following, firstVertex, star) * size;
            after = normalize(c - b0);
            float2 normal = float2(edge.y, -edge.x);
            float2 normalBefore = float2(before.y, -before.x);
            float2 normalAfter = float2(after.y, -after.x);
            float turnA = before.x * edge.y - before.y * edge.x;
            float turnB = edge.x * after.y - edge.y * after.x;
            bool roundA = offset * turnA > 0.00001;
            bool roundB = offset * turnB > 0.00001;
            start = a + (roundA ? normal * offset
                : (normalBefore + normal) * (offset / max(1.0 + dot(before, edge), 0.0001)));
            end = b0 + (roundB ? normal * offset
                : (normal + normalAfter) * (offset / max(1.0 + dot(edge, after), 0.0001)));
            if (roundA)
            {
                float2 arcFrom = normalBefore * sign(offset);
                kind = 1u;
                from = a;
                to = abs(offset);
                arcStart = atan2(arcFrom.y, arcFrom.x);
                arcSweep = atan2(turnA, dot(before, edge));
            }
            else
            {
                kind = 3u;
                from = start;
                joinBefore = before;
                joinAfter = edge;
            }
        }
        else
        {
            kind = 2u;
            from = start;
            to = end;
            a = b0;
            b0 = c;
            before = edge;
            edge = after;
        }

        if (kind == 1u)
            Evo_DashArc(p, from, to, arcStart, arcSweep, arcSquircle, path);
        else if (kind == 2u)
            Evo_DashLine(p, from, to, path);
        else if (kind == 3u)
            Evo_DashJoin(p, from, joinBefore, joinAfter, path);
    }
}

inline float4 Evo_UI_DashPattern(float2 b, float4 r, float squircle, float shape, float width, float4 dash)
{
    if (dash.x <= 0.0 || width <= 0.001) return 0.0;
    Evo_DashPath path = (Evo_DashPath)0;
    path.halfWidth = min(width * 0.5, min(b.x, b.y) * 0.999);
    // Vertex stage only: fit an integer number of repeats to the closed path.
    Evo_DashShape(0.0, b, r, squircle, shape, path);
    float dashLength = max(dash.x, dash.w > 0.5 ? path.halfWidth * 2.0 : 0.0001);
    float requestedPeriod = dashLength + dash.y;
    float count = max(1.0, floor(path.length / requestedPeriod + 0.5));
    float period = max(path.length / count, 0.0001);
    float body = period * dashLength / requestedPeriod;
    // Keep round caps from overlapping the gap when the fitted period shrinks.
    if (dash.w > 0.5) body = max(body, min(path.halfWidth * 2.0, period));
    return float4(period, max(0.0, body * 0.5 - (dash.w > 0.5 ? path.halfWidth : 0.0)), dash.z * period, dash.w);
}

inline float Evo_UI_DashedOutline(float2 p, float2 b, float4 r, float squircle,
    float shape, float width, float aa, float4 pattern)
{
    Evo_DashPath path = (Evo_DashPath)0;
    path.halfWidth = min(width * 0.5, min(b.x, b.y) * 0.999);
    path.pattern = pattern;
    path.seam = aa * 2.0;
    path.distance = 1e20;
    Evo_DashShape(p, b, r, squircle, shape, path);
    return path.distance;
}

#endif