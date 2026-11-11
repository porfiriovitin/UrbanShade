using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using UrbanShade.Domain.Entities;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Geospatial;
using UrbanShade.Domain.Repositories;

namespace UrbanShade.Infraestructure.DataAcess.Repositories;

internal sealed class GreenAreaRepository : IGreenAreaRepository
{
    private readonly UrbanShadeDbContext _context;
    public GreenAreaRepository(UrbanShadeDbContext context) { _context = context; }
    public void AddRange(IEnumerable<GreenArea> areas) => _context.GreenAreas.AddRange(areas);
    public async Task<bool> ImportOpenStreetMap(GeoAnalysisJob job, OpenStreetMapSnapshot snapshot, CancellationToken cancellationToken)
    {
        if (_context.Database.CurrentTransaction is null) throw new InvalidOperationException("An import transaction is required.");
        /// :: Serialize imports per region and prevent an older request from replacing a newer snapshot.
        var accepted = await _context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "GeoRegions" SET "OpenStreetMapRequestedAt" = {job.CreatedAt}, "OpenStreetMapReferenceDate" = {snapshot.ReferenceDate}
            WHERE "Id" = {job.GeoRegionId} AND "Active" AND "BoundaryVersion" = {job.BoundaryVersion}
              AND ("OpenStreetMapRequestedAt" IS NULL OR "OpenStreetMapRequestedAt" <= {job.CreatedAt})
              AND ("OpenStreetMapReferenceDate" IS NULL OR "OpenStreetMapReferenceDate" <= {snapshot.ReferenceDate})
            """, cancellationToken);
        if (accepted == 0) return false;
        await _context.GreenAreaRegions.Where(link => link.GeoRegionId == job.GeoRegionId).ExecuteDeleteAsync(cancellationToken);
        foreach (var area in snapshot.Areas.OrderBy(area => area.ElementType).ThenBy(area => area.ElementId))
        {
            var geometry = area.Geometry.AsText();
            var intersects = await _context.Database.SqlQuery<bool>($"""
                SELECT ST_Intersects(ST_GeomFromText({geometry},4326), ST_GeomFromText({job.Boundary.AsText()},4326)) AS "Value"
                """).SingleAsync(cancellationToken);
            if (!intersects) continue;
            await _context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "GreenAreas" ("Id", "Active", "CreatedAt", "Name", "Geometry", "Source", "EstimatedAreaSquareMeters", "ReferenceDate", "OsmElementType", "OsmElementId", "ImportedAt", "SourceMetadata")
                VALUES ({Guid.NewGuid()}, true, now(), {area.Name}, ST_GeomFromText({geometry},4326), 'OpenStreetMap',
                    ST_Area(ST_GeomFromText({geometry},4326)::geography), {snapshot.ReferenceDate}, {area.ElementType}, {area.ElementId}, now(), CAST({area.Tags} AS jsonb))
                ON CONFLICT ("OsmElementType", "OsmElementId") WHERE "OsmElementType" IS NOT NULL AND "OsmElementId" IS NOT NULL
                DO UPDATE SET "Name" = EXCLUDED."Name", "Geometry" = EXCLUDED."Geometry", "EstimatedAreaSquareMeters" = EXCLUDED."EstimatedAreaSquareMeters",
                    "ReferenceDate" = EXCLUDED."ReferenceDate", "ImportedAt" = EXCLUDED."ImportedAt", "SourceMetadata" = EXCLUDED."SourceMetadata", "Active" = true
                WHERE "GreenAreas"."ReferenceDate" IS NULL OR "GreenAreas"."ReferenceDate" <= EXCLUDED."ReferenceDate"
                """, cancellationToken);
            await _context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "GreenAreaRegions" ("GreenAreaId", "GeoRegionId", "BoundaryVersion")
                SELECT "Id", {job.GeoRegionId}, {job.BoundaryVersion} FROM "GreenAreas" WHERE "OsmElementType" = {area.ElementType} AND "OsmElementId" = {area.ElementId}
                ON CONFLICT DO NOTHING
                """, cancellationToken);
        }
        return true;
    }
    public Task<List<GreenArea>> GetByBounds(Geometry bounds, int page, int pageSize, CancellationToken cancellationToken) =>
        _context.GreenAreas.AsNoTracking().Where(area => area.Active && area.Geometry.Intersects(bounds) &&
            (area.Source == GreenAreaSource.OpenStreetMap && _context.GreenAreaRegions.Any(link => link.GreenAreaId == area.Id &&
                _context.GeoRegions.Any(region => region.Id == link.GeoRegionId && region.Active && region.BoundaryVersion == link.BoundaryVersion)) || _context.SatelliteAnalyses.Any(analysis => analysis.Id == area.SatelliteAnalysisId &&
                _context.GeoRegions.Any(region => region.Id == analysis.GeoRegionId && region.Active && region.BoundaryVersion == analysis.BoundaryVersion) &&
                !_context.SatelliteAnalyses.Any(newer => newer.GeoRegionId == analysis.GeoRegionId && newer.BoundaryVersion == analysis.BoundaryVersion && newer.AnalysisType == GeoAnalysisType.Vegetation &&
                    (newer.ObservationEndAt > analysis.ObservationEndAt || (newer.ObservationEndAt == analysis.ObservationEndAt && newer.ProcessedAt > analysis.ProcessedAt))))))
        .OrderBy(area => area.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
}
