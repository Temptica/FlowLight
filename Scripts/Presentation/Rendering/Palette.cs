using FlowLight.Core.Control;
using FlowLight.Core.Drivers;
using FlowLight.Core.Maps;
using Godot;

namespace FlowLight.Presentation.Rendering;

public static class Palette
{
    public static readonly Color Sky = new("#9fb7c9");
    public static readonly Color Ground = new("#7d9a63");
    public static readonly Color Asphalt = new("#3b3f46");
    public static readonly Color JunctionAsphalt = new("#41454d");
    public static readonly Color Marking = new("#e9e9e2");
    public static readonly Color CenterLine = new("#f2d36b");
    public static readonly Color Detector = new("#1d2024");
    public static readonly Color Pole = new("#5c6168");
    public static readonly Color SignalHousing = new("#1b1d21");
    public static readonly Color InactiveAsphalt = new("#5b5e63");
    public static readonly Color InactiveMarking = new("#8a8c8e");
    public static readonly Color CrossingHint = new("#b5bac0");
    public static readonly Color Bicycle = new("#2fbf71");
    public static readonly Color Boundary = new("#6fa8dc");
    public static readonly Color SignRed = new("#d8343a");
    public static readonly Color Selection = new(1f, 0.85f, 0.2f, 0.16f);

    public static readonly Color SignalGreen = new("#3ddc6b");
    public static readonly Color SignalAmber = new("#ffb02e");
    public static readonly Color SignalRed = new("#ff4b4b");
    public static readonly Color PriorityYellow = new("#ffd23f");

    public static Color Area(AreaKind kind) => kind switch
    {
        AreaKind.Water => new Color("#4f86a8"),
        AreaKind.Park => new Color("#5d8a4a"),
        AreaKind.Plaza => new Color("#b9ab92"),
        _ => new Color("#c9bfae")
    };

    public static Color Roof(int seed) => (seed % 4) switch
    {
        0 => new Color("#a65e48"),
        1 => new Color("#8c8f96"),
        2 => new Color("#b37b55"),
        _ => new Color("#6e6a66")
    };

    public static Color Driver(DriverArchetype archetype) => archetype switch
    {
        DriverArchetype.Cautious => new Color("#5aa9e6"),
        DriverArchetype.Hurried => new Color("#ff9f43"),
        DriverArchetype.Reckless => new Color("#e84855"),
        _ => new Color("#e8e8e8")
    };

    public static Color Aspect(SignalAspect aspect) => aspect switch
    {
        SignalAspect.Green => SignalGreen,
        SignalAspect.Amber => SignalAmber,
        SignalAspect.Red => SignalRed,
        SignalAspect.PriorityRoad => PriorityYellow,
        _ => Marking
    };
}
