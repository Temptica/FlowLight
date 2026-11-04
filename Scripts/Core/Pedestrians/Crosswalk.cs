using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FlowLight.Core.Network;

namespace FlowLight.Core.Pedestrians;

public sealed class Crosswalk
{
    public const float Width = 3f;
    private const float SideMargin = 1.5f;

    public Crosswalk(Junction junction, Road road, Vector2 start, Vector2 end)
    {
        Junction = junction;
        Road = road;
        Start = start;
        End = end;
    }

    public Junction Junction { get; }
    public Road Road { get; }
    public Vector2 Start { get; }
    public Vector2 End { get; }
    public float Length => Vector2.Distance(Start, End);
    public Vector2 Across => Vector2.Normalize(End - Start);
    public Vector2 Center => (Start + End) * 0.5f;
    public bool HasZebra => Junction.HasZebra(Road);
    public List<Pedestrian> Pedestrians { get; } = new();

    public bool IsOccupied => Pedestrians.Any(p => p.State == PedestrianState.Crossing);
    public bool IsOccupiedOnSide(bool incoming)
    {
        if (Road.IsOneWay)
            return IsOccupied;

        var middle = Length * 0.5f;
        foreach (var pedestrian in Pedestrians)
        {
            if (pedestrian.State != PedestrianState.Crossing)
                continue;

            var fromStart = pedestrian.FromStart ? pedestrian.Progress : Length - pedestrian.Progress;
            if (incoming ? fromStart > middle - SideMargin : fromStart < middle + SideMargin)
                return true;
        }

        return false;
    }

    public int Waiting => Pedestrians.Count(p => p.State == PedestrianState.Waiting);

    public IEnumerable<Lane> IncomingLanes => Road.LanesInto(Junction);
}
