namespace FlowLight.Core.Simulation;

public sealed record SimulationSettings
{
    public int Seed { get; init; } = 1337;
    public float RouteRefreshInterval { get; init; } = 8f;
    public float RouteNoise { get; init; } = 0.12f;
    public float CameraCatchChance { get; init; } = 0.9f;
}
