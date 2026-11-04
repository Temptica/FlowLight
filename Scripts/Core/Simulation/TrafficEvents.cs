using FlowLight.Core.Network;
using FlowLight.Core.Vehicles;

namespace FlowLight.Core.Simulation;

public enum ViolationKind
{
    RanRedLight,
    IgnoredPriority,
    RolledStopSign,
    Speeding
}

public readonly record struct CrashReport(Junction Junction, Vehicle First, Vehicle Second);

public readonly record struct ViolationReport(Junction Junction, Vehicle Vehicle, ViolationKind Kind, bool Caught);
