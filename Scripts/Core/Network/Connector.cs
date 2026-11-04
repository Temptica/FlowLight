using System;
using System.Collections.Generic;
using FlowLight.Core.Geometry;
using FlowLight.Core.Vehicles;

namespace FlowLight.Core.Network;

public sealed class Connector : ITrafficPath
{
    private readonly Dictionary<Connector, float> _conflicts = new();

    public Connector(Junction junction, Lane from, Lane to, TurnDirection turn, Polyline shape)
    {
        Junction = junction;
        From = from;
        To = to;
        Turn = turn;
        Shape = shape;
    }

    public Junction Junction { get; }
    public Lane From { get; }
    public Lane To { get; }
    public TurnDirection Turn { get; }
    public Polyline Shape { get; }
    public float Length => Shape.Length;
    public bool IsRemoved { get; internal set; }
    public List<Vehicle> Vehicles { get; } = new();
    public IReadOnlyCollection<Connector> Conflicts => _conflicts.Keys;

    public float SpeedLimit => Turn switch
    {
        TurnDirection.Right => 5f,
        TurnDirection.Left => 7f,
        TurnDirection.UTurn => 3.5f,
        _ => Math.Min(From.SpeedLimit, To.SpeedLimit)
    };

    public bool ConflictsWith(Connector other) => _conflicts.ContainsKey(other);

    public float ConflictDistance(Connector other) => _conflicts.TryGetValue(other, out var distance) ? distance : Length;

    public bool HasCleared(Vehicles.Vehicle vehicle, Connector other)
    {
        if (vehicle.Path != this)
            return false;

        return vehicle.Distance - vehicle.Type.Length > ConflictDistance(other) + 1f;
    }

    internal void ClearConflicts() => _conflicts.Clear();

    internal void TryRegisterConflict(Connector other)
    {
        if (other.From == From)
            return;

        if (other.To == To)
        {
            _conflicts[other] = Length;
            other._conflicts[this] = other.Length;
            return;
        }

        if (!Shape.TryFindIntersection(other.Shape, out var mine, out var theirs))
            return;

        _conflicts[other] = mine;
        other._conflicts[this] = theirs;
    }
}
