using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using UrbanShade.Domain.Enums;
using UrbanShade.Domain.Repositories;
using UrbanShade.Domain.Routing;

namespace UrbanShade.Infraestructure.DataAcess.Repositories;

internal sealed class RouteGeometryRepository : IRouteGeometryRepository
{
    private readonly UrbanShadeDbContext _context;
    private readonly RoutingSettings _settings;

    public RouteGeometryRepository(UrbanShadeDbContext context, RoutingSettings settings)
    {
        _context = context;
        _settings = settings;
    }

    public async Task<IReadOnlyList<RouteCandidate>> RemoveDuplicates(IReadOnlyList<RouteCandidate> routes, CancellationToken cancellationToken)
    {
        var json = Serialize(routes);
        var pairs = await _context.Database.SqlQuery<DuplicatePair>($"""
            WITH routes AS (
                SELECT (r->>'index')::int AS id, ST_SetSRID(ST_GeomFromGeoJSON(r->'geometry'),4326) AS geom
                FROM jsonb_array_elements({json}::jsonb) r
            )
            SELECT a.id AS "First", b.id AS "Second"
            FROM routes a JOIN routes b ON a.id < b.id
            WHERE ST_Length(ST_Intersection(a.geom::geography, ST_Buffer(b.geom::geography,{_settings.DuplicateToleranceMeters})))
                / NULLIF(ST_Length(a.geom::geography),0) * 100 >= {_settings.DuplicateOverlapPercent}
            AND ST_Length(ST_Intersection(b.geom::geography, ST_Buffer(a.geom::geography,{_settings.DuplicateToleranceMeters})))
                / NULLIF(ST_Length(b.geom::geography),0) * 100 >= {_settings.DuplicateOverlapPercent}
            """).ToListAsync(cancellationToken);
        var selected = new List<int>();
        for (var index = 0; index < routes.Count && selected.Count < _settings.MaxAlternativeRoutes; index++)
            if (!selected.Any(other => pairs.Any(pair => pair.First == other && pair.Second == index))) selected.Add(index);
        return selected.Select(index => routes[index]).ToList();
    }

