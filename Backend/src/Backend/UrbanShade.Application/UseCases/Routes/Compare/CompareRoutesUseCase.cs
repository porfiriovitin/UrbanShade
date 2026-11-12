using System.Data.Common;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using UrbanShade.Communication.Requests.Routes;
using UrbanShade.Communication.Responses.Routes;
using UrbanShade.Domain.Repositories;
using UrbanShade.Domain.Routing;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.Routes.Compare;

public sealed class CompareRoutesUseCase : ICompareRoutesUseCase
{
    private readonly IRoutingProvider _provider;
    private readonly IRouteGeometryRepository _geometries;
    private readonly RouteExposureCalculator _exposure;
    private readonly RouteRecommendationCalculator _recommendation;
    private readonly RoutingSettings _settings;
    private readonly TimeProvider _time;
    private readonly ILogger<CompareRoutesUseCase> _logger;

    public CompareRoutesUseCase(IRoutingProvider provider, IRouteGeometryRepository geometries, RouteExposureCalculator exposure, RouteRecommendationCalculator recommendation, RoutingSettings settings, TimeProvider time, ILogger<CompareRoutesUseCase> logger)
    {
        _provider = provider;
        _geometries = geometries;
        _exposure = exposure;
        _recommendation = recommendation;
        _settings = settings;
        _time = time;
        _logger = logger;
    }

    public async Task<RouteComparisonResponse> Execute(CompareRoutesRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request);
        if (!_settings.Enabled)
            throw new RoutingException(ErrorKind.Unavailable, "Routing integration is disabled.");

        var timer = Stopwatch.StartNew();

        /// :: Request routes through the authenticated backend.
        var (conventional, candidates) = await GetRoutes(request, cancellationToken);
        var (routes, segments) = await AnalyzeRoutes(conventional, candidates, cancellationToken);

        var now = _time.GetUtcNow().UtcDateTime;
        var exposures = CalculateExposures(routes, segments, now);
        var recommendation = _recommendation.Recommend(exposures);
        _logger.LogInformation("Compared {RouteCount} routes and {SegmentCount} segments in {DurationMs} ms; reason {Reason}, coverage {Coverage}, indices {Indices}", exposures.Count, exposures.Sum(item => item.Segments.Count), timer.ElapsedMilliseconds, recommendation.Reason, exposures.Select(item => item.ThermalCoveragePercent).ToArray(), exposures.Select(item => item.ExposureIndex).ToArray());

