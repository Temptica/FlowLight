using FlowLight.Core.Campaign;
using FlowLight.Core.Economy;
using FlowLight.Core.Editing;
using FlowLight.Core.Maps;
using FlowLight.Core.Simulation;

namespace FlowLight.Core;

public sealed class GameSession
{
    public const float StepSeconds = 1f / 30f;

    public GameSession(Scenario scenario, SimulationSettings? settings = null)
    {
        Scenario = scenario;
        Simulation = new TrafficSimulation(scenario.Network, new DemandProfile(0f, 0f, 1f, 1f), settings ?? new SimulationSettings());
        var treasury = new Treasury(scenario.Definition.StartingBudget);
        Economy = new CityEconomy(Simulation, treasury, new TaxPolicy());
        Campaign = new CampaignProgress(scenario.Definition, scenario.Network, Simulation);
        Editor = new CityEditor(scenario.Network, Simulation, treasury, Campaign);
        Campaign.Start();
    }

    public Scenario Scenario { get; }
    public TrafficSimulation Simulation { get; }
    public CityEconomy Economy { get; }
    public CampaignProgress Campaign { get; }
    public CityEditor Editor { get; }

    public void Step()
    {
        Simulation.Step(StepSeconds);
        Economy.Update(StepSeconds);
        Campaign.Update(StepSeconds);
    }
}
