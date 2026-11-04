using FlowLight.Core.Pedestrians;
using Godot;

namespace FlowLight.Presentation.Rendering;

public partial class PedestrianView : Node3D
{
    private const int CapacityStep = 128;
    private const float BodyHeight = 2f;

    private static readonly Color[] Clothes =
    {
        new("#e0c068"), new("#6c8ebf"), new("#c9675b"), new("#8bb174"), new("#b38fd1"), new("#e8e8e8"), new("#3d4a5c")
    };

    private readonly MultiMesh _bodies = new()
    {
        TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
        UseColors = true
    };

    private PedestrianTraffic _traffic = null!;

    public void Initialize(PedestrianTraffic traffic)
    {
        _traffic = traffic;
        _bodies.Mesh = new CapsuleMesh
        {
            Radius = 0.45f,
            Height = BodyHeight,
            Material = new StandardMaterial3D { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 0.8f }
        };
        _bodies.InstanceCount = CapacityStep;
        _bodies.VisibleInstanceCount = 0;
        AddChild(new MultiMeshInstance3D { Multimesh = _bodies });
    }

    public override void _Process(double delta)
    {
        var pedestrians = _traffic.All;
        if (pedestrians.Count > _bodies.InstanceCount)
        {
            _bodies.VisibleInstanceCount = 0;
            _bodies.InstanceCount = (pedestrians.Count / CapacityStep + 1) * CapacityStep;
        }

        for (var i = 0; i < pedestrians.Count; i++)
        {
            var pedestrian = pedestrians[i];
            _bodies.SetInstanceTransform(i, new Transform3D(Basis.Identity, pedestrian.Position.ToWorld(BodyHeight * 0.5f)));
            var color = pedestrian.IsJaywalking && pedestrian.State == PedestrianState.Crossing
                ? Palette.SignalRed
                : Clothes[pedestrian.Id % Clothes.Length];
            _bodies.SetInstanceColor(i, color);
        }

        _bodies.VisibleInstanceCount = pedestrians.Count;
    }
}
