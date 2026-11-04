using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FlowLight.Core.Network;
using FlowLight.Core.Vehicles;

namespace FlowLight.Core.Simulation;

internal static class CollisionDetector
{
    public static IEnumerable<CrashReport> Detect(IEnumerable<Junction> junctions)
    {
        foreach (var junction in junctions)
        {
            if (junction.Reservations.Count < 2)
                continue;

            var occupants = junction.Reservations
                .Where(r => r.Key.Path == r.Value)
                .ToList();

            for (var i = 0; i < occupants.Count; i++)
            for (var j = i + 1; j < occupants.Count; j++)
            {
                var (a, aPath) = occupants[i];
                var (b, bPath) = occupants[j];
                if (a.Status == VehicleStatus.Crashed && b.Status == VehicleStatus.Crashed)
                    continue;
                if (!aPath.ConflictsWith(bPath) || !Overlaps(a, b))
                    continue;

                yield return new CrashReport(junction, a, b);
            }
        }
    }

    private static bool Overlaps(Vehicle a, Vehicle b)
    {
        var reach = (a.Type.Width + b.Type.Width + a.Type.Length + b.Type.Length) * 0.25f;
        return Vector2.DistanceSquared(a.Center, b.Center) < reach * reach;
    }
}
