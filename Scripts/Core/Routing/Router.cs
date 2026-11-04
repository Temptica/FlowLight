using System;
using System.Collections.Generic;
using System.Linq;
using FlowLight.Core.Control;
using FlowLight.Core.Network;

namespace FlowLight.Core.Routing;

public sealed class Router
{
    private readonly RoadNetwork _network;
    private readonly Dictionary<Junction, Dictionary<Lane, float>> _liveTables = new();
    private readonly Dictionary<Junction, Dictionary<Lane, float>> _freeFlowTables = new();
    private readonly Dictionary<Lane, List<Connector>> _predecessors = new();

    public Router(RoadNetwork network)
    {
        _network = network;
    }

    public void RebuildTopology()
    {
        _predecessors.Clear();
        foreach (var connector in _network.Connectors)
        {
            if (!_predecessors.TryGetValue(connector.To, out var list))
                _predecessors[connector.To] = list = new List<Connector>();
            list.Add(connector);
        }

        Compute(_freeFlowTables, live: false);
        Compute(_liveTables, live: true);
    }

    public void RefreshCongestion() => Compute(_liveTables, live: true);

    public float CostToDestination(Lane lane, Junction destination) =>
        _liveTables.TryGetValue(destination, out var table) && table.TryGetValue(lane, out var cost) ? cost : float.PositiveInfinity;

    public float FreeFlowTime(Lane entry, Junction destination) =>
        _freeFlowTables.TryGetValue(destination, out var table) && table.TryGetValue(entry, out var cost) ? cost : float.PositiveInfinity;

    public Connector? ChooseConnector(Lane lane, Junction destination, Random random, float noise)
    {
        Connector? best = null;
        var bestCost = float.PositiveInfinity;
        foreach (var connector in lane.Connectors)
        {
            var cost = (ConnectorCost(connector, live: true) + CostToDestination(connector.To, destination))
                       * (1f + noise * random.NextSingle());
            if (cost < bestCost)
            {
                bestCost = cost;
                best = connector;
            }
        }

        return best ?? lane.Connectors.FirstOrDefault();
    }

    public Lane? ChooseEntryLane(Junction origin, Junction destination) =>
        origin.OutgoingLanes
            .Where(l => !float.IsPositiveInfinity(CostToDestination(l, destination)))
            .OrderBy(l => CostToDestination(l, destination) + (l.Length - l.FreeSpaceAtStart()) * 0.05f)
            .FirstOrDefault();

    private void Compute(Dictionary<Junction, Dictionary<Lane, float>> tables, bool live)
    {
        tables.Clear();
        foreach (var gateway in _network.Gateways)
            tables[gateway] = ReverseDijkstra(gateway, live);
    }

    private Dictionary<Lane, float> ReverseDijkstra(Junction destination, bool live)
    {
        var costs = new Dictionary<Lane, float>();
        var queue = new PriorityQueue<Lane, float>();
        foreach (var lane in destination.Roads.SelectMany(r => r.LanesInto(destination)))
        {
            var cost = LaneCost(lane, live);
            costs[lane] = cost;
            queue.Enqueue(lane, cost);
        }

        while (queue.TryDequeue(out var lane, out var cost))
        {
            if (cost > costs[lane])
                continue;
            if (!_predecessors.TryGetValue(lane, out var incoming))
                continue;

            foreach (var connector in incoming)
            {
                var candidate = cost + ConnectorCost(connector, live) + LaneCost(connector.From, live);
                if (costs.TryGetValue(connector.From, out var existing) && existing <= candidate)
                    continue;

                costs[connector.From] = candidate;
                queue.Enqueue(connector.From, candidate);
            }
        }

        return costs;
    }

    private static float LaneCost(Lane lane, bool live)
    {
        var limit = lane.SpeedLimit;
        if (!live || lane.Vehicles.Count == 0)
            return lane.Length / limit;

        var meanSpeed = lane.Vehicles.Average(v => v.Speed);
        var weight = Math.Min(1f, lane.Vehicles.Count / 5f);
        var effective = Math.Max(1.5f, limit * (1f - weight) + meanSpeed * weight);
        return lane.Length / effective + lane.Vehicles.Count(v => v.IsStopped) * 1.5f;
    }

    private static float ConnectorCost(Connector connector, bool live)
    {
        var travel = connector.Length / connector.SpeedLimit;
        var junction = connector.Junction;
        if (live && junction.Statistics.Throughput > 5)
            return travel + junction.Statistics.AverageDelay;

        return travel + junction.Control.AspectFor(connector.From.Road) switch
        {
            SignalAspect.None => junction.IsIntersection ? 3f : 0f,
            SignalAspect.PriorityRoad => 0.5f,
            SignalAspect.Yield => 4f,
            SignalAspect.Stop => 6f,
            _ => 10f
        };
    }
}
