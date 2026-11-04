using System;

namespace FlowLight.Core.Network;

public sealed class JunctionStatistics
{
    private const float DelaySmoothing = 0.04f;
    private const float SignGrace = 5f;
    private const float SignalGrace = 10f;
    private const float DelayForZeroFlow = 75f;
    private const float StuckWait = 60f;
    private const float StuckGrowthPerSecond = 0.05f;
    private const float CrashPenalty = 0.25f;
    private const float NearMissPenalty = 0.02f;
    private const float PenaltyRecoveryPerSecond = 0.004f;

    private const float PedestrianWeight = 0.5f;

    private float _penalty;
    private float _grace = SignGrace;

    public int Crashes { get; private set; }
    public int Violations { get; private set; }
    public int NearMisses { get; private set; }
    public int Throughput { get; private set; }
    public float AverageDelay { get; private set; }
    public float Flow => Math.Clamp(1f - Math.Max(0f, AverageDelay - _grace) / (DelayForZeroFlow - _grace) - _penalty, 0f, 1f);
    public float FlowPercent => Flow * 100f;

    internal void RecordPassage(float delay)
    {
        Throughput++;
        AverageDelay += (delay - AverageDelay) * DelaySmoothing;
    }

    internal void RecordPedestrian(float wait) =>
        AverageDelay += (wait - AverageDelay) * DelaySmoothing * PedestrianWeight;

    internal void Tick(float deltaTime, float longestWait)
    {
        _penalty = Math.Max(0f, _penalty - PenaltyRecoveryPerSecond * deltaTime);
        if (longestWait > StuckWait && longestWait > AverageDelay)
            AverageDelay += (longestWait - AverageDelay) * Math.Min(1f, StuckGrowthPerSecond * deltaTime);
    }

    internal void RecordCrash()
    {
        Crashes++;
        _penalty = Math.Min(1f, _penalty + CrashPenalty);
    }

    internal void UseGrace(bool signalized) => _grace = signalized ? SignalGrace : SignGrace;

    internal void RecordViolation() => Violations++;

    internal void RecordNearMiss()
    {
        NearMisses++;
        _penalty = Math.Min(1f, _penalty + NearMissPenalty);
    }

    internal void ResetFlow(float value)
    {
        _penalty = 0f;
        AverageDelay = _grace + (1f - value) * (DelayForZeroFlow - _grace);
    }
}
