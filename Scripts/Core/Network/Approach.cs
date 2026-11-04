using System.Collections.Generic;
using System.Numerics;
using FlowLight.Core.Geometry;

namespace FlowLight.Core.Network;

public sealed class Approach
{
    public Approach(Road road, IReadOnlyList<Lane> lanes)
    {
        Road = road;
        Lanes = lanes;
        Heading = lanes[0].Heading;
    }

    public Road Road { get; }
    public IReadOnlyList<Lane> Lanes { get; }
    public Vector2 Heading { get; }

    public bool IsToTheRightOf(Approach other) => VectorMath.Cross(other.Heading, -Heading) > 0.3f;

    public bool IsOpposite(Approach other) => Vector2.Dot(Heading, other.Heading) < -0.7f;
}
