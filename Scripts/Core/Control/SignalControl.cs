using System;
using System.Collections.Generic;
using System.Linq;
using FlowLight.Core.Network;
using FlowLight.Core.Vehicles;

namespace FlowLight.Core.Control;

public abstract class SignalControl : IIntersectionControl
{
    public const float AmberTime = 3f;
    public const float AllRedTime = 1f;
    public const float DefaultGreen = 20f;
    public const int MaxPhases = 6;
    protected const float DetectionZone = 40f;

    private readonly List<SignalPhase> _phases = new();
    private readonly Dictionary<Movement, float> _redSince = new();
    private readonly Dictionary<Road, float> _roadRedSince = new();
    private float _clock;

    protected Junction Junction { get; private set; } = null!;

    public abstract ControlKind Kind { get; }
    public abstract string DisplayName { get; }

    public IReadOnlyList<SignalPhase> Phases => _phases;
    public int CurrentPhaseIndex { get; private set; }
    public int NextPhaseIndex { get; private set; }
    public SignalStage Stage { get; private set; } = SignalStage.Green;
    public float StageTime { get; private set; }
    protected SignalPhase CurrentPhase => _phases[CurrentPhaseIndex];

    public IEnumerable<Movement> AvailableMovements => Junction.Approaches
        .SelectMany(a => a.Lanes.SelectMany(l => l.Connectors).Select(c => new Movement(a.Road, c.Turn)))
        .Distinct();

    public void Attach(Junction junction)
    {
        Junction = junction;
        var approachRoads = junction.Approaches.Select(a => a.Road).ToHashSet();
        if (_phases.Count > 0 && _phases.SelectMany(p => p.Roads).All(approachRoads.Contains) && approachRoads.All(r => _phases.Any(p => p.Contains(r))))
            return;

        _phases.Clear();
        foreach (var group in GroupOpposingApproaches(junction))
            _phases.Add(new SignalPhase(AllMovements(group), DefaultGreen));

        Restart();
    }

    public void CopyPlanFrom(SignalControl other)
    {
        _phases.Clear();
        _phases.AddRange(other._phases.Select(p => p.Clone()));
        Restart();
    }

    internal bool ToggleMovement(int phaseIndex, Movement movement) => _phases[phaseIndex].Toggle(movement);

    internal void AddPhase()
    {
        _phases.Add(new SignalPhase(Array.Empty<Movement>(), DefaultGreen));
    }

    internal void RemovePhase(int index)
    {
        _phases.RemoveAt(index);
        Restart();
    }

    public bool IsServed(Movement movement) => _phases.Any(p => p.Contains(movement));

    public void Update(float deltaTime)
    {
        if (_phases.Count == 0)
            return;

        _clock += deltaTime;
        StageTime += deltaTime;
        switch (Stage)
        {
            case SignalStage.Green when (CurrentPhase.IsEmpty || ShouldEndGreen()) && HasOtherPhase():
                NextPhaseIndex = SkipEmpty(ChooseNextPhase());
                EnterStage(SignalStage.Amber);
                break;
            case SignalStage.Amber when StageTime >= AmberTime:
                EnterStage(SignalStage.AllRed);
                MarkRed();
                break;
            case SignalStage.AllRed when StageTime >= AllRedTime:
                CurrentPhaseIndex = NextPhaseIndex;
                EnterStage(SignalStage.Green);
                break;
        }
    }

    public SignalAspect AspectFor(Movement movement)
    {
        if (_phases.Count == 0 || !CurrentPhase.Contains(movement))
            return SignalAspect.Red;
        if (Stage == SignalStage.Green || _phases[NextPhaseIndex].Contains(movement))
            return SignalAspect.Green;

        return Stage == SignalStage.Amber ? SignalAspect.Amber : SignalAspect.Red;
    }

    public SignalAspect AspectFor(Connector connector) => AspectFor(new Movement(connector.From.Road, connector.Turn));

    public SignalAspect AspectFor(Road road)
    {
        var aspects = Movements(road).Select(AspectFor).ToList();
        if (aspects.Contains(SignalAspect.Green))
            return SignalAspect.Green;

        return aspects.Contains(SignalAspect.Amber) ? SignalAspect.Amber : SignalAspect.Red;
    }

    public SignalAspect AspectFor(Lane lane)
    {
        var aspects = lane.Connectors.Select(AspectFor).Distinct().ToList();
        if (aspects.Count == 1)
            return aspects[0];

        return aspects.Contains(SignalAspect.Green) ? SignalAspect.Green : aspects.Contains(SignalAspect.Amber) ? SignalAspect.Amber : SignalAspect.Red;
    }

