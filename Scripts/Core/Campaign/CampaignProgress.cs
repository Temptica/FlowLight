using System;
using System.Collections.Generic;
using System.Linq;
using FlowLight.Core.Editing;
using FlowLight.Core.Maps;
using FlowLight.Core.Network;
using FlowLight.Core.Simulation;

namespace FlowLight.Core.Campaign;

public sealed class CampaignProgress
{
    public const float RequiredHoldSeconds = 90f;
    private const float SettleSeconds = 120f;

    private readonly RoadNetwork _network;
    private readonly TrafficSimulation _simulation;
    private readonly List<Level> _levels;

    public CampaignProgress(MapDefinition map, RoadNetwork network, TrafficSimulation simulation)
    {
        _network = network;
        _simulation = simulation;
        _levels = map.Levels.Select((definition, index) => new Level(index + 1, definition, network.Junction(definition.Junction))).ToList();
        if (_levels.Count == 0)
            throw new InvalidOperationException("The map defines no levels.");
    }

    public IReadOnlyList<Level> Levels => _levels;
    public int CurrentIndex { get; private set; } = -1;
    public Level Current => _levels[CurrentIndex];
    public float HeldSeconds { get; private set; }
    public float HoldProgress => Math.Clamp(HeldSeconds / RequiredHoldSeconds, 0f, 1f);
    public bool CanAdvance => Current.IsCompleted && CurrentIndex < _levels.Count - 1;
    public float WarmUpRemaining => Math.Max(0f, _simulation.Demand.RampSeconds + SettleSeconds - _simulation.DemandTime);
    public bool IsWarmingUp => WarmUpRemaining > 0f;

    public IReadOnlySet<ToolKind> UnlockedTools =>
        _levels.Take(CurrentIndex + 1).SelectMany(l => l.Definition.Unlocks).Append(ToolKind.Inspect).ToHashSet();

    public event Action<Level>? LevelStarted;
    public event Action<Level>? LevelCompleted;

    public bool IsUnlocked(ToolKind tool) => UnlockedTools.Contains(tool);

    public bool IsPlayable(Junction junction) => _levels.Take(CurrentIndex + 1).Any(l => l.Junction == junction);

    public void Start() => StartLevel(0);

    public void Advance()
    {
        if (CanAdvance)
            StartLevel(CurrentIndex + 1);
    }

    public void Update(float deltaTime)
    {
        var level = Current;
        var meetsTarget = !IsWarmingUp && level.Junction.Statistics.FlowPercent >= level.TargetPercent;
        HeldSeconds = meetsTarget ? HeldSeconds + deltaTime : Math.Max(0f, HeldSeconds - deltaTime * 2f);
        if (level.IsCompleted || HeldSeconds < RequiredHoldSeconds)
            return;

        level.IsCompleted = true;
        LevelCompleted?.Invoke(level);
    }

    private void StartLevel(int index)
    {
        CurrentIndex = index;
        HeldSeconds = 0f;
        var level = Current;
        _network.Activate(_levels.Take(index + 1).Select(l => l.Junction));
        foreach (var junction in _network.Junctions)
        {
            junction.DemandBoost = 1f;
            junction.IsFocus = junction == level.Junction;
        }
        foreach (var road in level.Junction.Roads)
        {
            var neighbour = road.Other(level.Junction);
            if (neighbour.IsGateway)
                neighbour.DemandBoost = level.Definition.FocusBoost;
        }

        var demand = level.Definition.Demand;
        _simulation.SetDemand(new DemandProfile(demand.StartPerMinute / 60f, demand.PeakPerMinute / 60f, demand.RampMinutes * 60f, demand.WaveMinutes * 60f, demand.PedestriansPerMinute / 60f, demand.CyclistShare));
        level.Junction.Statistics.ResetFlow(0.6f);
        LevelStarted?.Invoke(level);
    }
}
