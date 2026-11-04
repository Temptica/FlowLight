namespace FlowLight.Core.Drivers;

public enum DriverArchetype
{
    Cautious,
    Average,
    Hurried,
    Reckless
}

public sealed record DriverProfile(
    DriverArchetype Archetype,
    float SpeedFactor,
    float Compliance,
    float TimeHeadway,
    float MinimumGap,
    float GapAcceptance,
    float Patience,
    float AccelerationFactor,
    float Attentiveness);
