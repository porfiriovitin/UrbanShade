using UrbanShade.Domain.Geospatial;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using UrbanShade.Domain.InstitutionalDashboard;
using UrbanShade.Domain.RegionThermalAnalysis;
using UrbanShade.Domain.Repositories;
using UrbanShade.Domain.ThermalAnalysis;

namespace UrbanShade.Infraestructure.DataAcess.Repositories;

internal sealed class InstitutionalDashboardRepository : IInstitutionalDashboardRepository
{
    private readonly UrbanShadeDbContext _context;
    private readonly RegionThermalAnalysisSettings _regional;
    private readonly ThermalAnalysisSettings _thermal;
    private readonly GeoAnalysisSettings _geo;
    private readonly InstitutionalDashboardSettings _settings;
    private readonly InstitutionalDashboardCache _cache;
    private readonly TimeProvider _time;
    private readonly ILogger<InstitutionalDashboardRepository> _logger;

    public InstitutionalDashboardRepository(UrbanShadeDbContext context, RegionThermalAnalysisSettings regional,
        ThermalAnalysisSettings thermal, GeoAnalysisSettings geo, InstitutionalDashboardSettings settings, InstitutionalDashboardCache cache,
        TimeProvider time, ILogger<InstitutionalDashboardRepository> logger)
    {
        _context = context;
        _regional = regional;
        _thermal = thermal;
        _geo = geo;
        _settings = settings;
        _cache = cache;
        _time = time;
        _logger = logger;
    }

    /// :: Every query checks live membership in SQL, including requests served from cache.
    private const string Scope = """
        WITH scoped_regions AS (
            SELECT r.* FROM "GeoRegions" r
            JOIN "OrganizationRegions" link ON link."GeoRegionId" = r."Id" AND link."Active"
            JOIN "Organizations" o ON o."Id" = link."OrganizationId"
            WHERE link."OrganizationId" = @organization AND o."Active" AND r."Active"
            AND EXISTS (SELECT 1 FROM "Users" u WHERE u."Id" = @actor AND u."Active" AND NOT u."MustChangePassword"
                AND (u."Role" = 'PlatformAdministrator' OR (u."Role" = 'Manager' AND EXISTS (
                    SELECT 1 FROM "OrganizationMembers" m WHERE m."UserId" = u."Id" AND m."OrganizationId" = o."Id" AND m."Active"))))
        ), selected_regions AS (
            SELECT * FROM scoped_regions r WHERE (@region IS NULL OR r."Id" = @region)
            AND (cardinality(@ids) = 0 OR r."Id" = ANY(@ids))
            AND (@west IS NULL OR r."Boundary" && ST_MakeEnvelope(@west, @south, @east, @north, 4326))
        )
        """;

    private static readonly string Regions = """
        , region_sources AS (
            SELECT r.*, a."Id" AS analysis_id, a."ExposureIndex", a."ExposureLevel", a."DataQuality", a."DataOrigin",
                a."AlgorithmVersion", a."AnalyzedAt", a."AverageHeatIndex", a."SurfaceTemperature", a."SurfaceTemperatureAnomaly",
                a."MeanNdvi", a."VegetationCoveragePercent", a."IsHeatIslandCandidate", a."SourceMetadata",
                LEAST(
                    COALESCE((SELECT MIN((s->>'MeasuredAt')::timestamptz) + (@sensorMinutes * interval '1 minute')
                        FROM jsonb_array_elements(COALESCE(a."SourceMetadata"->'sensors', '[]'::jsonb)) s),
                        (a."SourceMetadata"->'weather'->>'ObservedAt')::timestamptz + make_interval(mins => @weatherMinutes), '-infinity'::timestamptz),
                    CASE WHEN a."SourceMetadata"->'scores' ? 'Vegetation' THEN COALESCE(
                        (a."SourceMetadata"->'vegetation'->>'ObservationEndAt')::timestamptz + make_interval(days => @vegetationDays), '-infinity'::timestamptz) ELSE 'infinity'::timestamptz END,
                    CASE WHEN a."SourceMetadata"->'scores' ? 'SurfaceTemperature' THEN LEAST(
                        COALESCE((a."SourceMetadata"->'surface'->>'ObservationEndAt')::timestamptz + make_interval(days => @surfaceDays), '-infinity'::timestamptz),
                        COALESCE((a."SourceMetadata"->'referenceSurface'->>'ObservationEndAt')::timestamptz + make_interval(days => @surfaceDays), '-infinity'::timestamptz)) ELSE 'infinity'::timestamptz END
                ) AS expires
            FROM selected_regions r
            LEFT JOIN LATERAL (SELECT * FROM "RegionThermalAnalyses" a WHERE a."GeoRegionId" = r."Id" AND a."Active"
                AND a."BoundaryVersion" = r."BoundaryVersion" AND a."AnalyzedAt" <= @now
                AND (@start IS NULL OR a."AnalyzedAt" >= @start) AND (@end IS NULL OR a."AnalyzedAt" <= @end)
                ORDER BY a."AnalyzedAt" DESC, a."Id" DESC LIMIT 1) a ON true
        ), region_current AS (
            SELECT s.*, COALESCE((analysis_id IS NOT NULL AND "ExposureIndex" IS NOT NULL AND "AlgorithmVersion" = @algorithm
                AND expires > @now AND "DataQuality" = 'COMPLETE'
                AND (__ELIGIBILITY__)
                AND (NOT ("SourceMetadata"->'scores' ? 'SurfaceTemperature') OR EXISTS (
                    SELECT 1 FROM "GeoRegions" ref WHERE ref."Id" = s."ReferenceGeoRegionId" AND ref."Active"
                    AND ref."Id"::text = s."SourceMetadata"->>'referenceRegionId'
                    AND ref."BoundaryVersion" = (s."SourceMetadata"->>'referenceBoundaryVersion')::int))), false) AS current
            FROM region_sources s
        )
        """.Replace("__ELIGIBILITY__", AnalysisEligibilitySql.Predicate("s"));

