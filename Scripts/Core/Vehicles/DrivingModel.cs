using System;

namespace FlowLight.Core.Vehicles;

public static class DrivingModel
{
    public const float EmergencyDeceleration = 8f;
    private const float BrakingOnset = 0.7f;

    public static float FreeRoad(Vehicle vehicle, float desiredSpeed)
    {
        var maxAcceleration = MaxAcceleration(vehicle);
        if (desiredSpeed <= 0.1f)
            return -vehicle.Type.ComfortableDeceleration;

        var ratio = vehicle.Speed / desiredSpeed;
        return maxAcceleration * (1f - ratio * ratio * ratio * ratio);
    }

    public static float Following(Vehicle vehicle, float desiredSpeed, float gap, float leaderSpeed)
    {
        var maxAcceleration = MaxAcceleration(vehicle);
        var comfortable = vehicle.Type.ComfortableDeceleration;
        var speed = vehicle.Speed;
        var approachRate = speed - leaderSpeed;
        var desiredGap = vehicle.Driver.MinimumGap
                         + Math.Max(0f, speed * vehicle.Driver.TimeHeadway + speed * approachRate / (2f * MathF.Sqrt(maxAcceleration * comfortable)));
        var effectiveGap = Math.Max(gap, 0.1f);
        var interaction = desiredGap / effectiveGap;
        return FreeRoad(vehicle, desiredSpeed) - maxAcceleration * interaction * interaction;
    }

    public static float ApproachSpeed(Vehicle vehicle, float targetSpeed, float distance)
    {
        if (vehicle.Speed <= targetSpeed)
            return float.MaxValue;

        var required = (vehicle.Speed * vehicle.Speed - targetSpeed * targetSpeed) / (2f * Math.Max(distance, 0.5f));
        return required < vehicle.Type.ComfortableDeceleration * BrakingOnset ? float.MaxValue : -required;
    }

    public static float StoppingDistance(Vehicle vehicle, float deceleration) =>
        vehicle.Speed * vehicle.Speed / (2f * deceleration);

    public static float Clamp(float acceleration) => Math.Clamp(acceleration, -EmergencyDeceleration, 4f);

    private static float MaxAcceleration(Vehicle vehicle) => vehicle.Type.MaxAcceleration * vehicle.Driver.AccelerationFactor;
}
