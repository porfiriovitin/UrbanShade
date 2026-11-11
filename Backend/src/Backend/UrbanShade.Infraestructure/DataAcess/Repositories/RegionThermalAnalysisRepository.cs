using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NetTopologySuite.Geometries;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Geospatial;
using UrbanShade.Domain.RegionThermalAnalysis;
using UrbanShade.Domain.Repositories;
using UrbanShade.Domain.ThermalAnalysis;

namespace UrbanShade.Infraestructure.DataAcess.Repositories;

internal sealed class RegionThermalAnalysisRepository : IRegionThermalAnalysisRepository
{
    private readonly UrbanShadeDbContext _context;
    private readonly RegionThermalAnalysisSettings _settings;
    private readonly GeoAnalysisSettings _geo;
    private readonly ThermalAnalysisSettings _thermal;

    public RegionThermalAnalysisRepository(UrbanShadeDbContext context, RegionThermalAnalysisSettings settings, GeoAnalysisSettings geo, ThermalAnalysisSettings thermal)
    {
        _context = context;
        _settings = settings;
        _geo = geo;
        _thermal = thermal;
    }

    public async Task<IRegionAnalysisTransaction?> Acquire(Guid regionId, CancellationToken cancellationToken)
    {
        var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var acquired = await _context.Database.SqlQuery<bool>($"SELECT pg_try_advisory_xact_lock(hashtextextended({"region-thermal:" + regionId}, 0)) AS \"Value\"").SingleAsync(cancellationToken);
        if (acquired) return new ProcessingTransaction(transaction);
        await transaction.DisposeAsync();
        return null;
    }

    public async Task<RegionAnalysisData?> GetData(Guid regionId, DateTime now, CancellationToken cancellationToken)
    {
        /// :: Hold shared region locks until the analysis is saved, including its reference polygon.
        var regions = await _context.GeoRegions.FromSqlInterpolated($"""
            SELECT * FROM "GeoRegions" WHERE "Id" = {regionId}
            OR "Id" = (SELECT "ReferenceGeoRegionId" FROM "GeoRegions" WHERE "Id" = {regionId})
            ORDER BY "Id" FOR SHARE
            """).AsNoTracking().ToListAsync(cancellationToken);
        var region = regions.SingleOrDefault(item => item.Id == regionId && item.Active);
        if (region is null) return null;
        var reference = regions.SingleOrDefault(item => item.Id == region.ReferenceGeoRegionId && item.Active);
        var sensors = await _context.Sensors.FromSqlInterpolated($"""
            SELECT s.* FROM "Sensors" s JOIN "GeoRegions" r ON r."Id" = {regionId}
            WHERE s."Active" AND s."DeletedAt" IS NULL AND s."Status" = 'ACTIVE'
            AND ST_Covers(r."Boundary", ST_SetSRID(ST_MakePoint(s."Longitude", s."Latitude"),4326))
            """).AsNoTracking().ToListAsync(cancellationToken);
        var ids = sensors.Select(item => item.Id).ToList();
        var cutoff = now.AddMinutes(-Math.Min(_settings.SourceValidity.SensorMinutes, _thermal.StaleDataThresholdMinutes));
        var readings = await (from analysis in _context.ThermalAnalyses.AsNoTracking()
            join reading in _context.SensorReadings.AsNoTracking() on analysis.SensorReadingId equals reading.Id
            where analysis.Active && reading.Active && analysis.AlgorithmVersion == _thermal.AlgorithmVersion && ids.Contains(reading.SensorId)
                && reading.MeasuredAt >= cutoff && reading.MeasuredAt <= now
            select new { Analysis = analysis, Reading = reading }).GroupBy(item => item.Reading.SensorId)
            .Select(group => group.OrderByDescending(item => item.Reading.MeasuredAt).ThenByDescending(item => item.Reading.Id).First()).ToListAsync(cancellationToken);
        /// :: The current ingestion flow generates simulated readings only.
        var points = readings.Select(item => new RegionSensorData(sensors.Single(sensor => sensor.Id == item.Reading.SensorId), item.Reading, item.Analysis, true)).OrderBy(item => item.Sensor.Id).ToList();
        var weatherCutoff = now.AddMinutes(-_settings.SourceValidity.WeatherMinutes);
        var weather = points.Count > 0 ? null : await _context.WeatherSnapshots.AsNoTracking()
            .Where(item => item.GeoRegionId == regionId && item.BoundaryVersion == region.BoundaryVersion && item.Active && item.ObservedAt >= weatherCutoff && item.ObservedAt <= now && item.Temperature != null && item.Humidity >= 0 && item.Humidity <= 100 && item.Temperature >= -273.15m)
            .OrderByDescending(item => item.ObservedAt).ThenByDescending(item => item.FetchedAt).FirstOrDefaultAsync(cancellationToken);
        var vegetation = await ValidSatellite(region, GeoAnalysisType.Vegetation, now.AddDays(-_settings.SourceValidity.VegetationDays), now)
            .Where(item => item.VegetationCoveragePercent >= 0 && item.VegetationCoveragePercent <= 100 && item.MeanValue >= -1 && item.MeanValue <= 1)
            .OrderByDescending(item => item.ObservationEndAt).ThenByDescending(item => item.ProcessedAt).FirstOrDefaultAsync(cancellationToken);
        var surfaces = ValidSatellite(region, GeoAnalysisType.SurfaceTemperature, now.AddDays(-_settings.SourceValidity.SurfaceTemperatureDays), now);
        SatelliteAnalysis? surface = null;
        SatelliteAnalysis? referenceSurface = null;
        if (reference is not null)
        {
            var references = ValidSatellite(reference, GeoAnalysisType.SurfaceTemperature, now.AddDays(-_settings.SourceValidity.SurfaceTemperatureDays), now);
            var pair = await (from item in surfaces join other in references on new { item.ImageId, item.DatasetId, item.DataSource, item.ObservationEndAt } equals new { other.ImageId, other.DatasetId, other.DataSource, other.ObservationEndAt }
                where item.ImageId != "" orderby item.ObservationEndAt descending, item.ProcessedAt descending
                select new { Surface = item, Reference = other }).FirstOrDefaultAsync(cancellationToken);
            surface = pair?.Surface;
            referenceSurface = pair?.Reference;
        }
        surface ??= await surfaces.OrderByDescending(item => item.ObservationEndAt).ThenByDescending(item => item.ProcessedAt).FirstOrDefaultAsync(cancellationToken);
        return new(region, points, weather, vegetation, surface, reference, referenceSurface);
    }

