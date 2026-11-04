using System;
using System.Collections.Generic;
using System.Linq;
using FlowLight.Core;
using FlowLight.Core.Control;
using FlowLight.Core.Editing;
using FlowLight.Core.Network;
using FlowLight.Core.Vehicles;
using FlowLight.Presentation.Interaction;
using FlowLight.Presentation.Rendering;
using Godot;

namespace FlowLight.Presentation.Interface;

public partial class InfoCard : PanelContainer
{
    private const float Width = 260f;
    private const float RefreshInterval = 0.25f;
    private const float MaxEditorHeight = 300f;
    private const float TurnButtonSize = 25f;
    private static readonly TurnDirection[] TurnOrder = { TurnDirection.Left, TurnDirection.Straight, TurnDirection.Right };

    private GameSession _session = null!;
    private SelectionState _selection = null!;
    private VBoxContainer _column = null!;
    private Label _title = null!;
    private Label _body = null!;
    private VBoxContainer _extra = null!;
    private ScrollContainer _scroll = null!;
    private float _refresh;
    private readonly List<Label> _phaseLabels = new();

    public event Action<EditResult>? Edited;

    public void Bind(GameSession session, SelectionState selection)
    {
        _session = session;
        _selection = selection;
        _column = Ui.Column(6);
        _column.AddChild(_title = Ui.Label(size: 17, color: Ui.Accent));
        _column.AddChild(_body = Ui.Label(size: 13, wrap: true));
        _body.CustomMinimumSize = new Vector2(Width - 24f, 0f);
        _extra = Ui.Column(4);
        _extra.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _scroll.AddChild(_extra);
        _column.AddChild(_scroll);

        var style = new StyleBoxFlat { BgColor = Ui.PanelColor };
        style.SetCornerRadiusAll(10);
        style.SetContentMarginAll(12f);
        AddThemeStyleboxOverride("panel", style);
        AddChild(_column);
        Ui.PinTopRight(this, Width, 12f);
        OffsetTop = 150f;

        selection.Changed += _ => Rebuild();
        session.Editor.Changed += Rebuild;
        Rebuild();
    }

    public override void _Process(double delta)
    {
        _refresh -= (float)delta;
        if (_refresh > 0f || !Visible)
            return;

        _refresh = RefreshInterval;
        if (_selection.Current is VehicleSelection { Vehicle.Status: VehicleStatus.Finished })
        {
            _selection.Select(null);
            return;
        }

        _body.Text = Describe(_selection.Current);
        HighlightCurrentPhase();
    }

    private void Rebuild()
    {
        Ui.Clear(_extra);
        _phaseLabels.Clear();
        _scroll.CustomMinimumSize = Vector2.Zero;
        _scroll.VerticalScrollMode = ScrollContainer.ScrollMode.Disabled;
        var selection = _selection.Current;
        Visible = selection is not null;
        if (selection is null)
            return;

        _title.Text = selection switch
        {
            VehicleSelection { Vehicle: var vehicle } => $"{vehicle.Type.Name} #{vehicle.Id}",
            JunctionSelection { Junction: var junction } => junction.Name,
            RoadSelection { Road: var road } => road.Name,
            _ => ""
        };
        _body.Text = Describe(selection);

        if (selection is JunctionSelection { Junction.Control: SignalControl signal } junctionSelection && _session.Editor.CanEdit(junctionSelection.Junction))
            AddPhaseControls(junctionSelection.Junction, signal);
    }

    private void AddPhaseControls(Junction junction, SignalControl signal)
    {
        var detailed = _session.Campaign.IsUnlocked(ToolKind.TurnPhases);
        _extra.AddChild(Ui.Label(detailed ? "Phases: tap a turn to give it green" : "Green time per phase", 12, Ui.Muted));
        _phaseLabels.Clear();
        for (var i = 0; i < signal.Phases.Count; i++)
            _extra.AddChild(PhaseBlock(junction, signal, i, detailed));
        if (detailed)
        {
            _scroll.VerticalScrollMode = ScrollContainer.ScrollMode.Auto;
            _scroll.CustomMinimumSize = new Vector2(0f, Math.Min(MaxEditorHeight, 40f + signal.Phases.Count * (30f + junction.Approaches.Count * 27f)));
        }

        if (detailed && signal.Phases.Count < SignalControl.MaxPhases)
            _extra.AddChild(Ui.Button("+ Add phase", () => Edited?.Invoke(_session.Editor.AddPhase(junction)), 13));
        else if (!detailed)
            _extra.AddChild(Ui.Label("Green per turn direction unlocks later.", 11, Ui.Muted, wrap: true));
    }

