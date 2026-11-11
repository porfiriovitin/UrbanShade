using System.Text.Json;

namespace UrbanShade.Communication.Responses.Routes;

public sealed class RouteComparisonResponse
{
    public string RecommendedRouteId { get; init; } = string.Empty;
    public string RecommendationReason { get; init; } = string.Empty;
    public List<RouteResponse> Routes { get; init; } = [];
    public string AnalysisMode { get; init; } = "ESTIMATED";
    public DateTime CalculatedAt { get; init; }
}

public sealed class RouteResponse
{
    public string Id { get; init; } = string.Empty;
    public double DistanceMeters { get; init; }
    public double GeometryDistanceMeters { get; init; }
    public double DurationSeconds { get; init; }
    public decimal? ExposureIndex { get; init; }
    public string? ExposureLevel { get; init; }
    public decimal ThermalCoveragePercent { get; init; }
    public double HighExposureDistanceMeters { get; init; }
    public decimal HighExposurePercent { get; init; }
    public int HighExposureSegmentCount { get; init; }
    public bool IsRecommended { get; init; }
    public bool IsConventional { get; init; }
    public bool CanRecommendThermally { get; init; }
    public string DataQuality { get; init; } = string.Empty;
    public bool HasSimulatedData { get; init; }
    public DateTime? SourceReferenceAt { get; init; }
    public string Provider { get; init; } = string.Empty;
    public string Attribution { get; init; } = string.Empty;
    public JsonElement Geometry { get; init; }
    public List<RouteSegmentResponse> Segments { get; init; } = [];
    public List<RouteInstructionResponse> Instructions { get; init; } = [];
}

public sealed class RouteSegmentResponse
{
    public Guid? GeoRegionId { get; init; }
    public Guid? AnalysisId { get; init; }
    public JsonElement Geometry { get; init; }
    public double DistanceMeters { get; init; }
    public decimal? ExposureIndex { get; init; }
    public string? ExposureLevel { get; init; }
    public string DataStatus { get; init; } = "UNKNOWN";
    public string? DataQuality { get; init; }
    public string? DataOrigin { get; init; }
    public string? ConfidenceLevel { get; init; }
    public DateTime? AnalyzedAt { get; init; }
    public DateTime? SourceReferenceAt { get; init; }
}

public sealed record RouteInstructionResponse(string Text, double DistanceMeters, double DurationSeconds, int StartPointIndex, int EndPointIndex);
