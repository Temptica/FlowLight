using System;
using System.Collections.Generic;
using System.Linq;
using FlowLight.Core.Control;
using FlowLight.Core.Network;
using Godot;

namespace FlowLight.Presentation.Rendering;

public partial class JunctionView : Node3D
{
    private const float DetectorLength = 12f;
    private const float PoleOffset = 1.6f;
    private const float SignHeight = 3f;
    private const float SignSize = 1.5f;
    private const float HeadHeight = 5.5f;
    private const float ArrowSize = 1.5f;

    private readonly List<(Func<SignalAspect> Aspect, StandardMaterial3D Material)> _lamps = new();
    private readonly List<(Approach Approach, Label3D Label)> _detectorLabels = new();
    private RoadNetwork _network = null!;

    public void Initialize(RoadNetwork network)
    {
        _network = network;
        Rebuild();
    }

    public void Rebuild()
    {
        foreach (var child in GetChildren())
            child.QueueFree();
        _lamps.Clear();
        _detectorLabels.Clear();

        var loops = new MeshBuilder();
        var signs = new MeshBuilder();
        var heads = new MeshBuilder();
        foreach (var junction in _network.Junctions.Where(j => j.IsIntersection))
        {
            foreach (var approach in junction.Approaches)
                Decorate(junction, approach, loops, signs, heads);

            if (junction.Equipment.HasCamera)
                AddCamera(junction);
        }

        if (loops.Commit() is { } loopMesh)
            AddChild(new MeshInstance3D { Mesh = loopMesh, MaterialOverride = MeshBuilder.VertexColorMaterial(unshaded: true) });
        if (signs.Commit() is { } signMesh)
            AddChild(new MeshInstance3D { Mesh = signMesh, MaterialOverride = MeshBuilder.VertexColorMaterial(unshaded: true) });
        if (heads.Commit() is { } headMesh)
            AddChild(new MeshInstance3D { Mesh = headMesh, MaterialOverride = MeshBuilder.VertexColorMaterial(unshaded: true) });
    }

    public override void _Process(double delta)
    {
        foreach (var (aspect, material) in _lamps)
        {
            var color = Palette.Aspect(aspect());
            material.AlbedoColor = color;
            material.Emission = color;
        }

        foreach (var (approach, label) in _detectorLabels)
        {
            var queued = approach.Lanes.Sum(l => l.Vehicles.Count(v => v.IsStopped));
            var waiting = approach.Lanes.SelectMany(l => l.Vehicles).Select(v => v.WaitAtStopLine).DefaultIfEmpty(0f).Max();
            label.Text = queued == 0 ? "0" : $"{queued}  ·  {waiting:0}s";
            label.Modulate = queued switch
            {
                >= 8 => Palette.SignalRed,
                >= 4 => Palette.SignalAmber,
                _ => Colors.White
            };
        }
    }

