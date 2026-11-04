using System.Numerics;

namespace FlowLight.Core.Pedestrians;

public enum PedestrianState
{
    Waiting,
    Crossing,
    Done
}

public sealed class Pedestrian
{
    private const float CurbGap = 1.2f;

    internal Pedestrian(int id, Crosswalk crosswalk, bool fromStart, float speed, float patience, float lateral)
    {
        Id = id;
        Crosswalk = crosswalk;
        FromStart = fromStart;
        Speed = speed;
        Patience = patience;
        Lateral = lateral;
    }

    public int Id { get; }
    public Crosswalk Crosswalk { get; internal set; }
    public bool FromStart { get; }
    public float Speed { get; }
    public float Patience { get; }
    public float Lateral { get; }
    public PedestrianState State { get; internal set; }
    public float Progress { get; internal set; }
    public float Wait { get; internal set; }
    public bool IsJaywalking { get; internal set; }

    public Vector2 Direction => FromStart ? Crosswalk.Across : -Crosswalk.Across;

    public Vector2 Position
    {
        get
        {
            var origin = FromStart ? Crosswalk.Start : Crosswalk.End;
            var side = new Vector2(-Direction.Y, Direction.X) * Lateral;
            return State == PedestrianState.Waiting
                ? origin - Direction * CurbGap + side
                : origin + Direction * Progress + side;
        }
    }
}
