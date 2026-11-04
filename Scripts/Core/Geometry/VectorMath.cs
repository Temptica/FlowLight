using System;
using System.Numerics;

namespace FlowLight.Core.Geometry;

public static class VectorMath
{
    public static Vector2 RightPerpendicular(this Vector2 v) => new(-v.Y, v.X);

    public static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

    public static Vector2 SafeNormalize(this Vector2 v)
    {
        var length = v.Length();
        return length > 1e-5f ? v / length : Vector2.UnitX;
    }

    public static float Angle(this Vector2 v) => MathF.Atan2(v.Y, v.X);

    public static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var lengthSquared = ab.LengthSquared();
        if (lengthSquared < 1e-6f)
            return Vector2.Distance(point, a);

        var t = Math.Clamp(Vector2.Dot(point - a, ab) / lengthSquared, 0f, 1f);
        return Vector2.Distance(point, a + ab * t);
    }

    public static bool TryIntersectLines(Vector2 p, Vector2 pDirection, Vector2 q, Vector2 qDirection, out Vector2 hit)
    {
        var denominator = Cross(pDirection, qDirection);
        if (MathF.Abs(denominator) < 1e-4f)
        {
            hit = default;
            return false;
        }

        var t = Cross(q - p, qDirection) / denominator;
        hit = p + pDirection * t;
        return true;
    }

    public static bool TryIntersectSegments(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2, out float t, out float u)
    {
        var d1 = a2 - a1;
        var d2 = b2 - b1;
        var denominator = Cross(d1, d2);
        t = u = 0f;
        if (MathF.Abs(denominator) < 1e-6f)
            return false;

        var offset = b1 - a1;
        t = Cross(offset, d2) / denominator;
        u = Cross(offset, d1) / denominator;
        return t is >= 0f and <= 1f && u is >= 0f and <= 1f;
    }
}
