using System;

namespace FlowLight.Core.Simulation;

public sealed class SimulationClock
{
    private const int MaxStepsPerFrame = 40;

    private readonly float _stepSeconds;
    private double _accumulated;

    public SimulationClock(float stepSeconds)
    {
        _stepSeconds = stepSeconds;
    }

    public float SpeedMultiplier { get; set; } = 1f;
    public bool IsPaused { get; set; }

    public int ConsumeSteps(double frameSeconds)
    {
        if (IsPaused)
            return 0;

        _accumulated += frameSeconds * SpeedMultiplier;
        var steps = (int)Math.Floor(_accumulated / _stepSeconds);
        if (steps > MaxStepsPerFrame)
        {
            steps = MaxStepsPerFrame;
            _accumulated = 0;
        }
        else
        {
            _accumulated -= steps * _stepSeconds;
        }

        return steps;
    }
}
