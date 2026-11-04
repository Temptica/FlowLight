using System.Linq;

namespace FlowLight.Core.Control;

public sealed class AdaptiveSignalControl : SignalControl
{
    private const float PressureZone = 90f;
    private const float MaxGreen = 60f;

    public override ControlKind Kind => ControlKind.AdaptiveSignal;
    public override string DisplayName => "Adaptive light (self-optimising)";

    protected override bool ShouldEndGreen()
    {
        if (StageTime < SignalPhase.MinimumGreen + 2f)
            return false;

        var current = Pressure(CurrentPhaseIndex);
        var best = BestOtherPhase();
        if (best < 0)
            return false;

        var challenger = Pressure(best);
        return challenger > current * 1.3f + 1f || (StageTime >= MaxGreen && challenger > 0f);
    }

    protected override int ChooseNextPhase()
    {
        var best = BestOtherPhase();
        return best >= 0 && Pressure(best) > 0f ? best : NextIndex(CurrentPhaseIndex);
    }

    private int BestOtherPhase() =>
        Enumerable.Range(0, Phases.Count)
            .Where(i => i != CurrentPhaseIndex && !Phases[i].IsEmpty)
            .OrderByDescending(Pressure)
            .DefaultIfEmpty(-1)
            .First();

    private float Pressure(int index)
    {
        var phase = Phases[index];
        return Demand(phase, PressureZone) + WaitingTime(phase) * 0.15f;
    }
}
