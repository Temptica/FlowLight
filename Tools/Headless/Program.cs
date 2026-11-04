using System;
using System.IO;
using System.Linq;
using FlowLight.Core;
using FlowLight.Core.Campaign;
using FlowLight.Core.Editing;
using FlowLight.Core.Maps;
using FlowLight.Core.Network;
using FlowLight.Core.Simulation;

var mapPath = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "../../../../../Data/Maps/riverside.json");
var minutes = args.Length > 1 ? int.Parse(args[1]) : 10;
var targetLevel = args.Length > 2 ? int.Parse(args[2]) : 1;
var strategy = args.Length > 3 ? args[3] : "none";
var seed = args.Length > 4 ? int.Parse(args[4]) : 7;
var verbose = args.Contains("-v");

var session = new GameSession(MapLoader.Load(File.ReadAllText(mapPath)), new SimulationSettings { Seed = seed });
var editor = session.Editor;
var campaign = session.Campaign;
var stepsPerMinute = (int)(60f / GameSession.StepSeconds);
session.Economy.Treasury.Earn(100000f, "Balancing budget");

void Report(EditResult result)
{
    if (verbose)
        Console.WriteLine($"  {(result.Success ? "+" : "x")} {result.Message}");
}

void ApplySigns(Junction junction)
{
    if (!junction.IsIntersection)
        return;

    var main = junction.Roads.OrderByDescending(r => r.Class).ThenBy(r => r.Id).Take(2).ToList();
    foreach (var road in junction.Roads)
        Report(editor.SetSign(junction, road, main.Contains(road) ? ApproachSign.Priority : ApproachSign.Yield));
}

void TuneGreens(Junction junction)
{
    if (junction.Control is not FlowLight.Core.Control.SignalControl signal)
        return;

    for (var i = 0; i < signal.Phases.Count; i++)
    {
        var major = signal.Phases[i].Roads.Max(r => r.Class) == junction.Roads.Max(r => r.Class);
        var steps = int.Parse(Environment.GetEnvironmentVariable("GREENSTEPS") ?? "2");
        for (var step = 0; step < steps; step++)
            Report(editor.AdjustGreen(junction, i, major ? 1 : -1));
    }
}

void ProtectLefts(Junction junction)
{
    if (junction.Control is not FlowLight.Core.Control.SignalControl signal)
        return;

    var count = signal.Phases.Count;
    for (var i = 0; i < count; i++)
    {
        var lefts = signal.Phases[i].Movements.Where(m => m.Turn == TurnDirection.Left).ToList();
        if (lefts.Count == 0 || signal.Phases[i].Roads.Count() < 2)
            continue;

        signal.AddPhase();
        var added = signal.Phases.Count - 1;
        foreach (var left in lefts)
        {
            signal.ToggleMovement(i, left);
            signal.ToggleMovement(added, left);
        }

        signal.Phases[added].GreenTime = 10f;
        foreach (var road in lefts.Select(l => l.Road).Distinct())
        {
            var lanes = road.LanesInto(junction).OrderBy(l => l.Index).ToList();
            if (lanes.Count < 2)
                continue;

            var leftmost = lanes[^1];
            foreach (var straight in leftmost.Connectors.Where(c => c.Turn != TurnDirection.Left).Select(c => c.To).ToList())
                Report(editor.ToggleLaneConnection(leftmost, straight));
        }
    }

    if (Environment.GetEnvironmentVariable("SMART") == "1")
    {
        Report(editor.InstallDetectors(junction));
        Report(editor.UpgradeLight(junction));
    }
}

void DedicateLeftLanes(Junction junction)
{
    foreach (var road in junction.Roads)
    {
        var lanes = road.LanesInto(junction).OrderBy(l => l.Index).ToList();
        if (lanes.Count == 0 || !lanes[^1].Connectors.Any(c => c.Turn == TurnDirection.Left) || lanes[^1].Connectors.All(c => c.Turn == TurnDirection.Left))
            continue;
        if (lanes.Count < 2)
        {
            Report(editor.ChangeLanes(road, junction, 1));
            lanes = road.LanesInto(junction).OrderBy(l => l.Index).ToList();
        }
        var leftmost = lanes[^1];
        foreach (var target in leftmost.Connectors.Where(c => c.Turn != TurnDirection.Left).Select(c => c.To).ToList())
            Report(editor.ToggleLaneConnection(leftmost, target));
    }
}

