using System;
using System.Collections.Generic;
using System.Linq;
using FlowLight.Core.Drivers;
using FlowLight.Core.Network;
using FlowLight.Core.Pedestrians;
using FlowLight.Core.Routing;
using FlowLight.Core.Vehicles;

namespace FlowLight.Core.Simulation;

public sealed class TrafficSimulation
{
    private const float LookAheadDistance = 120f;
    private const float StopLineMargin = 1f;
    private const float StopLineWaitZone = 12f;
    private const float SpeedingTolerance = 1.12f;

    private readonly SimulationSettings _settings;
    private DemandProfile _demand;
    private float _demandTime;
    private readonly Random _random;
    private readonly Router _router;
    private readonly VehicleSpawner _spawner;
    private readonly RightOfWayArbiter _arbiter;
    private readonly List<Vehicle> _vehicles = new();
    private readonly Dictionary<Vehicle, float> _accelerations = new();
    private readonly List<Vehicle> _departed = new();
    private float _routeRefreshTimer;

    public TrafficSimulation(RoadNetwork network, DemandProfile demand, SimulationSettings settings)
    {
        Network = network;
        _demand = demand;
        _settings = settings;
        _random = new Random(settings.Seed);
        _router = new Router(network);
        _spawner = new VehicleSpawner(network, _router, new DriverFactory(_random), _random);
        _arbiter = new RightOfWayArbiter(ReportViolation);
        Pedestrians = new PedestrianTraffic(_random);

        network.TopologyChanged += OnTopologyChanged;
        _router.RebuildTopology();
    }

    public RoadNetwork Network { get; }
    public IReadOnlyList<Vehicle> Vehicles => _vehicles;
    public float Time { get; private set; }
    public float CurrentDemand => _demand.RateAt(_demandTime);
    public float DemandTime => _demandTime;
    public DemandProfile Demand => _demand;
    public CityServices Services { get; } = new();
    public PedestrianTraffic Pedestrians { get; }

    public void SetDemand(DemandProfile demand)
    {
        _demand = demand;
        _demandTime = 0f;
        Pedestrians.RatePerSecond = demand.PedestrianRate;
    }
    public int Backlog => _spawner.Backlog;
    public int CompletedTrips { get; private set; }
    public int Crashes { get; private set; }

    public event Action<Vehicle>? VehicleArrived;
    public event Action<CrashReport>? CrashOccurred;
    public event Action<ViolationReport>? ViolationCommitted;

    public void Step(float deltaTime)
    {
        Time += deltaTime;
        _demandTime += deltaTime;
        foreach (var junction in Network.Junctions)
            junction.Control.Update(deltaTime);
        foreach (var junction in Network.ActiveJunctions)
            junction.Statistics.Tick(deltaTime, LongestWait(junction));
        Pedestrians.Update(deltaTime, Network);

        _accelerations.Clear();
        foreach (var vehicle in _vehicles)
        {
            if (vehicle.Status == VehicleStatus.Driving)
                _accelerations[vehicle] = ComputeAcceleration(vehicle, deltaTime);
        }

        foreach (var vehicle in _vehicles)
        {
            if (vehicle.Status == VehicleStatus.Crashed)
            {
                UpdateCrashed(vehicle, deltaTime);
                continue;
            }

            vehicle.Advance(deltaTime, _accelerations[vehicle]);
            TrackApproachDelay(vehicle, deltaTime);
            HandleTransitions(vehicle);
            if (vehicle.Status == VehicleStatus.Driving)
                vehicle.UpdatePose();
        }

        foreach (var crash in CollisionDetector.Detect(Network.Junctions).ToList())
            RegisterCrash(crash);

        foreach (var vehicle in _departed)
            _vehicles.Remove(vehicle);
        _departed.Clear();

        foreach (var vehicle in _spawner.Spawn(deltaTime, CurrentDemand, _demand.CyclistShare))
        {
            _vehicles.Add(vehicle);
            OnLaneEntered(vehicle, (Lane)vehicle.Path);
        }

        _routeRefreshTimer += deltaTime;
        if (_routeRefreshTimer >= _settings.RouteRefreshInterval)
        {
            _routeRefreshTimer = 0f;
            _router.RefreshCongestion();
        }
    }

    private float ComputeAcceleration(Vehicle vehicle, float deltaTime)
    {
        var desired = DesiredSpeed(vehicle, vehicle.Path);
        var acceleration = DrivingModel.FreeRoad(vehicle, desired);
        if (FindLeader(vehicle) is var (leader, gap) && leader is not null)
            acceleration = Math.Min(acceleration, DrivingModel.Following(vehicle, desired, gap, leader.Speed));

        if (vehicle.Path is Lane lane && !lane.To.IsGateway)
            acceleration = Math.Min(acceleration, ApproachJunction(vehicle, lane, desired, deltaTime));

        return DrivingModel.Clamp(acceleration);
    }

