using System;
using System.Linq;
using FlowLight.Core.Simulation;
using FlowLight.Core.Vehicles;

namespace FlowLight.Core.Economy;

public sealed class CityEconomy
{
    private const float RateSmoothing = 0.02f;
    private const float UpkeepBillingInterval = 10f;

    private readonly TrafficSimulation _simulation;
    private float _upkeepAccrued;
    private float _billingTimer;
    private float _incomeThisTick;

    public CityEconomy(TrafficSimulation simulation, Treasury treasury, TaxPolicy policy)
    {
        _simulation = simulation;
        Treasury = treasury;
        Policy = policy;

        simulation.VehicleArrived += CollectRoadTax;
        simulation.ViolationCommitted += CollectFine;
        simulation.CrashOccurred += ChargeCrash;
    }

    public Treasury Treasury { get; }
    public TaxPolicy Policy { get; }
    public float IncomePerMinute { get; private set; }
    public float UpkeepPerMinute { get; private set; }
    public int FinesIssued { get; private set; }

    public event Action<string>? Notice;

    public void Update(float deltaTime)
    {
        UpkeepPerMinute = _simulation.Network.Junctions.Sum(Policy.UpkeepPerMinute);
        _upkeepAccrued += UpkeepPerMinute / 60f * deltaTime;

        _billingTimer += deltaTime;
        if (_billingTimer >= UpkeepBillingInterval && _upkeepAccrued > 0f)
        {
            Treasury.Charge(_upkeepAccrued, "Running costs of lights and equipment");
            _upkeepAccrued = 0f;
            _billingTimer = 0f;
        }

        var instantRate = deltaTime > 0f ? _incomeThisTick / deltaTime * 60f : 0f;
        IncomePerMinute += (instantRate - IncomePerMinute) * Math.Min(1f, RateSmoothing * deltaTime);
        _incomeThisTick = 0f;
    }

    private void CollectRoadTax(Vehicle vehicle)
    {
        var tax = Policy.RoadTax(vehicle);
        _incomeThisTick += tax;
        Treasury.Earn(tax, "Road tax");
    }

    private void CollectFine(ViolationReport report)
    {
        if (!report.Caught)
            return;

        var fine = Policy.Fine(report.Kind);
        FinesIssued++;
        _incomeThisTick += fine;
        Treasury.Earn(fine, $"Fine at {report.Junction.Name}");
        if (report.Kind != ViolationKind.Speeding)
            Notice?.Invoke($"Camera at {report.Junction.Name} fined a driver +€{fine:0}");
    }

    private void ChargeCrash(CrashReport crash)
    {
        Treasury.Charge(Policy.CrashCost, $"Crash at {crash.Junction.Name}");
        Notice?.Invoke($"Crash at {crash.Junction.Name}! Clean-up −€{Policy.CrashCost:0}");
    }
}
