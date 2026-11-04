using System;
using System.Collections.Generic;
using System.Linq;
using FlowLight.Core.Simulation;
using FlowLight.Core.Vehicles;
using Godot;

namespace FlowLight.Presentation.Rendering;

public enum VehicleColorMode
{
    DriverType,
    Happiness,
    WaitReason
}

public partial class VehicleView : Node3D
{
    private const int CapacityStep = 256;

    private readonly MultiMesh _bodies = new()
    {
        TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
        UseColors = true
    };

    private readonly List<Label3D> _crashLabels = new();
    private TrafficSimulation _simulation = null!;
    private float _blink;

    public VehicleColorMode ColorMode { get; private set; } = VehicleColorMode.DriverType;

    public void Initialize(TrafficSimulation simulation)
    {
        _simulation = simulation;
        _bodies.Mesh = new BoxMesh
        {
            Size = Vector3.One,
            Material = new StandardMaterial3D { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 0.5f }
        };
        _bodies.InstanceCount = CapacityStep;
        _bodies.VisibleInstanceCount = 0;
        AddChild(new MultiMeshInstance3D { Multimesh = _bodies });
    }

    public void CycleColorMode() =>
        ColorMode = (VehicleColorMode)(((int)ColorMode + 1) % Enum.GetValues<VehicleColorMode>().Length);

    public override void _Process(double delta)
    {
        _blink += (float)delta;
        var vehicles = _simulation.Vehicles;
        EnsureCapacity(vehicles.Count);

        for (var i = 0; i < vehicles.Count; i++)
        {
            var vehicle = vehicles[i];
            _bodies.SetInstanceTransform(i, TransformFor(vehicle));
            _bodies.SetInstanceColor(i, ColorFor(vehicle));
        }

        _bodies.VisibleInstanceCount = vehicles.Count;
        UpdateCrashLabels(vehicles.Where(v => v.Status == VehicleStatus.Crashed).ToList());
    }

    private void UpdateCrashLabels(IReadOnlyList<Vehicle> crashed)
    {
        while (_crashLabels.Count < crashed.Count)
        {
            var label = new Label3D
            {
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                FontSize = 56,
                PixelSize = 0.07f,
                OutlineSize = 14,
                NoDepthTest = true,
                Modulate = Palette.SignalAmber
            };
            AddChild(label);
            _crashLabels.Add(label);
        }

        for (var i = 0; i < _crashLabels.Count; i++)
        {
            var label = _crashLabels[i];
            label.Visible = i < crashed.Count;
            if (!label.Visible)
                continue;

            label.Text = $"Crash · clearing {crashed[i].CrashTimer:0}s";
            label.Position = crashed[i].Center.ToWorld(7f);
        }
    }

    private void EnsureCapacity(int count)
    {
        if (count <= _bodies.InstanceCount)
            return;

        _bodies.VisibleInstanceCount = 0;
        _bodies.InstanceCount = (count / CapacityStep + 1) * CapacityStep;
    }

    private static Transform3D TransformFor(Vehicle vehicle)
    {
        var type = vehicle.Type;
        var forward = new Vector3(vehicle.Heading.X, 0f, vehicle.Heading.Y);
        var side = new Vector3(forward.Z, 0f, -forward.X);
        var basis = new Basis(side * type.Width, Vector3.Up * type.Height, forward * type.Length);
        return new Transform3D(basis, vehicle.Center.ToWorld(type.Height * 0.5f + 0.05f));
    }

    private Color ColorFor(Vehicle vehicle)
    {
        if (vehicle.Status == VehicleStatus.Crashed)
            return Mathf.PosMod(_blink, 0.6f) < 0.3f ? Palette.SignalRed : new Color("#30343a");

        return ColorMode switch
        {
            VehicleColorMode.Happiness => Palette.SignalRed.Lerp(Palette.SignalGreen, vehicle.Happiness),
            VehicleColorMode.WaitReason => vehicle.WaitReason switch
            {
                WaitReason.RedLight => Palette.SignalRed,
                WaitReason.GivingWay => Palette.SignalAmber,
                WaitReason.JunctionBusy => new Color("#b05cff"),
                WaitReason.ExitBlocked => new Color("#ff5cc8"),
                WaitReason.Pedestrians => new Color("#5cd6ff"),
                _ => vehicle.IsStopped ? new Color("#8a8a8a") : Palette.SignalGreen
            },
            _ => vehicle.Type.IsBicycle ? Palette.Bicycle : Palette.Driver(vehicle.Driver.Archetype)
        };
    }
}