    private float ApproachJunction(Vehicle vehicle, Lane lane, float desired, float deltaTime)
    {
        var remaining = vehicle.RemainingOnPath;
        if (vehicle.PlannedConnector is not { IsRemoved: false } connector)
        {
            PlanConnector(vehicle, lane);
            return vehicle.PlannedConnector is null ? DrivingModel.Following(vehicle, desired, remaining - StopLineMargin, 0f) : float.MaxValue;
        }

        var acceleration = DrivingModel.ApproachSpeed(vehicle, DesiredSpeed(vehicle, connector), remaining);

        if (!vehicle.HasClearance)
        {
            var decisionDistance = DrivingModel.StoppingDistance(vehicle, vehicle.Type.ComfortableDeceleration)
                                   + vehicle.Speed * vehicle.Driver.TimeHeadway + 10f;
            if (remaining <= decisionDistance && IsFirstInLine(vehicle, lane))
                _arbiter.TryGrant(vehicle, connector, remaining);
        }

        if (!vehicle.HasClearance)
        {
            if (vehicle.IsStopped && remaining <= StopLineWaitZone)
            {
                vehicle.WaitAtStopLine += deltaTime;
                vehicle.HasStoppedAtLine = true;
            }
            return Math.Min(acceleration, DrivingModel.Following(vehicle, desired, remaining - StopLineMargin, 0f));
        }

        if (!vehicle.IsViolating && RightOfWayArbiter.ConflictingOccupant(vehicle, connector) is not null)
            acceleration = Math.Min(acceleration, AvoidIntruder(vehicle, connector, remaining));

        return acceleration;
    }

    private static bool IsFirstInLine(Vehicle vehicle, Lane lane)
    {
        var index = lane.Vehicles.IndexOf(vehicle);
        return index == 0 || lane.Vehicles[index - 1].HasClearance;
    }

    private float AvoidIntruder(Vehicle vehicle, Connector connector, float remaining)
    {
        if (DrivingModel.StoppingDistance(vehicle, DrivingModel.EmergencyDeceleration) > remaining || vehicle.MistakeRoll > vehicle.Driver.Attentiveness)
            return float.MaxValue;

        if (!vehicle.HasHadNearMiss && vehicle.Speed > 3f)
        {
            vehicle.HasHadNearMiss = true;
            vehicle.NearMisses++;
            connector.Junction.Statistics.RecordNearMiss();
        }

        return -(vehicle.Speed * vehicle.Speed) / (2f * Math.Max(remaining - StopLineMargin, 0.3f));
    }

    private (Vehicle? Leader, float Gap) FindLeader(Vehicle vehicle)
    {
        var vehicles = vehicle.Path.Vehicles;
        var index = vehicles.IndexOf(vehicle);
        if (index > 0)
        {
            var ahead = vehicles[index - 1];
            return (ahead, ahead.Distance - ahead.Type.Length - vehicle.Distance);
        }

        var travelled = vehicle.RemainingOnPath;
        var next = NextPath(vehicle, vehicle.Path);
        while (next is not null && travelled < LookAheadDistance)
        {
            if (next.Vehicles.Count > 0)
            {
                var last = next.Vehicles[^1];
                return (last, travelled + last.Distance - last.Type.Length);
            }

            travelled += next.Length;
            next = next is Connector connector ? connector.To : null;
        }

        return (null, float.PositiveInfinity);
    }

    private static ITrafficPath? NextPath(Vehicle vehicle, ITrafficPath path) => path switch
    {
        Lane when vehicle.HasClearance => vehicle.PlannedConnector,
        Connector connector => connector.To,
        _ => null
    };

    private float DesiredSpeed(Vehicle vehicle, ITrafficPath path)
    {
        var factor = path is Connector ? Math.Min(vehicle.Driver.SpeedFactor, 1.05f) : vehicle.Driver.SpeedFactor;
        return Math.Min(path.SpeedLimit * factor, vehicle.Type.MaxSpeed * vehicle.Driver.SpeedFactor);
    }

    private static void TrackApproachDelay(Vehicle vehicle, float deltaTime)
    {
        if (vehicle.Path is not Lane lane || !lane.To.IsActive)
            return;

        var desired = Math.Min(lane.SpeedLimit, vehicle.Type.MaxSpeed) * vehicle.Driver.SpeedFactor;
        vehicle.ApproachDelay += deltaTime * Math.Clamp(1f - vehicle.Speed / desired, 0f, 1f);
    }