    private Control PhaseBlock(Junction junction, SignalControl signal, int index, bool detailed)
    {
        var phase = signal.Phases[index];
        var block = Ui.Column(1);
        var header = Ui.Row(4);
        var name = Ui.Label(detailed ? $"Phase {index + 1}" : $"{index + 1}. {string.Join(" & ", phase.Roads.Select(r => r.Name))}", 13);
        name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        name.ClipText = true;
        _phaseLabels.Add(name);
        header.AddChild(name);
        header.AddChild(Ui.Button("−", () => Edited?.Invoke(_session.Editor.AdjustGreen(junction, index, -1))));
        header.AddChild(Ui.Label($"{phase.GreenTime:0}s", 13));
        header.AddChild(Ui.Button("+", () => Edited?.Invoke(_session.Editor.AdjustGreen(junction, index, 1))));
        if (detailed && signal.Phases.Count > 1)
            header.AddChild(Ui.Button("×", () => Edited?.Invoke(_session.Editor.RemovePhase(junction, index))));
        block.AddChild(header);

        if (!detailed)
            return block;

        foreach (var road in junction.Approaches.Select(a => a.Road))
        {
            var row = Ui.Row(2);
            var roadName = Ui.Label(road.Name, 12, Ui.Muted);
            roadName.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            roadName.ClipText = true;
            row.AddChild(roadName);
            foreach (var turn in TurnOrder)
            {
                var movement = new Movement(road, turn);
                if (!signal.AvailableMovements.Contains(movement))
                {
                    row.AddChild(new Control { CustomMinimumSize = new Vector2(TurnButtonSize, TurnButtonSize) });
                    continue;
                }

                var served = signal.IsServed(movement);
                var tooltip = $"{road.Name} {turn.ToString().ToLowerInvariant()}{(served ? "" : " (never green!)")}";
                row.AddChild(Ui.SwitchButton(TurnIcon(turn), tooltip, phase.Contains(movement), Palette.SignalGreen,
                    () => Edited?.Invoke(_session.Editor.ToggleMovement(junction, index, movement)), TurnButtonSize));
            }

            block.AddChild(row);
        }

        return block;
    }

    private static IconKind TurnIcon(TurnDirection turn) => turn switch
    {
        TurnDirection.Left => IconKind.TurnLeft,
        TurnDirection.Right => IconKind.TurnRight,
        _ => IconKind.TurnStraight
    };

    private void HighlightCurrentPhase()
    {
        if (_selection.Current is not JunctionSelection { Junction.Control: SignalControl signal })
            return;

        for (var i = 0; i < _phaseLabels.Count; i++)
        {
            var current = i == signal.CurrentPhaseIndex;
            _phaseLabels[i].Modulate = current ? Palette.Aspect(signal.Stage == SignalStage.Green ? SignalAspect.Green : SignalAspect.Amber) : Colors.White;
        }
    }

    private string Describe(Selection? selection) => selection switch
    {
        VehicleSelection { Vehicle: var vehicle } => DescribeVehicle(vehicle),
        JunctionSelection { Junction: var junction } => DescribeJunction(junction),
        RoadSelection { Road: var road } => DescribeRoad(road),
        _ => ""
    };

    private static string DescribeVehicle(Vehicle vehicle)
    {
        var state = vehicle.Status == VehicleStatus.Crashed
            ? $"Crashed, cleared in {vehicle.CrashTimer:0}s"
            : vehicle.IsStopped ? WaitText(vehicle.WaitReason) : $"Driving {vehicle.Speed * 3.6f:0} km/h";
        return $"{vehicle.Driver.Archetype} {(vehicle.Type.IsBicycle ? "cyclist" : "driver")}\n{state}\nMood {vehicle.Happiness * 100f:0}%  ·  on the road {vehicle.TripTime:0}s\nGoing to {vehicle.Destination.Name}";
    }

    private static string WaitText(WaitReason reason) => reason switch
    {
        WaitReason.RedLight => "Waiting for green",
        WaitReason.GivingWay => "Giving way",
        WaitReason.StopSign => "Stopping at the stop sign",
        WaitReason.JunctionBusy => "Waiting for the junction to clear",
        WaitReason.ExitBlocked => "Exit is blocked",
        WaitReason.Pedestrians => "Letting people cross",
        _ => "Stuck in a queue"
    };

    private string DescribeJunction(Junction junction)
    {
        var stats = junction.Statistics;
        var control = junction.Control switch
        {
            SignalControl signal => signal.DisplayName,
            _ => "Signs and priority to the right"
        };
        var extras = string.Join(", ", new[] { junction.Equipment.HasDetectors ? "detectors" : null, junction.Equipment.HasCamera ? "camera" : null }.Where(e => e is not null));
        var walkers = junction.Crosswalks.Sum(c => c.Waiting);
        var zebras = junction.Crosswalks.Count(c => c.HasZebra);
        var upkeep = _session.Economy.Policy.UpkeepPerMinute(junction);
        var running = upkeep > 0f ? $"\nRunning cost {Ui.Money(upkeep)} per minute" : "";
        return $"Flow {stats.FlowPercent:0}%  ·  delay {stats.AverageDelay:0.0}s\n{stats.Throughput} cars passed  ·  {stats.Crashes} crashes\n{walkers} people waiting to cross  ·  {zebras} zebra(s)\n{control}{(extras.Length > 0 ? $" + {extras}" : "")}{running}";
    }

    private static string DescribeRoad(Road road)
    {
        var lanes = road.IsOneWay
            ? $"One-way, {road.LaneCount(road.Direction == TravelDirection.Forward ? road.A : road.B)} lane(s) towards {(road.Direction == TravelDirection.Forward ? road.B : road.A).Name}"
            : $"{road.LaneCount(road.A)} lane(s) towards {road.B.Name}, {road.LaneCount(road.B)} towards {road.A.Name}";
        return $"{road.SpeedLimitKmh} km/h  ·  {road.Class} road\n{lanes}";
    }
}
