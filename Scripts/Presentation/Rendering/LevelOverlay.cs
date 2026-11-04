using System.Linq;
using FlowLight.Core;
using FlowLight.Core.Editing;
using FlowLight.Core.Network;
using FlowLight.Presentation.Interaction;
using FlowLight.Presentation.Interface;
using Godot;

namespace FlowLight.Presentation.Rendering;

public partial class LevelOverlay : Node3D
{
    private const float Height = 0.12f;
    private const float MarkerRadius = 1.1f;

    private readonly StandardMaterial3D _material = new()
    {
        VertexColorUseAsAlbedo = true,
        VertexColorIsSrgb = true,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        NoDepthTest = true,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha
    };

    private GameSession _session = null!;
    private ToolState _tools = null!;
    private MeshInstance3D? _mesh;
    private float _pulse;

    public void Bind(GameSession session, ToolState tools)
    {
        _session = session;
        _tools = tools;
        tools.Changed += Rebuild;
        session.Campaign.LevelStarted += _ => Rebuild();
        Rebuild();
    }

    public override void _Process(double delta)
    {
        _pulse += (float)delta;
        _material.AlbedoColor = new Color(1f, 1f, 1f, 0.75f + 0.25f * Mathf.Sin(_pulse * 3f));
    }

    public void Rebuild()
    {
        var builder = new MeshBuilder();
        var focus = _session.Campaign.Current.Junction;
        builder.Ring(focus.Position.ToGodot(), focus.Radius + 7f, focus.Radius + 8.2f, Height, new Color(1f, 0.82f, 0.25f, 0.8f));

        if (_tools.Current == ToolKind.LaneArrows)
            AddLaneMarkers(builder);

        _mesh?.QueueFree();
        _mesh = null;
        if (builder.Commit() is not { } mesh)
            return;

        _mesh = new MeshInstance3D { Mesh = mesh, MaterialOverride = _material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_mesh);
    }

    private void AddLaneMarkers(MeshBuilder builder)
    {
        var edited = _tools.EditedLane;
        foreach (var junction in _session.Scenario.Network.ActiveJunctions.Where(j => j.IsIntersection))
        foreach (var lane in junction.IncomingLanes)
        {
            var color = lane == edited ? Ui.Accent : new Color(1f, 1f, 1f, 0.9f);
            builder.Disc(WorldPicker.LaneMarker(lane, atStart: false).ToGodot(), MarkerRadius, Height, color, 16);
        }

        if (edited is null)
            return;

        var targets = edited.Connectors.Select(c => c.To).ToHashSet();
        foreach (var connector in edited.Connectors)
            AddPath(builder, connector);

        foreach (var exit in edited.To.OutgoingLanes)
        {
            var color = targets.Contains(exit) ? Palette.SignalGreen : new Color(0.6f, 0.6f, 0.6f, 0.9f);
            builder.Disc(WorldPicker.LaneMarker(exit, atStart: true).ToGodot(), MarkerRadius, Height, color, 16);
        }
    }

    private static void AddPath(MeshBuilder builder, Connector connector)
    {
        const int segments = 12;
        var shape = connector.Shape;
        var previous = shape.Sample(0f).Position.ToGodot();
        for (var i = 1; i <= segments; i++)
        {
            var next = shape.Sample(shape.Length * i / segments).Position.ToGodot();
            builder.FlatStrip(previous, next, 0.5f, Height, Palette.SignalGreen);
            previous = next;
        }
    }
}
