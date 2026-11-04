using FlowLight.Presentation.Rendering;
using Godot;

namespace FlowLight.Presentation.Interface;

public partial class FlowGauge : Control
{
    private float _value;
    private float _target = 90f;
    private bool _muted;

    public FlowGauge()
    {
        CustomMinimumSize = new Vector2(0f, 22f);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public void Show(float value, float target, bool muted)
    {
        _muted = muted;
        _value = Mathf.Clamp(value, 0f, 100f);
        _target = target;
        QueueRedraw();
    }

    public static Color ColorFor(float value, float target) =>
        value >= target ? Palette.SignalGreen : value >= target - 20f ? Palette.SignalAmber : Palette.SignalRed;

    public override void _Draw()
    {
        var box = new Rect2(Vector2.Zero, Size);
        DrawRect(box, new Color(1f, 1f, 1f, 0.08f));
        DrawRect(new Rect2(Vector2.Zero, new Vector2(Size.X * _value / 100f, Size.Y)), _muted ? new Color("#7d8a99") : ColorFor(_value, _target));
        var x = Size.X * _target / 100f;
        DrawLine(new Vector2(x, -3f), new Vector2(x, Size.Y + 3f), Colors.White, 2f);
        DrawString(ThemeDB.FallbackFont, new Vector2(8f, Size.Y - 5f), $"{_value:0}%", HorizontalAlignment.Left, -1, 15, new Color("#111418"));
    }
}
