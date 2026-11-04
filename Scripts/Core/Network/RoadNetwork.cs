using System;
using System.Collections.Generic;
using System.Linq;

namespace FlowLight.Core.Network;

public sealed class RoadNetwork
{
    private readonly Dictionary<int, Junction> _junctions = new();
    private readonly List<Road> _roads = new();

    public RoadNetwork(string name)
    {
        Name = name;
    }

    public string Name { get; }
    public IReadOnlyCollection<Junction> Junctions => _junctions.Values;
    public IReadOnlyList<Road> Roads => _roads;
    public IEnumerable<Junction> Gateways => _junctions.Values.Where(j => j.IsGateway);
    public IEnumerable<Junction> ActiveJunctions => _junctions.Values.Where(j => j.IsActive);
    public IEnumerable<Lane> Lanes => _roads.SelectMany(r => r.Lanes);
    public IEnumerable<Connector> Connectors => _junctions.Values.SelectMany(j => j.Connectors);
    public int Version { get; private set; }

    public event Action? TopologyChanged;

    public Junction Junction(int id) => _junctions[id];

    public void AddJunction(Junction junction) => _junctions.Add(junction.Id, junction);

    public void AddRoad(Road road)
    {
        _roads.Add(road);
        road.A.Roads.Add(road);
        road.B.Roads.Add(road);
    }

    public void Activate(IEnumerable<Junction> active)
    {
        var activeSet = active.ToHashSet();
        var changed = new HashSet<Junction>();
        foreach (var junction in _junctions.Values)
        {
            var role = activeSet.Contains(junction) ? JunctionRole.Active
                : junction.Roads.Any(r => activeSet.Contains(r.Other(junction))) ? JunctionRole.Boundary
                : JunctionRole.Inactive;
            if (role == junction.Role && Version > 0)
                continue;

            junction.Role = role;
            changed.Add(junction);
        }

        Rebuild(changed);
    }

    public void RebuildAround(Road road) => Rebuild(new HashSet<Junction> { road.A, road.B });

    public void Reconnect(Junction junction)
    {
        NetworkBuilder.BuildConnectors(junction);
        Changed();
    }

    private void Rebuild(ISet<Junction> changed)
    {
        var roads = changed.SelectMany(j => j.Roads).Distinct().ToList();
        var junctions = new HashSet<Junction>(changed);
        foreach (var road in roads)
        {
            junctions.Add(road.A);
            junctions.Add(road.B);
        }

        NetworkBuilder.Build(junctions, roads, changed);
        Changed();
    }

    private void Changed()
    {
        Version++;
        TopologyChanged?.Invoke();
    }
}
