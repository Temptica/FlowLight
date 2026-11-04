using System;
using System.Linq;
using FlowLight.Core.Control;
using FlowLight.Core.Network;
using FlowLight.Core.Vehicles;

namespace FlowLight.Core.Simulation;

internal sealed class RightOfWayArbiter
{
    private const float FreshRedWindow = 3f;
    private const float ClearingMargin = 3f;
    private const float MovingExitSpeed = 3f;

    private readonly Action<Junction, Vehicle, ViolationKind> _reportViolation;

    public RightOfWayArbiter(Action<Junction, Vehicle, ViolationKind> reportViolation)
    {
        _reportViolation = reportViolation;
    }

    public bool TryGrant(Vehicle vehicle, Connector connector, float distanceToStopLine)
    {
        var junction = connector.Junction;
        var decision = junction.Control.Evaluate(vehicle, connector, distanceToStopLine);
        var reckless = false;
        ViolationKind? violation = null;

        if (junction.Control.AspectFor(connector.From.Road) == SignalAspect.Stop && !vehicle.HasStoppedAtLine)
        {
            if (!RollsThroughStop(vehicle, junction))
                return Deny(vehicle, WaitReason.StopSign);
            violation = ViolationKind.RolledStopSign;
        }

        switch (decision.Verdict)
        {
            case EntryVerdict.Stop:
                if (!RunsRedLight(vehicle, junction, connector, decision.SinceRed))
                    return Deny(vehicle, WaitReason.RedLight);
                reckless = true;
                violation = ViolationKind.RanRedLight;
                break;
            case EntryVerdict.Yield when vehicle.WaitAtStopLine >= vehicle.Driver.Patience:
                break;
            case EntryVerdict.Yield when MakesMistake(vehicle, junction, connector):
                reckless = true;
                break;
            case EntryVerdict.Yield when ForcesPriority(vehicle, junction):
                reckless = true;
                violation = ViolationKind.IgnoredPriority;
                break;
            case EntryVerdict.Yield:
                return Deny(vehicle, WaitReason.GivingWay);
        }

        if (PedestriansCrossing(connector))
            return Deny(vehicle, WaitReason.Pedestrians);
        if (IsBlocked(vehicle, connector, onlyOccupants: reckless))
            return Deny(vehicle, WaitReason.JunctionBusy);
        if (!HasExitSpace(vehicle, connector))
            return Deny(vehicle, WaitReason.ExitBlocked);

        junction.Reservations[vehicle] = connector;
        vehicle.HasClearance = true;
        vehicle.IsViolating = reckless;
        vehicle.WaitReason = WaitReason.None;
        if (violation is { } kind)
        {
            vehicle.ViolationsCommitted++;
            _reportViolation(junction, vehicle, kind);
        }

        return true;
    }

    private static bool PedestriansCrossing(Connector connector) =>
        connector.Junction.CrosswalkFor(connector.From.Road)?.IsOccupiedOnSide(incoming: true) == true
        || connector.Junction.CrosswalkFor(connector.To.Road)?.IsOccupiedOnSide(incoming: false) == true;

    private static bool Deny(Vehicle vehicle, WaitReason reason)
    {
        vehicle.WaitReason = reason;
        return false;
    }

    public static Vehicle? ConflictingOccupant(Vehicle vehicle, Connector connector)
    {
        foreach (var (other, theirs) in connector.Junction.Reservations)
        {
            if (other == vehicle || other.Path != theirs || theirs.From == connector.From)
                continue;
            if (!connector.ConflictsWith(theirs) || theirs.HasCleared(other, connector))
                continue;
            if (other.IsViolating || other.Status == VehicleStatus.Crashed)
                return other;
        }

        return null;
    }

    public static float Danger(Junction junction, Connector connector)
    {
        var control = junction.Control.AspectFor(connector.From.Road) switch
        {
            SignalAspect.None => 1f,
            SignalAspect.Stop => 0.4f,
            SignalAspect.Yield or SignalAspect.PriorityRoad => 0.6f,
            _ => 0.25f
        };
        var speed = connector.From.Road.SpeedLimitKmh / 50f;
        var complexity = junction.Approaches.Count / 4f + (connector.From.Road.LaneCount(connector.From.From) > 1 ? 0.25f : 0f);
        return control * speed * speed * complexity;
    }

    private static bool RunsRedLight(Vehicle vehicle, Junction junction, Connector connector, float sinceRed)
    {
        if (vehicle.MistakeRoll < MistakeChance(vehicle, junction, connector) * 0.1f)
            return true;
        if (sinceRed > FreshRedWindow)
            return false;

        return vehicle.ViolationRoll < (1f - vehicle.Driver.Compliance) * 0.35f * junction.Equipment.Enforcement;
    }

    private static bool RollsThroughStop(Vehicle vehicle, Junction junction) =>
        vehicle.ViolationRoll < (1f - vehicle.Driver.Compliance) * 0.5f * junction.Equipment.Enforcement;

    private static bool ForcesPriority(Vehicle vehicle, Junction junction) =>
        vehicle.ViolationRoll < (1f - vehicle.Driver.Compliance) * 0.3f * junction.Equipment.Enforcement;

    private static bool MakesMistake(Vehicle vehicle, Junction junction, Connector connector) =>
        vehicle.MistakeRoll < MistakeChance(vehicle, junction, connector);

    private static float MistakeChance(Vehicle vehicle, Junction junction, Connector connector) =>
        (1f - vehicle.Driver.Attentiveness) * Danger(junction, connector) * 0.6f;

    private static bool IsBlocked(Vehicle vehicle, Connector connector, bool onlyOccupants)
    {
        foreach (var (other, theirs) in connector.Junction.Reservations)
        {
            if (other == vehicle || theirs.From == connector.From || !connector.ConflictsWith(theirs))
                continue;
            if (onlyOccupants && other.Path != theirs)
                continue;
            if (theirs.HasCleared(other, connector))
                continue;
            if (other.Path != theirs && PriorityRules.TimeToArrival(other) > vehicle.Driver.GapAcceptance + ClearingMargin)
                continue;

            return true;
        }

        return false;
    }

    private static bool HasExitSpace(Vehicle vehicle, Connector connector)
    {
        var target = connector.To;
        if (target.Vehicles.Count > 0 && target.Vehicles[^1].Speed > MovingExitSpeed)
            return true;

        var claimed = connector.Junction.Reservations
            .Where(r => r.Key != vehicle && r.Value.To == target)
            .Sum(r => r.Key.Type.Length + 2f);
        return target.FreeSpaceAtStart() - claimed >= vehicle.Type.Length + 2f;
    }
}
