using Godot;

namespace FlowLight.Presentation.Interface;

public partial class NoticeFeed : VBoxContainer
{
    private const int MaxNotices = 6;
    private const float VisibleSeconds = 7f;

    public override void _Ready()
    {
        Ui.PinBottomLeft(this, 430f, 14f);
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeConstantOverride("separation", 4);
    }

    public void Push(string message) => Push(message, false);

    public void Push(string message, bool warning)
    {
        var label = Ui.Label(message, 13, warning ? Rendering.Palette.SignalAmber : null, wrap: true);
        label.CustomMinimumSize = new Vector2(400f, 0f);
        label.MouseFilter = MouseFilterEnum.Ignore;
        var panel = Ui.Panel(label, 8f);
        panel.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(panel);

        while (GetChildCount() > MaxNotices)
        {
            var oldest = GetChild(0);
            RemoveChild(oldest);
            oldest.QueueFree();
        }

        var tween = panel.CreateTween();
        tween.TweenInterval(VisibleSeconds);
        tween.TweenProperty(panel, "modulate:a", 0f, 1.2f);
        tween.TweenCallback(Callable.From(panel.QueueFree));
    }
}
