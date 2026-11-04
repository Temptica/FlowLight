using System;
using System.Linq;

namespace FlowLight.Core.Control;

public sealed class ActuatedSignalControl : SignalControl
{
    private const float GapOutZone = 25f;

    public override ControlKind Kind => ControlKind.ActuatedSignal;
    public override string DisplayName => "Smart light (detector actuated)";

    protected override bool ShouldEndGreen()
    {
        if (StageTime < SignalPhase.MinimumGreen)
            return false;

        var othersWaiting = Phases.Where((_, i) => i != CurrentPhaseIndex).Any(p => !p.IsEmpty && Demand(p) > 0);
        if (!othersWaiting)
            return false;

        var maxGreen = Math.Min(CurrentPhase.GreenTime * 2f, 60f);
        return StageTime >= maxGreen || Demand(CurrentPhase, GapOutZone) == 0;
    }

    protected override int ChooseNextPhase()
    {
        var index = CurrentPhaseIndex;
        for (var step = 0; step < Phases.Count; step++)
        {
            index = NextIndex(index);
            if (!Phases[index].IsEmpty && Demand(Phases[index]) > 0)
                return index;
        }

        return NextIndex(CurrentPhaseIndex);
    }
}
