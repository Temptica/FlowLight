using FlowLight.Core.Network;
using FlowLight.Core.Vehicles;

namespace FlowLight.Core.Control;

public sealed class SignControl : IIntersectionControl
{
    private Junction? _junction;

    public ControlKind Kind => ControlKind.Signs;
    public string DisplayName => "Signs";

    public void Attach(Junction junction) => _junction = junction;

    public void Update(float deltaTime)
    {
    }

    public SignalAspect AspectFor(Road road) => _junction!.SignFor(road) switch
    {
        ApproachSign.Priority => SignalAspect.PriorityRoad,
        ApproachSign.Yield => SignalAspect.Yield,
        ApproachSign.Stop => SignalAspect.Stop,
        _ => SignalAspect.None
    };

    public EntryDecision Evaluate(Vehicle vehicle, Connector connector, float distanceToStopLine)
    {
        var junction = _junction!;
        var myRank = Rank(junction.SignFor(connector.From.Road));
        var rightRule = PriorityRules.RightHandRule(connector);

        bool OtherHasPriority(Approach mine, Approach other, Connector theirs)
        {
            var otherRank = Rank(junction.SignFor(other.Road));
            if (otherRank != myRank)
                return otherRank > myRank;

            return myRank == Rank(ApproachSign.Priority)
                ? connector.Turn == TurnDirection.Left && theirs.Turn != TurnDirection.Left
                : rightRule(mine, other, theirs);
        }

        return PriorityRules.MustYield(vehicle, connector, junction, OtherHasPriority)
            ? EntryDecision.Yield
            : EntryDecision.Proceed;
    }

    private static int Rank(ApproachSign sign) => sign switch
    {
        ApproachSign.Priority => 2,
        ApproachSign.None => 1,
        _ => 0
    };
}
