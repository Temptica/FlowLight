using System.Collections.Generic;
using Godot;

namespace FlowLight.Presentation.Rendering;

public sealed class MeshBuilder
{
    private readonly SurfaceTool _surface = new();
    private bool _isEmpty = true;

    public MeshBuilder()
    {
        _surface.Begin(Mesh.PrimitiveType.Triangles);
    }

    public static StandardMaterial3D VertexColorMaterial(bool unshaded = false) => new()
    {
        VertexColorUseAsAlbedo = true,
        VertexColorIsSrgb = true,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        ShadingMode = unshaded ? BaseMaterial3D.ShadingModeEnum.Unshaded : BaseMaterial3D.ShadingModeEnum.PerPixel,
        Roughness = 0.9f
    };

    public void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color, Vector3 normal)
    {
        _isEmpty = false;
        _surface.SetColor(color);
        _surface.SetNormal(normal);
        _surface.AddVertex(a);
        _surface.SetColor(color);
        _surface.SetNormal(normal);
        _surface.AddVertex(b);
        _surface.SetColor(color);
        _surface.SetNormal(normal);
        _surface.AddVertex(c);
    }

    public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color, Vector3 normal)
    {
        Triangle(a, b, c, color, normal);
        Triangle(a, c, d, color, normal);
    }

    public void FlatStrip(Vector2 from, Vector2 to, float width, float height, Color color)
    {
        var direction = (to - from).Normalized();
        var side = new Vector2(-direction.Y, direction.X) * (width * 0.5f);
        Quad(Lift(from + side, height), Lift(to + side, height), Lift(to - side, height), Lift(from - side, height), color, Vector3.Up);
    }

    public void DashedStrip(Vector2 from, Vector2 to, float width, float height, Color color, float dash = 3f, float gap = 6f)
    {
        var length = from.DistanceTo(to);
        var direction = (to - from) / Mathf.Max(length, 0.001f);
        for (var start = 0f; start < length; start += dash + gap)
        {
            var end = Mathf.Min(start + dash, length);
            FlatStrip(from + direction * start, from + direction * end, width, height, color);
        }
    }

    public void Disc(Vector2 center, float radius, float height, Color color, int segments = 28)
    {
        var middle = Lift(center, height);
        for (var i = 0; i < segments; i++)
        {
            var a = center + Vector2.FromAngle(Mathf.Tau * i / segments) * radius;
            var b = center + Vector2.FromAngle(Mathf.Tau * (i + 1) / segments) * radius;
            Triangle(middle, Lift(a, height), Lift(b, height), color, Vector3.Up);
        }
    }

    public void RegularPolygon(Vector2 center, float radius, float height, Color color, int sides, float angle)
    {
        var middle = Lift(center, height);
        for (var i = 0; i < sides; i++)
        {
            var a = center + Vector2.FromAngle(angle + Mathf.Tau * i / sides) * radius;
            var b = center + Vector2.FromAngle(angle + Mathf.Tau * (i + 1) / sides) * radius;
            Triangle(middle, Lift(a, height), Lift(b, height), color, Vector3.Up);
        }
    }

    public void Ring(Vector2 center, float inner, float outer, float height, Color color, int segments = 48)
    {
        for (var i = 0; i < segments; i++)
        {
            var a = Vector2.FromAngle(Mathf.Tau * i / segments);
            var b = Vector2.FromAngle(Mathf.Tau * (i + 1) / segments);
            Quad(Lift(center + a * inner, height), Lift(center + a * outer, height), Lift(center + b * outer, height), Lift(center + b * inner, height), color, Vector3.Up);
        }
    }

    public void Arrowhead(Vector2 tip, Vector2 direction, float length, float width, float height, Color color)
    {
        var side = new Vector2(-direction.Y, direction.X) * (width * 0.5f);
        var back = tip - direction * length;
        Triangle(Lift(tip, height), Lift(back + side, height), Lift(back - side, height), color, Vector3.Up);
    }

    public void Polygon(IReadOnlyList<Vector2> points, float height, Color color)
    {
        var array = new Vector2[points.Count];
        for (var i = 0; i < points.Count; i++)
            array[i] = points[i];

        var indices = Geometry2D.TriangulatePolygon(array);
        for (var i = 0; i + 2 < indices.Length; i += 3)
            Triangle(Lift(array[indices[i]], height), Lift(array[indices[i + 1]], height), Lift(array[indices[i + 2]], height), color, Vector3.Up);
    }

    public void Prism(IReadOnlyList<Vector2> points, float height, Color roof, Color wall)
    {
        Polygon(points, height, roof);
        for (var i = 0; i < points.Count; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % points.Count];
            var edge = (b - a).Normalized();
            var normal = new Vector3(edge.Y, 0f, -edge.X);
            Quad(Lift(a, 0f), Lift(b, 0f), Lift(b, height), Lift(a, height), wall, normal);
        }
    }

    public ArrayMesh? Commit() => _isEmpty ? null : _surface.Commit();

    private static Vector3 Lift(Vector2 point, float height) => new(point.X, height, point.Y);
}
