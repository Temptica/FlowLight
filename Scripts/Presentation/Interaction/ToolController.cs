using System;
using System.Linq;
using FlowLight.Core;
using FlowLight.Core.Editing;
using FlowLight.Core.Network;
using FlowLight.Presentation.Interface;
using Godot;
using NumericVector = System.Numerics.Vector2;

namespace FlowLight.Presentation.Interaction;

public partial class ToolController : Node
{
    private const float ClickDrift = 6f;

    private GameSession _session = null!;
    private CameraRig _camera = null!;
    private ToolState _tools = null!;
    private SelectionState _selection = null!;
    private SignPicker _signs = null!;
    private Vector2 _pressedAt;

    public event Action<EditResult>? Edited;

    public void Bind(GameSession session, CameraRig camera, ToolState tools, SelectionState selection, SignPicker signs)
    {
        _session = session;
        _camera = camera;
        _tools = tools;
        _selection = selection;
        _signs = signs;
        tools.Changed += _signs.Close;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left or MouseButton.Right } button when button.Pressed:
                _pressedAt = button.Position;
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left or MouseButton.Right } button when button.Position.DistanceTo(_pressedAt) < ClickDrift:
                _signs.Close();
                if (_camera.ScreenToGround(button.Position) is { } ground)
                    Click(ground.ToPlane(), button.Position, button.ButtonIndex == MouseButton.Right);
                break;
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }:
                Cancel();
                break;
        }
    }

    private void Cancel()
    {
        _signs.Close();
        if (_tools.EditedLane is not null)
            _tools.EditLane(null);
        else if (_tools.Current != ToolKind.Inspect)
            _tools.Select(ToolKind.Inspect);
        else
            _selection.Select(null);
    }

    private void Click(NumericVector point, Vector2 screen, bool secondary)
    {
        var network = _session.Scenario.Network;
        var editor = _session.Editor;
        var junctions = network.ActiveJunctions.Where(editor.CanEdit).ToList();
        var roads = network.Roads.Where(r => r.IsActive && editor.CanEdit(r)).ToList();

        switch (_tools.Current)
        {
            case ToolKind.Inspect:
                _selection.Select(secondary ? null : WorldPicker.Pick(_session.Simulation, point));
                break;
            case ToolKind.Signs when !secondary && WorldPicker.PickApproach(junctions, point) is var (junction, approach):
                _signs.Open(screen, approach.Road.Name, junction.SignFor(approach.Road), sign => Report(editor.SetSign(junction, approach.Road, sign)));
                break;
            case ToolKind.Crossings when !secondary && WorldPicker.PickCrosswalk(junctions, point) is { } crosswalk:
                Report(editor.ToggleZebra(crosswalk.Junction, crosswalk.Road));
                break;
            case ToolKind.LaneArrows:
                EditLanes(point, secondary, junctions);
                break;
            case ToolKind.TurnLanes when WorldPicker.PickApproach(junctions, point) is var (junction, approach):
                Report(editor.ChangeLanes(approach.Road, junction, secondary ? -1 : 1));
                break;
            case ToolKind.TrafficLight when !secondary && WorldPicker.PickJunction(junctions, point) is { } junction:
                Report(editor.ToggleSignal(junction));
                _selection.Select(new JunctionSelection(junction));
                break;
            case ToolKind.SpeedLimit when WorldPicker.PickRoad(roads, point) is { } road:
                Report(editor.ChangeSpeed(road, secondary ? -1 : 1));
                break;
            case ToolKind.OneWay when !secondary && WorldPicker.PickRoad(roads, point) is { } road:
                Report(editor.CycleDirection(road));
                break;
            case ToolKind.Detectors when !secondary && WorldPicker.PickJunction(junctions, point) is { } junction:
                Report(editor.InstallDetectors(junction));
                break;
            case ToolKind.SmartLight when !secondary && WorldPicker.PickJunction(junctions, point) is { } junction:
                Report(editor.UpgradeLight(junction));
                _selection.Select(new JunctionSelection(junction));
                break;
            case ToolKind.Camera when !secondary && WorldPicker.PickJunction(junctions, point) is { } junction:
                Report(editor.InstallCamera(junction));
                break;
        }
    }

    private void EditLanes(NumericVector point, bool secondary, System.Collections.Generic.IReadOnlyList<Junction> junctions)
    {
        if (secondary)
        {
            _tools.EditLane(null);
            return;
        }

        if (_tools.EditedLane is { IsRemoved: false } edited
            && WorldPicker.PickLaneEnd(edited.To.OutgoingLanes, point, atStart: true) is { } exit)
        {
            Report(_session.Editor.ToggleLaneConnection(edited, exit));
            _tools.EditLane(edited.Road.Lanes.FirstOrDefault(l => l.Key == edited.Key));
            return;
        }

        _tools.EditLane(WorldPicker.PickLaneEnd(junctions.SelectMany(j => j.IncomingLanes), point, atStart: false));
    }

    private void Report(EditResult result) => Edited?.Invoke(result);
}
