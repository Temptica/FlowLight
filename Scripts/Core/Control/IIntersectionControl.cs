using FlowLight.Core.Network;
using FlowLight.Core.Vehicles;

namespace FlowLight.Core.Control;

public enum ControlKind
{
    Signs,
    FixedSignal,
    ActuatedSignal,
    AdaptiveSignal
}

public enum SignalAspect
{
    None,
    Green,
    Amber,
    Red,
    PriorityRoad,
    Yield,
    Stop
}

public enum EntryVerdict
{
    Proceed,
    Yield,
    Stop
}

public readonly record struct EntryDecision(EntryVerdict Verdict, float SinceRed = 0f)
{
    public static readonly EntryDecision Proceed = new(EntryVerdict.Proceed);
    public static readonly EntryDecision Yield = new(EntryVerdict.Yield);
}

public interface IIntersectionControl
{
    ControlKind Kind { get; }
    string DisplayName { get; }
    void Attach(Junction junction);
    void Update(float deltaTime);
    SignalAspect AspectFor(Road road);
    SignalAspect AspectFor(Connector connector) => AspectFor(connector.From.Road);
    EntryDecision Evaluate(Vehicle vehicle, Connector connector, float distanceToStopLine);
}
