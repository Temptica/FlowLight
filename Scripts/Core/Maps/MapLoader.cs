using System;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using FlowLight.Core.Network;

namespace FlowLight.Core.Maps;

public sealed record Scenario(MapDefinition Definition, RoadNetwork Network);

public static class MapLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static MapDefinition Parse(string json) =>
        JsonSerializer.Deserialize<MapDefinition>(json, Options) ?? throw new InvalidOperationException("Map file is empty.");

    public static Scenario Load(string json)
    {
        var definition = Parse(json);
        var network = new RoadNetwork(definition.Name);

        foreach (var junction in definition.Junctions)
            network.AddJunction(new Junction(junction.Id, junction.Name, new Vector2(junction.X, junction.Y), junction.Gateway, junction.Weight));

        foreach (var road in definition.Roads)
        {
            network.AddRoad(new Road(
                road.Id,
                road.Name,
                network.Junction(road.From),
                network.Junction(road.To),
                road.Class,
                Math.Clamp(road.LanesForward ?? road.Lanes, 1, Road.MaxLanesPerDirection),
                Math.Clamp(road.LanesBackward ?? road.Lanes, 1, Road.MaxLanesPerDirection),
                road.SpeedKmh,
                road.Direction));
        }

        return new Scenario(definition, network);
    }
}