    public async Task<IReadOnlyList<RouteThermalSegment>> Analyze(IReadOnlyList<RouteCandidate> routes, CancellationToken cancellationToken)
    {
        var json = Serialize(routes);
        /// :: Clip each directed edge separately to preserve distance when a route revisits the same street.
        var rows = await _context.Database.SqlQuery<SegmentRow>($"""
            WITH routes AS (
                SELECT (r->>'index')::int AS id, ST_SetSRID(ST_GeomFromGeoJSON(r->'geometry'),4326) AS geom
                FROM jsonb_array_elements({json}::jsonb) r
            ), edges AS (
                SELECT r.id, d.path[1] AS edge, d.geom FROM routes r CROSS JOIN LATERAL ST_DumpSegments(r.geom) d
                WHERE NOT ST_Equals(ST_StartPoint(d.geom),ST_EndPoint(d.geom))
            ), regions AS MATERIALIZED (
                SELECT g.*, ST_Area(g."Boundary"::geography) AS area FROM "GeoRegions" g
                WHERE g."Active" AND EXISTS (SELECT 1 FROM routes r WHERE g."Boundary" && r.geom AND ST_Intersects(g."Boundary",r.geom))
            ), intersections AS (
                SELECT e.id, e.edge, e.geom AS original, d.geom AS piece
                FROM edges e JOIN regions g ON g."Boundary" && e.geom AND ST_Intersects(g."Boundary",e.geom)
                CROSS JOIN LATERAL ST_Dump(ST_CollectionExtract(ST_Intersection(e.geom,g."Boundary"),2)) d
            ), fractions AS (
                SELECT id, edge, 0::float8 AS position FROM edges UNION SELECT id, edge, 1::float8 FROM edges
                UNION SELECT id, edge, ST_LineLocatePoint(original,ST_StartPoint(piece)) FROM intersections
                UNION SELECT id, edge, ST_LineLocatePoint(original,ST_EndPoint(piece)) FROM intersections
            ), intervals AS (
                SELECT id, edge, position, lead(position) OVER (PARTITION BY id,edge ORDER BY position) AS ending FROM fractions
            ), pieces AS (
                SELECT i.id,i.edge,i.position,ST_LineSubstring(e.geom,i.position,i.ending) AS geom
                FROM intervals i JOIN edges e USING(id,edge) WHERE i.ending > i.position
            )
            SELECT p.id AS "RouteIndex", p.edge AS "EdgeIndex", p.position AS "Position", ST_AsGeoJSON(p.geom,15) AS "Geometry",
                ST_Length(p.geom::geography) AS "DistanceMeters", g."Id" AS "GeoRegionId", a."Id" AS "AnalysisId",
                a."ExposureIndex", a."ExposureLevel", a."DataQuality", a."ConfidenceLevel", a."DataOrigin", a."AlgorithmVersion", a."AnalyzedAt",
                a."SourceMetadata"::text AS "SourceMetadata"
            FROM pieces p
            LEFT JOIN LATERAL (SELECT r.* FROM regions r WHERE ST_Covers(r."Boundary",ST_LineInterpolatePoint(p.geom,0.5)) ORDER BY r.area,r."Id" LIMIT 1) g ON true
            LEFT JOIN LATERAL (SELECT a.* FROM "RegionThermalAnalyses" a WHERE a."GeoRegionId" = g."Id" AND a."BoundaryVersion" = g."BoundaryVersion" AND a."Active"
                ORDER BY a."AnalyzedAt" DESC,a."Id" DESC LIMIT 1) a
                ON (a."SourceMetadata"->>'referenceRegionId') IS NOT DISTINCT FROM g."ReferenceGeoRegionId"::text
                AND (g."ReferenceGeoRegionId" IS NULL OR EXISTS (SELECT 1 FROM "GeoRegions" ref WHERE ref."Id" = g."ReferenceGeoRegionId" AND ref."Active"
                    AND ref."BoundaryVersion"::text = a."SourceMetadata"->>'referenceBoundaryVersion'))
            ORDER BY p.id,p.edge,p.position
            LIMIT {_settings.MaximumSegments * routes.Count + 1}
            """).ToListAsync(cancellationToken);
        if (rows.Count > _settings.MaximumSegments * routes.Count) throw new InvalidOperationException("Route segmentation exceeds configured limits.");
        return rows.Where(row => row.DistanceMeters > 0).Select(row => new RouteThermalSegment(row.RouteIndex, row.EdgeIndex, row.Position,
            ReadLine(row.Geometry), row.DistanceMeters, row.GeoRegionId, row.AnalysisId, row.ExposureIndex,
            Parse<ExposureLevel>(row.ExposureLevel), Parse<RegionDataQuality>(row.DataQuality), Parse<ConfidenceLevel>(row.ConfidenceLevel),
            row.DataOrigin, row.AlgorithmVersion, row.AnalyzedAt, row.SourceMetadata)).ToList();
    }

    private static T? Parse<T>(string? value) where T : struct, Enum => Enum.TryParse<T>(value, out var parsed) && Enum.IsDefined(parsed) ? parsed : null;

    private static string Serialize(IReadOnlyList<RouteCandidate> routes) => JsonSerializer.Serialize(routes.Select((route, index) => new
    {
        index, geometry = new { type = "LineString", coordinates = route.Geometry.Coordinates.Select(point => new[] { point.X, point.Y }) }
    }));

    private static LineString ReadLine(string json)
    {
        using var document = JsonDocument.Parse(json);
        return new LineString(document.RootElement.GetProperty("coordinates").EnumerateArray().Select(point => new Coordinate(point[0].GetDouble(),point[1].GetDouble())).ToArray()) { SRID = 4326 };
    }

    private sealed class DuplicatePair
    {
        public int First { get; set; }
        public int Second { get; set; }
    }

    private sealed class SegmentRow
    {
        public int RouteIndex { get; set; }
        public int EdgeIndex { get; set; }
        public double Position { get; set; }
        public string Geometry { get; set; } = string.Empty;
        public double DistanceMeters { get; set; }
        public Guid? GeoRegionId { get; set; }
        public Guid? AnalysisId { get; set; }
        public decimal? ExposureIndex { get; set; }
        public string? ExposureLevel { get; set; }
        public string? DataQuality { get; set; }
        public string? ConfidenceLevel { get; set; }
        public string? DataOrigin { get; set; }
        public string? AlgorithmVersion { get; set; }
        public DateTime? AnalyzedAt { get; set; }
        public string? SourceMetadata { get; set; }
    }
}
