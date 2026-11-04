using System;

namespace FlowLight.Core.Drivers;

public sealed class DriverFactory
{
    private readonly Random _random;

    public DriverFactory(Random random)
    {
        _random = random;
    }

    public DriverProfile Create()
    {
        var archetype = PickArchetype();
        return archetype switch
        {
            DriverArchetype.Cautious => Build(archetype, speed: 0.9f, compliance: 0.97f, headway: 1.7f, gap: 6.0f, patience: 40f, accel: 0.8f, attention: 0.98f),
            DriverArchetype.Hurried => Build(archetype, speed: 1.12f, compliance: 0.8f, headway: 1.0f, gap: 3.5f, patience: 12f, accel: 1.25f, attention: 0.92f),
            DriverArchetype.Reckless => Build(archetype, speed: 1.28f, compliance: 0.45f, headway: 0.7f, gap: 2.5f, patience: 6f, accel: 1.5f, attention: 0.85f),
            _ => Build(archetype, speed: 1.0f, compliance: 0.92f, headway: 1.3f, gap: 4.5f, patience: 22f, accel: 1f, attention: 0.96f)
        };
    }

    private DriverArchetype PickArchetype()
    {
        var roll = _random.NextSingle();
        return roll switch
        {
            < 0.22f => DriverArchetype.Cautious,
            < 0.74f => DriverArchetype.Average,
            < 0.94f => DriverArchetype.Hurried,
            _ => DriverArchetype.Reckless
        };
    }

    private DriverProfile Build(DriverArchetype archetype, float speed, float compliance, float headway, float gap, float patience, float accel, float attention) =>
        new(
            archetype,
            SpeedFactor: Vary(speed, 0.06f, 0.7f, 1.5f),
            Compliance: Vary(compliance, 0.05f, 0f, 1f),
            TimeHeadway: Vary(headway, 0.15f, 0.6f, 2.6f),
            MinimumGap: 2f,
            GapAcceptance: Vary(gap, 0.6f, 1.5f, 9f),
            Patience: Vary(patience, patience * 0.25f, 3f, 90f),
            AccelerationFactor: Vary(accel, 0.1f, 0.6f, 1.8f),
            Attentiveness: Vary(attention, 0.02f, 0.7f, 1f));

    private float Vary(float mean, float deviation, float min, float max)
    {
        var u1 = 1f - _random.NextSingle();
        var u2 = _random.NextSingle();
        var gaussian = MathF.Sqrt(-2f * MathF.Log(u1)) * MathF.Cos(2f * MathF.PI * u2);
        return Math.Clamp(mean + gaussian * deviation, min, max);
    }
}