    private IQueryable<SatelliteAnalysis> ValidSatellite(GeoRegion region, GeoAnalysisType type, DateTime cutoff, DateTime now) =>
        _context.SatelliteAnalyses.AsNoTracking().Where(item => item.Active && item.GeoRegionId == region.Id && item.BoundaryVersion == region.BoundaryVersion && item.AnalysisType == type &&
            item.ObservationEndAt >= cutoff && item.ObservationEndAt <= now && item.MeanValue != null && item.ValidPixelPercent >= (decimal)_geo.MinimumValidPixelPercent && item.ValidPixelPercent <= 100);

    public void Add(RegionThermalAnalysis analysis) => _context.RegionThermalAnalyses.Add(analysis);

    public Task<RegionThermalAnalysis?> GetLatest(Guid regionId, int? boundaryVersion, CancellationToken cancellationToken) =>
        _context.RegionThermalAnalyses.AsNoTracking().Where(item => item.GeoRegionId == regionId && (!boundaryVersion.HasValue || item.BoundaryVersion == boundaryVersion))
            .OrderByDescending(item => item.AnalyzedAt).ThenByDescending(item => item.Id).FirstOrDefaultAsync(cancellationToken);

    public async Task<(List<RegionThermalAnalysis> Items, int Total)> GetHistory(Guid regionId, DateTime? start, DateTime? end, ExposureLevel? level, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _context.RegionThermalAnalyses.AsNoTracking().Where(item => item.GeoRegionId == regionId && (!start.HasValue || item.AnalyzedAt >= start) && (!end.HasValue || item.AnalyzedAt <= end) && (!level.HasValue || item.ExposureLevel == level));
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(item => item.AnalyzedAt).ThenByDescending(item => item.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<List<(GeoRegion Region, RegionThermalAnalysis Analysis)>> GetLatestRegions(List<Guid>? ids, Geometry? bounds, decimal? minIndex, ExposureLevel? level, bool includePartial, bool ranking, int take, CancellationToken cancellationToken)
    {
        var query = from region in _context.GeoRegions.AsNoTracking()
            where region.Active && (ids == null || ids.Contains(region.Id)) && (bounds == null || region.Boundary.Intersects(bounds))
            from analysis in _context.RegionThermalAnalyses.Where(item => item.GeoRegionId == region.Id && item.BoundaryVersion == region.BoundaryVersion).OrderByDescending(item => item.AnalyzedAt).ThenByDescending(item => item.Id).Take(1)
            select new { Region = region, Analysis = analysis };
        query = query.Where(item => (!ranking || item.Analysis.ExposureIndex != null) && (!minIndex.HasValue || item.Analysis.ExposureIndex >= minIndex) && (!level.HasValue || item.Analysis.ExposureLevel == level) && (includePartial || item.Analysis.DataQuality == RegionDataQuality.COMPLETE));
        var ordered = ranking ? query.OrderByDescending(item => item.Analysis.ExposureIndex).ThenBy(item => item.Region.Id) : query.OrderBy(item => item.Region.Id);
        var items = await ordered.Take(take).ToListAsync(cancellationToken);
        return items.Select(item => (item.Region, item.Analysis)).ToList();
    }

    public Task<List<Guid>> GetActiveRegionIds(Guid? after, int take, CancellationToken cancellationToken) =>
        _context.GeoRegions.AsNoTracking().Where(item => item.Active && (!after.HasValue || item.Id.CompareTo(after.Value) > 0)).OrderBy(item => item.Id).Take(take).Select(item => item.Id).ToListAsync(cancellationToken);

    private sealed class ProcessingTransaction(IDbContextTransaction transaction) : IRegionAnalysisTransaction
    {
        public Task Commit(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
