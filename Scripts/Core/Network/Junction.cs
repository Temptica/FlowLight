using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FlowLight.Core.Control;
using FlowLight.Core.Pedestrians;
using FlowLight.Core.Vehicles;

namespace FlowLight.Core.Network;

public enum JunctionRole
{
    Inactive,
    Boundary,
    Active
}

public enum ApproachSign
{
    None,
    Priority,
    Yield,
    Stop
}

public sealed class Junction
{
    private readonly List<Approach> _approaches = new();
    private readonly Dictionary<Road, ApproachSign> _signs = new();
    private readonly Dictionary<LaneKey, HashSet<LaneKey>> _laneConnections = new();
    private readonly HashSet<Road> _zebras = new();
    private readonly Dictionary<Road, Crosswalk> _crosswalks = new();
    private IIntersectionControl _control = new SignControl();

    public Junction(int id, string name, Vector2 position, bool isMapEdge, float edgeWeight)
    {
        Id = id;
        Name = name;
        Position = position;
        IsMapEdge = isMapEdge;
        EdgeWeight = edgeWeight;
    }

    public int Id { get; }
    public string Name { get; }
    public Vector2 Position { get; }
    public bool IsMapEdge { get; }
    public float EdgeWeight { get; }
    public JunctionRole Role { get; internal set; }
    public float Radius { get; internal set; }
    public float DemandBoost { get; internal set; } = 1f;
    public bool IsFocus { get; internal set; }
    public List<Road> Roads { get; } = new();
    public List<Connector> Connectors { get; } = new();
    public IReadOnlyList<Approach> Approaches => _approaches;
    public JunctionEquipment Equipment { get; } = new();
    public Dictionary<Vehicle, Connector> Reservations { get; } = new();
    public JunctionStatistics Statistics { get; } = new();

    public bool IsActive => Role == JunctionRole.Active;
    public bool IsGateway => Role == JunctionRole.Boundary;
    public bool IsIntersection => IsActive && Roads.Count >= 3;
    public bool HasSignal => _control is SignalControl;

    public float SpawnWeight => DemandBoost * (IsMapEdge
        ? EdgeWeight
        : Roads.Where(r => r.Other(this).IsActive).Select(r => (int)r.Class + 1f).DefaultIfEmpty(0f).Max());

    public IIntersectionControl Control
    {
        get => _control;
        set
        {
            _control = value;
            _control.Attach(this);
            Statistics.UseGrace(HasSignal);
        }
    }

    public IEnumerable<Lane> IncomingLanes => _approaches.SelectMany(a => a.Lanes);

    public IEnumerable<Lane> OutgoingLanes => Roads.SelectMany(r => r.LanesFrom(this));

    public Approach? ApproachFor(Road road) => _approaches.FirstOrDefault(a => a.Road == road);

    public ApproachSign SignFor(Road road) => _signs.TryGetValue(road, out var sign) ? sign : ApproachSign.None;

    internal void SetSign(Road road, ApproachSign sign) => _signs[road] = sign;

    public IReadOnlyCollection<Crosswalk> Crosswalks => _crosswalks.Values;

    public Crosswalk? CrosswalkFor(Road road) => _crosswalks.GetValueOrDefault(road);

    public bool HasZebra(Road road) => _zebras.Contains(road);

    internal void SetZebra(Road road, bool present)
    {
        if (present)
            _zebras.Add(road);
        else
            _zebras.Remove(road);
    }

    internal void ReplaceCrosswalks(IEnumerable<Crosswalk> crosswalks)
    {
        _crosswalks.Clear();
        foreach (var crosswalk in crosswalks)
            _crosswalks[crosswalk.Road] = crosswalk;
    }

    public IReadOnlySet<LaneKey>? ConnectionsFor(LaneKey incoming) =>
        _laneConnections.TryGetValue(incoming, out var targets) ? targets : null;

    internal void SetConnections(LaneKey incoming, IEnumerable<LaneKey> targets) =>
        _laneConnections[incoming] = new HashSet<LaneKey>(targets);

    internal void RebuildApproaches()
    {
        _approaches.Clear();
        if (IsActive)
        {
            foreach (var road in Roads)
            {
                var lanes = road.LanesInto(this).OrderBy(l => l.Index).ToList();
                if (lanes.Count > 0)
                    _approaches.Add(new Approach(road, lanes));
            }
        }

        _control.Attach(this);
    }

    public override string ToString() => Name;
}
