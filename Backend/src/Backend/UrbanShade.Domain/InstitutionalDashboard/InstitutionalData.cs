namespace UrbanShade.Domain.InstitutionalDashboard;

public sealed class InstitutionalRegionData
{
    public Guid RegionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int BoundaryVersion { get; set; }
    public string Geometry { get; set; } = "{}";
    public decimal? ExposureIndex { get; set; }
    public string ExposureLevel { get; set; } = "INSUFFICIENT_DATA";
    public string DataQuality { get; set; } = "INSUFFICIENT_DATA";
    public string DataOrigin { get; set; } = "UNAVAILABLE";
    public string? AlgorithmVersion { get; set; }
    public DateTime? AnalyzedAt { get; set; }
    public DateTime? ValidUntil { get; set; }
    public bool IsStale { get; set; }
    public bool IsCurrent { get; set; }
    public decimal? AverageHeatIndex { get; set; }
    public decimal? SurfaceTemperature { get; set; }
    public decimal? SurfaceTemperatureAnomaly { get; set; }
    public decimal? MeanNdvi { get; set; }
    public decimal? VegetationCoveragePercent { get; set; }
    public bool? IsHeatIslandCandidate { get; set; }
    public int SensorCount { get; set; }
    public string SourceMetadata { get; set; } = "{}";
}

public sealed class InstitutionalOverviewData
{
    public int TotalRegions { get; set; }
    public int RegionsWithValidAnalysis { get; set; }
    public int StaleRegions { get; set; }
    public int Low { get; set; }
    public int Moderate { get; set; }
    public int High { get; set; }
    public int Critical { get; set; }
    public int InsufficientData { get; set; }
    public int HeatIslandCandidates { get; set; }
    public decimal? AverageExposureIndex { get; set; }
    public int ExposureSampleCount { get; set; }
    public bool HasSimulatedData { get; set; }
    public DateTime? ValidUntil { get; set; }
}

public sealed class InstitutionalSensorData
{
    public int TotalSensors { get; set; }
    public int ActiveSensors { get; set; }
    public int InactiveSensors { get; set; }
    public int MaintenanceSensors { get; set; }
    public int SensorsWithCurrentData { get; set; }
    public int SensorsWithStaleData { get; set; }
    public int SensorsWithoutReadings { get; set; }
    public decimal? AverageTemperature { get; set; }
    public int TemperatureSampleCount { get; set; }
    public decimal? AverageHeatIndex { get; set; }
    public int HeatIndexSampleCount { get; set; }
    public DateTime? LastSimulatedCommunication { get; set; }
    public DateTime? ValidUntil { get; set; }
}

public sealed class InstitutionalEnvironmentData
{
    public double? TotalAreaSquareMeters { get; set; }
    public double? VegetationSampleAreaSquareMeters { get; set; }
    public double? SurfaceSampleAreaSquareMeters { get; set; }
    public double? AverageVegetationCoveragePercent { get; set; }
    public double? AverageNdvi { get; set; }
    public double? AverageSurfaceTemperature { get; set; }
    public decimal? MaximumSurfaceTemperature { get; set; }
    public double? AverageSurfaceTemperatureAnomaly { get; set; }
    public int VegetationSampleCount { get; set; }
    public int SurfaceSampleCount { get; set; }
    public int AnomalySampleCount { get; set; }
    public int LowVegetationRegions { get; set; }
    public int HeatIslandCandidates { get; set; }
    public DateTime? FirstObservationAt { get; set; }
    public DateTime? LastObservationAt { get; set; }
    public DateTime? ValidUntil { get; set; }
}

public sealed class InstitutionalTrendData
{
    public DateTime Date { get; set; }
    public decimal? Value { get; set; }
    public int SampleCount { get; set; }
    public string AlgorithmVersion { get; set; } = string.Empty;
    public string Composition { get; set; } = string.Empty;
    public string DataOrigin { get; set; } = string.Empty;
    public string DataQuality { get; set; } = string.Empty;
}

public sealed class InstitutionalFeatureData
{
    public Guid Id { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Geometry { get; set; } = "{}";
    public string Properties { get; set; } = "{}";
    public int CoordinateCount { get; set; }
}

public sealed class InstitutionalReportData
{
    public Guid RegionId { get; set; }
    public string RegionName { get; set; } = string.Empty;
    public DateTime AnalyzedAt { get; set; }
    public decimal? ExposureIndex { get; set; }
    public string? ExposureLevel { get; set; }
    public decimal? HeatIndex { get; set; }
    public decimal? SurfaceTemperature { get; set; }
    public decimal? VegetationCoveragePercent { get; set; }
    public string AlgorithmVersion { get; set; } = string.Empty;
    public int BoundaryVersion { get; set; }
    public string DataQuality { get; set; } = string.Empty;
    public string DataOrigin { get; set; } = string.Empty;
    public string SourceMetadata { get; set; } = "{}";
}