        return new RouteComparisonResponse
        {
            RecommendedRouteId = recommendation.RouteId,
            RecommendationReason = recommendation.Reason,
            CalculatedAt = now,
            AnalysisMode = exposures.Any(item => item.HasSimulatedData) ? "DEMONSTRATION" : "ESTIMATED",
            Routes = exposures.Select(item => ToResponse(item, recommendation.RouteId)).ToList()
        };
    }

    private async Task<(RouteCandidate Conventional, List<RouteCandidate> Routes)> GetRoutes(CompareRoutesRequest request, CancellationToken cancellationToken)
    {
        var candidates = await _provider.GetRoutes(new(request.Origin.Latitude, request.Origin.Longitude), new(request.Destination.Latitude, request.Destination.Longitude), cancellationToken);
        var conventional = candidates.SingleOrDefault(item => item.IsConventional) ?? throw new RoutingException(ErrorKind.InvalidUpstreamResponse, "The provider did not return a conventional walking route.");
        var ordered = new[] { conventional }.Concat(candidates.Where(item => !item.IsConventional)).Where(item => ValidRoute(item, request)).ToList();
        if (!ordered.Contains(conventional))
            throw new RoutingException(ErrorKind.Unprocessable, "The conventional route exceeds walking or geometry limits.");

        return (conventional, ordered);
    }

    private async Task<(IReadOnlyList<RouteCandidate> Routes, IReadOnlyList<RouteThermalSegment> Segments)> AnalyzeRoutes(RouteCandidate conventional, IReadOnlyList<RouteCandidate> ordered, CancellationToken cancellationToken)
    {
        IReadOnlyList<RouteCandidate> routes;
        IReadOnlyList<RouteThermalSegment> segments;

        try
        {
            routes = await _geometries.RemoveDuplicates(ordered, cancellationToken);
            segments = await _geometries.Analyze(routes, cancellationToken);
        }
        catch (Exception exception) when (exception is DbException or InvalidOperationException or ArgumentException)
        {
            /// :: Keep the conventional route if thermal analysis fails.
            _logger.LogWarning("Route spatial analysis unavailable; returning conventional route with unknown thermal data");
            routes = [conventional];
            segments = UnknownSegments(conventional);
        }

        return (routes, segments);
    }

    private List<RouteExposure> CalculateExposures(IReadOnlyList<RouteCandidate> routes, IReadOnlyList<RouteThermalSegment> segments, DateTime now)
    {
        var exposures = new List<RouteExposure>();

        for (var index = 0; index < routes.Count; index++)
        {
            var route = routes[index];

            try
            {
                var parts = segments.Where(item => item.RouteIndex == index).OrderBy(item => item.EdgeIndex).ThenBy(item => item.Position).ToList();
                var expectedLength = GeometryDistance(route.Geometry);
                if (parts.Count == 0 || parts.Count > _settings.MaximumSegments || Math.Abs(parts.Sum(item => item.DistanceMeters) - expectedLength) > Math.Max(1, expectedLength * .02))
                    throw new ArgumentException("Inconsistent spatial segmentation.");

                exposures.Add(_exposure.Calculate(route, parts, now));
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                _logger.LogWarning("Discarded invalid thermal segmentation for route {RouteId}", route.Id);
                if (route.IsConventional)
                    exposures.Add(_exposure.Calculate(route, UnknownSegments(route), now));
            }
        }

        return exposures;
    }

    private void Validate(CompareRoutesRequest request)
    {
        var result = new CompareRoutesValidator(_settings).Validate(request);
        if (!result.IsValid)
            throw new RoutingException(ErrorKind.Validation, [.. result.Errors.Select(error => error.ErrorMessage)]);
    }

    private bool ValidRoute(RouteCandidate route, CompareRoutesRequest request)
    {
        if (route.Geometry.SRID != 4326 || !route.Geometry.IsValid || route.Geometry.NumPoints < 2 || route.Geometry.NumPoints > _settings.MaximumGeometryPoints || !double.IsFinite(route.DistanceMeters) || route.DistanceMeters <= 0 || route.DistanceMeters > _settings.MaximumWalkingDistanceMeters || !double.IsFinite(route.DurationSeconds) || route.DurationSeconds <= 0 || GeometryDistance(route.Geometry) > _settings.MaximumWalkingDistanceMeters) return false;

        return CompareRoutesValidator.Distance(request.Origin.Latitude, request.Origin.Longitude, route.Geometry.StartPoint.Y, route.Geometry.StartPoint.X) <= _settings.MaximumSnapDistanceMeters && CompareRoutesValidator.Distance(request.Destination.Latitude, request.Destination.Longitude, route.Geometry.EndPoint.Y, route.Geometry.EndPoint.X) <= _settings.MaximumSnapDistanceMeters;
    }

    private static double GeometryDistance(LineString line) => Enumerable.Range(1, line.NumPoints - 1).Sum(index => CompareRoutesValidator.Distance(line.GetCoordinateN(index - 1).Y, line.GetCoordinateN(index - 1).X, line.GetCoordinateN(index).Y, line.GetCoordinateN(index).X));

    private static IReadOnlyList<RouteThermalSegment> UnknownSegments(RouteCandidate route) => Enumerable.Range(1, route.Geometry.NumPoints - 1).Select(index => new LineString([route.Geometry.GetCoordinateN(index - 1), route.Geometry.GetCoordinateN(index)]) { SRID = 4326 }).Where(line => line.Length > 0).Select((line, index) => new RouteThermalSegment(0, index, 0, line, GeometryDistance(line), null, null, null, null, null, null, null, null, null, null)).ToList();

    private static JsonElement Geometry(LineString line) => JsonSerializer.SerializeToElement(new
    {
        type = "LineString",
        coordinates = line.Coordinates.Select(point => new[] { point.X, point.Y })
    });

    private static RouteResponse ToResponse(RouteExposure exposure, string recommendedId) => new()
    {
        Id = exposure.Route.Id,
        DistanceMeters = exposure.Route.DistanceMeters,
        GeometryDistanceMeters = exposure.GeometryDistanceMeters,
        DurationSeconds = exposure.Route.DurationSeconds,
        ExposureIndex = exposure.ExposureIndex,
        ExposureLevel = exposure.ExposureLevel?.ToString(),
        ThermalCoveragePercent = decimal.Round(exposure.ThermalCoveragePercent, 2),
        HighExposureDistanceMeters = exposure.HighExposureDistanceMeters,
        HighExposurePercent = exposure.HighExposurePercent,
        HighExposureSegmentCount = exposure.HighExposureSegmentCount,
        IsRecommended = exposure.Route.Id == recommendedId,
        IsConventional = exposure.Route.IsConventional,
        CanRecommendThermally = exposure.CanRecommendThermally,
        DataQuality = exposure.DataQuality,
        HasSimulatedData = exposure.HasSimulatedData,
        SourceReferenceAt = exposure.Segments.Where(item => item.IsValid).Select(item => item.SourceReferenceAt).Min(),
        Provider = exposure.Route.Provider,
        Attribution = exposure.Route.Attribution,
        Geometry = Geometry(exposure.Route.Geometry),
        Instructions = exposure.Route.Instructions.Select(item => new RouteInstructionResponse(item.Text, item.DistanceMeters, item.DurationSeconds, item.StartPointIndex, item.EndPointIndex)).ToList(),
        Segments = exposure.Segments.Select(item => new RouteSegmentResponse
        {
            GeoRegionId = item.Segment.GeoRegionId,
            AnalysisId = item.Segment.AnalysisId,
            Geometry = Geometry(item.Segment.Geometry),
            DistanceMeters = item.Segment.DistanceMeters,
            ExposureIndex = item.IsValid ? item.Segment.ExposureIndex : null,
            ExposureLevel = item.IsValid ? item.Segment.ExposureLevel?.ToString() : null,
            DataStatus = item.IsValid ? "VALID" : "UNKNOWN",
            DataQuality = item.Segment.DataQuality?.ToString(),
            DataOrigin = item.Segment.DataOrigin,
            ConfidenceLevel = item.Segment.ConfidenceLevel?.ToString(),
            AnalyzedAt = item.Segment.AnalyzedAt,
            SourceReferenceAt = item.SourceReferenceAt
        }).ToList()
    };
}
