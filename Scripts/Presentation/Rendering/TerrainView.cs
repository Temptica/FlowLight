using System.Linq;
using FlowLight.Core.Maps;
using Godot;

namespace FlowLight.Presentation.Rendering;

public partial class TerrainView : Node3D
{
    private const float GroundMargin = 400f;

    public void Build(MapDefinition map, Rect2 bounds)
    {
        var ground = new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = bounds.Size + Vector2.One * GroundMargin * 2f },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = Palette.Ground, Roughness = 1f },
            Position = new Vector3(bounds.GetCenter().X, -0.05f, bounds.GetCenter().Y)
        };
        AddChild(ground);

        var flat = new MeshBuilder();
        var buildings = new MeshBuilder();
        var index = 0;
        foreach (var area in map.Areas)
        {
            var points = area.Points.Select(p => new Vector2(p[0], p[1])).ToList();
            if (points.Count < 3)
                continue;

            if (area.Kind == AreaKind.Block)
                buildings.Prism(points, area.Height, Palette.Roof(index++), Palette.Area(AreaKind.Block));
            else
                flat.Polygon(points, area.Kind == AreaKind.Water ? -0.02f : 0.01f, Palette.Area(area.Kind));
        }

        AddMesh(flat.Commit(), castShadows: false);
        AddMesh(buildings.Commit(), castShadows: true);
    }

    private void AddMesh(ArrayMesh? mesh, bool castShadows)
    {
        if (mesh is null)
            return;

        AddChild(new MeshInstance3D
        {
            Mesh = mesh,
            MaterialOverride = MeshBuilder.VertexColorMaterial(),
            CastShadow = castShadows ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off
        });
    }
}