void AddLanes(Junction junction, int count)
{
    foreach (var road in junction.Roads.OrderByDescending(r => r.Class).ThenBy(r => r.Id).Take(count))
        Report(editor.ChangeLanes(road, junction, 1));
}

void ApplyFix(Level level)
{
    var junction = level.Junction;
    ApplySigns(junction);
    var plan = level.Number == targetLevel && Environment.GetEnvironmentVariable("FIX") is { } forced ? forced switch
    {
        "lanes" => 2,
        "light" => 3,
        "oneway" => 4,
        "smart" => 6,
        "phases" => 8,
        "light0" => 9,
        "lightleft" => 10,
        _ => level.Number
    } : level.Number;
    switch (plan)
    {
        case 2:
        case 5:
            AddLanes(junction, 2);
            break;
        case 3:
            AddLanes(junction, junction.Roads.Count);
            Report(editor.ToggleSignal(junction));
            TuneGreens(junction);
            break;
        case 4:
            foreach (var road in junction.Roads.Where(r => r.Name == "Kerkpad"))
            {
                Report(editor.CycleDirection(road));
                if (road.LaneCount(road.A) > 0 && road.AllowsTravelFrom(road.Other(junction)))
                    Report(editor.CycleDirection(road));
            }
            break;
        case 8:
            AddLanes(junction, junction.Roads.Count);
            Report(editor.ToggleSignal(junction));
            ProtectLefts(junction);
            break;
        case 9:
            Report(editor.ToggleSignal(junction));
            TuneGreens(junction);
            break;
        case 10:
            Report(editor.ToggleSignal(junction));
            TuneGreens(junction);
            DedicateLeftLanes(junction);
            break;
        case 6:
            AddLanes(junction, junction.Roads.Count);
            Report(editor.ToggleSignal(junction));
            Report(editor.InstallDetectors(junction));
            Report(editor.UpgradeLight(junction));
            break;
        case 7:
            AddLanes(junction, junction.Roads.Count);
            Report(editor.InstallCamera(junction));
            break;
    }
}

while (campaign.Current.Number < targetLevel)
{
    ApplyFix(campaign.Current);
    campaign.Current.IsCompleted = true;
    campaign.Advance();
}

var current = campaign.Current;
if (Environment.GetEnvironmentVariable("DEMAND") is { } demandOverride)
{
    var parts = demandOverride.Split(',').Select(float.Parse).ToArray();
    session.Simulation.SetDemand(new DemandProfile(parts[0] / 60f, parts[1] / 60f, 180f, 180f, parts.Length > 2 ? parts[2] / 60f : 0f, parts.Length > 3 ? parts[3] : 0f));
}
if (strategy == "signs")
    ApplySigns(current.Junction);
else if (strategy == "fix")
    ApplyFix(current);

var completedAt = -1;
var flowSum = 0f;
if (Environment.GetEnvironmentVariable("DIAG") == "1")
    session.Simulation.CrashOccurred += c => Console.WriteLine($"  CRASH {c.First.Path} [{(c.First.Path as Connector)?.Turn}] viol {c.First.IsViolating} vs {c.Second.Path} [{(c.Second.Path as Connector)?.Turn}] viol {c.Second.IsViolating}");
