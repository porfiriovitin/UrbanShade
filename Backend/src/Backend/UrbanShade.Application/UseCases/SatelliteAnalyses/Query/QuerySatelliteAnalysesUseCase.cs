using System.Net;
using UrbanShade.Application.UseCases.GeoRegions;
using UrbanShade.Communication.Responses.Geo;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Geospatial;
using UrbanShade.Domain.Repositories;
using UrbanShade.Exceptions;

namespace UrbanShade.Application.UseCases.SatelliteAnalyses.Query;

public sealed class QuerySatelliteAnalysesUseCase : IQuerySatelliteAnalysesUseCase
{
    private readonly IGeoRegionRepository _regions;
    private readonly ISatelliteAnalysisRepository _analyses;
    private readonly GeoAnalysisSettings _settings;
    private readonly TimeProvider _time;
    public QuerySatelliteAnalysesUseCase(IGeoRegionRepository regions, ISatelliteAnalysisRepository analyses, GeoAnalysisSettings settings, TimeProvider time)
    { _regions = regions; _analyses = analyses; _settings = settings; _time = time; }
    private async Task<GeoRegion> Region(Guid id, CancellationToken cancellationToken) => await _regions.FindById(id, cancellationToken) ?? throw new GeoException(HttpStatusCode.NotFound, "Region not found.");
    private string Status(SatelliteAnalysis? analysis) => analysis is null ? "UNAVAILABLE" : analysis.ObservationEndAt < _time.GetUtcNow().UtcDateTime.AddDays(-_settings.OutdatedAfterDays) ? "OUTDATED" : "AVAILABLE";
    public static string Source(SatelliteDataSource source) => source switch { SatelliteDataSource.Sentinel2 => "SENTINEL2", SatelliteDataSource.Landsat8 => "LANDSAT8", _ => "LANDSAT9" };
    public async Task<VegetationResponse> Vegetation(Guid regionId, CancellationToken cancellationToken)
    {
        var region = await Region(regionId, cancellationToken);
        var analysis = await _analyses.GetLatest(regionId, region.BoundaryVersion, GeoAnalysisType.Vegetation, cancellationToken);
        return new(regionId, region.Name, Status(analysis), analysis is null ? null : Source(analysis.DataSource), analysis?.MeanValue, analysis?.MinValue, analysis?.MaxValue,
            analysis?.VegetationCoveragePercent, analysis?.VegetationAreaSquareMeters, analysis?.ValidPixelPercent, analysis?.ObservationEndAt, analysis?.ProcessedAt);
    }
    public async Task<SurfaceTemperatureResponse> Temperature(Guid regionId, CancellationToken cancellationToken)
    {
        var region = await Region(regionId, cancellationToken);
        var analysis = await _analyses.GetLatest(regionId, region.BoundaryVersion, GeoAnalysisType.SurfaceTemperature, cancellationToken);
        return new(regionId, Status(analysis), analysis is null ? null : Source(analysis.DataSource), analysis?.MeanValue, analysis?.MinValue, analysis?.MaxValue, analysis?.ValidPixelPercent, analysis?.ObservationEndAt, analysis?.ProcessedAt);
    }
    public async Task<List<SatelliteAnalysisResponse>> History(Guid regionId, DateTime? start, DateTime? end, int page, int pageSize, CancellationToken cancellationToken)
    {
        await Region(regionId, cancellationToken);
        GeoRegionValidator.Page(page, pageSize);
        if ((start.HasValue && start.Value.Kind != DateTimeKind.Utc) || (end.HasValue && end.Value.Kind != DateTimeKind.Utc) || (start.HasValue && end.HasValue && start >= end))
            throw new GeoException(HttpStatusCode.BadRequest, "Provide a valid UTC history period.");
        return (await _analyses.GetHistory(regionId, start, end, page, pageSize, cancellationToken)).Select(analysis => new SatelliteAnalysisResponse(analysis.Id, regionId, Source(analysis.DataSource),
            analysis.AnalysisType == GeoAnalysisType.Vegetation ? "VEGETATION" : "SURFACE_TEMPERATURE", analysis.MeanValue, analysis.MinValue, analysis.MaxValue,
            analysis.VegetationCoveragePercent, analysis.VegetationAreaSquareMeters, analysis.ValidPixelPercent, analysis.ObservationStartAt, analysis.ObservationEndAt,
            analysis.ProcessedAt, analysis.AlgorithmVersion, analysis.DatasetId, analysis.BoundaryVersion)).ToList();
    }
}
