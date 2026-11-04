using Godot;

namespace FlowLight.Presentation.Interaction;

public partial class CameraRig : Node3D
{
    private const float MinDistance = 40f;
    private const float MaxDistance = 1400f;
    private const float MinPitch = 35f;
    private const float MaxPitch = 89f;
    private const float PanSpeed = 0.9f;
    private const float RotateSpeed = 70f;

    private Camera3D _camera = null!;
    private float _distance = 700f;
    private float _pitch = 72f;
    private float _yaw;

    public override void _Ready()
    {
        _camera = new Camera3D { Fov = 50f, Far = 5000f, Current = true };
        AddChild(_camera);
        ApplyTransform();
    }

    public void Frame(Rect2 bounds)
    {
        Position = new Vector3(bounds.GetCenter().X, 0f, bounds.GetCenter().Y);
        _distance = Mathf.Clamp(Mathf.Max(bounds.Size.X, bounds.Size.Y) * 0.55f, MinDistance, MaxDistance);
        ApplyTransform();
    }

    public void Focus(Vector3 target, float distance, bool animate = true)
    {
        distance = Mathf.Clamp(distance, MinDistance, MaxDistance);
        if (!animate || _camera is null)
        {
            Position = target;
            _distance = distance;
            ApplyTransform();
            return;
        }

        var tween = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(this, "position", target, 0.8f);
        tween.TweenMethod(Callable.From<float>(value => _distance = value), _distance, distance, 0.8f);
    }

    public Vector3? ScreenToGround(Vector2 screenPosition)
    {
        var origin = _camera.ProjectRayOrigin(screenPosition);
        var direction = _camera.ProjectRayNormal(screenPosition);
        if (Mathf.Abs(direction.Y) < 1e-4f)
            return null;

        var distance = -origin.Y / direction.Y;
        return distance < 0f ? null : origin + direction * distance;
    }

    public override void _Process(double delta)
    {
        var step = (float)delta;
        var move = Vector2.Zero;
        if (Godot.Input.IsKeyPressed(Key.W) || Godot.Input.IsKeyPressed(Key.Up)) move.Y -= 1f;
        if (Godot.Input.IsKeyPressed(Key.S) || Godot.Input.IsKeyPressed(Key.Down)) move.Y += 1f;
        if (Godot.Input.IsKeyPressed(Key.A) || Godot.Input.IsKeyPressed(Key.Left)) move.X -= 1f;
        if (Godot.Input.IsKeyPressed(Key.D) || Godot.Input.IsKeyPressed(Key.Right)) move.X += 1f;
        if (move != Vector2.Zero)
            Pan(move.Normalized() * _distance * PanSpeed * step);

        if (Godot.Input.IsKeyPressed(Key.Q)) _yaw += RotateSpeed * step;
        if (Godot.Input.IsKeyPressed(Key.E)) _yaw -= RotateSpeed * step;
        if (Godot.Input.IsKeyPressed(Key.R)) _pitch = Mathf.Clamp(_pitch + RotateSpeed * 0.5f * step, MinPitch, MaxPitch);
        if (Godot.Input.IsKeyPressed(Key.F)) _pitch = Mathf.Clamp(_pitch - RotateSpeed * 0.5f * step, MinPitch, MaxPitch);
        ApplyTransform();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp }:
                _distance = Mathf.Max(MinDistance, _distance * 0.88f);
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown }:
                _distance = Mathf.Min(MaxDistance, _distance / 0.88f);
                break;
            case InputEventMouseMotion motion when (motion.ButtonMask & (MouseButtonMask.Right | MouseButtonMask.Middle)) != 0:
                Pan(-motion.Relative * _distance * 0.0016f);
                break;
        }
    }

    private void Pan(Vector2 screenDelta)
    {
        var yaw = Mathf.DegToRad(_yaw);
        var right = new Vector3(Mathf.Cos(yaw), 0f, -Mathf.Sin(yaw));
        var forward = new Vector3(-Mathf.Sin(yaw), 0f, -Mathf.Cos(yaw));
        Position += right * screenDelta.X - forward * screenDelta.Y;
    }

    private void ApplyTransform()
    {
        if (_camera is null)
            return;

        Rotation = new Vector3(0f, Mathf.DegToRad(_yaw), 0f);
        var pitch = Mathf.DegToRad(_pitch);
        _camera.Position = new Vector3(0f, Mathf.Sin(pitch) * _distance, Mathf.Cos(pitch) * _distance);
        _camera.Rotation = new Vector3(-pitch, 0f, 0f);
    }
}