    private const string Sensors = """
        , selected_sensors AS (
            SELECT s.* FROM "Sensors" s WHERE s."OrganizationId" = @organization AND s."DeletedAt" IS NULL
            AND EXISTS (SELECT 1 FROM selected_regions r WHERE ST_Covers(r."Boundary", ST_SetSRID(ST_MakePoint(s."Longitude", s."Latitude"),4326)))
        ), sensor_data AS (
            SELECT s.*, reading."MeasuredAt", reading."Temperature", a."HeatIndex",
                reading."MeasuredAt" + (@sensorMinutes * interval '1 minute') AS expires
            FROM selected_sensors s
            LEFT JOIN LATERAL (SELECT * FROM "SensorReadings" reading WHERE reading."SensorId" = s."Id" AND reading."Active"
                AND reading."MeasuredAt" <= @now AND reading."Temperature" >= -273.15 AND reading."Humidity" BETWEEN 0 AND 100
                AND (@start IS NULL OR reading."MeasuredAt" >= @start) AND (@end IS NULL OR reading."MeasuredAt" <= @end)
                ORDER BY reading."MeasuredAt" DESC, reading."Id" DESC LIMIT 1) reading ON true
            LEFT JOIN "ThermalAnalyses" a ON a."SensorReadingId" = reading."Id" AND a."Active" AND a."AlgorithmVersion" = @thermalAlgorithm
        )
        """;

