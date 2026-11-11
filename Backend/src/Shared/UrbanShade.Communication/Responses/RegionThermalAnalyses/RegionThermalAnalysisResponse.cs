using System.Text.Json;

namespace UrbanShade.Communication.Responses.RegionThermalAnalyses;

public sealed class RegionThermalAnalysisResponse
{
    public Guid Id { get; init; }
    public Guid RegionId { get; init; }
    public string RegionName { get; init; } = string.Empty;
    public int BoundaryVersion { get; init; }
    public decimal? ExposureIndex { get; init; }
    public string? ExposureLevel { get; init; }
    public decimal? AverageHeatIndex { get; init; }
    public decimal? SurfaceTemperature { get; init; }
    public decimal? SurfaceTemperatureAnomaly { get; init; }
    public decimal? MeanNdvi { get; init; }
    public decimal? VegetationCoveragePercent { get; init; }
    public int SensorCount { get; init; }
    public bool? IsHeatIslandCandidate { get; init; }
    public string DataQuality { get; init; } = string.Empty;
    public string ConfidenceLevel { get; init; } = string.Empty;
    public string DataOrigin { get; init; } = string.Empty;
    public string AnalysisMode { get; init; } = string.Empty;
    public string AlgorithmVersion { get; init; } = string.Empty;
    public DateTime AnalyzedAt { get; init; }
    public JsonElement SourceMetadata { get; init; }
}
