using System.Numerics;
using FlowLight.Core.Drivers;
using FlowLight.Core.Network;

namespace FlowLight.Core.Vehicles;

public enum WaitReason
{
    None,
    RedLight,
    GivingWay,
    JunctionBusy,
    ExitBlocked,
    StopSign,
    Pedestrians
}

public enum VehicleStatus
{
    Driving,
    Crashed,
    Finished
}

public sealed class Vehicle
{
    private const float StoppedSpeed = 0.5f;

    public Vehicle(int id, VehicleType type, DriverProfile driver, Junction origin, Junction destination, float expectedTripTime)
    {
        Id = id;
        Type = type;
        Driver = driver;
        Origin = origin;
        Destination = destination;
        ExpectedTripTime = expectedTripTime;
    }

    public int Id { get; }
    public VehicleType Type { get; }
    public DriverProfile Driver { get; }
    public Junction Origin { get; }
    public Junction Destination { get; internal set; }
    public float ExpectedTripTime { get; }

    public ITrafficPath Path { get; private set; } = null!;
    public float Distance { get; internal set; }
    public float Speed { get; internal set; }
    public float Acceleration { get; internal set; }
    public VehicleStatus Status { get; internal set; } = VehicleStatus.Driving;
    public float CrashTimer { get; internal set; }

    public Connector? PlannedConnector { get; internal set; }
    public bool HasClearance { get; internal set; }
    public bool IsViolating { get; internal set; }
    public float ViolationRoll { get; internal set; }
    public float MistakeRoll { get; internal set; }
    public bool HasHadNearMiss { get; internal set; }
    public bool HasStoppedAtLine { get; internal set; }
    public float WaitAtStopLine { get; internal set; }
    public float ApproachDelay { get; internal set; }
    public WaitReason WaitReason { get; internal set; }

    public float TripTime { get; private set; }
    public float WaitTime { get; private set; }
    public int Stops { get; private set; }
    public int NearMisses { get; internal set; }
    public int ViolationsCommitted { get; internal set; }
    public bool IsStopped => Speed < StoppedSpeed;

    public Vector2 Position { get; private set; }
    public Vector2 Heading { get; private set; } = Vector2.UnitX;
    public Vector2 Center => Position - Heading * (Type.Length * 0.5f);

    public Lane? CurrentLane => Path as Lane;
    public Connector? CurrentConnector => Path as Connector;
    public float RemainingOnPath => Path.Length - Distance;

    public float TripEfficiency => TripTime <= 0f ? 1f : System.Math.Clamp(ExpectedTripTime / TripTime, 0f, 1f);

    public float Happiness => System.Math.Clamp(
        0.2f + 0.8f * TripEfficiency - Stops * 0.025f - NearMisses * 0.15f - System.Math.Max(0f, WaitTime - Driver.Patience) * 0.004f,
        0f,
        1f);

    internal void EnterPath(ITrafficPath path, float distance)
    {
        Path = path;
        Distance = distance;
        path.Vehicles.Add(this);
        UpdatePose();
    }

    internal void LeavePath() => Path.Vehicles.Remove(this);

    internal void Advance(float deltaTime, float acceleration)
    {
        var wasStopped = IsStopped;
        Acceleration = acceleration;
        Speed = System.Math.Max(0f, Speed + acceleration * deltaTime);
        Distance += Speed * deltaTime;
        TripTime += deltaTime;

        if (IsStopped)
        {
            WaitTime += deltaTime;
            if (!wasStopped)
                Stops++;
        }
    }

    internal void UpdatePose()
    {
        var sample = Path.Shape.Sample(Distance);
        Position = sample.Position;
        Heading = sample.Direction;
    }

    internal void ResetApproachState(float violationRoll, float mistakeRoll)
    {
        MistakeRoll = mistakeRoll;
        HasHadNearMiss = false;
        HasStoppedAtLine = false;
        PlannedConnector = null;
        HasClearance = false;
        IsViolating = false;
        WaitAtStopLine = 0f;
        ApproachDelay = 0f;
        WaitReason = WaitReason.None;
        ViolationRoll = violationRoll;
    }
}
