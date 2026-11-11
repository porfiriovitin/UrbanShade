using System.Text.Json;

namespace UrbanShade.Communication.Responses.InstitutionalDashboard;

public sealed record InstitutionalAverageResponse(decimal? Value, int SampleCount);

public sealed record InstitutionalOverviewResponse
{
    public Guid OrganizationId { get; init; }
    public int TotalRegions { get; init; }
    public int TotalSensors { get; init; }
    public int ActiveSensors { get; init; }
    public int InactiveSensors { get; init; }
    public int MaintenanceSensors { get; init; }
    public InstitutionalAverageResponse AverageTemperature { get; init; } = new(null, 0);
    public InstitutionalAverageResponse AverageHeatIndex { get; init; } = new(null, 0);
    public InstitutionalAverageResponse AverageExposureIndex { get; init; } = new(null, 0);
    public double? AverageVegetationCoveragePercent { get; init; }
    public int VegetationSampleCount { get; init; }
    public double? VegetationSampleAreaSquareMeters { get; init; }
    public int CriticalRegions { get; init; }
    public int HeatIslandCandidates { get; init; }
    public int RegionsWithValidAnalysis { get; init; }
    public int StaleRegions { get; init; }
    public int RegionsWithInsufficientData { get; init; }
    public string ExposureScope { get; init; } = "MONITORED_REGIONS";
    public string AnalysisMode { get; init; } = "EXPERIMENTAL";
    public DateTime CalculatedAt { get; init; }
}

public sealed record InstitutionalDistributionResponse(int TotalRegions, int Low, int Moderate, int High, int Critical, int InsufficientData);

public sealed record InstitutionalRegionResponse
{
    public Guid RegionId { get; init; }
    public string Name { get; init; } = string.Empty;
    public JsonElement? Geometry { get; init; }
    public int BoundaryVersion { get; init; }
    public int SensorCount { get; init; }
    public InstitutionalAverageResponse? AverageTemperature { get; init; }
    public decimal? AverageHeatIndex { get; init; }
    public decimal? ExposureIndex { get; init; }
    public string ExposureLevel { get; init; } = "INSUFFICIENT_DATA";
    public string DataQuality { get; init; } = "INSUFFICIENT_DATA";
    public bool IsStale { get; init; }
    public decimal? VegetationCoveragePercent { get; init; }
    public decimal? MeanNdvi { get; init; }
    public decimal? SurfaceTemperature { get; init; }
    public decimal? SurfaceTemperatureAnomaly { get; init; }
    public bool? IsHeatIslandCandidate { get; init; }
    public string Priority { get; init; } = "UNDETERMINED";
    public string DataOrigin { get; init; } = "UNAVAILABLE";
    public string AnalysisMode { get; init; } = "EXPERIMENTAL";
    public string? AlgorithmVersion { get; init; }
    public DateTime? AnalyzedAt { get; init; }
    public DateTime? ValidUntil { get; init; }
    public JsonElement SourceMetadata { get; init; }
    public string Notice { get; init; } = "Experimental environmental screening for planning; not an official public health alert or a guarantee of intervention results.";
}

public sealed record InstitutionalSensorsResponse
{
    public int TotalSensors { get; init; }
    public int ActiveSensors { get; init; }
    public int InactiveSensors { get; init; }
    public int MaintenanceSensors { get; init; }
    public int SensorsWithCurrentData { get; init; }
    public int SensorsWithStaleData { get; init; }
    public int SensorsWithoutReadings { get; init; }
    public DateTime? LastSimulatedCommunication { get; init; }
    public string DataOrigin { get; init; } = "SIMULATED_SENSORS";
    public List<InstitutionalRegionSensorResponse> Regions { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalRegions { get; init; }
    public bool RegionCountsMayOverlap { get; init; } = true;
}

public sealed record InstitutionalRegionLinkResponse(Guid RegionId, string Name);

public sealed record InstitutionalRegionSensorResponse(Guid RegionId, string Name, int SensorCount);

public sealed record InstitutionalEnvironmentResponse
{
    public double? TotalAreaSquareMeters { get; init; }
    public double? VegetationSampleAreaSquareMeters { get; init; }
    public double? SurfaceSampleAreaSquareMeters { get; init; }
    public double? AverageVegetationCoveragePercent { get; init; }
    public double? AverageNdvi { get; init; }
    public double? AverageSurfaceTemperature { get; init; }
    public decimal? MaximumSurfaceTemperature { get; init; }
    public double? AverageSurfaceTemperatureAnomaly { get; init; }
    public int VegetationSampleCount { get; init; }
    public int SurfaceSampleCount { get; init; }
    public int AnomalySampleCount { get; init; }
    public int LowVegetationRegions { get; init; }
    public int HeatIslandCandidates { get; init; }
    public DateTime? FirstObservationAt { get; init; }
    public DateTime? LastObservationAt { get; init; }
    public string DataContext { get; init; } = "HISTORICAL_ENVIRONMENTAL_CONTEXT";
    public string OverlapRule { get; init; } = "LATEST_OBSERVATION_THEN_REGION_ID";
    public string AggregationMethod { get; init; } = "AREA_WEIGHTED_REGIONAL_ESTIMATES";
}

public sealed record InstitutionalTrendPointResponse(DateTime LocalDate, decimal? Value, int SampleCount, string DataQuality, string AlgorithmVersion, string Composition, string DataOrigin);
public sealed record InstitutionalTrendsResponse(Guid RegionId, string Metric, string Interval, string TimeZoneId, List<InstitutionalTrendPointResponse> Series)
{
    public string GapPolicy { get; init; } = "OMITTED_INTERVALS_HAVE_NO_OBSERVATIONS";
}
public sealed record InstitutionalMapResponse(string Type, List<InstitutionalFeatureResponse> Features);
public sealed record InstitutionalFeatureResponse(string Type, Guid Id, JsonElement Geometry, JsonElement Properties);
public sealed record InstitutionalExportResponse(byte[] Content, string ContentType, string FileName);
