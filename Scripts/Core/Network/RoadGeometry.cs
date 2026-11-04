using System;
using System.Linq;
using System.Numerics;
using FlowLight.Core.Geometry;

namespace FlowLight.Core.Network;

public static class RoadGeometry
{
    public const float LaneWidth = 3.5f;
    public const float ShoulderWidth = 0.75f;

    public static float LaneOffset(Road road, Junction origin, int laneIndex)
    {
        var lanes = road.LaneCount(origin);
        return road.IsOneWay
            ? (lanes / 2f - laneIndex - 0.5f) * LaneWidth
            : (lanes - laneIndex - 0.5f) * LaneWidth;
    }

    public static float JunctionRadius(Junction junction)
    {
        if (!junction.IsActive || junction.Roads.Count == 0)
            return 0f;

        var widest = junction.Roads.Max(r => r.Width);
        return junction.Roads.Count <= 2 ? widest * 0.5f : widest * 0.5f + 3f;
    }

    public static TurnDirection ClassifyTurn(Vector2 incoming, Vector2 outgoing)
    {
        var angle = MathF.Atan2(VectorMath.Cross(incoming, outgoing), Vector2.Dot(incoming, outgoing));
        var degrees = MathF.Abs(angle) * 180f / MathF.PI;
        if (degrees < 35f)
            return TurnDirection.Straight;
        if (degrees > 150f)
            return TurnDirection.UTurn;

        return angle > 0f ? TurnDirection.Right : TurnDirection.Left;
    }

    public static Polyline ConnectorShape(Lane from, Lane to, TurnDirection turn)
    {
        var start = from.Shape.End;
        var end = to.Shape.Start;
        if (turn == TurnDirection.Straight)
            return Polyline.Line(start, end);

        var span = Vector2.Distance(start, end);
        if (VectorMath.TryIntersectLines(start, from.Heading, end, to.Shape.StartDirection, out var control)
            && Vector2.Distance(start, control) < span * 2f
            && Vector2.Dot(control - start, from.Heading) > 0f)
        {
            return Polyline.QuadraticBezier(start, control, end);
        }

        var bulge = from.Heading * span * 0.6f;
        return Polyline.QuadraticBezier(start, (start + end) * 0.5f + bulge, end);
    }
}
