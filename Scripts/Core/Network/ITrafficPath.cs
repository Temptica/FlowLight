using System.Collections.Generic;
using FlowLight.Core.Geometry;
using FlowLight.Core.Vehicles;

namespace FlowLight.Core.Network;

public interface ITrafficPath
{
    Polyline Shape { get; }
    float Length { get; }
    float SpeedLimit { get; }
    bool IsRemoved { get; }
    List<Vehicle> Vehicles { get; }
}
