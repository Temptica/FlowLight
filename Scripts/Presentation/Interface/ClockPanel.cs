using FlowLight.Core;
using FlowLight.Core.Simulation;
using FlowLight.Presentation.Rendering;
using Godot;

namespace FlowLight.Presentation.Interface;

public partial class ClockPanel : PanelContainer
{
    private GameSession _session = null!;
    private SimulationClock _clock = null!;
    private Label _money = null!;
    private Label _income = null!;
    private Button _pause = null!;
    private Button _play = null!;
    private Button _fast = null!;

    public void Bind(GameSession session, SimulationClock clock)
    {
        _session = session;
        _clock = clock;

        var column = Ui.Column(6);
        column.AddChild(_money = Ui.Label(size: 24, color: Ui.Accent));
        _money.HorizontalAlignment = HorizontalAlignment.Right;
        column.AddChild(_income = Ui.Label(size: 12, color: Ui.Muted));
        _income.HorizontalAlignment = HorizontalAlignment.Right;
        _income.MouseFilter = MouseFilterEnum.Pass;

        var row = Ui.Row(4);
        row.Alignment = BoxContainer.AlignmentMode.End;
        row.AddChild(_pause = Ui.IconButton(IconKind.Pause, "Pause (Space)", () => _clock.IsPaused = true, 36f));
        row.AddChild(_play = Ui.IconButton(IconKind.Play, "Normal speed (1)", () => SetSpeed(1f), 36f));
        row.AddChild(_fast = Ui.IconButton(IconKind.Fast, "Fast forward (2)", () => SetSpeed(4f), 36f));
        foreach (var button in new[] { _pause, _play, _fast })
            button.ToggleMode = true;
        column.AddChild(row);

        AddChild(Ui.Panel(column, 10f));
        AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        Ui.PinTopRight(this, 190f, 12f);
    }

    public override void _Process(double delta)
    {
        var economy = _session.Economy;
        _money.Text = Ui.Money(economy.Treasury.Balance);
        var net = economy.IncomePerMinute - economy.UpkeepPerMinute;
        _income.Text = $"{(net >= 0f ? "+" : "")}{Ui.Money(net)} per minute";
        _income.Modulate = net >= 0f ? Colors.White : Palette.SignalRed;
        _income.TooltipText = $"Road tax +{Ui.Money(economy.IncomePerMinute)} per minute. Happy drivers pay more.\nLights, detectors and cameras cost {Ui.Money(economy.UpkeepPerMinute)} per minute to run.";
        _pause.SetPressedNoSignal(_clock.IsPaused);
        _play.SetPressedNoSignal(!_clock.IsPaused && _clock.SpeedMultiplier <= 1f);
        _fast.SetPressedNoSignal(!_clock.IsPaused && _clock.SpeedMultiplier > 1f);
    }

    private void SetSpeed(float speed)
    {
        _clock.SpeedMultiplier = speed;
        _clock.IsPaused = false;
    }
}
