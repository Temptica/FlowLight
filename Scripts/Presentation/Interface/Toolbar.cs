using System.Collections.Generic;
using FlowLight.Core;
using FlowLight.Core.Editing;
using FlowLight.Presentation.Interaction;
using Godot;

namespace FlowLight.Presentation.Interface;

public partial class Toolbar : VBoxContainer
{
    private readonly Dictionary<ToolKind, Button> _buttons = new();
    private GameSession _session = null!;
    private ToolState _tools = null!;
    private HBoxContainer _row = null!;
    private Label _hint = null!;

    public event System.Action<EditResult>? Edited;

    public static IconKind IconFor(ToolKind tool) => tool switch
    {
        ToolKind.Inspect => IconKind.Inspect,
        ToolKind.Signs => IconKind.Yield,
        ToolKind.Crossings => IconKind.Zebra,
        ToolKind.LaneArrows => IconKind.LaneArrows,
        ToolKind.TurnLanes => IconKind.TurnLanes,
        ToolKind.TrafficLight => IconKind.TrafficLight,
        ToolKind.SpeedLimit => IconKind.SpeedLimit,
        ToolKind.OneWay => IconKind.OneWay,
        ToolKind.Dispatch => IconKind.Dispatch,
        ToolKind.Detectors => IconKind.Detectors,
        ToolKind.SmartLight => IconKind.SmartLight,
        _ => IconKind.Camera
    };

    public void Bind(GameSession session, ToolState tools)
    {
        _session = session;
        _tools = tools;
        Alignment = AlignmentMode.Center;
        AddThemeConstantOverride("separation", 6);
        Ui.PinBottomCenter(this, 14f);

        _hint = Ui.Label(size: 14);
        _hint.HorizontalAlignment = HorizontalAlignment.Center;
        var hintPanel = Ui.Panel(_hint, 8f);
        hintPanel.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        AddChild(hintPanel);

        _row = Ui.Row(6);
        var panel = Ui.Panel(_row, 8f);
        panel.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        AddChild(panel);

        session.Campaign.LevelStarted += _ => Rebuild();
        session.Editor.Changed += Refresh;
        tools.Changed += Refresh;
        Rebuild();
    }

    private void Rebuild()
    {
        Ui.Clear(_row);
        _buttons.Clear();
        foreach (var tool in ToolCatalog.All)
        {
            if (!_session.Campaign.IsUnlocked(tool.Kind))
                continue;

            var kind = tool.Kind;
            var button = Ui.IconButton(IconFor(kind), Tooltip(tool), () => Choose(kind));
            button.ToggleMode = kind != ToolKind.Dispatch;
            _row.AddChild(button);
            _buttons[kind] = button;
        }

        Refresh();
    }

    private void Choose(ToolKind tool)
    {
        if (tool == ToolKind.Dispatch)
        {
            Edited?.Invoke(_session.Editor.BuildDispatch());
            return;
        }

        _tools.Select(tool);
    }

    private void Refresh()
    {
        foreach (var (kind, button) in _buttons)
            button.SetPressedNoSignal(kind == _tools.Current);

        if (_buttons.TryGetValue(ToolKind.Dispatch, out var dispatch))
        {
            dispatch.Disabled = _session.Simulation.Services.HasDispatch;
            dispatch.TooltipText = dispatch.Disabled ? "Dispatch built: crashes are cleared in about 20 seconds." : Tooltip(ToolCatalog.Get(ToolKind.Dispatch));
        }

        var info = ToolCatalog.Get(_tools.Current);
        _hint.Text = _tools.Current == ToolKind.LaneArrows && _tools.EditedLane is { } lane
            ? $"Editing a lane on {lane.Road.Name}: click the exits to allow or forbid them. Right click to stop."
            : info.Description;
    }

    private static string Tooltip(ToolInfo tool) =>
        tool.Cost > 0 ? $"{tool.Title}  ·  €{tool.Cost}\n{tool.Description}" : $"{tool.Title}\n{tool.Description}";
}