    private void HandleTransitions(Vehicle vehicle)
    {
        while (vehicle.Status == VehicleStatus.Driving && vehicle.Distance >= vehicle.Path.Length)
        {
            var overflow = vehicle.Distance - vehicle.Path.Length;
            switch (vehicle.Path)
            {
                case Lane lane when lane.To.IsGateway || lane.Connectors.Count == 0:
                    Arrive(vehicle);
                    return;
                case Lane lane when vehicle.HasClearance && vehicle.PlannedConnector is { IsRemoved: false } connector:
                    EnterJunction(vehicle, connector, overflow);
                    break;
                case Lane lane:
                    vehicle.Distance = lane.Length;
                    vehicle.Speed = 0f;
                    return;
                case Connector connector:
                    connector.Junction.Reservations.Remove(vehicle);
                    vehicle.LeavePath();
                    vehicle.EnterPath(connector.To, overflow);
                    OnLaneEntered(vehicle, connector.To);
                    break;
            }
        }
    }

    private void EnterJunction(Vehicle vehicle, Connector connector, float overflow)
    {
        var junction = connector.Junction;
        junction.Statistics.RecordPassage(vehicle.ApproachDelay);
        if (vehicle.Speed > connector.From.SpeedLimit * SpeedingTolerance)
            ReportViolation(junction, vehicle, ViolationKind.Speeding);

        vehicle.LeavePath();
        vehicle.EnterPath(connector, overflow);
    }

    private void OnLaneEntered(Vehicle vehicle, Lane lane)
    {
        vehicle.ResetApproachState(_random.NextSingle(), _random.NextSingle());
        PlanConnector(vehicle, lane);
    }

    private void PlanConnector(Vehicle vehicle, Lane lane)
    {
        if (!lane.To.IsGateway)
            vehicle.PlannedConnector = _router.ChooseConnector(lane, vehicle.Destination, _random, _settings.RouteNoise);
    }

    private void Arrive(Vehicle vehicle)
    {
        Depart(vehicle);
        vehicle.Status = VehicleStatus.Finished;
        CompletedTrips++;
        VehicleArrived?.Invoke(vehicle);
    }

    private void Depart(Vehicle vehicle)
    {
        vehicle.LeavePath();
        if (vehicle.PlannedConnector is { } planned)
            planned.Junction.Reservations.Remove(vehicle);
        if (vehicle.Path is Connector connector)
            connector.Junction.Reservations.Remove(vehicle);
        _departed.Add(vehicle);
    }

    private void UpdateCrashed(Vehicle vehicle, float deltaTime)
    {
        vehicle.CrashTimer -= deltaTime;
        if (vehicle.CrashTimer > 0f)
            return;

        vehicle.Status = VehicleStatus.Finished;
        Depart(vehicle);
    }

    private void RegisterCrash(CrashReport crash)
    {
        foreach (var vehicle in new[] { crash.First, crash.Second })
        {
            if (vehicle.Status == VehicleStatus.Crashed)
                continue;

            vehicle.Status = VehicleStatus.Crashed;
            vehicle.Speed = 0f;
            vehicle.CrashTimer = Services.CrashClearTime;
        }

        Crashes++;
        crash.Junction.Statistics.RecordCrash();
        CrashOccurred?.Invoke(crash);
    }

    private void ReportViolation(Junction junction, Vehicle vehicle, ViolationKind kind)
    {
        junction.Statistics.RecordViolation();
        var caught = junction.Equipment.HasCamera && _random.NextSingle() < _settings.CameraCatchChance;
        ViolationCommitted?.Invoke(new ViolationReport(junction, vehicle, kind, caught));
    }

    private void RetargetStrandedVehicles()
    {
        var gateways = Network.Gateways.ToList();
        if (gateways.Count == 0)
            return;

        foreach (var vehicle in _vehicles.Where(v => !v.Destination.IsGateway))
        {
            vehicle.Destination = gateways[_random.Next(gateways.Count)];
            if (vehicle.Path is Lane lane && !vehicle.HasClearance)
                PlanConnector(vehicle, lane);
        }
    }

    private static float LongestWait(Junction junction)
    {
        var longest = 0f;
        foreach (var approach in junction.Approaches)
        foreach (var lane in approach.Lanes)
        {
            if (lane.Vehicles.Count > 0)
                longest = Math.Max(longest, lane.Vehicles[0].WaitAtStopLine);
        }

        return longest;
    }

    private void OnTopologyChanged()
    {
        foreach (var junction in Network.Junctions)
        {
            foreach (var stale in junction.Reservations.Where(r => r.Value.IsRemoved).Select(r => r.Key).ToList())
                junction.Reservations.Remove(stale);
        }

        foreach (var vehicle in _vehicles)
        {
            if (vehicle.Path.IsRemoved)
            {
                vehicle.Status = VehicleStatus.Finished;
                Depart(vehicle);
            }
            else if (vehicle.PlannedConnector is { IsRemoved: true })
            {
                vehicle.ResetApproachState(vehicle.ViolationRoll, vehicle.MistakeRoll);
            }
        }

        foreach (var vehicle in _departed)
            _vehicles.Remove(vehicle);
        _departed.Clear();

        _router.RebuildTopology();
        Pedestrians.Rebuild(Network);
        RetargetStrandedVehicles();
    }
}
