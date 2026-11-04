using System.Linq;
using FlowLight.Core.Network;
using FlowLight.Core.Pedestrians;
using Godot;

namespace FlowLight.Presentation.Rendering;

public partial class RoadView : Node3D
{
    private const float AsphaltHeight = 0.02f;
    private const float JunctionHeight = 0.03f;
    private const float MarkingHeight = 0.05f;
    private const float MarkingWidth = 0.18f;
    private const float ArrowSetback = 9f;

    private readonly StandardMaterial3D _material = MeshBuilder.VertexColorMaterial();
    private RoadNetwork _network = null!;
    private MeshInstance3D? _surface;

    public void Initialize(RoadNetwork network)
    {
        _network = network;
        Rebuild();
    }

    public void Rebuild()
    {
        var builder = new MeshBuilder();
        foreach (var road in _network.Roads)
            AddRoad(builder, road);

        foreach (var junction in _network.Junctions.Where(j => !j.IsMapEdge && j.Roads.Count > 0))
        {
            var asphalt = junction.IsActive ? Palette.JunctionAsphalt : Palette.InactiveAsphalt;
            builder.Disc(junction.Position.ToGodot(), DisplayRadius(junction), JunctionHeight, asphalt);
            if (junction.IsIntersection)
                AddApproachMarkings(builder, junction);
            foreach (var crosswalk in junction.Crosswalks)
                AddCrosswalk(builder, crosswalk);
        }

        foreach (var junction in _network.Gateways)
            builder.Ring(junction.Position.ToGodot(), 4f, 6f, MarkingHeight, Palette.Boundary);

        _surface?.QueueFree();
        _surface = new MeshInstance3D
        {
            Mesh = builder.Commit(),
            MaterialOverride = _material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        AddChild(_surface);
    }

    private static float DisplayRadius(Junction junction)
    {
        var widest = junction.Roads.Max(r => r.Width) * 0.5f;
        return junction.IsActive ? Mathf.Max(junction.Radius, widest) : widest + (junction.Roads.Count >= 3 ? 3f : 0f);
    }

    private static void AddRoad(MeshBuilder builder, Road road)
    {
        var a = road.A.Position.ToGodot();
        var b = road.B.Position.ToGodot();
        var direction = (b - a).Normalized();
        var right = new Vector2(-direction.Y, direction.X);
        var center = right * ((road.RightExtent + road.LeftExtent) * 0.5f);
        var active = road.IsActive;
        builder.FlatStrip(a + center, b + center, road.Width, AsphaltHeight, active ? Palette.Asphalt : Palette.InactiveAsphalt);

        var start = a + direction * road.A.Radius;
        var end = b - direction * road.B.Radius;
        if (start.DistanceTo(end) < 1f)
            return;

        var lanes = road.Lanes
            .Select(l => (Lane: l, Lateral: (l.Shape.Start.ToGodot() - a).Dot(right)))
            .OrderBy(l => l.Lateral)
            .ToList();
        if (lanes.Count == 0)
            return;

        var marking = active ? Palette.Marking : Palette.InactiveMarking;
        var half = RoadGeometry.LaneWidth * 0.5f;
        builder.FlatStrip(start + right * (lanes[0].Lateral - half), end + right * (lanes[0].Lateral - half), MarkingWidth, MarkingHeight, marking);
        builder.FlatStrip(start + right * (lanes[^1].Lateral + half), end + right * (lanes[^1].Lateral + half), MarkingWidth, MarkingHeight, marking);

        for (var i = 1; i < lanes.Count; i++)
        {
            var offset = right * ((lanes[i - 1].Lateral + lanes[i].Lateral) * 0.5f);
            var opposing = lanes[i - 1].Lane.To != lanes[i].Lane.To;
            var color = opposing && active ? Palette.CenterLine : marking;
            if (opposing && road.Class != RoadClass.Local)
                builder.FlatStrip(start + offset, end + offset, MarkingWidth * 1.4f, MarkingHeight, color);
            else
                builder.DashedStrip(start + offset, end + offset, MarkingWidth, MarkingHeight, color);
        }

        if (road.IsOneWay)
            AddOneWayArrows(builder, road.Lanes[0], start, end, marking);
    }

    private static void AddOneWayArrows(MeshBuilder builder, Lane lane, Vector2 start, Vector2 end, Color color)
    {
        var forward = lane.Heading.ToGodot();
        var length = start.DistanceTo(end);
        var axisStart = lane.Road.A == lane.From ? start : end;
        for (var distance = 25f; distance < length - 25f; distance += 45f)
            builder.Arrowhead(axisStart + forward * (distance + 3f), forward, 3f, 2.4f, MarkingHeight, color);
    }

    private static void AddApproachMarkings(MeshBuilder builder, Junction junction)
    {
        foreach (var approach in junction.Approaches)
        {
            var sign = junction.HasSignal ? ApproachSign.Stop : junction.SignFor(approach.Road);
            foreach (var lane in approach.Lanes)
            {
                var end = lane.Shape.End.ToGodot();
                var forward = lane.Heading.ToGodot();
                var side = new Vector2(-forward.Y, forward.X) * (RoadGeometry.LaneWidth * 0.5f);
                switch (sign)
                {
                    case ApproachSign.Yield:
                        AddSharkTeeth(builder, end - forward * 0.3f, forward, side);
                        break;
                    case ApproachSign.Stop:
                        builder.FlatStrip(end - side, end + side, 0.6f, MarkingHeight, Palette.Marking);
                        break;
                }

                AddTurnArrows(builder, lane, end - forward * ArrowSetback, forward);
            }
        }
    }

    private static void AddCrosswalk(MeshBuilder builder, Crosswalk crosswalk)
    {
        var start = crosswalk.Start.ToGodot();
        var end = crosswalk.End.ToGodot();
        var across = (end - start).Normalized();
        var along = new Vector2(-across.Y, across.X) * (Crosswalk.Width * 0.5f);
        if (!crosswalk.HasZebra)
        {
            builder.DashedStrip(start + along, end + along, 0.12f, MarkingHeight, Palette.CrossingHint, 0.6f, 0.6f);
            builder.DashedStrip(start - along, end - along, 0.12f, MarkingHeight, Palette.CrossingHint, 0.6f, 0.6f);
            return;
        }

        for (var offset = 0.5f; offset < crosswalk.Length - 0.2f; offset += 1f)
        {
            var center = start + across * offset;
            builder.FlatStrip(center - along, center + along, 0.5f, MarkingHeight, Palette.Marking);
        }
    }

    private static void AddTurnArrows(MeshBuilder builder, Lane lane, Vector2 origin, Vector2 forward)
    {
        if (lane.Connectors.Count == 0)
            return;

        const float width = 0.5f;
        var right = new Vector2(-forward.Y, forward.X);
        builder.FlatStrip(origin - forward * 5f, origin, width, MarkingHeight, Palette.Marking);
        foreach (var turn in lane.Connectors.Select(c => c.Turn).Distinct())
        {
            var direction = turn switch
            {
                TurnDirection.Right => right,
                TurnDirection.Left => -right,
                TurnDirection.UTurn => -forward,
                _ => forward
            };
            if (turn == TurnDirection.UTurn)
                continue;

            var reach = turn == TurnDirection.Straight ? 2.4f : 0.9f;
            builder.FlatStrip(origin - direction * (width * 0.5f), origin + direction * reach, width, MarkingHeight, Palette.Marking);
            builder.Arrowhead(origin + direction * (reach + 1.6f), direction, 1.8f, 1.8f, MarkingHeight, Palette.Marking);
        }
    }

    private static void AddSharkTeeth(MeshBuilder builder, Vector2 center, Vector2 forward, Vector2 side)
    {
        const int teeth = 4;
        var step = side * 2f / teeth;
        for (var i = 0; i < teeth; i++)
        {
            var left = center - side + step * i;
            var right = left + step;
            var tip = (left + right) * 0.5f - forward * 1.1f;
            builder.Triangle(
                new Vector3(left.X, MarkingHeight, left.Y),
                new Vector3(right.X, MarkingHeight, right.Y),
                new Vector3(tip.X, MarkingHeight, tip.Y),
                Palette.Marking,
                Vector3.Up);
        }
    }
}
