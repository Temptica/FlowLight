using FlowLight.Presentation.Rendering;
using Godot;

namespace FlowLight.Presentation.Interface;

public enum IconKind
{
    Inspect,
    Priority,
    Yield,
    Stop,
    NoSign,
    Zebra,
    LaneArrows,
    TurnLanes,
    TrafficLight,
    SpeedLimit,
    OneWay,
    Dispatch,
    Detectors,
    SmartLight,
    Camera,
    Pause,
    Play,
    Fast,
    Lock,
    Next,
    TurnLeft,
    TurnStraight,
    TurnRight
}

public partial class Icon : Control
{
    private static readonly Color Ink = new("#f4f4ef");
    private static readonly Color Dark = new("#22262b");
    private static readonly Color SignRed = new("#d8343a");
    private static readonly Color SignBlue = new("#2e6fd1");

    public Icon(IconKind kind, float size = 30f)
    {
        Kind = kind;
        CustomMinimumSize = new Vector2(size, size);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public IconKind Kind { get; }

    public override void _Draw()
    {
        var s = Mathf.Min(Size.X, Size.Y);
        var c = Size * 0.5f;
        switch (Kind)
        {
            case IconKind.Inspect:
                DrawColoredPolygon(new[] { P(c, s, -0.25f, -0.4f), P(c, s, 0.3f, 0.12f), P(c, s, 0.04f, 0.14f), P(c, s, 0.18f, 0.42f), P(c, s, 0.06f, 0.46f), P(c, s, -0.07f, 0.19f), P(c, s, -0.25f, 0.34f) }, Ink);
                break;
            case IconKind.Priority:
                DrawColoredPolygon(Regular(c, s * 0.48f, 4, 0f), Ink);
                DrawColoredPolygon(Regular(c, s * 0.34f, 4, 0f), Palette.PriorityYellow);
                break;
            case IconKind.Yield:
                DrawColoredPolygon(Regular(c + new Vector2(0f, -s * 0.06f), s * 0.5f, 3, Mathf.Pi * 0.5f), SignRed);
                DrawColoredPolygon(Regular(c + new Vector2(0f, -s * 0.06f), s * 0.28f, 3, Mathf.Pi * 0.5f), Ink);
                break;
            case IconKind.Stop:
                DrawColoredPolygon(Regular(c, s * 0.48f, 8, Mathf.Pi / 8f), SignRed);
                DrawRect(new Rect2(c - new Vector2(s * 0.28f, s * 0.07f), new Vector2(s * 0.56f, s * 0.14f)), Ink);
                break;
            case IconKind.NoSign:
                DrawArc(c, s * 0.38f, 0f, Mathf.Tau, 32, Ink, s * 0.08f, true);
                DrawLine(c + new Vector2(-s * 0.27f, s * 0.27f), c + new Vector2(s * 0.27f, -s * 0.27f), Ink, s * 0.08f, true);
                break;
            case IconKind.Zebra:
                DrawRect(new Rect2(c - new Vector2(s * 0.46f, s * 0.46f), new Vector2(s * 0.92f, s * 0.92f)), Dark);
                for (var i = 0; i < 4; i++)
                    DrawRect(new Rect2(c + new Vector2(-s * 0.38f + i * s * 0.21f, -s * 0.36f), new Vector2(s * 0.12f, s * 0.72f)), Ink);
                break;
            case IconKind.LaneArrows:
                DrawLine(c + new Vector2(-s * 0.1f, s * 0.42f), c + new Vector2(-s * 0.1f, -s * 0.05f), Ink, s * 0.1f);
                DrawArc(c + new Vector2(s * 0.1f, -s * 0.05f), s * 0.2f, Mathf.Pi, Mathf.Pi * 1.5f, 10, Ink, s * 0.1f);
                DrawColoredPolygon(new[] { c + new Vector2(s * 0.42f, -s * 0.25f), c + new Vector2(s * 0.12f, -s * 0.42f), c + new Vector2(s * 0.12f, -s * 0.08f) }, Ink);
                break;
            case IconKind.TurnLanes:
                for (var i = -1; i <= 1; i += 2)
                    DrawLine(c + new Vector2(i * s * 0.16f, -s * 0.42f), c + new Vector2(i * s * 0.16f, s * 0.42f), Ink, s * 0.07f);
                DrawRect(new Rect2(c + new Vector2(s * 0.24f, -s * 0.05f), new Vector2(s * 0.24f, s * 0.1f)), Palette.SignalGreen);
                DrawRect(new Rect2(c + new Vector2(s * 0.31f, -s * 0.12f), new Vector2(s * 0.1f, s * 0.24f)), Palette.SignalGreen);
                break;
            case IconKind.TrafficLight:
            case IconKind.SmartLight:
                DrawRect(new Rect2(c - new Vector2(s * 0.18f, s * 0.46f), new Vector2(s * 0.36f, s * 0.92f)), Dark);
                DrawCircle(c + new Vector2(0f, -s * 0.28f), s * 0.11f, Palette.SignalRed);
                DrawCircle(c, s * 0.11f, Palette.SignalAmber);
                DrawCircle(c + new Vector2(0f, s * 0.28f), s * 0.11f, Palette.SignalGreen);
                if (Kind == IconKind.SmartLight)
                    DrawSpark(c + new Vector2(s * 0.32f, -s * 0.3f), s * 0.16f);
                break;
            case IconKind.SpeedLimit:
                DrawCircle(c, s * 0.46f, Ink);
                DrawArc(c, s * 0.4f, 0f, Mathf.Tau, 32, SignRed, s * 0.1f, true);
                DrawString(ThemeDB.FallbackFont, c + new Vector2(-s * 0.24f, s * 0.12f), "50", HorizontalAlignment.Center, s * 0.48f, (int)(s * 0.34f), Dark);
                break;
            case IconKind.OneWay:
                DrawRect(new Rect2(c - new Vector2(s * 0.44f, s * 0.44f), new Vector2(s * 0.88f, s * 0.88f)), SignBlue);
                DrawLine(c + new Vector2(-s * 0.3f, 0f), c + new Vector2(s * 0.1f, 0f), Ink, s * 0.14f);
                DrawColoredPolygon(new[] { c + new Vector2(s * 0.34f, 0f), c + new Vector2(s * 0.06f, -s * 0.22f), c + new Vector2(s * 0.06f, s * 0.22f) }, Ink);
                break;
            case IconKind.Dispatch:
                DrawRect(new Rect2(c + new Vector2(-s * 0.46f, -s * 0.1f), new Vector2(s * 0.6f, s * 0.3f)), Palette.SignalAmber);
                DrawRect(new Rect2(c + new Vector2(-s * 0.3f, -s * 0.3f), new Vector2(s * 0.26f, s * 0.22f)), Palette.SignalAmber);
                DrawLine(c + new Vector2(s * 0.12f, s * 0.05f), c + new Vector2(s * 0.44f, -s * 0.3f), Ink, s * 0.07f);
                DrawLine(c + new Vector2(s * 0.44f, -s * 0.3f), c + new Vector2(s * 0.44f, -s * 0.08f), Ink, s * 0.05f);
                DrawCircle(c + new Vector2(-s * 0.3f, s * 0.24f), s * 0.1f, Dark);
                DrawCircle(c + new Vector2(s * 0.02f, s * 0.24f), s * 0.1f, Dark);
                DrawCircle(c + new Vector2(-s * 0.17f, -s * 0.36f), s * 0.07f, SignBlue);
                break;
            case IconKind.Detectors:
                DrawRect(new Rect2(c - new Vector2(s * 0.24f, s * 0.36f), new Vector2(s * 0.48f, s * 0.72f)), Ink, false, s * 0.08f);
                for (var i = 1; i <= 2; i++)
                    DrawArc(c, s * (0.1f + 0.1f * i), -0.8f, 0.8f, 10, Palette.SignalGreen, s * 0.05f);
                break;
            case IconKind.Camera:
                DrawRect(new Rect2(c - new Vector2(s * 0.42f, s * 0.2f), new Vector2(s * 0.6f, s * 0.4f)), Ink);
                DrawColoredPolygon(new[] { c + new Vector2(s * 0.18f, -s * 0.1f), c + new Vector2(s * 0.44f, -s * 0.24f), c + new Vector2(s * 0.44f, s * 0.24f), c + new Vector2(s * 0.18f, s * 0.1f) }, Ink);
                DrawCircle(c + new Vector2(-s * 0.12f, 0f), s * 0.1f, SignRed);
                break;
            case IconKind.Pause:
                DrawRect(new Rect2(c - new Vector2(s * 0.26f, s * 0.3f), new Vector2(s * 0.18f, s * 0.6f)), Ink);
                DrawRect(new Rect2(c + new Vector2(s * 0.08f, -s * 0.3f), new Vector2(s * 0.18f, s * 0.6f)), Ink);
                break;
            case IconKind.Play:
                DrawColoredPolygon(new[] { c + new Vector2(-s * 0.22f, -s * 0.32f), c + new Vector2(s * 0.32f, 0f), c + new Vector2(-s * 0.22f, s * 0.32f) }, Ink);
                break;
            case IconKind.Fast:
                for (var i = 0; i < 2; i++)
                {
                    var o = new Vector2((i - 0.5f) * s * 0.4f, 0f);
                    DrawColoredPolygon(new[] { c + o + new Vector2(-s * 0.2f, -s * 0.28f), c + o + new Vector2(s * 0.2f, 0f), c + o + new Vector2(-s * 0.2f, s * 0.28f) }, Ink);
                }
                break;
            case IconKind.Lock:
                DrawArc(c + new Vector2(0f, -s * 0.08f), s * 0.18f, Mathf.Pi, Mathf.Tau, 12, Ink, s * 0.08f);
                DrawRect(new Rect2(c + new Vector2(-s * 0.26f, -s * 0.08f), new Vector2(s * 0.52f, s * 0.42f)), Ink);
                break;
            case IconKind.Next:
                DrawLine(c + new Vector2(-s * 0.32f, 0f), c + new Vector2(s * 0.1f, 0f), Ink, s * 0.14f);
                DrawColoredPolygon(new[] { c + new Vector2(s * 0.38f, 0f), c + new Vector2(s * 0.06f, -s * 0.26f), c + new Vector2(s * 0.06f, s * 0.26f) }, Ink);
                break;
            case IconKind.TurnStraight:
                DrawLine(P(c, s, 0f, 0.42f), P(c, s, 0f, -0.08f), Ink, s * 0.14f);
                DrawColoredPolygon(new[] { P(c, s, 0f, -0.44f), P(c, s, 0.26f, -0.08f), P(c, s, -0.26f, -0.08f) }, Ink);
                break;
            case IconKind.TurnLeft:
            case IconKind.TurnRight:
                var mirror = Kind == IconKind.TurnRight ? -1f : 1f;
                DrawLine(P(c, s, mirror * 0.2f, 0.42f), P(c, s, mirror * 0.2f, -0.12f), Ink, s * 0.14f);
                DrawLine(P(c, s, mirror * 0.27f, -0.12f), P(c, s, mirror * -0.1f, -0.12f), Ink, s * 0.14f);
                DrawColoredPolygon(new[] { P(c, s, mirror * -0.44f, -0.12f), P(c, s, mirror * -0.1f, -0.38f), P(c, s, mirror * -0.1f, 0.14f) }, Ink);
                break;
        }
    }

    private void DrawSpark(Vector2 center, float radius)
    {
        for (var i = 0; i < 4; i++)
        {
            var direction = Vector2.FromAngle(i * Mathf.Pi * 0.5f + Mathf.Pi * 0.25f);
            DrawLine(center - direction * radius, center + direction * radius, Palette.PriorityYellow, radius * 0.4f);
        }
    }

    private static Vector2 P(Vector2 center, float size, float x, float y) => center + new Vector2(x, y) * size;

    private static Vector2[] Regular(Vector2 center, float radius, int sides, float angle)
    {
        var points = new Vector2[sides];
        for (var i = 0; i < sides; i++)
            points[i] = center + Vector2.FromAngle(angle + Mathf.Tau * i / sides) * radius;
        return points;
    }
}
