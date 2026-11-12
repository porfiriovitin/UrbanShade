using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using UrbanShade.Domain.RegionThermalAnalysis;
using UrbanShade.Domain.ThermalAnalysis;

namespace UrbanShade.Infraestructure.DataAcess;

/// <summary>SQL projection of AnalysisEligibility, shared by operational queries.</summary>
internal static class AnalysisEligibilitySql
{
    public static string Predicate(string alias) => Template.Replace("a.", alias + ".");
    private const string Template = """
        a."Active" AND @enabled AND a."AlgorithmVersion" = @algorithm AND a."AnalyzedAt" <= @now
        AND a."ExposureIndex" BETWEEN 0 AND 100
        AND COALESCE(a."SourceMetadata"->>'schemaVersion', '0') IN ('0','1')
        AND a."SourceMetadata"->'parameters'->'Weights' = @policyWeights
        AND a."SourceMetadata"->'parameters'->'RiskThresholds' = @policyThresholds
        AND a."SourceMetadata"->'scores' ? 'Thermal'
        AND (a."SourceMetadata"->'scores' ? 'Vegetation' OR a."SourceMetadata"->'scores' ? 'SurfaceTemperature')
        AND jsonb_typeof(a."SourceMetadata"->'appliedWeights') = 'object'
        AND a."SourceMetadata"->'appliedWeights' <> '{}'::jsonb
        AND CASE WHEN jsonb_typeof(a."SourceMetadata"->'sensors') = 'array' AND jsonb_array_length(a."SourceMetadata"->'sensors') > 0
            THEN NOT EXISTS (SELECT 1 FROM jsonb_array_elements(a."SourceMetadata"->'sensors') sensor
                WHERE NOT COALESCE((sensor->>'MeasuredAt')::timestamptz <= @now
                    AND (sensor->>'MeasuredAt')::timestamptz + @sensorMinutes * interval '1 minute' > @now, false))
            ELSE COALESCE((a."SourceMetadata"->'weather'->>'ObservedAt')::timestamptz <= @now
                AND (a."SourceMetadata"->'weather'->>'ObservedAt')::timestamptz + @weatherMinutes * interval '1 minute' > @now, false) END
        AND (NOT (a."SourceMetadata"->'scores' ? 'Vegetation') OR COALESCE(
            (a."SourceMetadata"->'vegetation'->>'ObservationEndAt')::timestamptz <= @now
            AND (a."SourceMetadata"->'vegetation'->>'ObservationEndAt')::timestamptz + @vegetationDays * interval '1 day' > @now, false))
        AND (NOT (a."SourceMetadata"->'scores' ? 'SurfaceTemperature') OR COALESCE(
            (a."SourceMetadata"->'surface'->>'ObservationEndAt')::timestamptz <= @now
            AND (a."SourceMetadata"->'referenceSurface'->>'ObservationEndAt')::timestamptz <= @now
            AND (a."SourceMetadata"->'surface'->>'ObservationEndAt')::timestamptz + @surfaceDays * interval '1 day' > @now
            AND (a."SourceMetadata"->'referenceSurface'->>'ObservationEndAt')::timestamptz + @surfaceDays * interval '1 day' > @now, false))
        """;

    public static object[] Parameters(RegionThermalAnalysisSettings settings, ThermalAnalysisSettings thermal, DateTime now) =>
    [
        new NpgsqlParameter("enabled", settings.Enabled), new NpgsqlParameter("algorithm", settings.AlgorithmVersion), new NpgsqlParameter("now", now),
        new NpgsqlParameter("sensorMinutes", Math.Min(settings.SourceValidity.SensorMinutes, thermal.StaleDataThresholdMinutes)),
        new NpgsqlParameter("weatherMinutes", settings.SourceValidity.WeatherMinutes), new NpgsqlParameter("vegetationDays", settings.SourceValidity.VegetationDays),
        new NpgsqlParameter("surfaceDays", settings.SourceValidity.SurfaceTemperatureDays),
        new NpgsqlParameter("policyWeights", NpgsqlDbType.Jsonb) { Value = JsonSerializer.Serialize(settings.Weights) },
        new NpgsqlParameter("policyThresholds", NpgsqlDbType.Jsonb) { Value = JsonSerializer.Serialize(settings.RiskThresholds) }
    ];
}
