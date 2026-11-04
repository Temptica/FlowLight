using System.Linq;
using FlowLight.Core;
using FlowLight.Core.Campaign;
using FlowLight.Core.Editing;
using FlowLight.Core.Maps;
using FlowLight.Core.Simulation;
using FlowLight.Presentation.Interaction;
using FlowLight.Presentation.Interface;
using FlowLight.Presentation.Rendering;
using Godot;

namespace FlowLight.Presentation;

public partial class GameRoot : Node3D
{
    [Export(PropertyHint.File, "*.json")]
    public string MapPath { get; set; } = "res://Data/Maps/riverside.json";

    private const float FocusDistance = 140f;

    private readonly SimulationClock _clock = new(GameSession.StepSeconds);
    private readonly SelectionState _selection = new();
    private readonly ToolState _tools = new();
    private GameSession _session = null!;
    private CameraRig _camera = null!;
    private RoadView _roads = null!;
    private JunctionView _junctions = null!;
    private LevelOverlay _overlay = null!;
    private NoticeFeed _notices = null!;
    private bool _layoutDirty;

    public override void _Ready()
    {
        var scenario = MapLoader.Load(FileAccess.GetFileAsString(MapPath));
        _session = new GameSession(scenario);

        AddChild(CreateEnvironment());
        AddChild(new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-55f, 35f, 0f),
            ShadowEnabled = true,
            LightEnergy = 0.75f,
            DirectionalShadowMaxDistance = 1500f
        });

        Add(new TerrainView()).Build(scenario.Definition, Bounds(scenario));
        _roads = Add(new RoadView());
        _roads.Initialize(scenario.Network);
        _junctions = Add(new JunctionView());
        _junctions.Initialize(scenario.Network);
        Add(new VehicleView()).Initialize(_session.Simulation);
        Add(new PedestrianView()).Initialize(_session.Simulation.Pedestrians);
        _overlay = Add(new LevelOverlay());
        _overlay.Bind(_session, _tools);

        var highlight = Add(new SelectionHighlight());
        _selection.Changed += highlight.Show;

        _camera = Add(new CameraRig());
        BuildInterface();
        FocusOn(_session.Campaign.Current, animate: false);

        scenario.Network.TopologyChanged += () => _layoutDirty = true;
        _session.Editor.Changed += () => _layoutDirty = true;
        _session.Campaign.LevelStarted += level =>
        {
            _tools.Select(ToolKind.Inspect);
            FocusOn(level, animate: true);
            _notices.Push($"New junction: {level.Title}. Cars from here now drive on to {level.Title}.");
        };
        _session.Campaign.LevelCompleted += level => _notices.Push($"{level.Title} is flowing! Move on whenever you are ready.");
    }

    public override void _Process(double delta)
    {
        var steps = _clock.ConsumeSteps(delta);
        for (var i = 0; i < steps; i++)
            _session.Step();

        if (!_layoutDirty)
            return;

        _layoutDirty = false;
        _roads.Rebuild();
        _junctions.Rebuild();
        _overlay.Rebuild();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
            return;

        switch (key.Keycode)
        {
            case Key.Space:
                _clock.IsPaused = !_clock.IsPaused;
                break;
            case Key.Key1:
                SetSpeed(1f);
                break;
            case Key.Key2:
                SetSpeed(4f);
                break;
            case Key.Key3:
                SetSpeed(8f);
                break;
        }
    }

    private void BuildInterface()
    {
        var ui = Add(new CanvasLayer());
        var card = new LevelCard();
        ui.AddChild(card);
        card.Bind(_session);
        card.FocusRequested += level => FocusOn(level, animate: true);

        var clock = new ClockPanel();
        ui.AddChild(clock);
        clock.Bind(_session, _clock);

        var info = new InfoCard();
        ui.AddChild(info);
        info.Bind(_session, _selection);

        var toolbar = new Toolbar();
        ui.AddChild(toolbar);
        toolbar.Bind(_session, _tools);

        var signs = new SignPicker();
        ui.AddChild(signs);

        _notices = new NoticeFeed();
        ui.AddChild(_notices);

        var controller = Add(new ToolController());
        controller.Bind(_session, _camera, _tools, _selection, signs);

        controller.Edited += ShowResult;
        toolbar.Edited += ShowResult;
        info.Edited += ShowResult;
        _session.Economy.Notice += _notices.Push;
        _notices.Push($"Welcome to {_session.Scenario.Definition.Name}! Fix one junction at a time. Hover a button to see what it does.");
    }

    private void ShowResult(EditResult result) => _notices.Push(result.Message, !result.Success);

    private void FocusOn(Level level, bool animate) =>
        _camera.Focus(level.Junction.Position.ToWorld(), FocusDistance, animate);

    private void SetSpeed(float speed)
    {
        _clock.SpeedMultiplier = speed;
        _clock.IsPaused = false;
    }

    private T Add<T>(T node) where T : Node
    {
        AddChild(node);
        return node;
    }

    private static WorldEnvironment CreateEnvironment() => new()
    {
        Environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = Palette.Sky,
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color("#c9d6e3"),
            AmbientLightEnergy = 0.35f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic
        }
    };

    private static Rect2 Bounds(Scenario scenario)
    {
        var points = scenario.Network.Junctions.Select(j => j.Position.ToGodot()).ToList();
        var rect = new Rect2(points[0], Vector2.Zero);
        foreach (var point in points)
            rect = rect.Expand(point);
        return rect;
    }
}
