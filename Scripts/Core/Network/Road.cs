using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace FlowLight.Core.Network;

public sealed class Road
{
    public const int MaxLanesPerDirection = 3;

    private int _lanesForward;
    private int _lanesBackward;

    public Road(int id, string name, Junction a, Junction b, RoadClass roadClass, int lanesForward, int lanesBackward, int speedLimitKmh, TravelDirection direction)
    {
        Id = id;
        Name = name;
        A = a;
        B = b;
        Class = roadClass;
        _lanesForward = lanesForward;
        _lanesBackward = lanesBackward;
        SpeedLimitKmh = speedLimitKmh;
        Direction = direction;
    }

    public int Id { get; }
    public string Name { get; }
    public Junction A { get; }
    public Junction B { get; }
    public RoadClass Class { get; }
    public int SpeedLimitKmh { get; internal set; }
    public TravelDirection Direction { get; internal set; }
    public List<Lane> Lanes { get; } = new();

    public float SpeedLimit => SpeedLimitKmh / 3.6f;
    public float Length => Vector2.Distance(A.Position, B.Position);
    public bool IsOneWay => Direction != TravelDirection.TwoWay;
    public bool IsActive => A.IsActive || B.IsActive;
    public Vector2 Axis => Vector2.Normalize(B.Position - A.Position);

    public float Width => (RightExtent - LeftExtent) + RoadGeometry.ShoulderWidth * 2f;
    public float RightExtent => IsOneWay ? LaneCountTotal * RoadGeometry.LaneWidth * 0.5f : LaneCount(A) * RoadGeometry.LaneWidth;
    public float LeftExtent => IsOneWay ? -LaneCountTotal * RoadGeometry.LaneWidth * 0.5f : -LaneCount(B) * RoadGeometry.LaneWidth;
    private int LaneCountTotal => AllowsTravelFrom(A) ? LaneCount(A) : LaneCount(B);

    public Junction Other(Junction junction) => junction == A ? B : A;

    public int LaneCount(Junction origin) => origin == A ? _lanesForward : _lanesBackward;

    internal void SetLaneCount(Junction origin, int count)
    {
        count = Math.Clamp(count, 1, MaxLanesPerDirection);
        if (origin == A)
            _lanesForward = count;
        else
            _lanesBackward = count;
    }

    public bool AllowsTravelFrom(Junction origin) => Direction switch
    {
        TravelDirection.Forward => origin == A,
        TravelDirection.Backward => origin == B,
        _ => true
    };

    public IEnumerable<Lane> LanesInto(Junction junction) => Lanes.Where(l => l.To == junction);

    public IEnumerable<Lane> LanesFrom(Junction junction) => Lanes.Where(l => l.From == junction);

    public override string ToString() => Name;
}
