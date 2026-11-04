using System;
using FlowLight.Core.Network;
using FlowLight.Core.Vehicles;

namespace FlowLight.Core.Control;

public delegate bool PriorityPredicate(Approach mine, Approach other, Connector theirs);

public static class PriorityRules
{
    private const float ConsideredRange = 70f;

    public static PriorityPredicate RightHandRule(Connector mine) =>
        (myApproach, other, theirs) => other.IsToTheRightOf(myApproach) || OncomingHasPriority(mine, myApproach, other, theirs);

    public static bool OncomingHasPriority(Connector mine, Approach myApproach, Approach other, Connector theirs) =>
        mine.Turn == TurnDirection.Left && other.IsOpposite(myApproach) && theirs.Turn != TurnDirection.Left;

    public static bool MustYield(Vehicle vehicle, Connector connector, Junction junction, PriorityPredicate otherHasPriority)
    {
        var mine = junction.ApproachFor(connector.From.Road);
        if (mine is null)
            return false;

        foreach (var other in junction.Approaches)
        foreach (var lane in other.Lanes)
        {
            if (lane == connector.From)
                continue;

            var rival = FrontWaitingVehicle(lane);
            if (rival?.PlannedConnector is not { } theirs || rival == vehicle)
                continue;
            if (!connector.ConflictsWith(theirs) || !otherHasPriority(mine, other, theirs))
                continue;
            if (TimeToArrival(rival) < vehicle.Driver.GapAcceptance)
                return true;
        }

        return false;
    }

    public static Vehicle? FrontWaitingVehicle(Lane lane)
    {
        foreach (var vehicle in lane.Vehicles)
        {
            if (vehicle.HasClearance)
                continue;

            return vehicle.RemainingOnPath <= ConsideredRange ? vehicle : null;
        }

        return null;
    }

    public static float TimeToArrival(Vehicle vehicle)
    {
        var remaining = vehicle.RemainingOnPath;
        if (remaining < 8f)
            return 0f;

        return remaining / Math.Max(vehicle.Speed, 1f);
    }
}
