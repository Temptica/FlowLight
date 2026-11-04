using System;

namespace FlowLight.Core.Vehicles;

public sealed record VehicleType(string Name, float Length, float Width, float Height, float MaxAcceleration, float ComfortableDeceleration, float MaxSpeed = 50f)
{
    public static readonly VehicleType Car = new("Car", 4.5f, 1.8f, 1.5f, 2.6f, 2.8f);
    public static readonly VehicleType Van = new("Van", 5.6f, 2.0f, 2.2f, 1.8f, 2.4f);
    public static readonly VehicleType Truck = new("Truck", 9.5f, 2.5f, 3.2f, 1.1f, 2.0f);
    public static readonly VehicleType Bicycle = new("Bicycle", 1.8f, 0.7f, 1.7f, 1.0f, 2.0f, 5.5f);

    public bool IsBicycle => this == Bicycle;

    public static VehicleType Pick(Random random, float cyclistShare)
    {
        if (random.NextSingle() < cyclistShare)
            return Bicycle;

        return random.NextSingle() switch
        {
            < 0.82f => Car,
            < 0.95f => Van,
            _ => Truck
        };
    }
}
