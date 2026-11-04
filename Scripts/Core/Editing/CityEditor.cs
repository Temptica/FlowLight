using System;
using System.Linq;
using FlowLight.Core.Campaign;
using FlowLight.Core.Control;
using FlowLight.Core.Economy;
using FlowLight.Core.Network;
using FlowLight.Core.Simulation;

namespace FlowLight.Core.Editing;

public readonly record struct EditResult(bool Success, string Message)
{
    public static EditResult Done(string message) => new(true, message);
    public static EditResult Refused(string message) => new(false, message);
}

public sealed class CityEditor
{
    public const int SpeedStepKmh = 10;
    public const int MinSpeedKmh = 20;
    public const int MaxSpeedKmh = 90;
    public const float GreenStep = 5f;

    private readonly RoadNetwork _network;
    private readonly TrafficSimulation _simulation;
    private readonly Treasury _treasury;
    private readonly CampaignProgress _campaign;

    public CityEditor(RoadNetwork network, TrafficSimulation simulation, Treasury treasury, CampaignProgress campaign)
    {
        _network = network;
        _simulation = simulation;
        _treasury = treasury;
        _campaign = campaign;
    }

    public event Action? Changed;

    public bool CanEdit(Junction junction) => _campaign.IsPlayable(junction);

    public bool CanEdit(Road road) => CanEdit(road.A) || CanEdit(road.B);

    public EditResult SetSign(Junction junction, Road road, ApproachSign sign)
    {
        if (Check(ToolKind.Signs, junction) is { } refused)
            return refused;
        if (junction.HasSignal)
            return EditResult.Refused("This junction has a traffic light. Remove it first to use signs.");
        if (junction.SignFor(road) == sign)
            return EditResult.Refused("That sign is already there.");

        return Purchase(ToolCatalog.SignCost, $"{SignName(sign)} on {road.Name}", () => junction.SetSign(road, sign));
    }

    public EditResult ToggleZebra(Junction junction, Road road)
    {
        if (Check(ToolKind.Crossings, junction) is { } refused)
            return refused;
        if (junction.CrosswalkFor(road) is null)
            return EditResult.Refused("Zebras go where walkers cross, at junctions of three or more roads.");

        var adding = !junction.HasZebra(road);
        return Purchase(adding ? ToolCatalog.ZebraCost : 0, adding ? $"Zebra on {road.Name}" : $"Removed the zebra on {road.Name}", () => junction.SetZebra(road, adding));
    }

    public EditResult ToggleLaneConnection(Lane incoming, Lane outgoing)
    {
        var junction = incoming.To;
        if (Check(ToolKind.LaneArrows, junction) is { } refused)
            return refused;
        if (outgoing.From != junction)
            return EditResult.Refused("That lane does not leave this junction.");

        var targets = incoming.Connectors.Select(c => c.To.Key).ToHashSet();
        var allowing = !targets.Remove(outgoing.Key);
        if (allowing)
            targets.Add(outgoing.Key);
        if (targets.Count == 0)
            return EditResult.Refused("Every lane needs at least one way to go.");

        var verb = allowing ? "Allowed" : "Removed";
        return Purchase(ToolCatalog.LaneArrowCost, $"{verb} {incoming.Road.Name} → {outgoing.Road.Name}", () =>
        {
            junction.SetConnections(incoming.Key, targets);
            _network.Reconnect(junction);
        });
    }

    public EditResult ChangeLanes(Road road, Junction towards, int delta)
    {
        if (Check(ToolKind.TurnLanes, towards) is { } refused)
            return refused;

        var origin = road.Other(towards);
        if (!road.AllowsTravelFrom(origin))
            return EditResult.Refused("Traffic on this road does not drive towards the junction.");

        var current = road.LaneCount(origin);
        var target = Math.Clamp(current + delta, 1, Road.MaxLanesPerDirection);
        if (target == current)
            return EditResult.Refused(delta > 0 ? $"A road holds at most {Road.MaxLanesPerDirection} lanes per direction." : "A road needs at least one lane.");

        var cost = delta > 0 ? ToolCatalog.AddLaneCost : ToolCatalog.RemoveLaneCost;
        return Purchase(cost, $"{road.Name} now has {target} lane(s) into {towards.Name}", () =>
        {
            road.SetLaneCount(origin, target);
            _network.RebuildAround(road);
        });
    }

