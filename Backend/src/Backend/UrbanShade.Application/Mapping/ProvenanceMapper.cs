using System.Text.Json;

namespace UrbanShade.Application.Mapping;

public static class ProvenanceMapper
{
    public static JsonElement PublicMetadata(string json)
    {
        /// :: Regional access exposes provenance without disclosing third-party sensor identifiers.
        var metadata = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? [];
        if (metadata.TryGetValue("sensors", out var sensors) && sensors.ValueKind == JsonValueKind.Array)
            metadata["sensorObservations"] = JsonSerializer.SerializeToElement(sensors.EnumerateArray().Select(sensor =>
                sensor.EnumerateObject().Where(property => property.Name is "MeasuredAt" or "AlgorithmVersion" or "IsSimulated")
                    .ToDictionary(property => property.Name, property => property.Value)));
        metadata.Remove("sensors");
        return JsonSerializer.SerializeToElement(metadata);
    }
}
