using System.Text.Json;

namespace UrbanShade.Communication.Responses.Geo;

public sealed record GeoRegionResponse(Guid Id, Guid CityId, string Name, string? Description, JsonElement Boundary, bool IsActive, int BoundaryVersion, DateTime CreatedAt, DateTime? UpdatedAt);
public sealed record GeoJobResponse(Guid Id, Guid GeoRegionId, string Status, DateTime CreatedAt, DateTime? StartedAt, DateTime? FinishedAt, string? ErrorMessage, string? ResultSummary, int Attempts);
public sealed record SatelliteAnalysisResponse(Guid Id, Guid RegionId, string Source, string AnalysisType, decimal? MeanValue, decimal? MinValue, decimal? MaxValue,
    decimal? VegetationCoveragePercent, decimal? VegetationAreaSquareMeters, decimal ValidPixelPercent, DateTime ObservationStartAt, DateTime ObservationEndAt,
    DateTime ProcessedAt, string AlgorithmVersion, string DatasetId, int BoundaryVersion);
public sealed record VegetationResponse(Guid RegionId, string RegionName, string DataStatus, string? Source, decimal? MeanNdvi, decimal? MinimumNdvi, decimal? MaximumNdvi,
    decimal? VegetationCoveragePercent, decimal? VegetationAreaSquareMeters, decimal? ValidPixelPercent, DateTime? ObservationDate, DateTime? ProcessedAt);
public sealed record SurfaceTemperatureResponse(Guid RegionId, string DataStatus, string? Source, decimal? AverageTemperature, decimal? MinimumTemperature,
    decimal? MaximumTemperature, decimal? ValidPixelPercent, DateTime? ObservationDate, DateTime? ProcessedAt, string Unit = "CELSIUS");