    public EditResult ToggleSignal(Junction junction)
    {
        if (Check(ToolKind.TrafficLight, junction) is { } refused)
            return refused;
        if (!junction.IsIntersection)
            return EditResult.Refused("Traffic lights need a junction of three or more roads.");

        return junction.HasSignal
            ? Purchase(ToolCatalog.RemoveLightCost, $"Removed the light at {junction.Name}", () => junction.Control = new SignControl())
            : Purchase(ToolCatalog.TrafficLightCost, $"Traffic light at {junction.Name}", () => junction.Control = new FixedSignalControl());
    }

    public EditResult AdjustGreen(Junction junction, int phaseIndex, int direction)
    {
        if (!CanEdit(junction))
            return EditResult.Refused("Finish the current junction to edit this one.");
        if (junction.Control is not SignalControl signal || phaseIndex < 0 || phaseIndex >= signal.Phases.Count)
            return EditResult.Refused("There is no light to time here.");

        var phase = signal.Phases[phaseIndex];
        phase.GreenTime += Math.Sign(direction) * GreenStep;
        Changed?.Invoke();
        return EditResult.Done($"Phase {phaseIndex + 1} green: {phase.GreenTime:0}s");
    }

    public EditResult ToggleMovement(Junction junction, int phaseIndex, Movement movement)
    {
        if (CheckPhases(junction) is { } refused)
            return refused;

        var signal = (SignalControl)junction.Control;
        if (phaseIndex < 0 || phaseIndex >= signal.Phases.Count)
            return EditResult.Refused("That phase does not exist.");

        var added = signal.ToggleMovement(phaseIndex, movement);
        Changed?.Invoke();
        var turn = movement.Turn.ToString().ToLowerInvariant();
        return EditResult.Done(added
            ? $"Phase {phaseIndex + 1}: {movement.Road.Name} {turn} now gets green"
            : $"Phase {phaseIndex + 1}: {movement.Road.Name} {turn} stays red{(signal.IsServed(movement) ? "" : ". It never gets green now!")}");
    }

    public EditResult AddPhase(Junction junction)
    {
        if (CheckPhases(junction) is { } refused)
            return refused;

        var signal = (SignalControl)junction.Control;
        if (signal.Phases.Count >= SignalControl.MaxPhases)
            return EditResult.Refused($"A light can have at most {SignalControl.MaxPhases} phases.");

        signal.AddPhase();
        Changed?.Invoke();
        return EditResult.Done($"Added phase {signal.Phases.Count}. Pick which directions get green in it.");
    }

    public EditResult RemovePhase(Junction junction, int phaseIndex)
    {
        if (CheckPhases(junction) is { } refused)
            return refused;

        var signal = (SignalControl)junction.Control;
        if (signal.Phases.Count <= 1 || phaseIndex < 0 || phaseIndex >= signal.Phases.Count)
            return EditResult.Refused("A light needs at least one phase.");

        signal.RemovePhase(phaseIndex);
        Changed?.Invoke();
        return EditResult.Done($"Removed phase {phaseIndex + 1}");
    }

    private EditResult? CheckPhases(Junction junction)
    {
        if (Check(ToolKind.TurnPhases, junction) is { } refused)
            return refused;
        return junction.Control is SignalControl ? null : EditResult.Refused("Install a traffic light first.");
    }

    public EditResult ChangeSpeed(Road road, int direction)
    {
        if (Check(ToolKind.SpeedLimit, road) is { } refused)
            return refused;

        var target = Math.Clamp(road.SpeedLimitKmh + Math.Sign(direction) * SpeedStepKmh, MinSpeedKmh, MaxSpeedKmh);
        if (target == road.SpeedLimitKmh)
            return EditResult.Refused($"Speed limits go from {MinSpeedKmh} to {MaxSpeedKmh} km/h.");

        return Purchase(ToolCatalog.SpeedLimitCost, $"{road.Name}: {target} km/h", () => road.SpeedLimitKmh = target);
    }

