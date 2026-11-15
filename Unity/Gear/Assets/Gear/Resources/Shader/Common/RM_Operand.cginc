#ifndef RM_OPERAND
#define RM_OPERAND

uniform half _GearOperandEnabled;
uniform half4 _GearOperandCenterSize; // center.xy, size.zw
uniform half _GearOperandShape;       // 0 rounded rect, 1 circle
uniform half _GearOperandOp;          // 0 union, 1 subtract
uniform half _GearOperandSmoothing;
uniform half _GearOperandCorner;

float RM_OperandMask(float2 uv)
{
    if (_GearOperandEnabled < 0.5) return 0;

    float2 center = _GearOperandCenterSize.xy;
    float2 halfSize = max(_GearOperandCenterSize.zw, 0.0001) * 0.5;
    float2 delta = uv - center;

    float distanceToEdge;
    if (_GearOperandShape > 0.5)
    {
        distanceToEdge = length(delta) - min(halfSize.x, halfSize.y);
    }
    else
    {
        float2 d = abs(delta) - max(halfSize - _GearOperandCorner, 0.0);
        distanceToEdge = length(max(d, 0.0)) + min(max(d.x, d.y), 0.0) - _GearOperandCorner;
    }

    float softness = max(_GearOperandSmoothing, 0.0001);
    return 1.0 - smoothstep(-softness, softness, distanceToEdge);
}

void RM_ApplyOperand(inout half4 color, float2 uv, half tintAlpha)
{
    float mask = RM_OperandMask(uv);
    if (mask <= 0) return;

    if (_GearOperandOp > 0.5)
        color.a *= 1.0 - mask;
    else
        color.a = max(color.a, mask * tintAlpha);
}

#endif
