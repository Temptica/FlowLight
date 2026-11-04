using FlowLight.Core.Vehicles;
using FlowLight.Presentation.Interaction;
using Godot;

namespace FlowLight.Presentation.Rendering;

public partial class SelectionHighlight : Node3D
{
    private readonly StandardMaterial3D _material = new()
    {
        AlbedoColor = Palette.Selection,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        NoDepthTest = true
    };

    private MeshInstance3D? _marker;
    private Vehicle? _followed;

    public void Show(Selection? selection)
    {
        _marker?.QueueFree();
        _marker = null;
        _followed = null;

        switch (selection)
        {
            case JunctionSelection { Junction: var junction }:
                _marker = Create(new CylinderMesh { TopRadius = junction.Radius + 4f, BottomRadius = junction.Radius + 4f, Height = 0.2f }, junction.Position.ToWorld(0.3f));
                break;
            case RoadSelection { Road: var road }:
                var a = road.A.Position.ToGodot();
                var b = road.B.Position.ToGodot();
                var direction = b - a;
                _marker = Create(new BoxMesh { Size = new Vector3(road.Width + 2f, 0.2f, direction.Length()) }, ((a + b) * 0.5f).ToWorld3(0.3f));
                _marker.Rotation = new Vector3(0f, Mathf.Atan2(direction.X, direction.Y), 0f);
                break;
            case VehicleSelection { Vehicle: var vehicle }:
                _followed = vehicle;
                _marker = Create(new CylinderMesh { TopRadius = vehicle.Type.Length * 0.8f, BottomRadius = vehicle.Type.Length * 0.8f, Height = 0.2f }, vehicle.Center.ToWorld(0.3f));
                break;
        }
    }

    public override void _Process(double delta)
    {
        if (_followed is null || _marker is null)
            return;

        if (_followed.Status == VehicleStatus.Finished)
        {
            Show(null);
            return;
        }

        _marker.Position = _followed.Center.ToWorld(0.3f);
    }

    private MeshInstance3D Create(Mesh mesh, Vector3 position)
    {
        var marker = new MeshInstance3D { Mesh = mesh, MaterialOverride = _material, Position = position, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(marker);
        return marker;
    }
}
