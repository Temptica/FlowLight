using System;
using System.Collections.Generic;
using System.Linq;
using FlowLight.Core.Geometry;

namespace FlowLight.Core.Network;

internal static class NetworkBuilder
{
    public static void Build(IEnumerable<Junction> junctions, IEnumerable<Road> roads, ISet<Junction> resizedJunctions)
    {
        foreach (var junction in resizedJunctions)
            junction.Radius = RoadGeometry.JunctionRadius(junction);

        foreach (var road in roads)
            BuildLanes(road);

        foreach (var junction in junctions)
            BuildConnectors(junction);
    }

    public static void BuildConnectors(Junction junction)
    {
        foreach (var connector in junction.Connectors)
            connector.IsRemoved = true;

        junction.Connectors.Clear();
        var incoming = junction.Roads.SelectMany(r => r.LanesInto(junction)).ToList();
        foreach (var lane in incoming)
        {
            lane.Connectors.Clear();
            if (!junction.IsActive)
                continue;

            var targets = CustomTargets(junction, lane) ?? DefaultTargets(junction, lane);
            foreach (var target in targets)
                Connect(junction, lane, target);
        }

        for (var i = 0; i < junction.Connectors.Count; i++)
        for (var j = i + 1; j < junction.Connectors.Count; j++)
            junction.Connectors[i].TryRegisterConflict(junction.Connectors[j]);

        junction.RebuildApproaches();
    }

    public static IEnumerable<Lane> DefaultTargets(Junction junction, Lane lane)
    {
        foreach (var exit in ExitRoads(junction, lane.Road))
        {
            var exitLanes = exit.LanesFrom(junction).OrderBy(l => l.Index).ToList();
            if (exitLanes.Count == 0)
                continue;

            var turn = RoadGeometry.ClassifyTurn(lane.Heading, exitLanes[0].Shape.StartDirection);
            if (!DefaultLaneAllows(lane, turn))
                continue;

            yield return turn switch
            {
                TurnDirection.Right => exitLanes[0],
                TurnDirection.Left or TurnDirection.UTurn => exitLanes[^1],
                _ => exitLanes[Math.Min(lane.Index, exitLanes.Count - 1)]
            };
        }
    }

    private static List<Lane>? CustomTargets(Junction junction, Lane lane)
    {
        if (junction.ConnectionsFor(lane.Key) is not { } keys)
            return null;

        var targets = junction.OutgoingLanes.Where(l => keys.Contains(l.Key)).ToList();
        return targets.Count > 0 ? targets : null;
    }

    private static void BuildLanes(Road road)
    {
        foreach (var lane in road.Lanes)
            lane.IsRemoved = true;

        road.Lanes.Clear();
        if (road.AllowsTravelFrom(road.A))
            AddLanes(road, road.A, road.B);
        if (road.AllowsTravelFrom(road.B))
            AddLanes(road, road.B, road.A);
    }

    private static void AddLanes(Road road, Junction from, Junction to)
    {
        var direction = (to.Position - from.Position).SafeNormalize();
        var right = direction.RightPerpendicular();
        var start = from.Position + direction * from.Radius;
        var end = to.Position - direction * to.Radius;

        for (var index = 0; index < road.LaneCount(from); index++)
        {
            var offset = right * RoadGeometry.LaneOffset(road, from, index);
            road.Lanes.Add(new Lane(road, from, to, index, Polyline.Line(start + offset, end + offset)));
        }
    }

    private static void Connect(Junction junction, Lane lane, Lane target)
    {
        var turn = target.Road == lane.Road
            ? TurnDirection.UTurn
            : RoadGeometry.ClassifyTurn(lane.Heading, target.Shape.StartDirection);
        var connector = new Connector(junction, lane, target, turn, RoadGeometry.ConnectorShape(lane, target, turn));
        lane.Connectors.Add(connector);
        junction.Connectors.Add(connector);
    }

    private static IEnumerable<Road> ExitRoads(Junction junction, Road arrival)
    {
        var exits = junction.Roads.Where(r => r != arrival && r.AllowsTravelFrom(junction)).ToList();
        if (exits.Count == 0 && arrival.AllowsTravelFrom(junction))
            exits.Add(arrival);

        return exits;
    }

    private static bool DefaultLaneAllows(Lane lane, TurnDirection turn) => turn switch
    {
        TurnDirection.Right => lane.Index == 0,
        TurnDirection.Left or TurnDirection.UTurn => lane.IsLeftmost,
        _ => true
    };
}
