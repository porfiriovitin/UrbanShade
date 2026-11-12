namespace UrbanShade.Domain.RegionThermalAnalysis;

public sealed record AnalysisValidity(bool IsCurrent, DateTime? ValidUntil, DateTime? SourceReferenceAt);
public static class AnalysisEligibility
{
    public static AnalysisValidity Evaluate(AnalysisProvenance? provenance, string? algorithm, DateTime? analyzedAt, DateTime now, RegionThermalAnalysisSettings settings, double sensorValidityMinutes = double.MaxValue)
    {
        if (!settings.Enabled || provenance is null || algorithm != settings.AlgorithmVersion || analyzedAt is null || analyzedAt > now || provenance.Parameters is not { Weights: not null, RiskThresholds: not null } saved || !SamePolicy(saved, settings) || !provenance.Scores.ContainsKey("Thermal") || provenance.AppliedWeights.Count == 0) return new(false, null, null);

        var sources = new List<(DateTime Date, TimeSpan Lifetime)>();
        if (provenance.Sensors.Count > 0)
            sources.AddRange(provenance.Sensors.Select(sensor => (sensor.MeasuredAt, TimeSpan.FromMinutes(Math.Min(settings.SourceValidity.SensorMinutes, sensorValidityMinutes)))));
        else if (provenance.Weather is { } weather)
            sources.Add((weather.ObservedAt, TimeSpan.FromMinutes(settings.SourceValidity.WeatherMinutes)));
        else
            return new(false, null, null);

        var environmental = false;
        if (provenance.Scores.ContainsKey("Vegetation"))
        {
            if (provenance.Vegetation is not { } vegetation) return new(false, null, null);

            sources.Add((vegetation.ObservationEndAt, TimeSpan.FromDays(settings.SourceValidity.VegetationDays)));
            environmental = true;
        }

        if (provenance.Scores.ContainsKey("SurfaceTemperature"))
        {
            if (provenance.Surface is not { } surface || provenance.ReferenceSurface is not { } reference) return new(false, null, null);

            sources.Add((surface.ObservationEndAt, TimeSpan.FromDays(settings.SourceValidity.SurfaceTemperatureDays)));
            sources.Add((reference.ObservationEndAt, TimeSpan.FromDays(settings.SourceValidity.SurfaceTemperatureDays)));
            environmental = true;
        }

        if (!environmental || sources.Any(item => item.Date.Kind != DateTimeKind.Utc || item.Date > now || item.Date == DateTime.MinValue)) return new(false, null, null);

        try
        {
            var until = sources.Min(item => item.Date.Add(item.Lifetime));

            return new(until > now, until, sources.Min(item => item.Date));
        }
        catch (ArgumentOutOfRangeException)
        {
            return new(false, null, null);
        }
    }

    private static bool SamePolicy(RegionThermalAnalysisSettings saved, RegionThermalAnalysisSettings current) => saved.Weights.Thermal == current.Weights.Thermal && saved.Weights.SurfaceTemperature == current.Weights.SurfaceTemperature && saved.Weights.Vegetation == current.Weights.Vegetation && saved.RiskThresholds.Moderate == current.RiskThresholds.Moderate && saved.RiskThresholds.High == current.RiskThresholds.High && saved.RiskThresholds.Critical == current.RiskThresholds.Critical;
}
