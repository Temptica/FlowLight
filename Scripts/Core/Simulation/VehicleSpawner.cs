using System;
using System.Collections.Generic;
using System.Linq;
using FlowLight.Core.Drivers;
using FlowLight.Core.Network;
using FlowLight.Core.Routing;
using FlowLight.Core.Vehicles;

namespace FlowLight.Core.Simulation;

internal sealed class VehicleSpawner
{
    private const float MaxBacklog = 4f;
    private const float SafeEntryGap = 3f;
    private const float EntryHeadway = 1.2f;

    private readonly RoadNetwork _network;
    private readonly Router _router;
    private readonly DriverFactory _drivers;
    private readonly Random _random;
    private readonly Dictionary<Junction, float> _accumulated = new();
    private int _nextId;

    public VehicleSpawner(RoadNetwork network, Router router, DriverFactory drivers, Random random)
    {
        _network = network;
        _router = router;
        _drivers = drivers;
        _random = random;
    }

    public int Backlog => (int)_accumulated.Values.Sum(v => MathF.Floor(v));

    public IEnumerable<Vehicle> Spawn(float deltaTime, float rate, float cyclistShare)
    {
        var gateways = _network.Gateways.ToList();
        var totalWeight = gateways.Sum(g => g.SpawnWeight);
        if (totalWeight <= 0f)
            yield break;

        foreach (var gateway in gateways)
        {
            _accumulated.TryGetValue(gateway, out var pending);
            pending = Math.Min(pending + rate * gateway.SpawnWeight / totalWeight * deltaTime, MaxBacklog);
            if (pending >= 1f && TryCreate(gateway, gateways, totalWeight, cyclistShare) is { } vehicle)
            {
                pending -= 1f;
                yield return vehicle;
            }

            _accumulated[gateway] = pending;
        }
    }

    private Vehicle? TryCreate(Junction origin, IReadOnlyList<Junction> gateways, float totalWeight, float cyclistShare)
    {
        var destination = PickDestination(origin, gateways, totalWeight - origin.SpawnWeight);
        if (destination is null)
            return null;

        var lane = _router.ChooseEntryLane(origin, destination);
        if (lane is null)
            return null;

        var type = VehicleType.Pick(_random, cyclistShare);
        var cruise = Math.Min(lane.SpeedLimit * 0.85f, type.MaxSpeed);
        var speed = lane.Vehicles.Count == 0 ? cruise : Math.Min(cruise, lane.Vehicles[^1].Speed);
        if (lane.FreeSpaceAtStart() < SafeEntryGap + speed * EntryHeadway)
            return null;

        var slowdown = Math.Max(1f, lane.SpeedLimit / type.MaxSpeed);
        var expected = _router.FreeFlowTime(lane, destination) * 1.15f * slowdown + 8f;
        var vehicle = new Vehicle(++_nextId, type, _drivers.Create(), origin, destination, expected)
        {
            Speed = speed
        };
        vehicle.EnterPath(lane, 0f);
        return vehicle;
    }

    private Junction? PickDestination(Junction origin, IReadOnlyList<Junction> gateways, float weight)
    {
        var roll = _random.NextSingle() * weight;
        foreach (var gateway in gateways)
        {
            if (gateway == origin)
                continue;

            roll -= gateway.SpawnWeight;
            if (roll <= 0f)
                return gateway;
        }

        return gateways.LastOrDefault(g => g != origin);
    }
}