    private void Decorate(Junction junction, Approach approach, MeshBuilder loops, MeshBuilder signs, MeshBuilder heads)
    {
        var curbLane = approach.Lanes[0];
        var stop = curbLane.Shape.End.ToGodot();
        var forward = curbLane.Heading.ToGodot();
        var right = new Vector2(-forward.Y, forward.X);
        var polePosition = stop + right * (RoadGeometry.LaneWidth * 0.5f + PoleOffset);

        if (junction.Control is SignalControl)
            AddSignal(junction, approach, polePosition, forward, heads);
        else
            AddSign(signs, junction.SignFor(approach.Road), polePosition, forward);

        if (!junction.Equipment.HasDetectors)
            return;

        foreach (var lane in approach.Lanes)
        {
            var end = lane.Shape.End.ToGodot() - forward * 1.5f;
            loops.FlatStrip(end, end - forward * DetectorLength, RoadGeometry.LaneWidth * 0.6f, 0.06f, Palette.Detector);
        }

        var label = new Label3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 64,
            PixelSize = 0.08f,
            OutlineSize = 12,
            NoDepthTest = true,
            Position = (stop - forward * (DetectorLength + 6f)).ToWorld3(6f)
        };
        AddChild(label);
        _detectorLabels.Add((approach, label));
    }

    private void AddSignal(Junction junction, Approach approach, Vector2 polePosition, Vector2 forward, MeshBuilder heads)
    {
        var right = new Vector2(-forward.Y, forward.X);
        var far = approach.Lanes[^1].Shape.End.ToGodot() - right * RoadGeometry.LaneWidth * 0.5f;
        AddPole(polePosition, HeadHeight);
        heads.FlatStrip(polePosition, far, 0.35f, HeadHeight - 0.3f, Palette.Pole);

        foreach (var lane in approach.Lanes)
        {
            var stop = lane.Shape.End.ToGodot();
            AddLamp(BarMesh(stop, right), () => LaneAspect(junction, lane));

            var turns = lane.Connectors.Select(c => c.Turn).Where(t => t != TurnDirection.UTurn).Distinct().OrderBy(TurnOrder).ToList();
            var center = stop - forward * 1.8f;
            var spacing = RoadGeometry.LaneWidth / Math.Max(turns.Count, 1);
            heads.FlatStrip(center - right * RoadGeometry.LaneWidth * 0.45f, center + right * RoadGeometry.LaneWidth * 0.45f, 3.2f, HeadHeight - 0.1f, Palette.SignalHousing);
            for (var i = 0; i < turns.Count; i++)
            {
                var turn = turns[i];
                var slot = center + right * ((i - (turns.Count - 1) * 0.5f) * spacing);
                var movement = new Movement(approach.Road, turn);
                AddLamp(ArrowMesh(slot, forward, turn), () => junction.Control is SignalControl signal ? signal.AspectFor(movement) : SignalAspect.Red);
            }
        }
    }

    private void AddLamp(ArrayMesh? mesh, Func<SignalAspect> aspect)
    {
        if (mesh is null)
            return;

        var material = new StandardMaterial3D { EmissionEnabled = true, EmissionEnergyMultiplier = 1.5f, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        AddChild(new MeshInstance3D { Mesh = mesh, MaterialOverride = material });
        _lamps.Add((aspect, material));
    }

    private static SignalAspect LaneAspect(Junction junction, Lane lane) =>
        junction.Control is SignalControl signal ? signal.AspectFor(lane) : SignalAspect.Red;

    private static ArrayMesh? BarMesh(Vector2 stop, Vector2 right)
    {
        var builder = new MeshBuilder();
        builder.FlatStrip(stop - right * RoadGeometry.LaneWidth * 0.48f, stop + right * RoadGeometry.LaneWidth * 0.48f, 0.6f, 0.08f, Colors.White);
        return builder.Commit();
    }

    private static ArrayMesh? ArrowMesh(Vector2 center, Vector2 forward, TurnDirection turn)
    {
        var right = new Vector2(-forward.Y, forward.X);
        var direction = turn switch
        {
            TurnDirection.Left => -right,
            TurnDirection.Right => right,
            _ => forward
        };
        var builder = new MeshBuilder();
        var bend = turn == TurnDirection.Straight ? center - forward * ArrowSize * 0.6f : center - direction * ArrowSize * 0.45f;
        if (turn != TurnDirection.Straight)
            builder.FlatStrip(bend - forward * ArrowSize * 0.7f, bend + forward * 0.22f, 0.45f, HeadHeight, Colors.White);
        builder.FlatStrip(bend, bend + direction * ArrowSize * 0.5f, 0.45f, HeadHeight, Colors.White);
        builder.Arrowhead(bend + direction * ArrowSize, direction, ArrowSize * 0.55f, ArrowSize * 0.75f, HeadHeight, Colors.White);
        return builder.Commit();
    }

    private static int TurnOrder(TurnDirection turn) => turn switch
    {
        TurnDirection.Left => 0,
        TurnDirection.Straight => 1,
        _ => 2
    };

    private void AddSign(MeshBuilder signs, ApproachSign sign, Vector2 position, Vector2 forward)
    {
        if (sign == ApproachSign.None)
            return;

        AddPole(position, SignHeight);
        var facing = Mathf.Atan2(-forward.Y, -forward.X);
        switch (sign)
        {
            case ApproachSign.Priority:
                signs.RegularPolygon(position, SignSize, SignHeight, Palette.Marking, 4, facing);
                signs.RegularPolygon(position, SignSize * 0.7f, SignHeight + 0.01f, Palette.PriorityYellow, 4, facing);
                break;
            case ApproachSign.Yield:
                signs.RegularPolygon(position, SignSize, SignHeight, Palette.SignRed, 3, facing);
                signs.RegularPolygon(position, SignSize * 0.55f, SignHeight + 0.01f, Palette.Marking, 3, facing);
                break;
            case ApproachSign.Stop:
                signs.RegularPolygon(position, SignSize, SignHeight, Palette.SignRed, 8, facing + Mathf.Pi / 8f);
                signs.FlatStrip(position - new Vector2(-forward.Y, forward.X) * SignSize * 0.55f, position + new Vector2(-forward.Y, forward.X) * SignSize * 0.55f, SignSize * 0.25f, SignHeight + 0.01f, Palette.Marking);
                break;
        }
    }

    private void AddCamera(Junction junction)
    {
        var corner = junction.Position.ToGodot() + new Vector2(1f, -1f).Normalized() * (junction.Radius + 2f);
        AddPole(corner, 6f);
        AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(1.4f, 0.8f, 0.8f) },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#22262b") },
            Position = corner.ToWorld3(6.3f)
        });
        AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.25f, Height = 0.5f },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = Palette.SignalRed, EmissionEnabled = true, Emission = Palette.SignalRed },
            Position = corner.ToWorld3(6.9f)
        });
    }

    private void AddPole(Vector2 position, float height)
    {
        AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.15f, Height = height },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = Palette.Pole },
            Position = position.ToWorld3(height * 0.5f)
        });
    }
}
