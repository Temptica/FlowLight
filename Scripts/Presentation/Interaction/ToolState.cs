using System;
using FlowLight.Core.Editing;
using FlowLight.Core.Network;

namespace FlowLight.Presentation.Interaction;

public sealed class ToolState
{
    public ToolKind Current { get; private set; } = ToolKind.Inspect;
    public Lane? EditedLane { get; private set; }

    public event Action? Changed;

    public void Select(ToolKind tool)
    {
        Current = tool;
        EditedLane = null;
        Changed?.Invoke();
    }

    public void EditLane(Lane? lane)
    {
        EditedLane = lane;
        Changed?.Invoke();
    }
}
