using System;
using System.Collections.Generic;
using System.Numerics;

namespace FlowLight.Core.Geometry;

public readonly record struct PathSample(Vector2 Position, Vector2 Direction);

public sealed class Polyline
{
    private readonly Vector2[] _points;
    private readonly float[] _cumulative;

    public Polyline(IReadOnlyList<Vector2> points)
    {
        if (points.Count < 2)
            throw new ArgumentException("A polyline needs at least two points.", nameof(points));

        _points = new Vector2[points.Count];
        _cumulative = new float[points.Count];
        for (var i = 0; i < points.Count; i++)
        {
            _points[i] = points[i];
            if (i > 0)
                _cumulative[i] = _cumulative[i - 1] + Vector2.Distance(points[i - 1], points[i]);
        }

        Length = Math.Max(_cumulative[^1], 0.01f);
    }

    public float Length { get; }
    public IReadOnlyList<Vector2> Points => _points;
    public Vector2 Start => _points[0];
    public Vector2 End => _points[^1];
    public Vector2 StartDirection => (_points[1] - _points[0]).SafeNormalize();
    public Vector2 EndDirection => (_points[^1] - _points[^2]).SafeNormalize();

    public static Polyline Line(Vector2 from, Vector2 to) => new(new[] { from, to });

    public static Polyline QuadraticBezier(Vector2 p0, Vector2 p1, Vector2 p2, int segments = 12)
    {
        var points = new Vector2[segments + 1];
        for (var i = 0; i <= segments; i++)
        {
            var t = i / (float)segments;
            var u = 1f - t;
            points[i] = u * u * p0 + 2f * u * t * p1 + t * t * p2;
        }

        return new Polyline(points);
    }

    public PathSample Sample(float distance)
    {
        distance = Math.Clamp(distance, 0f, Length);
        var index = Array.BinarySearch(_cumulative, distance);
        if (index < 0)
            index = ~index;

        index = Math.Clamp(index, 1, _points.Length - 1);
        var segmentStart = _cumulative[index - 1];
        var segmentLength = _cumulative[index] - segmentStart;
        var t = segmentLength > 1e-5f ? (distance - segmentStart) / segmentLength : 0f;
        var a = _points[index - 1];
        var b = _points[index];
        return new PathSample(Vector2.Lerp(a, b, t), (b - a).SafeNormalize());
    }

    public bool TryFindIntersection(Polyline other, out float distanceAlongThis, out float distanceAlongOther)
    {
        for (var i = 1; i < _points.Length; i++)
        for (var j = 1; j < other._points.Length; j++)
        {
            if (!VectorMath.TryIntersectSegments(_points[i - 1], _points[i], other._points[j - 1], other._points[j], out var t, out var u))
                continue;

            distanceAlongThis = _cumulative[i - 1] + (_cumulative[i] - _cumulative[i - 1]) * t;
            distanceAlongOther = other._cumulative[j - 1] + (other._cumulative[j] - other._cumulative[j - 1]) * u;
            return true;
        }

        distanceAlongThis = 0f;
        distanceAlongOther = 0f;
        return false;
    }
}
