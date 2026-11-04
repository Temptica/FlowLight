using System;
using Godot;

namespace FlowLight.Presentation.Interface;

public static class Ui
{
    public static readonly Color PanelColor = new(0.08f, 0.09f, 0.11f, 0.88f);
    public static readonly Color Muted = new("#9aa3ad");
    public static readonly Color Accent = new("#ffd23f");

    public static PanelContainer Panel(Control content, float padding = 12f)
    {
        var style = new StyleBoxFlat { BgColor = PanelColor };
        style.SetCornerRadiusAll(8);
        style.SetContentMarginAll(padding);
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", style);
        panel.AddChild(content);
        return panel;
    }

    public static Label Label(string text = "", int size = 14, Color? color = null, bool wrap = false)
    {
        var label = new Label { Text = text, AutowrapMode = wrap ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off };
        label.AddThemeFontSizeOverride("font_size", size);
        if (color is { } tint)
            label.AddThemeColorOverride("font_color", tint);
        return label;
    }

    public static Button Button(string text, Action onPressed, int size = 14)
    {
        var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.None };
        button.AddThemeFontSizeOverride("font_size", size);
        button.Pressed += onPressed;
        return button;
    }

    public static Button IconButton(IconKind icon, string tooltip, Action onPressed, float size = 44f)
    {
        var button = new Button
        {
            FocusMode = Control.FocusModeEnum.None,
            TooltipText = tooltip,
            CustomMinimumSize = new Vector2(size, size)
        };
        button.AddThemeStyleboxOverride("normal", ButtonStyle(new Color(1f, 1f, 1f, 0.06f), null));
        button.AddThemeStyleboxOverride("hover", ButtonStyle(new Color(1f, 1f, 1f, 0.16f), null));
        button.AddThemeStyleboxOverride("pressed", ButtonStyle(new Color(1f, 0.82f, 0.25f, 0.22f), Accent));
        button.AddThemeStyleboxOverride("hover_pressed", ButtonStyle(new Color(1f, 0.82f, 0.25f, 0.3f), Accent));
        button.AddThemeStyleboxOverride("disabled", ButtonStyle(new Color(1f, 1f, 1f, 0.02f), null));
        var glyph = new Icon(icon, size * 0.62f);
        glyph.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        glyph.OffsetLeft = glyph.OffsetTop = size * 0.19f;
        glyph.OffsetRight = glyph.OffsetBottom = -size * 0.19f;
        button.AddChild(glyph);
        button.Pressed += onPressed;
        return button;
    }

    public static Button SwitchButton(IconKind icon, string tooltip, bool on, Color onColor, Action onPressed, float size = 30f)
    {
        var button = IconButton(icon, tooltip, onPressed, size);
        if (!on)
        {
            button.Modulate = new Color(1f, 1f, 1f, 0.45f);
            return button;
        }

        button.AddThemeStyleboxOverride("normal", ButtonStyle(new Color(onColor, 0.35f), onColor));
        button.AddThemeStyleboxOverride("hover", ButtonStyle(new Color(onColor, 0.5f), onColor));
        return button;
    }

    public static VBoxContainer Column(int separation = 6)
    {
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", separation);
        return column;
    }

    public static HBoxContainer Row(int separation = 8)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", separation);
        return row;
    }

    public static void Clear(Node container)
    {
        foreach (var child in container.GetChildren())
        {
            container.RemoveChild(child);
            child.QueueFree();
        }
    }

    public static void PinTopRight(Control control, float width, float margin)
    {
        control.AnchorLeft = control.AnchorRight = 1f;
        control.AnchorTop = control.AnchorBottom = 0f;
        control.OffsetLeft = -width - margin;
        control.OffsetRight = -margin;
        control.OffsetTop = margin;
        control.GrowHorizontal = Control.GrowDirection.Begin;
    }

    public static void PinBottomRight(Control control, float width, float margin)
    {
        control.AnchorLeft = control.AnchorRight = 1f;
        control.AnchorTop = control.AnchorBottom = 1f;
        control.OffsetLeft = -width - margin;
        control.OffsetRight = -margin;
        control.OffsetTop = control.OffsetBottom = -margin;
        control.GrowHorizontal = Control.GrowDirection.Begin;
        control.GrowVertical = Control.GrowDirection.Begin;
    }

    public static void PinBottomLeft(Control control, float width, float margin)
    {
        control.AnchorLeft = control.AnchorRight = 0f;
        control.AnchorTop = control.AnchorBottom = 1f;
        control.OffsetLeft = margin;
        control.OffsetRight = margin + width;
        control.OffsetTop = control.OffsetBottom = -margin;
        control.GrowVertical = Control.GrowDirection.Begin;
    }

    public static void PinTopCenter(Control control, float width, float margin)
    {
        control.AnchorLeft = control.AnchorRight = 0.5f;
        control.AnchorTop = control.AnchorBottom = 0f;
        control.OffsetLeft = -width * 0.5f;
        control.OffsetRight = width * 0.5f;
        control.OffsetTop = margin;
        control.GrowHorizontal = Control.GrowDirection.Both;
    }

    public static void PinBottomCenter(Control control, float margin)
    {
        control.AnchorLeft = control.AnchorRight = 0.5f;
        control.AnchorTop = control.AnchorBottom = 1f;
        control.OffsetTop = control.OffsetBottom = -margin;
        control.GrowHorizontal = Control.GrowDirection.Both;
        control.GrowVertical = Control.GrowDirection.Begin;
    }

    public static string Duration(float seconds) => $"{(int)seconds / 60}:{(int)seconds % 60:00}";

    private static StyleBoxFlat ButtonStyle(Color background, Color? border)
    {
        var style = new StyleBoxFlat { BgColor = background };
        style.SetCornerRadiusAll(8);
        if (border is { } edge)
        {
            style.BorderColor = edge;
            style.SetBorderWidthAll(2);
        }

        return style;
    }

    public static string Money(float amount) => amount < 0 ? $"−€{-amount:N0}" : $"€{amount:N0}";
}