var flowSamples = 0;
var tally = new System.Collections.Generic.Dictionary<string, int>();
for (var minute = 1; minute <= minutes; minute++)
{
    for (var i = 0; i < stepsPerMinute; i++)
    {
        session.Step();
        if (Environment.GetEnvironmentVariable("DIAG") == "2")
            foreach (var lane in current.Junction.Approaches.SelectMany(a => a.Lanes))
                if (lane.Vehicles.Count > 0 && lane.Vehicles[0].IsStopped)
                {
                    var key = $"{lane.Road.Name}[{lane.Index}] {lane.Vehicles[0].WaitReason} {(current.Junction.Control as FlowLight.Core.Control.SignalControl)?.AspectFor(lane)}";
                    tally[key] = (tally.TryGetValue(key, out var n) ? n : 0) + 1;
                    var me = lane.Vehicles[0];
                    if (me.WaitReason == FlowLight.Core.Vehicles.WaitReason.JunctionBusy && me.PlannedConnector is { } mine)
                        foreach (var (o, t) in current.Junction.Reservations)
                            if (o != me && t.From != mine.From && mine.ConflictsWith(t) && !t.HasCleared(o, mine))
                            {
                                var bk = $"   {lane.Road.Name}[{lane.Index}] {mine.Turn} blocked by {t.From.Road.Name} {t.Turn} onPath {o.Path == t} {o.Status} spd {(o.Speed < 0.5f ? "stop" : "move")}";
                                tally[bk] = (tally.TryGetValue(bk, out var m) ? m : 0) + 1;
                            }
                }
        if (current.IsCompleted && completedAt < 0)
            completedAt = minute;
    }

    var stats = current.Junction.Statistics;
    if (minute > 4)
    {
        flowSum += stats.FlowPercent;
        flowSamples++;
    }

    if (Environment.GetEnvironmentVariable("DIAG") == "1")
    {
        foreach (var approach in current.Junction.Approaches)
        {
            var vs = approach.Lanes.SelectMany(l => l.Vehicles).ToList();
            foreach (var lane in approach.Lanes)
                Console.WriteLine($"        lane {lane.Index} [{string.Join("/", lane.Connectors.Select(c => c.Turn).Distinct())}] cars {lane.Vehicles.Count} stopped {lane.Vehicles.Count(v => v.IsStopped)} len {lane.Length:0} first {(lane.Vehicles.Count > 0 ? $"{lane.Vehicles[0].WaitReason} {lane.Vehicles[0].PlannedConnector?.Turn} rem {lane.Vehicles[0].RemainingOnPath:0}" : "-")}");
            Console.WriteLine($"     {approach.Road.Name,-16} lanes {approach.Lanes.Count} queued {vs.Count(v => v.IsStopped),3} maxwait {vs.Select(v => v.WaitAtStopLine).DefaultIfEmpty(0).Max(),5:0} reasons {string.Join(",", vs.Where(v => v.IsStopped).GroupBy(v => v.WaitReason).Select(g => $"{g.Key}:{g.Count()}"))}");
        }
        if (current.Junction.Control is FlowLight.Core.Control.SignalControl sc)
            Console.WriteLine($"     phases: {string.Join(" | ", sc.Phases.Select(p => $"{p.GreenTime:0}s {p.Describe()}"))}");
    }
    if (verbose)
        Console.WriteLine($"     income {session.Economy.IncomePerMinute:0}/min upkeep {session.Economy.UpkeepPerMinute:0}/min treasury {session.Economy.Treasury.Balance:0}");
    if (verbose)
        Console.WriteLine($"min {minute,3} | cars {session.Simulation.Vehicles.Count,4} waiting {session.Simulation.Vehicles.Count(v => v.IsStopped),4} crashes {session.Simulation.Crashes,3} | flow {stats.FlowPercent,3:0}% delay {stats.AverageDelay,4:0.0}s held {campaign.HeldSeconds,3:0}s");
}

Console.WriteLine($"L{current.Number} {current.Title,-14} {strategy,-6} seed {seed}: avg flow {flowSum / Math.Max(1, flowSamples),3:0}%  {(completedAt > 0 ? $"done at min {completedAt}" : "not done")}  crashes {current.Junction.Statistics.Crashes} walkers {session.Simulation.Pedestrians.Crossed} jay {session.Simulation.Pedestrians.Jaywalkers}");
foreach (var (k, v) in tally.OrderBy(kv => kv.Key))
    Console.WriteLine($"  {k,-40} {v * GameSession.StepSeconds,6:0}s");
