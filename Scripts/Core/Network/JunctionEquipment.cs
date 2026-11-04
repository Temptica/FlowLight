namespace FlowLight.Core.Network;

public sealed class JunctionEquipment
{
    public bool HasDetectors { get; internal set; }
    public bool HasCamera { get; internal set; }

    public float Enforcement => HasCamera ? 0.35f : 1f;
}
