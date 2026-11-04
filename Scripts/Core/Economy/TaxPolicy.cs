using System;
using FlowLight.Core.Control;
using FlowLight.Core.Network;
using FlowLight.Core.Simulation;
using FlowLight.Core.Vehicles;

namespace FlowLight.Core.Economy;

public sealed record TaxPolicy
{
    public float BaseRoadTax { get; init; } = 1.5f;
    public float TaxPerExpectedSecond { get; init; } = 0.09f;
    public float HappinessExponent { get; init; } = 1.4f;
    public float CrashCost { get; init; } = 350f;
    public float FixedLightUpkeep { get; init; } = 15f;
    public float SmartLightUpkeep { get; init; } = 20f;
    public float AdaptiveLightUpkeep { get; init; } = 25f;
    public float DetectorUpkeep { get; init; } = 3f;
    public float CameraUpkeep { get; init; } = 4f;

    public float RoadTax(Vehicle vehicle) => vehicle.Type.IsBicycle ? 0f :
        (BaseRoadTax + vehicle.ExpectedTripTime * TaxPerExpectedSecond) * MathF.Pow(vehicle.Happiness, HappinessExponent);

    public float Fine(ViolationKind kind) => kind switch
    {
        ViolationKind.RanRedLight => 90f,
        ViolationKind.IgnoredPriority => 55f,
        _ => 35f
    };

    public float UpkeepPerMinute(Junction junction)
    {
        var control = junction.Control.Kind switch
        {
            ControlKind.FixedSignal => FixedLightUpkeep,
            ControlKind.ActuatedSignal => SmartLightUpkeep,
            ControlKind.AdaptiveSignal => AdaptiveLightUpkeep,
            _ => 0f
        };
        var detectors = junction.Equipment.HasDetectors ? DetectorUpkeep : 0f;
        var camera = junction.Equipment.HasCamera ? CameraUpkeep : 0f;
        return control + detectors + camera;
    }
}
