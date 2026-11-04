using System;
using System.Collections.Generic;
using System.Linq;
using FlowLight.Core.Geometry;
using FlowLight.Core.Network;
using FlowLight.Core.Simulation;
using FlowLight.Core.Vehicles;
using NumericVector = System.Numerics.Vector2;

namespace FlowLight.Presentation.Interaction;

public abstract record Selection;

public sealed record JunctionSelection(Junction Junction) : Selection;

public sealed record RoadSelection(Road Road) : Selection;

public sealed record VehicleSelection(Vehicle Vehicle) : Selection;

public sealed class SelectionState
{
    public Selection? Current { get; private set; }

    public event Action<Selection?>? Changed;

    public void Select(Selection? selection)
    {
        Current = selection;
        Changed?.Invoke(selection);
    }
}

public static class WorldPicker
{
    private const float VehicleRadius = 4f;
    private const float JunctionMargin = 8f;
    private const float RoadMargin = 3f;
    private const float ApproachReach = 22f;
    private const float LaneReach = 3f;

    public static Selection? Pick(TrafficSimulation simulation, NumericVector point)
    {
        var vehicle = simulation.Vehicles
            .Where(v => NumericVector.Distance(v.Center, point) < VehicleRadius)
            .OrderBy(v => NumericVector.Distance(v.Center, point))
            .FirstOrDefault();
        if (vehicle is not null)
            return new VehicleSelection(vehicle);

        if (PickJunction(simulation.Network.ActiveJunctions, point) is { } junction)
            return new JunctionSelection(junction);

        return PickRoad(simulation.Network.Roads, point) is { } road ? new RoadSelection(road) : null;
    }

    public static Junction? PickJunction(IEnumerable<Junction> junctions, NumericVector point) => junctions
        .Where(j => NumericVector.Distance(j.Position, point) < j.Radius + JunctionMargin)
        .OrderBy(j => NumericVector.Distance(j.Position, point))
        .FirstOrDefault();

    public static Road? PickRoad(IEnumerable<Road> roads, NumericVector point) => roads
        .Select(r => (Road: r, Distance: VectorMath.DistanceToSegment(point, r.A.Position, r.B.Position)))
        .Where(r => r.Distance < r.Road.Width * 0.5f + RoadMargin)
        .OrderBy(r => r.Distance)
        .Select(r => r.Road)
        .FirstOrDefault();

    public static (Junction Junction, Approach Approach)? PickApproach(IEnumerable<Junction> junctions, NumericVector point)
    {
        var best = junctions
            .SelectMany(j => j.Approaches.Select(a => (Junction: j, Approach: a, Distance: NumericVector.Distance(StopLineCenter(a) - a.Heading * 6f, point))))
            .Where(c => c.Distance < ApproachReach)
            .OrderBy(c => c.Distance)
            .FirstOrDefault();
        return best.Approach is null ? null : (best.Junction, best.Approach);
    }

    public static FlowLight.Core.Pedestrians.Crosswalk? PickCrosswalk(IEnumerable<Junction> junctions, NumericVector point) => junctions
        .SelectMany(j => j.Crosswalks)
        .Where(c => NumericVector.Distance(c.Center, point) < c.Length * 0.5f + 3f)
        .OrderBy(c => NumericVector.Distance(c.Center, point))
        .FirstOrDefault();

    public static Lane? PickLaneEnd(IEnumerable<Lane> lanes, NumericVector point, bool atStart) => lanes
        .Select(l => (Lane: l, Distance: NumericVector.Distance(LaneMarker(l, atStart), point)))
        .Where(l => l.Distance < LaneReach)
        .OrderBy(l => l.Distance)
        .Select(l => l.Lane)
        .FirstOrDefault();

    public static NumericVector LaneMarker(Lane lane, bool atStart) =>
        atStart ? lane.Shape.Start + lane.Shape.StartDirection * 4f : lane.Shape.End - lane.Heading * 4f;

    public static NumericVector StopLineCenter(Approach approach)
    {
        var sum = NumericVector.Zero;
        foreach (var lane in approach.Lanes)
            sum += lane.Shape.End;
        return sum / approach.Lanes.Count;
    }
}
