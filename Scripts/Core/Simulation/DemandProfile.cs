using System;

namespace FlowLight.Core.Simulation;

public sealed record DemandProfile(float StartRate, float PeakRate, float RampSeconds, float WavePeriodSeconds, float PedestrianRate = 0f, float CyclistShare = 0f)
{
    public float RateAt(float time)
    {
        var ramp = Math.Clamp(time / Math.Max(RampSeconds, 1f), 0f, 1f);
        var eased = ramp * ramp * (3f - 2f * ramp);
        var baseline = StartRate + (PeakRate - StartRate) * eased;
        var wave = 1f + 0.15f * MathF.Sin(time / Math.Max(WavePeriodSeconds, 1f) * MathF.Tau);
        return baseline * wave;
    }
}
