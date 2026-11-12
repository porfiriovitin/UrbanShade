using System.Text.Json;
using System.Text.Json.Serialization;

namespace UrbanShade.Domain.RegionThermalAnalysis;

/// :: Version zero supports legacy provenance.
public sealed record AnalysisProvenance
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; }

    [JsonPropertyName("sensors")]
    public List<SensorProvenance> Sensors { get; init; } = [];

    [JsonPropertyName("weather")]
    public WeatherProvenance? Weather { get; init; }

    [JsonPropertyName("vegetation")]
    public SatelliteProvenance? Vegetation { get; init; }

    [JsonPropertyName("surface")]
    public SatelliteProvenance? Surface { get; init; }

    [JsonPropertyName("referenceSurface")]
    public SatelliteProvenance? ReferenceSurface { get; init; }

    [JsonPropertyName("referenceRegionId")]
    public Guid? ReferenceRegionId { get; init; }

    [JsonPropertyName("referenceBoundaryVersion")]
    public int? ReferenceBoundaryVersion { get; init; }

    [JsonPropertyName("scores")]
    public Dictionary<string, decimal> Scores { get; init; } = [];

    [JsonPropertyName("appliedWeights")]
    public Dictionary<string, decimal> AppliedWeights { get; init; } = [];

    [JsonPropertyName("parameters")]
    public RegionThermalAnalysisSettings? Parameters { get; init; }

    [JsonPropertyName("missingComponents")]
    public string[] MissingComponents { get; init; } = [];

    [JsonPropertyName("occupiedQuadrants")]
    public int OccupiedQuadrants { get; init; }

    [JsonPropertyName("confidenceRules")]
    public OperationalConfidence? ConfidenceRules { get; init; }

    [JsonPropertyName("experimental")]
    public bool Experimental { get; init; } = true;

    [JsonPropertyName("satelliteContext")]
    public string SatelliteContext { get; init; } = "HISTORICAL_ENVIRONMENTAL_CONTEXT";

    public static AnalysisProvenance? Read(string? json)
    {
        try
        {
            var value = JsonSerializer.Deserialize<AnalysisProvenance>(json ?? "{}");

            return value is { SchemaVersion: 0 or 1, Sensors: not null, Scores: not null, AppliedWeights: not null } && value.Sensors.All(sensor => sensor is not null) ? value : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

public sealed record SensorProvenance
{
    [JsonPropertyName("sensorId")]
    public Guid SensorId { get; init; }

    [JsonPropertyName("readingId")]
    public Guid ReadingId { get; init; }

    [JsonPropertyName("analysisId")]
    public Guid AnalysisId { get; init; }
    public DateTime MeasuredAt { get; init; }
    public string AlgorithmVersion { get; init; } = "";
    public bool IsSimulated { get; init; }
}

public sealed record WeatherProvenance
{
    public Guid Id { get; init; }
    public DateTime ObservedAt { get; init; }
    public string Source { get; init; } = "";
    public string? Model { get; init; }

    [JsonPropertyName("algorithmVersion")]
    public string AlgorithmVersion { get; init; } = "";
}

public sealed record SatelliteProvenance
{
    public Guid Id { get; init; }
    public Guid GeoRegionId { get; init; }
    public int BoundaryVersion { get; init; }

    [JsonPropertyName("source")]
    public string Source { get; init; } = "";
    public string ImageId { get; init; } = "";
    public string DatasetId { get; init; } = "";
    public DateTime ObservationStartAt { get; init; }
    public DateTime ObservationEndAt { get; init; }
    public decimal? ValidPixelPercent { get; init; }
    public string AlgorithmVersion { get; init; } = "";
}
