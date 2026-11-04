using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FlowLight.Core.Control;
using FlowLight.Core.Geometry;
using FlowLight.Core.Network;
using FlowLight.Core.Vehicles;

namespace FlowLight.Core.Pedestrians;

public sealed class PedestrianTraffic
{
    private const int MaxWaitingPerSide = 6;
    private const float GapSeconds = 5f;
    private const float FocusWeight = 3f;
    private const float StopMargin = 4f;
    private const float CommittedDistance = 6f;
    private const float WalkWindow = 7f;
    private const float CourtesyWait = 6f;

    private readonly Random _random;
    private readonly List<Pedestrian> _pedestrians = new();
    private float _accumulated;
    private int _nextId;

    internal PedestrianTraffic(Random random)
    {
        _random = random;
    }

    public IReadOnlyList<Pedestrian> All => _pedestrians;
    public float RatePerSecond { get; internal set; }
    public int Crossed { get; private set; }
    public int Jaywalkers { get; private set; }

    internal void Rebuild(RoadNetwork network)
    {
        foreach (var junction in network.Junctions)
        {
            var fresh = junction.IsIntersection
                ? junction.Roads.Select(road => Build(junction, road)).ToList()
                : new List<Crosswalk>();
            foreach (var pedestrian in _pedestrians.Where(p => p.Crosswalk.Junction == junction).ToList())
            {
                var replacement = fresh.FirstOrDefault(c => c.Road == pedestrian.Crosswalk.Road);
                if (replacement is null)
                {
                    _pedestrians.Remove(pedestrian);
                    continue;
                }

                pedestrian.Crosswalk = replacement;
                replacement.Pedestrians.Add(pedestrian);
            }

            junction.ReplaceCrosswalks(fresh);
        }
    }

    internal void Update(float deltaTime, RoadNetwork network)
    {
        Spawn(deltaTime, network);
        foreach (var pedestrian in _pedestrians.ToList())
        {
            switch (pedestrian.State)
            {
                case PedestrianState.Waiting:
                    pedestrian.Wait += deltaTime;
                    if (MayCross(pedestrian))
                        pedestrian.State = PedestrianState.Crossing;
                    break;
                case PedestrianState.Crossing:
                    pedestrian.Progress += pedestrian.Speed * deltaTime;
                    if (pedestrian.Progress >= pedestrian.Crosswalk.Length)
                        Finish(pedestrian);
                    break;
            }
        }
    }

    private static Crosswalk Build(Junction junction, Road road)
    {
        var outward = (road.Other(junction).Position - junction.Position).SafeNormalize();
        var side = outward.RightPerpendicular();
        var lateral = (road.RightExtent + road.LeftExtent) * 0.5f * (road.A == junction ? 1f : -1f);
        var center = junction.Position + outward * Math.Max(junction.Radius - Crosswalk.Width * 0.5f - 0.5f, 2f) + side * lateral;
        var half = road.Width * 0.5f;
        return new Crosswalk(junction, road, center + side * half, center - side * half);
    }

    private void Spawn(float deltaTime, RoadNetwork network)
    {
        _accumulated += RatePerSecond * deltaTime;
        if (_accumulated < 1f)
            return;

        _accumulated -= 1f;
        var crosswalks = network.ActiveJunctions.SelectMany(j => j.Crosswalks).ToList();
        if (crosswalks.Count == 0)
            return;

        var total = crosswalks.Sum(Weight);
        var roll = _random.NextSingle() * total;
        var chosen = crosswalks[^1];
        foreach (var crosswalk in crosswalks)
        {
            roll -= Weight(crosswalk);
            if (roll > 0f)
                continue;

            chosen = crosswalk;
            break;
        }

        var fromStart = _random.NextSingle() < 0.5f;
        if (chosen.Pedestrians.Count(p => p.FromStart == fromStart && p.State == PedestrianState.Waiting) >= MaxWaitingPerSide)
            return;

        var pedestrian = new Pedestrian(++_nextId, chosen, fromStart, 1.1f + _random.NextSingle() * 0.5f, 30f + _random.NextSingle() * 60f, (_random.NextSingle() - 0.5f) * 1.6f);
        _pedestrians.Add(pedestrian);
        chosen.Pedestrians.Add(pedestrian);
    }

    private static float Weight(Crosswalk crosswalk) => crosswalk.Junction.IsFocus ? FocusWeight : 1f;

    private void Finish(Pedestrian pedestrian)
    {
        pedestrian.State = PedestrianState.Done;
        pedestrian.Crosswalk.Pedestrians.Remove(pedestrian);
        _pedestrians.Remove(pedestrian);
        pedestrian.Crosswalk.Junction.Statistics.RecordPedestrian(pedestrian.Wait);
        Crossed++;
    }

    private bool MayCross(Pedestrian pedestrian)
    {
        var crosswalk = pedestrian.Crosswalk;
        var junction = crosswalk.Junction;
        if (VehicleCommitted(crosswalk) || VehicleCannotStop(crosswalk))
            return false;

        if (pedestrian.Wait > pedestrian.Patience)
        {
            if (!crosswalk.HasZebra)
            {
                pedestrian.IsJaywalking = true;
                Jaywalkers++;
            }

            return true;
        }

        if (junction.Control is SignalControl signal && junction.ApproachFor(crosswalk.Road) is not null)
            return signal.AspectFor(crosswalk.Road) == SignalAspect.Red && signal.SinceRed(crosswalk.Road) < WalkWindow;

        if (crosswalk.HasZebra)
            return crosswalk.IsOccupied || !CarsKeptWaiting(crosswalk);

        return crosswalk.IsOccupied || !VehicleWithinGap(crosswalk);
    }

    private static bool VehicleCommitted(Crosswalk crosswalk)
    {
        foreach (var (vehicle, connector) in crosswalk.Junction.Reservations)
        {
            if (connector.To.Road == crosswalk.Road)
                return true;
            if (connector.From.Road == crosswalk.Road && (vehicle.Path != connector || vehicle.Distance < CommittedDistance))
                return true;
        }

        return false;
    }

    private static bool VehicleCannotStop(Crosswalk crosswalk)
    {
        foreach (var lane in crosswalk.IncomingLanes)
        {
            var front = lane.Vehicles.FirstOrDefault(v => !v.HasClearance);
            if (front is { IsStopped: false } && front.RemainingOnPath < DrivingModel.StoppingDistance(front, front.Type.ComfortableDeceleration) + StopMargin)
                return true;
        }

        return false;
    }

    private static bool CarsKeptWaiting(Crosswalk crosswalk)
    {
        foreach (var lane in crosswalk.Junction.IncomingLanes)
        {
            if (lane.Vehicles.FirstOrDefault() is { WaitReason: WaitReason.Pedestrians } waiting
                && waiting.WaitAtStopLine > CourtesyWait
                && (lane.Road == crosswalk.Road || waiting.PlannedConnector?.To.Road == crosswalk.Road))
                return true;
        }

        return false;
    }

    private static bool VehicleWithinGap(Crosswalk crosswalk)
    {
        foreach (var lane in crosswalk.IncomingLanes)
        {
            if (PriorityRules.FrontWaitingVehicle(lane) is { } front && PriorityRules.TimeToArrival(front) < GapSeconds && !front.IsStopped)
                return true;
        }

        foreach (var lane in crosswalk.Junction.IncomingLanes)
        {
            if (PriorityRules.FrontWaitingVehicle(lane) is { PlannedConnector: { } planned } turning
                && planned.To.Road == crosswalk.Road
                && PriorityRules.TimeToArrival(turning) < GapSeconds)
                return true;
        }

        return false;
    }
}
