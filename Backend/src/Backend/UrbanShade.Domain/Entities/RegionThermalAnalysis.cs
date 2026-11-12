using UrbanShade.Domain.Enums;
using UrbanShade.Domain.RegionThermalAnalysis;

namespace UrbanShade.Domain.Entities;

public sealed class RegionThermalAnalysis : EntityBase
{
    private RegionThermalAnalysis()
    {
    }

    public RegionThermalAnalysis(RegionAnalysisData data, ExposureInput input, ExposureResult result, string algorithmVersion, string inputSignature, string sourceMetadata, DateTime analyzedAt)
    {
        if (analyzedAt.Kind != DateTimeKind.Utc)
            throw new ArgumentException("AnalyzedAt must be UTC.", nameof(analyzedAt));

        GeoRegionId = data.Region.Id;
        BoundaryVersion = data.Region.BoundaryVersion;
        ExposureIndex = result.Index;
        ExposureLevel = result.Level;
        AverageHeatIndex = input.HeatIndex;
        SurfaceTemperature = data.Surface?.MeanValue;
        SurfaceTemperatureAnomaly = input.SurfaceAnomaly;
        MeanNdvi = data.Vegetation?.MeanValue;
        VegetationCoveragePercent = input.VegetationCoveragePercent;
        SensorCount = input.SensorCount;
        IsHeatIslandCandidate = result.IsHeatIslandCandidate;
        DataQuality = result.Quality;
        ConfidenceLevel = result.Confidence;
        DataOrigin = input.SensorCount > 0 ? input.HasSimulatedSensors ? "SIMULATED_SENSORS" : "SENSORS" : input.HeatIndex.HasValue ? "WEATHER_MODEL" : "UNAVAILABLE";
        AlgorithmVersion = algorithmVersion;
        InputSignature = inputSignature;
        SourceMetadata = sourceMetadata;
        AnalyzedAt = analyzedAt;
    }

    public Guid GeoRegionId { get; private set; }
    public int BoundaryVersion { get; private set; }
    public decimal? ExposureIndex { get; private set; }
    public ExposureLevel? ExposureLevel { get; private set; }
    public decimal? AverageHeatIndex { get; private set; }
    public decimal? SurfaceTemperature { get; private set; }
    public decimal? SurfaceTemperatureAnomaly { get; private set; }
    public decimal? MeanNdvi { get; private set; }
    public decimal? VegetationCoveragePercent { get; private set; }
    public int SensorCount { get; private set; }
    public bool? IsHeatIslandCandidate { get; private set; }
    public RegionDataQuality DataQuality { get; private set; }
    public ConfidenceLevel ConfidenceLevel { get; private set; }
    public string DataOrigin { get; private set; } = string.Empty;
    public string AlgorithmVersion { get; private set; } = string.Empty;
    public string InputSignature { get; private set; } = string.Empty;
    public string SourceMetadata { get; private set; } = "{}";
    public DateTime AnalyzedAt { get; private set; }
}
