using FlowLight.Core.Maps;
using FlowLight.Core.Network;

namespace FlowLight.Core.Campaign;

public sealed class Level
{
    public Level(int number, LevelDefinition definition, Junction junction)
    {
        Number = number;
        Definition = definition;
        Junction = junction;
    }

    public int Number { get; }
    public LevelDefinition Definition { get; }
    public Junction Junction { get; }
    public bool IsCompleted { get; internal set; }
    public string Title => string.IsNullOrWhiteSpace(Definition.Title) ? Junction.Name : Definition.Title;
    public float TargetPercent => Definition.TargetFlowPercent;
}
