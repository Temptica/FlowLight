using System.Collections.Generic;
using FlowLight.Core.Editing;
using FlowLight.Core.Network;

namespace FlowLight.Core.Maps;

public sealed record MapDefinition
{
    public string Name { get; init; } = "Unnamed";
    public float StartingBudget { get; init; } = 1500f;
    public List<JunctionDefinition> Junctions { get; init; } = new();
    public List<RoadDefinition> Roads { get; init; } = new();
    public List<LevelDefinition> Levels { get; init; } = new();
    public List<AreaDefinition> Areas { get; init; } = new();
}

public sealed record LevelDefinition
{
    public int Junction { get; init; }
    public string Title { get; init; } = "";
    public string Intro { get; init; } = "";
    public float TargetFlowPercent { get; init; } = 90f;
    public float FocusBoost { get; init; } = 3f;
    public List<ToolKind> Unlocks { get; init; } = new();
    public DemandDefinition Demand { get; init; } = new();
}

public sealed record DemandDefinition
{
    public float StartPerMinute { get; init; } = 20f;
    public float PeakPerMinute { get; init; } = 40f;
    public float RampMinutes { get; init; } = 2f;
    public float WaveMinutes { get; init; } = 3f;
    public float PedestriansPerMinute { get; init; } = 4f;
    public float CyclistShare { get; init; } = 0.06f;
}

public sealed record JunctionDefinition
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public float X { get; init; }
    public float Y { get; init; }
    public bool Gateway { get; init; }
    public float Weight { get; init; } = 1f;
}

public sealed record RoadDefinition
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public int From { get; init; }
    public int To { get; init; }
    public RoadClass Class { get; init; } = RoadClass.Local;
    public int Lanes { get; init; } = 1;
    public int? LanesForward { get; init; }
    public int? LanesBackward { get; init; }
    public int SpeedKmh { get; init; } = 50;
    public TravelDirection Direction { get; init; } = TravelDirection.TwoWay;
}

public enum AreaKind
{
    Block,
    Park,
    Water,
    Plaza
}

public sealed record AreaDefinition
{
    public AreaKind Kind { get; init; } = AreaKind.Block;
    public float Height { get; init; } = 10f;
    public List<float[]> Points { get; init; } = new();
}