    private object[] Parameters(InstitutionalQuery query) =>
    [
        new NpgsqlParameter("enabled", _regional.Enabled),
        new NpgsqlParameter("policyWeights", NpgsqlDbType.Jsonb) { Value = JsonSerializer.Serialize(_regional.Weights) },
        new NpgsqlParameter("policyThresholds", NpgsqlDbType.Jsonb) { Value = JsonSerializer.Serialize(_regional.RiskThresholds) },
        new NpgsqlParameter("organization", query.OrganizationId), new NpgsqlParameter("actor", query.ActorId),
        new NpgsqlParameter("region", NpgsqlDbType.Uuid) { Value = (object?)query.RegionId ?? DBNull.Value },
        new NpgsqlParameter("ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = query.RegionIds },
        new NpgsqlParameter("start", NpgsqlDbType.TimestampTz) { Value = (object?)query.StartDate ?? DBNull.Value },
        new NpgsqlParameter("end", NpgsqlDbType.TimestampTz) { Value = (object?)query.EndDate ?? DBNull.Value },
        new NpgsqlParameter("now", _time.GetUtcNow().UtcDateTime),
        new NpgsqlParameter("algorithm", _regional.AlgorithmVersion), new NpgsqlParameter("thermalAlgorithm", _thermal.AlgorithmVersion),
        new NpgsqlParameter("sensorMinutes", Math.Min(_regional.SourceValidity.SensorMinutes, _thermal.StaleDataThresholdMinutes)),
        new NpgsqlParameter("weatherMinutes", _regional.SourceValidity.WeatherMinutes),
        new NpgsqlParameter("vegetationDays", _regional.SourceValidity.VegetationDays), new NpgsqlParameter("surfaceDays", _regional.SourceValidity.SurfaceTemperatureDays),
        new NpgsqlParameter("west", NpgsqlDbType.Double) { Value = (object?)query.Bounds?.ElementAt(0) ?? DBNull.Value },
        new NpgsqlParameter("south", NpgsqlDbType.Double) { Value = (object?)query.Bounds?.ElementAt(1) ?? DBNull.Value },
        new NpgsqlParameter("east", NpgsqlDbType.Double) { Value = (object?)query.Bounds?.ElementAt(2) ?? DBNull.Value },
        new NpgsqlParameter("north", NpgsqlDbType.Double) { Value = (object?)query.Bounds?.ElementAt(3) ?? DBNull.Value },
        new NpgsqlParameter("level", NpgsqlDbType.Text) { Value = (object?)query.ExposureLevel ?? DBNull.Value },
        new NpgsqlParameter("minimum", NpgsqlDbType.Numeric) { Value = (object?)query.MinExposureIndex ?? DBNull.Value },
        new NpgsqlParameter("take", query.PageSize), new NpgsqlParameter("skip", (query.Page - 1) * query.PageSize),
        new NpgsqlParameter("limit", query.Limit), new NpgsqlParameter("zone", query.TimeZoneId),
        new NpgsqlParameter("interval", query.Interval.ToLowerInvariant()),
        new NpgsqlParameter("satelliteType", query.Metric == "VEGETATION_COVERAGE" ? "Vegetation" : "SurfaceTemperature"),
        new NpgsqlParameter("maxExport", _settings.MaxExportRows + 1), new NpgsqlParameter("maxFeatures", _settings.MaxMapFeatures + 1),
        new NpgsqlParameter("lowVegetation", _settings.LowVegetationCoveragePercent),
        new NpgsqlParameter("minimumPixels", (decimal)_geo.MinimumValidPixelPercent),
        new NpgsqlParameter("geoAlgorithm", _geo.AlgorithmVersion)
    ];

    private Task<List<T>> Query<T>(InstitutionalQuery query, string sql, CancellationToken cancellationToken)
    {
        /// :: SQL fragments are fixed in this repository; all caller values use named parameters.
        var commandText = string.Concat(Scope, sql).Replace("{", "{{").Replace("}", "}}");
        return _context.Database.SqlQueryRaw<T>(commandText, Parameters(query)).ToListAsync(cancellationToken);
    }

    private async Task<string> GetScopeVersion(InstitutionalQuery query, CancellationToken cancellationToken)
    {
        var values = await Query<string>(query, """
            SELECT COALESCE(string_agg(r."Id"::text || ':' || r."BoundaryVersion"::text || ':' || COALESCE(ref."Id"::text,'') || ':' || COALESCE(ref."BoundaryVersion"::text,'') || ':' || COALESCE(ref."Active"::text,''), ',' ORDER BY r."Id"), '') AS "Value" FROM selected_regions r LEFT JOIN "GeoRegions" ref ON ref."Id" = r."ReferenceGeoRegionId"
            """, cancellationToken);
        return values.Single();
    }

    private async Task<T> Cached<T>(InstitutionalQuery query, string operation, int minutes, Func<Task<T>> load,
        Func<T, DateTime?> validUntil, CancellationToken cancellationToken)
    {
        var scope = await GetScopeVersion(query, cancellationToken);
        var key = $"institutional:{operation}:{JsonSerializer.Serialize(query)}:{scope}:{JsonSerializer.Serialize(_regional)}:{JsonSerializer.Serialize(_geo)}:{_thermal.AlgorithmVersion}:{_thermal.StaleDataThresholdMinutes}";
        if (_cache.TryGetValue<T>(key, out var cached) && (validUntil(cached!) is not { } deadlineAtRead || deadlineAtRead > _time.GetUtcNow().UtcDateTime))
        {
            _logger.LogInformation("Institutional cache hit for {Operation}, organization {OrganizationId}", operation, query.OrganizationId);
            return cached!;
        }
        var result = await load();
        var expires = _time.GetUtcNow().AddMinutes(minutes);
        if (validUntil(result) is { } deadline && deadline > _time.GetUtcNow().UtcDateTime && deadline < expires.UtcDateTime)
            expires = new DateTimeOffset(deadline);
        _cache.Set(key, result, expires);
        return result;
    }

    public Task<InstitutionalOverviewData> GetOverview(InstitutionalQuery query, CancellationToken cancellationToken) =>
        Cached(query, "overview", Math.Min(_settings.OverviewCacheMinutes, _settings.DistributionCacheMinutes), async () => (await Query<InstitutionalOverviewData>(query, Regions + """
            SELECT count(*)::int AS "TotalRegions", count(*) FILTER (WHERE current)::int AS "RegionsWithValidAnalysis",
                count(*) FILTER (WHERE analysis_id IS NOT NULL AND (expires <= @now OR "AlgorithmVersion" <> @algorithm))::int AS "StaleRegions",
                count(*) FILTER (WHERE current AND "ExposureLevel" = 'LOW')::int AS "Low",
                count(*) FILTER (WHERE current AND "ExposureLevel" = 'MODERATE')::int AS "Moderate",
                count(*) FILTER (WHERE current AND "ExposureLevel" = 'HIGH')::int AS "High",
                count(*) FILTER (WHERE current AND "ExposureLevel" = 'CRITICAL')::int AS "Critical",
                count(*) FILTER (WHERE NOT current)::int AS "InsufficientData",
                count(*) FILTER (WHERE current AND "IsHeatIslandCandidate")::int AS "HeatIslandCandidates",
                avg("ExposureIndex") FILTER (WHERE current) AS "AverageExposureIndex",
                count("ExposureIndex") FILTER (WHERE current)::int AS "ExposureSampleCount",
                COALESCE(bool_or("DataOrigin" = 'SIMULATED_SENSORS') FILTER (WHERE current), false) AS "HasSimulatedData",
                min(expires) FILTER (WHERE current) AS "ValidUntil" FROM region_current
            """, cancellationToken)).Single(), data => data.ValidUntil, cancellationToken);

    public Task<InstitutionalSensorData> GetSensorsOverview(InstitutionalQuery query, CancellationToken cancellationToken) =>
        Cached(query, "sensors", _settings.OverviewCacheMinutes, async () => (await Query<InstitutionalSensorData>(query, Sensors + """
            SELECT count(*)::int AS "TotalSensors", count(*) FILTER (WHERE "Status" = 'ACTIVE')::int AS "ActiveSensors",
                count(*) FILTER (WHERE "Status" = 'INACTIVE')::int AS "InactiveSensors",
                count(*) FILTER (WHERE "Status" = 'MAINTENANCE')::int AS "MaintenanceSensors",
                count(*) FILTER (WHERE expires > @now)::int AS "SensorsWithCurrentData",
                count(*) FILTER (WHERE expires <= @now)::int AS "SensorsWithStaleData",
                count(*) FILTER (WHERE "MeasuredAt" IS NULL)::int AS "SensorsWithoutReadings",
                avg("Temperature") FILTER (WHERE expires > @now AND "Status" = 'ACTIVE' AND "Active") AS "AverageTemperature",
                count("Temperature") FILTER (WHERE expires > @now AND "Status" = 'ACTIVE' AND "Active")::int AS "TemperatureSampleCount",
                avg("HeatIndex") FILTER (WHERE expires > @now AND "Status" = 'ACTIVE' AND "Active") AS "AverageHeatIndex",
                count("HeatIndex") FILTER (WHERE expires > @now AND "Status" = 'ACTIVE' AND "Active")::int AS "HeatIndexSampleCount",
                max("MeasuredAt") AS "LastSimulatedCommunication", min(expires) FILTER (WHERE expires > @now) AS "ValidUntil"
            FROM sensor_data
            """, cancellationToken)).Single(), data => data.ValidUntil, cancellationToken);

    private const string RegionProjection = """
        SELECT "Id" AS "RegionId", "Name", "BoundaryVersion", ST_AsGeoJSON("Boundary") AS "Geometry", "ExposureIndex",
            COALESCE("ExposureLevel", 'INSUFFICIENT_DATA') AS "ExposureLevel", COALESCE("DataQuality", 'INSUFFICIENT_DATA') AS "DataQuality",
            COALESCE("DataOrigin", 'UNAVAILABLE') AS "DataOrigin", "AlgorithmVersion", "AnalyzedAt",
            CASE WHEN expires > '-infinity'::timestamptz AND expires < 'infinity'::timestamptz THEN expires END AS "ValidUntil",
            COALESCE(current, false) AS "IsCurrent",
            (analysis_id IS NOT NULL AND (expires <= @now OR "AlgorithmVersion" <> @algorithm)) AS "IsStale", "AverageHeatIndex", "SurfaceTemperature", "SurfaceTemperatureAnomaly", "MeanNdvi", "VegetationCoveragePercent",
            "IsHeatIslandCandidate", COALESCE("SourceMetadata", '{}'::jsonb)::text AS "SourceMetadata",
            (SELECT count(*)::int FROM "Sensors" s WHERE s."OrganizationId" = @organization AND s."DeletedAt" IS NULL
                AND ST_Covers("Boundary", ST_SetSRID(ST_MakePoint(s."Longitude", s."Latitude"),4326))) AS "SensorCount"
        FROM region_current WHERE (@level IS NULL OR (@level = 'INSUFFICIENT_DATA' AND NOT current) OR (current AND "ExposureLevel" = @level))
            AND (@minimum IS NULL OR (current AND "ExposureIndex" >= @minimum))
        """;

    public Task<(List<InstitutionalRegionData> Items, int Total)> GetRegions(InstitutionalQuery query, bool ranking, CancellationToken cancellationToken)
    {
        if (!ranking) return GetRegionRows(query, false, cancellationToken);
        return Cached(query, "ranking", _settings.RankingCacheMinutes, () => GetRegionRows(query, true, cancellationToken),
            data => data.Items.Select(item => item.ValidUntil).Min(), cancellationToken);
    }

    private async Task<(List<InstitutionalRegionData> Items, int Total)> GetRegionRows(InstitutionalQuery query, bool ranking, CancellationToken cancellationToken)
    {
        var filter = ranking ? " AND current" : "";
        var total = (await Query<int>(query, Regions + "SELECT count(*)::int AS \"Value\" FROM (" + RegionProjection + filter + ") rows", cancellationToken)).Single();
        var order = ranking ? (query.Sort == "ASC" ? " ORDER BY \"ExposureIndex\", \"Id\"" : " ORDER BY \"ExposureIndex\" DESC, \"Id\"") : " ORDER BY \"Name\", \"Id\"";
        var items = await Query<InstitutionalRegionData>(query, Regions + RegionProjection + filter + order + (ranking ? " LIMIT @limit" : " LIMIT @take OFFSET @skip"), cancellationToken);
        return (items, total);
    }

    public Task<InstitutionalEnvironmentData> GetEnvironment(InstitutionalQuery query, CancellationToken cancellationToken) =>
        Cached(query, "environment", _settings.EnvironmentCacheMinutes, async () => (await Query<InstitutionalEnvironmentData>(query, Regions + """
            , satellite_candidates AS (
                SELECT r."Id" AS region_id, r."Boundary", s.*,
                    row_number() OVER (PARTITION BY r."Id", s."AnalysisType" ORDER BY s."ObservationEndAt" DESC, s."ProcessedAt" DESC, s."Id") AS rn
                FROM selected_regions r JOIN "SatelliteAnalyses" s ON s."GeoRegionId" = r."Id" AND s."BoundaryVersion" = r."BoundaryVersion"
                WHERE s."Active" AND s."ValidPixelPercent" BETWEEN @minimumPixels AND 100 AND s."MeanValue" IS NOT NULL AND s."AlgorithmVersion" = @geoAlgorithm
                    AND s."ObservationEndAt" <= @now AND s."ObservationEndAt" >= @now - make_interval(days => CASE WHEN s."AnalysisType" = 'Vegetation' THEN @vegetationDays ELSE @surfaceDays END)
                    AND (@start IS NULL OR s."ObservationEndAt" >= @start) AND (@end IS NULL OR s."ObservationEndAt" <= @end)
                    AND ((s."AnalysisType" = 'Vegetation' AND s."MeanValue" BETWEEN -1 AND 1 AND s."VegetationCoveragePercent" BETWEEN 0 AND 100)
                        OR s."AnalysisType" = 'SurfaceTemperature')
            ), satellite_latest AS (SELECT * FROM satellite_candidates WHERE rn = 1), pieces AS (
                SELECT s.*, ST_Area(ST_Transform(ST_Difference(s."Boundary", COALESCE((SELECT ST_UnaryUnion(ST_Collect(other."Boundary")) FROM satellite_latest other
                    WHERE other."AnalysisType" = s."AnalysisType" AND (other."ObservationEndAt" > s."ObservationEndAt"
                        OR (other."ObservationEndAt" = s."ObservationEndAt" AND other.region_id < s.region_id)) AND other."Boundary" && s."Boundary"),
                        ST_SetSRID('POLYGON EMPTY'::geometry,4326))),6933)) AS area
                FROM satellite_latest s
            ), anomaly_pieces AS (
                SELECT r."SurfaceTemperatureAnomaly" AS value,
                    ST_Area(ST_Transform(ST_Difference(r."Boundary", COALESCE((SELECT ST_UnaryUnion(ST_Collect(other."Boundary"))
                        FROM region_current other WHERE other.current AND other."SurfaceTemperatureAnomaly" IS NOT NULL
                        AND ((other."SourceMetadata"->'surface'->>'ObservationEndAt')::timestamptz > (r."SourceMetadata"->'surface'->>'ObservationEndAt')::timestamptz
                            OR ((other."SourceMetadata"->'surface'->>'ObservationEndAt')::timestamptz = (r."SourceMetadata"->'surface'->>'ObservationEndAt')::timestamptz AND other."Id" < r."Id"))
                        AND other."Boundary" && r."Boundary"), ST_SetSRID('POLYGON EMPTY'::geometry,4326))),6933)) AS area
                FROM region_current r WHERE r.current AND r."SurfaceTemperatureAnomaly" IS NOT NULL
            )
            SELECT (SELECT ST_Area(ST_Transform(ST_UnaryUnion(ST_Collect("Boundary")),6933)) FROM selected_regions) AS "TotalAreaSquareMeters",
                sum(area) FILTER (WHERE "AnalysisType" = 'Vegetation' AND area > 0) AS "VegetationSampleAreaSquareMeters",
                sum(area) FILTER (WHERE "AnalysisType" = 'SurfaceTemperature' AND area > 0) AS "SurfaceSampleAreaSquareMeters",
                sum("VegetationCoveragePercent"::double precision * area) FILTER (WHERE "AnalysisType" = 'Vegetation') /
                    NULLIF(sum(area) FILTER (WHERE "AnalysisType" = 'Vegetation'),0) AS "AverageVegetationCoveragePercent",
                sum("MeanValue"::double precision * area) FILTER (WHERE "AnalysisType" = 'Vegetation') /
                    NULLIF(sum(area) FILTER (WHERE "AnalysisType" = 'Vegetation'),0) AS "AverageNdvi",
                sum("MeanValue"::double precision * area) FILTER (WHERE "AnalysisType" = 'SurfaceTemperature') /
                    NULLIF(sum(area) FILTER (WHERE "AnalysisType" = 'SurfaceTemperature'),0) AS "AverageSurfaceTemperature",
                max("MaxValue") FILTER (WHERE "AnalysisType" = 'SurfaceTemperature' AND area > 0) AS "MaximumSurfaceTemperature",
                (SELECT sum(value::double precision * area) / NULLIF(sum(area),0) FROM anomaly_pieces WHERE area > 0) AS "AverageSurfaceTemperatureAnomaly",
                count(*) FILTER (WHERE "AnalysisType" = 'Vegetation' AND area > 0)::int AS "VegetationSampleCount",
                count(*) FILTER (WHERE "AnalysisType" = 'SurfaceTemperature' AND area > 0)::int AS "SurfaceSampleCount",
                (SELECT count(*)::int FROM anomaly_pieces WHERE area > 0) AS "AnomalySampleCount",
                count(*) FILTER (WHERE "AnalysisType" = 'Vegetation' AND "VegetationCoveragePercent" < @lowVegetation)::int AS "LowVegetationRegions",
                (SELECT count(*)::int FROM region_current WHERE current AND "IsHeatIslandCandidate") AS "HeatIslandCandidates",
                min("ObservationEndAt") FILTER (WHERE area > 0) AS "FirstObservationAt", max("ObservationEndAt") FILTER (WHERE area > 0) AS "LastObservationAt",
                LEAST(min("ObservationEndAt" + make_interval(days => CASE WHEN "AnalysisType" = 'Vegetation' THEN @vegetationDays ELSE @surfaceDays END)),
                    (SELECT min(expires) FROM region_current WHERE current)) AS "ValidUntil"
            FROM pieces
            """, cancellationToken)).Single(), data => data.ValidUntil, cancellationToken);

    public Task<List<InstitutionalTrendData>> GetTrends(InstitutionalQuery query, CancellationToken cancellationToken) =>
        Cached(query, "trends", _settings.HistoryCacheMinutes, () => Query<InstitutionalTrendData>(query, HistoricalSource(query) + """
            SELECT date_trunc(@interval, observed AT TIME ZONE @zone) AS "Date", avg(value) AS "Value", count(value)::int AS "SampleCount",
                version AS "AlgorithmVersion", composition AS "Composition", origin AS "DataOrigin", quality AS "DataQuality"
            FROM observations WHERE observed >= @start AND observed <= @end AND observed <= @now
            GROUP BY date_trunc(@interval, observed AT TIME ZONE @zone), version, composition, origin, quality
            ORDER BY "Date", version, composition, origin, quality
            """, cancellationToken), _ => null, cancellationToken);

    private static string HistoricalSource(InstitutionalQuery query)
    {
        /// :: Only fixed SQL expressions are selected; metric names never become SQL supplied by the caller.
        if (query.Metric is "SURFACE_TEMPERATURE" or "VEGETATION_COVERAGE")
        {
            var vegetation = query.Metric == "VEGETATION_COVERAGE";
            var value = vegetation ? "s.\"VegetationCoveragePercent\"" : "s.\"MeanValue\"";
            var valid = vegetation ? "s.\"VegetationCoveragePercent\" BETWEEN 0 AND 100 AND s.\"MeanValue\" BETWEEN -1 AND 1" : "s.\"MeanValue\" >= -273.15";
            var eligible = "s.\"ValidPixelPercent\" BETWEEN @minimumPixels AND 100 AND " + valid;
            return """
                , satellite_history AS (
                    SELECT DISTINCT ON (s."GeoRegionId", s."BoundaryVersion", s."DataSource", s."DatasetId", s."ImageId", s."ObservationEndAt", s."AlgorithmVersion") s.*
                    FROM "SatelliteAnalyses" s JOIN selected_regions r ON r."Id" = s."GeoRegionId"
                    WHERE s."Active" AND s."AnalysisType" = @satelliteType AND s."ObservationEndAt" >= @start AND s."ObservationEndAt" <= @end
                    ORDER BY s."GeoRegionId", s."BoundaryVersion", s."DataSource", s."DatasetId", s."ImageId", s."ObservationEndAt", s."AlgorithmVersion", s."ProcessedAt" DESC, s."Id"
                ), observations AS (SELECT s."ObservationEndAt" AS observed,
                """ + "CASE WHEN " + eligible + " THEN " + value + " END" + """
                 AS value, s."AlgorithmVersion" AS version, s."BoundaryVersion"::text || ':' || s."DatasetId" AS composition, s."DataSource" AS origin,
                """ + "CASE WHEN " + eligible + " THEN 'COMPLETE' ELSE 'INSUFFICIENT_DATA' END AS quality " + """
                FROM satellite_history s)
                """;
        }
        if (query.Metric == "EXPOSURE_INDEX") return """
            , observations AS (SELECT a."AnalyzedAt" AS observed, a."ExposureIndex" AS value, a."AlgorithmVersion" AS version,
                a."BoundaryVersion"::text || ':' || COALESCE((a."SourceMetadata"->'appliedWeights')::text,'{}') AS composition,
                a."DataOrigin" AS origin, a."DataQuality" AS quality
                FROM "RegionThermalAnalyses" a JOIN selected_regions r ON r."Id" = a."GeoRegionId" WHERE a."Active")
            """;
        var temperature = query.Metric == "TEMPERATURE";
        return """
            , observations AS (SELECT reading."MeasuredAt" AS observed,
            """ + (temperature ? "reading.\"Temperature\"" : "a.\"HeatIndex\"") + """
             AS value,
            """ + (temperature ? "'SENSOR_READING'" : "a.\"AlgorithmVersion\"") + """
             AS version, 'SENSORS' AS composition, 'SIMULATED_SENSORS' AS origin, 'COMPLETE' AS quality
            FROM "SensorReadings" reading JOIN "Sensors" sensor ON sensor."Id" = reading."SensorId"
            """ + (temperature ? "" : " JOIN \"ThermalAnalyses\" a ON a.\"SensorReadingId\" = reading.\"Id\" AND a.\"Active\" ") + """
            WHERE sensor."OrganizationId" = @organization AND sensor."DeletedAt" IS NULL AND reading."Active"
                AND reading."Temperature" >= -273.15 AND reading."Humidity" BETWEEN 0 AND 100
                AND EXISTS (SELECT 1 FROM selected_regions r WHERE ST_Covers(r."Boundary", ST_SetSRID(ST_MakePoint(sensor."Longitude",sensor."Latitude"),4326))))
            """;
    }

    public Task<List<InstitutionalFeatureData>> GetMap(InstitutionalQuery query, CancellationToken cancellationToken)
    {
        var sensors = query.IncludeSensors ? """
            UNION ALL SELECT s."Id", 'SENSOR', ST_AsGeoJSON(ST_SetSRID(ST_MakePoint(s."Longitude",s."Latitude"),4326)),
                jsonb_build_object('name',s."Name",'status',s."Status",'dataOrigin','SIMULATED_SENSORS')::text, 1
                FROM "Sensors" s WHERE s."OrganizationId" = @organization AND s."DeletedAt" IS NULL
                AND (@west IS NULL OR ST_MakeEnvelope(@west,@south,@east,@north,4326) && ST_SetSRID(ST_MakePoint(s."Longitude",s."Latitude"),4326))
                AND EXISTS (SELECT 1 FROM region_current r WHERE ST_Covers(r."Boundary",ST_SetSRID(ST_MakePoint(s."Longitude",s."Latitude"),4326))
                    AND (@level IS NULL OR (@level = 'INSUFFICIENT_DATA' AND NOT r.current) OR (r.current AND r."ExposureLevel" = @level)))
            """ : "";
        var green = query.IncludeGreenAreas ? """
            UNION ALL SELECT g."Id", 'GREEN_AREA', ST_AsGeoJSON(ST_Intersection(g."Geometry",authorized.boundary)),
                jsonb_build_object('name',g."Name",'source',g."Source",'referenceDate',g."ReferenceDate")::text,
                ST_NPoints(ST_Intersection(g."Geometry",authorized.boundary))
                FROM "GreenAreas" g CROSS JOIN LATERAL (SELECT ST_UnaryUnion(ST_Collect(r."Boundary")) AS boundary
                    FROM region_current r WHERE (@level IS NULL OR (@level = 'INSUFFICIENT_DATA' AND NOT r.current) OR (r.current AND r."ExposureLevel" = @level))) authorized
                WHERE g."Active" AND ST_Intersects(g."Geometry",authorized.boundary)
                AND (EXISTS (SELECT 1 FROM region_current r JOIN "SatelliteAnalyses" satellite ON satellite."GeoRegionId" = r."Id"
                        AND satellite."BoundaryVersion" = r."BoundaryVersion" AND satellite."Active"
                        WHERE g."GeoRegionId" = r."Id" AND g."SatelliteAnalysisId" = satellite."Id")
                    OR EXISTS (SELECT 1 FROM "GreenAreaRegions" link JOIN region_current r ON r."Id" = link."GeoRegionId"
                        AND r."BoundaryVersion" = link."BoundaryVersion" WHERE link."GreenAreaId" = g."Id"))
                AND (@west IS NULL OR g."Geometry" && ST_MakeEnvelope(@west,@south,@east,@north,4326))
            """ : "";
        return Query<InstitutionalFeatureData>(query, Regions + """
            SELECT * FROM (
                SELECT "Id" AS "Id", 'REGION' AS "Kind", ST_AsGeoJSON("Boundary") AS "Geometry",
                    jsonb_build_object('name',"Name",'exposureIndex',CASE WHEN current THEN "ExposureIndex" END,
                        'exposureLevel',CASE WHEN current THEN "ExposureLevel" ELSE 'INSUFFICIENT_DATA' END,
                        'dataQuality',"DataQuality",'isStale',analysis_id IS NOT NULL AND NOT current,'dataOrigin',"DataOrigin")::text AS "Properties",
                    ST_NPoints("Boundary") AS "CoordinateCount"
                FROM region_current WHERE (@level IS NULL OR (@level = 'INSUFFICIENT_DATA' AND NOT current) OR (current AND "ExposureLevel" = @level))
            """ + sensors + green + " ) features ORDER BY \"Kind\", \"Id\" LIMIT @maxFeatures", cancellationToken);
    }

    public Task<List<InstitutionalReportData>> GetReport(InstitutionalQuery query, CancellationToken cancellationToken) =>
        Query<InstitutionalReportData>(query, """
            SELECT a."GeoRegionId" AS "RegionId", r."Name" AS "RegionName", a."AnalyzedAt", a."ExposureIndex", a."ExposureLevel",
                a."AverageHeatIndex" AS "HeatIndex", a."SurfaceTemperature", a."VegetationCoveragePercent", a."AlgorithmVersion", a."BoundaryVersion",
                a."DataQuality", a."DataOrigin", a."SourceMetadata"::text AS "SourceMetadata"
            FROM selected_regions r JOIN "RegionThermalAnalyses" a ON a."GeoRegionId" = r."Id"
            WHERE a."Active" AND a."AnalyzedAt" >= @start AND a."AnalyzedAt" <= @end
            ORDER BY a."AnalyzedAt", r."Id", a."Id" LIMIT @maxExport
            """, cancellationToken);
}
