using System;
using FlowLight.Core.Network;
using Godot;

namespace FlowLight.Presentation.Interface;

public partial class SignPicker : PanelContainer
{
    private static readonly (ApproachSign Sign, IconKind Icon, string Tooltip)[] Choices =
    {
        (ApproachSign.Priority, IconKind.Priority, "Priority road: drivers here go first."),
        (ApproachSign.Yield, IconKind.Yield, "Yield: give way to traffic on the other roads."),
        (ApproachSign.Stop, IconKind.Stop, "Stop: come to a full stop, then give way."),
        (ApproachSign.None, IconKind.NoSign, "No sign: give way to traffic from the right.")
    };

    private Label _title = null!;
    private HBoxContainer _row = null!;
    private Action<ApproachSign>? _onChosen;

    public override void _Ready()
    {
        var column = Ui.Column(6);
        column.AddChild(_title = Ui.Label(size: 13, color: Ui.Muted));
        column.AddChild(_row = Ui.Row(6));
        AddChild(column);
        var style = new StyleBoxFlat { BgColor = Ui.PanelColor };
        style.SetCornerRadiusAll(8);
        style.SetContentMarginAll(8f);
        AddThemeStyleboxOverride("panel", style);
        Visible = false;
    }

    public void Open(Vector2 screenPosition, string road, ApproachSign current, Action<ApproachSign> onChosen)
    {
        _onChosen = onChosen;
        _title.Text = $"Sign for {road}";
        Ui.Clear(_row);
        foreach (var (sign, icon, tooltip) in Choices)
        {
            var choice = sign;
            var button = Ui.IconButton(icon, tooltip, () => Choose(choice), 48f);
            button.ToggleMode = true;
            button.SetPressedNoSignal(sign == current);
            _row.AddChild(button);
        }

        Visible = true;
        ResetSize();
        var viewport = GetViewportRect().Size;
        Position = new Vector2(
            Mathf.Clamp(screenPosition.X - 110f, 8f, viewport.X - 240f),
            Mathf.Clamp(screenPosition.Y - 100f, 8f, viewport.Y - 100f));
    }

    public void Close()
    {
        Visible = false;
        _onChosen = null;
    }

    private void Choose(ApproachSign sign)
    {
        var callback = _onChosen;
        Close();
        callback?.Invoke(sign);
    }
}