    public EditResult CycleDirection(Road road)
    {
        if (Check(ToolKind.OneWay, road) is { } refused)
            return refused;
        if (road.A.IsGateway || road.B.IsGateway)
            return EditResult.Refused("Roads that leave the area stay two-way.");

        var next = road.Direction switch
        {
            TravelDirection.TwoWay => TravelDirection.Forward,
            TravelDirection.Forward => TravelDirection.Backward,
            _ => TravelDirection.TwoWay
        };
        var label = next == TravelDirection.TwoWay ? "two-way" : $"one-way towards {(next == TravelDirection.Forward ? road.B : road.A).Name}";
        return Purchase(ToolCatalog.OneWayCost, $"{road.Name} is now {label}", () =>
        {
            road.Direction = next;
            _network.RebuildAround(road);
        });
    }

    public EditResult BuildDispatch()
    {
        if (!_campaign.IsUnlocked(ToolKind.Dispatch))
            return Locked(ToolKind.Dispatch);
        if (_simulation.Services.HasDispatch)
            return EditResult.Refused("The city already has a dispatch.");

        return Purchase(ToolCatalog.DispatchCost, "Tow-truck dispatch built", () => _simulation.Services.HasDispatch = true);
    }

    public EditResult InstallDetectors(Junction junction)
    {
        if (Check(ToolKind.Detectors, junction) is { } refused)
            return refused;
        if (junction.Equipment.HasDetectors)
            return EditResult.Refused("Detectors are already installed here.");

        return Purchase(ToolCatalog.DetectorCost, $"Detectors at {junction.Name}", () => junction.Equipment.HasDetectors = true);
    }

    public EditResult UpgradeLight(Junction junction)
    {
        if (Check(ToolKind.SmartLight, junction) is { } refused)
            return refused;
        if (junction.Control is not SignalControl current)
            return EditResult.Refused("Install a traffic light first.");
        if (!junction.Equipment.HasDetectors)
            return EditResult.Refused("Smart lights need detectors.");

        (SignalControl next, int cost)? upgrade = current.Kind switch
        {
            ControlKind.FixedSignal => (new ActuatedSignalControl(), ToolCatalog.SmartLightCost),
            ControlKind.ActuatedSignal => (new AdaptiveSignalControl(), ToolCatalog.AdaptiveLightCost),
            _ => null
        };
        if (upgrade is not { } chosen)
            return EditResult.Refused("This light is already fully automatic.");

        return Purchase(chosen.cost, $"{junction.Name}: {chosen.next.DisplayName}", () =>
        {
            junction.Control = chosen.next;
            chosen.next.CopyPlanFrom(current);
        });
    }

    public EditResult InstallCamera(Junction junction)
    {
        if (Check(ToolKind.Camera, junction) is { } refused)
            return refused;
        if (junction.Equipment.HasCamera)
            return EditResult.Refused("There is already a camera here.");

        return Purchase(ToolCatalog.CameraCost, $"Camera at {junction.Name}", () => junction.Equipment.HasCamera = true);
    }

    private EditResult? Check(ToolKind tool, Junction junction)
    {
        if (!_campaign.IsUnlocked(tool))
            return Locked(tool);
        return CanEdit(junction) ? null : EditResult.Refused("Finish the current junction to edit this one.");
    }

    private EditResult? Check(ToolKind tool, Road road)
    {
        if (!_campaign.IsUnlocked(tool))
            return Locked(tool);
        return CanEdit(road) ? null : EditResult.Refused("Only roads at your junctions can be changed.");
    }

    private static EditResult Locked(ToolKind tool) =>
        EditResult.Refused($"{ToolCatalog.Get(tool).Title} unlocks in a later level.");

    private EditResult Purchase(int cost, string description, Action apply)
    {
        if (!_treasury.TrySpend(cost, description))
            return EditResult.Refused($"Not enough money: this costs €{cost}.");

        apply();
        Changed?.Invoke();
        return EditResult.Done(description);
    }

    private static string SignName(ApproachSign sign) => sign switch
    {
        ApproachSign.Priority => "Priority road",
        ApproachSign.Yield => "Yield sign",
        ApproachSign.Stop => "Stop sign",
        _ => "No sign"
    };
}
