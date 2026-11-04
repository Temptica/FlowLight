namespace FlowLight.Core.Control;

public sealed class FixedSignalControl : SignalControl
{
    public override ControlKind Kind => ControlKind.FixedSignal;
    public override string DisplayName => "Traffic light (fixed timing)";

    protected override bool ShouldEndGreen() => StageTime >= CurrentPhase.GreenTime;

    protected override int ChooseNextPhase() => NextIndex(CurrentPhaseIndex);
}
