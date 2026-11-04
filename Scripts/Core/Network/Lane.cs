using System.Collections.Generic;
using System.Numerics;
using FlowLight.Core.Geometry;
using FlowLight.Core.Vehicles;

namespace FlowLight.Core.Network;

public readonly record struct LaneKey(int RoadId, int FromJunctionId, int Index);

public sealed class Lane : ITrafficPath
{
    public Lane(Road road, Junction from, Junction to, int index, Polyline shape)
    {
        Road = road;
        From = from;
        To = to;
        Index = index;
        Shape = shape;
    }

    public Road Road { get; }
    public Junction From { get; }
    public Junction To { get; }
    public int Index { get; }
    public Polyline Shape { get; }
    public float Length => Shape.Length;
    public float SpeedLimit => Road.SpeedLimit;
    public bool IsRemoved { get; internal set; }
    public List<Vehicle> Vehicles { get; } = new();
    public List<Connector> Connectors { get; } = new();
    public Vector2 Heading => Shape.EndDirection;
    public bool IsLeftmost => Index == Road.LaneCount(From) - 1;
    public LaneKey Key => new(Road.Id, From.Id, Index);

    public float FreeSpaceAtStart()
    {
        if (Vehicles.Count == 0)
            return Length;

        var last = Vehicles[^1];
        return last.Distance - last.Type.Length;
    }

    public override string ToString() => $"{Road.Name} → {To.Name} [{Index}]";
}
