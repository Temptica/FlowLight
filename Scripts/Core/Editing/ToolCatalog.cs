using System.Collections.Generic;

namespace FlowLight.Core.Editing;

public sealed record ToolInfo(ToolKind Kind, string Title, string Description, int Cost);

public static class ToolCatalog
{
    public const int SignCost = 50;
    public const int LaneArrowCost = 25;
    public const int ZebraCost = 75;
    public const int AddLaneCost = 400;
    public const int RemoveLaneCost = 50;
    public const int TrafficLightCost = 1200;
    public const int RemoveLightCost = 100;
    public const int SpeedLimitCost = 100;
    public const int OneWayCost = 300;
    public const int DispatchCost = 1000;
    public const int DetectorCost = 500;
    public const int SmartLightCost = 1500;
    public const int AdaptiveLightCost = 2500;
    public const int CameraCost = 800;

    public static readonly IReadOnlyList<ToolInfo> All = new[]
    {
        new ToolInfo(ToolKind.Inspect, "Inspect", "Click a car, road or junction to see what is going on.", 0),
        new ToolInfo(ToolKind.Signs, "Signs", "Click the end of a road at a junction to give it priority, a yield sign or a stop sign.", SignCost),
        new ToolInfo(ToolKind.Crossings, "Zebra crossing", "Click the end of a road to paint or remove a zebra. Walkers on a zebra go first; without one they wait for a gap.", ZebraCost),
        new ToolInfo(ToolKind.LaneArrows, "Lane arrows", "Click an incoming lane, then click exits to allow or forbid that turn.", LaneArrowCost),
        new ToolInfo(ToolKind.TurnLanes, "Turn lanes", "Left click a road end to add a lane towards the junction, right click to remove one.", AddLaneCost),
        new ToolInfo(ToolKind.TrafficLight, "Traffic light", "Click a junction to install or remove a light. Select it to set green times. Every light costs money each minute to run.", TrafficLightCost),
        new ToolInfo(ToolKind.TurnPhases, "Turn phases", "Click a traffic light to plan its phases: choose which directions get green together, and add a phase for left turns.", 0),
        new ToolInfo(ToolKind.SpeedLimit, "Speed limit", "Left click a road to raise the limit, right click to lower it.", SpeedLimitCost),
        new ToolInfo(ToolKind.OneWay, "One-way", "Click a road to cycle: two-way, one-way, reversed.", OneWayCost),
        new ToolInfo(ToolKind.Dispatch, "Dispatch", "Build a tow-truck dispatch so crashes are cleared much faster.", DispatchCost),
        new ToolInfo(ToolKind.Detectors, "Detectors", "Loops in the road that count waiting cars. Needed for smart lights.", DetectorCost),
        new ToolInfo(ToolKind.SmartLight, "Smart light", "Upgrade a light: first it reacts to waiting cars, then it adapts by itself. Smarter lights cost a bit more to run.", SmartLightCost),
        new ToolInfo(ToolKind.Camera, "Camera", "Fewer red-light runners and speeders. Fines become income.", CameraCost),
    };

    public static ToolInfo Get(ToolKind kind)
    {
        foreach (var tool in All)
        {
            if (tool.Kind == kind)
                return tool;
        }

        return All[0];
    }
}
