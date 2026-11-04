using System;
using System.Collections.Generic;
using System.Linq;
using FlowLight.Core.Network;

namespace FlowLight.Core.Control;

public readonly record struct Movement(Road Road, TurnDirection Turn);

public sealed class SignalPhase
{
    public const float MinimumGreen = 5f;
    public const float MaximumGreen = 90f;

    private readonly HashSet<Movement> _movements;
    private float _greenTime;

    public SignalPhase(IEnumerable<Movement> movements, float greenTime)
    {
        _movements = new HashSet<Movement>(movements);
        GreenTime = greenTime;
    }

    public IReadOnlySet<Movement> Movements => _movements;
    public IEnumerable<Road> Roads => _movements.Select(m => m.Road).Distinct();
    public bool IsEmpty => _movements.Count == 0;

    public float GreenTime
    {
        get => _greenTime;
        set => _greenTime = Math.Clamp(value, MinimumGreen, MaximumGreen);
    }

    public bool Contains(Movement movement) => _movements.Contains(movement);

    public bool Contains(Road road) => _movements.Any(m => m.Road == road);

    internal bool Toggle(Movement movement)
    {
        if (_movements.Remove(movement))
            return false;

        _movements.Add(movement);
        return true;
    }

    internal SignalPhase Clone() => new(_movements, _greenTime);

    public string Describe() => IsEmpty
        ? "nothing"
        : string.Join(", ", _movements.GroupBy(m => m.Road.Name).Select(g => $"{g.Key} {string.Join("/", g.Select(m => TurnName(m.Turn)).Distinct())}"));

    private static string TurnName(TurnDirection turn) => turn switch
    {
        TurnDirection.Left => "left",
        TurnDirection.Right => "right",
        TurnDirection.UTurn => "U-turn",
        _ => "straight"
    };
}

public enum SignalStage
{
    Green,
    Amber,
    AllRed
}
