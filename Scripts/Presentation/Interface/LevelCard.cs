using System;
using FlowLight.Core;
using FlowLight.Core.Campaign;
using Godot;

namespace FlowLight.Presentation.Interface;

public partial class LevelCard : PanelContainer
{
    private const float Width = 500f;

    private GameSession _session = null!;
    private Label _title = null!;
    private Label _intro = null!;
    private FlowGauge _gauge = null!;
    private Label _status = null!;
    private Button _next = null!;
    private HBoxContainer _levels = null!;

    public event Action<Level>? FocusRequested;

    public void Bind(GameSession session)
    {
        _session = session;
        var column = Ui.Column(6);
        column.AddChild(_title = Ui.Label(size: 20, color: Ui.Accent));
        column.AddChild(_intro = Ui.Label(size: 13, color: Ui.Muted, wrap: true));
        column.AddChild(_gauge = new FlowGauge());

        var statusRow = Ui.Row(8);
        statusRow.AddChild(_status = Ui.Label(size: 14));
        _status.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _next = Ui.Button("Next junction", () => _session.Campaign.Advance(), 15);
        _next.TooltipText = "Move on to the next junction. You can still come back and change this one.";
        statusRow.AddChild(_next);
        column.AddChild(statusRow);

        column.AddChild(_levels = Ui.Row(4));

        var style = new StyleBoxFlat { BgColor = Ui.PanelColor };
        style.SetCornerRadiusAll(10);
        style.SetContentMarginAll(14f);
        AddThemeStyleboxOverride("panel", style);
        AddChild(column);
        Ui.PinTopCenter(this, Width, 12f);

        session.Campaign.LevelStarted += _ => RebuildLevels();
        session.Campaign.LevelCompleted += _ => RebuildLevels();
        RebuildLevels();
    }

    public override void _Process(double delta)
    {
        var campaign = _session.Campaign;
        var level = campaign.Current;
        var flow = level.Junction.Statistics.FlowPercent;
        _gauge.Show(flow, level.TargetPercent, campaign.IsWarmingUp && !level.IsCompleted);
        _next.Visible = campaign.CanAdvance;
        _status.Text = StatusText(campaign, flow);
    }

    private void RebuildLevels()
    {
        var campaign = _session.Campaign;
        var level = campaign.Current;
        _title.Text = $"Junction {level.Number} of {campaign.Levels.Count}  ·  {level.Title}";
        _intro.Text = string.IsNullOrWhiteSpace(level.Definition.Intro)
            ? $"Get the flow at {level.Title} above {level.TargetPercent:0}% and keep it there."
            : level.Definition.Intro;
        _intro.CustomMinimumSize = new Vector2(Width - 28f, 0f);

        Ui.Clear(_levels);
        foreach (var each in campaign.Levels)
        {
            var target = each;
            var reachable = each.Number <= level.Number;
            var button = Ui.Button(each.Number.ToString(), () => FocusRequested?.Invoke(target), 12);
            button.CustomMinimumSize = new Vector2(28f, 24f);
            button.Disabled = !reachable;
            button.TooltipText = reachable ? $"Look at {each.Title}" : "Not unlocked yet";
            if (each == level)
                button.AddThemeColorOverride("font_color", Ui.Accent);
            else if (each.IsCompleted)
                button.AddThemeColorOverride("font_color", new Color("#3ddc6b"));
            _levels.AddChild(button);
        }
    }

    private static string StatusText(CampaignProgress campaign, float flow)
    {
        var level = campaign.Current;
        if (level.IsCompleted)
            return campaign.CanAdvance ? "Fixed! Traffic flows smoothly." : "Every junction in town is fixed!";
        if (campaign.IsWarmingUp)
            return $"Rush hour is building up · {Ui.Duration(campaign.WarmUpRemaining)}";
        if (flow >= level.TargetPercent)
            return $"Hold it there · {Ui.Duration(CampaignProgress.RequiredHoldSeconds - campaign.HeldSeconds)} to go";

        return $"Goal: {level.TargetPercent:0}% flow";
    }
}
