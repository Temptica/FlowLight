using Godot;

namespace FlowLight.Presentation;

public static class Conversions
{
    public static Vector3 ToWorld(this System.Numerics.Vector2 point, float height = 0f) => new(point.X, height, point.Y);

    public static Vector2 ToGodot(this System.Numerics.Vector2 point) => new(point.X, point.Y);

    public static Vector3 ToWorld3(this Vector2 point, float height = 0f) => new(point.X, height, point.Y);

    public static System.Numerics.Vector2 ToPlane(this Vector3 point) => new(point.X, point.Z);
}
