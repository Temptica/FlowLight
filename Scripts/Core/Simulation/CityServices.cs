namespace FlowLight.Core.Simulation;

public sealed class CityServices
{
    public const float CrashClearSeconds = 75f;
    public const float CrashClearWithDispatchSeconds = 20f;

    public bool HasDispatch { get; internal set; }

    public float CrashClearTime => HasDispatch ? CrashClearWithDispatchSeconds : CrashClearSeconds;
}