    public EntryDecision Evaluate(Vehicle vehicle, Connector connector, float distanceToStopLine)
    {
        var movement = new Movement(connector.From.Road, connector.Turn);
        switch (AspectFor(movement))
        {
            case SignalAspect.Red:
                return new EntryDecision(EntryVerdict.Stop, SinceRed(movement));
            case SignalAspect.Amber when distanceToStopLine > DrivingModel.StoppingDistance(vehicle, 4f) + 1f:
                return new EntryDecision(EntryVerdict.Stop);
        }

        bool OtherHasPriority(Approach mine, Approach other, Connector theirs)
        {
            if (AspectFor(theirs) == SignalAspect.Red)
                return false;
            if (PriorityRules.OncomingHasPriority(connector, mine, other, theirs))
                return true;
            if (connector.Turn != TurnDirection.Straight && theirs.Turn == TurnDirection.Straight)
                return true;
            if (connector.Turn == TurnDirection.Straight && theirs.Turn != TurnDirection.Straight)
                return false;

            return other.IsToTheRightOf(mine);
        }

        return PriorityRules.MustYield(vehicle, connector, Junction, OtherHasPriority)
            ? EntryDecision.Yield
            : EntryDecision.Proceed;
    }

    public float SinceRed(Movement movement) => _redSince.TryGetValue(movement, out var since) ? _clock - since : 999f;

    public float SinceRed(Road road) => _roadRedSince.TryGetValue(road, out var since) ? _clock - since : 999f;

    protected abstract bool ShouldEndGreen();

    protected abstract int ChooseNextPhase();

    protected int NextIndex(int index) => (index + 1) % _phases.Count;

    protected int Demand(SignalPhase phase, float zone = DetectionZone) =>
        WaitingVehicles(phase, zone).Count();

    protected float WaitingTime(SignalPhase phase) =>
        WaitingVehicles(phase, float.MaxValue).Sum(v => v.WaitAtStopLine);

    private IEnumerable<Vehicle> WaitingVehicles(SignalPhase phase, float zone) =>
        phase.Roads
            .Select(road => Junction.ApproachFor(road))
            .Where(approach => approach is not null)
            .SelectMany(approach => approach!.Lanes.SelectMany(l => l.Vehicles))
            .Where(v => v.RemainingOnPath <= zone && v.PlannedConnector is { } planned && phase.Contains(new Movement(planned.From.Road, planned.Turn)));

    private IEnumerable<Movement> Movements(Road road) =>
        Junction.ApproachFor(road)?.Lanes.SelectMany(l => l.Connectors).Select(c => new Movement(road, c.Turn)).Distinct()
        ?? Enumerable.Empty<Movement>();

    private IEnumerable<Movement> AllMovements(IEnumerable<Road> roads) => roads.SelectMany(Movements);

    private bool HasOtherPhase() => _phases.Where((p, i) => i != CurrentPhaseIndex).Any(p => !p.IsEmpty);

    private int SkipEmpty(int index)
    {
        for (var step = 0; step < _phases.Count; step++)
        {
            var candidate = (index + step) % _phases.Count;
            if (candidate != CurrentPhaseIndex && !_phases[candidate].IsEmpty)
                return candidate;
        }

        return CurrentPhaseIndex;
    }

    private void MarkRed()
    {
        var next = _phases[NextPhaseIndex];
        foreach (var movement in CurrentPhase.Movements.Where(m => !next.Contains(m)))
            _redSince[movement] = _clock;
        foreach (var road in CurrentPhase.Roads.Where(r => !next.Contains(r)))
            _roadRedSince[road] = _clock;
    }

    private void Restart()
    {
        CurrentPhaseIndex = 0;
        NextPhaseIndex = 0;
        Stage = SignalStage.Green;
        StageTime = 0f;
        _redSince.Clear();
        _roadRedSince.Clear();
    }

    private void EnterStage(SignalStage stage)
    {
        Stage = stage;
        StageTime = 0f;
    }

    private static IEnumerable<List<Road>> GroupOpposingApproaches(Junction junction)
    {
        var remaining = junction.Approaches.ToList();
        while (remaining.Count > 0)
        {
            var first = remaining[0];
            remaining.RemoveAt(0);
            var partner = remaining
                .Where(a => a.IsOpposite(first))
                .OrderBy(a => System.Numerics.Vector2.Dot(a.Heading, first.Heading))
                .FirstOrDefault();

            var group = new List<Road> { first.Road };
            if (partner is not null)
            {
                remaining.Remove(partner);
                group.Add(partner.Road);
            }

            yield return group;
        }
    }
}
