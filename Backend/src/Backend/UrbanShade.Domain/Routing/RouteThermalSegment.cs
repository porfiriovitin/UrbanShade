using NetTopologySuite.Geometries;
using UrbanShade.Domain.Enums;

namespace UrbanShade.Domain.Routing;

public sealed record RouteThermalSegment(int RouteIndex, int EdgeIndex, double Position, LineString Geometry, double DistanceMeters,
    Guid? GeoRegionId, Guid? AnalysisId, decimal? ExposureIndex, ExposureLevel? ExposureLevel, RegionDataQuality? DataQuality,
    ConfidenceLevel? ConfidenceLevel, string? DataOrigin, string? AlgorithmVersion, DateTime? AnalyzedAt, string? SourceMetadata);

public sealed record EvaluatedRouteSegment(RouteThermalSegment Segment, bool IsValid, string? Composition, DateTime? SourceReferenceAt);

public sealed record RouteExposure(RouteCandidate Route, IReadOnlyList<EvaluatedRouteSegment> Segments,
    double GeometryDistanceMeters, decimal? ExposureIndex, ExposureLevel? ExposureLevel, decimal ThermalCoveragePercent,
    double HighExposureDistanceMeters, decimal HighExposurePercent, int HighExposureSegmentCount,
    bool CanRecommendThermally, string Composition, bool HasSimulatedData, string DataQuality);

public sealed record RouteRecommendation(string RouteId, string Reason);
